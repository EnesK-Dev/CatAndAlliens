using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class player : MonoBehaviour
{
    [Header("Hareket Ayarlari")]
    [SerializeField] private float moveSpeed; 

    [Header("Giris Bileseni")]
    [SerializeField] private FixedJoystick joystick; 

    [Header("Vampire Hunter Saldiri Ayarlari")]
    [SerializeField] private GameObject attackPointObject; 
    [SerializeField] private float attackOffset = 1f; 
    [SerializeField] private float attackDuration = 0.15f;
    [SerializeField] private float attackCooldown = 0.5f;
    [SerializeField] private float minAttackCooldown = 0.1f; // Saldiri hizi upgrade'inin inebilecegi taban — 0'a inmesin
    [SerializeField] private float autoAttackRange = 2f;
    [SerializeField] private float playerDamage = 25f; // Karakterin vurus hasari
    [SerializeField] private float attackRadius = 0.5f; // Hasar alaninin yaricapi
    [Tooltip("Vurulunca dusmani oyuncudan uzaga HAFIF geri itme mesafesi (0 = kapali).")]
    [SerializeField] private float attackKnockback = 0.25f;
    [SerializeField] private float attackVisualAngleOffset = 0f; // Pence PNG'sinin varsayilan yonune gore duzeltme (derece)
    [SerializeField] private bool rotateAttackVisual = true; // Pence dusmana dogru donsun mu? Test icin kapatilabilir
    [SerializeField] private LayerMask enemyLayers; // Dusmanlarin bulundugu Layer

    [Header("Kamera Sallanti Ayarlari")]
    [SerializeField] private CameraShake cameraShake;
    [SerializeField] private float shakeDuration;
    [SerializeField] private float shakeMagnitude;

    [Tooltip("Hasar alinca uygulanan sarsinti — vurus sarsintisindan belirgin sekilde guclu olmali.")]
    [SerializeField] private float hurtShakeDuration = 0.35f;
    [SerializeField] private float hurtShakeMagnitude = 0.45f;

    [Header("Dash Ayarlari")]
    [SerializeField] private float dashSpeed = 15f;
    [SerializeField] private float dashDuration = 0.15f;
    [SerializeField] private float dashCooldown = 1.5f;
    [Tooltip("Dash sirasinda dusmanlari itme yaricapi (kucuk tut — sadece degenler).")]
    [SerializeField] private float dashPushRadius = 0.55f;
    [Tooltip("Dash sirasinda her kare uygulanan KUCUK itme mesafesi. Amac: kalabalıktan cikis, uzaga firlatma DEGIL.")]
    [SerializeField] private float dashPushStep = 0.12f;

    [Tooltip("Dash ile itilen dusman KISA sure yerinde dursun (sonra yurur) — 'duvari ittirme' hissi. Saniye.")]
    [SerializeField] private float dashPushStunDuration = 0.18f;

    [Header("Dash Afterimage (Sandevistan)")]
    [Tooltip("Dash sirasinda geride birakilan hayalet klonlar arasi sure (kucuk = daha sik iz).")]
    [SerializeField] private float dashAfterImageInterval = 0.04f;
    [Tooltip("Hayaletin baslangic saydamligi.")]
    [SerializeField] private float dashAfterImageAlpha = 0.5f;
    [Tooltip("Hayaletin solma suresi.")]
    [SerializeField] private float dashAfterImageFadeTime = 0.3f;
    [Tooltip("Hayalet rengi (Sandevistan icin mavi/turkuaz).")]
    [SerializeField] private Color dashAfterImageColor = new Color(0.3f, 0.8f, 1f, 1f);

    [Tooltip("Dash HIZ egrisi (ease-out): yatay=dash ilerlemesi(0-1), dikey=hiz carpani. Basta 1 (tam hiz) " +
             "sona dogru dusuk -> patlama gibi firlar sonra suzulur. Sabit tutarsan (hep 1) 'kosu' gibi hissettirir. " +
             "Mesafe azaldiysa dashSpeed'i yukselt.")]
    [SerializeField] private AnimationCurve dashSpeedCurve =
        new AnimationCurve(new Keyframe(0f, 1f, 0f, -1.5f), new Keyframe(1f, 0.15f, -0.5f, 0f));

    [Header("Can Ayarlari")]
    [SerializeField] private float maxHealth = 18f; // 9 kalp x 2 yarim-kalp
    [SerializeField] private Color hurtColor = Color.red;
    [SerializeField] private float hurtFlashDuration = 0.1f;
    [SerializeField] private float deathAnimDuration = 0.8f;

    // Vurus suresince hasar taramasi icin — her karede yeni dizi ayirmamak icin tekrar kullanilir
    private const int MaxHitBufferSize = 32;
    private readonly Collider2D[] _hitBuffer = new Collider2D[MaxHitBufferSize];
    // AoE: bir swing icinde ayni dusmana tekrar vurmayi engeller (her kare taranir). Alloc'suz — Clear ile yeniden kullanilir.
    private readonly HashSet<Collider2D> _swingHitSet = new HashSet<Collider2D>();

    // Multi-slash (Silah 4): kac yonde vurulacak (1 = normal). MultiSlashWeapon ayarlar.
    private int _attackDirectionCount = 1;
    private readonly List<GameObject> _extraClaws = new List<GameObject>(); // ekstra yonlerin claw gorselleri (havuz)

    // Pence, vurus suresinin ilk bu oraninda hedefin tam mesafesine uzanir; kalan surede o noktada kalir.
    // Uzak dusmanlarda (menzil upgrade'i) gorselin gercekten degmesini ve overlap'in tam acilimda
    // dusmani yakalamasini saglar — "vuruyor ama hasar yok" sorununu kokten cozer.
    private const float AttackReachPortion = 0.7f;

    // Joystick girisi bu buyuklugun altindaysa "durgun" say (kucuk noise deadzone).
    // HEM hareket HEM animasyon ayni esigi kullanir — "yavas hareket ama animasyon yok" tutarsizligini onler.
    private const float InputDeadZoneSqr = 0.0025f; // magnitude ~0.05

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private Animator animator;

    private bool isCooldown = false;
    private bool isDashing = false;
    private bool isDashOnCooldown = false;
    private float dashStartTime; // dash basladigi an — cooldown gostergesi (0->1) + hiz egrisi ilerlemesi icin
    private Vector2 _dashDirection; // dash yonu (basta kilitlenir; egri boyunca bu yonde sonumlenir)
    private readonly Collider2D[] dashHitBuffer = new Collider2D[16]; // dash itme icin alloc'suz overlap tamponu
    private bool isPaused = false; // Upgrade paneli acikken true — Update input'u isler islemez keser
    private Vector2 lastMoveDirection = Vector2.right;

    private float currentHealth;
    private bool isDead;
    private SpriteRenderer spriteRenderer;
    private Coroutine _dieRoutine;

    // ---- Ultimate (Nuke) ----
    // Saf ekran-temizleme: ultimate artik SUREKLI stat buff'i DEGIL. Basinca kisa bir "aktif
    // pencere" boyunca OnUltimateActiveChanged(true) yayilir (aura + sinema bunu dinler), pencere
    // bitince (false). Dusmanlari silme / kamera zoom / ekran overlay'i UltimateCinematic yapar.
    [Header("Ultimate")]
    [Tooltip("Ultimate sinema/aura penceresinin saniye suresi — bu sure boyunca 'aktif' kabul edilir.")]
    [SerializeField] private float ultimateActiveDuration = 3.1f;
    [Tooltip("Ultimate sinemasinda karakterin gecici olarak takindigi kameraya-donuk poz sprite'i.")]
    [SerializeField] private Sprite ultimatePoseSprite;
    private Coroutine _ultRoutine;
    private bool _ultPosing; // true iken hareket/saldiri kilitli, sprite pozda sabit (animator kapali)

    public event System.Action<float> OnHealthChanged;

    /// <summary>Ultimate buff'i basladiginda (true) ve bittiginde (false) tetiklenir. UltimateAura bunu dinler.</summary>
    public event System.Action<bool> OnUltimateActiveChanged;

    /// <summary>Player ölüm animasyonu bittiğinde bir kez tetiklenir. GameOverUI bunu dinler.</summary>
    public static event System.Action OnPlayerDied;

    /// <summary>Player hasar aldığında alınan hasar miktarıyla tetiklenir. DamageScreenFlash bunu dinler.</summary>
    public static event System.Action<float> OnPlayerDamaged;

    /// <summary>Ölüm animasyonu BAŞLARKEN (Die() çağrılır çağrılmaz) ölüm konumuyla tetiklenir. CameraDeathZoom bunu dinler.</summary>
    public static event System.Action<Vector3> OnPlayerDeathStarted;

    void Awake()
    {
        InitializeComponents();
    }

    void Update()
    {
        if (isDead) return;
        if (isPaused) return; // Panel aciksa hareket/saldiri/dash girisi kilitli (timeScale=0'a ek garanti)
        if (_ultPosing) return; // Ultimate pozundayken input yok (kilidi asagida fizik de destekler)

        HandleMovementInput();
        UpdateAnimationState();
        HandleVampireHunterAttack();
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) TriggerDash();
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) UltimateManager.TryActivate(); // E = ulti (hazirsa)
    }

    void FixedUpdate()
    {
        MoveCharacterPhysics();
    }

    private void InitializeComponents()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        currentHealth = maxHealth;
    }

    private void HandleMovementInput()
    {
        // Joystick girisi (mobil)
        Vector2 joystickInput = Vector2.zero;
        if (joystick != null)
            joystickInput = new Vector2(joystick.Horizontal, joystick.Vertical);

        // WASD klavye girisi (masaustu). Yeni Input System uzerinden okunur.
        Vector2 keyboardInput = ReadKeyboardInput();

        // Iki kaynak birlestirilir; klavye varsa onceligi klavyeye ver, yoksa joystick.
        // Boylece ayni anda ikisi de kullanilsa cakisma olmaz.
        moveInput = keyboardInput.sqrMagnitude > InputDeadZoneSqr ? keyboardInput : joystickInput;

        if (moveInput.sqrMagnitude > 1f)
            moveInput.Normalize();

        if (moveInput.sqrMagnitude > InputDeadZoneSqr)
            lastMoveDirection = moveInput.normalized;
    }

    /// <summary>WASD / ok tuslarindan hareket vektoru okur (yeni Input System).</summary>
    private Vector2 ReadKeyboardInput()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return Vector2.zero;

        Vector2 input = Vector2.zero;

        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1f;

        return input;
    }

    private void UpdateAnimationState()
    {
        if (isDashing) return;

        if (moveInput.sqrMagnitude > InputDeadZoneSqr)
        {
            animator.SetBool("isRuning", true);
            animator.SetFloat("MoveX", moveInput.x);
            animator.SetFloat("MoveY", moveInput.y);
        }
        else
        {
            animator.SetBool("isRuning", false);
        }
    }

    private void MoveCharacterPhysics()
    {
        if (_ultPosing)
        {
            rb.linearVelocity = Vector2.zero; // Slow-mo'da timeScale>0 oldugundan FixedUpdate calisir; pozda kaymayi engelle
            return;
        }

        if (isDashing)
        {
            // Hiz egrisi: dash ilerlemesine (0->1) gore carpan. Ease-out -> basta patlama, sonda suzulme.
            float p = dashDuration > 0f ? Mathf.Clamp01((Time.time - dashStartTime) / dashDuration) : 1f;
            float speedMul = (dashSpeedCurve != null && dashSpeedCurve.length > 0) ? dashSpeedCurve.Evaluate(p) : 1f;
            rb.linearVelocity = _dashDirection * (dashSpeed * speedMul);
            return;
        }

        // Deadzone altindaysa tam dur — animasyonla ayni esik, tutarli davranis.
        // Hareket hizi combo carpaniyla ARTAR (kullanici istegi: sadece hareket + hasar comboyla artar).
        rb.linearVelocity = moveInput.sqrMagnitude > InputDeadZoneSqr
            ? moveInput * (moveSpeed * ComboManager.Multiplier)
            : Vector2.zero;
    }

    private void HandleVampireHunterAttack()
    {
        if (isCooldown) return;

        Transform targetEnemy = GetClosestEnemy();

        if (targetEnemy != null)
        {
            Vector2 directionToEnemy = (targetEnemy.position - transform.position).normalized;
            StartCoroutine(VampireAttackRoutine(directionToEnemy));
            SfxManager.Play(SfxId.PlayerHit); // pence vurus sesi (menzilde dusman varken her swing)
        }
    }

    private Transform GetClosestEnemy()
    {
        // Sahnedeki her seyi aramak yerine sadece menzildeki enemyLayer'lari radarla tarar
        Collider2D[] enemiesInRange = Physics2D.OverlapCircleAll(transform.position, autoAttackRange, enemyLayers);
        
        Transform closestEnemy = null;
        float closestDistance = Mathf.Infinity;

        foreach (Collider2D enemyCollider in enemiesInRange)
        {
            // EnemyController, BurstShooterEnemy, BoomerangEnemy ya da Boss — biri varsa geçerli hedef
            bool isEnemy = enemyCollider.GetComponent<EnemyController>() != null
                        || enemyCollider.GetComponent<BurstShooterEnemy>() != null
                        || enemyCollider.GetComponent<BoomerangEnemy>() != null
                        || enemyCollider.GetComponent<BossController>() != null;
            if (!isEnemy) continue;

            float distanceToEnemy = Vector2.Distance(transform.position, enemyCollider.transform.position);
            if (distanceToEnemy < closestDistance)
            {
                closestDistance = distanceToEnemy;
                closestEnemy = enemyCollider.transform;
            }
        }

        return closestEnemy;
    }

    private IEnumerator VampireAttackRoutine(Vector2 targetDirection)
{
    isCooldown = true;

    // Düşmanı TEKRAR bul (coroutine başlamadan önce hareket etmiş olabilir)
    Transform targetEnemy = GetClosestEnemy();

    // Vuruş yönü: düşman hâlâ varsa güncel yönünü kullan, kaybolmuşsa başlangıç yönü
    Vector2 attackDirection = targetDirection;

    // Pençenin ULAŞACAĞI mesafe: düşman varsa onun GERÇEK mesafesi (menzil upgrade'iyle büyür),
    // yoksa sabit attackOffset. attackOffset'ten bu mesafeye dogru vurus suresince Lerp'lenecek —
    // sabit kisa mesafede kalip havaya sallamak yerine pence gercekten dusmana ulasir.
    float targetDistance = attackOffset;
    if (targetEnemy != null)
    {
        attackDirection = ((Vector2)targetEnemy.position - (Vector2)transform.position).normalized;
        targetDistance = Vector2.Distance(transform.position, targetEnemy.position);
    }

    // Pençe DÜŞMANIN üstüne ışınlanmaz; kedinin ÖNÜNDE, düşman yönünde durur (baslangic mesafesi)
    attackPointObject.transform.localPosition =
        new Vector3(attackDirection.x, attackDirection.y, 0f) * attackOffset;

    // Pençe görselini düşmana doğru döndür (sprite'ın varsayılan yönüne göre offset ile düzeltilir)
    // rotateAttackVisual kapalıysa hiç döndürme — "düşman etrafında dönme" sorununu izole etmek için
    if (rotateAttackVisual)
    {
        float angle = Mathf.Atan2(attackDirection.y, attackDirection.x) * Mathf.Rad2Deg + attackVisualAngleOffset;
        attackPointObject.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }
    else
    {
        attackPointObject.transform.localRotation = Quaternion.identity;
    }

    attackPointObject.SetActive(true);

    // Multi-slash (Silah 4): birden fazla yonde vur. dirs=1 iken davranis eskisiyle AYNI.
    int dirs = Mathf.Max(1, _attackDirectionCount);
    float baseAngleDeg = Mathf.Atan2(attackDirection.y, attackDirection.x) * Mathf.Rad2Deg;
    float dirStep = 360f / dirs;
    SetupExtraClaws(dirs, baseAngleDeg, dirStep);

    // Kamera sarsıntısı vuruş başına 1 kez (tek anlık, döngüden önce)
    if (cameraShake != null)
        cameraShake.TriggerShake(shakeDuration, shakeMagnitude);

    // AoE: bu swing boyunca ALANDAKI TUM dusmanlara (dedup ile bir kez) hasar + hafif geri itme.
    _swingHitSet.Clear();
    bool comboRegistered = false;

    float elapsed = 0f;
    while (elapsed < attackDuration)
    {
        // Pençeyi baslangic mesafesinden (attackOffset) hedefin GERCEK mesafesine dogru ilerlet —
        // ilk %70'te tam mesafeye ULASIR (reachT 1'e kilitlenir), kalan surede o noktada kalir.
        // Boylece uzak dusmanda gorsel gercekten deger ve overlap tam acilimda yakalar.
        float reachT = attackDuration > 0f
            ? Mathf.Clamp01((elapsed / attackDuration) / AttackReachPortion)
            : 1f;
        float currentDistance = Mathf.Lerp(attackOffset, targetDistance, reachT);
        attackPointObject.transform.localPosition = new Vector3(attackDirection.x, attackDirection.y, 0f) * currentDistance;

        // Ekstra claw gorsellerini (multi-slash) ayni mesafeye ilerlet
        UpdateExtraClawPositions(dirs, baseAngleDeg, dirStep, currentDistance);

        // Her YONDE (dirs) overlap tara; her dusmana bu swing'de BIR kez hasar + geri itme (tum yonlerde dedup).
        for (int d = 0; d < dirs; d++)
        {
            float ang = (baseAngleDeg + d * dirStep) * Mathf.Deg2Rad;
            Vector2 dirVec = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            Vector3 worldAttackPosition = (Vector2)transform.position + dirVec * currentDistance;
            int hitCount = Physics2D.OverlapCircleNonAlloc(worldAttackPosition, attackRadius, _hitBuffer, enemyLayers);
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D col = _hitBuffer[i];
                if (col == null || _swingHitSet.Contains(col)) continue; // ayni dusmani tekrar vurma
                _swingHitSet.Add(col);
                ApplyDamage(col, playerDamage * ComboManager.Multiplier); // combo carpani CARPILIR
                ApplyKnockback(col); // hafif geri it
            }
        }
        // Combo swing basina 1 kez artar (AoE'de her dusman icin ayri artmasin — combo sismesin).
        if (_swingHitSet.Count > 0 && !comboRegistered)
        {
            ComboManager.RegisterHit();
            comboRegistered = true;
        }

        elapsed += Time.deltaTime;
        yield return null;
    }

    // GARANTI: swing boyunca hic kimse yakalanmadiysa (uzak hedef + dusuk FPS), nisan alinan dusmana vur.
    if (_swingHitSet.Count == 0 && targetEnemy != null)
    {
        Collider2D targetCollider = targetEnemy.GetComponent<Collider2D>();
        if (targetCollider != null)
        {
            ApplyDamage(targetCollider, playerDamage * ComboManager.Multiplier);
            ApplyKnockback(targetCollider);
            ComboManager.RegisterHit();
        }
    }

    attackPointObject.SetActive(false);
    DeactivateExtraClaws(); // multi-slash ekstra claw'larini gizle

    // Combo saldiri hizini ETKILEMEZ: cooldown sabit. Combo yalnizca HASAR ve HAREKET hizina uygulanir.
    yield return new WaitForSeconds(attackCooldown);
    isCooldown = false;
}

    /// <summary>Vurulan dusmani oyuncudan uzaga HAFIF geri iter (tek atimlik konum nudge'i). AI kisa surede geri toparlar.</summary>
    private void ApplyKnockback(Collider2D enemyCollider)
    {
        if (attackKnockback <= 0f || enemyCollider == null) return;
        if (enemyCollider.GetComponent<BossController>() != null) return; // boss agir — geri itilmez
        Vector2 dir = (Vector2)enemyCollider.transform.position - (Vector2)transform.position;
        dir = dir.sqrMagnitude < 0.0001f ? lastMoveDirection : dir.normalized; // ust uste ise vurus yonune it
        enemyCollider.transform.position += (Vector3)(dir * attackKnockback);
    }

    /// <summary>Verilen collider'a hasar uygular; uc dusman tipini + boss'u destekler.</summary>
    private void ApplyDamage(Collider2D enemyCollider, float damage)
    {
        // Boss ayri sinif, kendi TakeDamage'i var — once onu dene
        BossController boss = enemyCollider.GetComponent<BossController>();
        if (boss != null)
        {
            boss.TakeDamage(damage);
            return;
        }

        EnemyController enemy = enemyCollider.GetComponent<EnemyController>();
        if (enemy != null)
        {
            enemy.TakeDamage(damage);
            return;
        }

        // EnemyController degilse burst shooter olabilir — ayri sinif, kendi TakeDamage'i var
        BurstShooterEnemy burst = enemyCollider.GetComponent<BurstShooterEnemy>();
        if (burst != null)
        {
            burst.TakeDamage(damage);
            return;
        }

        // Ikisi de degilse boomerang dusmani olabilir — ayri sinif, kendi TakeDamage'i var
        BoomerangEnemy boomerang = enemyCollider.GetComponent<BoomerangEnemy>();
        if (boomerang != null)
            boomerang.TakeDamage(damage);
    }

    /// <summary>Multi-slash silahi (Silah 4) cagirir: saldirinin kac YONDE vuracagini ayarlar (1 = normal, max 6).</summary>
    public void SetAttackDirections(int count) => _attackDirectionCount = Mathf.Max(1, count);

    /// <summary>Ekstra yonler icin claw gorsellerini hazirlar (havuz; gerekli kadar aktif, digerleri gizli).</summary>
    private void SetupExtraClaws(int dirs, float baseAngleDeg, float step)
    {
        int needed = dirs - 1; // 0. yon zaten attackPointObject

        while (_extraClaws.Count < needed) // havuzu gerektikce buyut (attackPointObject kopyalari, ayni parent)
        {
            GameObject clone = Instantiate(attackPointObject, attackPointObject.transform.parent);
            clone.SetActive(false);
            _extraClaws.Add(clone);
        }

        for (int i = 0; i < _extraClaws.Count; i++)
        {
            GameObject claw = _extraClaws[i];
            if (claw == null) continue;
            if (i < needed)
            {
                float ang = baseAngleDeg + (i + 1) * step;
                claw.transform.localRotation = rotateAttackVisual
                    ? Quaternion.Euler(0f, 0f, ang + attackVisualAngleOffset)
                    : Quaternion.identity;
                claw.SetActive(true); // OnEnable claw animasyonunu bastan oynatir
            }
            else claw.SetActive(false);
        }
    }

    /// <summary>Ekstra claw'lari swing boyunca yonlerine gore ayni mesafeye ilerletir.</summary>
    private void UpdateExtraClawPositions(int dirs, float baseAngleDeg, float step, float distance)
    {
        int needed = dirs - 1;
        for (int i = 0; i < needed && i < _extraClaws.Count; i++)
        {
            if (_extraClaws[i] == null) continue;
            float ang = (baseAngleDeg + (i + 1) * step) * Mathf.Deg2Rad;
            _extraClaws[i].transform.localPosition = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * distance;
        }
    }

    /// <summary>Tum ekstra claw'lari gizler (swing sonu).</summary>
    private void DeactivateExtraClaws()
    {
        for (int i = 0; i < _extraClaws.Count; i++)
            if (_extraClaws[i] != null) _extraClaws[i].SetActive(false);
    }

    /// <summary>UI Dash butonuna bağla. Cooldown'daysa veya zaten dash'teyse yoksayar.</summary>
    public void TriggerDash()
    {
        if (isDashing || isDashOnCooldown) return;
        dashStartTime = Time.time; // cooldown gostergesi bu andan itibaren 0->1 dolar
        StartCoroutine(DashRoutine());
        SfxManager.Play(SfxId.Dash); // dash whoosh
    }

    private IEnumerator DashRoutine()
    {
        isDashing = true;
        isDashOnCooldown = true;

        _dashDirection = lastMoveDirection; // yonu dash basinda kilitle; hiz egriyle sonumlenir
        animator.SetFloat("DashX", lastMoveDirection.x);
        animator.SetFloat("DashY", lastMoveDirection.y);
        animator.SetTrigger("Dash");

        // Dash boyunca her kare degen dusmanlari HAFIFCE it (kalabalıktan cikis). Player bu sure boyunca immun.
        // Ayrica Sandevistan tarzi geride saydam hayalet klonlar birak (telegraph YOK — sadece iz).
        float elapsed = 0f;
        float aiTimer = 0f;
        while (elapsed < dashDuration)
        {
            PushEnemiesAside();

            if (spriteRenderer != null)
            {
                aiTimer += Time.deltaTime;
                if (aiTimer >= dashAfterImageInterval)
                {
                    aiTimer = 0f;
                    AfterImage.Spawn(spriteRenderer, dashAfterImageColor, dashAfterImageAlpha,
                                     dashAfterImageFadeTime, spriteRenderer.sortingOrder - 1);
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
        isDashing = false;

        yield return new WaitForSeconds(dashCooldown);
        isDashOnCooldown = false;
    }

    /// <summary>Dash sirasinda yaricaptaki dusmanlari oyuncudan uzaga COK KUCUK iter (uzaga firlatmaz, sadece yol acar).</summary>
    private void PushEnemiesAside()
    {
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, dashPushRadius, dashHitBuffer, enemyLayers);
        for (int i = 0; i < count; i++)
        {
            Collider2D col = dashHitBuffer[i];
            if (col == null) continue;
            Vector2 dir = (Vector2)col.transform.position - (Vector2)transform.position;
            // Tam ust uste ise (yon belirsiz) dash yonunun tersine it — yol acilsin
            dir = dir.sqrMagnitude < 0.0001f ? lastMoveDirection : dir.normalized;
            col.transform.position += (Vector3)(dir * dashPushStep);

            // Itilen dusman KISA sure yerinde dursun (sonra yurur) — 'duvari ittirme' hissi. Boss stun'lanmaz.
            if (col.TryGetComponent(out EnemyController ec)) ec.ApplyPushStun(dashPushStunDuration);
            else if (col.TryGetComponent(out BurstShooterEnemy bs)) bs.ApplyPushStun(dashPushStunDuration);
            else if (col.TryGetComponent(out BoomerangEnemy be)) be.ApplyPushStun(dashPushStunDuration);
        }
    }

    /// <summary>Dash cooldown ilerleme orani: 0 = yeni atildi, 1 = hazir. UI (DashButtonUI) bunu okur.</summary>
    public float DashCooldownNormalized
    {
        get
        {
            if (!isDashOnCooldown) return 1f;
            float total = dashDuration + dashCooldown;
            return total <= 0f ? 1f : Mathf.Clamp01((Time.time - dashStartTime) / total);
        }
    }

    /// <summary>Dash su an kullanilabilir mi (cooldown'da degil).</summary>
    public bool IsDashReady => !isDashOnCooldown;

    /// <summary>Player'a hasar verir; can sıfırlanınca ölüm tetiklenir.</summary>
    public void TakeDamage(float amount)
    {
        if (isDead) return;
        if (isPaused) return; // Upgrade paneli acikken IMMUN — kart secerken hicbir kaynaktan hasar alinmaz
                              // (timeScale=0 fizigi durdurur ama 'yield return null' tabanli hasar donguleri
                              //  render karesinde calismaya devam edebilir; tek cikis noktasindan kesin garanti)
        if (isDashing) return; // Dash sirasinda IMMUN — hasar, flash, sarsinti hicbiri tetiklenmez

        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        OnHealthChanged?.Invoke(currentHealth);
        OnPlayerDamaged?.Invoke(amount); // Ekran kirmizi flash'i tetikler (sadece player hasar alinca)

        // Hasar sarsintisi vurus sarsintisindan guclu — CameraShake guclü olani onceler
        if (cameraShake != null)
            cameraShake.TriggerShake(hurtShakeDuration, hurtShakeMagnitude);

        if (spriteRenderer != null)
            StartCoroutine(HurtFlash());

        if (currentHealth <= 0f)
            Die();
    }

    /// <summary>Mevcut canı döndürür. HeartUI gibi dış sistemler başlangıçta okur.</summary>
    public float GetCurrentHealth() => currentHealth;

    /// <summary>Maksimum canı döndürür.</summary>
    public float GetMaxHealth() => maxHealth;

    // ---- Upgrade / Pause API (FAZ 5) ----
    // UpgradeSelectionUI bu metodları çağırır. Denge (miktar) UI tarafında SerializeField;
    // player sadece stat'ı uygular, "God" mantık tutmaz.

    /// <summary>Upgrade paneli açılınca true, kapanınca false. Update input'unu kilitler (timeScale=0'a ek garanti).</summary>
    public void SetPaused(bool paused) => isPaused = paused;

    /// <summary>Vuruş hasarını kalıcı arttırır (hasar upgrade'i).</summary>
    public void AddDamage(float amount) => playerDamage += amount;

    /// <summary>Saldırı bekleme süresini çarpanla kısaltır (küçük = hızlı). Taban minAttackCooldown ile sınırlı.</summary>
    /// <param name="cooldownMultiplier">Örn. 0.85 → cooldown %15 kısalır. 0-1 arası verilmeli.</param>
    public void ApplyAttackSpeedMultiplier(float cooldownMultiplier)
    {
        attackCooldown = Mathf.Max(minAttackCooldown, attackCooldown * cooldownMultiplier);
    }

    /// <summary>Oto-saldırı menzilini kalıcı arttırır (menzil upgrade'i).</summary>
    public void AddAttackRange(float amount) => autoAttackRange += amount;

    /// <summary>Eksik cani doldurur (heal upgrade'i). Max can DEGISMEZ - hep sabit kalir. OnHealthChanged fırlatır.</summary>
    public void Heal(float amount)
    {
        currentHealth = Mathf.Clamp(currentHealth + amount, 0f, maxHealth);
        OnHealthChanged?.Invoke(currentHealth);
    }

    /// <summary>
    /// Ultimate'i aktive eder (SAF NUKE): stat buff'i YOK. Sadece 'ultimateActiveDuration' boyunca
    /// OnUltimateActiveChanged(true) yayar; bu pencere boyunca aura + UltimateCinematic sahneyi
    /// yonetir (kamera zoom, alev, beyaz impact, tum dusmanlari dropsuz silme). Pencere bitince
    /// (false) yayilir. Tekrar cagrilirsa REFRESH — pencere bastan baslar. UltimateManager cagirir.
    /// </summary>
    public void ActivateUltimate()
    {
        if (_ultRoutine != null) StopCoroutine(_ultRoutine);
        _ultRoutine = StartCoroutine(UltimateNukeRoutine());
        SfxManager.Play(SfxId.Ultimate); // ulti aktivasyon sesi
    }

    /// <summary>Ultimate sinema penceresi su an aktif mi (aura/UI icin).</summary>
    public bool IsUltimateActive => _ultRoutine != null;

    /// <summary>
    /// Ultimate sinemasi icin karakteri "kameraya donuk poz"a sokar: hareket/saldiri kilitlenir,
    /// Animator KAPATILIR (yoksa her kare sprite'i ezer — bilinen tuzak) ve ultimatePoseSprite gosterilir.
    /// UltimateCinematic zamanlar; sprite atanmamissa yalnizca hareketi dondurur. ExitUltimatePose ile geri alinir.
    /// </summary>
    public void EnterUltimatePose()
    {
        _ultPosing = true;
        moveInput = Vector2.zero;
        if (rb != null) rb.linearVelocity = Vector2.zero;

        if (animator != null) animator.enabled = false; // sprite'i sabitleyebilmek icin animator'i sustur
        if (spriteRenderer != null && ultimatePoseSprite != null)
        {
            spriteRenderer.color = Color.white; // yarim kalmis hurt-flash rengini temizle
            spriteRenderer.sprite = ultimatePoseSprite;
        }
    }

    /// <summary>Ultimate pozundan cikar: Animator tekrar acilir (bir sonraki karede normal sprite'i surer), input serbest.</summary>
    public void ExitUltimatePose()
    {
        _ultPosing = false;
        if (animator != null) animator.enabled = true;
    }

    private IEnumerator UltimateNukeRoutine()
    {
        // Aktif pencereyi ac: aura + sinema baslar. Stat DEGISMEZ (saf nuke).
        OnUltimateActiveChanged?.Invoke(true);
        // Realtime: sinema slow-mo (Time.timeScale<1) uygulasa bile pencere gercek saniyeyle olculur.
        yield return new WaitForSecondsRealtime(ultimateActiveDuration);
        _ultRoutine = null;
        OnUltimateActiveChanged?.Invoke(false);
    }

    private IEnumerator HurtFlash()
    {
        spriteRenderer.color = hurtColor;
        yield return new WaitForSeconds(hurtFlashDuration);
        spriteRenderer.color = Color.white;
    }

    private void Die()
    {
        isDead = true;
        rb.linearVelocity = Vector2.zero;
        animator.SetTrigger("Death");
        OnPlayerDeathStarted?.Invoke(transform.position); // Kamera zoom'unu animasyonla ES ZAMANLI baslat
        _dieRoutine = StartCoroutine(DieRoutine());
    }

    private IEnumerator DieRoutine()
    {
        yield return new WaitForSeconds(deathAnimDuration);
        _dieRoutine = null;
        OnPlayerDied?.Invoke(); // Game Over ekranini tetikle (once event, sonra deaktif)
        gameObject.SetActive(false);
    }

    // Editör ekranında saldırı alanını görebilmek için çizim fonksiyonu
    private void OnDrawGizmosSelected()
    {
        if (attackPointObject != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPointObject.transform.position, attackRadius);
        }
    }
}
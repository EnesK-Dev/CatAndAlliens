using System.Collections;
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

    [Header("Can Ayarlari")]
    [SerializeField] private float maxHealth = 18f; // 9 kalp x 2 yarim-kalp
    [SerializeField] private Color hurtColor = Color.red;
    [SerializeField] private float hurtFlashDuration = 0.1f;
    [SerializeField] private float deathAnimDuration = 0.8f;

    // Vurus suresince hasar taramasi icin — her karede yeni dizi ayirmamak icin tekrar kullanilir
    private const int MaxHitBufferSize = 32;
    private readonly Collider2D[] _hitBuffer = new Collider2D[MaxHitBufferSize];

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
    private bool isPaused = false; // Upgrade paneli acikken true — Update input'u isler islemez keser
    private Vector2 lastMoveDirection = Vector2.right;
    private Vector2 dashVelocity;

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
        if (Keyboard.current != null && Keyboard.current.leftShiftKey.wasPressedThisFrame) TriggerDash();
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
            rb.linearVelocity = dashVelocity;
            return;
        }

        // Deadzone altindaysa tam dur — animasyonla ayni esik, tutarli davranis
        rb.linearVelocity = moveInput.sqrMagnitude > InputDeadZoneSqr
            ? moveInput * moveSpeed
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
            // EnemyController, BurstShooterEnemy ya da BoomerangEnemy — biri varsa geçerli düşman
            bool isEnemy = enemyCollider.GetComponent<EnemyController>() != null
                        || enemyCollider.GetComponent<BurstShooterEnemy>() != null
                        || enemyCollider.GetComponent<BoomerangEnemy>() != null;
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

    // Kamera sarsıntısı vuruş başına 1 kez (tek anlık, döngüden önce)
    if (cameraShake != null)
        cameraShake.TriggerShake(shakeDuration, shakeMagnitude);

    // Bu vuruşta yalnızca EN YAKIN 1 düşmana hasar verilir. Vurulunca döngü artık hasar aramaz.
    bool hasHitThisSwing = false;

    // Vuruş süresince (attackDuration) her kare taranır; henüz kimseye vurulmadıysa
    // alandaki en yakın düşman bulunup sadece ona hasar uygulanır (single target).
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

        if (!hasHitThisSwing)
        {
            Vector3 worldAttackPosition = attackPointObject.transform.position;
            int hitCount = Physics2D.OverlapCircleNonAlloc(worldAttackPosition, attackRadius, _hitBuffer, enemyLayers);

            Collider2D closestEnemy = GetClosestEnemyCollider(worldAttackPosition, hitCount);
            if (closestEnemy != null)
            {
                // Combo carpani hasara CARPILIR. ComboManager yoksa 1 doner (etkisiz).
                ApplyDamage(closestEnemy, playerDamage * ComboManager.Multiplier);
                ComboManager.RegisterHit(); // Basarili vurus — combo sayaci +1
                hasHitThisSwing = true;
            }
        }

        elapsed += Time.deltaTime;
        yield return null;
    }

    // GARANTI: swing bitti ama overlap dusmani yakalayamadiysa (uzak hedef + dusuk FPS'te olabilir),
    // nisan alinan dusmana DOGRUDAN hasar uygula. targetEnemy yok olmussa Unity fake-null ile atlanir.
    if (!hasHitThisSwing && targetEnemy != null)
    {
        Collider2D targetCollider = targetEnemy.GetComponent<Collider2D>();
        if (targetCollider != null)
        {
            ApplyDamage(targetCollider, playerDamage * ComboManager.Multiplier);
            ComboManager.RegisterHit(); // Garanti-vurus da combo'yu sayar
        }
    }

    attackPointObject.SetActive(false);

    // Combo carpani saldiri hizina da uygulanir: carpan buyudukce cooldown KISALIR (bolen).
    yield return new WaitForSeconds(attackCooldown / ComboManager.Multiplier);
    isCooldown = false;
}

    /// <summary>Overlap sonuclari arasindan verilen noktaya en yakin dusman collider'ini dondurur.</summary>
    private Collider2D GetClosestEnemyCollider(Vector3 fromPosition, int hitCount)
    {
        Collider2D closest = null;
        float closestSqr = Mathf.Infinity;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D candidate = _hitBuffer[i];
            if (candidate == null) continue;

            // sqrMagnitude — karekok maliyetinden kacinmak icin (sadece kiyaslama yapiyoruz)
            float sqrDistance = ((Vector2)candidate.transform.position - (Vector2)fromPosition).sqrMagnitude;
            if (sqrDistance < closestSqr)
            {
                closestSqr = sqrDistance;
                closest = candidate;
            }
        }

        return closest;
    }

    /// <summary>Verilen collider'a hasar uygular; uc dusman tipini de destekler.</summary>
    private void ApplyDamage(Collider2D enemyCollider, float damage)
    {
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

    /// <summary>UI Dash butonuna bağla. Cooldown'daysa veya zaten dash'teyse yoksayar.</summary>
    public void TriggerDash()
    {
        if (isDashing || isDashOnCooldown) return;
        StartCoroutine(DashRoutine());
        SfxManager.Play(SfxId.Dash); // dash whoosh
    }

    private IEnumerator DashRoutine()
    {
        isDashing = true;
        isDashOnCooldown = true;

        dashVelocity = lastMoveDirection * dashSpeed;
        animator.SetFloat("DashX", lastMoveDirection.x);
        animator.SetFloat("DashY", lastMoveDirection.y);
        animator.SetTrigger("Dash");

        yield return new WaitForSeconds(dashDuration);
        isDashing = false;

        yield return new WaitForSeconds(dashCooldown);
        isDashOnCooldown = false;
    }

    /// <summary>Player'a hasar verir; can sıfırlanınca ölüm tetiklenir.</summary>
    public void TakeDamage(float amount)
    {
        if (isDead) return;

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
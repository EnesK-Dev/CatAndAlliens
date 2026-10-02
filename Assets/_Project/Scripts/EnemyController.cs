using UnityEngine;

public class EnemyController : MonoBehaviour, IDifficultyScaled
{
    #region Serialized Fields
    [Header("Yapay Zeka Ayarlari")]
    [SerializeField] private float moveSpeed = 3f;

    [Tooltip("Duvara dik gelince pinlenmek yerine duvar boyunca kaysin (wall slide). Kapatilirsa eski davranis.")]
    [SerializeField] private bool wallSlide = true;

    [Tooltip("Onde bu mesafede duvar varsa kayma devreye girer (dunya birimi).")]
    [SerializeField] private float wallProbeDistance = 0.4f;

    [Header("Can Ayarlari")]
    [SerializeField] private float maxHealth;
    [Tooltip("Zorluk 1 iken (zamanla) can carpani. Spawn aninda GLOBAL DifficultyFactor ile Lerp'lenir — " +
             "gec spawn olan dusman daha tanktir (oyuncunun artan hasarina karsi denge).")]
    [SerializeField] private float healthMultiplierAtMaxDifficulty = 3f;

    [Header("Vurulma Flash Ayarlari")]
    [SerializeField] private Color hitFlashColor = Color.red;
    [SerializeField] private float hitFlashDuration = 0.08f;

    [Header("Olum Animasyonu Ayarlari")]
    [SerializeField] private Sprite[] deathFrames;
    [SerializeField] private float deathFrameRate = 12f;

    [Header("Dash (siradan dusman — seyrek, kisa, duz)")]
    [Tooltip("Aciksa bu dusman ara sira KISA bir dash atar (elite/lazer olanlar atmaz). Sadece normal Enemy'de ac.")]
    [SerializeField] private bool enableDash = false;
    [Tooltip("Iki dash arasi rastgele bekleme araligi (sn) — seyrek olsun.")]
    [SerializeField] private float dashIntervalMin = 4f;
    [SerializeField] private float dashIntervalMax = 8f;
    [Tooltip("Dash oncesi CHARGE suresi — bu surede ok telegraph gorunur ve dusman durur (oyuncu tepki verebilir).")]
    [SerializeField] private float dashChargeTime = 0.5f;

    [Header("Dash Telegraph (ok)")]
    [Tooltip("Telegraph (cizgi + ok) rengi.")]
    [SerializeField] private Color dashTelegraphColor = new Color(1f, 0.25f, 0.25f, 0.9f);
    [SerializeField] private float dashTelegraphWidth = 0.12f;
    [Tooltip("Dash mesafesi — kisa tut (zayif his).")]
    [SerializeField] private float dashDistance = 2.5f;
    [Tooltip("Dash hizi (birim/sn).")]
    [SerializeField] private float dashSpeed = 14f;
    [Range(0f, 1f)]
    [Tooltip("Dash'in oyuncuya dogru olma olasiligi; kalani rastgele yon (sag/sol/herhangi).")]
    [SerializeField] private float dashAtPlayerChance = 0.5f;

    [Tooltip("Dash sirasinda HAFIF afterimage (Sandevistan'in kisik hali) biraksin mi.")]
    [SerializeField] private bool leaveDashAfterImage = true;
    [SerializeField] private float dashAfterImageInterval = 0.05f;
    [Range(0f, 1f)]
    [SerializeField] private float dashAfterImageAlpha = 0.3f;
    [SerializeField] private float dashAfterImageFadeTime = 0.25f;
    [SerializeField] private Color dashAfterImageColor = new Color(0.7f, 0.9f, 1f, 1f);

    [Header("Lazer Saldiri Ayarlari")]
    [SerializeField] private float laserDamage = 2f; // 1 tam kalp = 2 yarim-kalp birimi

    [Header("Temas Hasari Ayarlari")]
    [SerializeField] private float bodyContactDamage = 1f;       // yarim kalp
    [SerializeField] private float contactDamageCooldown = 1f;   // saniyede max 1 kez vurur
    [SerializeField] private float chargeDuration = 1.5f;
    [SerializeField] private float laserDuration = 0.5f;
    [Tooltip("Lazer YERDE dururken kac saniyede bir hasar verir (tick araligi). Isin gorundugu sure boyunca surekli vurur. <=0 ise 0.4.")]
    [SerializeField] private float laserTickInterval = 0.4f;
    [Tooltip("Lazer hasar KUTUSUNUN genisligi (dunya birimi) ~gorsel isin kalinligi. Ince raycast yerine bu genislikte tarar -> isina SONRADAN giren de yer. <=0 ise 1.")]
    [SerializeField] private float laserDamageWidth = 1f;
    [SerializeField] private float minAttackCooldown = 3f;
    [SerializeField] private float maxAttackCooldown = 7f;
    [SerializeField] private LayerMask obstacleLayers;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private float eliteMoveSpeed = 1.5f;

    [Header("Lazer Gorseli")]
    [SerializeField] private LaserVisual laserVisualPrefab;

    [Header("Varyasyon Ayarlari")]
    [Range(0f, 1f)] [SerializeField] private float laserChance = 0.4f;

    [Header("Zorluk Olceklendirme (FAZ 3)")]
    [Tooltip("Zorluk 1 iken sarj (telegraph) suresi. chargeDuration'dan KISA ver — alt sinir, sifira inmez.")]
    [SerializeField] private float chargeDurationAtMaxDifficulty = 0.7f;

    [Tooltip("Zorluk 1 iken saldiri bekleme araligi ALT siniri. minAttackCooldown'dan kisa.")]
    [SerializeField] private float minAttackCooldownAtMaxDifficulty = 1.5f;

    [Tooltip("Zorluk 1 iken saldiri bekleme araligi UST siniri. maxAttackCooldown'dan kisa.")]
    [SerializeField] private float maxAttackCooldownAtMaxDifficulty = 3f;

    [Tooltip("Zorluk 1 iken hareket hizi carpani (1 = degisme, 1.15 = %15 hizli). Abartma.")]
    [SerializeField] private float moveSpeedMultiplierAtMaxDifficulty = 1.15f;

    [Tooltip("Zorluk 1 iken lazer kalinlik carpani (1 = degismez, 1.6 = %60 kalin). Her atista yeniden hesaplanir.")]
    [SerializeField] private float laserWidthMultiplierAtMaxDifficulty = 1.6f;

    [Header("Core Drop (FAZ 4)")]
    [Tooltip("Normal (lazersiz) dusman olunce dusen core ALT siniri (dahil).")]
    [SerializeField] private int coreDropNormal = 1;

    [Tooltip("Normal dusman core UST siniri (dahil). Min'e esit/kucukse aralik YOK (sabit). Ornek 2-4 icin: normal=2, normalMax=4.")]
    [SerializeField] private int coreDropNormalMax = 1;

    [Tooltip("Elite (lazerli) dusman olunce dusen core alt siniri (dahil).")]
    [SerializeField] private int coreDropEliteMin = 2;

    [Tooltip("Elite (lazerli) dusman olunce dusen core ust siniri (dahil).")]
    [SerializeField] private int coreDropEliteMax = 3;

    [Header("UltFood Drop (Ultimate)")]
    [Tooltip("Bu dusman olunce ultFood dusme ihtimali (0 = hic, 1 = her zaman).")]
    [Range(0f, 1f)] [SerializeField] private float ultFoodDropChance = 0.25f;
    #endregion

    #region Private Fields
    private bool isAttacking = false;
    private float nextAttackTime;
    private bool canUseLaser = false;

    private LaserVisual laserVisualInstance;
    // Lazer hasar taramasi icin alloc'suz buffer (Player layer'inda birden fazla collider var:
    // govde CapsuleCollider2D [trigger=0] + AttackPoint menzil dairesi [trigger=1]).
    private readonly Collider2D[] _laserHitBuffer = new Collider2D[8];
    private Transform playerTransform;
    private Rigidbody2D rb;
    // Duvar kaymasi (wall slide) — Obstacle layer'ina rb.Cast ile bakip pinlenmeyi onler.
    private ContactFilter2D _wallFilter;
    private bool _wallFilterReady;
    private static readonly RaycastHit2D[] _wallHits = new RaycastHit2D[4];
    private Vector2 moveDirection;
    private float currentHealth;
    private SpriteRenderer spriteRenderer;
    private float lastContactDamageTime;
    private Color baseColor;
    private Coroutine hitFlashRoutine;
    private Collider2D bodyCollider;
    private SpriteAnimator spriteAnimator;
    private bool isDying;
    private bool vacuumed; // Ultimate vacuum: AI/collider kapali, transform'u director oyuncuya ceker
    private float effectiveMoveSpeed;

    // Yerel zorluk (tipin acilisindan beri). Override yoksa global DifficultyFactor'a duser.
    private float _spawnDifficulty;
    private bool _hasSpawnDifficulty;
    private float EffectiveDifficulty => _hasSpawnDifficulty ? _spawnDifficulty : DifficultyManager.DifficultyFactor;
    private const float MinChargeDuration = 0.05f; // sarj suresinin inebilecegi guvenli taban
    #endregion

    // Havuz (pool) icin: base degerler SADECE ilk Instantiate'te (Awake) yakalanir; her yeniden kullanimda
    // (OnEnable) ResetForSpawn bunlardan sifirlar. Boylece can zorlukla ust uste CARPILIP sismez, scale/splitDepth
    // birikmez, olum durumu (isDying/animator/collider/rb) geri alinir.
    private float _baseMaxHealth;
    private Vector3 _baseScale;
    private float _baseMoveSpeed;
    private bool _baseEnableDash;
    private Color _originalBaseColor = Color.white; // prefab'in ILK rengi (elite ise mor). SetTint baseColor'i kirletebilir; reset buna doner.
    private bool _initedOnce;

    #region Unity Callbacks
    /// <summary>Base degerleri prefab'dan yakalar (SADECE ilk Instantiate; SetMaxHealth'ten ONCE calisir).</summary>
    protected virtual void Awake()
    {
        _baseMaxHealth = maxHealth;
        _baseScale = transform.localScale;
        _baseMoveSpeed = moveSpeed;
        _baseEnableDash = enableDash;
        // Prefab'in ORIJINAL rengini SetTint'ten ONCE yakala (Awake, Instantiate sirasinda calisir).
        // Boylece split-child olarak dogup SetTint yese bile, havuz reset'i yesil'e (prefab rengine) doner.
        var sr0 = GetComponent<SpriteRenderer>();
        if (sr0 != null) _originalBaseColor = sr0.color;
    }

    /// <summary>Havuzdan yeniden kullanimda calisir (ilk spawn'da Start hallettigi icin atlanir).</summary>
    protected virtual void OnEnable()
    {
        if (_initedOnce) ResetForSpawn();
    }

    /// <summary>Havuza donunce (despawn) lazer gorselini gizle — orphan kalmasin.</summary>
    protected virtual void OnDisable()
    {
        if (laserVisualInstance != null) laserVisualInstance.Hide();
    }

    private void Start()
    {
        InitializeComponents();
        InitializeHealthSystem();
        CalculateNextAttackTime();
        DetermineEnemyVariation();
        ComputeEffectiveMoveSpeed();
        FindTargetPlayer();
        SpawnLaserVisual();

        _nextDashTime = Time.time + Random.Range(dashIntervalMin, dashIntervalMax); // spawn'da hemen dash atmasin
        _initedOnce = true; // bundan sonra yeniden kullanimda OnEnable->ResetForSpawn devreye girer
    }

    /// <summary>
    /// Havuzdan yeniden kullanimda TUM durumu sifirlar (olum bayraklari, fizik, collider, animator, flash,
    /// knockback, can[base x zorluk], scale). DetermineEnemyVariation TEKRAR calismaz — her instance kendi
    /// elite/normal varyasyonunu korur (lazer gorseli o instance'a bagli kalir). Alt sinif ek reset icin override eder.
    /// </summary>
    protected virtual void ResetForSpawn()
    {
        isDying = false;
        vacuumed = false;
        _isDashing = false;
        _healthOverridden = false;
        _noFoodDrop = false;
        _noCoreDrop = false;
        _suppressDeathEffects = false;

        transform.localScale = _baseScale;

        // Hiz + dash'i base'e dondur (reuse'da onceki spawn'in child-multiplier'i/dash-kapatmasi kalmasin).
        // Elite ise hiz eliteMoveSpeed'e doner (DetermineEnemyVariation reuse'da tekrar calismaz).
        enableDash = _baseEnableDash;
        moveSpeed = canUseLaser ? eliteMoveSpeed : _baseMoveSpeed;
        ComputeEffectiveMoveSpeed();

        // Can: base x GUNCEL zorluk (SetMaxHealth cagrilirsa sonra ezer — ornek splitter yavrusu)
        maxHealth = _baseMaxHealth * Mathf.Lerp(1f, healthMultiplierAtMaxDifficulty, DifficultyManager.DifficultyFactor) * RunStats.EnemyHealthMult;
        currentHealth = maxHealth;

        if (rb != null) { rb.simulated = true; rb.linearVelocity = Vector2.zero; }
        if (bodyCollider != null) bodyCollider.enabled = true;
        if (spriteAnimator != null) spriteAnimator.enabled = true;

        _knockUntil = 0f;
        _pushStunUntil = 0f;
        HideDashTelegraph();

        // ORIJINAL renge don — SetTint (splitter yavrusu) baseColor'i kirletmis olabilir. Yesil dash dusman
        // havuzdan tekrar cikinca splitter renginde gelmesin.
        baseColor = _originalBaseColor;
        if (spriteRenderer != null)
        {
            spriteRenderer.color = baseColor;
            FlashFx.Clear(spriteRenderer);
            FlashFx.SetTint(spriteRenderer, baseColor);
        }
        if (laserVisualInstance != null) laserVisualInstance.Hide();

        CalculateNextAttackTime();
        _nextDashTime = Time.time + Random.Range(dashIntervalMin, dashIntervalMax);
    }

    protected virtual void Update()
    {
        if (isDying || vacuumed) return;
        if (playerTransform == null) return;
        if (_isDashing) return; // dash coroutine hareketi kontrol ediyor

        // Donmus: hareket/saldiri yok (velocity FixedUpdate'te de sifirlanir)
        if (EnemyFreeze.IsFrozen)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // Seyrek/kisa dash — SADECE normal dusman (elite lazer atar, dash atmaz)
        if (enableDash && !canUseLaser && Time.time >= _nextDashTime)
        {
            StartCoroutine(EnemyDashRoutine());
            return;
        }

        if (isAttacking)
        {
            rb.linearVelocity = Vector2.zero;
        }
        else
        {
            CalculateTrackingDirection();
            HandleAttackTiming();
        }
    }

    private void FixedUpdate()
    {
        if (isDying || vacuumed) return;
        if (_isDashing) return; // dash sirasinda velocity'yi coroutine yonetir
        if (EnemyFreeze.IsFrozen)
        {
            if (rb != null) rb.linearVelocity = Vector2.zero;
            return;
        }
        if (Time.time < _knockUntil) // silah vurusu ile itiliyor (knockback) — yon + sonme
        {
            if (rb != null)
            {
                float rem = _knockDur > 0f ? Mathf.Clamp01((_knockUntil - Time.time) / _knockDur) : 0f;
                rb.linearVelocity = _knockVel * rem; // sona dogru sonumlenir (yumusak sove)
            }
            return;
        }
        if (Time.time < _pushStunUntil) // dash ile itildi — kisa sure yerinde dur (duvar hissi)
        {
            if (rb != null) rb.linearVelocity = Vector2.zero;
            return;
        }
        MoveTowardsPlayer();
    }

    private float _pushStunUntil;
    private float _knockUntil;
    private float _knockDur;
    private Vector2 _knockVel;

    /// <summary>Dash ile itilince kisa sure yerinde dursun (sonra yurumeye devam) — 'duvari ittirme' hissi.</summary>
    public void ApplyPushStun(float duration)
    {
        if (duration > 0f) _pushStunUntil = Mathf.Max(_pushStunUntil, Time.time + duration);
    }

    /// <summary>Silah vurusu geri tepmesi: dusmani 'dir' yonunde 'speed' hiziyla 'duration' sure iter (sonumlenir).</summary>
    public void ApplyKnockback(Vector2 dir, float speed, float duration)
    {
        if (duration <= 0f || speed <= 0f || dir.sqrMagnitude < 0.0001f) return;
        _knockVel = dir.normalized * speed;
        _knockDur = duration;
        _knockUntil = Time.time + duration;
        _pushStunUntil = Mathf.Max(_pushStunUntil, _knockUntil + HitFreezeAfterKnockback); // geri gidince cok kisa DON (hit-stop hissi)
    }

    /// <summary>Knockback bitince dusmanin yerinde kalacagi cok kisa donma suresi (hit-stop hissi).</summary>
    private const float HitFreezeAfterKnockback = 0.06f;

    private bool _isDashing;
    private float _nextDashTime;

    /// <summary>Seyrek/kisa/duz dash: yon sec -> CHARGE (ok telegraph, dusman durur) -> tek yonde hizli kayma + hafif afterimage.</summary>
    private System.Collections.IEnumerator EnemyDashRoutine()
    {
        _isDashing = true;
        if (rb != null) rb.linearVelocity = Vector2.zero;

        // Yon: bazen oyuncuya, bazen rastgele — telegraph icin ONCE sec
        Vector2 dir;
        if (playerTransform != null && Random.value < dashAtPlayerChance)
            dir = ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;
        else
            dir = Random.insideUnitCircle.normalized;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;

        // CHARGE: ok telegraph goster, dusman durur (oyuncu tepki verebilsin)
        EnsureDashTelegraph();
        ShowDashTelegraph(dir);
        float c = 0f;
        while (c < dashChargeTime)
        {
            if (isDying) { HideDashTelegraph(); _isDashing = false; yield break; }
            if (rb != null) rb.linearVelocity = Vector2.zero;
            if (!EnemyFreeze.IsFrozen) c += Time.deltaTime; // donunca charge de duraklasin
            yield return null;
        }
        HideDashTelegraph();

        // DASH: secilen yonde kisa hizli kayma + hafif afterimage
        Vector2 startPos = transform.position;
        float aiTimer = 0f;
        while (Vector2.Distance(transform.position, startPos) < dashDistance)
        {
            if (isDying) break;
            if (EnemyFreeze.IsFrozen) { if (rb != null) rb.linearVelocity = Vector2.zero; yield return null; continue; }

            if (rb != null) rb.linearVelocity = dir * dashSpeed;

            if (leaveDashAfterImage && spriteRenderer != null)
            {
                aiTimer += Time.deltaTime;
                if (aiTimer >= dashAfterImageInterval)
                {
                    aiTimer = 0f;
                    AfterImage.Spawn(spriteRenderer, dashAfterImageColor, dashAfterImageAlpha, dashAfterImageFadeTime, spriteRenderer.sortingOrder - 1);
                }
            }
            yield return null;
        }

        if (rb != null) rb.linearVelocity = Vector2.zero;
        _isDashing = false;
        _nextDashTime = Time.time + Random.Range(dashIntervalMin, dashIntervalMax);
    }

    #region Dash Telegraph (ok)
    private LineRenderer _dashLine;
    private static Material _dashTelegraphMat; // tum dusmanlar paylasir (alloc/leak yok)

    /// <summary>Telegraph cizgisi + ucundaki ok'u (koddan mesh) bir kez olusturur (baslangicta gizli).</summary>
    private void EnsureDashTelegraph()
    {
        if (_dashLine != null) return;

        if (_dashTelegraphMat == null)
        {
            Shader sh = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            _dashTelegraphMat = new Material(sh);
        }
        int order = (spriteRenderer != null ? spriteRenderer.sortingOrder : 0) + 1;

        var lgo = new GameObject("DashTelegraph");
        lgo.transform.SetParent(transform, false);
        _dashLine = lgo.AddComponent<LineRenderer>();
        _dashLine.useWorldSpace = true;
        _dashLine.numCapVertices = 2;
        _dashLine.material = _dashTelegraphMat;
        _dashLine.startColor = _dashLine.endColor = dashTelegraphColor;
        _dashLine.startWidth = _dashLine.endWidth = dashTelegraphWidth;
        _dashLine.sortingOrder = order;
        _dashLine.positionCount = 2;
        _dashLine.enabled = false;
    }

    /// <summary>Telegraph'i verilen yonde gosterir (sadece cizgi — dusmandan dash hedefine).</summary>
    private void ShowDashTelegraph(Vector2 dir)
    {
        Vector2 origin = transform.position;
        Vector2 end = origin + dir * dashDistance;
        if (_dashLine != null) { _dashLine.enabled = true; _dashLine.SetPosition(0, origin); _dashLine.SetPosition(1, end); }
    }

    private void HideDashTelegraph()
    {
        if (_dashLine != null) _dashLine.enabled = false;
    }
    #endregion

    private void OnDestroy()
    {
        if (laserVisualInstance != null)
            Destroy(laserVisualInstance.gameObject);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (EnemyFreeze.IsFrozen) return; // donmusken temas hasari vermez
        if (Time.time < lastContactDamageTime + contactDamageCooldown) return;

        player cat = collision.gameObject.GetComponent<player>();
        if (cat == null) return;

        cat.TakeDamage(bodyContactDamage);
        lastContactDamageTime = Time.time;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (EnemyFreeze.IsFrozen) return; // donmusken temas hasari vermez
        if (Time.time < lastContactDamageTime + contactDamageCooldown) return;

        player cat = other.GetComponent<player>();
        if (cat == null) return;

        cat.TakeDamage(bodyContactDamage);
        lastContactDamageTime = Time.time;
    }
    #endregion

    #region Public Methods
    /// <summary>EnemyGenerator spawn'da cagirir: YEREL zorlugu ayarlar (0=taban, 1=tam), hiz + saldiri araligini yeniler.</summary>
    public void SetSpawnDifficulty(float factor01)
    {
        _spawnDifficulty = Mathf.Clamp01(factor01);
        _hasSpawnDifficulty = true;
        ComputeEffectiveMoveSpeed();
        CalculateNextAttackTime(); // ilk saldiri araligi da yerel zorluga gore (ilk cikinca seyrek)
    }

    /// <summary>Hareket hizini base'in KATI olarak ayarlar (ornek: splitter yavrulari hizlansin). ResetForSpawn base'e dondurur.</summary>
    public void SetSpeedMultiplier(float multiplier)
    {
        moveSpeed = (canUseLaser ? eliteMoveSpeed : _baseMoveSpeed) * Mathf.Max(0f, multiplier);
        ComputeEffectiveMoveSpeed();
    }

    /// <summary>Dash'i ac/kapa (ornek: splitter yavrulari dash atmasin). ResetForSpawn base'e dondurur (havuz kirlenmez).</summary>
    public void SetDashEnabled(bool enabled)
    {
        enableDash = enabled;
        if (!enabled) { _isDashing = false; HideDashTelegraph(); }
    }

    /// <summary>Düşmana hasar verir; can bitince ölüm tetiklenir.</summary>
    public void TakeDamage(float damageAmount)
    {
        if (isDying) return;

        currentHealth -= damageAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        TriggerHitFlash();
        DamagePopupManager.Show(transform.position, damageAmount);

        if (currentHealth <= 0f)
            Die(dropLoot: true);
    }
    #endregion

    #region Private Methods
    private void InitializeComponents()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        bodyCollider = GetComponent<Collider2D>();
        spriteAnimator = GetComponent<SpriteAnimator>();

        // Duvar kaymasi icin Obstacle layer'ina bakan cast filtresi (trigger'lari yok say).
        int obstacleLayer = LayerMask.NameToLayer("Obstacle");
        if (obstacleLayer >= 0)
        {
            _wallFilter = new ContactFilter2D { useLayerMask = true, layerMask = 1 << obstacleLayer, useTriggers = false };
            _wallFilterReady = true;
        }
    }

    private void InitializeHealthSystem()
    {
        // Can dışarıdan set edildiyse (ornek: splitter yavrusu) tekrar zorlukla olcekleme (cift-olcek olmasin).
        if (!_healthOverridden)
            maxHealth *= Mathf.Lerp(1f, healthMultiplierAtMaxDifficulty, DifficultyManager.DifficultyFactor) * RunStats.EnemyHealthMult;
        currentHealth = maxHealth;
    }

    private void CalculateNextAttackTime()
    {
        // Zorlukla saldiri araligi kisalir; base -> hard degerine Lerp. YEREL zorluk — ilk cikinca seyrek atar.
        float factor = EffectiveDifficulty;
        float scaledMin = Mathf.Lerp(minAttackCooldown, minAttackCooldownAtMaxDifficulty, factor);
        float scaledMax = Mathf.Lerp(maxAttackCooldown, maxAttackCooldownAtMaxDifficulty, factor);
        nextAttackTime = Time.time + Random.Range(scaledMin, scaledMax);
    }

    /// <summary>Hareket hizini spawn anindaki zorluga gore bir kez hesaplar (hafif hizlanma).</summary>
    private void ComputeEffectiveMoveSpeed()
    {
        float factor = EffectiveDifficulty;
        effectiveMoveSpeed = moveSpeed * Mathf.Lerp(1f, moveSpeedMultiplierAtMaxDifficulty, factor) * RunStats.EnemySpeedMult; // adaptif hiz (kill-rate)
    }

    private void DetermineEnemyVariation()
    {
        if (Random.value <= laserChance)
        {
            canUseLaser = true;
            moveSpeed = eliteMoveSpeed;
            if (spriteRenderer != null)
            {
                Color eliteColor;
                if (ColorUtility.TryParseHtmlString("#D46FE0", out eliteColor))
                {
                    spriteRenderer.color = eliteColor;
                    _originalBaseColor = eliteColor; // elite'in ORIJINAL rengi MOR (reset buna doner)
                }
            }
        }

        // baseColor = bu instance'in SU ANKI rengi (split child ise SetTint ile splitter rengi olabilir).
        // _originalBaseColor'a DOKUNMA — o prefab/elite rengi (Awake'te yakalandi), reset icin kullanilir.
        if (spriteRenderer != null)
        {
            baseColor = spriteRenderer.color;
            FlashFx.SetTint(spriteRenderer, baseColor); // gorsel tint _Color'dan gelir (URP 2D)
        }
    }

    private void SpawnLaserVisual()
    {
        if (!canUseLaser || laserVisualPrefab == null) return;

        laserVisualInstance = Instantiate(laserVisualPrefab);
        laserVisualInstance.SetSortingBehind(spriteRenderer != null ? spriteRenderer.sortingOrder : 0); // lazer dusmanin ARKASINDA
        laserVisualInstance.Hide();
    }

    private void HandleAttackTiming()
    {
        if (canUseLaser && Time.time >= nextAttackTime)
            StartCoroutine(LaserAttackRoutine());
    }

    private void FindTargetPlayer()
    {
        player targetPlayer = Object.FindFirstObjectByType<player>();
        if (targetPlayer != null)
            playerTransform = targetPlayer.transform;
    }

    private void CalculateTrackingDirection()
    {
        Vector2 direction = (playerTransform.position - transform.position);
        moveDirection = direction.normalized;
    }

    private void MoveTowardsPlayer()
    {
        if (isAttacking || playerTransform == null)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 dir = moveDirection;

        // DUVAR KAYMASI: onde duvar (Obstacle) varsa hizin duvara giren bileseni atilir -> duvar boyunca kayar
        // (pinlenmez). rb.Cast gercek collider sekliyle bakar. Tam dik gelirse oyuncunun oldugu tarafa tegetlenir.
        if (wallSlide && _wallFilterReady)
        {
            int hitCount = rb.Cast(dir, _wallFilter, _wallHits, wallProbeDistance);
            if (hitCount > 0)
            {
                Vector2 n = _wallHits[0].normal;
                Vector2 slide = dir - Vector2.Dot(dir, n) * n; // duvara paralel bilesen
                if (slide.sqrMagnitude > 0.02f)
                {
                    dir = slide.normalized;
                }
                else
                {
                    // Tam dik: duvar tegetini oyuncuya yakin yone cevir (duvari dolan)
                    Vector2 tangent = new Vector2(-n.y, n.x);
                    Vector2 toPlayer = (Vector2)playerTransform.position - rb.position;
                    if (Vector2.Dot(tangent, toPlayer) < 0f) tangent = -tangent;
                    dir = tangent;
                }
            }
        }

        rb.linearVelocity = dir * effectiveMoveSpeed;
    }

    /// <summary>Vurulunca kisa sure kirmizi flash yakar, sonra kendi rengine doner.</summary>
    private void TriggerHitFlash()
    {
        if (spriteRenderer == null) return;

        // Ust uste hasar gelirse onceki flash'i durdur ki kirmizida takili kalmasin
        if (hitFlashRoutine != null)
            StopCoroutine(hitFlashRoutine);

        hitFlashRoutine = StartCoroutine(HitFlashRoutine());
    }

    private System.Collections.IEnumerator HitFlashRoutine()
    {
        FlashFx.Set(spriteRenderer, hitFlashColor, 1f);
        yield return new WaitForSeconds(hitFlashDuration);
        FlashFx.Set(spriteRenderer, hitFlashColor, 0f);
        hitFlashRoutine = null;
    }

    private System.Collections.IEnumerator LaserAttackRoutine()
    {
        isAttacking = true;

        // Şarj başında yönü kilitle — charge ve ateş boyunca sabit kalır
        if (playerTransform != null)
            moveDirection = ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;

        if (laserVisualInstance != null)
        {
            laserVisualInstance.Show();
            laserVisualInstance.SetChargeMode(true);
        }

        // Zorlukla sarj (telegraph) suresi kisalir — alt sinir MinChargeDuration. YEREL zorluk (ilk cikinda uzun sarj).
        float factor = EffectiveDifficulty;
        float scaledCharge = Mathf.Max(MinChargeDuration, Mathf.Lerp(chargeDuration, chargeDurationAtMaxDifficulty, factor));

        float chargeTimer = 0f;
        while (chargeTimer < scaledCharge)
        {
            UpdateLaserVisual();
            chargeTimer += Time.deltaTime;
            yield return null;
        }

        // Ateş anında tam lazere geç - kalinlik da zorlukla buyur (bu atisa ozel, her seferinde yeniden hesaplanir)
        float widthMultiplier = Mathf.Lerp(1f, laserWidthMultiplierAtMaxDifficulty, factor);
        if (laserVisualInstance != null)
            laserVisualInstance.SetChargeMode(false, widthMultiplier);

        SfxManager.Play(SfxId.LaserFire); // lazer ateslendi

        // Lazer YERDE kaldigi laserDuration boyunca SUREKLI hasar: her laserTickInterval'da bir raycast + hasar.
        // Isin yonu sabit; oyuncu isinin icinden CIKARSA hasar durur, tekrar girerse devam eder.
        float tickInterval = laserTickInterval > 0.01f ? laserTickInterval : 0.4f; // 0 serialize tuzagina karsi
        float dmgWidth = (laserDamageWidth > 0.01f ? laserDamageWidth : 1f) * widthMultiplier; // gorsel genislige yakin + zorlukla buyur
        float activeTimer = 0f;
        float tickTimer = tickInterval; // >= interval -> ilk kare hemen vurur (ateslenir vurmaz)
        while (activeTimer < laserDuration)
        {
            UpdateLaserVisual(); // dusman hareket etse bile isin guncel kalir
            if (tickTimer >= tickInterval)
            {
                tickTimer = 0f;
                // Isin GENISLIGI kadar bir KUTU ile tara (ince raycast yerine) -> isinin icine sonradan giren de yer.
                Vector2 origin = transform.position;
                RaycastHit2D obs = Physics2D.Raycast(origin, moveDirection, Mathf.Infinity, obstacleLayers); // isin bir duvarda biter
                Vector2 endP = obs.collider != null ? obs.point : origin + moveDirection * 50f;
                float len = Vector2.Distance(origin, endP);
                if (len > 0.05f)
                {
                    Vector2 center = (origin + endP) * 0.5f;
                    float angleDeg = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
                    // TEK OverlapBox yerine ALL: Player layer'inda birden fazla collider var; tek donen collider
                    // AttackPoint'in trigger menzil dairesi olabilir (player component'i YOK) -> hasar kayboluyordu.
                    // Trigger'lari atla (sadece GOVDE isinin uzerindeyse vur), parent'tan player'i bul.
                    int hitCount = Physics2D.OverlapBoxNonAlloc(center, new Vector2(len, dmgWidth), angleDeg, _laserHitBuffer, playerLayer);
                    for (int hi = 0; hi < hitCount; hi++)
                    {
                        Collider2D hitP = _laserHitBuffer[hi];
                        if (hitP == null || hitP.isTrigger) continue; // menzil dairesi (trigger) degil, govde
                        player cat = hitP.GetComponentInParent<player>();
                        if (cat != null) { cat.TakeDamage(laserDamage); break; }
                    }
                }
            }
            activeTimer += Time.deltaTime;
            tickTimer += Time.deltaTime;
            yield return null;
        }

        if (laserVisualInstance != null)
            laserVisualInstance.Hide();

        CalculateNextAttackTime();
        isAttacking = false;
    }

    private void UpdateLaserVisual()
    {
        if (laserVisualInstance == null) return;

        RaycastHit2D hit = Physics2D.Raycast(transform.position, moveDirection, Mathf.Infinity, obstacleLayers);
        Vector2 endPoint = hit.collider != null
            ? hit.point
            : (Vector2)transform.position + moveDirection * 50f;

        laserVisualInstance.UpdateLaser(transform.position, endPoint);
    }

    /// <summary>
    /// Ultimate ekran-temizlemesi bu dusmani DROPSUZ ve ANINDA yok eder: core/ultFood birakmaz,
    /// olum animasyonu + duman OYNATMAZ (nuke temiz olsun). UltimateCinematic IMPACT aninda cagirir.
    /// </summary>
    public void Vaporize() { _noFoodDrop = true; _suppressDeathEffects = true; Die(dropLoot: true, playDeathAnim: false); } // nuke/ulti: core EVET, yemek + olum-efekti HAYIR

    private bool _noFoodDrop;          // nuke (bombardiman) ile olurse: ultFood YOK
    private bool _noCoreDrop;          // bomb rain/nuke ile olurse: core de BIRAKMAZ
    private bool _suppressDeathEffects; // nuke/ulti ile olurse: alt sinif olum efekti (patlama/bolunme) YOK
    private bool _healthOverridden;    // can disaridan set edildi (splitter yavrusu) -> zorlukla tekrar olcekleme

    /// <summary>Boss-oncesi bombardiman (nuke) bu dusmani oldururken cagirir: core birakir, ultFood + olum-efekti YOK.</summary>
    public void NukeKill(float damage)
    {
        _noFoodDrop = true;
        _noCoreDrop = true;           // bomb rain: core de birakma
        _suppressDeathEffects = true; // kitle temizliginde kamikaze zinciri / splitter cogalmasi olmasin
        TakeDamage(damage);
    }

    /// <summary>
    /// Olum aninda ALT SINIFLAR icin hook (kamikaze patlama, splitter bolunme). suppressed=true ise nuke/ulti
    /// ile olundu -> efekt uygulanmamali (zincir/cogalma engeli). Taban sinifta bos.
    /// </summary>
    protected virtual void OnDeath(bool suppressed) { }

    /// <summary>Alt siniflar icin: oyuncuyu bu dusmanla oldurmeden dusmani KENDISI oldurur (ornek: kamikaze patlamasi).</summary>
    /// <summary>Bu dusman olunce dusurecegi core miktari (elite/normal araligindan rastgele). Alt siniflar ozellestirir (SplitterEnemy son parca).</summary>
    protected virtual int GetCoreDropAmount()
    {
        return canUseLaser
            ? Random.Range(coreDropEliteMin, Mathf.Max(coreDropEliteMin, coreDropEliteMax) + 1)
            : Random.Range(coreDropNormal, Mathf.Max(coreDropNormal, coreDropNormalMax) + 1);
    }

    protected void KillSelf(bool dropLoot = true) => Die(dropLoot);

    /// <summary>Alt siniflarin oyuncu konumuna erismesi icin (kovaladigi hedef).</summary>
    protected Transform PlayerTransform => playerTransform;

    /// <summary>Bu dusmanin temel rengi (flash sonrasi donulen). Splitter yavrularini kendi rengine boyamak icin okur.</summary>
    public Color BaseColor => baseColor; // boss minion iz rengi disaridan da okunur
    public float CurrentHealth => currentHealth;   // boss can bari (SplitterEnemy boss toplam cani) icin

    /// <summary>Dis sistem (ornek: splitter) bu dusmanin rengini ayarlar — hem gorsel hem flash-donus rengi. Awake sonrasi cagrilmali.</summary>
    public void SetTint(Color c)
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) { spriteRenderer.color = c; FlashFx.SetTint(spriteRenderer, c); }
        baseColor = c;
    }

    /// <summary>Bu dusmanin GUNCEL max canini dondurur (zorlukla olceklenmis). Splitter yavru canini bundan hesaplar.</summary>
    protected float CurrentMaxHealth => maxHealth;

    /// <summary>Can'i dışarıdan set eder (Instantiate sonrasi, Start oncesi). Zorlukla yeniden olceklenmez.</summary>
    public void SetMaxHealth(float hp)
    {
        maxHealth = hp;
        currentHealth = hp;
        _healthOverridden = true;
    }

    /// <summary>
    /// Ultimate CHARGE fazi: dusmani "emilebilir" hale getirir — AI durur, fizik+collider kapanir
    /// (cekilirken temas hasari vermez), coroutine'ler kesilir. Olmez (isDying set edilmez); director
    /// transform'u oyuncuya ceker, IMPACT'te Vaporize ile silinir. Animator acik kalir (canli gorunur).
    /// </summary>
    public void BeginUltimateVacuum()
    {
        if (isDying || vacuumed) return;
        vacuumed = true;
        StopAllCoroutines();
        if (rb != null) { rb.linearVelocity = Vector2.zero; rb.simulated = false; }
        if (bodyCollider != null) bodyCollider.enabled = false;
        if (laserVisualInstance != null) laserVisualInstance.Hide();
    }

    private void Die(bool dropLoot, bool playDeathAnim = true)
    {
        if (isDying) return;
        isDying = true;

        if (dropLoot)
        {
            if (playDeathAnim) DifficultyManager.RegisterKill(); // adaptif can: normal (nuke/ulti degil) oyuncu olumu
            // Olum aninda core birak — hem normal hem elite ARALIKTAN rastgele (ust sinir min'e esitse sabit).
            // Random.Range(int, int) ust sinir HARIC oldugu icin +1. Mathf.Max: yanlis/eksik ayarda kilit onler.
            int coreAmount = GetCoreDropAmount();
            if (!_noCoreDrop) CoreManager.SpawnCores(transform.position, coreAmount);

            // Sansa bagli ultFood birak — dusmanin kendi rengiyle (olum animasyonuyla ayni renk)
            if (!_noFoodDrop && Random.value < RunStats.EffectiveFoodDropChance)
                UltimateManager.SpawnFood(transform.position, baseColor, 1);
        }

        OnDeath(_suppressDeathEffects); // alt sinif hook: kamikaze patlama / splitter bolunme (nuke/ulti'de bastirilir)

        // Olurken AI, hareket ve carpismalari durdur
        StopAllCoroutines();                    // devam eden lazer/flash coroutine'lerini kes
        _isDashing = false;
        HideDashTelegraph();                    // dash sirasinda olduyse telegraph ekranda kalmasin
        if (spriteAnimator != null)
            spriteAnimator.enabled = false;     // Kare oynaticiyi kapat — yoksa death frame'leri her kare ezer
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;               // fizikten cikar — ne itsin ne itilsin
        }
        if (bodyCollider != null)
            bodyCollider.enabled = false;       // artik temas hasari vermesin, icinden gecilebilsin
        if (laserVisualInstance != null)
            laserVisualInstance.Hide();

        // Nuke (Vaporize) icin ANINDA yok ol — death frame'leri/dumani oynatma.
        if (!playDeathAnim)
        {
            PoolManager.Despawn(gameObject); // Destroy yerine havuza
            return;
        }

        SfxManager.Play(SfxId.EnemyDeath); // olum animasyonu sesi (nuke'ta calmaz)
        StartCoroutine(DeathRoutine());
    }

    /// <summary>Olum animasyonunda oynatilacak kareler. Alt sinif override edebilir (ornek: kamikaze patlama kareleri).</summary>
    protected virtual Sprite[] GetDeathFrames() => deathFrames;

    private System.Collections.IEnumerator DeathRoutine()
    {
        // Flash yarim kalmis olabilir — rengi kendi rengine (elite ise mor) sifirla + flash miktarini kapat
        if (spriteRenderer != null)
        {
            spriteRenderer.color = baseColor;
            FlashFx.Clear(spriteRenderer);
        }

        Sprite[] frames = GetDeathFrames(); // alt sinif override edebilir (kamikaze patlama kareleri)
        if (frames != null && frames.Length > 0 && spriteRenderer != null)
        {
            float frameDuration = 1f / Mathf.Max(1f, deathFrameRate);
            for (int i = 0; i < frames.Length; i++)
            {
                spriteRenderer.sprite = frames[i];
                yield return new WaitForSeconds(frameDuration);
            }
        }

        PoolManager.Despawn(gameObject); // Destroy yerine havuza
    }
    #endregion
}

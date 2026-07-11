using UnityEngine;

public class EnemyController : MonoBehaviour
{
    #region Serialized Fields
    [Header("Yapay Zeka Ayarlari")]
    [SerializeField] private float moveSpeed = 3f;

    [Header("Can Ayarlari")]
    [SerializeField] private float maxHealth;

    [Header("Vurulma Flash Ayarlari")]
    [SerializeField] private Color hitFlashColor = Color.red;
    [SerializeField] private float hitFlashDuration = 0.08f;

    [Header("Olum Animasyonu Ayarlari")]
    [SerializeField] private Sprite[] deathFrames;
    [SerializeField] private float deathFrameRate = 12f;

    [Header("Lazer Saldiri Ayarlari")]
    [SerializeField] private float laserDamage = 2f; // 1 tam kalp = 2 yarim-kalp birimi

    [Header("Temas Hasari Ayarlari")]
    [SerializeField] private float bodyContactDamage = 1f;       // yarim kalp
    [SerializeField] private float contactDamageCooldown = 1f;   // saniyede max 1 kez vurur
    [SerializeField] private float chargeDuration = 1.5f;
    [SerializeField] private float laserDuration = 0.5f;
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

    [Header("Core Drop (FAZ 4)")]
    [Tooltip("Normal (lazersiz) dusman olunce dusen core sayisi.")]
    [SerializeField] private int coreDropNormal = 1;

    [Tooltip("Elite (lazerli) dusman olunce dusen core alt siniri (dahil).")]
    [SerializeField] private int coreDropEliteMin = 2;

    [Tooltip("Elite (lazerli) dusman olunce dusen core ust siniri (dahil).")]
    [SerializeField] private int coreDropEliteMax = 3;
    #endregion

    #region Private Fields
    private bool isAttacking = false;
    private float nextAttackTime;
    private bool canUseLaser = false;

    private LaserVisual laserVisualInstance;
    private Transform playerTransform;
    private Rigidbody2D rb;
    private Vector2 moveDirection;
    private float currentHealth;
    private SpriteRenderer spriteRenderer;
    private float lastContactDamageTime;
    private Color baseColor;
    private Coroutine hitFlashRoutine;
    private Collider2D bodyCollider;
    private Animator animator;
    private bool isDying;
    private float effectiveMoveSpeed;
    private const float MinChargeDuration = 0.05f; // sarj suresinin inebilecegi guvenli taban
    #endregion

    #region Unity Callbacks
    private void Start()
    {
        InitializeComponents();
        InitializeHealthSystem();
        CalculateNextAttackTime();
        DetermineEnemyVariation();
        ComputeEffectiveMoveSpeed();
        FindTargetPlayer();
        SpawnLaserVisual();
    }

    private void Update()
    {
        if (isDying) return;
        if (playerTransform == null) return;

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
        if (isDying) return;
        MoveTowardsPlayer();
    }

    private void OnDestroy()
    {
        if (laserVisualInstance != null)
            Destroy(laserVisualInstance.gameObject);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (Time.time < lastContactDamageTime + contactDamageCooldown) return;

        player cat = collision.gameObject.GetComponent<player>();
        if (cat == null) return;

        cat.TakeDamage(bodyContactDamage);
        lastContactDamageTime = Time.time;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (Time.time < lastContactDamageTime + contactDamageCooldown) return;

        player cat = other.GetComponent<player>();
        if (cat == null) return;

        cat.TakeDamage(bodyContactDamage);
        lastContactDamageTime = Time.time;
    }
    #endregion

    #region Public Methods
    /// <summary>Düşmana hasar verir; can bitince ölüm tetiklenir.</summary>
    public void TakeDamage(float damageAmount)
    {
        if (isDying) return;

        currentHealth -= damageAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        TriggerHitFlash();
        DamagePopupManager.Show(transform.position, damageAmount);

        if (currentHealth <= 0f)
            Die();
    }
    #endregion

    #region Private Methods
    private void InitializeComponents()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        bodyCollider = GetComponent<Collider2D>();
        animator = GetComponent<Animator>();
    }

    private void InitializeHealthSystem()
    {
        currentHealth = maxHealth;
    }

    private void CalculateNextAttackTime()
    {
        // Zorlukla saldiri araligi kisalir; base -> hard degerine Lerp (alt sinir = hard degerler).
        float factor = DifficultyManager.DifficultyFactor;
        float scaledMin = Mathf.Lerp(minAttackCooldown, minAttackCooldownAtMaxDifficulty, factor);
        float scaledMax = Mathf.Lerp(maxAttackCooldown, maxAttackCooldownAtMaxDifficulty, factor);
        nextAttackTime = Time.time + Random.Range(scaledMin, scaledMax);
    }

    /// <summary>Hareket hizini spawn anindaki zorluga gore bir kez hesaplar (hafif hizlanma).</summary>
    private void ComputeEffectiveMoveSpeed()
    {
        float factor = DifficultyManager.DifficultyFactor;
        effectiveMoveSpeed = moveSpeed * Mathf.Lerp(1f, moveSpeedMultiplierAtMaxDifficulty, factor);
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
                    spriteRenderer.color = eliteColor;
            }
        }

        // Flash sonrasi bu renge donulecek — elite ise mor, degilse varsayilan
        if (spriteRenderer != null)
            baseColor = spriteRenderer.color;
    }

    private void SpawnLaserVisual()
    {
        if (!canUseLaser || laserVisualPrefab == null) return;

        laserVisualInstance = Instantiate(laserVisualPrefab);
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

        rb.linearVelocity = moveDirection * effectiveMoveSpeed;
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
        spriteRenderer.color = hitFlashColor;
        yield return new WaitForSeconds(hitFlashDuration);
        spriteRenderer.color = baseColor;
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

        // Zorlukla sarj (telegraph) suresi kisalir — alt sinir MinChargeDuration.
        float factor = DifficultyManager.DifficultyFactor;
        float scaledCharge = Mathf.Max(MinChargeDuration, Mathf.Lerp(chargeDuration, chargeDurationAtMaxDifficulty, factor));

        float chargeTimer = 0f;
        while (chargeTimer < scaledCharge)
        {
            UpdateLaserVisual();
            chargeTimer += Time.deltaTime;
            yield return null;
        }

        // Ateş anında tam lazere geç
        if (laserVisualInstance != null)
            laserVisualInstance.SetChargeMode(false);

        UpdateLaserVisual();

        // Hasar uygula — engel ve player katmanlarini birlestirir
        LayerMask damageLayerMask = obstacleLayers | playerLayer;
        RaycastHit2D hit = Physics2D.Raycast(transform.position, moveDirection, Mathf.Infinity, damageLayerMask);
        if (hit.collider != null)
        {
            player cat = hit.collider.GetComponent<player>();
            if (cat != null)
                cat.TakeDamage(laserDamage);
        }

        yield return new WaitForSeconds(laserDuration);

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

    private void Die()
    {
        if (isDying) return;
        isDying = true;

        // Olum aninda core birak — elite ise araliktan rastgele, normal ise sabit.
        // Random.Range(int, int) ust sinir HARIC oldugu icin +1.
        int coreAmount = canUseLaser
            ? Random.Range(coreDropEliteMin, coreDropEliteMax + 1)
            : coreDropNormal;
        CoreManager.SpawnCores(transform.position, coreAmount);

        // Olurken AI, hareket ve carpismalari durdur
        StopAllCoroutines();                    // devam eden lazer/flash coroutine'lerini kes
        if (animator != null)
            animator.enabled = false;           // Animator'i kapat — yoksa death frame'leri her kare ezer
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;               // fizikten cikar — ne itsin ne itilsin
        }
        if (bodyCollider != null)
            bodyCollider.enabled = false;       // artik temas hasari vermesin, icinden gecilebilsin
        if (laserVisualInstance != null)
            laserVisualInstance.Hide();

        StartCoroutine(DeathRoutine());
    }

    private System.Collections.IEnumerator DeathRoutine()
    {
        // Flash yarim kalmis olabilir — rengi kendi rengine (elite ise mor) sifirla
        if (spriteRenderer != null)
            spriteRenderer.color = baseColor;

        if (deathFrames != null && deathFrames.Length > 0 && spriteRenderer != null)
        {
            float frameDuration = 1f / Mathf.Max(1f, deathFrameRate);
            for (int i = 0; i < deathFrames.Length; i++)
            {
                spriteRenderer.sprite = deathFrames[i];
                yield return new WaitForSeconds(frameDuration);
            }
        }

        Destroy(gameObject);
    }
    #endregion
}

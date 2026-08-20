using System.Collections;
using UnityEngine;

/// <summary>
/// 4. uzaylı tipi — oyuncuyla mesafe tutar, anlık konumuna bumerang fırlatır.
/// Bumerang fırlatılan noktanın etrafında dönüp bu düşmana geri döner (bkz. BoomerangProjectile).
/// </summary>
public class BoomerangEnemy : MonoBehaviour
{
    #region Serialized Fields
    [Header("Hareket Ayarlari")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float stopDistance = 4f;

    [Header("Can Ayarlari")]
    [SerializeField] private float maxHealth = 25f;

    [Header("Vurulma Flash Ayarlari")]
    [SerializeField] private Color hitFlashColor = Color.red;
    [SerializeField] private float hitFlashDuration = 0.08f;

    [Header("Olum Animasyonu Ayarlari")]
    [SerializeField] private Sprite[] deathFrames;
    [SerializeField] private float deathFrameRate = 12f;

    [Header("Temas Hasari")]
    [SerializeField] private float bodyContactDamage = 1f;
    [SerializeField] private float contactDamageCooldown = 1f;

    [Header("Bumerang Saldiri Ayarlari")]
    [SerializeField] private GameObject boomerangPrefab;
    [SerializeField] private float throwCooldown = 3f;
    [SerializeField] private float throwTelegraphDuration = 0.3f;

    [Tooltip("Oyuncu bu mesafeye girmeden saldiri sayaci baslamaz - spawn anda uzaktan atis yapmasin.")]
    [SerializeField] private float detectionRange = 8f;

    [Header("Renk Ayarlari")]
    [SerializeField] private Color enemyColor = new Color(0.9f, 0.5f, 0.1f); // turuncu varsayilan

    [Header("Zorluk Olceklendirme (FAZ 3)")]
    [Tooltip("Zorluk 1 iken atislar arasi bekleme. throwCooldown'dan kisa.")]
    [SerializeField] private float throwCooldownAtMaxDifficulty = 1.2f;

    [Tooltip("Zorluk 1 iken telegraph (hazirlik) suresi. throwTelegraphDuration'dan KISA ver.")]
    [SerializeField] private float throwTelegraphDurationAtMaxDifficulty = 0.15f;

    [Tooltip("Zorluk 1 iken hareket hizi carpani (1 = degismez, 1.15 = %15 hizli). Abartma.")]
    [SerializeField] private float moveSpeedMultiplierAtMaxDifficulty = 1.15f;

    [Header("Core Drop (FAZ 4)")]
    [Tooltip("Bu dusman olunce dusen core alt siniri (dahil).")]
    [SerializeField] private int coreDropMin = 2;

    [Tooltip("Bu dusman olunce dusen core ust siniri (dahil).")]
    [SerializeField] private int coreDropMax = 3;

    [Header("UltFood Drop (Ultimate)")]
    [Tooltip("Bu dusman olunce ultFood dusme ihtimali (0 = hic, 1 = her zaman).")]
    [Range(0f, 1f)] [SerializeField] private float ultFoodDropChance = 0.25f;
    #endregion

    #region Private Fields
    private Rigidbody2D _rb;
    private SpriteRenderer _spriteRenderer;
    private Transform _playerTransform;
    private float _currentHealth;
    private bool _isAttacking;
    private float _nextThrowTime;
    private float _lastContactDamageTime;
    private Coroutine _throwRoutine;
    private Coroutine _hitFlashRoutine;
    private Collider2D _bodyCollider;
    private Animator _animator;
    private bool _isDying;
    private float _effectiveMoveSpeed;
    private bool _hasDetectedPlayer;
    private const float MinTelegraphDuration = 0.05f; // telegraph suresinin inebilecegi guvenli taban
    #endregion

    #region Unity Callbacks
    private void Start()
    {
        _rb = GetComponent<Rigidbody2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _bodyCollider = GetComponent<Collider2D>();
        _animator = GetComponent<Animator>();
        _currentHealth = maxHealth;

        // Hareket hizini spawn anindaki zorluga gore bir kez hesapla (hafif hizlanma).
        _effectiveMoveSpeed = moveSpeed * Mathf.Lerp(1f, moveSpeedMultiplierAtMaxDifficulty, DifficultyManager.DifficultyFactor);

        if (_spriteRenderer != null)
            _spriteRenderer.color = enemyColor;

        player target = Object.FindFirstObjectByType<player>();
        if (target != null)
            _playerTransform = target.transform;
    }

    private void Update()
    {
        if (_isDying) return;
        if (_playerTransform == null || _isAttacking) return;

        if (!_hasDetectedPlayer)
        {
            float distToPlayer = Vector2.Distance(transform.position, _playerTransform.position);
            if (distToPlayer > detectionRange) return; // oyuncu henuz menzile girmedi - sayac baslamaz

            // Oyuncu ilk kez menzile girdi - sayac BURADAN baslar, spawn anindan degil
            _hasDetectedPlayer = true;
            _nextThrowTime = Time.time + throwCooldown;
        }

        if (Time.time >= _nextThrowTime)
            _throwRoutine = StartCoroutine(ThrowRoutine());
    }

    private void FixedUpdate()
    {
        if (_isDying) return;
        if (_playerTransform == null || _isAttacking)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        float dist = Vector2.Distance(transform.position, _playerTransform.position);
        if (dist > stopDistance)
        {
            Vector2 dir = ((Vector2)_playerTransform.position - (Vector2)transform.position).normalized;
            _rb.linearVelocity = dir * _effectiveMoveSpeed;
        }
        else
        {
            _rb.linearVelocity = Vector2.zero;
        }
    }

    private void OnDisable()
    {
        if (_throwRoutine != null)
        {
            StopCoroutine(_throwRoutine);
            _throwRoutine = null;
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (Time.time < _lastContactDamageTime + contactDamageCooldown) return;

        player cat = collision.gameObject.GetComponent<player>();
        if (cat == null) return;

        cat.TakeDamage(bodyContactDamage);
        _lastContactDamageTime = Time.time;
    }
    #endregion

    #region Public Methods
    /// <summary>Bu düşmana hasar verir; can bitince yok edilir.</summary>
    public void TakeDamage(float amount)
    {
        if (_isDying) return;

        _currentHealth -= amount;
        _currentHealth = Mathf.Clamp(_currentHealth, 0f, maxHealth);
        TriggerHitFlash();
        DamagePopupManager.Show(transform.position, amount);

        if (_currentHealth <= 0f)
            Die();
    }
    #endregion

    #region Private Methods
    private IEnumerator ThrowRoutine()
    {
        _isAttacking = true;

        // Zorlukla telegraph suresi kisalir - alt sinir MinTelegraphDuration.
        float factor = DifficultyManager.DifficultyFactor;
        float scaledTelegraph = Mathf.Max(MinTelegraphDuration, Mathf.Lerp(throwTelegraphDuration, throwTelegraphDurationAtMaxDifficulty, factor));
        yield return new WaitForSeconds(scaledTelegraph);

        if (_playerTransform != null && boomerangPrefab != null)
        {
            // Oyuncunun O ANKI konumunu oku ve kilitle - bumerang firlatildiktan sonra bu noktayi hedefler
            Vector2 targetPos = _playerTransform.position;

            GameObject boomerangObj = Instantiate(boomerangPrefab, transform.position, Quaternion.identity);
            BoomerangProjectile boomerang = boomerangObj.GetComponent<BoomerangProjectile>();
            if (boomerang != null)
                boomerang.Initialize(transform, targetPos);
        }

        _nextThrowTime = Time.time + Mathf.Lerp(throwCooldown, throwCooldownAtMaxDifficulty, factor);
        _throwRoutine = null;
        _isAttacking = false;
    }

    /// <summary>Vurulunca kisa sure kirmizi flash yakar, sonra kendi rengine doner.</summary>
    private void TriggerHitFlash()
    {
        if (_spriteRenderer == null) return;

        // Ust uste hasar gelirse onceki flash'i durdur ki kirmizida takili kalmasin
        if (_hitFlashRoutine != null)
            StopCoroutine(_hitFlashRoutine);

        _hitFlashRoutine = StartCoroutine(HitFlashRoutine());
    }

    private IEnumerator HitFlashRoutine()
    {
        _spriteRenderer.color = hitFlashColor;
        yield return new WaitForSeconds(hitFlashDuration);
        _spriteRenderer.color = enemyColor;
        _hitFlashRoutine = null;
    }

    private void Die()
    {
        if (_isDying) return;
        _isDying = true;

        // Olum aninda core birak - araliktan rastgele.
        // Random.Range(int, int) ust sinir HARIC oldugu icin +1.
        CoreManager.SpawnCores(transform.position, Random.Range(coreDropMin, coreDropMax + 1));

        // Sansa bagli ultFood birak — dusmanin kendi rengiyle (olum animasyonuyla ayni renk)
        if (Random.value < ultFoodDropChance)
            UltimateManager.SpawnFood(transform.position, enemyColor, 1);

        // Olurken AI, hareket ve carpismalari durdur
        StopAllCoroutines();                    // devam eden throw/flash coroutine'lerini kes
        if (_animator != null)
            _animator.enabled = false;          // Animator'i kapat - yoksa death frame'leri her kare ezer
        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.simulated = false;              // fizikten cikar - ne itsin ne itilsin
        }
        if (_bodyCollider != null)
            _bodyCollider.enabled = false;      // artik temas hasari vermesin, icinden gecilebilsin

        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        // Flash yarim kalmis olabilir - rengi kendi rengine sifirla
        if (_spriteRenderer != null)
            _spriteRenderer.color = enemyColor;

        if (deathFrames != null && deathFrames.Length > 0 && _spriteRenderer != null)
        {
            float frameDuration = 1f / Mathf.Max(1f, deathFrameRate);
            for (int i = 0; i < deathFrames.Length; i++)
            {
                _spriteRenderer.sprite = deathFrames[i];
                yield return new WaitForSeconds(frameDuration);
            }
        }

        Destroy(gameObject);
    }
    #endregion
}

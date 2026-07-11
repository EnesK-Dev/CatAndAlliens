using System.Collections;
using UnityEngine;

/// <summary>
/// 3. uzaylı tipi — oyuncunun anlık konumuna 3 mermi sıkır, aralara bekleme koyar.
/// Görsel olarak diğer uzaylılarla aynı prefab'ı kullanabilir; renk Inspector'dan ayarlanır.
/// </summary>
public class BurstShooterEnemy : MonoBehaviour
{
    #region Serialized Fields
    [Header("Hareket Ayarlari")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float stopDistance = 5f;

    [Header("Can Ayarlari")]
    [SerializeField] private float maxHealth = 30f;

    [Header("Vurulma Flash Ayarlari")]
    [SerializeField] private Color hitFlashColor = Color.red;
    [SerializeField] private float hitFlashDuration = 0.08f;

    [Header("Olum Animasyonu Ayarlari")]
    [SerializeField] private Sprite[] deathFrames;
    [SerializeField] private float deathFrameRate = 12f;

    [Header("Temas Hasari")]
    [SerializeField] private float bodyContactDamage = 1f;
    [SerializeField] private float contactDamageCooldown = 1f;

    [Header("Atış Ayarlari")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float shootCooldown = 4f;
    [SerializeField] private float delayBetweenShots = 0.4f;
    [SerializeField] private int burstCount = 3;

    [Header("Renk Ayarlari")]
    [SerializeField] private Color shooterColor = new Color(0.8f, 0.2f, 0.8f); // mor-pembe varsayılan

    [Header("Zorluk Olceklendirme (FAZ 3)")]
    [Tooltip("Zorluk 1 iken tek seride atilan mermi sayisi. burstCount'tan BUYUK ver (3->...->6 kademeli artar).")]
    [SerializeField] private int burstCountAtMaxDifficulty = 6;

    [Tooltip("Zorluk 1 iken seri ICI mermi arasi bekleme (saniye). delayBetweenShots'tan kisa.")]
    [SerializeField] private float delayBetweenShotsAtMaxDifficulty = 0.2f;

    [Tooltip("Zorluk 1 iken seriler ARASI bekleme (saniye). shootCooldown'dan kisa.")]
    [SerializeField] private float shootCooldownAtMaxDifficulty = 2f;

    [Tooltip("Zorluk 1 iken hareket hizi carpani (1 = degisme, 1.15 = %15 hizli). Abartma.")]
    [SerializeField] private float moveSpeedMultiplierAtMaxDifficulty = 1.15f;

    [Header("Core Drop (FAZ 4)")]
    [Tooltip("Bu dusman olunce dusen core alt siniri (dahil).")]
    [SerializeField] private int coreDropMin = 2;

    [Tooltip("Bu dusman olunce dusen core ust siniri (dahil).")]
    [SerializeField] private int coreDropMax = 3;
    #endregion

    #region Private Fields
    private Rigidbody2D _rb;
    private SpriteRenderer _spriteRenderer;
    private Transform _playerTransform;
    private float _currentHealth;
    private bool _isAttacking;
    private float _nextShootTime;
    private float _lastContactDamageTime;
    private Coroutine _burstRoutine;
    private Coroutine _hitFlashRoutine;
    private Collider2D _bodyCollider;
    private Animator _animator;
    private bool _isDying;
    private float _effectiveMoveSpeed;
    #endregion

    #region Unity Callbacks
    private void Start()
    {
        _rb = GetComponent<Rigidbody2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _bodyCollider = GetComponent<Collider2D>();
        _animator = GetComponent<Animator>();
        _currentHealth = maxHealth;
        _nextShootTime = Time.time + shootCooldown;

        // Hareket hizini spawn anindaki zorluga gore bir kez hesapla (hafif hizlanma).
        _effectiveMoveSpeed = moveSpeed * Mathf.Lerp(1f, moveSpeedMultiplierAtMaxDifficulty, DifficultyManager.DifficultyFactor);

        if (_spriteRenderer != null)
            _spriteRenderer.color = shooterColor;

        player target = Object.FindFirstObjectByType<player>();
        if (target != null)
            _playerTransform = target.transform;
    }

    private void Update()
    {
        if (_isDying) return;
        if (_playerTransform == null || _isAttacking) return;

        if (Time.time >= _nextShootTime)
            _burstRoutine = StartCoroutine(BurstRoutine());
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
        if (_burstRoutine != null)
        {
            StopCoroutine(_burstRoutine);
            _burstRoutine = null;
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
    private IEnumerator BurstRoutine()
    {
        _isAttacking = true;

        // Zorlugu seri basinda bir kez ornekle — sayi/bekleme bu seriye sabit uygulanir.
        float factor = DifficultyManager.DifficultyFactor;
        int shots = burstCount + Mathf.FloorToInt(factor * (burstCountAtMaxDifficulty - burstCount));
        shots = Mathf.Max(burstCount, shots); // guvenlik: max < base yanlis ayarlanirsa base'in altina inme
        float shotDelay = Mathf.Lerp(delayBetweenShots, delayBetweenShotsAtMaxDifficulty, factor);

        for (int i = 0; i < shots; i++)
        {
            if (_playerTransform == null) break;

            // Her ateşte oyuncunun O ANKİ konumunu oku — hareket ettiyse farklı yöne gider
            Vector2 targetPos = _playerTransform.position;

            if (bulletPrefab != null)
            {
                Vector2 dir = (targetPos - (Vector2)transform.position).normalized;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                Quaternion rotation = Quaternion.Euler(0f, 0f, angle);

                // Enemy collider dışından spawn et — içinde doğarsa physics çakışır
                Vector2 spawnPos = (Vector2)transform.position + dir * 0.8f;

                GameObject bulletObj = Instantiate(bulletPrefab, spawnPos, rotation);
                EnemyBullet bullet = bulletObj.GetComponent<EnemyBullet>();
                if (bullet != null)
                    bullet.Initialize(targetPos);
            }

            yield return new WaitForSeconds(shotDelay);
        }

        _nextShootTime = Time.time + Mathf.Lerp(shootCooldown, shootCooldownAtMaxDifficulty, factor);
        _burstRoutine = null;
        _isAttacking = false;
    }

    /// <summary>Vurulunca kisa sure kirmizi flash yakar, sonra shooterColor'a doner.</summary>
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
        _spriteRenderer.color = shooterColor;
        _hitFlashRoutine = null;
    }

    private void Die()
    {
        if (_isDying) return;
        _isDying = true;

        // Olum aninda core birak — araliktan rastgele.
        // Random.Range(int, int) ust sinir HARIC oldugu icin +1.
        CoreManager.SpawnCores(transform.position, UnityEngine.Random.Range(coreDropMin, coreDropMax + 1));

        // Olurken AI, hareket ve carpismalari durdur
        StopAllCoroutines();                    // devam eden burst/flash coroutine'lerini kes
        if (_animator != null)
            _animator.enabled = false;          // Animator'i kapat — yoksa death frame'leri her kare ezer
        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.simulated = false;              // fizikten cikar — ne itsin ne itilsin
        }
        if (_bodyCollider != null)
            _bodyCollider.enabled = false;      // artik temas hasari vermesin, icinden gecilebilsin

        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        // Flash yarim kalmis olabilir — rengi kendi rengine sifirla
        if (_spriteRenderer != null)
            _spriteRenderer.color = shooterColor;

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

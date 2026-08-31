using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss dusmani (mini-boss veya final boss). Yuksek can, yavas kovalama, temas hasari. Oyuncu
/// AoE saldirisiyla vurur (player.ApplyDamage boss'u tanir). Olunce bol core birakir ve
/// OnBossDefeated firlatir (final boss ise WinConditionManager kazanmayi tetikler).
/// Can degistikce OnBossHealthChanged -> ekran ustundeki boss can bari dinler.
///
/// FAZ 1: tanky kovalayici + can bari + odul. Saldirilar (lazer/burst/bomba) ve can-esikli
/// fazlar sonraki fazlarda eklenecek. Enemy davranis mimarisiyle tutarli (hit flash, death
/// routine, EnemyFreeze'e saygi). Gorsel SpriteAnimator ile (art sonradan takilir).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class BossController : MonoBehaviour
{
    #region Serialized Fields
    [Header("Kimlik")]
    [Tooltip("Final boss mu? true ise olunce oyunu KAZANDIRIR (WinConditionManager dinler). false = ara/mini boss.")]
    [SerializeField] private bool isFinalBoss = false;

    [Tooltip("Boss can barinda gosterilecek isim.")]
    [SerializeField] private string bossName = "BOSS";

    [Header("Can")]
    [SerializeField] private float maxHealth = 500f;

    [Header("Hareket")]
    [SerializeField] private float moveSpeed = 1.2f;

    [Tooltip("Oyuncuya bu mesafeden yakinsa durur (uzerine binmesin).")]
    [SerializeField] private float stopDistance = 1.5f;

    [Tooltip("true ise boss KOVALAMAZ; centerPoint'e gidip SABIT durur (lazer boss gibi).")]
    [SerializeField] private bool centerMode = false;

    [Tooltip("centerMode acikken boss'un oturacagi merkez nokta (dunya konumu).")]
    [SerializeField] private Vector2 centerPoint = Vector2.zero;

    [Header("Yon Cevirme")]
    [Tooltip("Boss oyuncuya gore saga/sola donsun mu (flipX).")]
    [SerializeField] private bool facePlayer = true;

    [Tooltip("Sprite VARSAYILAN olarak saga mi bakiyor? Boss ters donuyorsa bunu degistir.")]
    [SerializeField] private bool spriteFacesRight = true;

    [Header("Temas Hasari")]
    [SerializeField] private float bodyContactDamage = 2f;
    [SerializeField] private float contactDamageCooldown = 1f;

    [Header("Vurulma Flash")]
    [SerializeField] private Color hitFlashColor = Color.red;
    [SerializeField] private float hitFlashDuration = 0.08f;

    // Flash sonrasi donulecek "temel renk". Awake'te SpriteRenderer'in (Inspector'da verilen) renginden
    // okunur — boss'u tint'lemek icin SpriteRenderer > Color'a renk ver, kod onu ezmez.
    private Color baseColor = Color.white;

    [Header("Olum Animasyonu")]
    [SerializeField] private Sprite[] deathFrames;
    [SerializeField] private float deathFrameRate = 10f;

    [Header("Odul (Core)")]
    [SerializeField] private int coreDropMin = 20;
    [SerializeField] private int coreDropMax = 30;

    [Tooltip("Olunce oyuncuya doldurulacak can (yarim-kalp birimi: 2 = tam kalp). 0 = kapali.")]
    [SerializeField] private float healOnDeath = 0f;

    [Header("Dash Saldirisi")]
    [Tooltip("Boss telegraph'li dash saldirisi yapsin mi (kirmizi iz -> hizli dash).")]
    [SerializeField] private bool useDashAttack = true;

    [Tooltip("Iki saldiri arasi bekleme (saniye).")]
    [SerializeField] private float attackInterval = 4f;

    [Tooltip("Dash oncesi kirmizi iz (telegraph) suresi — bu surede kacabilirsin.")]
    [SerializeField] private float telegraphDuration = 0.85f;

    [Tooltip("Dash hizi (birim/sn) — cok hizli olmali.")]
    [SerializeField] private float dashSpeed = 20f;

    [Tooltip("Dash toplam mesafesi (birim).")]
    [SerializeField] private float dashDistance = 9f;

    [Tooltip("Zigzag pattern'de yanal sapma buyuklugu (birim).")]
    [SerializeField] private float zigzagAmplitude = 2.5f;

    [Tooltip("Dash yolu bu dikdortgene clamp'lenir — boss duvara girip sikismaz. Merkez + yari-boyut (dunya).")]
    [SerializeField] private Vector2 arenaCenter = Vector2.zero;
    [SerializeField] private Vector2 arenaHalfSize = new Vector2(20f, 18f);

    [Tooltip("Kirmizi telegraph izinin rengi.")]
    [SerializeField] private Color telegraphColor = new Color(1f, 0.12f, 0.12f, 0.85f);

    [Tooltip("Telegraph izinin kalinligi.")]
    [SerializeField] private float telegraphWidth = 0.35f;

    [Tooltip("Telegraph ucundaki kirmizi ucgen (ok ucu) boyutu. 0 = ucgen yok.")]
    [SerializeField] private float telegraphArrowSize = 0.9f;

    [Header("Afterimage (Sandevistan)")]
    [Tooltip("Dash sirasinda geride saydam hayalet klonlar biraksin mi.")]
    [SerializeField] private bool leaveAfterImages = true;

    [Tooltip("Kac saniyede bir hayalet birakilsin.")]
    [SerializeField] private float afterImageInterval = 0.045f;

    [Tooltip("Hayaletin solma suresi.")]
    [SerializeField] private float afterImageFadeTime = 0.35f;

    [Range(0f, 1f)]
    [Tooltip("Hayaletin baslangic saydamligi.")]
    [SerializeField] private float afterImageAlpha = 0.55f;

    [Tooltip("Hayalet rengi (Sandevistan icin mavi/turkuaz iyi durur).")]
    [SerializeField] private Color afterImageColor = new Color(0.3f, 0.8f, 1f, 1f);
    #endregion

    #region Private Fields
    private Rigidbody2D _rb;
    private SpriteRenderer _spriteRenderer;
    private SpriteAnimator _spriteAnimator;
    private Collider2D _bodyCollider;
    private Transform _playerTransform;
    private float _currentHealth;
    private float _lastContactDamageTime;
    private bool _isDying;
    private Coroutine _hitFlashRoutine;

    // Dash saldirisi
    private bool _isAttacking;
    private float _nextAttackTime;
    private LineRenderer _telegraph;   // kirmizi iz (runtime'da olusturulur)
    private Transform _telegraphArrow; // izin ucundaki kirmizi ucgen (ok ucu)
    private float _bossRadius = 1f;    // clamp icin (collider yaricapi * scale)

    // Canli boss sayaci (spawn pause icin)
    private static int _aliveCount;
    #endregion

    #region Static API
    /// <summary>Boss sahneye girince firlar (can bari bunu dinleyip gorunur olur). Parametre: boss.</summary>
    public static event Action<BossController> OnBossSpawned;

    /// <summary>Boss cani degisince firlar. Parametreler: guncel can, max can.</summary>
    public static event Action<float, float> OnBossHealthChanged;

    /// <summary>Boss olunce firlar. Parametre: final boss muydu (true ise kazanma tetiklenir).</summary>
    public static event Action<bool> OnBossDefeated;

    /// <summary>Can barinda gosterilecek isim.</summary>
    public string BossName => bossName;

    /// <summary>Bu boss final boss mu (olunce kazandirir mi).</summary>
    public bool IsFinalBoss => isFinalBoss;

    /// <summary>Sahnede su an canli boss var mi. EnemyGenerator normal spawn'i durdurmak icin kullanir.</summary>
    public static bool AnyBossAlive => _aliveCount > 0;

    /// <summary>Boss olum surecinde mi (attack modulleri bunu kontrol eder).</summary>
    public bool IsDying => _isDying;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _spriteAnimator = GetComponent<SpriteAnimator>();
        _bodyCollider = GetComponent<Collider2D>();
        _currentHealth = maxHealth;

        player p = FindFirstObjectByType<player>();
        if (p != null) _playerTransform = p.transform;

        if (_spriteRenderer != null) baseColor = _spriteRenderer.color; // Inspector'da verilen renk = temel renk

        // Boss yaricapi (dash clamp'i icin) — collider'dan
        if (_bodyCollider is CircleCollider2D cc)
            _bossRadius = cc.radius * Mathf.Max(Mathf.Abs(transform.localScale.x), Mathf.Abs(transform.localScale.y));

        _aliveCount++;          // canli boss say (spawn pause icin)
        CreateTelegraph();      // kirmizi iz cizgisini hazirla (baslangicta gizli)
    }

    private void Start()
    {
        // Gec enable olsa bile can bari baslangic durumunu alsin diye ilk event'leri firlat.
        OnBossSpawned?.Invoke(this);
        OnBossHealthChanged?.Invoke(_currentHealth, maxHealth);

        _nextAttackTime = Time.time + attackInterval; // ilk saldiri bir sure sonra
    }

    private void Update()
    {
        if (_isDying || !useDashAttack || _isAttacking) return;
        if (EnemyFreeze.IsFrozen || _playerTransform == null) return;

        if (Time.time >= _nextAttackTime)
        {
            StartCoroutine(DashAttackRoutine(UnityEngine.Random.value < 0.5f)); // duz/zigzag rastgele
        }
    }

    private void OnDestroy()
    {
        _aliveCount = Mathf.Max(0, _aliveCount - 1);
    }

    private void FixedUpdate()
    {
        if (_isDying) return;
        UpdateFacing(); // oyuncuya gore sag/sol don (donmus olsa bile gorsel)
        if (_isAttacking) return; // dash coroutine'i konumu kendi kontrol ediyor
        if (EnemyFreeze.IsFrozen)
        {
            if (_rb != null) _rb.linearVelocity = Vector2.zero;
            return;
        }

        if (centerMode) MoveToCenter(); // lazer boss: ortaya git, sabit dur
        else MoveTowardsPlayer();
    }

    private void OnCollisionStay2D(Collision2D collision) => TryContactDamage(collision.gameObject);
    private void OnTriggerStay2D(Collider2D other) => TryContactDamage(other.gameObject);
    #endregion

    #region Public Methods
    /// <summary>
    /// BossManager spawn'da cagirir: rengi (enemy tipinden alinan tint), final-boss durumunu ve
    /// (istege bagli) can'i ayarlar. Awake sonrasi cagrilir; baseColor'i tint yapar ki flash da ona donsun.
    /// </summary>
    /// <param name="tint">Boss gorsel rengi (beyaz = dogal). Awake'te yakalanan baseColor bununla ezilir.</param>
    /// <param name="isFinal">Final boss mu (olunce kazandirir).</param>
    /// <param name="maxHealthOverride">0 ise prefab varsayilani; >0 ise can bununla degisir.</param>
    public void Initialize(Color tint, bool isFinal, float maxHealthOverride = 0f)
    {
        isFinalBoss = isFinal;
        if (maxHealthOverride > 0f)
        {
            maxHealth = maxHealthOverride;
            _currentHealth = maxHealth;
        }
        baseColor = tint;
        if (_spriteRenderer != null) _spriteRenderer.color = tint;
    }

    /// <summary>Oyuncunun saldirisi cagirir (player.ApplyDamage boss'u tanir). Can azaltir, flash'lar, biterse olur.</summary>
    public void TakeDamage(float amount)
    {
        if (_isDying) return;

        _currentHealth -= amount;
        OnBossHealthChanged?.Invoke(Mathf.Max(0f, _currentHealth), maxHealth);
        DamagePopupManager.Show(transform.position, amount); // hasar sayisi (dusmanlarla ayni his)
        FlashHit();

        if (_currentHealth <= 0f)
            Die();
    }
    #endregion

    #region Private Methods
    /// <summary>Oyuncu sagda/soldaysa sprite'i o yone cevirir (flipX). Cok yakinsa titremesin diye degistirmez.</summary>
    private void UpdateFacing()
    {
        if (!facePlayer || _playerTransform == null || _spriteRenderer == null) return;

        float dx = _playerTransform.position.x - transform.position.x;
        if (Mathf.Abs(dx) < 0.05f) return; // neredeyse ayni x — bocalamayi onle

        bool playerOnRight = dx > 0f;
        // Sprite saga bakiyorsa: oyuncu sagda -> flip yok; solda -> flip. Sola bakiyorsa tersi.
        _spriteRenderer.flipX = spriteFacesRight ? !playerOnRight : playerOnRight;
    }

    /// <summary>centerMode: boss merkez noktaya gider ve orada sabit durur (kovalamaz).</summary>
    private void MoveToCenter()
    {
        Vector2 pos = transform.position;
        float d = Vector2.Distance(pos, centerPoint);
        if (d > 0.15f)
            _rb.linearVelocity = (centerPoint - pos).normalized * (moveSpeed * 3f); // merkeze biraz hizli gel
        else
            _rb.linearVelocity = Vector2.zero; // oturdu — sabit
    }

    private void MoveTowardsPlayer()
    {
        if (_playerTransform == null)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        float dist = Vector2.Distance(transform.position, _playerTransform.position);
        if (dist > stopDistance)
        {
            Vector2 dir = ((Vector2)_playerTransform.position - (Vector2)transform.position).normalized;
            _rb.linearVelocity = dir * moveSpeed;
        }
        else
        {
            _rb.linearVelocity = Vector2.zero;
        }
    }

    /// <summary>Kirmizi telegraph cizgisini runtime'da olusturur (child LineRenderer, baslangicta gizli).</summary>
    private void CreateTelegraph()
    {
        var go = new GameObject("Telegraph");
        go.transform.SetParent(transform, false);
        _telegraph = go.AddComponent<LineRenderer>();
        _telegraph.useWorldSpace = true;
        _telegraph.numCapVertices = 4;
        _telegraph.numCornerVertices = 4;

        // URP guvenli shader: once Sprites/Default, olmazsa alternatifler
        Shader sh = Shader.Find("Sprites/Default")
                    ?? Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Unlit/Color");
        var mat = new Material(sh);
        mat.color = telegraphColor;
        _telegraph.material = mat;

        _telegraph.startColor = telegraphColor;
        _telegraph.endColor = telegraphColor;
        _telegraph.startWidth = telegraphWidth;
        _telegraph.endWidth = telegraphWidth;
        _telegraph.sortingOrder = 3; // dusman/zemin ustunde
        _telegraph.enabled = false;

        // Iz ucundaki kirmizi ucgen (ok ucu) — koddan mesh (asset gerekmez)
        if (telegraphArrowSize > 0f)
        {
            var arrowGo = new GameObject("TelegraphArrow");
            arrowGo.transform.SetParent(transform, false);
            var mf = arrowGo.AddComponent<MeshFilter>();
            var mr = arrowGo.AddComponent<MeshRenderer>();

            float len = telegraphArrowSize;
            float halfW = telegraphArrowSize * 0.7f;
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(len, 0f, 0f),     // uc (ileri = +X)
                new Vector3(0f, halfW, 0f),   // taban ust
                new Vector3(0f, -halfW, 0f),  // taban alt
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 1 }; // cift tarafli (culling'e takilmasin)
            mesh.colors = new[] { telegraphColor, telegraphColor, telegraphColor };
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;

            mr.sharedMaterial = _telegraph.material; // ayni kirmizi material
            if (_spriteRenderer != null) mr.sortingLayerID = _spriteRenderer.sortingLayerID;
            mr.sortingOrder = 3;

            arrowGo.SetActive(false);
            _telegraphArrow = arrowGo.transform;
        }
    }

    /// <summary>Telegraph iz + ok ucunu gizler.</summary>
    private void HideTelegraph()
    {
        if (_telegraph != null) _telegraph.enabled = false;
        if (_telegraphArrow != null) _telegraphArrow.gameObject.SetActive(false);
    }

    /// <summary>Bir noktayi arena dikdortgenine clamp'ler (boss yaricapi kadar iceride) — dash duvara girmesin.</summary>
    private Vector3 ClampToArena(Vector3 p)
    {
        float mx = Mathf.Max(0f, arenaHalfSize.x - _bossRadius);
        float my = Mathf.Max(0f, arenaHalfSize.y - _bossRadius);
        p.x = Mathf.Clamp(p.x, arenaCenter.x - mx, arenaCenter.x + mx);
        p.y = Mathf.Clamp(p.y, arenaCenter.y - my, arenaCenter.y + my);
        return p;
    }

    /// <summary>
    /// Telegraph -> hizli dash -> afterimage. zigzag=false: oyuncuya DUZ cizgi dash. zigzag=true: yanal
    /// sapmali ZIGZAG yol. Dash sirasinda konumu bu coroutine kontrol eder (FixedUpdate hareketi duraklar).
    /// </summary>
    private IEnumerator DashAttackRoutine(bool zigzag)
    {
        _isAttacking = true;
        if (_rb != null) _rb.linearVelocity = Vector2.zero;

        // Yol noktalarini hesapla (dunya konumu) — oyuncuya dogru
        Vector2 start = transform.position;
        Vector2 dir = _playerTransform != null ? ((Vector2)_playerTransform.position - start) : Vector2.right;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
        dir.Normalize();

        var path = new List<Vector3> { start };
        if (!zigzag)
        {
            path.Add((Vector3)start + (Vector3)(dir * dashDistance));
        }
        else
        {
            Vector2 perp = new Vector2(-dir.y, dir.x);
            const int segs = 4;
            float segLen = dashDistance / segs;
            for (int i = 1; i <= segs; i++)
            {
                float lateral = (i == segs) ? 0f : ((i % 2 == 1) ? 1f : -1f) * zigzagAmplitude;
                path.Add((Vector3)start + (Vector3)(dir * (segLen * i) + perp * lateral));
            }
        }

        // Yolu arena icine clamp'le — boss duvara girip sikismaz
        for (int i = 0; i < path.Count; i++) path[i] = ClampToArena(path[i]);

        // Ok ucunun yonu: son segmentin yonu (dash'in gidecegi yon)
        Vector3 lastP = path[path.Count - 1];
        Vector2 arrowDir = (Vector2)(lastP - path[path.Count - 2]);
        if (arrowDir.sqrMagnitude < 0.0001f) arrowDir = dir;
        arrowDir.Normalize();
        float arrowAngle = Mathf.Atan2(arrowDir.y, arrowDir.x) * Mathf.Rad2Deg;

        // 1) TELEGRAPH: kirmizi iz boss'tan disariya UZAYARAK belirir, ucunda kirmizi ucgen
        _telegraph.positionCount = path.Count;
        _telegraph.enabled = true;
        float growDur = Mathf.Max(0.01f, telegraphDuration * 0.5f);
        float tEl = 0f;
        while (tEl < telegraphDuration)
        {
            if (_isDying) { HideTelegraph(); _isAttacking = false; yield break; }
            float g = Mathf.Clamp01(tEl / growDur);
            for (int i = 0; i < path.Count; i++)
                _telegraph.SetPosition(i, Vector3.Lerp(start, path[i], g));

            if (_telegraphArrow != null)
            {
                _telegraphArrow.gameObject.SetActive(true);
                _telegraphArrow.position = Vector3.Lerp(start, lastP, g); // izin ucunda
                _telegraphArrow.rotation = Quaternion.Euler(0f, 0f, arrowAngle);
            }

            tEl += Time.deltaTime;
            yield return null;
        }
        HideTelegraph();

        // 2) DASH: yol boyunca hizlica ilerle, arkada afterimage birak
        float aiTimer = 0f;
        for (int seg = 1; seg < path.Count; seg++)
        {
            Vector3 target = path[seg];
            // Guvenlik: beklenen sureden uzun surerse (bir sey takildiysa) segmenti bitir — sonsuz dongu/sikisma yok
            float segTimeout = (target - transform.position).magnitude / Mathf.Max(0.01f, dashSpeed) * 1.5f + 0.1f;
            float segT = 0f;
            while ((transform.position - target).sqrMagnitude > 0.0025f)
            {
                if (_isDying) { _isAttacking = false; yield break; }
                transform.position = Vector3.MoveTowards(transform.position, target, dashSpeed * Time.deltaTime);

                if (leaveAfterImages && _spriteRenderer != null)
                {
                    aiTimer += Time.deltaTime;
                    if (aiTimer >= afterImageInterval)
                    {
                        aiTimer = 0f;
                        AfterImage.Spawn(_spriteRenderer, afterImageColor, afterImageAlpha, afterImageFadeTime, _spriteRenderer.sortingOrder - 1);
                    }
                }

                segT += Time.deltaTime;
                if (segT > segTimeout) break; // takildi (duvar vb.) — bu segmenti bitir
                yield return null;
            }
        }

        // 3) bitti — normal kovalamaya don, cooldown
        if (_rb != null) _rb.linearVelocity = Vector2.zero;
        _isAttacking = false;
        _nextAttackTime = Time.time + attackInterval;
    }

    private void TryContactDamage(GameObject other)
    {
        if (_isDying || EnemyFreeze.IsFrozen) return;
        if (Time.time < _lastContactDamageTime + contactDamageCooldown) return;

        player cat = other.GetComponent<player>();
        if (cat == null) return;

        cat.TakeDamage(bodyContactDamage);
        _lastContactDamageTime = Time.time;
    }

    private void FlashHit()
    {
        if (_spriteRenderer == null) return;
        if (_hitFlashRoutine != null) StopCoroutine(_hitFlashRoutine);
        _hitFlashRoutine = StartCoroutine(HitFlashRoutine());
    }

    private IEnumerator HitFlashRoutine()
    {
        _spriteRenderer.color = hitFlashColor;
        yield return new WaitForSeconds(hitFlashDuration);
        _spriteRenderer.color = baseColor;
        _hitFlashRoutine = null;
    }

    private void Die()
    {
        if (_isDying) return;
        _isDying = true;

        StopAllCoroutines();
        _isAttacking = false;
        HideTelegraph();
        if (_spriteAnimator != null) _spriteAnimator.enabled = false; // death frame'leri her kare ezmesin
        if (_rb != null) { _rb.linearVelocity = Vector2.zero; _rb.simulated = false; }
        if (_bodyCollider != null) _bodyCollider.enabled = false;

        // Odul: bol core
        CoreManager.SpawnCores(transform.position, UnityEngine.Random.Range(coreDropMin, coreDropMax + 1));
        if (healOnDeath > 0f && _playerTransform != null)
        {
            player p = _playerTransform.GetComponent<player>();
            if (p != null) p.Heal(healOnDeath);
        }

        SfxManager.Play(SfxId.EnemyDeath);
        OnBossHealthChanged?.Invoke(0f, maxHealth); // bar sifirlansin
        OnBossDefeated?.Invoke(isFinalBoss);         // final boss ise kazanma tetiklenir

        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        if (deathFrames != null && deathFrames.Length > 0 && _spriteRenderer != null)
        {
            float frameTime = deathFrameRate > 0f ? 1f / deathFrameRate : 0.1f;
            for (int i = 0; i < deathFrames.Length; i++)
            {
                _spriteRenderer.sprite = deathFrames[i];
                yield return new WaitForSeconds(frameTime);
            }
        }
        Destroy(gameObject);
    }
    #endregion
}

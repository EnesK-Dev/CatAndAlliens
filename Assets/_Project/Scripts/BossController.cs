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

    [Header("Kite Modu (boss 3: merkezdeki dairede oyuncudan kac)")]
    [Tooltip("true ise boss oyuncudan KACAR ama harita merkezindeki dairesel alanda kalir (kenar/kose yok).")]
    [SerializeField] private bool kiteMode = false;
    [Tooltip("Kacis dairesinin merkezi (dunya konumu) — genelde harita ortasi.")]
    [SerializeField] private Vector2 kiteCenter = new Vector2(-1.5f, -0.5f);
    [Tooltip("Kacis dairesinin yaricapi — boss bu daireden disari cikmaz.")]
    [SerializeField] private float kiteRadius = 8f;

    [Tooltip("Kite (boss 3) HAREKET HIZI — oyuncudan kacma hizi. Sadece kiteMode acikken kullanilir.")]
    [SerializeField] private float kiteMoveSpeed = 3.5f;

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

    [Tooltip("Zigzag pattern'de yanal sapma buyuklugu (birim). (Eski alan — artik min/max araligi kullaniliyor.)")]
    [SerializeField] private float zigzagAmplitude = 2.5f;

    [Header("Dash Cesitliligi (rastgele patern)")]
    [Tooltip("Dash mesafesi bu aralikta rastgele — bazen yakina, bazen uzaga.")]
    [SerializeField] private float dashDistanceMin = 6f;
    [SerializeField] private float dashDistanceMax = 12f;

    [Tooltip("Zigzag yanal sapmasi bu aralikta rastgele — bazen daha genis kivrim.")]
    [SerializeField] private float zigzagAmplitudeMin = 1.5f;
    [SerializeField] private float zigzagAmplitudeMax = 4.5f;

    [Tooltip("Zigzag segment sayisi bu aralikta rastgele — bazen daha uzun/kivrimli zigzag.")]
    [SerializeField] private int zigzagSegmentsMin = 4;
    [SerializeField] private int zigzagSegmentsMax = 7;

    [Tooltip("Kafa atma (headbutt) dash'inde oyuncunun ne kadar OTESINE dalinsin (birim).")]
    [SerializeField] private float headbuttOvershoot = 3.5f;

    [Range(0f, 1f)]
    [Tooltip("Dash'in GERI CEKILME (retreat: oyuncudan uzaga) olma olasiligi.")]
    [SerializeField] private float retreatChance = 0.22f;

    [Range(0f, 1f)]
    [Tooltip("Retreat degilse: ZIGZAG yaklasma olasiligi; kalani DUZ kafa atma.")]
    [SerializeField] private float zigzagChance = 0.5f;

    [Tooltip("Dash yolu bu dikdortgene clamp'lenir — boss duvara girip sikismaz. Merkez + yari-boyut (dunya).")]
    [SerializeField] private Vector2 arenaCenter = Vector2.zero;
    [SerializeField] private Vector2 arenaHalfSize = new Vector2(20f, 18f);

    [Tooltip("Boss duvara ne kadar yaklasabilsin (dunya birimi). KUCUK = duvara daha cok yaklasir (oyuncu duvar kenarinda kolayca kacamaz/kampleyemez). Eskiden boss yaricapi kullaniliyordu, cok geride duruyordu.")]
    [SerializeField] private float wallClearance = 0.4f;

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

    #region Nested Types
    /// <summary>Dash saldiri paterni: duz kafa atma, kivrilarak yaklasma, ya da geri cekilme.</summary>
    private enum DashPattern { HeadbuttStraight, ZigzagApproach, Retreat }
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
    // Dis atak modulu (ornek: KamikazeBossAttack) atak yaparken hareketi durdurmak icin. Boss yururken atak
    // yapinca sabit kalsin diye modul bunu true/false yapar.
    private bool _externalAttacking;
    private float _nextAttackTime;
    private LineRenderer _telegraph;   // kirmizi iz (runtime'da olusturulur)
    private Transform _telegraphArrow; // izin ucundaki kirmizi ucgen (ok ucu)
    private float _bossRadius = 1f;    // clamp icin (collider yaricapi * scale)

    // Canli boss sayaci (spawn pause icin)
    private static int _aliveCount;

    // Hasar popup'i boss'un merkezinden bu kadar YUKARIDA cikar (buyuk sprite'ta gomulmesin).
    private const float PopupYOffset = 1.5f;
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

    /// <summary>Boss'un temel rengi (tint). Attack modulleri gorsel (ok vb.) icin boss rengini kullanir.</summary>
    public Color BaseColor => baseColor;

    /// <summary>
    /// Dis atak modulu (ornek: KamikazeBossAttack) atak yaparken TRUE der -> boss hareketi durur (yurumez).
    /// Atak bitince FALSE -> boss yine yurur. Boss'un kendi dash/lazer sistemine dokunmaz.
    /// </summary>
    public void SetExternalAttacking(bool value) => _externalAttacking = value;
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

        if (_spriteRenderer != null)
        {
            baseColor = _spriteRenderer.color; // Inspector'da verilen renk = temel renk
            FlashFx.SetTint(_spriteRenderer, baseColor); // gorsel tint _Color'dan gelir (URP 2D)
        }

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
            // Patern rastgele: uzaklas (retreat) / zigzag yaklas / duz kafa atma
            DashPattern pat;
            if (UnityEngine.Random.value < retreatChance) pat = DashPattern.Retreat;
            else if (UnityEngine.Random.value < zigzagChance) pat = DashPattern.ZigzagApproach;
            else pat = DashPattern.HeadbuttStraight;
            StartCoroutine(DashAttackRoutine(pat));
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
        if (_externalAttacking) // dis atak modulu (ornek: KamikazeBossAttack) atak yaparken TAM DUR
        {
            if (_rb != null) _rb.linearVelocity = Vector2.zero;
            return;
        }
        if (EnemyFreeze.IsFrozen)
        {
            if (_rb != null) _rb.linearVelocity = Vector2.zero;
            ClampInsideArena();
            return;
        }

        if (kiteMode) KiteMovement();       // burst boss: merkez dairede oyuncudan kac
        else if (centerMode) MoveToCenter(); // lazer boss: ortaya git, sabit dur
        else MoveTowardsPlayer();

        ClampInsideArena(); // arena disina TASMA — duvara takilma/sikisma engellenir
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

        // Beyaz tint = "dogal" -> prefab'in kendi SpriteRenderer rengini KORU (ornek: yesil boss).
        // Awake'te baseColor zaten prefab renginden okundu; sadece renkli bir tint verildiyse ez.
        bool tintIsNatural = tint.r > 0.99f && tint.g > 0.99f && tint.b > 0.99f && tint.a > 0.99f;
        if (!tintIsNatural)
        {
            baseColor = tint;
            if (_spriteRenderer != null) _spriteRenderer.color = tint;
        }
        if (_spriteRenderer != null) FlashFx.SetTint(_spriteRenderer, baseColor); // gorsel tint _Color'dan gelir (URP 2D)
    }

    /// <summary>Oyuncunun saldirisi cagirir (player.ApplyDamage boss'u tanir). Can azaltir, flash'lar, biterse olur.</summary>
    public void TakeDamage(float amount)
    {
        if (_isDying) return;

        _currentHealth -= amount;
        OnBossHealthChanged?.Invoke(Mathf.Max(0f, _currentHealth), maxHealth);
        DamagePopupManager.Show(transform.position + Vector3.up * PopupYOffset, amount); // hasar sayisi (dusmanlarla ayni his) — sprite ustunde
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

    /// <summary>
    /// Kite: oyuncudan KACAR, ama merkez daireden cikmaz. Daire kenarina yaklastikca yon merkeze dogru
    /// bukulur (kenar/koseye sikismaz). Boss ortada donerek oyuncudan uzaklasir.
    /// </summary>
    private void KiteMovement()
    {
        if (_playerTransform == null) { _rb.linearVelocity = Vector2.zero; return; }

        Vector2 pos = transform.position;
        Vector2 flee = pos - (Vector2)_playerTransform.position;
        flee = flee.sqrMagnitude > 0.0001f ? flee.normalized : UnityEngine.Random.insideUnitCircle.normalized;

        Vector2 toCenter = kiteCenter - pos;
        float d = toCenter.magnitude;
        float edge = Mathf.InverseLerp(kiteRadius * 0.6f, kiteRadius, d); // 0 = ic (kac), 1 = kenar (merkeze don)
        Vector2 dir = Vector2.Lerp(flee, d > 0.01f ? toCenter / d : flee, edge);
        if (dir.sqrMagnitude < 0.0001f) dir = flee;

        _rb.linearVelocity = dir.normalized * kiteMoveSpeed; // kite'a OZEL hiz (boss 3)
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

    /// <summary>Boss'un guncel konumunu arena dikdortgenine clamp'ler — normal harekette de disari tasmasin/sikismasin.</summary>
    private void ClampInsideArena()
    {
        Vector2 p = _rb != null ? _rb.position : (Vector2)transform.position;
        Vector2 c = ClampToArena(p);

        // Kite modu: ayrica MERKEZ DAIRESINE clamp (kenar/koseye kacamasin)
        if (kiteMode)
        {
            Vector2 fromCenter = c - kiteCenter;
            if (fromCenter.magnitude > kiteRadius)
                c = kiteCenter + fromCenter.normalized * kiteRadius;
        }

        if ((c - p).sqrMagnitude > 0.0000001f)
        {
            if (_rb != null) _rb.position = c;
            else transform.position = c;
        }
    }

    /// <summary>Bir noktayi arena dikdortgenine clamp'ler (boss yaricapi kadar iceride) — dash duvara girmesin.</summary>
    private Vector3 ClampToArena(Vector3 p)
    {
        // Eskiden _bossRadius kadar iceride tutuluyordu -> boss duvardan cok uzakta duruyordu, oyuncu kenarda kampliyordu.
        // Artik kucuk wallClearance kadar -> boss duvara yaklasip oyuncuyu kostebekleyebilir.
        float mx = Mathf.Max(0f, arenaHalfSize.x - wallClearance);
        float my = Mathf.Max(0f, arenaHalfSize.y - wallClearance);
        p.x = Mathf.Clamp(p.x, arenaCenter.x - mx, arenaCenter.x + mx);
        p.y = Mathf.Clamp(p.y, arenaCenter.y - my, arenaCenter.y + my);
        return p;
    }

    /// <summary>
    /// Telegraph -> hizli dash -> afterimage. zigzag=false: oyuncuya DUZ cizgi dash. zigzag=true: yanal
    /// sapmali ZIGZAG yol. Dash sirasinda konumu bu coroutine kontrol eder (FixedUpdate hareketi duraklar).
    /// </summary>
    private IEnumerator DashAttackRoutine(DashPattern pattern)
    {
        _isAttacking = true;
        if (_rb != null) _rb.linearVelocity = Vector2.zero;

        // Oyuncuya yon + mesafe
        Vector2 start = transform.position;
        Vector2 toPlayer = _playerTransform != null ? ((Vector2)_playerTransform.position - start) : Vector2.right;
        float distToPlayer = toPlayer.magnitude;
        Vector2 dir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.right;

        // Paterne gore: yon (yaklas/uzaklas), mesafe ve zigzag parametreleri rastgele
        bool zigzag = false;
        float dist;
        float amp = 0f;
        int segs = 1;
        switch (pattern)
        {
            case DashPattern.HeadbuttStraight: // oyuncunun icinden gec — kafa atma
                dist = distToPlayer + headbuttOvershoot;
                break;
            case DashPattern.Retreat:          // oyuncudan UZAGA dash
                dir = -dir;
                dist = UnityEngine.Random.Range(dashDistanceMin, dashDistanceMax);
                break;
            default:                           // ZigzagApproach — kivrilarak yaklas
                zigzag = true;
                dist = UnityEngine.Random.Range(dashDistanceMin, dashDistanceMax);
                amp = UnityEngine.Random.Range(zigzagAmplitudeMin, zigzagAmplitudeMax);
                segs = UnityEngine.Random.Range(zigzagSegmentsMin, zigzagSegmentsMax + 1);
                break;
        }
        dist = Mathf.Max(1f, dist);

        var path = new List<Vector3> { start };
        if (!zigzag)
        {
            path.Add((Vector3)start + (Vector3)(dir * dist));
        }
        else
        {
            Vector2 perp = new Vector2(-dir.y, dir.x);
            segs = Mathf.Max(2, segs);
            float segLen = dist / segs;
            for (int i = 1; i <= segs; i++)
            {
                float lateral = (i == segs) ? 0f : ((i % 2 == 1) ? 1f : -1f) * amp;
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
            if (_rb != null) _rb.linearVelocity = Vector2.zero; // charge sirasinda TAM DUR (yurumesin)
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
        FlashFx.Set(_spriteRenderer, hitFlashColor, 1f);
        yield return new WaitForSeconds(hitFlashDuration);
        FlashFx.Set(_spriteRenderer, hitFlashColor, 0f);
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
        FlashFx.Clear(_spriteRenderer); // yarim kalmis flash'i kapat (death frame'ler flash renginde gorunmesin)
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

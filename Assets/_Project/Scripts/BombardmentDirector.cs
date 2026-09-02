using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Boss ONCESI bombardiman olayini yonetir. Basladiginda enemy spawn durur (IsActive); mevcut dusmanlar
/// silinmez, bombalardan olur (alan temizlenir). Ucaklar (eliptik golge) rastgele desenlerde haritayi
/// tarar ve altlarina SIK bomba birakir: DUZ CIZGI (her yon: yatay/dikey/capraz), V DIZISI, ve oyuncu
/// USTUNDE DAIRE. Oyuncu bombardimanda hasar yemezse her rankRewardInterval saniyede +1 combo rank.
/// Sure bitince onFinished -> boss spawn olur. Tek sorumluluk: bombardiman (God manager degil).
/// Static erisim + event (CoreManager pattern). Bombalar ve ucaklar havuzlu (pooling).
/// </summary>
public class BombardmentDirector : MonoBehaviour
{
    #region Serialized Fields
    [Header("Referanslar")]
    [Tooltip("Bomba uyari/patlama prefab'i (BombWarning). Bos ise bombardiman calismaz.")]
    [SerializeField] private BombWarning bombWarningPrefab;

    [Tooltip("Bombardiman merkezi (oyuncu). Bos ise Awake'te bulunur.")]
    [SerializeField] private Transform followTarget;

    [Tooltip("Ucak golgesi (eliptik) sprite'i. Kod ile olcelenir; bos ise golge gorunmez ama bombalar duser.")]
    [SerializeField] private Sprite planeShadowSprite;

    [Header("Bombardiman Zamanlama")]
    [Tooltip("Bombardimanin toplam suresi (saniye) — bu sure boyunca ucaklar dalgalar halinde gelir.")]
    [SerializeField] private float duration = 7f;

    [Tooltip("Sure bitince son bombalarin patlamasi icin ek bekleme (saniye).")]
    [SerializeField] private float endLingerTime = 1.2f;

    [Tooltip("Iki ucak dalgasi arasi bekleme (saniye). Kucuk = daha yogun/kaotik (ust uste ucaklar).")]
    [SerializeField] private float waveInterval = 0.55f;

    [Tooltip("Oyuncu hasar yemezse KAC saniyede bir +1 combo rank kazanir.")]
    [SerializeField] private float rankRewardInterval = 3f;

    [Header("Alan (oyuncu etrafi, dunya birimi)")]
    [Tooltip("Bombardimanin kapladigi yari-genislik/yukseklik (oyuncu merkezli). Ekrani kaplamali.")]
    [SerializeField] private Vector2 areaHalfExtent = new Vector2(12f, 8f);

    [Tooltip("Ucak yolunun alan disina tasan ekstra uzunlugu (golge ekran disindan girip ciksin).")]
    [SerializeField] private float lineMargin = 4f;

    [Header("Ucak (Golge)")]
    [SerializeField] private float planeSpeed = 16f;
    [Tooltip("Ucagin altina KAC saniyede bir bomba biraktigi (kucuk = sik).")]
    [SerializeField] private float bombInterval = 0.18f;
    [Tooltip("V dizisinde kanat ucaklarinin yanal mesafesi.")]
    [SerializeField] private float vSpacing = 2.5f;
    [SerializeField] private Vector2 planeShadowSize = new Vector2(3.2f, 1.6f);
    [SerializeField] private Color planeShadowColor = new Color(0f, 0f, 0f, 0.28f);
    [SerializeField] private int planeSortingOrder = 6;
    [Tooltip("Her ucagin altina YAN YANA biraktigi 2 bomba arasi mesafe (dunya birimi). 0 = tek bomba.")]
    [SerializeField] private float bombSideGap = 0.9f;

    [Header("Swoop (oyuncu uzerinden yarim daire gecis)")]
    [Tooltip("Yay yaricapi bu aralikta rastgele — kucuk=keskin manevra, buyuk=genis/duz gecis.")]
    [SerializeField] private float swoopRadiusMin = 5f;
    [SerializeField] private float swoopRadiusMax = 9f;

    [Header("Bomba Hasar/Alan")]
    [SerializeField] private float explosionRadius = 1.1f;
    [SerializeField] private float bombWarningDuration = 0.6f;
    [SerializeField] private float bombExplosionDuration = 0.4f;
    [Tooltip("Bombanin OYUNCUYA hasari (vurursa).")]
    [SerializeField] private float playerBombDamage = 20f;
    [Tooltip("Bombanin DUSMANLARA hasari (alani temizlesin — normal dusmani 1-2 vurusta oldurmeli).")]
    [SerializeField] private float enemyBombDamage = 100f;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private LayerMask enemyLayers;

    [Header("Havuz")]
    [SerializeField] private int bombPoolSize = 40;

    [Header("TEST")]
    [Tooltip("Play'e girince bombardiman HEMEN baslasin (test). Yayinda KAPAT. Oyun icinde 'T' ile tekrar tetiklenir.")]
    [SerializeField] private bool playOnStartForTesting = false;
    #endregion

    #region Private Fields
    private static BombardmentDirector _instance;

    private readonly List<BombWarning> _bombPool = new List<BombWarning>();
    private readonly Queue<BombWarning> _bombAvailable = new Queue<BombWarning>();
    private readonly List<BombPlane> _planePool = new List<BombPlane>();

    private Coroutine _routine;
    private float _rankTimer;
    private Action _onFinished;
    private int _lastPattern = -1; // ayni deseni ust uste secme (okunur kalsin)
    #endregion

    #region Static API
    /// <summary>Bombardiman su an aktif mi. EnemyGenerator spawn'i durdurmak icin okur.</summary>
    public static bool IsActive { get; private set; }

    /// <summary>Bombardiman baslayinca firlar.</summary>
    public static event Action OnBombardmentStarted;

    /// <summary>Bombardiman bitince firlar (boss'tan hemen once).</summary>
    public static event Action OnBombardmentFinished;

    /// <summary>
    /// Bombardimani baslatir; bitince onFinished cagrilir (BossManager boss'u o an spawn eder).
    /// Director yoksa ya da zaten calisyorsa onFinished ANINDA cagrilir (boss beklemesin).
    /// </summary>
    public static void Trigger(Action onFinished)
    {
        if (_instance == null || IsActive || _instance.bombWarningPrefab == null)
        {
            onFinished?.Invoke();
            return;
        }
        _instance.Play(onFinished);
    }
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;

        if (followTarget == null)
        {
            player p = FindFirstObjectByType<player>();
            if (p != null) followTarget = p.transform;
        }

        BuildBombPool();
    }

    private void Start()
    {
        if (playOnStartForTesting) Play(null); // TEST: sahne acilinca bombardiman
    }

    private void Update()
    {
        // TEST: 'T' ile tekrar tetikle (yeni Input System; zaten calisyorsa yoksay)
        if (!IsActive && Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame) Play(null);
    }

    private void OnDestroy()
    {
        player.OnPlayerDamaged -= HandlePlayerDamaged; // calisyorken yok olursa abonelik sizmasin (-= guvenli)
        if (_instance == this) _instance = null;
        IsActive = false; // sahne degisince kilitli kalmasin
    }
    #endregion

    #region Public Methods
    /// <summary>Bombardimani baslatir (genelde Trigger uzerinden gelir).</summary>
    public void Play(Action onFinished)
    {
        _onFinished = onFinished;
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(RunBombardment());
    }
    #endregion

    #region Private Methods — Akis
    private IEnumerator RunBombardment()
    {
        IsActive = true;
        _rankTimer = 0f;
        player.OnPlayerDamaged += HandlePlayerDamaged; // hasar yerse odul sayaci sifirlanir
        OnBombardmentStarted?.Invoke();

        float t = 0f, waveT = 0f;
        LaunchRandomWave(); // ilk dalga hemen
        while (t < duration)
        {
            float dt = Time.deltaTime;

            // Odul: hasar yemeden gecen her rankRewardInterval icin +1 rank
            _rankTimer += dt;
            if (_rankTimer >= rankRewardInterval) { _rankTimer -= rankRewardInterval; ComboManager.GainRank(1); }

            // Dalga zamanlayici
            waveT += dt;
            if (waveT >= waveInterval) { waveT -= waveInterval; LaunchRandomWave(); }

            t += dt;
            yield return null;
        }

        // Son bombalar patlasin
        yield return new WaitForSeconds(endLingerTime);

        player.OnPlayerDamaged -= HandlePlayerDamaged;
        IsActive = false;
        _routine = null;
        OnBombardmentFinished?.Invoke();
        _onFinished?.Invoke(); // boss spawn
        _onFinished = null;
    }

    private void HandlePlayerDamaged(float amount) => _rankTimer = 0f; // hasar aldik -> temiz-gecis penceresi bastan

    /// <summary>
    /// TEK bir net formasyon firlatir (dalga = tek tip): DUZ SUPURME (paralel cizgiler, her yon) /
    /// V DIZISI (5 ucak) / DAIRE (oyuncu ustunde 2 ucak). Ayni desen ust uste secilmez — okunur kalsin.
    /// </summary>
    private void LaunchRandomWave()
    {
        int p = UnityEngine.Random.Range(0, 3);
        if (p == _lastPattern) p = (p + 1) % 3;
        _lastPattern = p;

        switch (p)
        {
            case 0: LaunchLineSweep(); break;   // paralel supurme (her yon)
            case 1: LaunchVFormation(); break;  // V dizisi
            default: LaunchSwoopWave(); break;  // oyuncu uzerinden yarim daire gecis
        }
    }

    /// <summary>DUZ SUPURME: bir yon secilir, AYNI yonde 3 PARALEL cizgi (bir duvar gibi suzulur). Yon rastgele (yatay/dikey/capraz).</summary>
    private void LaunchLineSweep()
    {
        float ang = RandomAngle();
        const int n = 3;
        float spacing = 4.5f;
        for (int i = 0; i < n; i++)
        {
            float off = (i - (n - 1) * 0.5f) * spacing; // -4.5, 0, +4.5
            LaunchLine(ang, off, 0f);
        }
    }

    /// <summary>V DIZISI: ayni yonde 5 ucak — lider onde, kanatlar yanal + GERIDE (net V).</summary>
    private void LaunchVFormation()
    {
        float ang = RandomAngle();
        LaunchLine(ang, 0f, 0f);                       // lider
        LaunchLine(ang, +vSpacing, vSpacing);          // sag ic
        LaunchLine(ang, -vSpacing, vSpacing);          // sol ic
        LaunchLine(ang, +vSpacing * 2f, vSpacing * 2f); // sag dis (daha geride)
        LaunchLine(ang, -vSpacing * 2f, vSpacing * 2f); // sol dis
    }

    /// <summary>DAIRE: oyuncu ustunde 2 ucak eliptik tur atar (net donen halka).</summary>
    /// <summary>SWOOP dalgasi: 1-2 ucak, disaridan gelip oyuncu uzerinden yarim daire manevrayla gecer (rastgele taraf/yon/yaricap).</summary>
    private void LaunchSwoopWave()
    {
        int n = UnityEngine.Random.Range(1, 3); // 1-2 ucak (biraz rastgelelik)
        for (int i = 0; i < n; i++) LaunchSwoop();
    }

    private void LaunchSwoop()
    {
        Vector2 pPos = Center();
        float R = UnityEngine.Random.Range(swoopRadiusMin, swoopRadiusMax);

        // Yayin buldugu taraf rastgele. Cember merkezi oyuncudan R uzakta -> cember oyuncudan GECER.
        float nAng = RandomAngle() * Mathf.Deg2Rad;
        Vector2 nrm = new Vector2(Mathf.Cos(nAng), Mathf.Sin(nAng));
        Vector2 cc = pPos + nrm * R;

        float playerAngle = Mathf.Atan2(-nrm.y, -nrm.x) * Mathf.Rad2Deg; // cc'den oyuncuya
        float dir = UnityEngine.Random.value < 0.5f ? 1f : -1f;          // donus yonu
        float startAngle = playerAngle - 90f * dir;                      // 180'lik yayin ORTASI oyuncudan gecsin
        float sweep = 180f * dir;

        BombPlane plane = GetPlane();
        plane.FlyArc(cc, R, startAngle, sweep, planeSpeed, bombInterval, bombSideGap, DropBomb, null);
    }

    private float RandomAngle() => UnityEngine.Random.Range(0f, 360f);

    private Vector2 Center() => followTarget != null ? (Vector2)followTarget.position : (Vector2)transform.position;

    /// <summary>Verilen acida, yanal ofsetli, alani boydan boya gecen bir cizgi ucusu baslatir.</summary>
    private void LaunchLine(float angleDeg, float lateralOffset, float startStagger)
    {
        Vector2 center = Center();
        float rad = angleDeg * Mathf.Deg2Rad;
        Vector2 d = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        Vector2 perp = new Vector2(-d.y, d.x);

        float half = Mathf.Max(areaHalfExtent.x, areaHalfExtent.y) + lineMargin;
        Vector2 mid = center + perp * lateralOffset;
        Vector2 start = mid - d * (half + startStagger);
        Vector2 end = mid + d * half;

        BombPlane plane = GetPlane();
        plane.FlyLine(start, end, planeSpeed, bombInterval, bombSideGap, DropBomb, null);
    }

    /// <summary>Ucagin altina bir bomba birakir (havuzdan).</summary>
    private void DropBomb(Vector2 pos)
    {
        BombWarning bomb = GetBomb();
        if (bomb == null) return; // havuz bos — bu bombayi atla
        bomb.Activate(pos);       // parametreler havuz kurulumunda bir kez ayarlandi
    }
    #endregion

    #region Private Methods — Havuzlar
    private void BuildBombPool()
    {
        if (bombWarningPrefab == null) return;
        for (int i = 0; i < bombPoolSize; i++)
        {
            BombWarning b = Instantiate(bombWarningPrefab, transform);
            // Parametreleri BIR KEZ ayarla (hepsi sabit) — her drop'ta yeniden Initialize + closure alloc olmasin
            b.Initialize(bombWarningDuration, bombExplosionDuration, explosionRadius, playerBombDamage, playerLayer, MakeReturn(b));
            b.ConfigureEnemyDamage(enemyLayers, enemyBombDamage);
            b.gameObject.SetActive(false);
            _bombPool.Add(b);
            _bombAvailable.Enqueue(b);
        }
    }

    /// <summary>Bombanin pool'a donus delegesi (bir kez uretilir, closure alloc havuz kurulumunda kalir).</summary>
    private Action MakeReturn(BombWarning b) => () => _bombAvailable.Enqueue(b);

    private BombWarning GetBomb() => _bombAvailable.Count > 0 ? _bombAvailable.Dequeue() : null;

    private BombPlane GetPlane()
    {
        foreach (var p in _planePool)
            if (p != null && !p.gameObject.activeSelf) return p;

        var go = new GameObject("BombPlane");
        go.transform.SetParent(transform);
        go.AddComponent<SpriteRenderer>();
        var plane = go.AddComponent<BombPlane>();
        plane.Configure(planeShadowSprite, planeSortingOrder, planeShadowColor, planeShadowSize);
        go.SetActive(false);
        _planePool.Add(plane);
        return plane;
    }
    #endregion
}

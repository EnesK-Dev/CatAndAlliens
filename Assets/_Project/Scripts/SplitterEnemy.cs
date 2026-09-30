using UnityEngine;

/// <summary>
/// Splitter dusman: oyuncuyu kovalar (EnemyController tabani) ve OLUNCE splitCount kucuk parcaya bolunur
/// (splitChildPrefab). Nuke/ulti ile olurse bolunmez (kitle temizligini bozmasin — suppressed). Cocuklar
/// genelde BOLUNMEYEN kucuk bir dusman prefab'i olmali (sonsuz bolunme olmasin). AoE silahlari odullendirir.
/// </summary>
public class SplitterEnemy : EnemyController
{
    #region Serialized Fields
    [Header("Splitter Bolunme")]
    [Tooltip("Olunce uretilecek kucuk parca prefab'i (BOLUNMEYEN bir dusman olmali).")]
    [SerializeField] private GameObject splitChildPrefab;

    [Tooltip("Bir olumde kac parcaya bolunsun.")]
    [SerializeField] private int splitCount = 2;

    [Tooltip("Kac KUSAK bolunebilir. 1 = bir kez boluner (normal splitter). Boss ornek: 4 -> 1>2>4>8>16 sonra oluler.")]
    [SerializeField] private int splitDepth = 1;

    [Tooltip("Yavrunun cani = parent cani * bu (0.55 = %55). Her kusak zayiflar. 0 = degistirme (parent prefab cani).")]
    [SerializeField] private float childHealthFactor = 0.55f;

    [Tooltip("Cocuklara uygulanan olcek carpani (kucuk gorunsun, her kusakta birikir).")]
    [SerializeField] private float childScale = 0.6f;

    [Tooltip("Cocuklarin dogum noktasi sacilimi (dunya birimi).")]
    [SerializeField] private float spawnSpread = 0.6f;

    [Tooltip("Yavrularin hiz carpani (parent base hizina gore). 1.3 = %30 hizli. Split cocuklari biraz hizlansin.")]
    [SerializeField] private float childSpeedMultiplier = 1.3f;

    [Tooltip("SPLITTER BOSS soyu mu? Aciksa tum parcalar (1+2+4+8+16) olene kadar oyun ilerlemesi DURUR (boss dovusu). Normal splitter'da KAPALI.")]
    [SerializeField] private bool isBossLineage = false;

    [Header("Boss Son Parca Odulu (sadece boss soyu + splitDepth=0)")]
    [Tooltip("SADECE boss soyunun SON parcasi (artik bolunmeyen) bu kadar core dusurur (yuksek odul; odul sona toplanir). <=0 ise 15.")]
    [SerializeField] private int finalPieceCoreMin = 15;
    [SerializeField] private int finalPieceCoreMax = 20;
    #endregion

    #region Boss Soyu (dovus takibi)
    private static int _lineageAlive;

    /// <summary>Splitter boss soyundan CANLI parca var mi. Difficulty/EnemyGenerator ilerlemeyi durdurmak icin okur.</summary>
    public static bool BossLineageAlive => _lineageAlive > 0;

    /// <summary>Bu parca boss soyundan mi (bombardiman onu OLDURMEMELI — spawn ani havada kalan bombaya kurban gitmesin).</summary>
    public bool IsBossLineage => isBossLineage;

    protected override void OnEnable() { base.OnEnable(); if (isBossLineage) _lineageAlive++; }
    protected override void OnDisable() { base.OnDisable(); if (isBossLineage) _lineageAlive = Mathf.Max(0, _lineageAlive - 1); }
    #endregion

    #region Pool Reset
    private int _baseSplitDepth;

    /// <summary>Base splitDepth'i yakala (havuz reset'i icin) — SADECE ilk Instantiate.</summary>
    protected override void Awake()
    {
        base.Awake();
        _baseSplitDepth = splitDepth;
    }

    /// <summary>Havuzdan yeniden kullanimda splitDepth'i base'e dondur (birikmesin/0'da kalmasin). SetSplitDepth sonra ezebilir.</summary>
    protected override void ResetForSpawn()
    {
        base.ResetForSpawn();
        splitDepth = _baseSplitDepth;
    }
    #endregion

    #region Public Methods
    /// <summary>Kalan bolunme kusagi (parent yavrusuna verir). 0'a inince yavru artik bolunmez.</summary>
    public void SetSplitDepth(int depth) => splitDepth = depth;
    #endregion

    #region Overrides
    /// <summary>Core drop: boss soyu + SON parca (splitDepth<=0, artik bolunmez) -> yuksek odul; digerleri taban.</summary>
    protected override int GetCoreDropAmount()
    {
        if (isBossLineage && splitDepth <= 0)
        {
            int min = finalPieceCoreMin > 0 ? finalPieceCoreMin : 15;                 // 0 serialize tuzagina karsi
            return Random.Range(min, Mathf.Max(min, finalPieceCoreMax) + 1);
        }
        return base.GetCoreDropAmount();
    }

    /// <summary>Olunce kucuk parcalara bolunur. Nuke/ulti ile olduysa (suppressed) bolunmez.</summary>
    protected override void OnDeath(bool suppressed)
    {
        if (suppressed || splitChildPrefab == null || splitCount <= 0 || splitDepth <= 0) return;

        SfxManager.Play(SfxId.SplitterSplit); // bolunme sesi

        for (int i = 0; i < splitCount; i++)
        {
            Vector2 offset = Random.insideUnitCircle * spawnSpread;
            GameObject child = PoolManager.Spawn(splitChildPrefab, (Vector2)transform.position + offset, Quaternion.identity); // havuzdan
            if (!Mathf.Approximately(childScale, 1f)) child.transform.localScale *= childScale;

            var ec = child.GetComponent<EnemyController>();
            if (ec != null)
            {
                ec.SetTint(BaseColor);                                          // parent rengini al (yesil olmasin)
                if (childHealthFactor > 0f) ec.SetMaxHealth(CurrentMaxHealth * childHealthFactor); // her kusak zayifla
                ec.SetDashEnabled(false);                                       // split cocuklari DASH ATMASIN — sadece bolunup kovalasin
                ec.SetSpeedMultiplier(childSpeedMultiplier);                    // biraz hizli olsunlar
            }

            // Yavru da splitter ise: bir sonraki kusak bir kez daha az bolunur (1>2>4>8>16 sonra durur)
            var sp = child.GetComponent<SplitterEnemy>();
            if (sp != null) sp.SetSplitDepth(splitDepth - 1);
        }
    }
    #endregion
}

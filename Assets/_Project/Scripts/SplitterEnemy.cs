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
    #endregion

    #region Public Methods
    /// <summary>Kalan bolunme kusagi (parent yavrusuna verir). 0'a inince yavru artik bolunmez.</summary>
    public void SetSplitDepth(int depth) => splitDepth = depth;
    #endregion

    #region Overrides
    /// <summary>Olunce kucuk parcalara bolunur. Nuke/ulti ile olduysa (suppressed) bolunmez.</summary>
    protected override void OnDeath(bool suppressed)
    {
        if (suppressed || splitChildPrefab == null || splitCount <= 0 || splitDepth <= 0) return;

        SfxManager.Play(SfxId.SplitterSplit); // bolunme sesi

        for (int i = 0; i < splitCount; i++)
        {
            Vector2 offset = Random.insideUnitCircle * spawnSpread;
            GameObject child = Instantiate(splitChildPrefab, (Vector2)transform.position + offset, Quaternion.identity);
            if (!Mathf.Approximately(childScale, 1f)) child.transform.localScale *= childScale;

            var ec = child.GetComponent<EnemyController>();
            if (ec != null)
            {
                ec.SetTint(BaseColor);                                          // parent rengini al (yesil olmasin)
                if (childHealthFactor > 0f) ec.SetMaxHealth(CurrentMaxHealth * childHealthFactor); // her kusak zayifla
            }

            // Yavru da splitter ise: bir sonraki kusak bir kez daha az bolunur (1>2>4>8>16 sonra durur)
            var sp = child.GetComponent<SplitterEnemy>();
            if (sp != null) sp.SetSplitDepth(splitDepth - 1);
        }
    }
    #endregion
}

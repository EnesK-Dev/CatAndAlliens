using System;
using UnityEngine;

/// <summary>
/// Cizgi uzerinde dusman spawn eder. Zorluk (DifficultyManager) ile bagli:
/// spawn araligi DifficultyFactor ile kisalir (seyrek -> sik) ve her dusman
/// tipi kendi unlockMilestone'una ulasilinca havuza girer. Tip secimi weighted
/// random; agirliklar DifficultyFactor ile buyur (elite/burst zamanla siklasir).
/// Enemy class'larina dokunmaz — sadece spawn tarafi.
/// </summary>
public class EnemyGenerator : MonoBehaviour
{
    #region Nested Types
    /// <summary>
    /// Tek bir dusman tipinin spawn tanimi. Prefab + hangi milestone'da acilacagi +
    /// zorlukla artan agirlik araligi.
    /// </summary>
    [Serializable]
    public class EnemySpawnEntry
    {
        [Tooltip("Spawn edilecek prefab (normal / elite / burst vb.).")]
        public GameObject prefab;

        [Tooltip("Bu tip, DifficultyManager'da bu milestone index'ine ulasilinca havuza girer. 0 = bastan acik.")]
        public int unlockMilestone = 0;

        [Tooltip("Zorluk 0 iken (oyun basi) secilme agirligi.")]
        public float baseWeight = 1f;

        [Tooltip("Zorluk 1 iken (maksimum) secilme agirligi. baseWeight'ten buyuk verirsen tip zamanla siklasir.")]
        public float maxWeight = 1f;
    }
    #endregion

    #region Serialized Fields
    [Header("Spawn Girisleri")]
    [Tooltip("Dusman tipleri. Ornek: [0] normal unlock=0, [1] elite unlock=1, [2] burst unlock=2.")]
    [SerializeField] private EnemySpawnEntry[] spawnEntries;

    [Header("Spawn Araligi (saniye)")]
    [Tooltip("Zorluk 0 iken iki spawn arasi bekleme (seyrek). Lerp'in sol ucu.")]
    [SerializeField] private float maxSpawnInterval = 3f;

    [Tooltip("Zorluk 1 iken iki spawn arasi bekleme (sik). Lerp'in sag ucu.")]
    [SerializeField] private float minSpawnInterval = 0.6f;

    [Tooltip("Her araliga eklenen rastgele +/- sapma (mekaniklik kirmak icin).")]
    [SerializeField] private float spawnIntervalJitter = 0.3f;

    [Header("Spawn Cizgisi")]
    [Tooltip("Cizginin toplam uzunlugu.")]
    [SerializeField] private float spawnLineLength = 5f;

    [Tooltip("Cizgi dikey (Y) mi yoksa yatay (X) mi uzansin.")]
    [SerializeField] private bool isVerticalLine = false;
    #endregion

    #region Private Fields
    private const float IntervalFloor = 0.05f; // araligin altina inemeyecegi guvenli taban
    private float _nextSpawnTime;
    #endregion

    #region Unity Callbacks
    private void Start()
    {
        ScheduleNextSpawn();
    }

    private void Update()
    {
        if (Time.time >= _nextSpawnTime)
        {
            SpawnEnemyOnLine();
            ScheduleNextSpawn();
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        float halfLength = spawnLineLength / 2f;

        if (isVerticalLine)
            Gizmos.DrawLine(transform.position + new Vector3(0f, -halfLength, 0f), transform.position + new Vector3(0f, halfLength, 0f));
        else
            Gizmos.DrawLine(transform.position + new Vector3(-halfLength, 0f, 0f), transform.position + new Vector3(halfLength, 0f, 0f));
    }
    #endregion

    #region Private Methods
    /// <summary>Zorluga gore bir sonraki spawn zamanini hesaplar (seyrek -> sik) ve jitter ekler.</summary>
    private void ScheduleNextSpawn()
    {
        float factor = DifficultyManager.DifficultyFactor;
        float interval = Mathf.Lerp(maxSpawnInterval, minSpawnInterval, factor);
        interval += UnityEngine.Random.Range(-spawnIntervalJitter, spawnIntervalJitter);
        interval = Mathf.Max(IntervalFloor, interval);
        _nextSpawnTime = Time.time + interval;
    }

    /// <summary>Cizgi uzerinde rastgele bir noktaya, weighted random ile secilen tipi spawn eder.</summary>
    private void SpawnEnemyOnLine()
    {
        GameObject prefabToSpawn = PickWeightedPrefab();
        if (prefabToSpawn == null) return;

        float halfLength = spawnLineLength / 2f;
        float randomOffset = UnityEngine.Random.Range(-halfLength, halfLength);

        Vector3 spawnPosition = transform.position;
        if (isVerticalLine)
            spawnPosition.y += randomOffset;
        else
            spawnPosition.x += randomOffset;

        Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);
    }

    /// <summary>
    /// Acilmis (unlockMilestone'una ulasilmis) tipler arasinda agirliga gore rastgele birini secer.
    /// Agirlik zorlukla (DifficultyFactor) Lerp'lenir. Alloc yok — dizide iki gecis yapar.
    /// </summary>
    private GameObject PickWeightedPrefab()
    {
        if (spawnEntries == null || spawnEntries.Length == 0) return null;

        float factor = DifficultyManager.DifficultyFactor;

        // 1. gecis: acilmis girislerin toplam agirligi
        float totalWeight = 0f;
        for (int i = 0; i < spawnEntries.Length; i++)
        {
            EnemySpawnEntry entry = spawnEntries[i];
            if (!IsUnlocked(entry)) continue;
            totalWeight += Mathf.Max(0f, Mathf.Lerp(entry.baseWeight, entry.maxWeight, factor));
        }

        if (totalWeight <= 0f) return null;

        // 2. gecis: rastgele nokta hangi girise denk geliyor
        float roll = UnityEngine.Random.value * totalWeight;
        GameObject lastValid = null;
        for (int i = 0; i < spawnEntries.Length; i++)
        {
            EnemySpawnEntry entry = spawnEntries[i];
            if (!IsUnlocked(entry)) continue;

            lastValid = entry.prefab;
            roll -= Mathf.Max(0f, Mathf.Lerp(entry.baseWeight, entry.maxWeight, factor));
            if (roll <= 0f) return entry.prefab;
        }

        // Float yuvarlama guvencesi: son gecerli girisi don
        return lastValid;
    }

    /// <summary>Giris gecerli mi ve milestone'una ulasildi mi? unlockMilestone &lt;= 0 ise bastan aciktir (manager yoksa da).</summary>
    private bool IsUnlocked(EnemySpawnEntry entry)
    {
        if (entry == null || entry.prefab == null) return false;
        if (entry.unlockMilestone <= 0) return true;
        return DifficultyManager.CurrentMilestone >= entry.unlockMilestone;
    }
    #endregion
}

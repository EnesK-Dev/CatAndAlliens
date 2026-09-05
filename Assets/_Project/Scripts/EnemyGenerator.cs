using System;
using System.Collections.Generic;
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

    [Header("Tip Guclenme Rampasi (yerel zorluk)")]
    [Tooltip("Bir dusman tipi ACILDIKTAN sonra kac SANIYEDE tam gucune ulassin. Bu sure boyunca o tip " +
             "0 (taban) -> 1 (tam) olceklenir. Boylece burst/lazer ILK cikinca zayif gelir, zamanla guclenir. " +
             "Global spawn sikligini ETKILEMEZ (o zamanla artmaya devam eder).")]
    [SerializeField] private float typeRampSeconds = 180f;

    [Header("Baslangic Dalgasi")]
    [Tooltip("Oyun basinda ANINDA spawn edilecek dusman sayisi (o an acik tipler; basta normal). " +
             "Acilisin bos/yavas hissetmemesi icin. 0 = kapali.")]
    [SerializeField] private int startingBurstCount = 5;

    [Header("Spawn Cizgisi")]
    [Tooltip("Cizginin toplam uzunlugu.")]
    [SerializeField] private float spawnLineLength = 5f;

    [Tooltip("Cizgi dikey (Y) mi yoksa yatay (X) mi uzansin.")]
    [SerializeField] private bool isVerticalLine = false;

    [Header("Son Dalga (kazanmadan hemen once)")]
    [Tooltip("Bu milestone index'ine ulasilinca 'son dalga' baslar - spawn araligi minSpawnInterval'in ALTINA iner. DifficultyFactor zaten 1'e ulasmis olsa da bu ekstra bir sicramadir.")]
    [SerializeField] private int finalWaveMilestoneIndex = 4;

    [Tooltip("Son dalgada iki spawn arasi sabit bekleme (+/- jitter). minSpawnInterval'den KISA olmali ki fark hissedilsin.")]
    [SerializeField] private float finalWaveSpawnInterval = 1.8f;

    [Header("Boss Sirasinda")]
    [Tooltip("Sahnede canli boss varken yeni normal dusman spawn'i dursun mu (mevcutlar yasamaya devam eder).")]
    [SerializeField] private bool pauseSpawnDuringBoss = true;

    [Header("Eszamanli Dusman Limiti")]
    [Tooltip("Ekranda ayni anda EN FAZLA kac dusman canli olabilir. Bu sayida dusman varken yeni spawn olmaz " +
             "(oldurdukce yeni gelir). 'Adim atacak yer kalmamasini' onler — spawn araligi ne olursa olsun " +
             "yogunluk sinirli kalir. 0 = limitsiz (eski davranis).")]
    [SerializeField] private int maxAliveEnemies = 25;
    #endregion

    #region Private Fields
    private const float IntervalFloor = 0.05f; // araligin altina inemeyecegi guvenli taban
    private float _nextSpawnTime;

    // Spawn ettigimiz dusmanlar — canli sayimi icin. Dusman olunce Destroy edilir, referans "fake null" olur;
    // her spawn denemesinde temizlenir. Enemy class'larina dokunmadan yogunlugu sinirlamak icin (tek-sorumluluk).
    private readonly List<GameObject> _aliveEnemies = new List<GameObject>();
    #endregion

    #region Unity Callbacks
    private void Start()
    {
        SpawnStartingBurst();
        ScheduleNextSpawn();
    }

    /// <summary>Oyun basinda bir grup dusmani aninda spawn eder (acilis bos hissetmesin). O an acik tipler.</summary>
    private void SpawnStartingBurst()
    {
        for (int i = 0; i < startingBurstCount; i++)
            SpawnEnemyOnLine();
    }

    private void Update()
    {
        if (Time.time >= _nextSpawnTime)
        {
            // Boss varken VEYA boss-oncesi bombardimanda spawn duraklar (mevcut dusmanlar silinmez;
            // bombardimanda bombalardan olurler). Tek-sorumluluk: sadece spawn tarafi.
            if (!((pauseSpawnDuringBoss && BossController.AnyBossAlive) || BombardmentDirector.IsActive || SplitterEnemy.BossLineageAlive))
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
        float interval;

        // Son dalga: DifficultyFactor zaten tavan yapmis olsa da (maxSpawnInterval/minSpawnInterval Lerp'i
        // 8dk'da zaten minimuma iniyor), bu ozel milestone'da onun da ALTINA inerek gercek bir sicrama yaratir.
        if (DifficultyManager.CurrentMilestone >= finalWaveMilestoneIndex)
        {
            interval = finalWaveSpawnInterval;
        }
        else
        {
            float factor = DifficultyManager.DifficultyFactor;
            interval = Mathf.Lerp(maxSpawnInterval, minSpawnInterval, factor);
        }

        interval += UnityEngine.Random.Range(-spawnIntervalJitter, spawnIntervalJitter);
        interval = Mathf.Max(IntervalFloor, interval);
        _nextSpawnTime = Time.time + interval;
    }

    /// <summary>Cizgi uzerinde rastgele bir noktaya, weighted random ile secilen tipi spawn eder.</summary>
    private void SpawnEnemyOnLine()
    {
        // Yogunluk siniri: ekranda zaten maxAliveEnemies kadar dusman varsa bu spawn'i atla.
        // (Bir sonraki aralikta tekrar denenir; oyuncu oldurdukce yer acilir.)
        if (maxAliveEnemies > 0 && CountAliveEnemies() >= maxAliveEnemies)
            return;

        EnemySpawnEntry entry = PickWeightedEntry();
        if (entry == null || entry.prefab == null) return;

        float halfLength = spawnLineLength / 2f;
        float randomOffset = UnityEngine.Random.Range(-halfLength, halfLength);

        Vector3 spawnPosition = transform.position;
        if (isVerticalLine)
            spawnPosition.y += randomOffset;
        else
            spawnPosition.x += randomOffset;

        GameObject spawned = PoolManager.Spawn(entry.prefab, spawnPosition, Quaternion.identity); // havuzdan (Instantiate yerine)
        _aliveEnemies.Add(spawned); // canli sayimi icin izle (limit kontrolu bunu kullanir)

        // YEREL ZORLUK: bu tip acilalı (unlockMilestone) ne kadar oldu -> 0..1 rampa. Ilk cikinca taban.
        float localFactor = typeRampSeconds > 0f
            ? Mathf.Clamp01(DifficultyManager.TimeSinceMilestone(entry.unlockMilestone) / typeRampSeconds)
            : 1f;
        // Dusman IDifficultyScaled uyguluyorsa yerel zorlugu ver (Awake sonrasi, Start oncesi — hesaplar buna gore).
        var scaled = spawned.GetComponent<IDifficultyScaled>();
        scaled?.SetSpawnDifficulty(localFactor);
    }

    /// <summary>
    /// Acilmis (unlockMilestone'una ulasilmis) tipler arasinda agirliga gore rastgele birini secer.
    /// Agirlik zorlukla (DifficultyFactor) Lerp'lenir. Alloc yok — dizide iki gecis yapar.
    /// </summary>
    private EnemySpawnEntry PickWeightedEntry()
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
        EnemySpawnEntry lastValid = null;
        for (int i = 0; i < spawnEntries.Length; i++)
        {
            EnemySpawnEntry entry = spawnEntries[i];
            if (!IsUnlocked(entry)) continue;

            lastValid = entry;
            roll -= Mathf.Max(0f, Mathf.Lerp(entry.baseWeight, entry.maxWeight, factor));
            if (roll <= 0f) return entry;
        }

        // Float yuvarlama guvencesi: son gecerli girisi don
        return lastValid;
    }

    /// <summary>
    /// Canli dusman sayisini dondurur; bu sirada Destroy edilmis (fake-null) VEYA havuza donmus (inactive)
    /// referanslari listeden atar. Havuz: despawn edilen dusman null degildir ama pasiftir — onu da 'olu' say.
    /// Liste kucuk (en fazla ~limit kadar) oldugu icin maliyeti onemsiz; her spawn denemesinde bir kez cagrilir.
    /// </summary>
    private int CountAliveEnemies()
    {
        for (int i = _aliveEnemies.Count - 1; i >= 0; i--)
        {
            GameObject e = _aliveEnemies[i];
            if (e == null || !e.activeSelf) // olmus/Destroy edilmis VEYA havuza donmus (pasif)
                _aliveEnemies.RemoveAt(i);
        }
        return _aliveEnemies.Count;
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

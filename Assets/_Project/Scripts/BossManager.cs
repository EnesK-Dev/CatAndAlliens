using System;
using UnityEngine;

/// <summary>
/// Boss dalgalarini zamanlar: DifficultyManager.OnMilestoneReached'i dinler ve belirlenen milestone'a
/// ulasilinca ilgili boss'u spawn eder (her biri bir enemy tipinin rengiyle tint'li). Son dalga final
/// boss'tur (yenince WinConditionManager kazanmayi tetikler). Sahnede tek obje, gevsek bagli (event).
/// God GameManager degil — sadece boss zamanlamasindan sorumlu. Boss davranisi BossController'da.
/// </summary>
public class BossManager : MonoBehaviour
{
    #region Nested Types
    /// <summary>Tek bir boss dalgasinin tanimi.</summary>
    [Serializable]
    public class BossWave
    {
        [Tooltip("Sadece Inspector'da tanimak icin (kod kullanmaz).")]
        public string label = "Boss";

        [Tooltip("Bu dalganin boss prefab'i (dash/lazer/burst farkli). Bos ise BossManager'daki varsayilan kullanilir.")]
        public BossController bossPrefab;

        [Tooltip("Bu milestone index'ine ulasilinca boss spawn olur (DifficultyManager.milestoneMinutes sirasi).")]
        public int milestoneIndex = 1;

        [Tooltip("Boss gorsel rengi — ilgili enemy tipinden alinir. Beyaz = dogal.")]
        public Color tint = Color.white;

        [Tooltip("Final boss mu? true ise yenince oyunu KAZANDIRIR. Genelde son dalga.")]
        public bool isFinalBoss = false;

        [Tooltip("Bu boss'un cani. 0 = prefab varsayilani.")]
        public float maxHealth = 0f;
    }
    #endregion

    #region Serialized Fields
    [Header("Referanslar")]
    [Tooltip("Spawn edilecek boss prefab'i (BossController).")]
    [SerializeField] private BossController bossPrefab;

    [Tooltip("Bos birakilirsa Awake'te otomatik bulunur — spawn ofseti bunun etrafinda.")]
    [SerializeField] private player playerRef;

    [Header("Dalgalar")]
    [Tooltip("Milestone -> boss eslesmeleri. Sirayla: normal / lazer / burst / boomerang renkleri.")]
    [SerializeField] private BossWave[] waves;

    [Header("Spawn Konumu")]
    [Tooltip("Oyuncuya gore spawn ofseti (dunya birimi). Boss buradan girer.")]
    [SerializeField] private Vector2 spawnOffset = new Vector2(0f, 6f);
    #endregion

    #region Private Fields
    private bool[] _spawned;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        if (playerRef == null) playerRef = FindFirstObjectByType<player>();
        _spawned = new bool[waves != null ? waves.Length : 0];
    }

    private void OnEnable()
    {
        DifficultyManager.OnMilestoneReached += HandleMilestone;
    }

    private void OnDisable()
    {
        DifficultyManager.OnMilestoneReached -= HandleMilestone;
    }

    private void Start()
    {
        // Gec enable olduysak: gecmis milestone'lardaki bosslari yakala (bir kez).
        int current = DifficultyManager.CurrentMilestone;
        if (current >= 0 && waves != null)
            for (int i = 0; i < waves.Length; i++)
                if (waves[i] != null && waves[i].milestoneIndex <= current)
                    TrySpawnWave(i);
    }
    #endregion

    #region Private Methods
    private void HandleMilestone(int milestoneIndex)
    {
        if (waves == null) return;
        for (int i = 0; i < waves.Length; i++)
            if (waves[i] != null && waves[i].milestoneIndex == milestoneIndex)
                TrySpawnWave(i);
    }

    /// <summary>Bir dalgayi (henuz spawn edilmediyse) spawn eder ve tint/final/can'i uygular.</summary>
    private void TrySpawnWave(int index)
    {
        if (_spawned == null || index < 0 || index >= _spawned.Length) return;
        if (_spawned[index]) return; // ayni boss iki kez spawn olmasin
        if (bossPrefab == null) return;

        _spawned[index] = true;
        BossWave wave = waves[index];

        // Dalganin kendi prefab'i varsa onu, yoksa varsayilani kullan (dash/lazer/burst)
        BossController prefabToUse = wave.bossPrefab != null ? wave.bossPrefab : bossPrefab;
        if (prefabToUse == null) return;

        Vector3 basePos = playerRef != null ? playerRef.transform.position : Vector3.zero;
        BossController boss = Instantiate(prefabToUse, basePos + (Vector3)spawnOffset, Quaternion.identity);
        boss.Initialize(wave.tint, wave.isFinalBoss, wave.maxHealth); // Awake sonrasi, Start oncesi
    }
    #endregion
}

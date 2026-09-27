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

        [Tooltip("ENEMY-tabanli boss (ornek: SplitterBoss). Doluysa bossPrefab yerine BU spawn edilir (BossController degil).")]
        public GameObject enemyBossPrefab;

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
    /// <summary>afterMilestone'dan SONRAKI ilk boss dalgasini (en kucuk milestoneIndex) bulur; renk + milestone doner.
    /// Bomba/geri sayim bari icin: "sonraki boss ne zaman, hangi renk". Yoksa false.</summary>
    public bool TryGetNextBoss(int afterMilestone, out int milestoneIndex, out Color color)
    {
        milestoneIndex = -1; color = Color.white;
        if (waves == null) return false;
        int best = int.MaxValue; BossWave bestWave = null;
        for (int i = 0; i < waves.Length; i++)
        {
            var w = waves[i];
            if (w == null) continue;
            if (w.milestoneIndex > afterMilestone && w.milestoneIndex < best) { best = w.milestoneIndex; bestWave = w; }
        }
        if (bestWave == null) return false;
        milestoneIndex = best;
        if (bestWave.enemyBossPrefab != null) color = Color.white; // enemy-based boss (Splitter, 2. boss) -> geri sayim bari BEYAZ (boss kendi rengini korur)
        else { var pf = bestWave.bossPrefab != null ? bestWave.bossPrefab : bossPrefab; color = EffectiveBossColor(pf, bestWave.tint); }
        return true;
    }

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

        BossWave wave = waves[index];
        if (wave == null) return;

        // ENEMY-tabanli boss (splitter gibi) — BossController degil, dogrudan prefab spawn edilir
        if (wave.enemyBossPrefab != null)
        {
            _spawned[index] = true;
            GameObject pf = wave.enemyBossPrefab;
            Color col = EnemyBossColor(pf, wave.tint);
            BombardmentDirector.Trigger(col, () => SpawnEnemyBoss(pf, wave.maxHealth));
            return;
        }

        // BossController boss — dalganin kendi prefab'i varsa onu, yoksa varsayilani kullan
        BossController prefabToUse = wave.bossPrefab != null ? wave.bossPrefab : bossPrefab;
        if (prefabToUse == null) return;

        _spawned[index] = true;

        // Boss'tan ONCE bombardiman: alani temizler; bitince boss spawn olur. Boss rengi -> "BOSS FIGHT" yazisi.
        Color bossColor = EffectiveBossColor(prefabToUse, wave.tint);
        BombardmentDirector.Trigger(bossColor, () => SpawnBoss(prefabToUse, wave));
    }

    /// <summary>Enemy-tabanli boss'u (SplitterBoss vb.) oyuncunun yaninda spawn eder + wave.maxHealth override'ini uygular.
    /// Bombardiman bitince cagrilir. Override > 0 ise boss DIGER bosslar gibi SABIT canli olur (difficulty/adaptif olcekleme yok).</summary>
    private void SpawnEnemyBoss(GameObject prefab, float maxHealthOverride)
    {
        if (prefab == null) return;
        Vector3 basePos = playerRef != null ? playerRef.transform.position : Vector3.zero;
        GameObject go = Instantiate(prefab, basePos + (Vector3)spawnOffset, Quaternion.identity);

        // Diger bosslar gibi SABIT can: SetMaxHealth _healthOverridden'i set eder -> Start/InitializeHealthSystem
        // difficulty + adaptif EnemyHealthMult ile TEKRAR olceklemez. (0 = eski davranis: prefab cani x zorluk.)
        if (maxHealthOverride > 0f)
        {
            var ec = go.GetComponent<EnemyController>();
            if (ec != null) ec.SetMaxHealth(maxHealthOverride);
        }
    }

    /// <summary>Enemy-tabanli boss'un gorunecek rengi (tint beyazsa prefab'in kendi rengi).</summary>
    private Color EnemyBossColor(GameObject prefab, Color tint)
    {
        bool natural = tint.r > 0.99f && tint.g > 0.99f && tint.b > 0.99f && tint.a > 0.99f;
        if (!natural) return tint;
        var sr = prefab.GetComponent<SpriteRenderer>();
        return sr != null ? sr.color : Color.white;
    }

    /// <summary>Boss'un GORUNECEK rengi: tint beyaz (dogal) ise prefab'in kendi rengi, degilse tint. (BossController.Initialize ile ayni mantik.)</summary>
    private Color EffectiveBossColor(BossController prefab, Color tint)
    {
        bool natural = tint.r > 0.99f && tint.g > 0.99f && tint.b > 0.99f && tint.a > 0.99f;
        if (!natural) return tint;
        var sr = prefab != null ? prefab.GetComponent<SpriteRenderer>() : null;
        return sr != null ? sr.color : Color.white;
    }

    /// <summary>Boss'u oyuncunun yaninda spawn eder ve tint/final/can'ini uygular. Bombardiman bitince cagrilir.</summary>
    private void SpawnBoss(BossController prefabToUse, BossWave wave)
    {
        if (prefabToUse == null) return;
        Vector3 basePos = playerRef != null ? playerRef.transform.position : Vector3.zero;
        BossController boss = Instantiate(prefabToUse, basePos + (Vector3)spawnOffset, Quaternion.identity);
        boss.Initialize(wave.tint, wave.isFinalBoss, wave.maxHealth); // Awake sonrasi, Start oncesi
    }
    #endregion
}

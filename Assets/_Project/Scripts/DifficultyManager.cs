using System;
using UnityEngine;

/// <summary>
/// Oyun suresine bagli TEK merkezi zorluk kaynagi. Gecen sureyi takip eder ve 0-1 arasi
/// bir DifficultyFactor uretir (AnimationCurve ile ayarlanabilir). Belirlenen milestone
/// dakikalarina ulasinca OnMilestoneReached (static event) firlatir — ileride yeni enemy
/// tipi / elite orani acmak icin. Sahnede tek obje olur, statik erisimlidir
/// (DamagePopupManager ile ayni pattern). Bu faz sadece altyapidir; hicbir sisteme baglanmaz.
/// </summary>
public class DifficultyManager : MonoBehaviour
{
    #region Serialized Fields
    [Header("Zorluk Egrisi")]
    [Tooltip("Zorlugun maksimuma ulasacagi sure (saniye). Egrinin sag ucu bu ana denk gelir.")]
    [SerializeField] private float timeToMaxDifficulty = 480f; // 8 dk

    [Tooltip("Normalize zaman (0-1) -> zorluk faktoru (0-1). Sol = oyun basi, sag = timeToMaxDifficulty.")]
    [SerializeField] private AnimationCurve difficultyCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Milestone'lar (dakika)")]
    [Tooltip("Bu dakikalara ulasinca OnMilestoneReached firlar (index sirasiyla). Artan sirada girin. Ileride enemy tipi/elite orani acmak icin.")]
    [SerializeField] private float[] milestoneMinutes = { 0f, 2f, 5f, 8f };

    [Header("Adaptif Dusman Cani (oyuncu hizli kesiyorsa can biraz artar)")]
    [Tooltip("Acik ise: oyuncu hizli oldurdukce dusman cani 1 -> maxEnemyHealthMult arasina cikar.")]
    [SerializeField] private bool adaptiveEnemyHealth = true;
    [Tooltip("Kill hizini kac saniyede bir olcup ayarlasin.")]
    [SerializeField] private float adaptWindow = 1.5f;
    [Tooltip("Saniyedeki oldurme bunun ALTINDA ise can carpani 1 (zayif oyuncu).")]
    [SerializeField] private float lowKillsPerSec = 1.2f;
    [Tooltip("Saniyedeki oldurme bunun USTUNDE ise can carpani tavana ulasir (cok hizli kesen oyuncu).")]
    [SerializeField] private float highKillsPerSec = 4f;
    [Tooltip("Dusman cani carpani tavani. 1.3 = en fazla %30 fazla can.")]
    [SerializeField] private float maxEnemyHealthMult = 1.3f;

    [Header("Debug")]
    [Tooltip("TEST: Oyunu bu MILESTONE'dan baslat. 0 = normal bas. 1 = dash boss'tan, 2 = splitter boss'tan... " +
             "Sadece hedef milestone tetiklenir (oncekiler atlanir). RELEASE'de 0 birak!")]
    [SerializeField] private int debugStartMilestone = 0;

    [Tooltip("Ekranin sol ustunde gecen sure / faktor / milestone gosterir. Test icin; sonra kapat.")]
    [SerializeField] private bool showDebugOverlay = false; // RELEASE: kapali

    [Tooltip("Debug yazisinin boyutu ekran yuksekligine gore oran (0.03 = ekranin %3'u). Buyut/kucult.")]
    [SerializeField] private float debugFontScale = 0.03f;
    #endregion

    #region Private Fields
    private static DifficultyManager _instance;
    private float _elapsedTime;
    private float _currentFactor;
    private int _reachedMilestoneIndex = -1;
    private int _killsInWindow;
    private float _windowTimer;
    private GUIStyle _debugStyle;
    #endregion

    #region Static API
    /// <summary>0-1 arasi guncel zorluk faktoru. Sahnede manager yoksa 0 doner.</summary>
    public static float DifficultyFactor => _instance != null ? _instance._currentFactor : 0f;

    /// <summary>Oyun basindan bu yana gecen (olcekli) sure, saniye. Manager yoksa 0.</summary>
    public static float ElapsedTime => _instance != null ? _instance._elapsedTime : 0f;

    /// <summary>Ulasilmis en yuksek milestone index'i (-1 = henuz hicbiri). Manager yoksa -1.</summary>
    public static int CurrentMilestone => _instance != null ? _instance._reachedMilestoneIndex : -1;

    /// <summary>En yuksek milestone index'i (milestoneMinutes son eleman). Manager yoksa 0. Cooldown/olcekleme icin.</summary>
    public static int MaxMilestoneIndex =>
        (_instance != null && _instance.milestoneMinutes != null && _instance.milestoneMinutes.Length > 0)
            ? _instance.milestoneMinutes.Length - 1 : 0;

    /// <summary>
    /// Verilen milestone'a ulasilmasindan bu yana gecen SANIYE (henuz ulasilmadiysa/manager yoksa 0).
    /// Milestone'lar milestoneMinutes[i]*60'ta tetiklendigi icin = elapsed - o zaman. Dusman tipinin
    /// "acilisindan beri" gecen sureyi olcmek icin EnemyGenerator kullanir (yerel zorluk rampasi).
    /// </summary>
    public static float TimeSinceMilestone(int milestoneIndex)
    {
        if (_instance == null) return 0f;
        var mm = _instance.milestoneMinutes;
        if (mm == null || milestoneIndex < 0 || milestoneIndex >= mm.Length)
            return _instance._elapsedTime; // unlock=0 gibi durumlar: bastan acik say
        return Mathf.Max(0f, _instance._elapsedTime - mm[milestoneIndex] * 60f);
    }

    /// <summary>Verilen milestone'un tetiklenecegi (olcekli) saniye. index &lt; 0 -> 0. Tasarsa son milestone. Bar/geri sayim icin.</summary>
    public static float MilestoneSeconds(int index)
    {
        if (_instance == null || _instance.milestoneMinutes == null || _instance.milestoneMinutes.Length == 0) return 0f;
        if (index < 0) return 0f;
        if (index >= _instance.milestoneMinutes.Length) index = _instance.milestoneMinutes.Length - 1;
        return _instance.milestoneMinutes[index] * 60f;
    }

    /// <summary>Bir dusman (oyuncu tarafindan) oldurulunce cagrilir — adaptif can icin kill hizi sayaci.</summary>
    public static void RegisterKill()
    {
        if (_instance != null) _instance._killsInWindow++;
    }

    /// <summary>Yeni bir milestone'a ulasilinca firlar. Parametre: milestone index'i.</summary>
    public static event Action<int> OnMilestoneReached;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        // Sahne basina tek manager — ikinciyi sessizce ele
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        _elapsedTime = 0f;
        _currentFactor = 0f;
        _reachedMilestoneIndex = -1;

        // TEST: belirli bir milestone'dan basla — sureyi o milestone'un dakikasina al ve oncekileri "gecilmis" say
        // ki sadece HEDEF milestone tetiklensin (ust uste boss binmesin). RELEASE'de debugStartMilestone=0.
        if (debugStartMilestone > 0 && milestoneMinutes != null && debugStartMilestone < milestoneMinutes.Length)
        {
            _elapsedTime = milestoneMinutes[debugStartMilestone] * 60f;
            _reachedMilestoneIndex = debugStartMilestone - 1;
        }
    }

    private void Update()
    {
        // Boss sekansi (bombardiman + boss dovusu) boyunca milestone SAATI DONAR — boss olmeden sonraki
        // milestone/boss gelmesin. Oyuncu bosstan kacip beklese bile clock ilerlemez; boss yavas olse bile
        // ust uste boss binmez. Boss olunce (AnyBossAlive=false) saat kaldigi yerden devam eder.
        if (BossController.AnyBossAlive || BombardmentDirector.IsActive || SplitterEnemy.BossLineageAlive) return;

        // Olcekli sure kullaniyoruz — Time.timeScale=0 (pause/upgrade paneli) oldugunda zorluk da otomatik durur.
        _elapsedTime += Time.deltaTime;
        _currentFactor = EvaluateFactor(_elapsedTime);
        CheckMilestones();
        UpdateAdaptiveEnemyHealth();
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void OnGUI()
    {
        if (!showDebugOverlay) return;

        // Font boyutunu ekran yuksekligine gore olcekle — mobilde/yuksek DPI'de okunabilir kalir.
        int fontSize = Mathf.RoundToInt(Screen.height * debugFontScale);
        _debugStyle ??= new GUIStyle();
        _debugStyle.fontSize = fontSize;
        _debugStyle.normal.textColor = Color.white;

        float margin = fontSize * 0.5f;
        GUI.Label(
            new Rect(margin, margin, Screen.width, fontSize + 4f),
            $"[Difficulty] Time: {_elapsedTime:F1}s   Factor: {_currentFactor:F2}   Milestone: {_reachedMilestoneIndex}",
            _debugStyle);
    }
    #endregion

    #region Private Methods
    /// <summary>Kill hizini periyodik olcer; hizliysa RunStats.EnemyHealthMult'i yumusakca 1 -> tavan arasina ceker.</summary>
    private void UpdateAdaptiveEnemyHealth()
    {
        if (!adaptiveEnemyHealth) { RunStats.EnemyHealthMult = 1f; return; }
        _windowTimer += Time.deltaTime;
        if (_windowTimer < adaptWindow) return;

        float kps = _killsInWindow / Mathf.Max(0.01f, _windowTimer);
        float u = Mathf.InverseLerp(lowKillsPerSec, highKillsPerSec, kps); // 0 (yavas) .. 1 (hizli)
        float target = Mathf.Lerp(1f, Mathf.Max(1f, maxEnemyHealthMult), u);
        RunStats.EnemyHealthMult = Mathf.Lerp(RunStats.EnemyHealthMult, target, 0.5f); // yumusak gecis (birkac pencerede oturur)
        _killsInWindow = 0;
        _windowTimer = 0f;
    }

    private float EvaluateFactor(float time)
    {
        float normalized = timeToMaxDifficulty > 0f ? Mathf.Clamp01(time / timeToMaxDifficulty) : 1f;
        return Mathf.Clamp01(difficultyCurve.Evaluate(normalized));
    }

    private void CheckMilestones()
    {
        if (milestoneMinutes == null) return;

        // Sirayla ilerle: gecilmemis bir sonraki milestone'a ulasildiysa firlat.
        // Ayni karede birden fazla milestone gecilmis olabilir (while) — sirayla hepsini firlatir.
        int next = _reachedMilestoneIndex + 1;
        while (next < milestoneMinutes.Length && _elapsedTime >= milestoneMinutes[next] * 60f)
        {
            _reachedMilestoneIndex = next;
            OnMilestoneReached?.Invoke(next);
            next++;
        }
    }
    #endregion
}

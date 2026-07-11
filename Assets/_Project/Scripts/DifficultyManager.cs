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

    [Header("Debug")]
    [Tooltip("Ekranin sol ustunde gecen sure / faktor / milestone gosterir. Test icin; sonra kapat.")]
    [SerializeField] private bool showDebugOverlay = true;

    [Tooltip("Debug yazisinin boyutu ekran yuksekligine gore oran (0.03 = ekranin %3'u). Buyut/kucult.")]
    [SerializeField] private float debugFontScale = 0.03f;
    #endregion

    #region Private Fields
    private static DifficultyManager _instance;
    private float _elapsedTime;
    private float _currentFactor;
    private int _reachedMilestoneIndex = -1;
    private GUIStyle _debugStyle;
    #endregion

    #region Static API
    /// <summary>0-1 arasi guncel zorluk faktoru. Sahnede manager yoksa 0 doner.</summary>
    public static float DifficultyFactor => _instance != null ? _instance._currentFactor : 0f;

    /// <summary>Oyun basindan bu yana gecen (olcekli) sure, saniye. Manager yoksa 0.</summary>
    public static float ElapsedTime => _instance != null ? _instance._elapsedTime : 0f;

    /// <summary>Ulasilmis en yuksek milestone index'i (-1 = henuz hicbiri). Manager yoksa -1.</summary>
    public static int CurrentMilestone => _instance != null ? _instance._reachedMilestoneIndex : -1;

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
    }

    private void Update()
    {
        // Olcekli sure kullaniyoruz — Time.timeScale=0 (pause/upgrade paneli) oldugunda
        // zorluk da otomatik durur; bu istenen davranis.
        _elapsedTime += Time.deltaTime;
        _currentFactor = EvaluateFactor(_elapsedTime);
        CheckMilestones();
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

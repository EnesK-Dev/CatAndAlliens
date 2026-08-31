using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Ekranin ustundeki boss can bari. BossController'in static event'lerini dinler: boss dogunca
/// gorunur olur, can degistikce fill guncellenir, boss olunce gizlenir. Sahnede tek obje
/// (HeartUI / CoreCounter pattern'i). God manager yok — sadece gorunumden sorumlu.
/// </summary>
public class BossHealthBarUI : MonoBehaviour
{
    #region Serialized Fields
    [Tooltip("Bar'in kok objesi — boss yokken kapali kalir.")]
    [SerializeField] private GameObject barRoot;

    [Tooltip("Doluluk gosteren Image (Image Type = Filled, Horizontal). fillAmount 0-1 ayarlanir.")]
    [SerializeField] private Image fillImage;

    [Tooltip("Boss ismi yazisi (opsiyonel).")]
    [SerializeField] private TMP_Text nameText;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        if (barRoot != null) barRoot.SetActive(false); // baslangicta gizli
    }

    private void OnEnable()
    {
        BossController.OnBossSpawned += HandleSpawned;
        BossController.OnBossHealthChanged += HandleHealthChanged;
        BossController.OnBossDefeated += HandleDefeated;
    }

    private void OnDisable()
    {
        BossController.OnBossSpawned -= HandleSpawned;
        BossController.OnBossHealthChanged -= HandleHealthChanged;
        BossController.OnBossDefeated -= HandleDefeated;
    }
    #endregion

    #region Private Methods
    private void HandleSpawned(BossController boss)
    {
        if (barRoot != null) barRoot.SetActive(true);
        if (nameText != null) nameText.text = boss != null ? boss.BossName : "BOSS";
        if (fillImage != null) fillImage.fillAmount = 1f;
    }

    private void HandleHealthChanged(float current, float max)
    {
        if (fillImage != null)
            fillImage.fillAmount = max > 0f ? Mathf.Clamp01(current / max) : 0f;
    }

    private void HandleDefeated(bool wasFinal)
    {
        if (barRoot != null) barRoot.SetActive(false);
    }
    #endregion
}

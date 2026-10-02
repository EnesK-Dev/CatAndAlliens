using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Ekranin ALT-ORTASINDA, canvas'a sabitlenmis boss can bari (boss ile birlikte HAREKET ETMEZ).
/// Her kare aktif boss'u sorgular (poll): BossController bosslari (dash/laser/kamikaze/burst) icin
/// BossController.ActiveBoss, Splitter boss icin SplitterEnemy.TryGetBossBar (tum soyun toplam cani).
/// Bar, o boss'un KENDI rengiyle (BaseColor) boyanir; boss yokken gizlenir. Sahnede tek obje
/// (HeartUI / CoreCounter pattern'i). God manager yok — sadece gorunumden sorumlu.
/// </summary>
public class BossHealthBarUI : MonoBehaviour
{
    #region Serialized Fields
    [Tooltip("Bar'in kok objesi (BG + Fill). Boss yokken kapali kalir. BU component'ten AYRI (hep aktif) olmali.")]
    [SerializeField] private GameObject barRoot;

    [Tooltip("Doluluk gosteren Image (Image Type = Filled, Horizontal). fillAmount 0-1; rengi boss rengine boyanir.")]
    [SerializeField] private Image fillImage;

    [Tooltip("Boss ismi yazisi (opsiyonel).")]
    [SerializeField] private TMP_Text nameText;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        if (barRoot != null) barRoot.SetActive(false); // baslangicta gizli
    }

    private void LateUpdate()
    {
        float cur, max; Color col; string nm; bool show;

        var boss = BossController.ActiveBoss;
        if (boss != null && !boss.IsDying)
        {
            cur = boss.CurrentHealthValue; max = boss.MaxHealthValue; col = boss.BaseColor;
            nm = boss.BossName; show = true;
        }
        else if (SplitterEnemy.TryGetBossBar(out cur, out max, out col))
        {
            nm = "SPLITTER"; show = true;
        }
        else
        {
            show = false; cur = max = 0f; col = Color.white; nm = string.Empty;
        }

        if (!show)
        {
            if (barRoot != null && barRoot.activeSelf) barRoot.SetActive(false);
            return;
        }

        if (barRoot != null && !barRoot.activeSelf) barRoot.SetActive(true);
        if (fillImage != null)
        {
            fillImage.fillAmount = max > 0f ? Mathf.Clamp01(cur / max) : 0f;
            fillImage.color = col; // boss'un kendi rengi (ust geri sayim barindaki renklerle ayni kaynak)
        }
        if (nameText != null) nameText.text = nm;
    }
    #endregion
}

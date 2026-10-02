using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ekranin en ustunde (telefon ust kenari) ince bir bar. Sonraki BOSS bombardimanina (bomba yagmuru)
/// kalan sureyi doldurarak gosterir; rengi SONRAKI bossun rengidir. Boylece oyuncu "bir sey geliyor"
/// hissini onceden alir. Boss/bombardiman sirasinda zorluk saati donar (DifficultyManager), bar da donar.
/// Sadece OKUR: DifficultyManager + BossManager sorgular. Basit: tek Filled Image.
/// </summary>
public class BombWarningBarUI : MonoBehaviour
{
    #region Serialized Fields
    [Tooltip("Dolan bar (Image Type=Filled, Horizontal). fillAmount 0->1 sonraki bombardimana kadar.")]
    [SerializeField] private Image fillImage;
    [Tooltip("Sonraki boss yoksa (oyun sonu) tum bar gizlensin mi.")]
    [SerializeField] private bool hideWhenNoBoss = true;
    [Tooltip("Bar renginin en dusuk parlakligi (0 renk sabit). Dolarken hafif parlama icin.")]
    [SerializeField] private float pulseOnNearFull = 0.15f;
    #endregion

    #region Private Fields
    private BossManager _boss;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        _boss = FindFirstObjectByType<BossManager>();
        if (fillImage == null) fillImage = GetComponentInChildren<Image>();
    }

    private void Update()
    {
        if (fillImage == null) return;
        if (_boss == null) { _boss = FindFirstObjectByType<BossManager>(); if (_boss == null) { SetVisible(false); return; } }

        // Saat DONMUSken (boss dovusu / bombardiman / splitter soyu) bar gizlensin —
        // sonraki bossun geri sayimi ancak zaman yeniden ilerlemeye baslayinca (boss olunce) gorunur.
        if (BossController.AnyBossAlive || BombardmentDirector.IsActive || SplitterEnemy.BossLineageAlive)
        {
            SetVisible(false);
            return;
        }

        int cur = DifficultyManager.CurrentMilestone;
        if (!_boss.TryGetNextBoss(cur, out int nextMi, out Color col))
        {
            SetVisible(!hideWhenNoBoss); // sonraki boss yok
            return;
        }

        SetVisible(true);
        float segStart = DifficultyManager.MilestoneSeconds(cur < 0 ? 0 : cur);
        float segEnd = DifficultyManager.MilestoneSeconds(nextMi);
        float t = segEnd > segStart ? Mathf.Clamp01((DifficultyManager.ElapsedTime - segStart) / (segEnd - segStart)) : 1f;

        // GERI SAYIM: bar dolu baslar (cok zaman var), bombardimana yaklastikca AZALIR (0 = simdi).
        fillImage.fillAmount = 1f - t;

        // Renk: sonraki boss rengi; dolmaya yakinken hafif nabiz (dikkat cek)
        Color c = col;
        if (pulseOnNearFull > 0f && t > 0.8f)
        {
            float pulse = 1f + pulseOnNearFull * Mathf.Sin(Time.unscaledTime * 8f);
            c = new Color(Mathf.Clamp01(col.r * pulse), Mathf.Clamp01(col.g * pulse), Mathf.Clamp01(col.b * pulse), col.a);
        }
        fillImage.color = c;
    }
    #endregion

    #region Private Methods
    private void SetVisible(bool v)
    {
        if (fillImage != null && fillImage.enabled != v) fillImage.enabled = v;
    }
    #endregion
}

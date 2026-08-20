using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ultimate UI kontrolcusu. UltimateManager'in static event'lerini dinler:
/// OnChargeChanged -> bar fill'ini (0-1) gunceller, OnReadyChanged -> Ult butonunu aktif/pasif eder.
/// Buton tiklaninca UltimateManager.TryActivate() cagirir (hazirsa 2x buff tetiklenir, bar sifirlanir).
/// God UI degil — sadece gosterim + giris; buff mekanigi player'da, sarj mantigi manager'da.
/// CoreCounterUI/HeartUI ile ayni pattern: Start'ta guncel durumu hemen cizer, OnDestroy'da abonelik biter.
/// </summary>
public class UltimateBarUI : MonoBehaviour
{
    #region Serialized Fields
    [Header("Bar (frame-swap / spritesheet)")]
    [Tooltip("Ekranda tek bar Image'i — sarja gore sprite'i degisir (karakter animasyonu gibi).")]
    [SerializeField] private Image barImage;

    [Tooltip("Dolum kareleri SIRAYLA: [0] = bos ... [son] = tam dolu. Her ahtapot bir frame ilerletir.")]
    [SerializeField] private Sprite[] frames;

    [Header("Buton")]
    [Tooltip("Ultimate'i tetikleyen buton.")]
    [SerializeField] private Button ultButton;

    [Tooltip("Butonun ikon Image'i — hazir/dolmakta durumuna gore renklenir.")]
    [SerializeField] private Image buttonIcon;

    [Tooltip("Ultimate hazir oldugunda ikon rengi (tam parlak).")]
    [SerializeField] private Color readyColor = Color.white;

    [Tooltip("Ultimate dolmakta iken ikon rengi (soluk/gri).")]
    [SerializeField] private Color chargingColor = new Color(1f, 1f, 1f, 0.35f);
    #endregion

    #region Unity Callbacks
    private void Start()
    {
        UltimateManager.OnChargeChanged += HandleChargeChanged;
        UltimateManager.OnReadyChanged += HandleReadyChanged;

        if (ultButton != null)
            ultButton.onClick.AddListener(HandleUltButtonClicked);

        // Baslangic durumunu hemen ciz (UI gec enable olsa bile dogru gorunsun)
        HandleChargeChanged(UltimateManager.Charge01);
        HandleReadyChanged(UltimateManager.IsReady);
    }

    private void OnDestroy()
    {
        UltimateManager.OnChargeChanged -= HandleChargeChanged;
        UltimateManager.OnReadyChanged -= HandleReadyChanged;

        if (ultButton != null)
            ultButton.onClick.RemoveListener(HandleUltButtonClicked);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Sarj degisince gosterilecek frame'i secer (spritesheet frame-swap).
    /// normalized 0-1 -> index = round(normalized * (frameSayisi-1)). frames[0]=bos, frames[son]=dolu.
    /// </summary>
    private void HandleChargeChanged(float normalized)
    {
        if (barImage == null || frames == null || frames.Length == 0) return;

        int idx = Mathf.RoundToInt(Mathf.Clamp01(normalized) * (frames.Length - 1));
        idx = Mathf.Clamp(idx, 0, frames.Length - 1);
        if (frames[idx] != null)
            barImage.sprite = frames[idx];
    }

    /// <summary>Hazir olma durumu degisince butonu aktif/pasif eder ve ikon rengini gunceller.</summary>
    private void HandleReadyChanged(bool ready)
    {
        if (ultButton != null)
            ultButton.interactable = ready;

        if (buttonIcon != null)
            buttonIcon.color = ready ? readyColor : chargingColor;
    }

    /// <summary>Buton tiklaninca ultimate'i tetiklemeye calisir. Hazir degilse manager zaten yoksayar.</summary>
    private void HandleUltButtonClicked()
    {
        UltimateManager.TryActivate();
    }
    #endregion
}

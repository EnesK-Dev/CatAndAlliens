using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ultimate sinemasinin TAM-EKRAN beyaz impact katmani. Tek bir full-screen white Image tutar; IMPACT
/// aninda parlar. Bu Image HER seyin (joystick/can bari/rank) USTUNDE cizilmeli — bu yuzden ayri Screen
/// Space Overlay canvas + yuksek sortingOrder'da yasar (render sirasi tuzagi). Zamanlamayi
/// UltimateCinematic yonetir; bu sinif sadece beyaz alpha'yi uygular.
/// </summary>
public class UltimateScreenFX : MonoBehaviour
{
    #region Serialized Fields
    [Tooltip("IMPACT aninda parlayan tam-ekran beyaz flash.")]
    [SerializeField] private Image whiteFlash;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        // Dokunmayi yutmasin — joystick/butonlar calismaya devam etsin
        if (whiteFlash != null) whiteFlash.raycastTarget = false;
        ResetFX();
    }
    #endregion

    #region Public Methods
    /// <summary>IMPACT beyaz flash alfasi (0-1) — cinematic egri boyunca surer.</summary>
    public void SetWhite(float alpha) => SetAlpha(whiteFlash, Mathf.Clamp01(alpha));

    /// <summary>Katmani sifirla (gorunmez).</summary>
    public void ResetFX() => SetAlpha(whiteFlash, 0f);
    #endregion

    #region Private Methods
    private static void SetAlpha(Image img, float a)
    {
        if (img == null) return;
        Color c = img.color;
        c.a = a;
        img.color = c;
    }
    #endregion
}

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Player hasar aldığında tüm ekranı kısa süreliğine kırmızıya boyar.
/// player.OnPlayerDamaged static event'ini dinler — düşman hasarı bu efekti TETİKLEMEZ.
/// Canvas altında ekranı tam kaplayan bir Image objesine eklenir.
/// </summary>
[RequireComponent(typeof(Image))]
public class DamageScreenFlash : MonoBehaviour
{
    #region Serialized Fields

    [Header("Renk")]
    [SerializeField] private Color flashColor = new Color(0.8f, 0f, 0f, 1f);
    [Tooltip("Flash'in en yogun anindaki saydamsizlik. 1 = ekran tamamen kirmizi (gorus kapanir), 0.5 civari onerilir.")]
    [Range(0f, 1f)]
    [SerializeField] private float peakAlpha = 0.5f;

    [Header("Zamanlama (saniye)")]
    [Tooltip("Kirmiziya cikis suresi. Cok kisa olmali ki vurus ani hissedilsin.")]
    [SerializeField] private float fadeInDuration = 0.05f;
    [Tooltip("En yogun renkte bekleme suresi.")]
    [SerializeField] private float holdDuration = 0.05f;
    [Tooltip("Sonup kaybolma suresi.")]
    [SerializeField] private float fadeOutDuration = 0.25f;

    [Header("Hasara Gore Siddet")]
    [Tooltip("Acikken buyuk hasar daha yogun flash verir. Kapaliyken her hasar ayni siddette.")]
    [SerializeField] private bool scaleWithDamage = false;
    [Tooltip("peakAlpha'ya ulasmak icin gereken hasar miktari. Sadece 'Scale With Damage' acikken kullanilir.")]
    [SerializeField] private float damageForMaxAlpha = 20f;
    [Tooltip("Kucuk hasarlarda bile en az bu kadar gorunur olsun (peakAlpha'nin carpani).")]
    [Range(0f, 1f)]
    [SerializeField] private float minAlphaRatio = 0.35f;

    #endregion

    #region Private Fields

    private Image flashImage;
    private Coroutine flashRoutine;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        flashImage = GetComponent<Image>();

        // Overlay'in dokunmayi yutmasini engelle — acikken joystick/butonlar calismaz hale gelirdi
        flashImage.raycastTarget = false;
        SetAlpha(0f);
    }

    private void OnEnable()
    {
        player.OnPlayerDamaged += HandlePlayerDamaged;
    }

    private void OnDisable()
    {
        player.OnPlayerDamaged -= HandlePlayerDamaged;

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }

        SetAlpha(0f);
    }

    #endregion

    #region Private Methods

    private void HandlePlayerDamaged(float damageAmount)
    {
        float targetAlpha = CalculateTargetAlpha(damageAmount);

        // Ust uste vurusta onceki flash'i kes, bastan basla (dusman hit flash'iyle ayni desen)
        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(FlashRoutine(targetAlpha));
    }

    private float CalculateTargetAlpha(float damageAmount)
    {
        if (!scaleWithDamage || damageForMaxAlpha <= 0f)
            return peakAlpha;

        float ratio = Mathf.Clamp01(damageAmount / damageForMaxAlpha);
        return peakAlpha * Mathf.Lerp(minAlphaRatio, 1f, ratio);
    }

    private IEnumerator FlashRoutine(float targetAlpha)
    {
        // Time.timeScale=0 olsa bile calissin (olum aninda GameOverUI oyunu durdurabiliyor)
        yield return FadeAlpha(0f, targetAlpha, fadeInDuration);

        float held = 0f;
        while (held < holdDuration)
        {
            held += Time.unscaledDeltaTime;
            yield return null;
        }

        yield return FadeAlpha(targetAlpha, 0f, fadeOutDuration);

        SetAlpha(0f);
        flashRoutine = null;
    }

    private IEnumerator FadeAlpha(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            SetAlpha(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Lerp(from, to, elapsed / duration));
            yield return null;
        }

        SetAlpha(to);
    }

    private void SetAlpha(float alpha)
    {
        if (flashImage == null) return;

        Color color = flashColor;
        color.a = alpha;
        flashImage.color = color;
    }

    #endregion
}

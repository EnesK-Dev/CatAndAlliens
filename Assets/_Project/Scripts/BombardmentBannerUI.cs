using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Bombardiman banner yazilari. Uyari fazinda "BOMB RAIN" (kirmizi outline) kirmizi ekranla birlikte
/// yanip soner; bombardiman bitince "BOSS FIGHT" (rengi gelecek bossdan) kisa sure gorunur. Font olarak
/// combo yazisiyla ayni TMP fontu Inspector'dan atanir. BombardmentDirector event'lerini dinler (gevsek
/// bagli). unscaled zaman kullanir (timeScale'den bagimsiz). Tek sorumluluk: banner gorunumu.
/// </summary>
public class BombardmentBannerUI : MonoBehaviour
{
    #region Serialized Fields
    [Header("Yazilar (font = combo fontu)")]
    [Tooltip("'BOMB RAIN' yazisi — uyari fazinda kirmizi outline'la yanip soner.")]
    [SerializeField] private TMP_Text warningText;

    [Tooltip("'BOSS FIGHT' yazisi — bombardiman bitince boss renginde cikar.")]
    [SerializeField] private TMP_Text bossFightText;

    [Header("Uyari (BOMB RAIN)")]
    [SerializeField] private Color warningFill = Color.white;
    [SerializeField] private Color warningOutline = new Color(0.9f, 0f, 0f, 1f);
    [Range(0f, 1f)] [SerializeField] private float warningOutlineWidth = 0.25f;

    [Header("BOSS FIGHT")]
    [SerializeField] private float bossFightShowTime = 1.8f;
    [SerializeField] private float bossFightFadeTime = 0.4f;
    [Tooltip("Okunakli olsun diye ince koyu outline.")]
    [SerializeField] private Color bossFightOutline = Color.black;
    [Range(0f, 1f)] [SerializeField] private float bossFightOutlineWidth = 0.2f;
    #endregion

    #region Private Fields
    private Coroutine _routine;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        if (warningText != null)
        {
            warningText.color = warningFill;
            ApplyOutline(warningText, warningOutline, warningOutlineWidth);
            SetAlpha(warningText, 0f);
            warningText.gameObject.SetActive(false);
        }
        if (bossFightText != null)
        {
            ApplyOutline(bossFightText, bossFightOutline, bossFightOutlineWidth);
            SetAlpha(bossFightText, 0f);
            bossFightText.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        BombardmentDirector.OnBombardmentWarning += HandleWarning;
        BombardmentDirector.OnBombardmentFinished += HandleFinished;
    }

    private void OnDisable()
    {
        BombardmentDirector.OnBombardmentWarning -= HandleWarning;
        BombardmentDirector.OnBombardmentFinished -= HandleFinished;
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
    }
    #endregion

    #region Event Handlers
    private void HandleWarning(float duration, int pulses)
    {
        if (warningText == null) return;
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(WarningRoutine(duration, Mathf.Max(1, pulses)));
    }

    private void HandleFinished(Color bossColor)
    {
        if (bossFightText == null) return;
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(BossFightRoutine(bossColor));
    }
    #endregion

    #region Routines
    private IEnumerator WarningRoutine(float duration, int pulses)
    {
        warningText.gameObject.SetActive(true);
        float per = duration / pulses;
        float up = per * 0.4f, down = per * 0.6f;
        for (int i = 0; i < pulses; i++)
        {
            yield return Fade(warningText, 0f, 1f, up);
            yield return Fade(warningText, 1f, 0f, down);
        }
        SetAlpha(warningText, 0f);
        warningText.gameObject.SetActive(false);
        _routine = null;
    }

    private IEnumerator BossFightRoutine(Color bossColor)
    {
        bossColor.a = 1f;
        bossFightText.color = bossColor; // rengi bossdan
        bossFightText.gameObject.SetActive(true);

        yield return Fade(bossFightText, 0f, 1f, 0.25f);
        float t = 0f;
        while (t < bossFightShowTime) { t += Time.unscaledDeltaTime; yield return null; }
        yield return Fade(bossFightText, 1f, 0f, bossFightFadeTime);

        bossFightText.gameObject.SetActive(false);
        _routine = null;
    }

    private IEnumerator Fade(TMP_Text txt, float from, float to, float dur)
    {
        if (dur <= 0f) { SetAlpha(txt, to); yield break; }
        float e = 0f;
        while (e < dur)
        {
            e += Time.unscaledDeltaTime;
            SetAlpha(txt, Mathf.Lerp(from, to, e / dur));
            yield return null;
        }
        SetAlpha(txt, to);
    }
    #endregion

    #region Helpers
    private void SetAlpha(TMP_Text txt, float a)
    {
        if (txt == null) return;
        Color c = txt.color; c.a = a; txt.color = c;
    }

    /// <summary>TMP outline'i material instance uzerinden uygular (paylasilan combo fontunu bozmaz).</summary>
    private void ApplyOutline(TMP_Text txt, Color color, float width)
    {
        var mat = txt.fontMaterial; // instance olusturur
        mat.SetColor(ShaderUtilities.ID_OutlineColor, color);
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
    }
    #endregion
}

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Sahne geçişlerini yönetir. Fade (siyaha kararma) efekti ile sahne yükler.
/// Her sahnede kendi fade paneli olur; sahne açılınca siyahtan açılır,
/// çıkışta siyaha kararıp yeni sahneyi yükler.
/// Butonların OnClick olaylarına public metotları bağlanır.
/// </summary>
public class SceneLoader : MonoBehaviour
{
    #region Serialized Fields

    [Header("Fade Ayarlari")]
    [Tooltip("Tam ekran siyah panelin CanvasGroup'u. Baslangicta alpha = 1 olmali.")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField] private float fadeDuration = 0.5f;

    #endregion

    #region Private Fields

    private Coroutine _fadeRoutine;

    #endregion

    #region Unity Callbacks

    private void Start()
    {
        // Sahne acilinca siyahtan oyuna gecis (fade-in)
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 1f;
            fadeCanvasGroup.blocksRaycasts = false; // Acilis aninda butonlari engellemesin
            _fadeRoutine = StartCoroutine(FadeRoutine(1f, 0f, null));
        }
    }

    private void OnDisable()
    {
        // CLAUDE.md kurali: coroutine'leri OnDisable'da durdur
        if (_fadeRoutine != null)
        {
            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }
    }

    #endregion

    #region Public Methods

    /// <summary>Verilen isimdeki sahneyi fade efekti ile yukler. PLAY ve ANA MENU butonlari buna baglanir.</summary>
    /// <param name="sceneName">Build listesindeki sahne adi (orn: "SampleScene", "MainMenu").</param>
    public void LoadScene(string sceneName)
    {
        if (_fadeRoutine != null) return; // Zaten gecis yapiliyorsa tekrar tetikleme

        if (fadeCanvasGroup == null)
        {
            // Fade paneli atanmamissa direkt yukle (guvenli geri donus)
            SceneManager.LoadScene(sceneName);
            return;
        }

        fadeCanvasGroup.blocksRaycasts = true; // Gecis sirasinda tiklamayi kilitle
        _fadeRoutine = StartCoroutine(FadeRoutine(0f, 1f, () => SceneManager.LoadScene(sceneName)));
    }

    /// <summary>Mevcut sahneyi yeniden yukler. (Tekrar oyna icin hazir; istege bagli.)</summary>
    public void ReloadCurrentScene()
    {
        LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>Oyundan cikar. Editor'de oynatma modunu durdurur, build'de uygulamayi kapatir.</summary>
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    #endregion

    #region Private Methods

    /// <summary>Alpha degerini "from" -> "to" yumusakca degistirir. Bitince onComplete'i calistirir.</summary>
    private IEnumerator FadeRoutine(float from, float to, System.Action onComplete)
    {
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime; // Oyun durdurulsa bile (timeScale=0) fade calissin
            fadeCanvasGroup.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
            yield return null;
        }

        fadeCanvasGroup.alpha = to;
        _fadeRoutine = null;

        onComplete?.Invoke();
    }

    #endregion
}

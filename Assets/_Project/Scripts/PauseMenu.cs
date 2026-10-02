using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Sag ustteki DURAKLAT butonu + duraklatma paneli (Continue / Restart / Main Menu). Butona basinca
/// Time.timeScale=0 (oyun donar) + panel acilir. Continue kaldigi yerden devam; Restart sahneyi yeniden
/// yukler; Main Menu ana menuye doner. Sahne degistirmeden ONCE timeScale=1 yapilir (yeni sahne donuk gelmesin).
/// Tek-sorumluluk: sadece duraklatma UI'si; sahne yukleme SceneLoader'da.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    #region Serialized Fields
    [Header("Referanslar")]
    [Tooltip("Sag ustteki duraklat butonu.")]
    [SerializeField] private Button pauseButton;

    [Tooltip("Duraklatma paneli (dimmer + butonlar). Baslangicta KAPALI olur.")]
    [SerializeField] private GameObject pausePanel;

    [Tooltip("Bu run'da toplanan core sayisini gosteren yazi (yaninda core ikonu). Bos ise atlanir.")]
    [SerializeField] private TMP_Text coreCountText;

    [SerializeField] private Button continueButton;
    [Tooltip("SHOP butonu — MainMenu'yu yukleyip shop panelini actirir (run core'lari once bankalanir).")]
    [SerializeField] private Button shopButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button mainMenuButton;

    [Tooltip("Sahne yukleme (fade). Bos ise otomatik bulunur.")]
    [SerializeField] private SceneLoader sceneLoader;

    [Tooltip("Ana menu sahnesinin build listesindeki adi.")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    #endregion

    #region Private Fields
    private bool _paused;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        if (sceneLoader == null) sceneLoader = FindFirstObjectByType<SceneLoader>();
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    private void OnEnable()
    {
        if (pauseButton != null) pauseButton.onClick.AddListener(Pause);
        if (continueButton != null) continueButton.onClick.AddListener(Continue);
        if (restartButton != null) restartButton.onClick.AddListener(Restart);
        if (shopButton != null) shopButton.onClick.AddListener(GoToShop);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(GoMainMenu);
    }

    private void OnDisable()
    {
        if (pauseButton != null) pauseButton.onClick.RemoveListener(Pause);
        if (continueButton != null) continueButton.onClick.RemoveListener(Continue);
        if (restartButton != null) restartButton.onClick.RemoveListener(Restart);
        if (shopButton != null) shopButton.onClick.RemoveListener(GoToShop);
        if (mainMenuButton != null) mainMenuButton.onClick.RemoveListener(GoMainMenu);

        // Guvenlik: bu obje kapanirken oyun donuk kalmasin
        if (_paused) { Time.timeScale = 1f; GameFlow.Paused = false; }
    }
    #endregion

    #region Private Methods
    /// <summary>Oyunu durdurur ve paneli acar.</summary>
    private void Pause()
    {
        if (_paused) return;
        _paused = true;
        Time.timeScale = 0f;
        GameFlow.Paused = true; // ulti sinemasi (ve timeScale'i 1'e cekenler) oyunu yeniden baslatmasin
        if (coreCountText != null) coreCountText.SetText("{0}", CoreManager.CoresThisRun); // bu run'daki toplam core
        if (pausePanel != null) pausePanel.SetActive(true);
    }

    /// <summary>Paneli kapatir ve oyunu kaldigi yerden devam ettirir.</summary>
    private void Continue()
    {
        _paused = false;
        GameFlow.Paused = false;
        Time.timeScale = 1f;
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    /// <summary>Sahneyi yeniden yukler (timeScale ONCE 1'e alinir; yoksa yeni sahne donuk gelir).</summary>
    private void Restart()
    {
        _paused = false;
        GameFlow.Paused = false;
        Time.timeScale = 1f;
        if (sceneLoader != null) sceneLoader.ReloadCurrentScene();
    }

    /// <summary>SHOP'a gider: run core'larini bankalar, MainMenu'yu yukler ve shop panelini actirir.</summary>
    private void GoToShop()
    {
        _paused = false;
        GameFlow.Paused = false;
        Time.timeScale = 1f;
        ShopUI.OpenOnLoad = true;
        CoreManager.BankRunCores(); // run'da toplanan parayi bankaya (shop'ta harcanabilsin)
        if (sceneLoader != null) sceneLoader.LoadScene(mainMenuSceneName);
    }

    /// <summary>Ana menuye doner (timeScale ONCE 1'e alinir).</summary>
    private void GoMainMenu()
    {
        _paused = false;
        GameFlow.Paused = false;
        Time.timeScale = 1f;
        CoreManager.BankRunCores(); // run'da toplanan parayi bankaya aktar (yoksa menuye donunce kaybolur)
        if (sceneLoader != null) sceneLoader.LoadScene(mainMenuSceneName);
    }
    #endregion
}

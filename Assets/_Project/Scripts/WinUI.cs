using UnityEngine;

/// <summary>
/// Kazanma kosulu saglaninca "VICTORY" ekranini gosterir. WinConditionManager'in
/// static OnGameWon event'ini dinler; paneli acar ve oyunu dondurur. GameOverUI ile
/// birebir ayni desen. Butonlar SceneLoader'a baglanir.
/// </summary>
public class WinUI : MonoBehaviour
{
    #region Serialized Fields

    [Header("Referanslar")]
    [Tooltip("Kazanilinca acilacak Win paneli. Baslangicta kapali olmali.")]
    [SerializeField] private GameObject winPanel;

    [Tooltip("Sahne gecislerini yapan SceneLoader objesi.")]
    [SerializeField] private SceneLoader sceneLoader;

    [Header("Ayarlar")]
    [Tooltip("Ana menu sahnesinin build listesindeki adi.")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Tooltip("Kazanilinca oyun zamani (Time.timeScale) 0'a cekilip dusmanlar dondurulsun mu?")]
    [SerializeField] private bool pauseGameOnWin = true;

    #endregion

    #region Unity Callbacks

    private void OnEnable()
    {
        WinConditionManager.OnGameWon += HandleGameWon;
    }

    private void OnDisable()
    {
        // CLAUDE.md kurali: cleanup - abonelikten cik (leak ve cift cagri onlenir)
        WinConditionManager.OnGameWon -= HandleGameWon;
    }

    private void Start()
    {
        // Guvenlik: panel sahne acilisinda kapali baslasin
        if (winPanel != null)
            winPanel.SetActive(false);
    }

    #endregion

    #region Public Methods

    /// <summary>Main Menu butonuna baglanir. Zamani normale dondurup ana menu sahnesini yukler.</summary>
    public void GoToMainMenu()
    {
        Time.timeScale = 1f; // timeScale sahneler arasi tasinir - donmus kalmasin diye sifirla
        if (sceneLoader != null)
            sceneLoader.LoadScene(mainMenuSceneName);
    }

    /// <summary>Restart butonuna baglanir. Zamani normale dondurup mevcut sahneyi yeniden yukler.</summary>
    public void RestartGame()
    {
        Time.timeScale = 1f;
        if (sceneLoader != null)
            sceneLoader.ReloadCurrentScene();
    }

    #endregion

    #region Private Methods

    /// <summary>Kazanma kosulu saglaninca cagrilir: paneli acar, istenirse oyunu dondurur.</summary>
    private void HandleGameWon()
    {
        if (winPanel != null)
            winPanel.SetActive(true);

        if (pauseGameOnWin)
            Time.timeScale = 0f;
    }

    #endregion
}

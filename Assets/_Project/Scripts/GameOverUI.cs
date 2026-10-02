using UnityEngine;
using TMPro;

/// <summary>
/// Player öldüğünde "YOU DIED" ekranını gösterir. player.cs'in static OnPlayerDied
/// event'ini dinler; paneli açar ve (istenirse) oyunu dondurur. Butonlar SceneLoader'a
/// bağlanır: Main Menu → MainMenu sahnesi, Restart → mevcut sahne yeniden yüklenir.
/// </summary>
public class GameOverUI : MonoBehaviour
{
    #region Serialized Fields

    [Header("Referanslar")]
    [Tooltip("Ölünce açılacak GameOver paneli (YOU DIED + butonlar). Başlangıçta kapalı olmalı.")]
    [SerializeField] private GameObject gameOverPanel;

    [Tooltip("Bu run'da toplanan core sayisini gosteren yazi (yaninda core ikonu). Bos ise atlanir.")]
    [SerializeField] private TMP_Text coreCountText;

    [Tooltip("Sahne geçişlerini yapan SceneLoader objesi. Butonlar dolaylı olarak buna bağlanır.")]
    [SerializeField] private SceneLoader sceneLoader;

    [Header("Ayarlar")]
    [Tooltip("Ana menü sahnesinin build listesindeki adı.")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Tooltip("Ölünce oyun zamanı (Time.timeScale) 0'a çekilip düşmanlar dondurulsun mu?")]
    [SerializeField] private bool pauseGameOnDeath = true;

    #endregion

    #region Unity Callbacks

    private void OnEnable()
    {
        player.OnPlayerDied += HandlePlayerDied;
    }

    private void OnDisable()
    {
        // CLAUDE.md kuralı: cleanup — abonelikten çık (leak ve çift çağrı önlenir)
        player.OnPlayerDied -= HandlePlayerDied;
    }

    private void Start()
    {
        // Güvenlik: panel sahne açılışında kapalı başlasın
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    #endregion

    #region Public Methods

    /// <summary>Main Menu butonuna bağlanır. Zamanı normale döndürüp ana menü sahnesini yükler.</summary>
    public void GoToMainMenu()
    {
        SfxManager.Play(SfxId.ButtonClick);
        Time.timeScale = 1f; // timeScale sahneler arası taşınır — donmuş kalmasın diye sıfırla
        if (sceneLoader != null)
            sceneLoader.LoadScene(mainMenuSceneName);
    }

    /// <summary>SHOP butonuna baglanir. Zamani normale dondurup MainMenu'yu yukler ve shop panelini actirir.
    /// (Olumde run core'lari zaten HandleRunEnd ile bankaya yazildi.)</summary>
    public void GoToShop()
    {
        SfxManager.Play(SfxId.ButtonClick);
        Time.timeScale = 1f;
        ShopUI.OpenOnLoad = true;
        if (sceneLoader != null)
            sceneLoader.LoadScene(mainMenuSceneName);
    }

    /// <summary>Restart butonuna bağlanır. Zamanı normale döndürüp mevcut sahneyi yeniden yükler.</summary>
    public void RestartGame()
    {
        SfxManager.Play(SfxId.ButtonClick);
        Time.timeScale = 1f;
        if (sceneLoader != null)
            sceneLoader.ReloadCurrentScene();
    }

    #endregion

    #region Private Methods

    /// <summary>Player öldüğünde çağrılır: paneli açar, istenirse oyunu dondurur.</summary>
    private void HandlePlayerDied()
    {
        GameFlow.Ended = true; // olum sonrasi ulti sinemasi timeScale'i 1'e dondurmesin

        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        if (coreCountText != null)
        {
            // Olumde HandleRunEnd sayaci sifirlamis olabilir -> canli 0 ise son run degerini goster
            int n = CoreManager.CoresThisRun > 0 ? CoreManager.CoresThisRun : CoreManager.LastRunCores;
            coreCountText.SetText("{0}", n);
        }

        if (pauseGameOnDeath)
            Time.timeScale = 0f;
    }

    #endregion
}

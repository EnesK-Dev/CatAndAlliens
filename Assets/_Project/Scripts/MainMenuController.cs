using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ana menunun mantigini yonetir: PLAY ile oyunu baslatma, Settings/Credits
/// panellerini acip kapatma ve ses toggle'inin baslangic degerini kayitli
/// ayara gore ayarlama. Butonlarin OnClick olaylari bu metotlara baglanir.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    #region Serialized Fields

    [Header("Sahne Gecisi")]
    [SerializeField] private SceneLoader sceneLoader;
    [Tooltip("Build listesindeki oyun sahnesinin adi.")]
    [SerializeField] private string gameSceneName = "SampleScene";

    [Header("Paneller")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject creditsPanel;

    [Header("Ses Ayari")]
    [SerializeField] private AudioManager audioManager;
    [SerializeField] private Toggle soundToggle;

    #endregion

    #region Unity Callbacks

    private void Start()
    {
        // Baslangicta paneller kapali olsun (Editor'de unutulursa diye garantiye al)
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false);

        // Toggle gorseli kayitli ayarla eslessin (ses kapaliysa tik kalkik gelsin)
        if (soundToggle != null && audioManager != null)
            soundToggle.isOn = audioManager.IsSoundEnabled();
    }

    #endregion

    #region Public Methods

    /// <summary>PLAY butonuna baglanir. Oyun sahnesini fade ile yukler.</summary>
    public void PlayGame()
    {
        if (sceneLoader != null)
            sceneLoader.LoadScene(gameSceneName);
    }

    /// <summary>SETTINGS butonuna baglanir. Ayarlar panelini acar.</summary>
    public void OpenSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    /// <summary>Ayarlar panelindeki kapat (X) butonuna baglanir.</summary>
    public void CloseSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    /// <summary>CREDITS butonuna baglanir. Credits panelini acar.</summary>
    public void OpenCredits()
    {
        if (creditsPanel != null) creditsPanel.SetActive(true);
    }

    /// <summary>Credits panelindeki kapat (X) butonuna baglanir.</summary>
    public void CloseCredits()
    {
        if (creditsPanel != null) creditsPanel.SetActive(false);
    }

    #endregion
}

using UnityEngine;

/// <summary>
/// Oyunun global ses durumunu (acik/kapali) yonetir ve PlayerPrefs ile kalici yapar.
/// AudioListener.volume her sahne yuklendiginde 1'e doner; bu yuzden her sahnede
/// bu component Awake() icinde kayitli ayari okuyup tekrar uygular.
/// </summary>
public class AudioManager : MonoBehaviour
{
    #region Private Fields

    // PlayerPrefs anahtari. 1 = ses acik, 0 = ses kapali.
    private const string SoundEnabledKey = "SoundEnabled";

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        // Sahne acilir acilmaz kayitli ayari uygula (ses kapaliysa kapali baslasin)
        ApplyVolume(IsSoundEnabled());
    }

    #endregion

    #region Public Methods

    /// <summary>Sesi acar/kapatir, ayari kaydeder ve aninda uygular. Settings Toggle'ina baglanir.</summary>
    /// <param name="enabled">true = ses acik, false = ses kapali.</param>
    public void SetSoundEnabled(bool enabled)
    {
        PlayerPrefs.SetInt(SoundEnabledKey, enabled ? 1 : 0);
        PlayerPrefs.Save(); // Diske hemen yaz (oyun aniden kapanirsa ayar kaybolmasin)
        ApplyVolume(enabled);
    }

    /// <summary>Kayitli ses durumunu dondurur. Toggle'in baslangic degerini ayarlamak icin okunur.</summary>
    /// <returns>true = ses acik (varsayilan), false = ses kapali.</returns>
    public bool IsSoundEnabled()
    {
        // Hic kayit yoksa varsayilan 1 (acik)
        return PlayerPrefs.GetInt(SoundEnabledKey, 1) == 1;
    }

    #endregion

    #region Private Methods

    /// <summary>Verilen duruma gore global ses seviyesini ayarlar.</summary>
    private void ApplyVolume(bool enabled)
    {
        AudioListener.volume = enabled ? 1f : 0f;
    }

    #endregion
}

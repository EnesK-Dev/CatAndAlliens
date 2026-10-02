using System;
using UnityEngine;

/// <summary>
/// Muzik ve efekt (SFX) ses seviyelerini AYRI tutar (0-1), PlayerPrefs ile kalici yapar. Slider'lar buraya
/// yazar; SfxManager buradan okur (canli guncelleme icin OnChanged). UnityEngine.AudioSettings ile karismasin
/// diye 'VolumeSettings' adi kullanildi. Global ac/kapa hala AudioManager (AudioListener.volume) uzerinden.
/// </summary>
public static class VolumeSettings
{
    #region Constants / Fields
    private const string MusicKey = "MusicVolume";
    private const string SfxKey = "SfxVolume";
    private static float _music = -1f; // -1 = henuz yuklenmedi
    private static float _sfx = -1f;
    #endregion

    /// <summary>Muzik veya efekt sesi degisince firlar (canli kaynaklar guncellensin diye).</summary>
    public static event Action OnChanged;

    #region Public API
    /// <summary>Muzik ses seviyesi (0-1). Kalici (PlayerPrefs).</summary>
    public static float Music
    {
        get { if (_music < 0f) _music = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, 1f)); return _music; }
        set { _music = Mathf.Clamp01(value); PlayerPrefs.SetFloat(MusicKey, _music); PlayerPrefs.Save(); OnChanged?.Invoke(); }
    }

    /// <summary>Efekt (SFX) ses seviyesi (0-1). Kalici (PlayerPrefs).</summary>
    public static float Sfx
    {
        get { if (_sfx < 0f) _sfx = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, 1f)); return _sfx; }
        set { _sfx = Mathf.Clamp01(value); PlayerPrefs.SetFloat(SfxKey, _sfx); PlayerPrefs.Save(); OnChanged?.Invoke(); }
    }
    #endregion
}

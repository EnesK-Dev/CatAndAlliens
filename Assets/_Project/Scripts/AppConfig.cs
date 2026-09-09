using UnityEngine;

/// <summary>
/// Uygulama basinda (sahne yuklenmeden) bir kez calisan global ayar. Mobilde Unity'nin
/// varsayilan FPS'i cogu zaman 30'a duser -> hareket "agir/kekeme" hisseder. targetFrameRate=60
/// ile ekran destekliyorsa 60 FPS'e kilitlenir. vSyncCount=0 olmadan targetFrameRate yoksayilir.
/// GameObject/sahne kurulumu GEREKMEZ ([RuntimeInitializeOnLoadMethod] otomatik cagrilir).
/// </summary>
public static class AppConfig
{
    #region Constants
    private const int TargetFps = 60;
    #endregion

    #region Public Methods
    /// <summary>Sahne yuklenmeden once bir kez calisir; FPS hedefini uygular (uygulama boyu gecerli).</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        QualitySettings.vSyncCount = 0;   // vSync targetFrameRate'i ezmesin
        Application.targetFrameRate = TargetFps;
    }
    #endregion
}

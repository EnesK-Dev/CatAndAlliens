using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Build'de tamamen silinen debug log yardımcısı. Log/Warning çağrıları sadece
/// Editor ve Development Build'de derlenir; release build'de çağrı satırı
/// argümanlarıyla birlikte compiler tarafından kaldırılır (allocation = 0).
/// </summary>
public static class GameLog
{
    #region Private Fields
    private static bool isEnabled = true;
    #endregion

    #region Public Methods
    /// <summary>Log çıktısını çalışma anında açar/kapatır. Sadece Editor ve Development Build'i etkiler.</summary>
    public static bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    /// <summary>Bilgi log'u yazar. Release build'de çağrı tamamen kaldırılır.</summary>
    /// <param name="message">Yazılacak mesaj.</param>
    /// <param name="context">Console'da tıklanınca seçilecek obje (opsiyonel).</param>
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Log(string message, Object context = null)
    {
        if (isEnabled)
            Debug.Log(message, context);
    }

    /// <summary>Uyarı log'u yazar. Release build'de çağrı tamamen kaldırılır.</summary>
    /// <param name="message">Yazılacak mesaj.</param>
    /// <param name="context">Console'da tıklanınca seçilecek obje (opsiyonel).</param>
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Warning(string message, Object context = null)
    {
        if (isEnabled)
            Debug.LogWarning(message, context);
    }

    /// <summary>Hata log'u yazar. Bu metot BİLEREK strip edilmez — release build'de de görünür.</summary>
    /// <param name="message">Yazılacak mesaj.</param>
    /// <param name="context">Console'da tıklanınca seçilecek obje (opsiyonel).</param>
    public static void Error(string message, Object context = null)
    {
        Debug.LogError(message, context);
    }
    #endregion
}

using UnityEngine;

/// <summary>
/// Tum dusmanlarin AI'sini kisa sureligine donduran hafif STATIK gecit. Sahne objesi/MonoBehaviour
/// gerektirmez. Dusmanlar Update/FixedUpdate/temas-hasari icinde EnemyFreeze.IsFrozen'i kontrol eder;
/// donmus haldeyken hareket etmez, yeni saldiri baslatmaz, temas hasari vermez (oyuncu serbest kalir).
///
/// Kullanim: upgrade paneli kapaninca EnemyFreeze.FreezeFor(0.6f) -> kart secip oyuna donunce oyuncuya
/// kisa bir nefes alma ani. Olcekli zaman (Time.time) kullanir: timeScale=0 iken (panel acik) sayac
/// ilerlemez, panel kapanip timeScale=1 olunca 0.6sn gercek sayar.
/// </summary>
public static class EnemyFreeze
{
    #region Private Fields
    private static float _frozenUntil = -1f;
    #endregion

    #region Public API
    /// <summary>Su an dusmanlar donmus mu.</summary>
    public static bool IsFrozen => Time.time < _frozenUntil;

    /// <summary>Dusmanlari 'seconds' saniye boyunca dondurur (olcekli zaman). 0/negatif ise hicbir sey yapmaz.</summary>
    public static void FreezeFor(float seconds)
    {
        if (seconds <= 0f) return;
        _frozenUntil = Time.time + seconds;
    }
    #endregion
}

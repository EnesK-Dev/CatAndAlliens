using UnityEngine;

/// <summary>
/// Kazanma kosulunu izler: DifficultyManager.ElapsedTime belirlenen sureye ulasinca
/// OnGameWon bir kez firlar. WinUI bunu dinler. God GameManager degil - sadece
/// kazanma kosulundan sorumlu, gevsek bagli (static event, DifficultyManager pattern'i).
/// </summary>
public class WinConditionManager : MonoBehaviour
{
    #region Serialized Fields
    [Tooltip("Bu sureye (saniye) hayatta kalinirsa oyun kazanilir. 600 = 10 dakika.")]
    [SerializeField] private float winTimeSeconds = 600f;
    #endregion

    #region Private Fields
    private bool _hasWon;
    #endregion

    #region Static API
    /// <summary>Kazanma kosulu saglaninca bir kez firlar.</summary>
    public static event System.Action OnGameWon;
    #endregion

    #region Unity Callbacks
    private void Update()
    {
        if (_hasWon) return;
        if (DifficultyManager.ElapsedTime < winTimeSeconds) return;

        _hasWon = true;
        OnGameWon?.Invoke();
    }
    #endregion
}

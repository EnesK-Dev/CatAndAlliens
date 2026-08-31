using UnityEngine;

/// <summary>
/// Kazanma kosulunu izler. Iki yol: (1) FINAL boss yenilince (BossController.OnBossDefeated, isFinal=true),
/// (2) belirlenen sureye hayatta kalinca (fallback). Kosul saglaninca OnGameWon BIR KEZ firlar; WinUI dinler.
/// God GameManager degil - sadece kazanma kosulundan sorumlu, gevsek bagli (static event).
/// </summary>
public class WinConditionManager : MonoBehaviour
{
    #region Serialized Fields
    [Tooltip("Final boss yenilince oyun kazanilsin mi (ana kazanma yolu).")]
    [SerializeField] private bool winOnFinalBossDefeated = true;

    [Tooltip("Bu sureye (saniye) hayatta kalinirsa da kazanilir (fallback). 600 = 10 dakika. " +
             "Boss'u yenmeyi ZORUNLU kilmak istersen cok buyuk bir deger ver.")]
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
    private void OnEnable()
    {
        BossController.OnBossDefeated += HandleBossDefeated;
    }

    private void OnDisable()
    {
        BossController.OnBossDefeated -= HandleBossDefeated;
    }

    private void Update()
    {
        if (_hasWon) return;
        if (DifficultyManager.ElapsedTime < winTimeSeconds) return;
        Win();
    }
    #endregion

    #region Private Methods
    private void HandleBossDefeated(bool wasFinal)
    {
        if (winOnFinalBossDefeated && wasFinal)
            Win();
    }

    private void Win()
    {
        if (_hasWon) return;
        _hasWon = true;
        OnGameWon?.Invoke();
    }
    #endregion
}

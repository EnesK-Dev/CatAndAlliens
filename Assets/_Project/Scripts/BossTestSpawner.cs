using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// GECICI TEST araci: bir klavye tusuna (varsayilan B) basinca boss spawn eder. FAZ 1'i test etmek icin.
/// FAZ 4'te gercek BossManager (zamanlanmis mini-boss + final boss) bunun yerine gececek — o zaman silinir.
/// </summary>
public class BossTestSpawner : MonoBehaviour
{
    #region Serialized Fields
    [Tooltip("Spawn edilecek boss prefab'i (B tusu).")]
    [SerializeField] private BossController bossPrefab;

    [Tooltip("Enemy-tabanli boss (ornek SplitterBoss). N tusu: bombardiman + BOSS FIGHT ile spawn.")]
    [SerializeField] private GameObject enemyBossPrefab;

    [Tooltip("Oyuncuya gore spawn ofseti (dunya birimi).")]
    [SerializeField] private Vector2 spawnOffset = new Vector2(0f, 4f);
    #endregion

    #region Private Fields
    private player _player;
    #endregion

    #region Unity Callbacks
    private void Awake() => _player = FindFirstObjectByType<player>();

    private void Update()
    {
        if (Keyboard.current == null) return;

        if (bossPrefab != null && Keyboard.current.bKey.wasPressedThisFrame)
        {
            Vector3 basePos = _player != null ? _player.transform.position : Vector3.zero;
            Instantiate(bossPrefab, basePos + (Vector3)spawnOffset, Quaternion.identity);
        }

        // N: enemy-tabanli boss (SplitterBoss) — bombardiman + BOSS FIGHT ile giris
        if (enemyBossPrefab != null && Keyboard.current.nKey.wasPressedThisFrame)
        {
            var sr = enemyBossPrefab.GetComponent<SpriteRenderer>();
            Color col = sr != null ? sr.color : Color.white;
            BombardmentDirector.Trigger(col, SpawnEnemyBoss);
        }
    }

    /// <summary>Bombardiman bitince enemy-tabanli boss'u oyuncunun yaninda spawn eder.</summary>
    private void SpawnEnemyBoss()
    {
        Vector3 basePos = _player != null ? _player.transform.position : Vector3.zero;
        Instantiate(enemyBossPrefab, basePos + (Vector3)spawnOffset, Quaternion.identity);
    }
    #endregion
}

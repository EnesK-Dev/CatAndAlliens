using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BombRainSystem : MonoBehaviour
{
    #region Serialized Fields
    [Header("Takip Ayarları")]
    [SerializeField] private Transform followTarget;

    [Header("Alan Ayarları")]
    [SerializeField] private float areaWidth = 10f;
    [SerializeField] private float areaHeight = 10f;

    [Header("Bomba Zamanlama Ayarları")]
    [SerializeField] private int bombsPerRound = 5;
    [SerializeField] private float minBombInterval = 0.3f;
    [SerializeField] private float maxBombInterval = 1f;
    [SerializeField] private float roundInterval = 5f;

    [Header("Bomba Hasar ve Süre Ayarları")]
    [SerializeField] private float warningDuration = 2f;
    [SerializeField] private float explosionDuration = 0.5f;
    [SerializeField] private float explosionRadius = 1f;
    [SerializeField] private float explosionDamage = 20f;

    [Header("Layer Ayarları")]
    [SerializeField] private LayerMask playerLayer;

    [Header("Object Pool")]
    [SerializeField] private BombWarning bombWarningPrefab;
    [SerializeField] private int poolSize = 10;
    #endregion

    #region Private Fields
    private readonly List<BombWarning> pool = new List<BombWarning>();
    private readonly Queue<BombWarning> available = new Queue<BombWarning>();
    private Coroutine rainCoroutine;
    #endregion

    #region Unity Callbacks
    private void Start()
    {
        if (bombWarningPrefab == null)
        {
            Debug.LogError("[BombRainSystem] Bomb Warning Prefab atanmamış! Inspector'dan prefab'ı ata.", this);
            return;
        }
        BuildPool();
        rainCoroutine = StartCoroutine(BombRainLoop());
    }

    private void OnDisable()
    {
        StopRain();
    }

    private void OnDestroy()
    {
        StopRain();
    }
    #endregion

    #region Public Methods
    /// <summary>Bomba yağmurunu durdurur. UI butonu veya başka bir sistem tarafından çağrılabilir.</summary>
    public void StopRain()
    {
        if (rainCoroutine != null)
        {
            StopCoroutine(rainCoroutine);
            rainCoroutine = null;
        }
    }

    /// <summary>Bomba yağmurunu başlatır veya yeniden başlatır.</summary>
    public void StartRain()
    {
        StopRain();
        rainCoroutine = StartCoroutine(BombRainLoop());
    }
    #endregion

    #region Private Methods
    private void BuildPool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            BombWarning bomb = Instantiate(bombWarningPrefab, transform);
            bomb.gameObject.SetActive(false);
            pool.Add(bomb);
            available.Enqueue(bomb);
        }
    }

    private BombWarning GetFromPool()
    {
        if (available.Count > 0)
            return available.Dequeue();

        return null;
    }

    private void ReturnToPool(BombWarning bomb)
    {
        available.Enqueue(bomb);
    }

    private Vector2 GetRandomPositionInArea()
    {
        Vector2 center = followTarget != null ? (Vector2)followTarget.position : (Vector2)transform.position;
        return new Vector2(
            center.x + Random.Range(-areaWidth * 0.5f, areaWidth * 0.5f),
            center.y + Random.Range(-areaHeight * 0.5f, areaHeight * 0.5f)
        );
    }

    private IEnumerator BombRainLoop()
    {
        while (true)
        {
            yield return StartCoroutine(SpawnBombRound());
            yield return new WaitForSeconds(roundInterval);
        }
    }

    private IEnumerator SpawnBombRound()
    {
        for (int i = 0; i < bombsPerRound; i++)
        {
            BombWarning bomb = GetFromPool();
            if (bomb != null)
            {
                Vector2 spawnPos = GetRandomPositionInArea();
                bomb.Initialize(
                    warningDuration,
                    explosionDuration,
                    explosionRadius,
                    explosionDamage,
                    playerLayer,
                    () => ReturnToPool(bomb)
                );
                bomb.Activate(spawnPos);
            }

            yield return new WaitForSeconds(Random.Range(minBombInterval, maxBombInterval));
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawWireCube(transform.position, new Vector3(areaWidth, areaHeight, 0f));
    }
    #endregion
}

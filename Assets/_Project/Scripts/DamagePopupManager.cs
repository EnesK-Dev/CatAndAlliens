using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hasar sayilari (DamageNumber) icin object pool + spawn yoneticisi.
/// Sahnede tek ornek olur. Dusmanlar statik DamagePopupManager.Show(...) ile cagirir;
/// boylece her hasar kaynagi manager'a dogrudan referans tutmadan sayi gosterebilir.
/// </summary>
public class DamagePopupManager : MonoBehaviour
{
    #region Serialized Fields
    [Header("Pool Ayarlari")]
    [SerializeField] private DamageNumber damageNumberPrefab;
    [SerializeField] private int initialPoolSize = 20;
    #endregion

    #region Private Fields
    private static DamagePopupManager _instance;
    private readonly Queue<DamageNumber> _pool = new Queue<DamageNumber>();
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        // Sahne basina tek manager — ikinciyi sessizce ele
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        PrewarmPool();
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Belirtilen dunya konumunda hasar sayisi gosterir. Sahnede manager yoksa
    /// sessizce hicbir sey yapmaz (Unity fake-null'i dogru kontrol eder).
    /// </summary>
    public static void Show(Vector3 worldPosition, float damage)
    {
        if (_instance != null)
            _instance.SpawnFromPool(worldPosition, damage);
    }
    #endregion

    #region Private Methods
    private void PrewarmPool()
    {
        if (damageNumberPrefab == null)
        {
            Debug.LogWarning("DamagePopupManager: damageNumberPrefab atanmamis — hasar sayilari gosterilmeyecek.");
            return;
        }

        for (int i = 0; i < initialPoolSize; i++)
            _pool.Enqueue(CreateInstance());
    }

    /// <summary>Yeni bir pasif DamageNumber ornegi olusturur (kuyruga eklemez).</summary>
    private DamageNumber CreateInstance()
    {
        DamageNumber instance = Instantiate(damageNumberPrefab, transform);
        instance.gameObject.SetActive(false);
        return instance;
    }

    private void SpawnFromPool(Vector3 worldPosition, float damage)
    {
        if (damageNumberPrefab == null) return;

        // Havuz bosalirsa buyu — patlama anlarinda sayi eksik kalmasin
        DamageNumber instance = _pool.Count > 0 ? _pool.Dequeue() : CreateInstance();

        instance.gameObject.SetActive(true);
        instance.Play(worldPosition, damage, ReturnToPool);
    }

    private void ReturnToPool(DamageNumber instance)
    {
        if (instance == null) return;

        instance.gameObject.SetActive(false);
        _pool.Enqueue(instance);
    }
    #endregion
}

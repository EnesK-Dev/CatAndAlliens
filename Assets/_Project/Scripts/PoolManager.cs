using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Basit, generic object pool. Prefab anahtarli havuzlar tutar: Spawn -> havuzdan cek (yoksa Instantiate),
/// Despawn -> SetActive(false) ile havuza geri koy. Instantiate/Destroy kaynakli GC sicramalarini onler
/// (VS-tarzi cok sayida dusman/mermi icin kritik). Static erisim (DamagePopupManager/CoreManager pattern'i).
///
/// KULLANIM:
///   var go = PoolManager.Spawn(prefab, pos, rot);   // Instantiate yerine
///   PoolManager.Despawn(go);                          // Destroy yerine
///
/// NOT: Yeniden kullanilan obje icin Awake/Start TEKRAR CALISMAZ; sadece OnEnable calisir. Duruma bagli
/// alanlar (can, bayraklar, flash...) OnEnable'da ya da Spawn sonrasi cagrilan bir reset'te sifirlanmali.
/// </summary>
public static class PoolManager
{
    #region Private Fields
    // prefab -> pasif (havuzdaki) instance kuyrugu
    private static readonly Dictionary<GameObject, Queue<GameObject>> _pools = new Dictionary<GameObject, Queue<GameObject>>();
    private static bool _hooked;
    #endregion

    #region Public Methods
    /// <summary>Havuzdan bir instance dondurur (yoksa Instantiate eder), verilen konum/rotasyonla aktif eder.</summary>
    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;
        EnsureHooked();

        if (_pools.TryGetValue(prefab, out Queue<GameObject> queue))
        {
            // Gecerli (yok edilmemis) bir instance bulana kadar kuyruktan cek
            while (queue.Count > 0)
            {
                GameObject pooled = queue.Dequeue();
                if (pooled == null) continue; // sahne degisiminde yok edilmis olabilir
                pooled.transform.SetPositionAndRotation(position, rotation);
                pooled.SetActive(true); // OnEnable burada tetiklenir (reset noktasi)
                return pooled;
            }
        }

        // Havuz bos -> yeni uret ve pool anahtarini isaretle
        GameObject go = Object.Instantiate(prefab, position, rotation);
        PooledObject po = go.GetComponent<PooledObject>();
        if (po == null) po = go.AddComponent<PooledObject>();
        po.SourcePrefab = prefab;
        return go;
    }

    /// <summary>Objeyi havuza geri koyar (SetActive false). Havuzlu degilse normal Destroy eder.</summary>
    public static void Despawn(GameObject go)
    {
        if (go == null) return;

        PooledObject po = go.GetComponent<PooledObject>();
        if (po == null || po.SourcePrefab == null)
        {
            Object.Destroy(go); // havuz disi obje — normal yok et
            return;
        }

        go.SetActive(false); // OnDisable burada tetiklenir (coroutine temizligi vb.)

        if (!_pools.TryGetValue(po.SourcePrefab, out Queue<GameObject> queue))
        {
            queue = new Queue<GameObject>();
            _pools[po.SourcePrefab] = queue;
        }
        queue.Enqueue(go);
    }
    #endregion

    #region Private Methods
    /// <summary>Sahne degisince havuzlari temizle (eski sahnenin yok edilmis objeleri kuyrukta kalmasin).</summary>
    private static void EnsureHooked()
    {
        if (_hooked) return;
        _hooked = true;
        SceneManager.sceneUnloaded += _ => _pools.Clear();
    }
    #endregion
}

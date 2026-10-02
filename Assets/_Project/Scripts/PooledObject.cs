using UnityEngine;

/// <summary>
/// Havuzdan gelen her objeye eklenir; hangi prefab havuzundan geldigini (pool anahtari) tutar.
/// PoolManager.Despawn bunu okuyup objeyi dogru havuza geri koyar. Kod tarafi; elle eklenmez.
/// </summary>
public class PooledObject : MonoBehaviour
{
    /// <summary>Bu instance'in uretildigi kaynak prefab (havuz anahtari). PoolManager doldurur.</summary>
    public GameObject SourcePrefab { get; set; }
}

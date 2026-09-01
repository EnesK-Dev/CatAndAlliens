using UnityEngine;

/// <summary>
/// Silah 1 — Auto-Blaster. BurstShooter gibi SUREKLI mermi atar ama oyuncu tarafinda: menzildeki
/// (ekrandaki) en yakin dusmana nisan alir. Hasari dusuktur. Seviye arttikca ATES HIZI ve HASAR artar.
/// WeaponBase'ten turer; alinmadan calismaz. Mermi = PlayerProjectile (dusmana hasar).
/// </summary>
public class AutoBlasterWeapon : WeaponBase
{
    #region Serialized Fields
    [Header("Auto-Blaster")]
    [Tooltip("Firlatilacak mermi (PlayerProjectile).")]
    [SerializeField] private PlayerProjectile bulletPrefab;

    [Tooltip("Dusman layer'lari — nisan alma icin.")]
    [SerializeField] private LayerMask enemyLayers;

    [Tooltip("Bu menzildeki en yakin dusmana ates eder.")]
    [SerializeField] private float range = 8f;

    [Tooltip("Mermi hizi ve omru.")]
    [SerializeField] private float bulletSpeed = 12f;
    [SerializeField] private float bulletLifetime = 2f;

    [Header("Seviye 1 Degerleri")]
    [SerializeField] private float baseFireInterval = 0.5f;
    [SerializeField] private float baseDamage = 5f;

    [Header("Seviye Basi Artis (upgrade)")]
    [Tooltip("Her seviyede ates araligi bu carpanla azalir (0.88 = %12 hizli).")]
    [SerializeField] private float fireIntervalMultiplierPerLevel = 0.88f;

    [Tooltip("Her seviyede eklenen hasar.")]
    [SerializeField] private float damagePerLevel = 3f;
    #endregion

    #region Private Fields
    private float _nextFireTime;
    private readonly Collider2D[] _hitBuffer = new Collider2D[32];
    #endregion

    #region Unity Callbacks
    private void Update()
    {
        // enabled=false iken (alinmadan) Unity Update cagirmaz; buraya ancak alindiktan sonra gelir.
        if (bulletPrefab == null) return;
        if (Time.time < _nextFireTime) return;

        Transform target = FindNearestEnemy();
        if (target == null) return;

        FireAt(target.position);
        _nextFireTime = Time.time + CurrentFireInterval();
    }
    #endregion

    #region Private Methods
    private float CurrentFireInterval() => baseFireInterval * Mathf.Pow(fireIntervalMultiplierPerLevel, Mathf.Max(0, Level - 1));
    private float CurrentDamage() => baseDamage + damagePerLevel * Mathf.Max(0, Level - 1);

    private Transform FindNearestEnemy()
    {
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, range, _hitBuffer, enemyLayers);
        Transform closest = null;
        float closestSqr = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Collider2D c = _hitBuffer[i];
            if (c == null) continue;
            float d = ((Vector2)c.transform.position - (Vector2)transform.position).sqrMagnitude;
            if (d < closestSqr) { closestSqr = d; closest = c.transform; }
        }
        return closest;
    }

    private void FireAt(Vector3 targetPos)
    {
        Vector2 dir = (Vector2)targetPos - (Vector2)transform.position;
        PlayerProjectile bullet = Instantiate(bulletPrefab, transform.position, Quaternion.identity);
        bullet.Launch(dir, bulletSpeed, CurrentDamage(), bulletLifetime);
    }
    #endregion
}

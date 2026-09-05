using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Silah 1 — Auto-Blaster. Menzildeki en yakin dusmana SUREKLI mermi atar (dusuk hasar).
/// Ayri yukseltmeler: ATES HIZI ve HASAR (her biri kendi track'i, ayri kart).
/// </summary>
public class AutoBlasterWeapon : WeaponBase
{
    #region Serialized Fields
    [Header("Auto-Blaster")]
    [SerializeField] private PlayerProjectile bulletPrefab;
    [SerializeField] private LayerMask enemyLayers;
    [SerializeField] private float range = 8f;
    [SerializeField] private float bulletSpeed = 12f;
    [SerializeField] private float bulletLifetime = 2f;

    [Header("Baslangic (Lv.1) Degerleri")]
    [SerializeField] private float baseFireInterval = 0.5f;
    [SerializeField] private float baseDamage = 5f;

    [Header("Ates Hizi Track'i")]
    [Tooltip("Her ates-hizi yukseltmesinde araligin carpani (0.88 = %12 hizli).")]
    [SerializeField] private float fireIntervalMultiplier = 0.88f;
    [SerializeField] private int maxFireRateLevel = 5;

    [Header("Hasar Track'i")]
    [SerializeField] private float damagePerLevel = 3f;
    [SerializeField] private int maxDamageLevel = 5;
    #endregion

    #region Private Fields
    private int _fireRateLevel;
    private int _damageLevel;
    private float _nextFireTime;
    private readonly Collider2D[] _hitBuffer = new Collider2D[32];
    #endregion

    #region Unity Callbacks
    private void Update()
    {
        if (bulletPrefab == null || Time.time < _nextFireTime) return;

        Transform target = FindNearestEnemy();
        if (target == null) return;

        FireAt(target.position);
        _nextFireTime = Time.time + CurrentFireInterval();
    }
    #endregion

    #region Overrides
    public override void CollectUpgrades(List<WeaponUpgradeOption> into)
    {
        if (_fireRateLevel < maxFireRateLevel)
            into.Add(new WeaponUpgradeOption("Blaster Fire Rate", "+Attack speed", _fireRateLevel + 1,
                () => _fireRateLevel++));

        if (_damageLevel < maxDamageLevel)
            into.Add(new WeaponUpgradeOption("Blaster Damage", "+" + damagePerLevel + " damage", _damageLevel + 1,
                () => _damageLevel++));
    }
    #endregion

    #region Private Methods
    private float CurrentFireInterval() => baseFireInterval * Mathf.Pow(fireIntervalMultiplier, _fireRateLevel);
    private float CurrentDamage() => baseDamage + damagePerLevel * _damageLevel;

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
        // Havuzdan cek (Instantiate yerine) — hizli ateste GC sicramasini onler
        GameObject go = PoolManager.Spawn(bulletPrefab.gameObject, transform.position, Quaternion.identity);
        PlayerProjectile bullet = go.GetComponent<PlayerProjectile>();
        if (bullet != null) bullet.Launch(dir, bulletSpeed, CurrentDamage(), bulletLifetime);
    }
    #endregion
}

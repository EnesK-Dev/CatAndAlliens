using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Silah 2 — Boomerang. En yakin dusmana bumerang atar; ileri gidip geri doner, iki yonde hasar verir.
/// Ayni anda ucan sayi currentCount ile sinirli (tutulmadan yenisi atilmaz). Dusman gormezse atmaz;
/// kediye donunce kisa dinlenir. Ayri yukseltmeler: SAYI (max 3), HIZ, HASAR, DINLENME (cooldown).
/// </summary>
public class BoomerangWeapon : WeaponBase
{
    #region Serialized Fields
    [Header("Boomerang")]
    [SerializeField] private PlayerBoomerang boomerangPrefab;
    [SerializeField] private LayerMask enemyLayers;
    [SerializeField] private float detectRange = 10f;
    [SerializeField] private float outDistance = 5f;
    [SerializeField] private float spin = 720f;
    [SerializeField] private float catchDistance = 0.6f;
    [Tooltip("Birden fazla bumerang atarken atislar arasi bekleme (ust uste cikmasin).")]
    [SerializeField] private float throwStagger = 0.25f;

    [Header("Baslangic (Lv.1) Degerleri")]
    [SerializeField] private float baseSpeed = 10f;
    [SerializeField] private float baseDamage = 12f;
    [Tooltip("Kediye donunce bir sonraki atistan once beklenen sure. Cooldown track'i azaltir.")]
    [SerializeField] private float baseRestCooldown = 1.2f;

    [Header("Sayi Track'i (max 3)")]
    [Tooltip("Kac kez +1 alinabilir (2 = max 3 bumerang: 1+2).")]
    [SerializeField] private int maxCountLevel = 2;

    [Header("Hiz Track'i")]
    [SerializeField] private float speedPerLevel = 1.5f;
    [SerializeField] private int maxSpeedLevel = 5;

    [Header("Hasar Track'i")]
    [SerializeField] private float damagePerLevel = 6f;
    [SerializeField] private int maxDamageLevel = 5;

    [Header("Dinlenme (Cooldown) Track'i")]
    [Tooltip("Her seviyede dinlenme carpani (0.85 = %15 kisa).")]
    [SerializeField] private float restCooldownMultiplier = 0.85f;
    [SerializeField] private float minRestCooldown = 0.2f;
    [SerializeField] private int maxCooldownLevel = 5;
    #endregion

    #region Private Fields
    private int _countLevel, _speedLevel, _damageLevel, _cooldownLevel;
    private int _inFlight;
    private float _nextThrowTime;
    private readonly Collider2D[] _hitBuffer = new Collider2D[32];
    #endregion

    #region Unity Callbacks
    private void Update()
    {
        if (boomerangPrefab == null) return;
        if (_inFlight >= CurrentCount()) return;    // hepsi havada — tutulmadan atma
        if (Time.time < _nextThrowTime) return;

        Transform target = FindNearestEnemy();
        if (target == null) return; // dusman gormezse ATMA

        Vector2 dir = (Vector2)target.position - (Vector2)transform.position;
        Throw(dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right);
        _nextThrowTime = Time.time + throwStagger;
    }
    #endregion

    #region Overrides
    public override void CollectUpgrades(List<WeaponUpgradeOption> into)
    {
        if (_countLevel < maxCountLevel)
            into.Add(new WeaponUpgradeOption("Boomerang +1", "One more boomerang (max 3)", _countLevel + 1, () => _countLevel++));
        if (_speedLevel < maxSpeedLevel)
            into.Add(new WeaponUpgradeOption("Boomerang Speed", "Flies faster", _speedLevel + 1, () => _speedLevel++));
        if (_damageLevel < maxDamageLevel)
            into.Add(new WeaponUpgradeOption("Boomerang Damage", "+" + damagePerLevel + " damage", _damageLevel + 1, () => _damageLevel++));
        if (_cooldownLevel < maxCooldownLevel)
            into.Add(new WeaponUpgradeOption("Boomerang Cooldown", "Throws sooner", _cooldownLevel + 1, () => _cooldownLevel++));
    }
    #endregion

    #region Private Methods
    private int CurrentCount() => 1 + _countLevel;
    private float CurrentSpeed() => baseSpeed + speedPerLevel * _speedLevel;
    private float CurrentDamage() => baseDamage + damagePerLevel * _damageLevel;
    private float CurrentRestCooldown() => Mathf.Max(minRestCooldown, baseRestCooldown * Mathf.Pow(restCooldownMultiplier, _cooldownLevel));

    private Transform FindNearestEnemy()
    {
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, detectRange, _hitBuffer, enemyLayers);
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

    private void Throw(Vector2 dir)
    {
        PlayerBoomerang b = Instantiate(boomerangPrefab, transform.position, Quaternion.identity);
        _inFlight++;
        b.Launch(transform, dir, outDistance, CurrentSpeed(), CurrentDamage(), spin, catchDistance, OnBoomerangReturned);
    }

    private void OnBoomerangReturned()
    {
        _inFlight = Mathf.Max(0, _inFlight - 1);
        _nextThrowTime = Mathf.Max(_nextThrowTime, Time.time + CurrentRestCooldown()); // donunce kisa dinlen
    }
    #endregion
}

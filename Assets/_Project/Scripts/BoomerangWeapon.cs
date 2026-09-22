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
    [SerializeField] private int maxSpeedLevel = 20;

    [Header("Hasar Track'i")]
    [SerializeField] private float damagePerLevel = 15f;
    [Tooltip("Hasar bilesik carpani (her level x bu). 1 = duz additive.")]
    [SerializeField] private float damageMult = 1.14f;
    [SerializeField] private int maxDamageLevel = 20;

    [Header("Dinlenme (Cooldown) Track'i")]
    [Tooltip("Her seviyede dinlenme carpani (0.85 = %15 kisa).")]
    [SerializeField] private float restCooldownMultiplier = 0.85f;
    [SerializeField] private float minRestCooldown = 0.1f;
    [SerializeField] private int maxCooldownLevel = 20;
    #endregion

    #region Private Fields
    private int _countLevel, _speedLevel, _damageLevel, _cooldownLevel;
    private int _inFlight;
    private float _nextThrowTime;
    private readonly Collider2D[] _hitBuffer = new Collider2D[32];
    private readonly List<Transform> _activeTargets = new List<Transform>(); // su an havadaki bumeranglarin hedefleri (farkli dusman onceligi)
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
        Throw(dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right, target);
        _nextThrowTime = Time.time + throwStagger;
    }
    #endregion

    #region Overrides
    public override void ApplyTrack(string key, int times)
    {
        if (times <= 0) return;
        switch (key)
        {
            case "count":    _countLevel    += times; break;
            case "speed":    _speedLevel    = Mathf.Min(maxSpeedLevel,    _speedLevel + times);    break;
            case "damage":   _damageLevel   = Mathf.Min(maxDamageLevel,   _damageLevel + times);   break;
            case "cooldown": _cooldownLevel = Mathf.Min(maxCooldownLevel, _cooldownLevel + times); break;
        }
    }

    public override void CollectUpgrades(List<WeaponUpgradeOption> into)
    {
        // LIMITSIZ + LUCKY
        into.Add(new WeaponUpgradeOption("Boomerang +1", "One more boomerang", _countLevel + 1, () => _countLevel++, true));
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
    // Bileşik hasar: her level (hasar + damagePerLevel) * damageMult -> sabit + carpan (Sharp Claws hissi).
    private float CurrentDamage()
    {
        if (_damageLevel <= 0) return baseDamage;
        if (damageMult <= 1.0001f) return baseDamage + damagePerLevel * _damageLevel; // carpan yoksa additive
        float mp = Mathf.Pow(damageMult, _damageLevel);
        return baseDamage * mp + damagePerLevel * damageMult * (mp - 1f) / (damageMult - 1f);
    }
    private float CurrentRestCooldown() => Mathf.Max(minRestCooldown, baseRestCooldown * Mathf.Pow(restCooldownMultiplier, _cooldownLevel));

    /// <summary>
    /// En yakin dusmani dondurur ama FARKLI dusman oncelikli: su an havadaki bumeranglarin gittigi
    /// hedefleri (_activeTargets) ELER — yani ikinci bumerangi baska (bir sonraki en yakin) dusmana atar.
    /// Baska dusman kalmadiysa (hepsi hedeflenmis) genel en yakina duser (atisi bosa harcamaz).
    /// </summary>
    private Transform FindNearestEnemy()
    {
        _activeTargets.RemoveAll(t => t == null); // olmus hedefleri temizle

        int count = Physics2D.OverlapCircleNonAlloc(transform.position, detectRange, _hitBuffer, enemyLayers);
        Transform closest = null, closestFree = null;
        float bestSqr = float.MaxValue, bestFreeSqr = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider2D c = _hitBuffer[i];
            if (c == null) continue;
            Transform t = c.transform;
            float d = ((Vector2)t.position - (Vector2)transform.position).sqrMagnitude;

            if (d < bestSqr) { bestSqr = d; closest = t; }                          // genel en yakin
            if (!_activeTargets.Contains(t) && d < bestFreeSqr) { bestFreeSqr = d; closestFree = t; } // hedeflenmemis en yakin
        }
        return closestFree != null ? closestFree : closest; // once FARKLI dusman, yoksa en yakin
    }

    private void Throw(Vector2 dir, Transform target)
    {
        PlayerBoomerang b = Instantiate(boomerangPrefab, transform.position, Quaternion.identity);
        _inFlight++;
        if (target != null) _activeTargets.Add(target); // bu hedef artik "hedeflenmis" (sonraki bumerang baskasina)
        b.Launch(transform, dir, outDistance, CurrentSpeed(), CurrentDamage(), spin, catchDistance, () => OnBoomerangReturned(target));
        SfxManager.Play(SfxId.BoomerangThrow); // klip atanmazsa sessiz
    }

    private void OnBoomerangReturned(Transform target)
    {
        _inFlight = Mathf.Max(0, _inFlight - 1);
        _activeTargets.Remove(target); // hedef serbest — tekrar hedeflenebilir
        _nextThrowTime = Mathf.Max(_nextThrowTime, Time.time + CurrentRestCooldown()); // donunce kisa dinlen
    }
    #endregion
}

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
    [SerializeField] private int maxFireRateLevel = 20;

    [Header("Hasar Track'i")]
    [SerializeField] private float damagePerLevel = 16f;
    [Tooltip("Hasar bilesik carpani (her level x bu). 1 = duz additive.")]
    [SerializeField] private float damageMult = 1.14f;
    [SerializeField] private int maxDamageLevel = 20;
    #endregion

    #region Private Fields
    private int _fireRateLevel;
    private int _damageLevel;
    private int _targetLevel;   // 0 = 1 hedef; her seviye +1 farkli dusman (LIMITSIZ)
    private float _nextFireTime;
    private readonly Collider2D[] _hitBuffer = new Collider2D[32];
    #endregion

    #region Unity Callbacks
    private void Update()
    {
        if (bulletPrefab == null || Time.time < _nextFireTime) return;

        // En yakin N FARKLI dusmana ayni anda mermi at (target sayisi upgrade'i). Kimse yoksa bekle.
        int fired = FireVolley(CurrentTargets());
        if (fired > 0)
            _nextFireTime = Time.time + CurrentFireInterval();
    }
    #endregion

    #region Overrides
    public override int GetTrackLevel(string key) => key switch { "firerate" => _fireRateLevel, "damage" => _damageLevel, "targets" => _targetLevel, _ => 0 };

    public override void ApplyTrack(string key, int times)
    {
        if (times <= 0) return;
        switch (key)
        {
            case "firerate": _fireRateLevel = Mathf.Min(maxFireRateLevel, _fireRateLevel + times); break;
            case "damage":   _damageLevel   = Mathf.Min(maxDamageLevel,   _damageLevel + times);   break;
            case "targets":  _targetLevel  += times; break;
        }
    }

    public override void CollectUpgrades(List<WeaponUpgradeOption> into)
    {
        if (_fireRateLevel < maxFireRateLevel)
            into.Add(new WeaponUpgradeOption("Blaster Fire Rate", "+Attack speed", _fireRateLevel + 1,
                () => _fireRateLevel++));

        if (_damageLevel < maxDamageLevel)
            into.Add(new WeaponUpgradeOption("Blaster Damage", "+" + damagePerLevel + " damage", _damageLevel + 1,
                () => _damageLevel++));

        // Hedef sayisi track'i — LIMITSIZ + LUCKY (ayni anda daha cok FARKLI dusmana ates)
        into.Add(new WeaponUpgradeOption("Blaster Targets", "+1 enemy targeted", _targetLevel + 1,
            () => _targetLevel++, true));
    }
    #endregion

    #region Private Methods
    private float CurrentFireInterval() => baseFireInterval * Mathf.Pow(fireIntervalMultiplier, _fireRateLevel) * RunStats.CooldownMult;
    // Bileşik hasar: her level (hasar + damagePerLevel) * damageMult -> sabit + carpan (Sharp Claws hissi).
    private float CurrentDamage()
    {
        float d;
        if (_damageLevel <= 0) d = baseDamage;
        else if (damageMult <= 1.0001f) d = baseDamage + damagePerLevel * _damageLevel;
        else { float mp = Mathf.Pow(damageMult, _damageLevel); d = baseDamage * mp + damagePerLevel * damageMult * (mp - 1f) / (damageMult - 1f); }
        return d; // hasar artik silah-basina (Charge/Firepower/Impact -> damage track)
    }
    private int CurrentTargets() => 1 + _targetLevel + RunStats.AmountBonus;

    /// <summary>En yakin 'targets' FARKLI dusmana birer mermi atar. Atilan mermi sayisini dondurur (0 = dusman yok).</summary>
    private int FireVolley(int targets)
    {
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, range * RunStats.AreaMult, _hitBuffer, enemyLayers); // Area tum silahlara etki eder
        int fired = 0;

        for (int t = 0; t < targets; t++)
        {
            // Kalan (secilmemis) dusmanlar arasindan en yakini bul
            int nearestIdx = -1;
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Collider2D c = _hitBuffer[i];
                if (c == null) continue; // secilmis (null'landi) ya da bos
                float d = ((Vector2)c.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (d < best) { best = d; nearestIdx = i; }
            }
            if (nearestIdx < 0) break; // baska dusman yok

            FireAt(_hitBuffer[nearestIdx].transform.position);
            _hitBuffer[nearestIdx] = null; // bu dusmani sonraki secimden cikar (ayni dusmana 2 mermi atma)
            fired++;
        }
        return fired;
    }

    private void FireAt(Vector3 targetPos)
    {
        Vector2 dir = (Vector2)targetPos - (Vector2)transform.position;
        // Juice: atis hizi arttikca cikis noktasi titrer (blaster titreme hissi)
        Vector3 spawnPos = transform.position + (Vector3)(Random.insideUnitCircle * BlasterJitter());
        // Havuzdan cek (Instantiate yerine) — hizli ateste GC sicramasini onler
        GameObject go = PoolManager.Spawn(bulletPrefab.gameObject, spawnPos, Quaternion.identity);
        PlayerProjectile bullet = go.GetComponent<PlayerProjectile>();
        if (bullet != null) { bullet.Launch(dir, bulletSpeed * RunStats.ProjectileSpeedMult, CurrentDamage(), bulletLifetime); bullet.SetPower(BlasterVisualScale(), BlasterTintT()); }
        SfxManager.Play(SfxId.BlasterFire); // klip atanmazsa sessiz
    }

    // Juice: hasar (level + global Might) arttikca mermi buyur (cap 3x).
    private float BlasterVisualScale() => Mathf.Min(3.6f, 1f + 0.22f * _damageLevel);  // prefab olcegi=Lv0; her level +%22 (cap 3.6x)
    // Juice: atis hizi seviyesiyle artan kucuk cikis-noktasi sarsintisi (titreme).
    private float BlasterJitter() => Mathf.Min(0.28f, _fireRateLevel * 0.02f);
    // Juice: hasar arttikca mermi isinir (beyaz->turuncu).
    private float BlasterTintT() => Mathf.Clamp01(0.09f * _damageLevel);
    #endregion
}

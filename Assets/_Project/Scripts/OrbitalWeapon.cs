using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Silah 3 — Orbital. Oyuncunun etrafinda esit aralikli donen orb'lar; dokundugu dusmana surekli hasar.
/// Ayri yukseltmeler: SAYI (1 -> max 3), DONME HIZI, HASAR, RADIUS. Orb'lar dunya uzayinda ayri objeler.
/// </summary>
public class OrbitalWeapon : WeaponBase
{
    #region Serialized Fields
    [Header("Orbital")]
    [SerializeField] private OrbitalOrb orbPrefab;
    [Tooltip("Ayni dusmana iki vurus arasi minimum sure.")]
    [SerializeField] private float hitCooldown = 0.4f;

    [Header("Baslangic (Lv.1) Degerleri")]
    [SerializeField] private float baseRadius = 2f;
    [SerializeField] private float baseRotationSpeed = 120f; // derece/sn
    [SerializeField] private float baseDamage = 8f;

    [Header("Sayi Track'i (1 -> max 3)")]
    [Tooltip("Kac kez +1 alinabilir (2 = max 3 orb: 1+2).")]
    [SerializeField] private int maxCountLevel = 2;

    [Header("Donme Hizi Track'i")]
    [SerializeField] private float rotationSpeedPerLevel = 30f;
    [SerializeField] private int maxRotationLevel = 5;

    [Header("Hasar Track'i")]
    [SerializeField] private float damagePerLevel = 18f;
    [SerializeField] private int maxDamageLevel = 8;

    [Header("Radius Track'i")]
    [SerializeField] private float radiusPerLevel = 0.35f;
    [SerializeField] private int maxRadiusLevel = 5;
    #endregion

    #region Private Fields
    private int _countLevel, _rotationLevel, _damageLevel, _radiusLevel;
    private OrbitalOrb[] _orbs;
    private float _angle;
    #endregion

    #region Unity Callbacks
    private void Update()
    {
        if (_orbs == null) return;

        _angle += CurrentRotationSpeed() * Time.deltaTime;
        float r = CurrentRadius();
        Vector2 center = transform.position;
        float step = 360f / Mathf.Max(1, _orbs.Length);

        for (int i = 0; i < _orbs.Length; i++)
        {
            if (_orbs[i] == null) continue;
            float a = (_angle + i * step) * Mathf.Deg2Rad;
            _orbs[i].transform.position = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            // Orb'lar SADECE oyuncunun etrafinda doner; kendi ekseninde donme KALDIRILDI.
        }
    }

    private void OnDestroy() => DestroyOrbs();
    #endregion

    #region Overrides
    protected override void OnAcquired()
    {
        EnsureOrbs();
        ConfigureOrbs();
    }

    public override void CollectUpgrades(List<WeaponUpgradeOption> into)
    {
        // LIMITSIZ + LUCKY
        into.Add(new WeaponUpgradeOption("Orbital +1", "One more orb", _countLevel + 1,
            () => { _countLevel++; EnsureOrbs(); ConfigureOrbs(); }, true));
        if (_rotationLevel < maxRotationLevel)
            into.Add(new WeaponUpgradeOption("Orbital Speed", "Spins faster", _rotationLevel + 1,
                () => _rotationLevel++));
        if (_damageLevel < maxDamageLevel)
            into.Add(new WeaponUpgradeOption("Orbital Damage", "+" + damagePerLevel + " damage", _damageLevel + 1,
                () => { _damageLevel++; ConfigureOrbs(); }));
        if (_radiusLevel < maxRadiusLevel)
            into.Add(new WeaponUpgradeOption("Orbital Radius", "Wider orbit", _radiusLevel + 1,
                () => _radiusLevel++));
    }
    #endregion

    #region Private Methods
    private int CurrentOrbCount() => 1 + _countLevel;
    private float CurrentRadius() => baseRadius + radiusPerLevel * _radiusLevel;
    private float CurrentRotationSpeed() => baseRotationSpeed + rotationSpeedPerLevel * _rotationLevel;
    private float CurrentDamage() => baseDamage + damagePerLevel * _damageLevel;

    private void EnsureOrbs()
    {
        if (orbPrefab == null) return;
        int want = CurrentOrbCount();
        if (_orbs != null && _orbs.Length == want) return;

        DestroyOrbs();
        _orbs = new OrbitalOrb[want];
        for (int i = 0; i < want; i++)
        {
            _orbs[i] = Instantiate(orbPrefab, transform.position, Quaternion.identity);
            _orbs[i].transform.SetParent(null, true);
        }
    }

    private void ConfigureOrbs()
    {
        if (_orbs == null) return;
        foreach (var o in _orbs)
            if (o != null) o.Configure(CurrentDamage(), hitCooldown);
    }

    private void DestroyOrbs()
    {
        if (_orbs == null) return;
        foreach (var o in _orbs)
            if (o != null) Destroy(o.gameObject);
        _orbs = null;
    }
    #endregion
}

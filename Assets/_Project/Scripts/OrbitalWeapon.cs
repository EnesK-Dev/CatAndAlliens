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

    [Tooltip("Toplam orb tavani (sayi track'i + global Amount dahil). Amount limitsiz oldugu icin\n" +
             "orbital'in ekrani orb'la doldurup abuse edilmesini engeller. <=0 ise 6 kabul edilir (serialize tuzagi).")]
    [SerializeField] private int maxOrbs = 6;

    [Header("Donme Hizi Track'i")]
    [SerializeField] private float rotationSpeedPerLevel = 30f;
    [SerializeField] private int maxRotationLevel = 20;

    [Header("Hasar Track'i")]
    [SerializeField] private float damagePerLevel = 12f;
    [Tooltip("Hasar bilesik carpani (her level x bu). 1 = duz additive.")]
    [SerializeField] private float damageMult = 1.10f;
    [SerializeField] private int maxDamageLevel = 20;

    [Header("Radius Track'i")]
    [SerializeField] private float radiusPerLevel = 0.35f;
    [SerializeField] private int maxRadiusLevel = 20;
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
            // Juice: hiz yukseltilince orb kendi ekseninde de doner (0 -> hizla artar).
            float selfSpin = CurrentSelfSpin();
            if (selfSpin != 0f) _orbs[i].transform.Rotate(0f, 0f, selfSpin * Time.deltaTime);
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

    public override void ApplyTrack(string key, int times)
    {
        if (times <= 0) return;
        switch (key)
        {
            case "count":  _countLevel += times; EnsureOrbs(); ConfigureOrbs(); break;
            case "speed":  _rotationLevel = Mathf.Min(maxRotationLevel, _rotationLevel + times); ConfigureOrbs(); break;
            case "damage": _damageLevel = Mathf.Min(maxDamageLevel, _damageLevel + times); ConfigureOrbs(); break;
            case "radius": _radiusLevel = Mathf.Min(maxRadiusLevel, _radiusLevel + times); break;
        }
        PulseOrbs(); // her level'da gorsel pop
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
    public override void RefreshStats() { EnsureOrbs(); ConfigureOrbs(); }

    private int CurrentOrbCount() => Mathf.Min(maxOrbs > 0 ? maxOrbs : 6, 1 + _countLevel + RunStats.AmountBonus);
    private float CurrentRadius() => (baseRadius + radiusPerLevel * _radiusLevel) * RunStats.AreaMult;
    private float CurrentRotationSpeed() => baseRotationSpeed + rotationSpeedPerLevel * _rotationLevel;
    // Bileşik hasar: her level (hasar + damagePerLevel) * damageMult -> sabit + carpan (Sharp Claws hissi).
    private float CurrentDamage()
    {
        float d;
        if (_damageLevel <= 0) d = baseDamage;
        else if (damageMult <= 1.0001f) d = baseDamage + damagePerLevel * _damageLevel;
        else { float mp = Mathf.Pow(damageMult, _damageLevel); d = baseDamage * mp + damagePerLevel * damageMult * (mp - 1f) / (damageMult - 1f); }
        return d; // hasar artik silah-basina (Charge/Firepower/Impact -> damage track)
    }
    // Option 3: donme hizi arttikca vurus araligi kisalir -> ayni dusmani daha sik vurur (min 0.08).
    private float CurrentHitCooldown() => Mathf.Max(0.08f, (hitCooldown - _rotationLevel * 0.02f) * RunStats.CooldownMult);
    // Juice: hasar (level + global Might) arttikca orb gorseli buyur; sansliysa devasa (cap 3.5x).
    private float OrbVisualScale() => Mathf.Min(4.2f, 0.65f + 0.22f * _damageLevel);  // kucuk basla, level basi belirgin buyu
    // Juice: hasar arttikca orb isinir (kendi rengi -> sicak).
    private float OrbTintT() => Mathf.Clamp01(0.11f * _damageLevel);  // daha hizli isin
    // Juice: level atlayinca orb'lar zipla.
    private void PulseOrbs() { if (_orbs == null) return; foreach (var o in _orbs) if (o != null) o.Pop(); }
    // Juice: donme hizi yukseltildikce orb kendi ekseninde doner (base 0).
    private float CurrentSelfSpin() => _rotationLevel * 80f;

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
            if (o != null) o.Configure(CurrentDamage(), CurrentHitCooldown(), OrbVisualScale(), OrbTintT());
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

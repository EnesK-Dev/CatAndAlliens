using UnityEngine;

/// <summary>
/// Silah 3 — Orbital. Oyuncunun etrafinda esit aralikli donen orb'lar; dokundugu dusmana surekli
/// hasar verir. Seviye: DONME HIZI, HASAR ve RADIUS artar. Orb'lar dunya uzayinda ayri objelerdir
/// (parent degil) ki oyuncu olcegi/donusu onlari bozmasin; konumlari her kare hesaplanir.
/// </summary>
public class OrbitalWeapon : WeaponBase
{
    #region Serialized Fields
    [Header("Orbital")]
    [SerializeField] private OrbitalOrb orbPrefab;

    [Tooltip("Kac orb doner (esit acilarla dagilir).")]
    [SerializeField] private int orbCount = 2;

    [Tooltip("Ayni dusmana iki vurus arasi minimum sure.")]
    [SerializeField] private float hitCooldown = 0.4f;

    [Header("Seviye 1 Degerleri")]
    [SerializeField] private float baseRadius = 2f;
    [SerializeField] private float baseRotationSpeed = 120f; // derece/sn
    [SerializeField] private float baseDamage = 8f;

    [Header("Seviye Basi (upgrade)")]
    [SerializeField] private float radiusPerLevel = 0.35f;
    [SerializeField] private float rotationSpeedPerLevel = 30f;
    [SerializeField] private float damagePerLevel = 4f;
    #endregion

    #region Private Fields
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
        }
    }

    private void OnDestroy()
    {
        DestroyOrbs();
    }
    #endregion

    #region Overrides
    protected override void OnLevelChanged()
    {
        SpawnOrbsIfNeeded();
        ConfigureOrbs(); // hasar guncelle
    }
    #endregion

    #region Private Methods
    private float CurrentRadius() => baseRadius + radiusPerLevel * Mathf.Max(0, Level - 1);
    private float CurrentRotationSpeed() => baseRotationSpeed + rotationSpeedPerLevel * Mathf.Max(0, Level - 1);
    private float CurrentDamage() => baseDamage + damagePerLevel * Mathf.Max(0, Level - 1);

    private void SpawnOrbsIfNeeded()
    {
        if (_orbs != null || orbPrefab == null) return;

        _orbs = new OrbitalOrb[Mathf.Max(1, orbCount)];
        for (int i = 0; i < _orbs.Length; i++)
        {
            _orbs[i] = Instantiate(orbPrefab, transform.position, Quaternion.identity);
            _orbs[i].transform.SetParent(null, true); // dunya uzayinda kalsin (oyuncu scale/flip etkilemesin)
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

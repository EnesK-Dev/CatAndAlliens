using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Silah 4 — Multi-slash. Yeni saldiri olusturmaz; oyuncunun MEVCUT pence saldirisinin vurdugu YON
/// sayisini arttirir. Alinca 2 yon (ileri+geri), her yukseltmede +1 (3,4,5,6 max 6). Tek track.
/// </summary>
public class MultiSlashWeapon : WeaponBase
{
    #region Serialized Fields
    [Header("Multi-slash")]
    [Tooltip("Bos birakilirsa ayni GameObject'ten alinir.")]
    [SerializeField] private player playerRef;

    [Tooltip("Ilk alimdaki yon sayisi (ileri+geri = 2).")]
    [SerializeField] private int startDirections = 2;

    [Tooltip("Maksimum yon sayisi.")]
    [SerializeField] private int maxDirections = 6;
    #endregion

    #region Private Fields
    private int _dirLevel; // 0 = start (2 yon), her seviye +1
    #endregion

    #region Unity Callbacks
    protected override void Awake()
    {
        base.Awake();
        if (playerRef == null) playerRef = GetComponent<player>();
    }
    #endregion

    #region Overrides
    protected override void OnAcquired() => ApplyDirections();

    public override void ApplyTrack(string key, int times)
    {
        if (times <= 0) return;
        if (key == "dir") { _dirLevel += times; ApplyDirections(); }
    }

    public override void CollectUpgrades(List<WeaponUpgradeOption> into)
    {
        // LIMITSIZ (sonsuz alinabilir) + LUCKY rozeti (bariz guclu)
        into.Add(new WeaponUpgradeOption("Multi-Slash +1", "One more slash direction", _dirLevel + 1,
            () => { _dirLevel++; ApplyDirections(); }, true));
    }
    #endregion

    #region Private Methods
    private int CurrentDirections() => startDirections + _dirLevel + RunStats.AmountBonus; // clamp YOK — sonsuz

    public override void RefreshStats() { ApplyDirections(); } // Amount degisince yon sayisini yenile
    private void ApplyDirections() { if (playerRef != null) playerRef.SetAttackDirections(CurrentDirections()); }
    #endregion
}

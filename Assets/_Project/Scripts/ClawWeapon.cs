using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Temel pence silahi. Oyuncunun YERLESIK claw saldirisini (player.VampireAttackRoutine) bir SILAH olarak
/// temsil eder: kusanilinca (Acquire) melee acilir, loadout'tan cikinca kapanir. Yeni saldiri mantigi YOK —
/// sadece "kusanildi mi" bayragini player'a bildirir. Multi-Slash bu saldirinin yon sayisini arttirir;
/// claw yoksa melee de yoktur. God degil, tek sorumluluk: claw'i ac/kapa + loadout kimligi.
/// </summary>
public class ClawWeapon : WeaponBase
{
    #region Serialized Fields
    [Tooltip("Bos birakilirsa ayni GameObject'ten alinir.")]
    [SerializeField] private player playerRef;
    #endregion

    #region Unity Callbacks
    protected override void Awake()
    {
        base.Awake();
        if (playerRef == null) playerRef = GetComponent<player>();
    }
    #endregion

    #region Overrides
    /// <summary>Silah alininca player'in claw saldirisini ac.</summary>
    protected override void OnAcquired()
    {
        if (playerRef != null) playerRef.SetClawEquipped(true);
    }

    /// <summary>Claw'in ayri upgrade track'i yok (hasar meta_might / Sharp Claws tracker'indan gelir).</summary>
    public override void CollectUpgrades(List<WeaponUpgradeOption> into) { }
    #endregion
}

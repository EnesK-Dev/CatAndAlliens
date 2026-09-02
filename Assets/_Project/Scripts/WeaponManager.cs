using UnityEngine;

/// <summary>
/// Oyuncunun silahlarini toplayan hafif merkez. Kart sistemi (UpgradeSelectionUI) buradan silahlari
/// sorgular: alinmamislar "NEW WEAPON" karti, alinmislar CollectUpgrades ile AYRI yukseltme kartlari verir.
/// God manager degil — sadece silah koleksiyonu. Silahlar davranislarini kendileri yapar.
/// </summary>
public class WeaponManager : MonoBehaviour
{
    #region Private Fields
    private WeaponBase[] _weapons;
    #endregion

    #region Properties
    /// <summary>Oyuncudaki tum silahlar (alinmis + alinmamis).</summary>
    public WeaponBase[] Weapons => _weapons;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        _weapons = GetComponents<WeaponBase>();
    }
    #endregion
}

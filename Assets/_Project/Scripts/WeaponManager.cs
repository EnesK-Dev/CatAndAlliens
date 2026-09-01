using UnityEngine;

/// <summary>
/// Oyuncunun silahlarini yoneten hafif merkez. Oyuncudaki tum WeaponBase component'lerini toplar;
/// kart sistemi (FAZ 2) buradan "alinabilir/yukseltilebilir" silahlari sorgular ve secince uygular.
/// God manager degil — sadece silah koleksiyonundan sorumlu. Silahlar kendi davranislarini kendileri yapar.
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

    #region Public Methods
    /// <summary>TEST/kart: bir silahi alir (yoksa) ya da yukseltir (varsa, max degilse).</summary>
    public void AcquireOrUpgrade(WeaponBase weapon)
    {
        if (weapon == null) return;
        if (!weapon.IsAcquired) weapon.Acquire();
        else if (!weapon.IsMaxed) weapon.LevelUp();
    }
    #endregion
}

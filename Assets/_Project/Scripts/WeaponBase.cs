using UnityEngine;

/// <summary>
/// Tum silahlarin taban sinifi. Silah oyuncunun bir component'idir; ALINMADAN once enabled=false
/// (calismaz). Karttan alininca Acquire() -> Level 1 + enabled=true. Yukseltme karti LevelUp().
/// Her silah bagimsiz calisir (additive) — eski saldiriyi kapatmaz. Alt siniflar Update/coroutine
/// icinde kendi atesini yapar ve CurrentX() degerlerinde Level'i kullanir.
/// </summary>
public abstract class WeaponBase : MonoBehaviour
{
    #region Serialized Fields
    [Header("Silah Kimlik")]
    [Tooltip("Kartta gosterilecek silah adi (Ingilizce).")]
    [SerializeField] protected string weaponName = "Weapon";

    [Tooltip("Silah ikonu (kart + gorsel). Sonradan atanabilir.")]
    [SerializeField] protected Sprite weaponIcon;

    [Tooltip("Ulasilabilecek en yuksek seviye (Lv.1 = ilk alim).")]
    [SerializeField] protected int maxLevel = 5;

    [Tooltip("SADECE TEST: oyun basinda kart olmadan otomatik alinir. Kart entegrasyonu (FAZ 2) bitince KAPAT.")]
    [SerializeField] protected bool acquireOnStartForTesting = false;
    #endregion

    #region Properties
    /// <summary>Guncel seviye. 0 = henuz alinmadi.</summary>
    public int Level { get; protected set; }

    /// <summary>Silah alindi mi (Level > 0).</summary>
    public bool IsAcquired => Level > 0;

    /// <summary>Maksimum seviyeye ulasildi mi.</summary>
    public bool IsMaxed => Level >= maxLevel;

    public string WeaponName => weaponName;
    public Sprite WeaponIcon => weaponIcon;
    public int MaxLevel => maxLevel;
    #endregion

    #region Unity Callbacks
    protected virtual void Awake()
    {
        if (acquireOnStartForTesting) Acquire(); // TEST: kart olmadan basta al
        else if (Level <= 0) enabled = false;    // Alinmadan calismasin — karttan alininca aktiflesir
    }
    #endregion

    #region Public Methods
    /// <summary>Silahi ilk kez alir: Level 1 + aktif eder.</summary>
    public virtual void Acquire()
    {
        if (Level > 0) return;
        Level = 1;
        enabled = true;
        OnLevelChanged();
    }

    /// <summary>Silahi bir seviye yukseltir (max'a kadar).</summary>
    public virtual void LevelUp()
    {
        if (Level <= 0 || Level >= maxLevel) return;
        Level++;
        OnLevelChanged();
    }
    #endregion

    #region Protected Methods
    /// <summary>Seviye degisince cagrilir (stat'lari yeniden hesaplamak icin). Alt sinif override edebilir.</summary>
    protected virtual void OnLevelChanged() { }
    #endregion
}

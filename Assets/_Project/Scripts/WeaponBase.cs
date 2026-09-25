using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tum silahlarin taban sinifi. Silah oyuncunun bir component'idir; ALINMADAN once enabled=false
/// (calismaz). Karttan alininca Acquire() -> aktif. Yukseltmeler TEK bir seviye degil; her silahin
/// BIRDEN COK ayri track'i vardir (hasar/hiz/sayi vb.), her biri ayri kart olarak CollectUpgrades ile
/// sunulur. Silah bagimsiz calisir (additive) — eski saldiriyi kapatmaz.
/// </summary>
public abstract class WeaponBase : MonoBehaviour
{
    #region Serialized Fields
    [Header("Silah Kimlik")]
    [Tooltip("Kartta gosterilecek silah adi (Ingilizce).")]
    [SerializeField] protected string weaponName = "Weapon";

    [Tooltip("Silah ikonu (kart + gorsel). Sonradan atanabilir.")]
    [SerializeField] protected Sprite weaponIcon;

    [Tooltip("SADECE TEST: oyun basinda kart olmadan otomatik alinir. Yayinda KAPAT.")]
    [SerializeField] protected bool acquireOnStartForTesting = false;
    #endregion

    #region Properties
    /// <summary>Silah alindi mi.</summary>
    public bool IsAcquired { get; private set; }

    public string WeaponName => weaponName;
    public Sprite WeaponIcon => weaponIcon;
    #endregion

    #region Unity Callbacks
    protected virtual void Awake()
    {
        if (acquireOnStartForTesting) Acquire(); // TEST
        else enabled = false;                    // alinmadan calismasin
    }
    #endregion

    #region Public Methods
    /// <summary>Silahi ilk kez alir: aktif eder ve baslangic (Lv.1) durumunu uygular.</summary>
    /// <summary>Deck: bir upgrade track'ini 'times' kez arttirir (stack). Alt siniflar override eder.</summary>
    public virtual void ApplyTrack(string key, int times) { }

    /// <summary>In-run global stat degisince (Amount/Area vb.) cache'li degerleri yeniden uygular.
    /// Live okuyan silahlar (blaster/boomerang) override gerektirmez; orbital/multislash override eder.</summary>
    public virtual void RefreshStats() { }

    public void Acquire()
    {
        if (IsAcquired) return;
        IsAcquired = true;
        enabled = true;
        OnAcquired();
    }

    /// <summary>Silahin su an ALINABILIR (max olmayan) yukseltme seceneklerini listeye ekler (ayri kartlar).</summary>
    public abstract void CollectUpgrades(List<WeaponUpgradeOption> into);
    #endregion

    #region Protected Methods
    /// <summary>Silah ilk alininca cagrilir (baslangic statlarini uygulamak icin). Alt sinif override edebilir.</summary>
    protected virtual void OnAcquired() { }
    #endregion
}

using System;

/// <summary>
/// Bir silahin TEK bir yukseltme secenegi (VS tarzi ayri kart). Kartta baslik/aciklama/seviye gosterilir;
/// secilince apply() cagrilir (ilgili track'i bir arttirir). Silahlar CollectUpgrades ile bunlari uretir.
/// </summary>
public struct WeaponUpgradeOption
{
    public string title;       // orn. "Boomerang Damage"
    public string description; // orn. "+6 hasar"
    public int nextLevel;      // secilince ulasilacak track seviyesi (Lv.N)
    public Action apply;       // track'i arttiran islem
    public bool lucky;         // "LUCKY" rozeti — bariz daha guclu upgrade (sayi/yon/hedef)

    public WeaponUpgradeOption(string title, string description, int nextLevel, Action apply, bool lucky = false)
    {
        this.title = title;
        this.description = description;
        this.nextLevel = nextLevel;
        this.apply = apply;
        this.lucky = lucky;
    }
}

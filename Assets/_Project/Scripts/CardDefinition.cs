using System;
using UnityEngine;

/// <summary>Kartin ait oldugu kategori — uygulanma ve shop davranisini belirler.</summary>
public enum CardCategory
{
    Stat,          // player stat'i (hasar/hiz/menzil/can/dash/food)
    Weapon,        // bir silahi ALIR (run'da o silah aktif olur)
    WeaponUpgrade  // belirli bir silahin bir track'ini yukseltir
}

/// <summary>Stat kartlarinin etkiledigi stat turu. amount'in ANLAMI buna gore degisir (bkz. CardDefinition.amount).</summary>
public enum CardStatType
{
    Damage,          // + hasar (additive)
    AttackSpeed,     // cooldown CARPANI (0.92 = %8 hizli; stack multiplicative)
    AttackRange,     // + menzil (additive)
    MaxHealth,       // + maksimum can (additive)
    DashCooldown,    // - dash cooldown saniye (additive, negatif etki = azaltir)
    FoodDropChance,  // + food drop sansi (additive, 0-1)
    UltimateDamage,  // + ulti (boss) hasari (additive) — RunStats.UltimateDamageBonus'a eklenir
    // ---- Oyun ici statlarin META (deck) karsiliklari (guclu baslangic) ----
    HasteGlobal,     // RunStats.CooldownMult *= amount (tum atis/vurus hizi)
    AreaGlobal,      // RunStats.AreaMult += amount (menzil/boyut)
    AmountGlobal,    // RunStats.AmountBonus += amount (+mermi/orb/hedef)
    OrbitalDamage,   // orbital silahi ApplyTrack("damage", amount)
    BlasterDamage,   // blaster  silahi ApplyTrack("damage", amount)
    BoomerangDamage, // boomerang silahi ApplyTrack("damage", amount)
    CurseSpawn       // RunStats.CurseMult += amount (dusman spawn hizi + alive-cap ARTAR; risk stati)
}

/// <summary>
/// TEK bir kartin tanimi (deck birimi). Blueprint modeli: bir kez alinir, slotlara dizilir; slotta stack'lenir.
/// Katalog (CardDatabase) tarafindan tutulur; MetaSave sadece id'leri saklar. Efekt FAZ 4'te uygulanir.
/// </summary>
[Serializable]
public class CardDefinition
{
    [Tooltip("Benzersiz kimlik — MetaSave bunu saklar. Degistirme (kayit kirilir).")]
    public string id = "";

    [Tooltip("Kart uzerinde gorunecek ad (Ingilizce).")]
    public string displayName = "Card";

    [TextArea]
    [Tooltip("Kisa aciklama.")]
    public string description = "";

    [Tooltip("Kart ikonu (stat/silah gorseli).")]
    public Sprite icon;

    [Tooltip("Kart cerceve sprite'i (kategori rengi). Kart gorseli bunu arka plan yapar.")]
    public Sprite frameSprite;

    [Tooltip("Cerceve Image renk tint'i. Beyaz = sprite'in kendi rengi. Silahlar gri sprite + ozel tint kullanir.")]
    public Color frameTint = Color.white;

    [Tooltip("Kartin kategorisi.")]
    public CardCategory category = CardCategory.Stat;

    [Tooltip("Shop'ta bu karti almanin core maliyeti (blueprint: tek seferlik).")]
    public int cost = 20;

    [Tooltip("Bir SLOTA en fazla kac kez konabilir. 0 = slot limiti (20). Silah karti icin 1 onerilir.")]
    public int maxPerSlot = 0;

    [Header("Stat karti (category = Stat)")]
    [Tooltip("Hangi stat'i etkiler.")]
    public CardStatType statType = CardStatType.Damage;

    [Tooltip("Etki miktari. Damage/Range/MaxHealth/FoodDrop: additive. AttackSpeed: cooldown carpani (0.92). " +
             "DashCooldown: saniye (negatif verilirse azaltir).")]
    public float amount = 1f;

    [Tooltip("Damage karti icin EK carpan (her kart: (hasar+amount)*amountMult). 1 = carpan yok.")]
    public float amountMult = 1f;

    [Header("Silah karti (category = Weapon / WeaponUpgrade)")]
    [Tooltip("Hedef silahin id'si (WeaponManager FAZ 5'te eslestirir).")]
    public string weaponId = "";

    [Tooltip("WeaponUpgrade icin: silahin hangi track'i (FAZ 5). Weapon-acquire'da bos.")]
    public string upgradeKey = "";
}

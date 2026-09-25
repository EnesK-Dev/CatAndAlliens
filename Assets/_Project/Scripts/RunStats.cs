using UnityEngine;

/// <summary>
/// Tek bir RUN boyunca gecerli, deck kartlarindan gelen KESISEN (cross-cutting) modifiyeler.
/// Ornek: food drop sansi global bonusu — dusmanlar kendi sanslarina bunu ekler. Statik, run basinda
/// DeckApplier tarafindan sifirlanip doldurulur. God manager degil: sadece run-modifier tasiyicisi.
/// </summary>
public static class RunStats
{
    /// <summary>Deck'ten gelen food drop sansi bonusu (0-1, additive).</summary>
    public static float FoodDropChanceBonus;

    /// <summary>Tum dusmanlar icin SABIT food drop tabani.</summary>
    public const float FoodBaseChance = 0.10f;
    /// <summary>Food drop sansi tavani.</summary>
    public const float MaxFoodDropChance = 0.5f;
    /// <summary>Efektif food drop sansi = min(tavan, taban + kart bonusu). Tum dusmanlar ayni.</summary>
    public static float EffectiveFoodDropChance => Mathf.Min(MaxFoodDropChance, FoodBaseChance + FoodDropChanceBonus);

    /// <summary>Deck'ten gelen ULTI hasar bonusu (additive). UltimateCinematic boss ultimate hasarina ekler.</summary>
    public static float UltimateDamageBonus;

    // ==== IN-RUN GLOBAL STATLAR ====
    // Oyun ici kart secimleri bunlari arttirir; HEM player (Sharp Claws) HEM tum silahlar okur.
    // Boylece "silah upgrade" ayri bir sey degil: statlar tum silahlarin hasar/hiz/sayi/alanini buyutur.
    /// <summary>Tum hasar carpani (Might). 1 = degisiklik yok.</summary>
    public static float DamageMult = 1f;
    /// <summary>Tum saldiri/atis bekleme carpani (Haste). Kucuk = hizli. 1 = degisiklik yok.</summary>
    public static float CooldownMult = 1f;
    /// <summary>Mermi/orb/yon/hedef sayisi bonusu (Amount). 0 = degisiklik yok.</summary>
    public static int AmountBonus = 0;
    /// <summary>Menzil/yaricap carpani (Area). 1 = degisiklik yok.</summary>
    public static float AreaMult = 1f;
    /// <summary>Mermi hizi carpani. 1 = degisiklik yok.</summary>
    public static float ProjectileSpeedMult = 1f;

    /// <summary>Haste'in inebilecegi taban cooldown carpani (sonsuz hizlanmayi onler).</summary>
    public const float MinCooldownMult = 0.35f;

    /// <summary>Adaptif dusman cani carpani (DifficultyManager, oyuncu hizli kesiyorsa 1 -> ~1.3 arasi). Dusmanlar spawn'da uygular.</summary>
    public static float EnemyHealthMult = 1f;

    /// <summary>Run basinda tum run-modifiyelerini sifirlar (DeckApplier cagirir).</summary>
    public static void Reset()
    {
        FoodDropChanceBonus = 0f;
        UltimateDamageBonus = 0f;
        DamageMult = 1f;
        CooldownMult = 1f;
        AmountBonus = 0;
        AreaMult = 1f;
        ProjectileSpeedMult = 1f;
        EnemyHealthMult = 1f;
    }
}

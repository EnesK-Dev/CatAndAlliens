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
    /// <summary>Adaptif food drop carpani (kill-rate yuksekse DUSER; hizli kesen oyuncunun asiri can/ulti toplamasini onler). 1 = normal.</summary>
    public static float FoodDropRateMult = 1f;

    /// <summary>Efektif food drop sansi = min(tavan, (taban + kart bonusu) * adaptif carpan). Tum dusmanlar ayni.</summary>
    public static float EffectiveFoodDropChance => Mathf.Min(MaxFoodDropChance, (FoodBaseChance + FoodDropChanceBonus) * FoodDropRateMult);

    /// <summary>Deck'ten gelen ULTI hasar bonusu (additive). UltimateCinematic boss ultimate hasarina ekler.</summary>
    public static float UltimateDamageBonus;

    // ==== IN-RUN GLOBAL STATLAR ====
    // Oyun ici kart secimleri bunlari arttirir; HEM player (Sharp Claws) HEM tum silahlar okur.
    // Boylece "silah upgrade" ayri bir sey degil: statlar tum silahlarin hasar/hiz/sayi/alanini buyutur.
    /// <summary>Tum hasar carpani (Might). 1 = degisiklik yok.</summary>
    public static float DamageMult = 1f;
    /// <summary>Tum saldiri/atis bekleme carpani (Haste). Kucuk = hizli. 1 = degisiklik yok.</summary>
    public static float CooldownMult = 1f;
    /// <summary>Kac Amount karti = +1 amount (META build + oyun ici ORTAK esik).</summary>
    public const int AmountCardsPerBonus = 2;
    /// <summary>Toplam alinan Amount karti (meta build + oyun ici). AmountBonus BUNDAN turer.</summary>
    public static int AmountPicks = 0;
    /// <summary>Mermi/orb/yon/hedef sayisi bonusu (Amount) = AmountPicks / AmountCardsPerBonus. 0 = degisiklik yok.</summary>
    public static int AmountBonus = 0;
    /// <summary>AmountPicks degisince AmountBonus'u yeniden hesaplar (2 kart = +1). Meta ve in-run ayni yolu kullanir.</summary>
    public static void RecomputeAmountBonus() { AmountBonus = AmountPicks / AmountCardsPerBonus; }

    /// <summary>Meta (build) deck'ten gelen stat kart SAYILARI, UpgradeType index'iyle (0..8). Tracker in-run level'i bundan (esik replay) baslatir.</summary>
    public static readonly int[] MetaStatPicks = new int[9];

    /// <summary>Esik replay: 'picks' kart, firstCost'tan baslayip her level +1 artan esikle kac level/ilerleme/sonraki-maliyet verir.</summary>
    public static void ThresholdReplay(int picks, int firstCost, out int level, out int progress, out int cost)
    {
        level = 0; progress = 0; cost = Mathf.Max(1, firstCost);
        for (int i = 0; i < picks; i++) { progress++; if (progress >= cost) { progress -= cost; level++; cost++; } }
    }
    /// <summary>Esik replay'in sadece level sonucu.</summary>
    public static int ThresholdLevels(int picks, int firstCost) { ThresholdReplay(picks, firstCost, out int l, out _, out _); return l; }
    /// <summary>Menzil/yaricap carpani (Area). 1 = degisiklik yok.</summary>
    public static float AreaMult = 1f;
    /// <summary>Mermi hizi carpani. 1 = degisiklik yok.</summary>
    public static float ProjectileSpeedMult = 1f;

    /// <summary>Haste'in inebilecegi taban cooldown carpani (sonsuz hizlanmayi onler).</summary>
    public const float MinCooldownMult = 0.35f;

    /// <summary>Adaptif dusman cani carpani (DifficultyManager, oyuncu hizli kesiyorsa 1 -> maxEnemyHealthMult). Dusmanlar spawn'da uygular.</summary>
    public static float EnemyHealthMult = 1f;

    /// <summary>Adaptif dusman HIZ carpani (ayni kill-hizi verisi, 1 -> maxEnemySpeedMult). Dusmanlar spawn'da uygular.</summary>
    public static float EnemySpeedMult = 1f;

    /// <summary>Curse (risk stati) carpani. 1 = normal. Buyudukce dusman SPAWN HIZI + alive-cap artar. Her Curse karti += amount.</summary>
    public static float CurseMult = 1f;

    /// <summary>Combo'yu dolduran AKTIF silahin anahtari (weaponId: claw/orbital/blaster/boomerang).
    /// BuildUI'da kedinin kafasindaki ORTA (turuncu COMBO) kutudaki silah. DeckApplier set eder. Bos = combo dolmaz.</summary>
    public static string ComboWeaponKey = "";

    /// <summary>Run basinda tum run-modifiyelerini sifirlar (DeckApplier cagirir).</summary>
    public static void Reset()
    {
        FoodDropChanceBonus = 0f;
        UltimateDamageBonus = 0f;
        DamageMult = 1f;
        CooldownMult = 1f;
        AmountBonus = 0;
        AmountPicks = 0;
        for (int i = 0; i < MetaStatPicks.Length; i++) MetaStatPicks[i] = 0;
        AreaMult = 1f;
        ProjectileSpeedMult = 1f;
        EnemyHealthMult = 1f;
        EnemySpeedMult = 1f;
        FoodDropRateMult = 1f;
        CurseMult = 1f;
        ComboWeaponKey = "";
    }
}

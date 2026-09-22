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

    /// <summary>Run basinda tum run-modifiyelerini sifirlar (DeckApplier cagirir).</summary>
    public static void Reset()
    {
        FoodDropChanceBonus = 0f;
        UltimateDamageBonus = 0f;
    }
}

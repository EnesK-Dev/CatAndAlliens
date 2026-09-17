using UnityEngine;

/// <summary>
/// Tek bir RUN boyunca gecerli, deck kartlarindan gelen KESISEN (cross-cutting) modifiyeler.
/// Ornek: food drop sansi global bonusu — dusmanlar kendi sanslarina bunu ekler. Statik, run basinda
/// DeckApplier tarafindan sifirlanip doldurulur. God manager degil: sadece run-modifier tasiyicisi.
/// </summary>
public static class RunStats
{
    /// <summary>Deck'ten gelen food drop sansi bonusu (0-1, additive). Dusmanlar ultFoodDropChance'a ekler.</summary>
    public static float FoodDropChanceBonus;

    /// <summary>Run basinda tum run-modifiyelerini sifirlar (DeckApplier cagirir).</summary>
    public static void Reset()
    {
        FoodDropChanceBonus = 0f;
    }
}

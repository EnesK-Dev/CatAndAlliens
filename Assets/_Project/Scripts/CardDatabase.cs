using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TUM kartlarin tek merkezi listesi (ScriptableObject — tek asset, Inspector'dan duzenlenir).
/// Resources altinda durur; CardCatalog bunu yukleyip id ile arama saglar. Denge burada tutulur.
/// </summary>
[CreateAssetMenu(fileName = "CardDatabase", menuName = "Meowvivors/Card Database")]
public class CardDatabase : ScriptableObject
{
    [Tooltip("Oyundaki tum kartlar. id'ler BENZERSIZ olmali.")]
    public List<CardDefinition> cards = new List<CardDefinition>();

    /// <summary>id ile kart bulur; yoksa null.</summary>
    public CardDefinition GetById(string id)
    {
        if (string.IsNullOrEmpty(id) || cards == null) return null;
        for (int i = 0; i < cards.Count; i++)
            if (cards[i] != null && cards[i].id == id) return cards[i];
        return null;
    }
}

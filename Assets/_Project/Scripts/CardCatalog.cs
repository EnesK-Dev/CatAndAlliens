using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kart kataloguna STATIK erisim (MetaSave pattern'i). CardDatabase asset'ini Resources'tan bir kez
/// yukler ve id ile kart aramayi saglar. Gameplay/UI her yerden CardCatalog.Get(id) ile kullanir.
/// </summary>
public static class CardCatalog
{
    private const string ResourcePath = "CardDatabase"; // Assets/.../Resources/CardDatabase.asset
    private static CardDatabase _db;

    /// <summary>Katalog asset'i (lazy load). Bulunamazsa null (uyarir).</summary>
    public static CardDatabase Database
    {
        get
        {
            if (_db == null)
            {
                _db = Resources.Load<CardDatabase>(ResourcePath);
                if (_db == null)
                    Debug.LogWarning("CardCatalog: Resources/" + ResourcePath + " bulunamadi — kart sistemi bos calisir.");
            }
            return _db;
        }
    }

    /// <summary>id ile kart tanimi; yoksa null.</summary>
    public static CardDefinition Get(string id) => Database != null ? Database.GetById(id) : null;

    /// <summary>Bu id gecerli bir kart mi?</summary>
    public static bool Exists(string id) => Get(id) != null;

    /// <summary>Tum kartlar (salt-okunur); katalog yoksa bos liste.</summary>
    public static IReadOnlyList<CardDefinition> All =>
        Database != null && Database.cards != null ? Database.cards : new List<CardDefinition>();
}

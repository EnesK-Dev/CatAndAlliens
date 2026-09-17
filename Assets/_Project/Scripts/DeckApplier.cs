using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Run BASINDA aktif build slotunun (MetaSave) kartlarini oyuncuya uygular: stat kartlari player'a,
/// food gibi kesisen etkiler RunStats'a. Blueprint modeli: ayni kart birden cok kez olabilir (stack).
/// Silah kartlari FAZ 5'te baglanacak. God manager degil: tek sorumluluk = deck'i runa yansitmak.
/// </summary>
public class DeckApplier : MonoBehaviour
{
    #region Serialized Fields
    [Tooltip("Bos birakilirsa Start'ta otomatik bulunur.")]
    [SerializeField] private player playerRef;
    [Tooltip("Uygulanan deck ozetini Console'a yaz (test icin).")]
    [SerializeField] private bool logApplied = true;
    #endregion

    #region Unity Callbacks
    private void Start()
    {
        if (playerRef == null) playerRef = FindFirstObjectByType<player>();
        ApplyActiveDeck();
    }
    #endregion

    #region Public Methods
    /// <summary>Aktif slotun tum kartlarini uygular. Once RunStats sifirlanir.</summary>
    public void ApplyActiveDeck()
    {
        RunStats.Reset();
        var deck = MetaSave.GetActiveDeck();
        int applied = 0;
        for (int i = 0; i < deck.Count; i++)
        {
            var card = CardCatalog.Get(deck[i]);
            if (card == null) continue;
            ApplyCard(card);
            applied++;
        }
        if (logApplied) GameLog.Log("[DeckApplier] Aktif slot " + MetaSave.ActiveSlot + " - uygulanan kart: " + applied + "/" + deck.Count, this);
    }
    #endregion

    #region Private Methods
    private void ApplyCard(CardDefinition card)
    {
        switch (card.category)
        {
            case CardCategory.Stat: ApplyStat(card); break;
            case CardCategory.Weapon: /* FAZ 5: silah alma */ break;
            case CardCategory.WeaponUpgrade: /* FAZ 5: silah upgrade */ break;
        }
    }

    private void ApplyStat(CardDefinition card)
    {
        switch (card.statType)
        {
            case CardStatType.Damage:         if (playerRef != null) playerRef.AddDamage(card.amount); break;
            case CardStatType.AttackSpeed:    if (playerRef != null) playerRef.ApplyAttackSpeedMultiplier(card.amount); break;
            case CardStatType.AttackRange:    if (playerRef != null) playerRef.AddAttackRange(card.amount); break;
            case CardStatType.MaxHealth:      if (playerRef != null) playerRef.AddMaxHealth(card.amount); break;
            case CardStatType.DashCooldown:   if (playerRef != null) playerRef.AddDashCooldown(card.amount); break;
            case CardStatType.FoodDropChance: RunStats.FoodDropChanceBonus += card.amount; break;
        }
    }
    #endregion

    #region Editor Test (read-only preview)
    /// <summary>Aktif deck'in TOPLAM etkisini hesaplar (player'a DOKUNMAZ) — edit-mode testi icin.</summary>
    public static string PreviewActiveDeck()
    {
        var deck = MetaSave.GetActiveDeck();
        float dmg = 0f, range = 0f, hp = 0f, dash = 0f, food = 0f, atkMul = 1f;
        int weapons = 0, unknown = 0;
        var wlist = new List<string>();
        foreach (var id in deck)
        {
            var c = CardCatalog.Get(id);
            if (c == null) { unknown++; continue; }
            if (c.category == CardCategory.Weapon) { weapons++; wlist.Add(c.weaponId); continue; }
            if (c.category != CardCategory.Stat) continue;
            switch (c.statType)
            {
                case CardStatType.Damage: dmg += c.amount; break;
                case CardStatType.AttackSpeed: atkMul *= c.amount; break;
                case CardStatType.AttackRange: range += c.amount; break;
                case CardStatType.MaxHealth: hp += c.amount; break;
                case CardStatType.DashCooldown: dash += c.amount; break;
                case CardStatType.FoodDropChance: food += c.amount; break;
            }
        }
        var sb = new StringBuilder();
        sb.AppendLine("Deck (slot " + MetaSave.ActiveSlot + ") kart=" + deck.Count + " bilinmeyen=" + unknown);
        sb.AppendLine("  +dmg=" + dmg + "  atkCooldownMul=" + atkMul.ToString("0.###") + "  +range=" + range + "  +maxHP=" + hp + "  dashMod=" + dash + "  +food=" + food);
        sb.AppendLine("  silah karti=" + weapons + " [" + string.Join(",", wlist) + "] (FAZ5'te baglanacak)");
        return sb.ToString();
    }
    #endregion
}

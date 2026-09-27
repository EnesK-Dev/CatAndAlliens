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

    #region Private Fields
    private WeaponManager _weapons;
    private readonly System.Collections.Generic.Dictionary<string,int> _upgradeAccum = new System.Collections.Generic.Dictionary<string,int>();
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
        if (_weapons == null && playerRef != null) _weapons = playerRef.GetComponent<WeaponManager>();
        _upgradeAccum.Clear();
        var deck = MetaSave.GetActiveDeck();
        int applied = 0;
        int multislashCopies = 0; // multislash silah karti stack'lendikce yon ekler
        for (int i = 0; i < deck.Count; i++)
        {
            var card = CardCatalog.Get(deck[i]);
            if (card == null) continue;
            // Yeni model: run BASINDA sadece SILAH alinir (loadout). Stat/upgrade kartlari yok sayilir —
            // stat gelisimi artik OYUN ICINDE (UpgradeSelectionUI + RunStats globals). Eski kayitlardaki
            // stat/upgrade kartlari sessizce atlanir.
            switch (card.category)
            {
                case CardCategory.Stat: ApplyStat(card); break; // deck stat kartlari run basinda uygulanir
                case CardCategory.Weapon: break; // silahlar weaponSlots'tan alinir; cards'taki silah kartlari yok sayilir
                default: break; // WeaponUpgrade -> yok say
            }
            applied++;
        }
        // Silahlar alindiktan SONRA upgrade track'lerini toplu (stack) uygula
        foreach (var kv in _upgradeAccum)
        {
            int sep = kv.Key.IndexOf('|');
            if (sep < 0) continue;
            var w = FindWeapon(kv.Key.Substring(0, sep));
            if (w != null) w.ApplyTrack(kv.Key.Substring(sep + 1), kv.Value);
        }
        // Multi-Slash: her ekstra kopya +1 yon (1 kopya = temel 2 yon).
        if (multislashCopies > 1)
        {
            var ms = FindWeapon("multislash");
            if (ms != null) ms.ApplyTrack("dir", multislashCopies - 1);
        }

        // Silahlar SABIT slotlardan alinir (BuildUI ile ayni). Combo silahi = orta slot (index 1); bos ise ilk dolu.
        RunStats.ComboWeaponKey = "";
        var wslots = MetaSave.GetWeaponSlots(MetaSave.ActiveSlot);
        for (int i = 0; i < wslots.Length; i++)
        {
            if (string.IsNullOrEmpty(wslots[i])) continue;
            var wc = CardCatalog.Get(wslots[i]);
            if (wc != null && wc.category == CardCategory.Weapon) AcquireWeapon(wc.weaponId);
        }
        string comboId = wslots.Length > 1 ? wslots[1] : "";
        if (string.IsNullOrEmpty(comboId))
            for (int i = 0; i < wslots.Length; i++) if (!string.IsNullOrEmpty(wslots[i])) { comboId = wslots[i]; break; }
        if (!string.IsNullOrEmpty(comboId))
        {
            var cc = CardCatalog.Get(comboId);
            if (cc != null) RunStats.ComboWeaponKey = (cc.weaponId == "multislash") ? "claw" : cc.weaponId;
        }

        if (logApplied) GameLog.Log("[DeckApplier] Aktif slot " + MetaSave.ActiveSlot + " - uygulanan kart: " + applied + "/" + deck.Count, this);
    }
    #endregion

    #region Private Methods

    private void ApplyStat(CardDefinition card)
    {
        switch (card.statType)
        {
            case CardStatType.Damage:         if (playerRef != null) { playerRef.AddDamage(card.amount); playerRef.MultiplyDamage(card.amountMult); } break;
            case CardStatType.AttackSpeed:    if (playerRef != null) playerRef.ApplyAttackSpeedMultiplier(card.amount); break;
            case CardStatType.AttackRange:    if (playerRef != null) playerRef.AddAttackRange(card.amount); break;
            case CardStatType.MaxHealth:      if (playerRef != null) playerRef.AddMaxHealth(card.amount); break;
            case CardStatType.DashCooldown:   if (playerRef != null) playerRef.AddDashCooldown(card.amount); break;
            case CardStatType.FoodDropChance: RunStats.FoodDropChanceBonus += card.amount; break;
            case CardStatType.UltimateDamage: RunStats.UltimateDamageBonus += card.amount; break;
            case CardStatType.HasteGlobal:    RunStats.CooldownMult = Mathf.Max(RunStats.MinCooldownMult, RunStats.CooldownMult * card.amount); break;
            case CardStatType.AreaGlobal:     RunStats.AreaMult += card.amount; break;
            case CardStatType.AmountGlobal:   RunStats.AmountBonus += Mathf.Max(1, Mathf.RoundToInt(card.amount)); break;
            case CardStatType.OrbitalDamage:  { var w = FindWeapon("orbital");  if (w != null) w.ApplyTrack("damage", Mathf.Max(1, Mathf.RoundToInt(card.amount))); } break;
            case CardStatType.BlasterDamage:  { var w = FindWeapon("blaster");   if (w != null) w.ApplyTrack("damage", Mathf.Max(1, Mathf.RoundToInt(card.amount))); } break;
            case CardStatType.BoomerangDamage:{ var w = FindWeapon("boomerang"); if (w != null) w.ApplyTrack("damage", Mathf.Max(1, Mathf.RoundToInt(card.amount))); } break;
        }
    }

    /// <summary>weaponId'ye karsilik gelen silahi (tip adiyla eslesir) alir/aktif eder. Ornek: "blaster" -> AutoBlasterWeapon.</summary>
    private void AcquireWeapon(string weaponId)
    {
        var w = FindWeapon(weaponId);
        if (w != null) w.Acquire();
    }

    /// <summary>weaponId'ye karsilik gelen silahi tip adiyla bulur (blaster -> AutoBlasterWeapon).</summary>
    private WeaponBase FindWeapon(string weaponId)
    {
        if (_weapons == null || _weapons.Weapons == null || string.IsNullOrEmpty(weaponId)) return null;
        string key = weaponId.ToLowerInvariant();
        var list = _weapons.Weapons;
        for (int i = 0; i < list.Length; i++)
            if (list[i] != null && list[i].GetType().Name.ToLowerInvariant().Contains(key)) return list[i];
        return null;
    }
    #endregion

    #region Editor Test (read-only preview)
    /// <summary>Aktif deck'in TOPLAM etkisini hesaplar (player'a DOKUNMAZ) — edit-mode testi icin.</summary>
    public static string PreviewActiveDeck()
    {
        var deck = MetaSave.GetActiveDeck();
        float dmg = 0f, range = 0f, hp = 0f, dash = 0f, food = 0f, ulti = 0f, atkMul = 1f;
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
                case CardStatType.UltimateDamage: ulti += c.amount; break;
            }
        }
        var sb = new StringBuilder();
        sb.AppendLine("Deck (slot " + MetaSave.ActiveSlot + ") kart=" + deck.Count + " bilinmeyen=" + unknown);
        sb.AppendLine("  +dmg=" + dmg + "  atkCooldownMul=" + atkMul.ToString("0.###") + "  +range=" + range + "  +maxHP=" + hp + "  dashMod=" + dash + "  +food=" + food + "  +ultiDmg=" + ulti);
        sb.AppendLine("  silah karti=" + weapons + " [" + string.Join(",", wlist) + "] (FAZ5'te baglanacak)");
        return sb.ToString();
    }
    #endregion
}

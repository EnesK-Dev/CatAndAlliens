using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Roguelite META ilerlemesinin KALICI kaydi (runlar arasi). Banka core, SAHIP OLUNAN KART ADETLERI
/// (ayni kart defalarca alinabilir -> stack) ve 3 build slotu (her biri max 20 kart) + aktif slot.
/// JsonUtility ile PlayerPrefs'e yazilir (mobil). Statik erisim, lazy load + auto-save. God manager degil.
/// </summary>
public static class MetaSave
{
    #region Constants
    public const int SlotCount = 3;
    public const int MaxCardsPerSlot = 25; // 3 silah + 20 stat
    public const int WeaponSlotCap = 3;   // kafanin cevresindeki 3 silah slotu (orta index 1 = combo)
    private const string PrefsKey = "MeowvivorsMeta_v2"; // v2: owned artik adet-tabanli
    #endregion

    #region Data Model
    [Serializable] private class SlotData { public List<string> cards = new List<string>(); public string[] weaponSlots; }
    [Serializable] private class OwnedEntry { public string id; public int count; }

    [Serializable]
    private class MetaData
    {
        public int bankedCores;
        public List<OwnedEntry> owned = new List<OwnedEntry>(); // kart id -> sahip olunan adet
        public SlotData[] slots;
        public int activeSlot;
        public bool clawGranted;      // migration: herkes wpn_claw'a sahip + loadout'lara eklendi (bir kez)
        public bool weaponsMigrated;  // migration: silahlar cards'tan sabit weaponSlots'a tasindi (bir kez)
    }
    #endregion

    #region Private Fields
    private static MetaData _data;
    #endregion

    #region Events
    /// <summary>Herhangi bir meta veri degisince firlar (UI genel yenilemesi).</summary>
    public static event Action OnChanged;
    /// <summary>Banka core degisince firlar. Parametre: yeni bakiye.</summary>
    public static event Action<int> OnCoresChanged;
    #endregion

    #region Public API — Cores
    public static int BankedCores { get { Ensure(); return _data.bankedCores; } }

    public static void AddCores(int amount)
    {
        if (amount <= 0) return;
        Ensure(); _data.bankedCores += amount; Save(); OnCoresChanged?.Invoke(_data.bankedCores);
    }

    public static bool SpendCores(int amount)
    {
        Ensure();
        if (amount < 0 || _data.bankedCores < amount) return false;
        _data.bankedCores -= amount; Save(); OnCoresChanged?.Invoke(_data.bankedCores); return true;
    }
    #endregion

    #region Public API — Owned Cards (adet)
    /// <summary>Bu karttan kac adet sahibiz (0 = yok). Ayni kart defalarca alinabilir.</summary>
    public static int OwnedCount(string id)
    {
        Ensure();
        var e = FindOwned(id);
        return e != null ? e.count : 0;
    }

    /// <summary>En az 1 adet var mi?</summary>
    public static bool OwnsCard(string id) => OwnedCount(id) > 0;

    /// <summary>Sahip olunan adete n ekler (shop'ta alinca). Yeni kart ise girdi olusturur.</summary>
    public static void AddOwned(string id, int n = 1)
    {
        if (string.IsNullOrEmpty(id) || n <= 0) return;
        Ensure();
        var e = FindOwned(id);
        if (e == null) { e = new OwnedEntry { id = id, count = 0 }; _data.owned.Add(e); }
        e.count += n;
        Save();
    }

    /// <summary>Sahip olunan (adedi > 0) DISTINCT kart id'leri. Build ekrani listeler.</summary>
    public static List<string> OwnedIds()
    {
        Ensure();
        var list = new List<string>();
        for (int i = 0; i < _data.owned.Count; i++)
            if (_data.owned[i] != null && _data.owned[i].count > 0) list.Add(_data.owned[i].id);
        return list;
    }
    #endregion

    #region Public API — Slots / Deck
    public static int ActiveSlot
    {
        get { Ensure(); return Mathf.Clamp(_data.activeSlot, 0, SlotCount - 1); }
        set { Ensure(); _data.activeSlot = Mathf.Clamp(value, 0, SlotCount - 1); Save(); }
    }

    public static IReadOnlyList<string> GetSlot(int slot)
    {
        Ensure();
        return ValidSlot(slot) ? _data.slots[slot].cards : Array.Empty<string>();
    }

    public static List<string> GetActiveDeck()
    {
        Ensure();
        int s = ActiveSlot;
        return ValidSlot(s) ? new List<string>(_data.slots[s].cards) : new List<string>();
    }

    /// <summary>Slotta belirli karttan kac adet var.</summary>
    public static int CountInSlot(int slot, string id)
    {
        Ensure();
        if (!ValidSlot(slot)) return 0;
        var list = _data.slots[slot].cards; int n = 0;
        for (int i = 0; i < list.Count; i++) if (list[i] == id) n++;
        return n;
    }

    public static bool AddCardToSlot(int slot, string id)
    {
        Ensure();
        if (!ValidSlot(slot) || string.IsNullOrEmpty(id)) return false;
        var list = _data.slots[slot].cards;
        if (list.Count >= MaxCardsPerSlot) return false;
        list.Add(id); Save(); return true;
    }

    /// <summary>Slottaki bir karti listenin BASINA tasir (combo silahi = ilk silah oldugu icin combo'yu bu silaha verir).</summary>
    public static bool MoveCardToFront(int slot, int index)
    {
        Ensure();
        if (!ValidSlot(slot)) return false;
        var list = _data.slots[slot].cards;
        if (index < 0 || index >= list.Count) return false;
        var id = list[index];
        list.RemoveAt(index);
        list.Insert(0, id);
        Save();
        return true;
    }

    // ---- Silah slotlari (sabit 3'lu; orta index 1 = combo). Silahlar cards'tan AYRI tutulur. ----
    /// <summary>Loadout slotunun 3'lu silah dizisini (kopya) dondurur; [0]=sol [1]=orta(combo) [2]=sag.</summary>
    public static string[] GetWeaponSlots(int slot)
    {
        Ensure();
        if (!ValidSlot(slot)) return new string[WeaponSlotCap];
        EnsureWeaponSlots(_data.slots[slot]);
        return (string[])_data.slots[slot].weaponSlots.Clone();
    }

    /// <summary>Combo silahi = orta slot (index 1). Bos ise "".</summary>
    public static string GetComboWeaponId(int slot)
    {
        var ws = GetWeaponSlots(slot);
        return ws.Length > 1 ? ws[1] : "";
    }

    /// <summary>Dolu silah slotu sayisi.</summary>
    public static int WeaponSlotFilledCount(int slot)
    {
        var ws = GetWeaponSlots(slot); int n = 0;
        for (int i = 0; i < ws.Length; i++) if (!string.IsNullOrEmpty(ws[i])) n++;
        return n;
    }

    /// <summary>Bu silah zaten bir slotta mi?</summary>
    public static bool HasWeaponInSlots(int slot, string id)
    {
        var ws = GetWeaponSlots(slot);
        for (int i = 0; i < ws.Length; i++) if (ws[i] == id) return true;
        return false;
    }

    /// <summary>Silahi ilk BOS slota koyar (center-first: orta,1 -> sol,0 -> sag,2). Zaten varsa/doluysa false.</summary>
    public static bool AddWeaponAuto(int slot, string id)
    {
        Ensure();
        if (!ValidSlot(slot) || string.IsNullOrEmpty(id)) return false;
        EnsureWeaponSlots(_data.slots[slot]);
        var ws = _data.slots[slot].weaponSlots;
        for (int i = 0; i < ws.Length; i++) if (ws[i] == id) return false; // zaten var
        int[] order = { 1, 0, 2 };
        for (int k = 0; k < order.Length; k++)
            if (string.IsNullOrEmpty(ws[order[k]])) { ws[order[k]] = id; Save(); return true; }
        return false; // dolu
    }

    /// <summary>Belirli bir silah slotunu bosaltir (sadece o slot; digerleri kaymaz).</summary>
    public static bool RemoveWeaponSlot(int slot, int index)
    {
        Ensure();
        if (!ValidSlot(slot)) return false;
        EnsureWeaponSlots(_data.slots[slot]);
        var ws = _data.slots[slot].weaponSlots;
        if (index < 0 || index >= ws.Length || string.IsNullOrEmpty(ws[index])) return false;
        ws[index] = ""; Save(); return true;
    }

    public static bool RemoveCardFromSlotAt(int slot, int index)
    {
        Ensure();
        if (!ValidSlot(slot) || index < 0 || index >= _data.slots[slot].cards.Count) return false;
        _data.slots[slot].cards.RemoveAt(index); Save(); return true;
    }

    public static void ClearSlot(int slot)
    {
        Ensure();
        if (!ValidSlot(slot)) return;
        _data.slots[slot].cards.Clear(); Save();
    }
    #endregion

    #region Public API — Maintenance
    public static void SaveNow() { Ensure(); Save(); }

    public static void ResetAll()
    {
        _data = FreshData(); Save(); OnCoresChanged?.Invoke(_data.bankedCores);
    }
    #endregion

    #region Private Methods
    private static OwnedEntry FindOwned(string id)
    {
        if (string.IsNullOrEmpty(id) || _data.owned == null) return null;
        for (int i = 0; i < _data.owned.Count; i++)
            if (_data.owned[i] != null && _data.owned[i].id == id) return _data.owned[i];
        return null;
    }

    private static bool ValidSlot(int s) => s >= 0 && s < SlotCount && _data.slots != null && s < _data.slots.Length && _data.slots[s] != null;

    private static void Ensure() { if (_data == null) { Load(); MigrateClaw(); MigrateWeaponsToSlots(); } }

    private const string ClawCardId = "wpn_claw"; // herkesin sahip oldugu temel pence silahi karti

    /// <summary>Bir kez: herkes wpn_claw'a sahip olsun ve claw olmayan loadout'lara eklensin (eski kayitlar melee'siz kalmasin).</summary>
    private static void MigrateClaw()
    {
        if (_data == null || _data.clawGranted) return;
        _data.clawGranted = true;
        var e = FindOwned(ClawCardId);
        if (e == null) { e = new OwnedEntry { id = ClawCardId, count = 0 }; _data.owned.Add(e); }
        if (e.count < 1) e.count = 1;
        if (_data.slots != null)
            for (int i = 0; i < _data.slots.Length; i++)
                if (_data.slots[i] != null && _data.slots[i].cards != null && !_data.slots[i].cards.Contains(ClawCardId))
                    _data.slots[i].cards.Insert(0, ClawCardId);
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(_data));
        PlayerPrefs.Save(); // Save() cagirmiyoruz: Load ortasinda OnChanged firlatmak istemeyiz
    }

    /// <summary>Bir slotun weaponSlots dizisini 3 uzunlukta + null'suz garanti eder.</summary>
    private static void EnsureWeaponSlots(SlotData sd)
    {
        if (sd == null) return;
        if (sd.weaponSlots == null || sd.weaponSlots.Length != WeaponSlotCap)
        {
            var fix = new string[WeaponSlotCap];
            for (int i = 0; i < WeaponSlotCap; i++)
                fix[i] = (sd.weaponSlots != null && i < sd.weaponSlots.Length && sd.weaponSlots[i] != null) ? sd.weaponSlots[i] : "";
            sd.weaponSlots = fix;
        }
        for (int i = 0; i < sd.weaponSlots.Length; i++) if (sd.weaponSlots[i] == null) sd.weaponSlots[i] = "";
    }

    /// <summary>Bir kez: cards icindeki SILAH kartlarini sabit weaponSlots'a tasir (center-first, orijinal sira). Statlar cards'ta kalir.</summary>
    private static void MigrateWeaponsToSlots()
    {
        if (_data == null || _data.weaponsMigrated) return;
        _data.weaponsMigrated = true;
        if (_data.slots == null) return;
        for (int s = 0; s < _data.slots.Length; s++)
        {
            var sd = _data.slots[s]; if (sd == null) continue;
            EnsureWeaponSlots(sd);
            if (sd.cards == null) continue;
            // TAHRIBATSIZ: cards'tan SILMEDEN silah kartlarini topla (veri kaybi olmasin). cards'taki silah
            // kartlari bundan sonra "hayalet" (DeckApplier/BuildUI yok sayar; kaynak = weaponSlots).
            var weaponIds = new List<string>();
            for (int i = 0; i < sd.cards.Count; i++)
            {
                var c = CardCatalog.Get(sd.cards[i]);
                if (c != null && c.category == CardCategory.Weapon && !weaponIds.Contains(sd.cards[i])) weaponIds.Add(sd.cards[i]);
            }
            int[] order = { 1, 0, 2 }; // center-first
            int wi = 0;
            for (int k = 0; k < order.Length && wi < weaponIds.Count; k++)
                if (string.IsNullOrEmpty(sd.weaponSlots[order[k]])) sd.weaponSlots[order[k]] = weaponIds[wi++];
        }
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(_data));
        PlayerPrefs.Save();
    }

    private static MetaData FreshData()
    {
        var d = new MetaData { bankedCores = 0, owned = new List<OwnedEntry>(), activeSlot = 0, slots = new SlotData[SlotCount] };
        for (int i = 0; i < SlotCount; i++) d.slots[i] = new SlotData();
        return d;
    }

    private static void Load()
    {
        string json = PlayerPrefs.GetString(PrefsKey, "");
        if (string.IsNullOrEmpty(json)) { _data = FreshData(); return; }
        try { _data = JsonUtility.FromJson<MetaData>(json); } catch { _data = null; }
        if (_data == null) { _data = FreshData(); return; }
        if (_data.owned == null) _data.owned = new List<OwnedEntry>();
        if (_data.slots == null || _data.slots.Length != SlotCount)
        {
            var fix = new SlotData[SlotCount];
            for (int i = 0; i < SlotCount; i++)
                fix[i] = (_data.slots != null && i < _data.slots.Length && _data.slots[i] != null) ? _data.slots[i] : new SlotData();
            _data.slots = fix;
        }
        for (int i = 0; i < SlotCount; i++) if (_data.slots[i].cards == null) _data.slots[i].cards = new List<string>();
        for (int i = 0; i < SlotCount; i++) EnsureWeaponSlots(_data.slots[i]);
    }

    private static void Save()
    {
        if (_data == null) return;
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(_data));
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }
    #endregion
}

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
    public const int MaxCardsPerSlot = 20;
    private const string PrefsKey = "MeowvivorsMeta_v2"; // v2: owned artik adet-tabanli
    #endregion

    #region Data Model
    [Serializable] private class SlotData { public List<string> cards = new List<string>(); }
    [Serializable] private class OwnedEntry { public string id; public int count; }

    [Serializable]
    private class MetaData
    {
        public int bankedCores;
        public List<OwnedEntry> owned = new List<OwnedEntry>(); // kart id -> sahip olunan adet
        public SlotData[] slots;
        public int activeSlot;
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

    private static void Ensure() { if (_data == null) Load(); }

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

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Roguelite META ilerlemesinin KALICI kaydi (runlar arasi). Banka core (kalici para),
/// sahip olunan kartlar (blueprint modeli: id sahipligi) ve 3 build slotu (her biri max 20 kart)
/// + aktif slot tutar. JsonUtility ile tek metne cevrilip PlayerPrefs'e yazilir (mobil uyumlu).
/// Statik erisim (DamagePopupManager/CoreManager pattern'i) — sahnede obje gerektirmez.
/// Lazy load + her mutasyonda auto-save. God manager DEGIL: sadece meta kaydin sorumlusu.
/// </summary>
public static class MetaSave
{
    #region Constants
    /// <summary>Build slot sayisi.</summary>
    public const int SlotCount = 3;
    /// <summary>Bir slota konabilecek en fazla kart.</summary>
    public const int MaxCardsPerSlot = 20;
    private const string PrefsKey = "MeowvivorsMeta_v1";
    #endregion

    #region Data Model
    [Serializable]
    private class SlotData
    {
        public List<string> cards = new List<string>();
    }

    [Serializable]
    private class MetaData
    {
        public int bankedCores;
        public List<string> ownedCards = new List<string>(); // blueprint: sahip olunan kart id'leri
        public SlotData[] slots;                             // uzunluk = SlotCount
        public int activeSlot;
    }
    #endregion

    #region Private Fields
    private static MetaData _data; // null ise henuz yuklenmedi
    #endregion

    #region Events
    /// <summary>Herhangi bir meta veri degisince firlar (UI genel yenilemesi icin).</summary>
    public static event Action OnChanged;
    /// <summary>Banka core degisince firlar. Parametre: yeni bakiye.</summary>
    public static event Action<int> OnCoresChanged;
    #endregion

    #region Public API — Cores
    /// <summary>Banka core bakiyesi (kalici para).</summary>
    public static int BankedCores { get { Ensure(); return _data.bankedCores; } }

    /// <summary>Run sonunda toplanan core'lari bankaya ekler (kalici).</summary>
    public static void AddCores(int amount)
    {
        if (amount <= 0) return;
        Ensure();
        _data.bankedCores += amount;
        Save();
        OnCoresChanged?.Invoke(_data.bankedCores);
    }

    /// <summary>Shop'ta harcama: yeterli bakiye varsa duser ve true doner; yoksa false (harcamaz).</summary>
    public static bool SpendCores(int amount)
    {
        Ensure();
        if (amount < 0 || _data.bankedCores < amount) return false;
        _data.bankedCores -= amount;
        Save();
        OnCoresChanged?.Invoke(_data.bankedCores);
        return true;
    }
    #endregion

    #region Public API — Owned Cards (blueprint)
    /// <summary>Bu kart id'sine sahip miyiz (bir kez alinir, tum slotlarda kullanilabilir).</summary>
    public static bool OwnsCard(string id)
    {
        Ensure();
        return !string.IsNullOrEmpty(id) && _data.ownedCards.Contains(id);
    }

    /// <summary>Karti sahiplige ekler (shop'tan alinca). Zaten sahipsek bir sey yapmaz.</summary>
    public static void AddOwnedCard(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        Ensure();
        if (!_data.ownedCards.Contains(id))
        {
            _data.ownedCards.Add(id);
            Save();
        }
    }

    /// <summary>Sahip olunan tum kart id'leri (salt-okunur).</summary>
    public static IReadOnlyList<string> OwnedCards { get { Ensure(); return _data.ownedCards; } }
    #endregion

    #region Public API — Slots / Deck
    /// <summary>Aktif slot indeksi (0..SlotCount-1). Run bu slotun kartlarini uygular.</summary>
    public static int ActiveSlot
    {
        get { Ensure(); return Mathf.Clamp(_data.activeSlot, 0, SlotCount - 1); }
        set { Ensure(); _data.activeSlot = Mathf.Clamp(value, 0, SlotCount - 1); Save(); }
    }

    /// <summary>Verilen slottaki kart id'leri (salt-okunur). Gecersiz slot -> bos.</summary>
    public static IReadOnlyList<string> GetSlot(int slot)
    {
        Ensure();
        return ValidSlot(slot) ? _data.slots[slot].cards : Array.Empty<string>();
    }

    /// <summary>Aktif slotun kartlarinin KOPYASI (run baslarken uygulanacak deck).</summary>
    public static List<string> GetActiveDeck()
    {
        Ensure();
        int s = ActiveSlot;
        return ValidSlot(s) ? new List<string>(_data.slots[s].cards) : new List<string>();
    }

    /// <summary>Slota kart ekler (max 20). Basarisizsa (slot dolu/gecersiz) false.</summary>
    public static bool AddCardToSlot(int slot, string id)
    {
        Ensure();
        if (!ValidSlot(slot) || string.IsNullOrEmpty(id)) return false;
        var list = _data.slots[slot].cards;
        if (list.Count >= MaxCardsPerSlot) return false;
        list.Add(id);
        Save();
        return true;
    }

    /// <summary>Slottan verilen indeksteki karti cikarir.</summary>
    public static bool RemoveCardFromSlotAt(int slot, int index)
    {
        Ensure();
        if (!ValidSlot(slot) || index < 0 || index >= _data.slots[slot].cards.Count) return false;
        _data.slots[slot].cards.RemoveAt(index);
        Save();
        return true;
    }

    /// <summary>Slotu tamamen bosaltir.</summary>
    public static void ClearSlot(int slot)
    {
        Ensure();
        if (!ValidSlot(slot)) return;
        _data.slots[slot].cards.Clear();
        Save();
    }
    #endregion

    #region Public API — Maintenance
    /// <summary>Bekleyen veriyi hemen diske yazar (guvenlik icin; normalde mutasyonlar zaten kaydeder).</summary>
    public static void SaveNow() { Ensure(); Save(); }

    /// <summary>TUM meta ilerlemeyi sifirlar (test/"save sil" icin). Geri alinamaz.</summary>
    public static void ResetAll()
    {
        _data = FreshData();
        Save();
        OnCoresChanged?.Invoke(_data.bankedCores);
    }
    #endregion

    #region Private Methods
    private static bool ValidSlot(int s) => s >= 0 && s < SlotCount && _data.slots != null && s < _data.slots.Length && _data.slots[s] != null;

    private static void Ensure() { if (_data == null) Load(); }

    private static MetaData FreshData()
    {
        var d = new MetaData
        {
            bankedCores = 0,
            ownedCards = new List<string>(),
            activeSlot = 0,
            slots = new SlotData[SlotCount]
        };
        for (int i = 0; i < SlotCount; i++) d.slots[i] = new SlotData();
        return d;
    }

    private static void Load()
    {
        string json = PlayerPrefs.GetString(PrefsKey, "");
        if (string.IsNullOrEmpty(json)) { _data = FreshData(); return; }

        try { _data = JsonUtility.FromJson<MetaData>(json); }
        catch { _data = null; }
        if (_data == null) { _data = FreshData(); return; }

        // Migration/guard: bozuk veya eski kayit -> eksikleri tamamla, cokme
        if (_data.ownedCards == null) _data.ownedCards = new List<string>();
        if (_data.slots == null || _data.slots.Length != SlotCount)
        {
            var fixedSlots = new SlotData[SlotCount];
            for (int i = 0; i < SlotCount; i++)
                fixedSlots[i] = (_data.slots != null && i < _data.slots.Length && _data.slots[i] != null) ? _data.slots[i] : new SlotData();
            _data.slots = fixedSlots;
        }
        for (int i = 0; i < SlotCount; i++)
            if (_data.slots[i].cards == null) _data.slots[i].cards = new List<string>();
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

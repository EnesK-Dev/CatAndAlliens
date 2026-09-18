using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Build (deck dizme) ekrani: 3 slottan birini secer, sahip olunan kartlari aktif slota diz/cikar.
/// Aktif slot run'da kullanilir (DeckApplier okur). Blueprint: sadece owned kartlar dizilir; maxPerSlot
/// (kart basi) + 20 (slot) sinirlari uygulanir. God manager degil: tek sorumluluk = deck dizme UI'si.
/// </summary>
public class BuildUI : MonoBehaviour
{
    #region Serialized Fields
    [Header("Referanslar")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text slotLabel;      // "Slot 1 - 5/20"
    [SerializeField] private Transform contentParent; // owned kart satirlari buraya
    [SerializeField] private BuildItemUI itemPrefab;
    [Tooltip("3 slot sekmesi butonu (sirayla slot 0/1/2).")]
    [SerializeField] private Button[] slotTabs;

    [Header("Sekme Renkleri")]
    [SerializeField] private Color activeTabColor = new Color(0.25f, 0.6f, 0.9f, 1f);
    [SerializeField] private Color inactiveTabColor = new Color(0.2f, 0.2f, 0.25f, 1f);
    #endregion

    #region Private Fields
    private readonly List<BuildItemUI> _items = new List<BuildItemUI>();
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        // Sekme butonlarini indekslerine bagla
        if (slotTabs != null)
            for (int i = 0; i < slotTabs.Length; i++)
            {
                int idx = i; // closure
                if (slotTabs[i] != null) slotTabs[i].onClick.AddListener(() => SelectSlot(idx));
            }
    }
    #endregion

    #region Public Methods
    /// <summary>Build panelini acar ve owned kart listesini (yeniden) kurar.</summary>
    public void Open()
    {
        SfxManager.Play(SfxId.ButtonClick);
        if (panelRoot != null) panelRoot.SetActive(true);
        Rebuild();
        RefreshAll();
    }

    /// <summary>Build panelini kapatir.</summary>
    public void Close()
    {
        SfxManager.Play(SfxId.ButtonClick);
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    /// <summary>Aktif slotu degistirir (sekme).</summary>
    public void SelectSlot(int index)
    {
        SfxManager.Play(SfxId.ButtonClick);
        MetaSave.ActiveSlot = index;
        RefreshAll();
    }
    #endregion

    #region Private Methods
    /// <summary>Sahip olunan her kart icin bir satir olusturur (owned degisebildigi icin her acilista yeniden).</summary>
    private void Rebuild()
    {
        for (int i = 0; i < _items.Count; i++)
            if (_items[i] != null) Destroy(_items[i].gameObject);
        _items.Clear();

        if (itemPrefab == null || contentParent == null) { Debug.LogWarning("BuildUI: itemPrefab/contentParent atanmamis."); return; }

        var owned = MetaSave.OwnedIds();
        for (int i = 0; i < owned.Count; i++)
        {
            var card = CardCatalog.Get(owned[i]);
            if (card == null) continue;
            var item = Instantiate(itemPrefab, contentParent);
            item.gameObject.SetActive(true);
            _items.Add(item);
        }
    }

    /// <summary>Tum satirlari (slottaki sayilar/limitler), sayaci ve sekme renklerini gunceller.</summary>
    private void RefreshAll()
    {
        int active = MetaSave.ActiveSlot;
        int total = MetaSave.GetSlot(active).Count;

        // Satirlar — stack limiti = min(sahip olunan adet, kart-basi cap)
        var owned = MetaSave.OwnedIds();
        int r = 0;
        for (int i = 0; i < owned.Count && r < _items.Count; i++)
        {
            var card = CardCatalog.Get(owned[i]);
            if (card == null) continue;
            int inSlot = MetaSave.CountInSlot(active, card.id);
            int perCardCap = card.maxPerSlot > 0 ? card.maxPerSlot : MetaSave.MaxCardsPerSlot;
            int maxAllowed = Mathf.Min(MetaSave.OwnedCount(card.id), perCardCap);
            _items[r].Bind(card, inSlot, maxAllowed, total >= MetaSave.MaxCardsPerSlot, HandleAdd, HandleRemove);
            r++;
        }

        if (slotLabel != null) slotLabel.text = "Slot " + (active + 1) + "  -  " + total + "/" + MetaSave.MaxCardsPerSlot;

        // Sekme renkleri
        if (slotTabs != null)
            for (int i = 0; i < slotTabs.Length; i++)
            {
                if (slotTabs[i] == null) continue;
                var img = slotTabs[i].targetGraphic as Image;
                if (img != null) img.color = (i == active) ? activeTabColor : inactiveTabColor;
            }
    }

    private void HandleAdd(string id)
    {
        var card = CardCatalog.Get(id);
        if (card == null) return;
        int active = MetaSave.ActiveSlot;
        int inSlot = MetaSave.CountInSlot(active, id);
        int perCardCap = card.maxPerSlot > 0 ? card.maxPerSlot : MetaSave.MaxCardsPerSlot;
        int maxAllowed = Mathf.Min(MetaSave.OwnedCount(id), perCardCap);
        if (inSlot >= maxAllowed) return;
        MetaSave.AddCardToSlot(active, id); // 20-cap MetaSave icinde
        RefreshAll();
    }

    private void HandleRemove(string id)
    {
        int active = MetaSave.ActiveSlot;
        var slot = MetaSave.GetSlot(active);
        for (int i = 0; i < slot.Count; i++)
            if (slot[i] == id) { MetaSave.RemoveCardFromSlotAt(active, i); break; }
        RefreshAll();
    }
    #endregion
}

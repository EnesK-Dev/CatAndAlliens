using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Build (deck dizme) ekrani: 3 slottan birini secer, sahip olunan kartlari aktif slota diz/cikar.
/// Duzen shop tarzi (silah + upgrade gruplari). Slotta EN AZ 1 olan kartlar cizginin USTUNDE (aktif),
/// hic dizilmemis (0) kartlar cizginin ALTINDA gri/soluk. Her ekle/cikar sonrasi yeniden dizilir.
/// God manager degil: tek sorumluluk = deck dizme UI'si.
/// </summary>
public class BuildUI : MonoBehaviour
{
    #region Serialized Fields
    [Header("Referanslar")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text slotLabel;
    [Tooltip("Satirlarin ekleneceği icerik (VerticalLayoutGroup + ShopCardSizer).")]
    [SerializeField] private Transform contentParent;
    [SerializeField] private BuildItemUI itemPrefab;
    [Tooltip("3 slot sekmesi butonu (sirayla slot 0/1/2).")]
    [SerializeField] private Button[] slotTabs;

    [Header("Sekme Renkleri")]
    [SerializeField] private Color activeTabColor = new Color(0.25f, 0.6f, 0.9f, 1f);
    [SerializeField] private Color inactiveTabColor = new Color(0.2f, 0.2f, 0.25f, 1f);

    [Header("Duzen")]
    [SerializeField] private float rowSpacing = 24f;
    #endregion

    #region Private Fields
    private readonly List<BuildItemUI> _items = new List<BuildItemUI>();
    private readonly List<string> _itemCardIds = new List<string>();
    private ShopCardSizer _sizer;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        if (slotTabs != null)
            for (int i = 0; i < slotTabs.Length; i++)
            {
                int idx = i;
                if (slotTabs[i] != null) slotTabs[i].onClick.AddListener(() => SelectSlot(idx));
            }
    }
    #endregion

    #region Public Methods
    /// <summary>Build panelini acar ve listeyi (yeniden) kurar.</summary>
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

    /// <summary>Aktif slotu degistirir (sekme) — dizilim de yenilenir.</summary>
    public void SelectSlot(int index)
    {
        SfxManager.Play(SfxId.ButtonClick);
        MetaSave.ActiveSlot = index;
        Rebuild();
        RefreshAll();
    }
    #endregion

    #region Private Methods
    /// <summary>Owned kartlari aktif(>=1)/pasif(0) diye ayirip shop tarzi dizer; arasina cizgi koyar.</summary>
    private void Rebuild()
    {
        if (itemPrefab == null || contentParent == null) { Debug.LogWarning("BuildUI: itemPrefab/contentParent atanmamis."); return; }
        _sizer = contentParent.GetComponent<ShopCardSizer>();

        // eski satir/cizgileri temizle
        for (int i = contentParent.childCount - 1; i >= 0; i--) Destroy(contentParent.GetChild(i).gameObject);
        _items.Clear();
        _itemCardIds.Clear();

        int active = MetaSave.ActiveSlot;
        var owned = MetaSave.OwnedIds().Select(CardCatalog.Get).Where(c => c != null).ToList();
        var used = owned.Where(c => MetaSave.CountInSlot(active, c.id) > 0).ToList();
        var unused = owned.Where(c => MetaSave.CountInSlot(active, c.id) == 0).ToList();

        BuildSection(used);                                   // aktif — cizgi ustu
        if (used.Count > 0 && unused.Count > 0) CreateDivider();
        BuildSection(unused);                                 // gri — cizgi alti

        if (_sizer != null) _sizer.Apply();
    }

    /// <summary>Verilen kartlari shop tarzi gruplar: renkli statlar + multislash, gri statlar, sonra her silah + upgrade satiri.</summary>
    private void BuildSection(List<CardDefinition> list)
    {
        if (list == null || list.Count == 0) return;

        bool IsGrey(CardDefinition c) => c.frameSprite != null && c.frameSprite.name.Contains("grey");
        int ColorRank(CardDefinition c)
        {
            string n = c.frameSprite != null ? c.frameSprite.name : "";
            if (n.Contains("blue")) return 0;
            if (n.Contains("red")) return 1;
            if (n.Contains("yellow")) return 2;
            if (n.Contains("green")) return 3;
            return 4;
        }

        var stats = list.Where(c => c.category == CardCategory.Stat).ToList();
        var colored = stats.Where(c => !IsGrey(c)).OrderBy(ColorRank).ToList();
        var greys = stats.Where(IsGrey).ToList();
        var multislash = list.FirstOrDefault(c => c.category == CardCategory.Weapon && c.weaponId == "multislash");

        if (colored.Count > 0 || multislash != null)
        {
            var r = CreateRow();
            foreach (var c in colored) AddCard(r, c);
            if (multislash != null) AddCard(r, multislash);
        }
        if (greys.Count > 0)
        {
            var r = CreateRow();
            foreach (var c in greys) AddCard(r, c);
        }

        foreach (var wid in new[] { "blaster", "boomerang", "orbital" })
        {
            var w = list.FirstOrDefault(c => c.category == CardCategory.Weapon && c.weaponId == wid);
            if (w != null) { var r = CreateRow(); AddCard(r, w); }
            var ups = list.Where(c => c.category == CardCategory.WeaponUpgrade && c.weaponId == wid).ToList();
            if (ups.Count > 0) { var r = CreateRow(); foreach (var u in ups) AddCard(r, u); }
        }
    }

    private Transform CreateRow()
    {
        var go = new GameObject("Row", typeof(RectTransform));
        go.transform.SetParent(contentParent, false);
        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = rowSpacing;
        hlg.childControlWidth = true;  hlg.childForceExpandWidth = false;
        hlg.childControlHeight = true; hlg.childForceExpandHeight = false;
        return go.transform;
    }

    private void CreateDivider()
    {
        var go = new GameObject("Divider", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(contentParent, false);
        var img = go.GetComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.22f);
        img.raycastTarget = false;
        var le = go.GetComponent<LayoutElement>();
        le.preferredHeight = 4f; le.minHeight = 4f; le.flexibleWidth = 1f;
    }

    private void AddCard(Transform row, CardDefinition card)
    {
        var item = Instantiate(itemPrefab, row);
        item.gameObject.SetActive(true);
        if (item.GetComponent<LayoutElement>() == null) item.gameObject.AddComponent<LayoutElement>();
        _items.Add(item);
        _itemCardIds.Add(card.id);
    }

    /// <summary>Tum kartlari (slottaki sayi/limit/soluk), sayaci ve sekme renklerini gunceller.</summary>
    private void RefreshAll()
    {
        int active = MetaSave.ActiveSlot;
        int total = MetaSave.GetSlot(active).Count;

        for (int i = 0; i < _items.Count; i++)
        {
            var card = CardCatalog.Get(_itemCardIds[i]);
            if (card == null) continue;
            int inSlot = MetaSave.CountInSlot(active, card.id);
            int perCardCap = card.maxPerSlot > 0 ? card.maxPerSlot : MetaSave.MaxCardsPerSlot;
            int maxAllowed = Mathf.Min(MetaSave.OwnedCount(card.id), perCardCap);
            _items[i].Bind(card, inSlot, maxAllowed, total >= MetaSave.MaxCardsPerSlot, HandleAdd, HandleRemove);
        }

        if (slotLabel != null) slotLabel.text = "Slot " + (active + 1) + "  -  " + total + "/" + MetaSave.MaxCardsPerSlot;

        if (slotTabs != null)
            for (int i = 0; i < slotTabs.Length; i++)
            {
                if (slotTabs[i] == null) continue;
                var img = slotTabs[i].targetGraphic as Image;
                if (img != null) img.color = (i == active) ? activeTabColor : inactiveTabColor;
            }

        if (_sizer != null) _sizer.Apply();
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
        MetaSave.AddCardToSlot(active, id);
        Rebuild();     // kart aktif/pasif tarafi degisebilir -> yeniden diz
        RefreshAll();
    }

    private void HandleRemove(string id)
    {
        int active = MetaSave.ActiveSlot;
        var slot = MetaSave.GetSlot(active);
        for (int i = 0; i < slot.Count; i++)
            if (slot[i] == id) { MetaSave.RemoveCardFromSlotAt(active, i); break; }
        Rebuild();
        RefreshAll();
    }
    #endregion
}

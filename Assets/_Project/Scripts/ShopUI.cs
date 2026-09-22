using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Shop ekrani: banka core'unu gosterir ve katalogdaki kartlari BOLUMLER halinde listeler:
/// once statlar (5'li satirlar), sonra HER SILAH ortada tek basina + altinda kendi upgrade'leri tek satir.
/// Silah upgrade'leri, o silah ALINMADAN kilitli (BUY pasif + kart soluk). Buy -> SpendCores + AddOwned.
/// Duzen dikey layout + satir (HorizontalLayoutGroup); kart boyutu ShopCardSizer ile responsive.
/// </summary>
public class ShopUI : MonoBehaviour
{
    #region Serialized Fields
    [Header("Referanslar")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text coresText;
    [Tooltip("Satirlarin ekleneceği icerik (VerticalLayoutGroup + ShopCardSizer).")]
    [SerializeField] private Transform contentParent;
    [Tooltip("Tek kart prefab'i (ShopItemUI).")]
    [SerializeField] private ShopItemUI itemPrefab;

    [Header("Duzen")]
    [Tooltip("Bir satirdaki kartlar arasi bosluk (ShopCardSizer.rowSpacing ile ayni olmali).")]
    [SerializeField] private float rowSpacing = 24f;
    [Tooltip("Stat kartlarinin bir satirda kac tane dizilecegi.")]
    [SerializeField] private int statsPerRow = 5;
    #endregion

    #region Private Fields
    private readonly List<ShopItemUI> _items = new List<ShopItemUI>();
    private readonly List<string> _itemCardIds = new List<string>();
    private ShopCardSizer _sizer;
    private bool _built;
    #endregion

    #region Unity Callbacks
    private void OnEnable() { MetaSave.OnCoresChanged += HandleCoresChanged; }
    private void OnDisable() { MetaSave.OnCoresChanged -= HandleCoresChanged; }
    #endregion

    #region Public Methods
    /// <summary>Shop panelini acar ve listeyi (ilk kez) kurar/yeniler.</summary>
    public void Open()
    {
        SfxManager.Play(SfxId.ButtonClick);
        if (panelRoot != null) panelRoot.SetActive(true);
        Build();
        RefreshAll();
    }

    /// <summary>Shop panelini kapatir.</summary>
    public void Close()
    {
        SfxManager.Play(SfxId.ButtonClick);
        if (panelRoot != null) panelRoot.SetActive(false);
    }
    #endregion

    #region Private Methods
    /// <summary>Katalogdan bolumlu duzeni kurar (bir kez): statlar + her silah & upgrade'leri.</summary>
    private void Build()
    {
        if (_built) return;
        if (itemPrefab == null || contentParent == null) { Debug.LogWarning("ShopUI: itemPrefab/contentParent atanmamis."); return; }

        _sizer = contentParent.GetComponent<ShopCardSizer>();
        var all = CardCatalog.All;

        // Cerceve rengine gore stat ayrimi (gri/beyaz kartlar ayri satirda).
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

        var stats = all.Where(c => c.category == CardCategory.Stat).ToList();
        var colored = stats.Where(c => !IsGrey(c)).OrderBy(ColorRank).ToList();
        var greys   = stats.Where(IsGrey).ToList();
        var multislash = all.FirstOrDefault(c => c.category == CardCategory.Weapon && c.weaponId == "multislash");

        // Satir 1: renkli statlar (mavi/kirmizi/sari/yesil) + tek kartlik Multi-Slash
        var row1 = CreateRow();
        foreach (var c in colored) AddCard(row1, c);
        if (multislash != null) AddCard(row1, multislash);

        // Satir 2: gri/beyaz kartlar (dash / food / ultimate)
        if (greys.Count > 0)
        {
            CreateDivider();
            var row2 = CreateRow();
            foreach (var c in greys) AddCard(row2, c);
        }

        // Sonra ozel silahlar (multislash haric): her silah kendi kategorisi -> arasina cizgi
        var weapons = all.Where(c => c.category == CardCategory.Weapon && c.weaponId != "multislash").ToList();
        foreach (var w in weapons)
        {
            CreateDivider();
            var wRow = CreateRow();
            AddCard(wRow, w);

            var ups = all.Where(c => c.category == CardCategory.WeaponUpgrade && c.weaponId == w.weaponId).ToList();
            if (ups.Count > 0)
            {
                var uRow = CreateRow();
                foreach (var u in ups) AddCard(uRow, u);
            }
        }

        _built = true;
        if (_sizer != null) _sizer.Apply();
    }

    /// <summary>Ortalayan bir satir (HorizontalLayoutGroup) olusturur.</summary>
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

    /// <summary>Kategoriler arasi ince ayirici cizgi (tam genislik). ShopCardSizer bunu atlar (isim "Divider").</summary>
    private void CreateDivider()
    {
        var go = new GameObject("Divider", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(LayoutElement));
        go.transform.SetParent(contentParent, false);
        var img = go.GetComponent<UnityEngine.UI.Image>();
        img.color = new Color(1f, 1f, 1f, 0.22f);
        img.raycastTarget = false;
        var le = go.GetComponent<LayoutElement>();
        le.preferredHeight = 4f; le.minHeight = 4f; le.flexibleWidth = 1f;
    }

    /// <summary>Bir satira kart ekler (LayoutElement garanti; boyutu ShopCardSizer verir).</summary>
    private void AddCard(Transform row, CardDefinition card)
    {
        var item = Instantiate(itemPrefab, row);
        item.gameObject.SetActive(true);
        if (item.GetComponent<LayoutElement>() == null) item.gameObject.AddComponent<LayoutElement>();
        _items.Add(item);
        _itemCardIds.Add(card.id);
    }

    /// <summary>Tum kartlari (owned/afford/kilit) ve core yazisini gunceller.</summary>
    private void RefreshAll()
    {
        int bank = MetaSave.BankedCores;
        for (int i = 0; i < _items.Count; i++)
        {
            var card = CardCatalog.Get(_itemCardIds[i]);
            if (card == null) continue;
            bool locked = card.category == CardCategory.WeaponUpgrade
                          && MetaSave.OwnedCount("wpn_" + card.weaponId) == 0;
            int effMax = card.maxPerSlot > 0 ? card.maxPerSlot : MetaSave.MaxCardsPerSlot;
            int owned = MetaSave.OwnedCount(card.id);
            bool soldOut = owned >= effMax; // max faydali adete ulasildi
            bool canBuy = !locked && !soldOut && bank >= card.cost;
            // Stack gorseli faydali max'i asmasin (silah=1 -> tek tepe, cok tepe yaniltici olmasin)
            int shownOwned = Mathf.Min(owned, effMax);
            _items[i].Bind(card, shownOwned, canBuy, locked, soldOut, HandleBuy);
        }
        RefreshCores();
        if (_sizer != null) _sizer.Apply();
    }

    private void RefreshCores()
    {
        if (coresText != null) coresText.text = ": " + MetaSave.BankedCores;
    }

    private void HandleBuy(string cardId)
    {
        var card = CardCatalog.Get(cardId);
        if (card == null) return;
        // Guvenlik: upgrade ise silah sahipligini burada da dogrula (UI kilidi atlansa bile).
        if (card.category == CardCategory.WeaponUpgrade && MetaSave.OwnedCount("wpn_" + card.weaponId) == 0) return;
        if (MetaSave.SpendCores(card.cost))
            MetaSave.AddOwned(cardId);
        RefreshAll();
    }

    private void HandleCoresChanged(int newBalance)
    {
        if (panelRoot != null && panelRoot.activeInHierarchy) RefreshAll();
    }
    #endregion
}

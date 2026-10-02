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
    [Tooltip("Bir statin her yeni aliminda fiyat bu carpanla artar (1.6 = her seferinde %60 pahali).")]
    [SerializeField] private float statCostGrowth = 2f;
    [Tooltip("Silah fiyat carpani. TUM silahlar PAYLASIR: herhangi bir silah alininca (claw haric) hepsinin fiyati bu carpanla artar.")]
    [SerializeField] private float weaponCostGrowth = 2f;
    #endregion

    #region Private Fields
    // Oyun ici (GameOver/Pause) 'SHOP' butonu bunu true yapip MainMenu sahnesini yukler;
    // sahne acilinca Start bunu gorup shop panelini otomatik acar (SplashIntro.NextSceneOverride deseni).
    public static bool OpenOnLoad;
    private readonly List<ShopItemUI> _items = new List<ShopItemUI>();
    private readonly List<string> _itemCardIds = new List<string>();
    private ShopCardSizer _sizer;
    private bool _built;
    #endregion

    #region Unity Callbacks
    private void OnEnable() { MetaSave.OnCoresChanged += HandleCoresChanged; }
    private void OnDisable() { MetaSave.OnCoresChanged -= HandleCoresChanged; }

    /// <summary>Sahne yuklenince: oyun ici SHOP butonundan gelindiyse (OpenOnLoad) shop panelini otomatik ac.</summary>
    private void Start()
    {
        if (OpenOnLoad)
        {
            OpenOnLoad = false;
            Open();
        }
    }
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

        // Duzen: SILAHLAR ustte -> cizgi -> STATLAR altta. Statlarin fiyati her alimda artar (CostFor).
        const int perRow = 5;
        var weapons = all.Where(c => c.category == CardCategory.Weapon).ToList();
        Transform row = null;
        for (int i = 0; i < weapons.Count; i++)
        {
            if (i % perRow == 0) row = CreateRow();
            AddCard(row, weapons[i]);
        }

        var stats = all.Where(c => c.category == CardCategory.Stat).ToList();
        if (stats.Count > 0)
        {
            CreateDivider();
            row = null;
            for (int i = 0; i < stats.Count; i++)
            {
                if (i % perRow == 0) row = CreateRow();
                AddCard(row, stats[i]);
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

    /// <summary>Bir kartin GUNCEL fiyati. Statlar: her sahip olunan adette carpanla artar. Silah: sabit.
    /// Carpan uygulandiktan SONRA fiyat 5in en yakin katina yuvarlanir (yukari/asagi) — oyun ici CoreManager ile ayni.</summary>
    private int CostFor(CardDefinition card)
    {
        if (card == null) return 0;
        if (card.category == CardCategory.Stat)
        {
            int owned = MetaSave.OwnedCount(card.id);
            return RoundTo5(card.cost * Mathf.Pow(Mathf.Max(1f, statCostGrowth), owned));
        }
        if (card.category == CardCategory.Weapon)
        {
            // PAYLASIMLI: alinan (claw disi) silah sayisi kadar TUM silahlarin fiyati artar.
            float wg = weaponCostGrowth >= 1.01f ? weaponCostGrowth : 2f; // yeni alan 0 serialize olursa guvenli varsayilan
            return RoundTo5(card.cost * Mathf.Pow(wg, WeaponsOwnedCount()));
        }
        return card.cost;
    }

    /// <summary>Ham fiyati 5in en yakin katina yuvarlar (yukari ya da asagi), en az 5. CoreManager.CostForCardsTaken ile ayni mantik.</summary>
    private static int RoundTo5(float raw)
    {
        int rounded = Mathf.RoundToInt(raw / 5f) * 5;
        return Mathf.Max(5, rounded);
    }

    /// <summary>Sahip olunan (claw haric) silah sayisi — silah fiyat artisi TUM silahlar arasinda PAYLASILIR.</summary>
    private int WeaponsOwnedCount()
    {
        int n = 0;
        var all = CardCatalog.All;
        for (int i = 0; i < all.Count; i++)
        {
            var c = all[i];
            if (c != null && c.category == CardCategory.Weapon && c.id != "wpn_claw" && MetaSave.OwnedCount(c.id) > 0) n++;
        }
        return n;
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
            int cost = CostFor(card);
            bool canBuy = !locked && !soldOut && bank >= cost;
            // Stack gorseli faydali max'i asmasin (silah=1 -> tek tepe, cok tepe yaniltici olmasin)
            int shownOwned = Mathf.Min(owned, effMax);
            _items[i].Bind(card, shownOwned, canBuy, locked, soldOut, HandleBuy, cost);
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
        if (MetaSave.SpendCores(CostFor(card)))
            MetaSave.AddOwned(cardId);
        RefreshAll();
    }

    private void HandleCoresChanged(int newBalance)
    {
        if (panelRoot != null && panelRoot.activeInHierarchy) RefreshAll();
    }
    #endregion
}

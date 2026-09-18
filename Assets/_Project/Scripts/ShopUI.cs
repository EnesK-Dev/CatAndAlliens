using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Shop ekrani: banka core'unu gosterir ve katalogdaki (CardCatalog) kartlari runtime'da listeler.
/// Buy -> MetaSave.SpendCores + AddOwnedCard (blueprint: bir kez alinir). Gevsek bagli (MetaSave event).
/// God manager degil: sadece satin alma UI'si. Build ekrani (slot dizme) FAZ 6.
/// </summary>
public class ShopUI : MonoBehaviour
{
    #region Serialized Fields
    [Header("Referanslar")]
    [Tooltip("Acilip kapanacak panel kok (bu script'in objesi OLMAMALI).")]
    [SerializeField] private GameObject panelRoot;
    [Tooltip("Banka core sayisini gosteren yazi.")]
    [SerializeField] private TMP_Text coresText;
    [Tooltip("Kart satirlarinin ekleneceği icerik (ScrollRect Content).")]
    [SerializeField] private Transform contentParent;
    [Tooltip("Tek kart satiri prefab'i (ShopItemUI).")]
    [SerializeField] private ShopItemUI itemPrefab;
    #endregion

    #region Private Fields
    private readonly List<ShopItemUI> _items = new List<ShopItemUI>();
    private bool _built;
    #endregion

    #region Unity Callbacks
    private void OnEnable()
    {
        MetaSave.OnCoresChanged += HandleCoresChanged;
    }
    private void OnDisable()
    {
        MetaSave.OnCoresChanged -= HandleCoresChanged;
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
    /// <summary>Katalogdaki her kart icin bir satir olusturur (bir kez).</summary>
    private void Build()
    {
        if (_built) return;
        if (itemPrefab == null || contentParent == null) { Debug.LogWarning("ShopUI: itemPrefab/contentParent atanmamis."); return; }

        var all = CardCatalog.All;
        for (int i = 0; i < all.Count; i++)
        {
            ShopItemUI item = Instantiate(itemPrefab, contentParent);
            item.gameObject.SetActive(true);
            _items.Add(item);
        }
        _built = true;
    }

    /// <summary>Tum satirlari ve core yazisini gunceller.</summary>
    private void RefreshAll()
    {
        var all = CardCatalog.All;
        int bank = MetaSave.BankedCores;
        for (int i = 0; i < _items.Count && i < all.Count; i++)
        {
            var card = all[i];
            _items[i].Bind(card, MetaSave.OwnedCount(card.id), bank >= card.cost, HandleBuy);
        }
        RefreshCores();
    }

    private void RefreshCores()
    {
        if (coresText != null) coresText.text = "Cores: " + MetaSave.BankedCores;
    }

    private void HandleBuy(string cardId)
    {
        var card = CardCatalog.Get(cardId);
        if (card == null) return;
        if (MetaSave.SpendCores(card.cost))
            MetaSave.AddOwned(cardId); // ayni kart defalarca alinabilir (stack)
        RefreshAll(); // satirlar (owned/afford) + core yenilenir
    }

    private void HandleCoresChanged(int newBalance)
    {
        // Baska bir kaynaktan ( or. run bankalama) core degisirse panel acikken guncelle
        if (panelRoot != null && panelRoot.activeInHierarchy) RefreshAll();
    }
    #endregion
}

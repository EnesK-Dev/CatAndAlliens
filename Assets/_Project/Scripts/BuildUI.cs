using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Build (loadout) ekrani — karakter donanim tarzi. SOL: kedi + kafasinda 3 SILAH slotu + altinda STAT grid'i (kutular).
/// SAG: sahip olunan kartlarin (silah+stat) listesi. Sagdan bir silaha basinca bos silah slotuna, bir stata basinca
/// stat grid kutusuna yerlesir. Doldurulmus slot/kutuya basinca cikar. Aktif deck (MetaSave) tek loadout olarak kullanilir:
/// silahlar (<=3) kafa slotlarina, statlar (<=statBoxCount) grid'e yansir. DeckApplier run basinda uygular.
/// </summary>
public class BuildUI : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField] private GameObject panelRoot;
    [Tooltip("Slot/kutu arka plani (card_background).")]
    [SerializeField] private Sprite slotBg;
    [Tooltip("Kafa cevresindeki 3 silah slotu (her biri: kok Button + child 'Icon' Image). Sahnede konumlanir.")]
    [SerializeField] private RectTransform[] weaponSlots;
    [Tooltip("Stat kutularinin uretilecegi scroll Content (GridLayoutGroup).")]
    [SerializeField] private Transform statGridContent;
    [Tooltip("Sag katalog listesi Content (owned kartlar buraya uretilir).")]
    [SerializeField] private Transform catalogContent;
    [Tooltip("Kac stat kutusu uretilecek (4x5 = 20).")]
    [SerializeField] private int statBoxCount = 20;
    [Tooltip("Katalog item kare boyutu (px).")]
    [SerializeField] private float catalogItemSize = 150f;
    #endregion

    #region Private Fields
    private const int WeaponCap = 3;
    private static readonly Color EmptyColor = new Color(1f, 1f, 1f, 0.30f);
    private static readonly Color FullColor = Color.white;
    private TMP_FontAsset _font;

    private readonly List<Image> _statIcons = new List<Image>();
    private readonly List<Button> _statButtons = new List<Button>();
    private bool _statBuilt;
    private readonly List<GameObject> _catalogItems = new List<GameObject>();
    #endregion

    #region Unity Callbacks
    private void OnEnable() { Rebuild(); }
    #endregion

    #region Public (buttons)
    public void Open()  { if (panelRoot != null) panelRoot.SetActive(true); Rebuild(); }
    public void Close() { if (panelRoot != null) panelRoot.SetActive(false); }
    #endregion

    #region Rebuild
    private void Rebuild()
    {
        if (_font == null) _font = FindFont();
        EnsureStatBoxes();

        int slot = MetaSave.ActiveSlot;
        var deck = MetaSave.GetActiveDeck(); // index'ler MetaSave slot listesiyle ayni sirada
        // deck icindeki silah/stat kart + gercek deck index'i
        var weapons = new List<KeyValuePair<int, CardDefinition>>();
        var stats = new List<KeyValuePair<int, CardDefinition>>();
        for (int i = 0; i < deck.Count; i++)
        {
            var c = CardCatalog.Get(deck[i]);
            if (c == null) continue;
            if (c.category == CardCategory.Weapon) weapons.Add(new KeyValuePair<int, CardDefinition>(i, c));
            else if (c.category == CardCategory.Stat) stats.Add(new KeyValuePair<int, CardDefinition>(i, c));
        }

        // 3 silah slotu
        if (weaponSlots != null)
        for (int s = 0; s < weaponSlots.Length; s++)
        {
            var slotRT = weaponSlots[s]; if (slotRT == null) continue;
            var icon = slotRT.Find("Icon")?.GetComponent<Image>();
            var btn = slotRT.GetComponent<Button>();
            bool filled = s < weapons.Count;
            if (icon != null)
            {
                icon.sprite = filled ? weapons[s].Value.icon : null;
                icon.enabled = filled && weapons[s].Value.icon != null;
                icon.color = FullColor;
            }
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                if (filled) { int di = weapons[s].Key; btn.onClick.AddListener(() => RemoveAt(di)); }
            }
        }

        // stat kutulari
        for (int i = 0; i < _statIcons.Count; i++)
        {
            bool filled = i < stats.Count;
            var ic = _statIcons[i];
            if (ic != null)
            {
                ic.sprite = filled ? stats[i].Value.icon : null;
                ic.enabled = filled && stats[i].Value.icon != null;
                ic.color = filled ? stats[i].Value.frameTint : FullColor;
            }
            var b = _statButtons[i];
            if (b != null)
            {
                b.onClick.RemoveAllListeners();
                if (filled) { int di = stats[i].Key; b.onClick.AddListener(() => RemoveAt(di)); }
            }
        }

        BuildCatalog(weapons.Count, stats.Count);
    }

    private void RemoveAt(int deckIndex)
    {
        MetaSave.RemoveCardFromSlotAt(MetaSave.ActiveSlot, deckIndex);
        SfxManager.Play(SfxId.ButtonClick);
        Rebuild();
    }

    private void BuildCatalog(int weaponsInDeck, int statsInDeck)
    {
        if (catalogContent == null) return;
        foreach (var go in _catalogItems) if (go != null) Destroy(go);
        _catalogItems.Clear();

        int slot = MetaSave.ActiveSlot;
        // sahip olunan kartlar: silahlar once, sonra statlar
        var owned = MetaSave.OwnedIds().Select(CardCatalog.Get)
                    .Where(c => c != null && (c.category == CardCategory.Weapon || c.category == CardCategory.Stat))
                    .OrderBy(c => c.category == CardCategory.Weapon ? 0 : 1).ToList();

        foreach (var card in owned)
        {
            int inDeck = MetaSave.CountInSlot(slot, card.id);
            int ownedCount = MetaSave.OwnedCount(card.id);
            bool canAdd;
            if (card.category == CardCategory.Weapon) canAdd = inDeck == 0 && weaponsInDeck < WeaponCap;
            else canAdd = inDeck < ownedCount && statsInDeck < statBoxCount;

            var item = NewCatalogItem(card, canAdd);
            var cardId = card.id; var cat = card.category;
            item.GetComponent<Button>().onClick.AddListener(() => TryAdd(cardId, cat));
            _catalogItems.Add(item);
        }
    }

    private void TryAdd(string id, CardCategory cat)
    {
        int slot = MetaSave.ActiveSlot;
        var deck = MetaSave.GetActiveDeck();
        int weaponsInDeck = deck.Count(x => { var c = CardCatalog.Get(x); return c != null && c.category == CardCategory.Weapon; });
        int statsInDeck = deck.Count(x => { var c = CardCatalog.Get(x); return c != null && c.category == CardCategory.Stat; });

        if (cat == CardCategory.Weapon)
        {
            if (weaponsInDeck >= WeaponCap) return;
            if (MetaSave.CountInSlot(slot, id) > 0) return; // silah tek
        }
        else
        {
            if (statsInDeck >= statBoxCount) return;
            if (MetaSave.CountInSlot(slot, id) >= MetaSave.OwnedCount(id)) return;
        }
        if (MetaSave.AddCardToSlot(slot, id)) { SfxManager.Play(SfxId.ButtonClick); Rebuild(); }
    }
    #endregion

    #region UI generation
    private void EnsureStatBoxes()
    {
        if (_statBuilt || statGridContent == null) return;
        for (int i = 0; i < statBoxCount; i++)
        {
            var box = NewBox(statGridContent, "StatBox" + i, out Image icon);
            _statIcons.Add(icon);
            _statButtons.Add(box.GetComponent<Button>());
        }
        _statBuilt = true;
    }

    /// <summary>Bir slot/kutu: kok Image(slotBg) + Button + child "Icon" Image.</summary>
    private RectTransform NewBox(Transform parent, string name, out Image icon)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false);
        var bg = go.GetComponent<Image>(); bg.sprite = slotBg; bg.type = Image.Type.Sliced; bg.color = Color.white;
        var icGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        var icrt = icGO.GetComponent<RectTransform>(); icrt.SetParent(rt, false);
        icrt.anchorMin = new Vector2(0.12f, 0.12f); icrt.anchorMax = new Vector2(0.88f, 0.88f);
        icrt.offsetMin = Vector2.zero; icrt.offsetMax = Vector2.zero;
        icon = icGO.GetComponent<Image>(); icon.preserveAspect = true; icon.enabled = false; icon.raycastTarget = false;
        return rt;
    }

    private GameObject NewCatalogItem(CardDefinition card, bool canAdd)
    {
        var go = new GameObject("Cat_" + card.id, typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = go.GetComponent<RectTransform>(); rt.SetParent(catalogContent, false);
        var bg = go.GetComponent<Image>(); bg.sprite = slotBg; bg.type = Image.Type.Sliced;
        bg.color = canAdd ? Color.white : new Color(1f, 1f, 1f, 0.4f);
        var le = go.AddComponent<LayoutElement>(); le.preferredWidth = catalogItemSize; le.preferredHeight = catalogItemSize; le.minWidth = catalogItemSize; le.minHeight = catalogItemSize;
        // icon
        var icGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        var icrt = icGO.GetComponent<RectTransform>(); icrt.SetParent(rt, false);
        icrt.anchorMin = new Vector2(0.14f, 0.24f); icrt.anchorMax = new Vector2(0.86f, 0.92f);
        icrt.offsetMin = Vector2.zero; icrt.offsetMax = Vector2.zero;
        var ic = icGO.GetComponent<Image>(); ic.sprite = card.icon; ic.enabled = card.icon != null; ic.preserveAspect = true; ic.raycastTarget = false;
        ic.color = card.category == CardCategory.Weapon ? Color.white : card.frameTint;
        // name
        var nGO = new GameObject("Name", typeof(RectTransform));
        var nrt = nGO.GetComponent<RectTransform>(); nrt.SetParent(rt, false);
        nrt.anchorMin = new Vector2(0.04f, 0.02f); nrt.anchorMax = new Vector2(0.96f, 0.24f);
        nrt.offsetMin = Vector2.zero; nrt.offsetMax = Vector2.zero;
        var t = nGO.AddComponent<TextMeshProUGUI>(); if (_font != null) t.font = _font;
        t.text = card.displayName; t.fontSize = 26; t.alignment = TextAlignmentOptions.Center; t.enableAutoSizing = true; t.fontSizeMin = 14; t.fontSizeMax = 28; t.color = Color.white; t.raycastTarget = false;
        return go;
    }

    private TMP_FontAsset FindFont()
    {
        var cv = GetComponentInParent<Canvas>();
        if (cv != null) foreach (var tt in cv.GetComponentsInChildren<TMP_Text>(true)) if (tt.font != null) return tt.font;
        return null;
    }
    #endregion
}

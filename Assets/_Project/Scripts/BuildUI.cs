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
    [Tooltip("Tum paneli kaydiran tek ScrollRect (MainScroll). Panel her acildiginda en uste doner.")]
    [SerializeField] private ScrollRect mainScroll;
    [Tooltip("Orta kolon: silah level onizleme paneli icerigi (VerticalLayoutGroup). Kusanilan statlara gore silah leveleri.")]
    [SerializeField] private Transform weaponPreviewContent;
    [Tooltip("Kac stat kutusu uretilecek (4x5 = 20).")]
    [SerializeField] private int statBoxCount = 20;
    [Tooltip("Katalog item kare boyutu (px).")]
    [SerializeField] private float catalogItemSize = 150f;
    #endregion

    #region Private Fields
    private const int WeaponCap = 3;
    private static readonly Color EmptyColor = new Color(1f, 1f, 1f, 0.30f);
    private static readonly Color FullColor = Color.white;
    private static readonly Color ComboSlotColor = new Color(1f, 0.55f, 0.12f, 1f); // ortadaki COMBO kutusu turuncu
    private TMP_FontAsset _font;

    private const float CardAspect = 0.5417f; // 623/1150 kart en/boy orani (cover-fit icin)
    private readonly List<Image> _statIcons = new List<Image>();
    private readonly List<Image> _statFrames = new List<Image>();
    private Sprite _defaultWeaponFrame; // bos silah slotlari icin kart deseni
    private readonly List<Button> _statButtons = new List<Button>();
    private bool _statBuilt;
    private readonly List<GameObject> _catalogItems = new List<GameObject>();
    #endregion

    #region Unity Callbacks
    private void OnEnable() { Rebuild(true); }
    #endregion

    #region Public (buttons)
    public void Open()  { if (panelRoot != null) panelRoot.SetActive(true); Rebuild(true); }
    public void Close() { if (panelRoot != null) panelRoot.SetActive(false); }
    #endregion

    #region Rebuild
    private void Rebuild(bool resetScroll = false)
    {
        if (_font == null) _font = FindFont();
        EnsureStatBoxes();

        int slot = MetaSave.ActiveSlot;
        var deck = MetaSave.GetActiveDeck(); // index'ler MetaSave slot listesiyle ayni sirada
        // deck'te artik sadece STAT kartlari (silahlar sabit weaponSlots'ta tutulur).
        var stats = new List<KeyValuePair<int, CardDefinition>>();
        for (int i = 0; i < deck.Count; i++)
        {
            var c = CardCatalog.Get(deck[i]);
            if (c != null && c.category == CardCategory.Stat) stats.Add(new KeyValuePair<int, CardDefinition>(i, c));
        }
        // Ayni statlar yan yana gorunsun: id'ye gore grupla. Key = gercek deck index oldugu icin silme dogru kalir.
        stats.Sort((a, b) => string.CompareOrdinal(a.Value.id, b.Value.id));

        // Silah slotlari SABIT: her UI slotu bir data index'ine esli (orta=1=COMBO, sol=0, sag=2).
        // Silah cikarilinca SADECE kendi slotu bosalir; digerleri KAYMAZ.
        if (weaponSlots != null && weaponSlots.Length > 0)
        {
            var wslots = MetaSave.GetWeaponSlots(slot);
            int centerUI = 0; float bestY = float.NegativeInfinity;
            for (int s = 0; s < weaponSlots.Length; s++)
                if (weaponSlots[s] != null && weaponSlots[s].anchoredPosition.y > bestY) { bestY = weaponSlots[s].anchoredPosition.y; centerUI = s; }
            int leftUI = -1, rightUI = -1;
            for (int s = 0; s < weaponSlots.Length; s++) { if (s == centerUI || weaponSlots[s] == null) continue; if (leftUI < 0) leftUI = s; else rightUI = s; }
            if (leftUI >= 0 && rightUI >= 0 && weaponSlots[leftUI].anchoredPosition.x > weaponSlots[rightUI].anchoredPosition.x) { int t = leftUI; leftUI = rightUI; rightUI = t; }

            for (int s = 0; s < weaponSlots.Length; s++)
            {
                var slotRT = weaponSlots[s]; if (slotRT == null) continue;
                int dataIdx = (s == centerUI) ? 1 : (s == leftUI ? 0 : 2);
                bool isCombo = (s == centerUI);
                string wid = (dataIdx >= 0 && dataIdx < wslots.Length) ? wslots[dataIdx] : "";
                var wcard = string.IsNullOrEmpty(wid) ? null : CardCatalog.Get(wid);
                bool filled = wcard != null;

                var bg = slotRT.GetComponent<Image>();
                if (bg != null) bg.color = new Color(0f, 0f, 0f, 0f); // arkadaki card_background kaldirildi (seffaf)
                EnsureComboLabel(slotRT, isCombo);
                var wfr = EnsureBoxFrame(slotRT);
                if (wfr != null)
                {
                    if (filled && wcard.frameSprite != null)
                    {
                        wfr.enabled = true;
                        wfr.sprite = wcard.frameSprite;
                        wfr.color = isCombo ? ComboSlotColor : wcard.frameTint;
                    }
                    else
                    {
                        // Silah YOKKEN bile kart sekli gorunsun (ikonsuz). Combo slotu turuncu, digerleri soluk.
                        var def = DefaultWeaponFrame();
                        wfr.enabled = def != null;
                        wfr.sprite = def;
                        wfr.color = isCombo ? ComboSlotColor : new Color(1f, 1f, 1f, 0.5f);
                    }
                }

                var icon = slotRT.Find("Icon")?.GetComponent<Image>();
                if (icon != null)
                {
                    icon.sprite = filled ? wcard.icon : null;
                    icon.enabled = filled && wcard.icon != null;
                    icon.color = FullColor;
                }
                var btn = slotRT.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    if (filled) { int di = dataIdx; btn.onClick.AddListener(() => RemoveWeapon(di)); }
                }
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
            var frm = i < _statFrames.Count ? _statFrames[i] : null;
            if (frm != null)
            {
                bool hasFrame = filled && stats[i].Value.frameSprite != null;
                frm.enabled = hasFrame; // bos kutu -> slotBg gorunur; dolu -> kart deseni cover-fit
                if (hasFrame) { frm.sprite = stats[i].Value.frameSprite; frm.color = stats[i].Value.frameTint; }
            }
            var b = _statButtons[i];
            if (b != null)
            {
                b.onClick.RemoveAllListeners();
                if (filled) { int di = stats[i].Key; b.onClick.AddListener(() => RemoveAt(di)); }
            }
        }

        BuildCatalog(stats.Count);
        BuildWeaponPreview();

        // Scroll Content yuksekligini en alttaki bolume (stat grid / katalog) gore buyut ki fazla stat alinca alt kutular kesilmesin.
        FitScrollContent();

        // Scroll'u SADECE panel acilinca en uste al (stat/silah ekle-cikar sirasinda konumu KORU).
        if (resetScroll && mainScroll != null) { Canvas.ForceUpdateCanvases(); mainScroll.verticalNormalizedPosition = 1f; }
    }

    private const float ScrollContentBottomPadding = 90f; // en alt kutunun altinda nefes payi
    private const float ScrollContentMinHeight = 1450f;   // taban (kisa icerikte kuculmesin)

    /// <summary>Scroll Content yuksekligini en alta uzanan bolume gore ayarlar. Stat grid / katalog CSF ile
    /// buyuyunce Content SABIT kaldigi icin alt kutular scroll disinda kaliyordu; burada dinamik buyutuluyor.</summary>
    private void FitScrollContent()
    {
        if (mainScroll == null || mainScroll.content == null) return;
        Canvas.ForceUpdateCanvases(); // CSF'ler guncel yukseklik versin
        LayoutRebuilder.ForceRebuildLayoutImmediate(mainScroll.content);

        float needed = ScrollContentMinHeight;
        needed = Mathf.Max(needed, SectionBottom(statGridContent as RectTransform));
        needed = Mathf.Max(needed, SectionBottom(catalogContent as RectTransform));
        needed += ScrollContentBottomPadding;

        var c = mainScroll.content;
        if (Mathf.Abs(c.sizeDelta.y - needed) > 1f)
            c.sizeDelta = new Vector2(c.sizeDelta.x, needed);
    }

    /// <summary>Bir bolumun Content ustunden alt kenarina mesafesi. Varsayim: bolum ust-pivotlu ve Content ustune ankajli
    /// (aPos.y negatif = asagi). statGrid/catalog ikisi de oyle. Boylece en dusuk kenari bulup Content'i ona gore buyuturuz.</summary>
    private float SectionBottom(RectTransform rt)
    {
        if (rt == null) return 0f;
        return -rt.anchoredPosition.y + rt.rect.height;
    }

    private static readonly Color PrevPipFull = new Color(1f, 0.82f, 0.30f, 1f);
    private static readonly Color PrevPipEmpty = new Color(1f, 1f, 1f, 0.18f);

    /// <summary>Orta kolon: kusanilan her silah icin, kusanilan statlara gore track level onizlemesi (pip'li, oyun ici mantik).</summary>
    private void BuildWeaponPreview()
    {
        if (weaponPreviewContent == null) return;
        for (int i = weaponPreviewContent.childCount - 1; i >= 0; i--) Destroy(weaponPreviewContent.GetChild(i).gameObject);

        int slot = MetaSave.ActiveSlot;
        var deck = MetaSave.GetActiveDeck();
        int might=0,haste=0,area=0,amount=0,charge=0,firepower=0,impact=0;
        foreach (var id in deck)
        {
            if (id=="meta_might") might++;
            else if (id=="meta_haste") haste++;
            else if (id=="meta_area") area++;
            else if (id=="meta_amount") amount++;
            else if (id=="meta_charge") charge++;
            else if (id=="meta_firepower") firepower++;
            else if (id=="meta_impact") impact++;
        }

        var ws = MetaSave.GetWeaponSlots(slot);
        bool any = false;
        foreach (var wid in ws)
        {
            if (string.IsNullOrEmpty(wid)) continue;
            var card = CardCatalog.Get(wid);
            if (card == null) continue;
            any = true;
            PreviewHeader(card.displayName);
            switch (card.weaponId)
            {
                case "claw": PreviewTrack("Damage","Might",might); break;
                case "orbital":
                    PreviewTrack("Damage","Charge",charge); PreviewTrack("Speed","Haste",haste);
                    PreviewTrack("Orbs","Amount",amount); PreviewTrack("Size","Area",area); break;
                case "boomerang":
                    PreviewTrack("Damage","Impact",impact); PreviewTrack("Speed","Haste",haste);
                    PreviewTrack("Boomerangs","Amount",amount); break;
                case "blaster":
                    PreviewTrack("Damage","Firepower",firepower); PreviewTrack("Fire Rate","Haste",haste);
                    PreviewTrack("Targets","Amount",amount); break;
                case "multislash":
                    PreviewTrack("Damage","Might",might); PreviewTrack("Slashes","Amount",amount); break;
            }
        }
        if (!any) PreviewHeader("No weapon equipped");
    }

    /// <summary>Silah adi basligi (beyaz, kalin, buyuk).</summary>
    private void PreviewHeader(string name)
    {
        var go = new GameObject("PrevHead", typeof(RectTransform));
        go.transform.SetParent(weaponPreviewContent, false);
        var t = go.AddComponent<TextMeshProUGUI>(); if (_font != null) t.font = _font;
        t.text = name; t.fontSize = 48f; t.color = new Color32(255, 158, 0, 255); t.fontStyle = FontStyles.Bold; // BUILD turuncusu
        t.alignment = TextAlignmentOptions.Left; t.raycastTarget = false;
        t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow;
        var le = go.AddComponent<LayoutElement>(); le.minHeight = 62f; le.preferredHeight = 62f;
    }

    /// <summary>Bir track satiri: etiket + pip'ler. Level/progress OYUN ICI mantikla (esik 3->4->5) n stattan hesaplanir.</summary>
    private void PreviewTrack(string effect, string stat, int n)
    {
        int level, prog, pipsShown;
        if (stat == "Amount") { level = n; prog = 0; pipsShown = 0; } // Amount: nadir -> aninda level, kutu yok
        else
        {
            level = 0; int cost = 3; prog = 0;
            for (int i = 0; i < n; i++) { prog++; if (prog >= cost) { prog -= cost; level++; cost++; } }
            pipsShown = Mathf.Min(cost, 6);
        }

        var row = new GameObject("PrevTrack", typeof(RectTransform));
        row.transform.SetParent(weaponPreviewContent, false);
        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10f; hlg.childAlignment = TextAnchor.MiddleLeft; hlg.padding = new RectOffset(26,0,0,0);
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        var rLE = row.AddComponent<LayoutElement>(); rLE.minHeight = 50f; rLE.preferredHeight = 50f;

        var lblGO = new GameObject("Lbl", typeof(RectTransform));
        lblGO.transform.SetParent(row.transform, false);
        var lt = lblGO.AddComponent<TextMeshProUGUI>(); if (_font != null) lt.font = _font;
        lt.text = stat + "  <color=#B9B9C9>Lv." + level + "</color>";
        lt.fontSize = 34f; lt.color = Color.white; lt.alignment = TextAlignmentOptions.Left; lt.raycastTarget = false;
        lt.enableWordWrapping = false; lt.overflowMode = TextOverflowModes.Overflow;
        var lLE = lblGO.AddComponent<LayoutElement>(); lLE.flexibleWidth = 1f;

        var pipsGO = new GameObject("Pips", typeof(RectTransform));
        pipsGO.transform.SetParent(row.transform, false);
        var ph = pipsGO.AddComponent<HorizontalLayoutGroup>();
        ph.spacing = 6f; ph.childAlignment = TextAnchor.MiddleRight;
        ph.childControlWidth = true; ph.childControlHeight = true;
        ph.childForceExpandWidth = false; ph.childForceExpandHeight = false;
        for (int i = 0; i < pipsShown; i++)
        {
            var pip = new GameObject("Pip", typeof(RectTransform));
            pip.transform.SetParent(pipsGO.transform, false);
            var img = pip.AddComponent<Image>();
            img.color = i < prog ? PrevPipFull : PrevPipEmpty; img.raycastTarget = false;
            var ple = pip.AddComponent<LayoutElement>(); ple.preferredWidth = 26; ple.preferredHeight = 26; ple.minWidth = 26; ple.minHeight = 26;
        }
    }

    private void RemoveAt(int deckIndex)
    {
        MetaSave.RemoveCardFromSlotAt(MetaSave.ActiveSlot, deckIndex);
        SfxManager.Play(SfxId.ButtonClick);
        Rebuild();
    }

    /// <summary>Bir silah slotunu bosaltir (sadece o slot; digerleri KAYMAZ).</summary>
    private void RemoveWeapon(int dataIndex)
    {
        MetaSave.RemoveWeaponSlot(MetaSave.ActiveSlot, dataIndex);
        SfxManager.Play(SfxId.ButtonClick);
        Rebuild();
    }

    /// <summary>Combo slotunun (turuncu orta kutu) USTUNE "COMBO" etiketini bir kez uretir; isCombo=false ise gizler.</summary>
    private void EnsureComboLabel(RectTransform slot, bool show)
    {
        var existing = slot.Find("ComboLabel");
        if (!show) { if (existing != null) existing.gameObject.SetActive(false); return; }
        TextMeshProUGUI t;
        if (existing == null)
        {
            var go = new GameObject("ComboLabel", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>(); rt.SetParent(slot, false);
            // Kutunun hemen USTUNE otur (kirpilmemesi icin cok yukari tasirma): ust kenarin biraz uzeri
            rt.anchorMin = new Vector2(-0.2f, 1f); rt.anchorMax = new Vector2(1.2f, 1f); rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 4f); rt.sizeDelta = new Vector2(0f, 40f);
            t = go.AddComponent<TextMeshProUGUI>();
            t.text = "COMBO"; t.alignment = TextAlignmentOptions.Center;
            t.enableAutoSizing = true; t.fontSizeMin = 16; t.fontSizeMax = 40;
            t.color = new Color32(255, 158, 0, 255); t.raycastTarget = false; // parlak sari (koyu uzay arka plani uzerinde okunur)
        }
        else { existing.gameObject.SetActive(true); t = existing.GetComponent<TextMeshProUGUI>(); }
        if (t != null)
        {
            if (t.font == null) t.font = _font != null ? _font : TMP_Settings.defaultFontAsset; // font garanti
            t.transform.SetAsLastSibling(); // ikon/kutunun USTUNDE cizilsin
        }
    }

    private void BuildCatalog(int statsInDeck)
    {
        if (catalogContent == null) return;
        foreach (var go in _catalogItems) if (go != null) Destroy(go);
        _catalogItems.Clear();

        int slot = MetaSave.ActiveSlot;
        int weaponsInSlots = MetaSave.WeaponSlotFilledCount(slot);
        // sahip olunan kartlar: silahlar once, sonra statlar
        var owned = MetaSave.OwnedIds().Select(CardCatalog.Get)
                    .Where(c => c != null && (c.category == CardCategory.Weapon || c.category == CardCategory.Stat))
                    .OrderBy(c => c.category == CardCategory.Weapon ? 0 : 1).ToList();

        foreach (var card in owned)
        {
            int ownedCount = MetaSave.OwnedCount(card.id);
            bool canAdd; int displayCount = ownedCount;
            if (card.category == CardCategory.Weapon)
                canAdd = !MetaSave.HasWeaponInSlots(slot, card.id) && weaponsInSlots < WeaponCap;
            else
            {
                int inDeck = MetaSave.CountInSlot(slot, card.id);
                canAdd = inDeck < ownedCount && statsInDeck < statBoxCount;
                displayCount = Mathf.Max(0, ownedCount - inDeck); // KALAN adet — kullanildikca azalir
            }

            var item = NewCatalogItem(card, canAdd, displayCount);
            var cardId = card.id; var cat = card.category;
            item.GetComponent<Button>().onClick.AddListener(() => TryAdd(cardId, cat));
            _catalogItems.Add(item);
        }
    }

    private void TryAdd(string id, CardCategory cat)
    {
        int slot = MetaSave.ActiveSlot;
        if (cat == CardCategory.Weapon)
        {
            // Silah: ilk bos slota (center-first). Zaten varsa/dolu ise AddWeaponAuto false doner.
            if (MetaSave.AddWeaponAuto(slot, id)) { SfxManager.Play(SfxId.ButtonClick); Rebuild(); }
            return;
        }
        var deck = MetaSave.GetActiveDeck();
        int statsInDeck = deck.Count(x => { var c = CardCatalog.Get(x); return c != null && c.category == CardCategory.Stat; });
        if (statsInDeck >= statBoxCount) return;
        if (MetaSave.CountInSlot(slot, id) >= MetaSave.OwnedCount(id)) return;
        if (MetaSave.AddCardToSlot(slot, id)) { SfxManager.Play(SfxId.ButtonClick); Rebuild(); }
    }
    #endregion

    #region UI generation
    /// <summary>Bos silah slotlari icin varsayilan silah kart cercevesi (katalogdaki ilk silah kartindan).</summary>
    private Sprite DefaultWeaponFrame()
    {
        if (_defaultWeaponFrame == null)
            foreach (var c in CardCatalog.All)
                if (c != null && c.category == CardCategory.Weapon && c.frameSprite != null) { _defaultWeaponFrame = c.frameSprite; break; }
        return _defaultWeaponFrame;
    }

    /// <summary>Bir kutuya kart deseni katmani ekler: RectMask2D + cover-fit (EnvelopeParent) "Frame" Image. Idempotent.</summary>
    private Image EnsureBoxFrame(RectTransform box)
    {
        if (box == null) return null;
        var fr = box.Find("Frame") as RectTransform;
        Image img;
        if (fr == null)
        {
            var go = new GameObject("Frame", typeof(RectTransform), typeof(Image));
            fr = go.GetComponent<RectTransform>(); fr.SetParent(box, false);
            img = go.GetComponent<Image>(); img.type = Image.Type.Simple; img.preserveAspect = false; img.raycastTarget = false;
        }
        else
        {
            img = fr.GetComponent<Image>();
            var arfOld = fr.GetComponent<AspectRatioFitter>(); if (arfOld != null) arfOld.enabled = false; // eski cover/fit'i kapat
        }
        // Kutuyu TAM doldur (kare form) — esneyerek kutu boyutuna gelir
        fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = Vector2.zero; fr.offsetMax = Vector2.zero;
        fr.SetAsFirstSibling(); // ikonun/COMBO yazisinin ALTINDA
        return img;
    }

    private void EnsureStatBoxes()
    {
        if (_statBuilt || statGridContent == null) return;
        for (int i = 0; i < statBoxCount; i++)
        {
            var box = NewBox(statGridContent, "StatBox" + i, out Image icon);
            _statIcons.Add(icon);
            _statButtons.Add(box.GetComponent<Button>());
            _statFrames.Add(EnsureBoxFrame(box));
        }
        _statBuilt = true;
    }

    /// <summary>Bir slot/kutu: kok Image(slotBg) + Button + child "Icon" Image.</summary>
    private RectTransform NewBox(Transform parent, string name, out Image icon)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false);
        var bg = go.GetComponent<Image>(); bg.sprite = null; bg.color = new Color(0f, 0f, 0f, 0f); bg.raycastTarget = true; // arkadaki kare kaldirildi (seffaf tiklama hedefi)
        var icGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        var icrt = icGO.GetComponent<RectTransform>(); icrt.SetParent(rt, false);
        icrt.anchorMin = new Vector2(0.12f, 0.12f); icrt.anchorMax = new Vector2(0.88f, 0.88f);
        icrt.offsetMin = Vector2.zero; icrt.offsetMax = Vector2.zero;
        icon = icGO.GetComponent<Image>(); icon.preserveAspect = true; icon.enabled = false; icon.raycastTarget = false;
        return rt;
    }

    private GameObject NewCatalogItem(CardDefinition card, bool canAdd, int count)
    {
        var go = new GameObject("Cat_" + card.id, typeof(RectTransform), typeof(Image), typeof(Button), typeof(CanvasGroup));
        var rt = go.GetComponent<RectTransform>(); rt.SetParent(catalogContent, false);
        // Shop karti gibi: kare slot degil, kart cercevesi (frameSprite) + kart rengi (frameTint). Aciklama yok.
        var bg = go.GetComponent<Image>();
        bg.sprite = card.frameSprite != null ? card.frameSprite : slotBg;
        bg.type = Image.Type.Simple; bg.preserveAspect = true;
        bg.color = card.frameTint;
        // Tukenmis/eklenemez kart: tum karti soluklastir + tiklanamaz yap (inactive hissi).
        var cg = go.GetComponent<CanvasGroup>();
        cg.alpha = canAdd ? 1f : 0.28f;
        cg.interactable = canAdd;
        var le = go.AddComponent<LayoutElement>(); le.preferredWidth = catalogItemSize; le.preferredHeight = catalogItemSize; le.minWidth = catalogItemSize; le.minHeight = catalogItemSize;
        // icon (kart ust yarisi) — beyaz ikon kart rengiyle boyanir
        var icGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        var icrt = icGO.GetComponent<RectTransform>(); icrt.SetParent(rt, false);
        icrt.anchorMin = new Vector2(0.24f, 0.50f); icrt.anchorMax = new Vector2(0.76f, 0.90f);
        icrt.offsetMin = Vector2.zero; icrt.offsetMax = Vector2.zero;
        var ic = icGO.GetComponent<Image>(); ic.sprite = card.icon; ic.enabled = card.icon != null; ic.preserveAspect = true; ic.raycastTarget = false;
        ic.color = card.frameTint;
        // sahip olunan adet — kart ORTASINDA "xN" (statlar icin)
        if (card.category == CardCategory.Stat)
        {
            var cGO = new GameObject("Count", typeof(RectTransform));
            var crt = cGO.GetComponent<RectTransform>(); crt.SetParent(rt, false);
            crt.anchorMin = new Vector2(0.10f, 0.28f); crt.anchorMax = new Vector2(0.90f, 0.48f);
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
            var ct = cGO.AddComponent<TextMeshProUGUI>(); if (_font != null) ct.font = _font;
            ct.text = "x" + count; ct.alignment = TextAlignmentOptions.Center; ct.fontStyle = FontStyles.Normal;
            ct.enableAutoSizing = true; ct.fontSizeMin = 14; ct.fontSizeMax = 28; ct.color = Color.white; ct.raycastTarget = false;
        }
        // name (kart altinda)
        var nGO = new GameObject("Name", typeof(RectTransform));
        var nrt = nGO.GetComponent<RectTransform>(); nrt.SetParent(rt, false);
        nrt.anchorMin = new Vector2(0.08f, 0.08f); nrt.anchorMax = new Vector2(0.92f, 0.30f);
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

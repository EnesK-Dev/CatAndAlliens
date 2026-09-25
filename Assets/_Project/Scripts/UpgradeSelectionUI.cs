using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Upgrade paneli. CoreManager.OnThresholdReached'i dinler; esige ulasinca oyunu duraklatir
/// (timeScale=0 + player.SetPaused) ve DINAMIK bir secenek havuzundan 3 kart gosterir:
/// stat upgrade'leri + silah ALMA ("NEW WEAPON") + silah YUKSELTME (farkli renk). Secilince
/// ilgili stat'i uygular ya da silahi alir/yukseltir, paneli kapatir. Gevsek bagli (static event).
/// </summary>
public class UpgradeSelectionUI : MonoBehaviour
{
    #region Nested Types
    /// <summary>Uygulanacak stat turu. player'daki upgrade API'sine denk gelir.</summary>
    public enum UpgradeType
    {
        Damage,       // Might     -> SADECE Sharp Claws hasari (tracker). Global degil.
        AttackSpeed,  // Haste     -> RunStats.CooldownMult (tum atis/vurus hizi, ORTAK)
        AttackRange,  // Area      -> RunStats.AreaMult (menzil/yaricap/orbit, ORTAK)
        GainHealth,   // Max Health-> player.AddMaxHealth. NOT: 4. sirada kalmali (sahne serialize int).
        Amount,       // +1 mermi/orb/yon/hedef -> RunStats.AmountBonus (ORTAK)
        Charge,       // -> SADECE Orbital hasari (tracker)
        Firepower,    // -> SADECE Auto-Blaster hasari (tracker)
        Impact        // -> SADECE Boomerang hasari (tracker)
    }

    /// <summary>Bir stat upgrade seceneginin verisi — Inspector'dan doldurulur (denge burada tutulur).</summary>
    [Serializable]
    public class UpgradeDefinition
    {
        public string title = "Upgrade";
        [TextArea] public string description = "";
        public Sprite icon;
        public UpgradeType type;

        [Tooltip("Damage: +hasar | AttackSpeed: cooldown carpani (0.85 = %15 hizli) | " +
                 "AttackRange: +menzil | GainHealth: anlik +can (max can degismez)")]
        public float amount = 1f;

        [Tooltip("Kartlarda cikma agirligi. 1 = normal, <1 = daha nadir (ornek Amount 0.35).")]
        public float weight = 1f;

        [HideInInspector] public int level; // kac kez secildi (runtime stack sayaci)
    }

    /// <summary>Panelde gosterilen tek bir kart secenegi (stat ya da silah). Runtime'da uretilir.</summary>
    private struct CardOption
    {
        public bool isWeapon;
        public int statIndex;             // stat ise upgrades[] indeksi
        public WeaponBase weapon;         // silah ise
        public bool isNewWeapon;          // silah henuz alinmadi (NEW WEAPON)
        public WeaponUpgradeOption upgrade; // silah yukseltmesi ise (isWeapon && !isNewWeapon)
    }
    #endregion

    #region Serialized Fields
    [Header("Referanslar")]
    [Tooltip("Panelin kok GameObject'i — acilip kapanacak (bu script'in objesi OLMAMALI).")]
    [SerializeField] private GameObject panelRoot;

    [Tooltip("Sol canli stat paneli — SADECE upgrade paneli acikken gorunur. Bos ise atlanir.")]
    [SerializeField] private GameObject liveStatsPanel;

    [Tooltip("Sahnedeki sabit kart slotlari (artik 2 tane, yan yana dikey kart).")]
    [SerializeField] private UpgradeCard[] cardSlots;

    [Header("Reroll")]
    [Tooltip("Kartlari yeniden dagitan buton (beyaz/siyah). Bos ise reroll kapali.")]
    [SerializeField] private Button rerollButton;
    [Tooltip("Reroll butonundaki yazi (kalan hak gosterilir). Bos ise atlanir.")]
    [SerializeField] private TMP_Text rerollLabel;
    [Tooltip("Panel her acildiginda kac reroll hakki. 2 = 2x reroll.")]
    [SerializeField] private int rerollsPerOpen = 2;

    [Tooltip("Bos birakilirsa Awake'te otomatik bulunur.")]
    [SerializeField] private player playerRef;

    [Tooltip("Bos birakilirsa player'dan otomatik bulunur. Silah kartlari icin.")]
    [SerializeField] private WeaponManager weaponManager;

    [Header("Kart Sonrasi")]
    [Tooltip("Kart secilip panel kapaninca dusmanlar bu kadar SANIYE donar (oyuncu serbest kalir).")]
    [SerializeField] private float enemyFreezeAfterUpgrade = 0.6f;

    [Header("Stat Upgrade Havuzu")]
    [SerializeField] private UpgradeDefinition[] upgrades;

    [Header("Kart Cerceve Sprite'lari (kategoriye gore renkli kart)")]
    [SerializeField] private Sprite damageCardSprite;      // sari
    [SerializeField] private Sprite attackSpeedCardSprite; // mavi
    [SerializeField] private Sprite attackRangeCardSprite; // yesil
    [SerializeField] private Sprite healthCardSprite;      // kirmizi
    [SerializeField] private Sprite weaponCardSprite;      // mor
    [SerializeField] private Sprite greyCardSprite;        // gri cerceve (yeni statlar: gri + tint)
    [SerializeField] private Sprite amountCardSprite;      // Amount icin rengarenk gradient cerceve (guclu his)

    [Tooltip("Renkli sprite'lar zaten renkli oldugu icin frame tint beyaz kalir.")]
    [SerializeField] private Color cardTint = Color.white;

    [Tooltip("Silah karti IKONLARINA uygulanan tint (kartla uyumlu mor). Stat ikonlari beyaz kalir.")]
    [SerializeField] private Color weaponIconColor = new Color(0.59f, 0.39f, 0.86f, 1f);

    [Header("Ikon Boyutu (kart icindeki ikonun yuksekligi, px)")]
    [Tooltip("Silah karti ikonunun yuksekligi. 0 = kart prefab'inin varsayilani (175). Buyutmek icin arttir.")]
    [SerializeField] private float weaponIconHeight = 260f;

    [Tooltip("Stat karti ikonunun yuksekligi. 0 = kart prefab'inin varsayilani (175).")]
    [SerializeField] private float statIconHeight = 0f;
    #endregion

    #region Private Fields
    private static UpgradeSelectionUI _instance;
    private int _pendingSelections;
    private int _rerollsLeft;
    private bool _isOpen;
    private Action<int> _cardCallback;

    private readonly List<CardOption> _available = new List<CardOption>(); // her acilista yeniden doldurulur
    private readonly List<WeaponUpgradeOption> _weaponUpgradeBuffer = new List<WeaponUpgradeOption>();
    private CardOption[] _shown;                                           // o an gosterilen kartlar (slot sirasi)
    private readonly List<CardOption> _picked = new List<CardOption>();    // agirlikli secilen (gosterilecek) kartlar
    private readonly List<CardOption> _weightPool = new List<CardOption>(); // agirlikli secim calisma listesi

    // --- Secim animasyonu (deneysel): secilmeyenler yukari kayip solar; secilen kucule kucule kedinin ARKASINA gider ---
    private const float SelectAnimDuration = 0.38f;      // animasyon suresi (unscaled)
    private const float UnselectedRiseDistance = 2400f;  // secilmeyen kartlar ekranin USTUNDEN tamamen cikacak kadar
    private bool _animating;                              // animasyon sirasinda ikinci secim engellenir
    private Canvas _flyCanvas;                            // secilen kart icin kamera-uzayi canvas (kedinin arkasi)
    private RectTransform _flyCanvasRT;
    private RectTransform _cardRow;                       // kartlarin normal ebeveyni (layout grubu)
    private HorizontalLayoutGroup _cardRowLayout;
    private Vector2[] _origAnchorMin, _origAnchorMax, _origPivot; // reset icin kart anchor/pivot yedegi
    #endregion

    #region Static API
    /// <summary>Bir STAT upgrade secilince firlar. Parametre: secilen stat turu. (Silahlar bunu firlatmaz.)</summary>
    public static event Action<UpgradeType> OnUpgradeSelected;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        _cardCallback = HandleCardSelected;
        if (rerollButton != null) rerollButton.onClick.AddListener(HandleReroll);

        if (playerRef == null) playerRef = FindFirstObjectByType<player>();
        if (weaponManager == null && playerRef != null) weaponManager = playerRef.GetComponent<WeaponManager>();

        _shown = new CardOption[cardSlots != null ? cardSlots.Length : 0];
        CaptureCardOriginals();

        if (panelRoot != null) panelRoot.SetActive(false);

        CoreManager.OnThresholdReached += HandleThresholdReached;
    }

    private void OnDestroy()
    {
        CoreManager.OnThresholdReached -= HandleThresholdReached;
        if (rerollButton != null) rerollButton.onClick.RemoveListener(HandleReroll);
        if (_instance == this) _instance = null;
    }
    #endregion

    #region Private Methods
    private void HandleThresholdReached(int thresholdLevel)
    {
        if (_isOpen) { _pendingSelections++; return; }
        OpenPanel();
    }

    /// <summary>Reroll: kalan hak varsa kartlari YENIDEN (tamamen rastgele) dagitir.</summary>
    private void HandleReroll()
    {
        if (!_isOpen || _animating) return;
        if (_rerollsLeft <= 0) return;
        _rerollsLeft--;
        UpdateRerollButton();
        StartCoroutine(RerollSlotMachine()); // slot makinesi gibi don, en son otur
    }

    /// <summary>Slot makinesi: kartlar hizla rastgele yuzler gosterir + dikey kayar (sol yukari, sag asagi),
    /// yavaslayarak en son yeni kartlara oturur. UNSCALED (panel timeScale=0).</summary>
    private System.Collections.IEnumerator RerollSlotMachine()
    {
        _animating = true;
        if (_cardRowLayout != null) _cardRowLayout.enabled = false; // pozisyonlari elle oynatabilmek icin

        int n = cardSlots != null ? cardSlots.Length : 0;
        var rts = new RectTransform[n];
        var basePos = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            if (cardSlots[i] == null) continue;
            rts[i] = cardSlots[i].GetComponent<RectTransform>();
            basePos[i] = rts[i].anchoredPosition;
            var cg = EnsureGroup(i); cg.blocksRaycasts = false; cg.interactable = false;
        }

        // Havuzu tazele (rastgele yuzler bundan gelir)
        BuildAvailableOptions();
        ShuffleAvailable();

        const float dur = 0.7f;
        float t = 0f, tick = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime; tick += Time.unscaledDeltaTime;
            if (tick >= 0.05f && _available.Count > 0)
            {
                tick = 0f;
                for (int i = 0; i < n; i++)
                {
                    if (cardSlots[i] == null || !cardSlots[i].gameObject.activeSelf) continue;
                    var opt = _available[UnityEngine.Random.Range(0, _available.Count)];
                    BindOptionToCard(cardSlots[i], i, opt); // rastgele yuz (gorsel)
                }
            }
            float amp = Mathf.Lerp(170f, 0f, t / dur); // sona dogru sonumlenir
            float e = t * 26f;
            for (int i = 0; i < n; i++)
            {
                if (rts[i] == null) continue;
                float dir = (i % 2 == 0) ? 1f : -1f; // sol yukari, sag asagi (zit)
                rts[i].anchoredPosition = basePos[i] + new Vector2(0f, Mathf.Sin(e) * amp * dir);
            }
            yield return null;
        }

        for (int i = 0; i < n; i++) if (rts[i] != null) rts[i].anchoredPosition = basePos[i];
        if (_cardRowLayout != null) _cardRowLayout.enabled = true;
        PopulateCards(); // final kartlar (raycast/interactable geri acilir)
        _animating = false;
    }

    /// <summary>Reroll butonu yazisini/etkinligini gunceller (kalan hak; 0 ise pasif).</summary>
    private void UpdateRerollButton()
    {
        if (rerollButton != null) rerollButton.interactable = _rerollsLeft > 0;
        if (rerollLabel != null) rerollLabel.SetText("REROLL ({0})", _rerollsLeft);
    }

    private void OpenPanel()
    {
        if (panelRoot == null || cardSlots == null || cardSlots.Length == 0)
        {
            Debug.LogWarning("UpgradeSelectionUI: panelRoot veya cardSlots atanmamis — panel acilamaz.");
            return;
        }

        _isOpen = true;
        panelRoot.SetActive(true);
        if (liveStatsPanel != null) liveStatsPanel.SetActive(true); // statlar sadece kart secimi sirasinda gorunsun
        Time.timeScale = 0f;
        if (playerRef != null) playerRef.SetPaused(true);

        _rerollsLeft = rerollsPerOpen;
        PopulateCards();
        UpdateRerollButton();
    }

    /// <summary>Dinamik havuzu (stat + silah) kurar, karistirir, slotlara basar.</summary>
    private void PopulateCards()
    {
        ResetCardVisuals(); // onceki secim animasyonunun (pozisyon/olcek/alpha/parent) izlerini temizle
        BuildAvailableOptions();

        int show = Mathf.Min(cardSlots.Length, _available.Count);
        PickWeighted(show); // agirliga gore N farkli kart sec (Amount daha nadir)

        for (int i = 0; i < cardSlots.Length; i++)
        {
            UpgradeCard card = cardSlots[i];
            if (card == null) continue;

            bool visible = i < _picked.Count;
            card.gameObject.SetActive(visible);
            if (!visible) continue;

            CardOption opt = _picked[i];
            _shown[i] = opt;
            BindOptionToCard(card, i, opt);
        }
    }

    /// <summary>Mevcut duruma gore secenek listesini doldurur: tum stat'lar + alinmamis/yukseltilebilir silahlar.</summary>
    private void BuildAvailableOptions()
    {
        _available.Clear();

        // Yeni model: kartlar SADECE stat. Sahip olunmayan silahin hasar stati (Charge/Firepower/Impact) havuza girmez.
        int statCount = upgrades != null ? upgrades.Length : 0;
        for (int i = 0; i < statCount; i++)
            if (IsStatRelevant(upgrades[i].type))
                _available.Add(new CardOption { isWeapon = false, statIndex = i });
    }

    /// <summary>Silah-ozel hasar stati mi ve o silah kusanildi mi? Ortak statlar hep gecerli.</summary>
    private bool IsStatRelevant(UpgradeType t)
    {
        string wid = t == UpgradeType.Charge ? "orbital"
                   : t == UpgradeType.Firepower ? "blaster"
                   : t == UpgradeType.Impact ? "boomerang" : null;
        if (wid == null) return true; // ortak stat (Might/Haste/Area/Amount/MaxHealth)
        if (weaponManager == null || weaponManager.Weapons == null) return false;
        foreach (var w in weaponManager.Weapons)
            if (w != null && w.IsAcquired && w.GetType().Name.ToLowerInvariant().Contains(wid)) return true;
        return false; // silah kusanilmadi -> bu hasar stati cikmasin
    }

    /// <summary>Agirliga gore (Amount daha nadir) N FARKLI secenek secip _picked'e koyar.</summary>
    private void PickWeighted(int count)
    {
        _picked.Clear();
        _weightPool.Clear();
        _weightPool.AddRange(_available);
        for (int n = 0; n < count && _weightPool.Count > 0; n++)
        {
            float total = 0f;
            for (int i = 0; i < _weightPool.Count; i++) total += StatWeight(_weightPool[i]);
            float roll = UnityEngine.Random.value * total;
            int idx = _weightPool.Count - 1;
            for (int i = 0; i < _weightPool.Count; i++)
            {
                roll -= StatWeight(_weightPool[i]);
                if (roll <= 0f) { idx = i; break; }
            }
            _picked.Add(_weightPool[idx]);
            _weightPool.RemoveAt(idx);
        }
    }

    /// <summary>Bir secenegin cikma agirligi (stat.weight; silah/gecersizse 1).</summary>
    private float StatWeight(CardOption o)
    {
        if (!o.isWeapon && upgrades != null && o.statIndex >= 0 && o.statIndex < upgrades.Length)
            return Mathf.Max(0.001f, upgrades[o.statIndex].weight);
        return 1f;
    }

    /// <summary>_available listesini Fisher-Yates ile karistirir (List, ekstra alloc yok).</summary>
    private void ShuffleAvailable()
    {
        for (int i = _available.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (_available[i], _available[j]) = (_available[j], _available[i]);
        }
    }

    /// <summary>Bir secenegi karta baglar (baslik/aciklama/ikon/level + renk/banner).</summary>
    private void BindOptionToCard(UpgradeCard card, int slotIndex, CardOption opt)
    {
        if (opt.isWeapon)
        {
            WeaponBase w = opt.weapon;
            string title = opt.isNewWeapon ? w.WeaponName : opt.upgrade.title;
            string desc = opt.isNewWeapon ? "New weapon!" : opt.upgrade.description;
            int level = opt.isNewWeapon ? 1 : opt.upgrade.nextLevel;
            bool lucky = !opt.isNewWeapon && opt.upgrade.lucky; // sayi/yon/hedef upgrade'i -> LUCKY
            card.Bind(slotIndex, w.WeaponIcon, weaponIconColor, weaponIconHeight, title, desc, level, weaponCardSprite, cardTint, opt.isNewWeapon, _cardCallback, lucky);
        }
        else
        {
            UpgradeDefinition def = upgrades[opt.statIndex];
            StatFrame(def.type, out Sprite frame, out Color tint);
            Color iconColor = (frame == greyCardSprite) ? tint : Color.white; // yeni statlar: ikon da kart rengi
            card.Bind(slotIndex, def.icon, iconColor, statIconHeight, def.title, def.description, def.level + 1, frame, tint, false, _cardCallback, false);
        }
    }

    /// <summary>Stat turune gore kart cercevesi + tint. Yeni statlar (Amount/Charge/Firepower/Impact) GRI cerceve + ozel tint.</summary>
    private void StatFrame(UpgradeType type, out Sprite frame, out Color tint)
    {
        tint = cardTint; // beyaz (renkli sprite'lar zaten renkli)
        switch (type)
        {
            case UpgradeType.Damage:      frame = damageCardSprite; break;      // sari
            case UpgradeType.AttackSpeed: frame = attackSpeedCardSprite; break; // mavi
            case UpgradeType.AttackRange: frame = attackRangeCardSprite; break; // yesil
            case UpgradeType.GainHealth:  frame = healthCardSprite; break;      // kirmizi
            case UpgradeType.Amount:      frame = amountCardSprite != null ? amountCardSprite : greyCardSprite; tint = Color.white; break; // rengarenk gradient (guclu his)
            case UpgradeType.Charge:      frame = greyCardSprite; tint = new Color(0.80f, 0.38f, 0.98f, 1f); break; // mor (orbital)
            case UpgradeType.Firepower:   frame = greyCardSprite; tint = new Color(1f, 0.50f, 0.14f, 1f);   break; // turuncu (blaster)
            case UpgradeType.Impact:      frame = greyCardSprite; tint = new Color(0.24f, 0.86f, 0.52f, 1f); break; // yesil (boomerang)
            default:                      frame = greyCardSprite; break;
        }
        if (frame == null) frame = greyCardSprite; // guvenlik
    }

    /// <summary>Stat turune gore kart cerceve sprite'i (kategori rengi).</summary>
    private Sprite StatCardSprite(UpgradeType type)
    {
        switch (type)
        {
            case UpgradeType.Damage:      return damageCardSprite;
            case UpgradeType.AttackSpeed: return attackSpeedCardSprite;
            case UpgradeType.AttackRange: return attackRangeCardSprite;
            case UpgradeType.GainHealth:  return healthCardSprite;
            default:                      return null;
        }
    }

    /// <summary>Bir kart secilince cagrilir (parametre = slot indeksi). Secenegi HEMEN uygular (oyun duraklali),
    /// sonra secim animasyonunu oynatip kapatir/devam eder.</summary>
    private void HandleCardSelected(int slotIndex)
    {
        if (_animating) return; // animasyon sirasinda ikinci secim yok
        if (_shown == null || slotIndex < 0 || slotIndex >= _shown.Length) return;

        ApplyChoice(_shown[slotIndex]); // timeScale=0 — hemen uygula, animasyon araya girsin
        _animating = true;
        StartCoroutine(AnimateAndContinue(slotIndex));
    }

    /// <summary>Secilen secenegi (stat/silah) uygular. Panel akisindan ayrildi ki animasyon araya girebilsin.</summary>
    private void ApplyChoice(CardOption opt)
    {
        if (opt.isWeapon)
        {
            if (opt.isNewWeapon) { if (opt.weapon != null) opt.weapon.Acquire(); }
            else { opt.upgrade.apply?.Invoke(); }
        }
        else if (upgrades != null && opt.statIndex >= 0 && opt.statIndex < upgrades.Length)
        {
            UpgradeDefinition def = upgrades[opt.statIndex];
            ApplyUpgrade(def);
            def.level++;
            OnUpgradeSelected?.Invoke(def.type);
        }
    }

    /// <summary>Secim animasyonunu oynatir; bitince bekleyen secim varsa yeni kartlari acar, yoksa paneli kapatir.</summary>
    private IEnumerator AnimateAndContinue(int chosenSlot)
    {
        yield return AnimateSelection(chosenSlot);
        _animating = false;
        if (_pendingSelections > 0) { _pendingSelections--; PopulateCards(); }
        else ClosePanel();
    }

    /// <summary>
    /// Secim animasyonu: secilmeyen kartlar yukari kayip solar; secilen kart kedinin ARKASINDAKI kamera-uzayi
    /// canvas'a tasinip kucule kucule + solarak kedinin ekran konumuna akar (layer olarak kedinin arkasinda kaybolur).
    /// UNSCALED zaman (panel timeScale=0).
    /// </summary>
    private IEnumerator AnimateSelection(int chosenSlot)
    {
        if (_cardRowLayout != null) _cardRowLayout.enabled = false; // layout pozisyonlari ezmesin

        int n = cardSlots != null ? cardSlots.Length : 0;
        var startPos = new Vector2[n];
        var active = new bool[n];
        Vector2 selStartLocal = Vector2.zero, selCatLocal = Vector2.zero;
        RectTransform selRT = null;

        for (int i = 0; i < n; i++)
        {
            var card = cardSlots[i];
            active[i] = card != null && card.gameObject.activeSelf;
            if (!active[i]) continue;
            var cg = EnsureGroup(i);
            cg.blocksRaycasts = false; cg.interactable = false; // animasyonda tiklanamaz
            startPos[i] = card.GetComponent<RectTransform>().anchoredPosition;
        }

        // Secilen karti kedinin arkasindaki kamera canvas'ina tasi + baslangic/kedi ekran noktalarini hesapla
        if (chosenSlot >= 0 && chosenSlot < n && active[chosenSlot])
        {
            selRT = cardSlots[chosenSlot].GetComponent<RectTransform>();
            Camera cam = Camera.main;
            EnsureFlyCanvas();
            if (_flyCanvas != null && cam != null)
            {
                Vector2 screenStart = RectTransformUtility.WorldToScreenPoint(null, selRT.position); // overlay -> ekran px
                Vector3 catWorld = playerRef != null ? playerRef.transform.position : Vector3.zero;
                Vector2 catScreen = cam.WorldToScreenPoint(catWorld);

                selRT.SetParent(_flyCanvasRT, false);                 // kedinin ARKASI (sortingOrder < player)
                selRT.anchorMin = selRT.anchorMax = new Vector2(0.5f, 0.5f);
                selRT.pivot = new Vector2(0.5f, 0.5f);                 // kendi merkezine dogru kuculsun
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_flyCanvasRT, screenStart, cam, out selStartLocal);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_flyCanvasRT, catScreen, cam, out selCatLocal);
                selRT.anchoredPosition = selStartLocal;
            }
            else selRT = null; // fly canvas yoksa secilen de sadece solar
        }

        float t = 0f;
        while (t < SelectAnimDuration)
        {
            t += Time.unscaledDeltaTime;
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / SelectAnimDuration));
            for (int i = 0; i < n; i++)
            {
                if (!active[i]) continue;
                var rt = cardSlots[i].GetComponent<RectTransform>();
                if (i == chosenSlot && selRT != null)
                {
                    rt.anchoredPosition = Vector2.Lerp(selStartLocal, selCatLocal, e);
                    rt.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.001f, e); // opak kalir, kedinin icine kuculerek kaybolur
                }
                else
                {
                    rt.anchoredPosition = startPos[i] + Vector2.up * (UnselectedRiseDistance * e); // opak kalir, yukari kayarak ekrandan cikar
                }
            }
            yield return null;
        }

        if (selRT != null) selRT.SetParent(_cardRow, false); // eve don (panel kapaninca/populate'te normale doner)
    }

    /// <summary>Secilen kartin arkasina cizilecegi kamera-uzayi canvas'i (lazily) olusturur. sortingOrder < player.</summary>
    private void EnsureFlyCanvas()
    {
        if (_flyCanvas != null) return;
        var go = new GameObject("UpgradeCardFlyCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var cv = go.GetComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceCamera;
        cv.worldCamera = Camera.main;
        cv.planeDistance = 5f;
        int order = 9;
        if (playerRef != null)
        {
            var sr = playerRef.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) order = sr.sortingOrder - 1; // kedinin bir ALTI = arkasi
        }
        cv.sortingOrder = order;

        var sc = go.GetComponent<CanvasScaler>();
        var mainScaler = panelRoot != null ? panelRoot.GetComponentInParent<CanvasScaler>() : null;
        if (mainScaler != null) // ayni olcek faktoru -> reparent'ta boyut sicramasi olmaz
        {
            sc.uiScaleMode = mainScaler.uiScaleMode;
            sc.referenceResolution = mainScaler.referenceResolution;
            sc.screenMatchMode = mainScaler.screenMatchMode;
            sc.matchWidthOrHeight = mainScaler.matchWidthOrHeight;
        }
        _flyCanvas = cv;
        _flyCanvasRT = go.GetComponent<RectTransform>();
    }

    /// <summary>Kartlarin animasyon oncesi orijinal anchor/pivot'unu yedekler (reset icin).</summary>
    private void CaptureCardOriginals()
    {
        if (cardSlots == null) return;
        int m = cardSlots.Length;
        _origAnchorMin = new Vector2[m]; _origAnchorMax = new Vector2[m]; _origPivot = new Vector2[m];
        for (int i = 0; i < m; i++)
        {
            if (cardSlots[i] == null) continue;
            var rt = cardSlots[i].GetComponent<RectTransform>();
            _origAnchorMin[i] = rt.anchorMin; _origAnchorMax[i] = rt.anchorMax; _origPivot[i] = rt.pivot;
        }
        if (m > 0 && cardSlots[0] != null)
        {
            _cardRow = cardSlots[0].transform.parent as RectTransform;
            if (_cardRow != null) _cardRowLayout = _cardRow.GetComponent<HorizontalLayoutGroup>();
        }
    }

    /// <summary>Kartlari animasyon sonrasi pristine hale getirir: eve tasi, olcek/alpha/anchor sifirla, layout'u geri ac.</summary>
    private void ResetCardVisuals()
    {
        if (_cardRowLayout != null) _cardRowLayout.enabled = true;
        if (cardSlots == null) return;
        for (int i = 0; i < cardSlots.Length; i++)
        {
            var card = cardSlots[i];
            if (card == null) continue;
            var rt = card.GetComponent<RectTransform>();
            if (_cardRow != null && rt.parent != _cardRow) { rt.SetParent(_cardRow, false); rt.SetSiblingIndex(i); }
            if (_origAnchorMin != null && i < _origAnchorMin.Length)
            {
                rt.anchorMin = _origAnchorMin[i]; rt.anchorMax = _origAnchorMax[i]; rt.pivot = _origPivot[i];
            }
            rt.localScale = Vector3.one;
            var cg = EnsureGroup(i);
            cg.alpha = 1f; cg.interactable = true; cg.blocksRaycasts = true;
        }
        if (_cardRow != null) LayoutRebuilder.ForceRebuildLayoutImmediate(_cardRow);
    }

    /// <summary>Kartin CanvasGroup'unu dondurur (yoksa runtime ekler) — fade icin.</summary>
    private CanvasGroup EnsureGroup(int i)
    {
        var cg = cardSlots[i].GetComponent<CanvasGroup>();
        if (cg == null) cg = cardSlots[i].gameObject.AddComponent<CanvasGroup>();
        return cg;
    }

    /// <summary>Secilen STAT upgrade turune gore player'in ilgili metodunu cagirir.</summary>
    private void ApplyUpgrade(UpgradeDefinition def)
    {
        if (playerRef == null || def == null) return;

        switch (def.type)
        {
            case UpgradeType.Damage:      // Might: SADECE Sharp Claws (tracker uygular) - global YOK
            case UpgradeType.Charge:      // Orbital hasari (tracker)
            case UpgradeType.Firepower:   // Blaster hasari (tracker)
            case UpgradeType.Impact:      // Boomerang hasari (tracker)
                break; // hasar artik silah-basina: WeaponLevelTracker ilgili silahin damage track'ini yukseltir
            case UpgradeType.AttackSpeed: // Haste: tum atis/vurus hizi (carpan, tabanla sinirli)
                RunStats.CooldownMult = Mathf.Max(RunStats.MinCooldownMult, RunStats.CooldownMult * def.amount);
                break;
            case UpgradeType.AttackRange: // Area: menzil/yaricap
                RunStats.AreaMult += def.amount;
                break;
            case UpgradeType.GainHealth:  // Max Health: kalici +can
                playerRef.AddMaxHealth(def.amount);
                break;
            case UpgradeType.Amount:      // +sayi (mermi/orb/yon/hedef)
                RunStats.AmountBonus += Mathf.Max(1, Mathf.RoundToInt(def.amount));
                break;
        }
        RefreshWeapons(); // cache'li silahlar (orbital/multislash) yeni stat'i hemen yansitsin
    }

    /// <summary>Alinmis tum silahlara RefreshStats cagirir (global stat degisince cache yenilensin).</summary>
    private void RefreshWeapons()
    {
        if (weaponManager == null || weaponManager.Weapons == null) return;
        foreach (var w in weaponManager.Weapons)
            if (w != null && w.IsAcquired) w.RefreshStats();
    }

    private void ClosePanel()
    {
        _isOpen = false;
        if (panelRoot != null) panelRoot.SetActive(false);
        if (liveStatsPanel != null) liveStatsPanel.SetActive(false);
        Time.timeScale = 1f;
        if (playerRef != null) playerRef.SetPaused(false);
        EnemyFreeze.FreezeFor(enemyFreezeAfterUpgrade);
    }
    #endregion
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
        Damage,
        AttackSpeed,
        AttackRange,
        GainHealth // Anlik can doldurur (max can DEGISMEZ). NOT: 4. sirada kalmali — sahne serialize'i int deger.
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

    [Tooltip("Sahnedeki sabit kart slotlari (genelde 3 tane).")]
    [SerializeField] private UpgradeCard[] cardSlots;

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
    private bool _isOpen;
    private Action<int> _cardCallback;

    private readonly List<CardOption> _available = new List<CardOption>(); // her acilista yeniden doldurulur
    private readonly List<WeaponUpgradeOption> _weaponUpgradeBuffer = new List<WeaponUpgradeOption>();
    private CardOption[] _shown;                                           // o an gosterilen kartlar (slot sirasi)

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
        if (_instance == this) _instance = null;
    }
    #endregion

    #region Private Methods
    private void HandleThresholdReached(int thresholdLevel)
    {
        if (_isOpen) { _pendingSelections++; return; }
        OpenPanel();
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
        Time.timeScale = 0f;
        if (playerRef != null) playerRef.SetPaused(true);

        PopulateCards();
    }

    /// <summary>Dinamik havuzu (stat + silah) kurar, karistirir, slotlara basar.</summary>
    private void PopulateCards()
    {
        ResetCardVisuals(); // onceki secim animasyonunun (pozisyon/olcek/alpha/parent) izlerini temizle
        BuildAvailableOptions();
        ShuffleAvailable();

        int show = Mathf.Min(cardSlots.Length, _available.Count);

        for (int i = 0; i < cardSlots.Length; i++)
        {
            UpgradeCard card = cardSlots[i];
            if (card == null) continue;

            bool visible = i < show;
            card.gameObject.SetActive(visible);
            if (!visible) continue;

            CardOption opt = _available[i];
            _shown[i] = opt;
            BindOptionToCard(card, i, opt);
        }
    }

    /// <summary>Mevcut duruma gore secenek listesini doldurur: tum stat'lar + alinmamis/yukseltilebilir silahlar.</summary>
    private void BuildAvailableOptions()
    {
        _available.Clear();

        int statCount = upgrades != null ? upgrades.Length : 0;
        for (int i = 0; i < statCount; i++)
            _available.Add(new CardOption { isWeapon = false, statIndex = i });

        if (weaponManager != null && weaponManager.Weapons != null)
        {
            foreach (WeaponBase w in weaponManager.Weapons)
            {
                if (w == null) continue;
                if (!w.IsAcquired)
                {
                    _available.Add(new CardOption { isWeapon = true, weapon = w, isNewWeapon = true });
                }
                else
                {
                    // Silahin AYRI yukseltmelerini (track'lerini) ayri kart secenekleri olarak ekle
                    _weaponUpgradeBuffer.Clear();
                    w.CollectUpgrades(_weaponUpgradeBuffer);
                    foreach (var up in _weaponUpgradeBuffer)
                        _available.Add(new CardOption { isWeapon = true, weapon = w, isNewWeapon = false, upgrade = up });
                }
            }
        }
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
            card.Bind(slotIndex, def.icon, Color.white, statIconHeight, def.title, def.description, def.level + 1, StatCardSprite(def.type), cardTint, false, _cardCallback, false);
        }
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
            case UpgradeType.Damage:
                playerRef.AddDamage(def.amount);
                break;
            case UpgradeType.AttackSpeed:
                playerRef.ApplyAttackSpeedMultiplier(def.amount);
                break;
            case UpgradeType.AttackRange:
                playerRef.AddAttackRange(def.amount);
                break;
            case UpgradeType.GainHealth:
                playerRef.Heal(def.amount);
                break;
        }
    }

    private void ClosePanel()
    {
        _isOpen = false;
        if (panelRoot != null) panelRoot.SetActive(false);
        Time.timeScale = 1f;
        if (playerRef != null) playerRef.SetPaused(false);
        EnemyFreeze.FreezeFor(enemyFreezeAfterUpgrade);
    }
    #endregion
}

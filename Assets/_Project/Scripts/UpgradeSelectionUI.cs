using System;
using System.Collections.Generic;
using UnityEngine;

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
            card.Bind(slotIndex, w.WeaponIcon, weaponIconColor, weaponIconHeight, title, desc, level, weaponCardSprite, cardTint, opt.isNewWeapon, _cardCallback);
        }
        else
        {
            UpgradeDefinition def = upgrades[opt.statIndex];
            card.Bind(slotIndex, def.icon, Color.white, statIconHeight, def.title, def.description, def.level + 1, StatCardSprite(def.type), cardTint, false, _cardCallback);
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

    /// <summary>Bir kart secilince cagrilir (parametre = slot indeksi). Secenegi uygular, kapat/devam et.</summary>
    private void HandleCardSelected(int slotIndex)
    {
        if (_shown == null || slotIndex < 0 || slotIndex >= _shown.Length) return;

        CardOption opt = _shown[slotIndex];

        if (opt.isWeapon)
        {
            if (opt.isNewWeapon)
            {
                if (opt.weapon != null) opt.weapon.Acquire();
            }
            else
            {
                opt.upgrade.apply?.Invoke(); // secilen track'i (hasar/hiz/sayi vb.) arttir
            }
        }
        else if (upgrades != null && opt.statIndex >= 0 && opt.statIndex < upgrades.Length)
        {
            UpgradeDefinition def = upgrades[opt.statIndex];
            ApplyUpgrade(def);
            def.level++;
            OnUpgradeSelected?.Invoke(def.type);
        }

        if (_pendingSelections > 0)
        {
            _pendingSelections--;
            PopulateCards();
        }
        else
        {
            ClosePanel();
        }
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

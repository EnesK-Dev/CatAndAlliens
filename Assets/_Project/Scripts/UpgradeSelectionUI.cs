using System;
using UnityEngine;

/// <summary>
/// FAZ 5 upgrade paneli. CoreManager.OnThresholdReached'i dinler; esige ulasinca oyunu
/// duraklatir (Time.timeScale=0 + player.SetPaused), 4 upgrade'den 3'unu rastgele secip
/// kartlara basar. Oyuncu bir kart secince ilgili stat'i player'a uygular, seviyeyi arttirir,
/// OnUpgradeSelected firlatir ve paneli kapatip oyunu devam ettirir. God GameManager degil —
/// sadece upgrade secim akisindan sorumlu, gevsek bagli (static event).
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
        MaxHealth
    }

    /// <summary>Bir upgrade seceneginin verisi — Inspector'dan doldurulur (denge burada tutulur).</summary>
    [Serializable]
    public class UpgradeDefinition
    {
        public string title = "Upgrade";
        [TextArea] public string description = "";
        public Sprite icon;
        public UpgradeType type;

        [Tooltip("Damage: +hasar | AttackSpeed: cooldown carpani (0.85 = %15 hizli) | " +
                 "AttackRange: +menzil | MaxHealth: +max can")]
        public float amount = 1f;

        [HideInInspector] public int level; // kac kez secildi (runtime stack sayaci)
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

    [Header("Upgrade Havuzu (4 tane onerilir, 3'u rastgele gosterilir)")]
    [SerializeField] private UpgradeDefinition[] upgrades;
    #endregion

    #region Private Fields
    private static UpgradeSelectionUI _instance;
    private int[] _indices;            // shuffle tamponu — bir kez alloc
    private int _pendingSelections;    // panel acikken gelen ekstra esikler (sirali islenir)
    private bool _isOpen;
    private Action<int> _cardCallback; // her acilista yeniden alloc olmasin diye cache
    #endregion

    #region Static API
    /// <summary>Bir upgrade secilince firlar. Parametre: secilen stat turu.</summary>
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

        if (playerRef == null)
            playerRef = FindFirstObjectByType<player>();

        int count = upgrades != null ? upgrades.Length : 0;
        _indices = new int[count];
        for (int i = 0; i < count; i++)
            _indices[i] = i;

        if (panelRoot != null)
            panelRoot.SetActive(false); // baslangicta kapali

        CoreManager.OnThresholdReached += HandleThresholdReached;
    }

    private void OnDestroy()
    {
        CoreManager.OnThresholdReached -= HandleThresholdReached;
        if (_instance == this)
            _instance = null;
    }
    #endregion

    #region Private Methods
    /// <summary>Esige ulasinca cagrilir. Panel zaten aciksa siraya alir, degilse acar.</summary>
    private void HandleThresholdReached(int thresholdLevel)
    {
        if (_isOpen)
        {
            _pendingSelections++; // ayni karede birden fazla esik gecilirse sirayla goster
            return;
        }
        OpenPanel();
    }

    /// <summary>Paneli acar: oyunu duraklatir ve kartlari doldurur.</summary>
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

    /// <summary>4 upgrade'den kart sayisi kadarini rastgele secip slotlara basar.</summary>
    private void PopulateCards()
    {
        int upgradeCount = upgrades != null ? upgrades.Length : 0;
        int show = Mathf.Min(cardSlots.Length, upgradeCount);

        ShuffleIndices(upgradeCount);

        for (int i = 0; i < cardSlots.Length; i++)
        {
            UpgradeCard card = cardSlots[i];
            if (card == null) continue;

            bool visible = i < show;
            card.gameObject.SetActive(visible);
            if (!visible) continue;

            int optionIndex = _indices[i];
            UpgradeDefinition def = upgrades[optionIndex];
            card.Bind(optionIndex, def.icon, def.title, def.description, def.level + 1, _cardCallback);
        }
    }

    /// <summary>_indices dizisinin ilk 'count' elemanini Fisher-Yates ile karistirir (alloc yok).</summary>
    private void ShuffleIndices(int count)
    {
        for (int i = count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (_indices[i], _indices[j]) = (_indices[j], _indices[i]);
        }
    }

    /// <summary>Bir kart secilince cagrilir: stat'i uygula, seviyeyi arttir, event firlat, kapat/devam et.</summary>
    private void HandleCardSelected(int upgradeIndex)
    {
        if (upgrades == null || upgradeIndex < 0 || upgradeIndex >= upgrades.Length) return;

        UpgradeDefinition def = upgrades[upgradeIndex];
        ApplyUpgrade(def);
        def.level++;
        OnUpgradeSelected?.Invoke(def.type);

        if (_pendingSelections > 0)
        {
            _pendingSelections--;
            PopulateCards(); // panel acik + duraklatilmis kalir, yeni 3 kart cikar
        }
        else
        {
            ClosePanel();
        }
    }

    /// <summary>Secilen upgrade turune gore player'in ilgili metodunu cagirir.</summary>
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
            case UpgradeType.MaxHealth:
                playerRef.AddMaxHealth(def.amount);
                break;
        }
    }

    /// <summary>Paneli kapatir ve oyunu devam ettirir.</summary>
    private void ClosePanel()
    {
        _isOpen = false;
        if (panelRoot != null) panelRoot.SetActive(false);
        Time.timeScale = 1f;
        if (playerRef != null) playerRef.SetPaused(false);
    }
    #endregion
}

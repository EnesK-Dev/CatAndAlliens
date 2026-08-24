using System;
using UnityEngine;

/// <summary>
/// Combo (vurus serisi) cekirdegi. Oyuncu dusmana her vurdugunda RegisterHit() ile sayaç artar;
/// belirli esiklerde rank yukselir (E -> D -> C -> B -> A -> S) ve her rank'in bir hasar/saldiri-hizi
/// carpani vardir. Combo "karma" kurallarla kirilir: 4sn vurulmazsa -1 tier (zaman decay),
/// hasar alininca -3 tier. Sahnede tek ornek + statik erisim (CoreManager pattern'i). God GameManager
/// degil — sadece combo sorumlulugunu tasir. Carpan; player.cs hasar ve saldiri cooldown'una uygulanir.
/// </summary>
public class ComboManager : MonoBehaviour
{
    #region Nested Types
    /// <summary>Bir combo rank'inin verisi: etiket, bu rank'e girmek icin gereken vurus, ve stat carpani.</summary>
    [Serializable]
    public class ComboTier
    {
        [Tooltip("UI'da gosterilecek rank etiketi (E/D/C/B/A/S).")]
        public string label = "E";

        [Tooltip("Bu rank'e girmek icin gereken TOPLAM ardisik vurus. Ilk tier 0 olmali.")]
        public int requiredHits = 0;

        [Tooltip("Bu rank aktifken hasar VE saldiri hizina uygulanan carpan (1 = etkisiz).")]
        public float multiplier = 1f;

        /// <summary>Inspector-dostu ctor + kod tarafi varsayilanlar icin.</summary>
        public ComboTier(string label, int requiredHits, float multiplier)
        {
            this.label = label;
            this.requiredHits = requiredHits;
            this.multiplier = multiplier;
        }
    }
    #endregion

    #region Serialized Fields
    [Header("Rank Tablosu")]
    [Tooltip("Rank esikleri ve carpanlari. requiredHits ARTAN sirada olmali; ilk eleman 0 vurus (taban rank).")]
    [SerializeField]
    private ComboTier[] tiers =
    {
        new ComboTier("E", 0, 1.00f),
        new ComboTier("D", 10, 1.10f),
        new ComboTier("C", 25, 1.25f),
        new ComboTier("B", 45, 1.45f),
        new ComboTier("A", 70, 1.70f),
        new ComboTier("S", 100, 2.00f),
    };

    [Header("Kirilma Kurallari (Karma)")]
    [Tooltip("Bu kadar saniye HIC vurulmazsa combo bir tier duser (zaman decay). Vurdukca sifirlanir.")]
    [SerializeField] private float decayInterval = 4f;

    [Tooltip("Zaman decay tetiklenince kac tier dusurulur.")]
    [SerializeField] private int tiersLostOnDecay = 1;

    [Tooltip("Player hasar aldiginda kac tier dusurulur (hasar cezasi).")]
    [SerializeField] private int tiersLostOnDamage = 3;

    [Header("Debug")]
    [Tooltip("Acikken Play sirasinda sol-ust kosede combo durumunu yazar.")]
    [SerializeField] private bool showDebugOverlay = false;
    #endregion

    #region Private Fields
    private static ComboManager _instance;

    private int _count;             // Guncel ardisik vurus sayisi
    private int _tierIndex;         // tiers[] icindeki guncel rank indeksi
    private float _timeSinceLastHit; // Son basarili vurustan beri gecen sure (zaman decay icin)
    #endregion

    #region Rank Renk Paleti
    // Rank'lara karsilik gelen renkler. Kod-tabanli tek kaynak: hem ComboUI hem DamageNumber
    // buradan okur, boylece renkler her yerde tutarli. Asset/tema gelince buradan degistirilir.
    // Sira tiers[] ile ayni: E, D, C, B, A, S.
    private static readonly Color[] RankColors =
    {
        new Color(0.85f, 0.85f, 0.85f), // E - gri/beyaz
        new Color(0.40f, 0.90f, 0.45f), // D - yesil
        new Color(0.35f, 0.75f, 1.00f), // C - mavi
        new Color(0.70f, 0.45f, 1.00f), // B - mor
        new Color(1.00f, 0.60f, 0.20f), // A - turuncu
        new Color(1.00f, 0.85f, 0.20f), // S - altin
    };

    /// <summary>Verilen rank indeksinin rengini dondurur (sinir disi indeks guvenli sekilde kirpilir).</summary>
    public static Color RankColorAt(int index) =>
        RankColors[Mathf.Clamp(index, 0, RankColors.Length - 1)];

    /// <summary>Guncel rank'in rengi.</summary>
    public static Color CurrentRankColor => RankColorAt(RankIndex);
    #endregion

    #region Static API
    /// <summary>Guncel ardisik vurus sayisi. Manager yoksa 0.</summary>
    public static int Count => _instance != null ? _instance._count : 0;

    /// <summary>Guncel rank indeksi (0 = ilk/taban rank). Manager yoksa 0.</summary>
    public static int RankIndex => _instance != null ? _instance._tierIndex : 0;

    /// <summary>En yuksek rank indeksi (tiers son eleman). Manager yoksa 0. S-tier tespiti icin.</summary>
    public static int MaxRankIndex => _instance != null ? _instance.tiers.Length - 1 : 0;

    /// <summary>Guncel rank etiketi (E/D/C/B/A/S). Manager yoksa "E".</summary>
    public static string RankLabel =>
        _instance != null ? _instance._tiers()[_instance._tierIndex].label : "E";

    /// <summary>
    /// Guncel hasar/saldiri-hizi carpani. player.cs bunu hasara CARPAR ve saldiri cooldown'una BOLER.
    /// Manager yoksa 1 (etkisiz) — combo sistemi olmadan oyun normal calisir.
    /// </summary>
    public static float Multiplier =>
        _instance != null ? _instance._tiers()[_instance._tierIndex].multiplier : 1f;

    /// <summary>Combo sayisi degistiginde firlar (vurus veya decay). Parametre: (yeni sayi, yeni rank indeksi). UI dinler.</summary>
    public static event Action<int, int> OnComboChanged;

    /// <summary>Rank degistiginde firlar. Parametre: (eski indeks, yeni indeks). Yukseliste juice/ses icin FAZ 2-3 dinler.</summary>
    public static event Action<int, int> OnRankChanged;

    /// <summary>Oyuncu bir dusmana basariyla vurdugunda player.cs cagirir. Sayaci arttirir, rank'i gunceller.</summary>
    public static void RegisterHit()
    {
        if (_instance != null)
            _instance.RegisterHitInternal();
    }
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        // Sahne basina tek manager — ikinciyi sessizce ele (CoreManager pattern'i)
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        ResetCombo(silent: true);
    }

    private void OnEnable()
    {
        // Hasar cezasi: player hasar alinca combo -3 tier. Mevcut static event'i dinliyoruz (yeni event yok).
        player.OnPlayerDamaged += HandlePlayerDamaged;
    }

    private void OnDisable()
    {
        player.OnPlayerDamaged -= HandlePlayerDamaged;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void Update()
    {
        // Zaman decay — sadece combo varken islet. Time.deltaTime kullaniyoruz (unscaled DEGIL):
        // upgrade paneli timeScale=0 yapinca combo da duraklamali, decay etmemeli.
        if (_count <= 0) return;

        _timeSinceLastHit += Time.deltaTime;
        if (_timeSinceLastHit >= decayInterval)
        {
            DropTiers(tiersLostOnDecay);
            _timeSinceLastHit = 0f; // Bir sonraki decay icin pencereyi yeniden baslat
        }
    }
    #endregion

    #region Private Methods
    /// <summary>tiers dizisini guvenli dondurur (bos/null ise minimal bir taban tier uretmez — Awake'te dolu varsayilir).</summary>
    private ComboTier[] _tiers() => tiers;

    private void RegisterHitInternal()
    {
        _count++;
        _timeSinceLastHit = 0f; // Vurdu — decay penceresi sifirlanir
        RecomputeRank();
        OnComboChanged?.Invoke(_count, _tierIndex);
    }

    /// <summary>Player hasar aldiginda cagrilir (OnPlayerDamaged). Combo'yu tiersLostOnDamage kadar dusurur.</summary>
    private void HandlePlayerDamaged(float amount)
    {
        if (_count <= 0) return; // Zaten tabanda — dusurecek bir sey yok
        DropTiers(tiersLostOnDamage);
    }

    /// <summary>
    /// Combo'yu 'n' tier asagi tasir: hedef tier'in requiredHits'ine oturtur ve sayaci oraya ceker.
    /// Boylece "tier icindeki ilerleme" de sifirlanir (30 vurus + 1 tier dus = alt tier'in tabanina).
    /// </summary>
    private void DropTiers(int n)
    {
        if (n <= 0 || _count <= 0) return;

        int oldTier = _tierIndex;
        int targetTier = Mathf.Max(0, _tierIndex - n);

        _count = tiers[targetTier].requiredHits;
        _tierIndex = targetTier;

        OnComboChanged?.Invoke(_count, _tierIndex);
        if (oldTier != _tierIndex)
            OnRankChanged?.Invoke(oldTier, _tierIndex);
    }

    /// <summary>Guncel _count'a gore rank indeksini yeniden hesaplar; degistiyse OnRankChanged firlar.</summary>
    private void RecomputeRank()
    {
        int newTier = 0;
        for (int i = 0; i < tiers.Length; i++)
        {
            if (_count >= tiers[i].requiredHits)
                newTier = i;
            else
                break; // requiredHits artan sirada — ilk gecilemeyen esikte dur
        }

        if (newTier != _tierIndex)
        {
            int oldTier = _tierIndex;
            _tierIndex = newTier;
            OnRankChanged?.Invoke(oldTier, _tierIndex);
        }
    }

    /// <summary>Combo'yu tabana (E) alir. silent=true ise event firlatmaz (Awake ilk kurulum icin).</summary>
    private void ResetCombo(bool silent)
    {
        int oldTier = _tierIndex;
        _count = 0;
        _tierIndex = 0;
        _timeSinceLastHit = 0f;

        if (silent) return;

        OnComboChanged?.Invoke(_count, _tierIndex);
        if (oldTier != _tierIndex)
            OnRankChanged?.Invoke(oldTier, _tierIndex);
    }

    private void OnGUI()
    {
        if (!showDebugOverlay) return;

        string text = $"COMBO  x{_count}   RANK {tiers[_tierIndex].label}   mult {tiers[_tierIndex].multiplier:0.00}";
        GUI.color = Color.yellow;
        GUI.Label(new Rect(12f, 12f, 400f, 24f), text);
    }
    #endregion
}

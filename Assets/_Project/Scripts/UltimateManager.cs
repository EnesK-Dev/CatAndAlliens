using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ultimate sarj + tetikleme cekirdegi. Sahnede tek ornek, statik erisimli (CoreManager pattern'i).
/// Oyuncu ultFood topladikca UltimateManager.AddCharge() cagrilir ve sarj dolar; dolunca "ready" olur.
/// UI'daki Ultimate butonu UltimateManager.TryActivate() cagirir; hazirsa player.ActivateUltimate ile
/// tum statlari sureli 2x'ler, sarji sifirlar. God GameManager degil — sadece ultimate akisindan sorumlu,
/// gevsek bagli (static event). Buff mekanigi player'da, gosterim/giris UI'da, koordinasyon burada.
/// </summary>
public class UltimateManager : MonoBehaviour
{
    #region Serialized Fields
    [Header("Sarj Ayarlari")]
    [Tooltip("Ultimate'in dolmasi icin toplanmasi gereken ultFood sayisi.")]
    [SerializeField] private int requiredFood = 8;

    [Header("Ultimate Etkisi")]
    [Tooltip("Aktive olunca tum statlarin carpani (2 = 2x hasar/hiz/menzil, yari cooldown).")]
    [SerializeField] private float ultMultiplier = 2f;

    [Tooltip("Buff'in saniye cinsinden suresi.")]
    [SerializeField] private float ultDuration = 8f;

    [Header("Debug")]
    [Tooltip("SADECE TEST: acikken oyun basinda bar dolu + ult hazir baslar. Yayinda KAPAT.")]
    [SerializeField] private bool startReadyForTesting = false;

    [Header("UltFood Pool Ayarlari")]
    [Tooltip("Dusman olunce birakilan toplanabilir ultFood prefab'i.")]
    [SerializeField] private UltFoodItem ultFoodPrefab;

    [Tooltip("Baslangicta on-isitilan (prewarm) food havuzu boyutu.")]
    [SerializeField] private int initialPoolSize = 20;

    [Tooltip("Ayni dusmandan birden fazla food dusunce ust uste binmesin diye bu yaricap icinde sacilir.")]
    [SerializeField] private float scatterRadius = 0.4f;

    [Header("Referanslar")]
    [Tooltip("Bos birakilirsa Awake'te otomatik bulunur.")]
    [SerializeField] private player playerRef;
    #endregion

    #region Private Fields
    private static UltimateManager _instance;
    private readonly Queue<UltFoodItem> _pool = new Queue<UltFoodItem>();
    private Transform _playerTransform;
    private int _currentFood;
    private bool _isReady;
    #endregion

    #region Static API
    /// <summary>
    /// Sarj degisince firlar. Parametre: 0-1 arasi normalize dolum orani (UI bar fill'i icin).
    /// </summary>
    public static event Action<float> OnChargeChanged;

    /// <summary>
    /// Ultimate hazir olma durumu degisince firlar. Parametre: true = dolu/kullanilabilir, false = degil.
    /// UI butonu bunu dinleyip interactable ayarlar.
    /// </summary>
    public static event Action<bool> OnReadyChanged;

    /// <summary>Ultimate su an hazir mi (dolu ve kullanilmayi bekliyor). Manager yoksa false.</summary>
    public static bool IsReady => _instance != null && _instance._isReady;

    /// <summary>Guncel sarj orani (0-1). Manager yoksa 0.</summary>
    public static float Charge01 => _instance != null ? _instance.ChargeNormalized() : 0f;

    /// <summary>
    /// Verilen dunya konumunda 'amount' adet ultFood birakir; dusmanin rengiyle boyar (olum
    /// animasyonuyla ayni). Sahnede manager yoksa sessizce hicbir sey yapmaz (fake-null guvenli).
    /// Dusmanlar Die() icinde sansa bagli cagirir. Ultimate ZATEN DOLUYSA yeni food dusmez (bosa gitmesin).
    /// </summary>
    public static void SpawnFood(Vector3 position, Color tint, int amount = 1)
    {
        if (_instance != null)
            _instance.SpawnFoodInternal(position, tint, amount);
    }

    /// <summary>
    /// Bir ultFood toplaninca cagrilir: sarji arttir, event firlat, dolduysa ready yap.
    /// Manager yoksa sessizce hicbir sey yapmaz (Unity fake-null guvenli).
    /// </summary>
    public static void AddCharge(int amount = 1)
    {
        if (_instance != null)
            _instance.AddChargeInternal(amount);
    }

    /// <summary>
    /// Ultimate butonu bunu cagirir. Hazirsa: buff'i aktive eder, sarji sifirlar, true doner.
    /// Hazir degilse hicbir sey yapmaz, false doner.
    /// </summary>
    public static bool TryActivate()
    {
        return _instance != null && _instance.TryActivateInternal();
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
        _currentFood = 0;
        _isReady = false;

        if (playerRef == null)
            playerRef = FindFirstObjectByType<player>();

        if (playerRef != null)
            _playerTransform = playerRef.transform;

        PrewarmPool();
    }

    private void Start()
    {
        // SADECE TEST: bar dolu + hazir baslat
        if (startReadyForTesting)
        {
            _currentFood = Mathf.Max(1, requiredFood);
            _isReady = true;
        }

        // UI gec enable olsa bile baslangic durumunu alsin diye ilk event'leri firlat
        OnChargeChanged?.Invoke(ChargeNormalized());
        OnReadyChanged?.Invoke(_isReady);
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
    #endregion

    #region Private Methods
    /// <summary>Baslangicta food havuzunu on-isit — yogun olum anlarinda Instantiate spike'i olmasin.</summary>
    private void PrewarmPool()
    {
        if (ultFoodPrefab == null)
        {
            Debug.LogWarning("UltimateManager: ultFoodPrefab atanmamis — dusmanlar ultFood birakmayacak.");
            return;
        }

        for (int i = 0; i < initialPoolSize; i++)
            _pool.Enqueue(CreateInstance());
    }

    /// <summary>Yeni bir pasif UltFoodItem ornegi olusturur (kuyruga eklemez).</summary>
    private UltFoodItem CreateInstance()
    {
        UltFoodItem instance = Instantiate(ultFoodPrefab, transform);
        instance.gameObject.SetActive(false);
        return instance;
    }

    private void SpawnFoodInternal(Vector3 position, Color tint, int amount)
    {
        if (ultFoodPrefab == null) return;
        if (_isReady) return; // ult zaten dolu — kullanilana kadar yeni food bosa gitmesin

        for (int i = 0; i < amount; i++)
        {
            // Havuz bosalirsa buyu — yogun olum anlarinda food eksik kalmasin
            UltFoodItem food = _pool.Count > 0 ? _pool.Dequeue() : CreateInstance();

            // Tek food ise tam noktaya, birden fazlaysa kucuk rastgele sacilma ver
            Vector2 offset = amount > 1 ? UnityEngine.Random.insideUnitCircle * scatterRadius : Vector2.zero;
            Vector3 spawnPos = position + (Vector3)offset;

            food.gameObject.SetActive(true);
            food.Play(spawnPos, tint, _playerTransform, HandleFoodCollected);
        }
    }

    /// <summary>Bir food toplaninca UltFoodItem tarafindan cagrilir: sarji arttir, pool'a dondur.</summary>
    private void HandleFoodCollected(UltFoodItem instance)
    {
        AddChargeInternal(1);
        ReturnToPool(instance);
    }

    private void ReturnToPool(UltFoodItem instance)
    {
        if (instance == null) return;

        instance.gameObject.SetActive(false);
        _pool.Enqueue(instance);
    }

    /// <summary>0-1 arasi normalize dolum orani. requiredFood <= 0 ise hemen 1 (kilitlenmeyi onle).</summary>
    private float ChargeNormalized()
    {
        if (requiredFood <= 0) return 1f;
        return Mathf.Clamp01((float)_currentFood / requiredFood);
    }

    private void AddChargeInternal(int amount)
    {
        if (_isReady) return; // zaten dolu — kullanilana kadar fazlasi bosa gitmesin

        int cap = Mathf.Max(1, requiredFood);
        _currentFood = Mathf.Clamp(_currentFood + Mathf.Max(1, amount), 0, cap);
        OnChargeChanged?.Invoke(ChargeNormalized());

        if (_currentFood >= cap)
        {
            _isReady = true;
            OnReadyChanged?.Invoke(true);
        }
    }

    private bool TryActivateInternal()
    {
        if (!_isReady) return false;

        if (playerRef != null)
            playerRef.ActivateUltimate(ultMultiplier, ultDuration);

        // Sarji sifirla — tekrar doldurulmasi gerekir
        _currentFood = 0;
        _isReady = false;
        OnChargeChanged?.Invoke(ChargeNormalized());
        OnReadyChanged?.Invoke(false);
        return true;
    }
    #endregion
}

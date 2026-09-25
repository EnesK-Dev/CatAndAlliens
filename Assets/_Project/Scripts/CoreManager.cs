using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Core (guclenme parasi) icin object pool + toplam sayaci. Sahnede tek ornek olur ve
/// statik erisimlidir (DamagePopupManager pattern'i). Dusmanlar olurken
/// CoreManager.SpawnCores(...) ile core birakir; oyuncu topladikca TotalCores artar ve
/// OnCoreCountChanged firlar (FAZ 5 upgrade esigi bunu dinleyecek). God GameManager degil —
/// sadece core sorumlulugunu tasir.
/// </summary>
public class CoreManager : MonoBehaviour
{
    #region Serialized Fields
    [Header("Pool Ayarlari")]
    [SerializeField] private CoreItem coreItemPrefab;
    [SerializeField] private int initialPoolSize = 30;

    [Header("Sacilma Ayarlari")]
    [Tooltip("Ayni dusmandan birden fazla core dusunce ust uste binmesin diye bu yaricap icinde rastgele sacilirlar.")]
    [SerializeField] private float scatterRadius = 0.5f;

    [Header("Upgrade Esigi (FAZ 5) — TIER modeli")]
    [Tooltip("Ilk tier'in kart maliyeti (kac core ile kart alinir). Ornek 10.")]
    [SerializeField] private int firstThreshold = 10;

    [Tooltip("KAC kartta bir maliyet artsin. Ornek 3 -> 3 kart ayni fiyat, sonra artar. (3 kere 10, 3 kere 20...)")]
    [SerializeField] private int cardsPerTier = 3;

    [Tooltip("Her tier'da maliyetin artis miktari. Ornek 10 -> 10, 20, 30, 40 ... Milestone'dan BAGIMSIZ, kendi ic sayaci.")]
    [SerializeField] private int thresholdAdditiveStep = 10;
    #endregion

    #region Private Fields
    private static CoreManager _instance;
    private readonly Queue<CoreItem> _pool = new Queue<CoreItem>();
    private Transform _playerTransform;
    private int _totalCores;               // HARCANABILIR in-run bakiye (kart alinca duser)
    private int _coresCollectedThisRun;    // bu run'da TOPLANAN brut core (harcamadan bagimsiz) — olunce bankaya bu yazilir
    private int _nextThreshold;
    private int _thresholdLevel;
    #endregion

    #region Static API
    /// <summary>Harcanabilir GUNCEL core bakiyesi (kart alinca duser). Sahnede manager yoksa 0 doner.</summary>
    public static int TotalCores => _instance != null ? _instance._totalCores : 0;

    /// <summary>Core bakiyesi degisince firlar (toplama VEYA kartta harcama). Parametre: yeni bakiye.</summary>
    public static event Action<int> OnCoreCountChanged;

    /// <summary>
    /// Core bakiyesi bir kart almaya yetince firlar (core'lar HARCANIR). Parametre: kacinci kart (1'den baslar).
    /// FAZ 5 upgrade paneli bunu dinleyip acilir.
    /// </summary>
    public static event Action<int> OnThresholdReached;

    /// <summary>Bir sonraki kartin MALIYETI (bu kadar core birikince kart alinir ve harcanir). Manager yoksa 0.</summary>
    public static int NextThreshold => _instance != null ? _instance._nextThreshold : 0;

    /// <summary>
    /// Verilen dunya konumunda 'amount' adet core birakir. Sahnede manager yoksa sessizce
    /// hicbir sey yapmaz (Unity fake-null guvenli).
    /// </summary>
    public static void SpawnCores(Vector3 position, int amount)
    {
        if (_instance != null)
            _instance.SpawnCoresInternal(position, amount, _instance.scatterRadius);
    }

    /// <summary>
    /// GENIS sacilma ile core birakir (ornek: boss oldugunde core'lari etrafa yayar, tek yiginda birakmaz).
    /// scatterOverride = sacilma yaricapi (dunya birimi).
    /// </summary>
    public static void SpawnCores(Vector3 position, int amount, float scatterOverride)
    {
        if (_instance != null)
            _instance.SpawnCoresInternal(position, amount, scatterOverride);
    }
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        // Sahne basina tek manager — ikinciyi sessizce ele
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        _totalCores = 0;
        _coresCollectedThisRun = 0;
        _nextThreshold = Mathf.Max(1, firstThreshold); // en az 1 — 0 verilirse kilitlenmeyi onle
        _thresholdLevel = 0;
        CachePlayer();
        PrewarmPool();

        player.OnPlayerDied += HandleRunEnd;           // olunce run core'lari bankaya
        WinConditionManager.OnGameWon += HandleRunEnd;  // kazaninca da bankala
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            player.OnPlayerDied -= HandleRunEnd;
            WinConditionManager.OnGameWon -= HandleRunEnd;
            _instance = null;
        }
    }
    #endregion

    #region Private Methods
    /// <summary>Oyuncu transform'unu bir kez bulup saklar — core'lar her karede magnet icin bunu kullanir.</summary>
    private void CachePlayer()
    {
        player target = FindFirstObjectByType<player>();
        if (target != null)
            _playerTransform = target.transform;
    }

    private void PrewarmPool()
    {
        if (coreItemPrefab == null)
        {
            Debug.LogWarning("CoreManager: coreItemPrefab atanmamis — dusmanlar core birakmayacak.");
            return;
        }

        for (int i = 0; i < initialPoolSize; i++)
            _pool.Enqueue(CreateInstance());
    }

    /// <summary>Yeni bir pasif CoreItem ornegi olusturur (kuyruga eklemez).</summary>
    private CoreItem CreateInstance()
    {
        CoreItem instance = Instantiate(coreItemPrefab, transform);
        instance.gameObject.SetActive(false);
        return instance;
    }

    /// <summary>Run bitince (olum VEYA kazanma) bu run'da toplanan core'lari KALICI bankaya (MetaSave) yazar.
    /// Sonra sayac sifirlanir (ayni run'da iki kez bankalanmasin).</summary>
    private void HandleRunEnd()
    {
        // Ayri takip: run icinde core HARCANSA da, bankaya bu run'da TOPLANAN brut miktar yazilir.
        if (_coresCollectedThisRun > 0) MetaSave.AddCores(_coresCollectedThisRun);
        _totalCores = 0;
        _coresCollectedThisRun = 0;
        OnCoreCountChanged?.Invoke(_totalCores);
    }

    private void SpawnCoresInternal(Vector3 position, int amount, float scatter)
    {
        if (coreItemPrefab == null) return;

        for (int i = 0; i < amount; i++)
        {
            // Havuz bosalirsa buyu — yogun olum anlarinda core eksik kalmasin
            CoreItem core = _pool.Count > 0 ? _pool.Dequeue() : CreateInstance();

            // Tek core ise tam noktaya, birden fazlaysa rastgele sacilma ver (scatter yaricapinda)
            Vector2 offset = amount > 1 ? UnityEngine.Random.insideUnitCircle * scatter : Vector2.zero;
            Vector3 spawnPos = position + (Vector3)offset;

            core.gameObject.SetActive(true);
            core.Play(spawnPos, _playerTransform, HandleCollected);
        }
    }

    /// <summary>Bir core toplaninca CoreItem tarafindan cagrilir: sayaci arttir, event firlat, esigi kontrol et, pool'a dondur.</summary>
    private void HandleCollected(CoreItem instance)
    {
        _totalCores++;
        _coresCollectedThisRun++;               // brut toplanan (banka icin) — harcamadan etkilenmez
        OnCoreCountChanged?.Invoke(_totalCores);
        CheckThreshold();                        // in-run level-up: bakiye esige yetince kart paneli acilir
        ReturnToPool(instance);
    }

    /// <summary>
    /// Core bakiyesi karti almaya yetiyorsa: core'lari HARCAR (bakiyeden duser), karti acar ve bir sonraki
    /// kartin maliyetini buyutur. while: tek karede birden fazla kart alinabilir (nadir, bakiye cok yuksekse).
    /// Bar 'bakiye / maliyet' gosterir; harcayinca dolgu sifira yakin doner.
    /// </summary>
    private void CheckThreshold()
    {
        while (_totalCores >= _nextThreshold && _nextThreshold > 0)
        {
            _totalCores -= _nextThreshold;                       // CORE HARCA (kart alindi)
            _thresholdLevel++;                                   // alinan kart sayaci (kendi ic sayaci)
            _nextThreshold = CostForCardsTaken(_thresholdLevel); // her cardsPerTier kartta bir pahalanir
            OnCoreCountChanged?.Invoke(_totalCores);             // bar: harcanmis bakiye + yeni maliyet
            OnThresholdReached?.Invoke(_thresholdLevel);         // upgrade paneli acilir
        }
    }

    /// <summary>
    /// cardsTaken kadar kart alinmisken SIRADAKI kartin maliyeti. Her cardsPerTier kartta bir tier atlanir ve
    /// maliyet thresholdAdditiveStep kadar artar. Milestone'dan BAGIMSIZ (kendi ic sayaci).
    /// Ornek (first=10, perTier=3, step=10): 10,10,10, 20,20,20, 30,30,30, 40...
    /// </summary>
    private int CostForCardsTaken(int cardsTaken)
    {
        int tier = cardsTaken / Mathf.Max(1, cardsPerTier);
        return Mathf.Max(1, firstThreshold + tier * thresholdAdditiveStep);
    }

    private void ReturnToPool(CoreItem instance)
    {
        if (instance == null) return;

        instance.gameObject.SetActive(false);
        _pool.Enqueue(instance);
    }
    #endregion
}

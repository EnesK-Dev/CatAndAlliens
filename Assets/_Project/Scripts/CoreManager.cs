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

    [Header("Upgrade Esigi (FAZ 5)")]
    [Tooltip("Ilk upgrade paneli bu kadar TOPLAM core toplaninca acilir.")]
    [SerializeField] private int firstThreshold = 50;

    [Tooltip("Esik nasil buyusun? Additive: her seferinde sabit ekle. Multiplicative: onceki esigi carpanla buyut.")]
    [SerializeField] private ThresholdGrowthMode thresholdGrowthMode = ThresholdGrowthMode.Additive;

    [Tooltip("Additive modda her esikte eklenen miktar (50 -> 100 -> 150 ...).")]
    [SerializeField] private int thresholdAdditiveStep = 50;

    [Tooltip("Multiplicative modda esik carpani (50 -> 75 -> 112 ... icin 1.5).")]
    [SerializeField] private float thresholdMultiplier = 1.5f;
    #endregion

    #region Nested Types
    /// <summary>Upgrade esiginin nasil buyuyecegini belirler.</summary>
    public enum ThresholdGrowthMode
    {
        Additive,
        Multiplicative
    }
    #endregion

    #region Private Fields
    private static CoreManager _instance;
    private readonly Queue<CoreItem> _pool = new Queue<CoreItem>();
    private Transform _playerTransform;
    private int _totalCores;
    private int _nextThreshold;
    private int _thresholdLevel;
    #endregion

    #region Static API
    /// <summary>Oyuncunun topladigi guncel toplam core. Sahnede manager yoksa 0 doner.</summary>
    public static int TotalCores => _instance != null ? _instance._totalCores : 0;

    /// <summary>Bir core toplaninca firlar. Parametre: guncellenen toplam core sayisi.</summary>
    public static event Action<int> OnCoreCountChanged;

    /// <summary>
    /// Toplam core bir sonraki esige ulasinca firlar. Parametre: kacinci esik oldugu (1'den baslar).
    /// FAZ 5 upgrade paneli bunu dinleyip acilir. Esik SAYAC'tir — core harcanmaz, toplam artmaya devam eder.
    /// </summary>
    public static event Action<int> OnThresholdReached;

    /// <summary>Bir sonraki upgrade esigi (hedef toplam core). Manager yoksa 0.</summary>
    public static int NextThreshold => _instance != null ? _instance._nextThreshold : 0;

    /// <summary>
    /// Verilen dunya konumunda 'amount' adet core birakir. Sahnede manager yoksa sessizce
    /// hicbir sey yapmaz (Unity fake-null guvenli).
    /// </summary>
    public static void SpawnCores(Vector3 position, int amount)
    {
        if (_instance != null)
            _instance.SpawnCoresInternal(position, amount);
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
        _nextThreshold = Mathf.Max(1, firstThreshold); // en az 1 — 0 verilirse kilitlenmeyi onle
        _thresholdLevel = 0;
        CachePlayer();
        PrewarmPool();
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
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

    private void SpawnCoresInternal(Vector3 position, int amount)
    {
        if (coreItemPrefab == null) return;

        for (int i = 0; i < amount; i++)
        {
            // Havuz bosalirsa buyu — yogun olum anlarinda core eksik kalmasin
            CoreItem core = _pool.Count > 0 ? _pool.Dequeue() : CreateInstance();

            // Tek core ise tam noktaya, birden fazlaysa kucuk rastgele sacilma ver
            Vector2 offset = amount > 1 ? UnityEngine.Random.insideUnitCircle * scatterRadius : Vector2.zero;
            Vector3 spawnPos = position + (Vector3)offset;

            core.gameObject.SetActive(true);
            core.Play(spawnPos, _playerTransform, HandleCollected);
        }
    }

    /// <summary>Bir core toplaninca CoreItem tarafindan cagrilir: sayaci arttir, event firlat, esigi kontrol et, pool'a dondur.</summary>
    private void HandleCollected(CoreItem instance)
    {
        _totalCores++;
        OnCoreCountChanged?.Invoke(_totalCores);
        CheckThreshold();
        ReturnToPool(instance);
    }

    /// <summary>
    /// Toplam core esigi gectiyse OnThresholdReached firlar ve bir sonraki esigi hesaplar.
    /// Esik SAYAC'tir — core harcanmaz. while: tek karede birden fazla esik gecilirse hepsini firlatir.
    /// </summary>
    private void CheckThreshold()
    {
        while (_totalCores >= _nextThreshold)
        {
            _thresholdLevel++;
            OnThresholdReached?.Invoke(_thresholdLevel);
            _nextThreshold = ComputeNextThreshold(_nextThreshold);
        }
    }

    /// <summary>Bir sonraki esik degerini moda gore hesaplar. Her zaman en az +1 artar (kilitlenmeyi onler).</summary>
    private int ComputeNextThreshold(int current)
    {
        if (thresholdGrowthMode == ThresholdGrowthMode.Multiplicative)
            return Mathf.Max(current + 1, Mathf.RoundToInt(current * thresholdMultiplier));

        return current + Mathf.Max(1, thresholdAdditiveStep);
    }

    private void ReturnToPool(CoreItem instance)
    {
        if (instance == null) return;

        instance.gameObject.SetActive(false);
        _pool.Enqueue(instance);
    }
    #endregion
}

using System;
using UnityEngine;

/// <summary>
/// Dusman olunce sansa bagli birakilan "ultFood" (ahtapot bacagi). Oyuncu toplayinca Ultimate sarji
/// dolar. CoreItem'in ikizi: hafif nabiz (pulse) + donme (spin), magnet ile oyuncuya akar, degince
/// toplanir ve pool'a doner. TEK FARK: spawn aninda dusmanin rengiyle (baseColor/shooterColor/enemyColor)
/// boyanir — olum animasyonuyla ayni renk. Pooled — yasam dongusunu UltimateManager yonetir.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class UltFoodItem : MonoBehaviour
{
    #region Serialized Fields
    [Header("Gorsel")]
    [Tooltip("Boyanacak sprite. Bos birakilirsa Awake'te bu objeden otomatik alinir.")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Nabiz (Pulse) Ayarlari")]
    [Tooltip("Nabizda temel boyutun ne kadar orani buyuyup kuculecegi (0.12 = ±%12).")]
    [SerializeField] private float pulseAmount = 0.12f;

    [Tooltip("Nabiz hizi — buyudukce daha hizli 'atar'.")]
    [SerializeField] private float pulseSpeed = 4f;

    [Header("Donme (Spin) Ayarlari")]
    [Tooltip("Saniyede derece.")]
    [SerializeField] private float spinSpeed = 60f;

    [Header("Magnet Ayarlari")]
    [Tooltip("Oyuncu bu yaricapa girince food ona dogru akmaya baslar.")]
    [SerializeField] private float magnetRadius = 2.5f;

    [Tooltip("Magnet baslangic hizi (birim/sn).")]
    [SerializeField] private float magnetSpeed = 6f;

    [Tooltip("Magnet suresince ivme — oyuncuya yaklastikca hizlanir.")]
    [SerializeField] private float magnetAcceleration = 18f;

    [Tooltip("Bu mesafenin altina inince toplanmis sayilir — hizli oyuncuda trigger kacsa bile garanti.")]
    [SerializeField] private float collectDistance = 0.35f;
    #endregion

    #region Private Fields
    private Transform _playerTransform;
    private Action<UltFoodItem> _onCollected;
    private Vector3 _baseScale = Vector3.one;
    private Quaternion _baseRotation = Quaternion.identity;
    private float _pulseTimer;
    private float _currentMagnetSpeed;
    private bool _isCollected;
    #endregion

    #region Public Methods
    /// <summary>
    /// Food'u verilen konumda etkinlestirir; dusmanin rengiyle boyar ve magnet/pulse durumunu sifirlar.
    /// </summary>
    /// <param name="position">Spawn dunya konumu.</param>
    /// <param name="tint">Dusmanin rengi (olum animasyonuyla ayni). Sprite bununla boyanir.</param>
    /// <param name="playerTransform">Magnet hedefi (oyuncu). null olabilir — magnet devre disi kalir.</param>
    /// <param name="onCollected">Toplaninca pool'a donmesi icin UltimateManager callback'i.</param>
    public void Play(Vector3 position, Color tint, Transform playerTransform, Action<UltFoodItem> onCollected)
    {
        transform.position = position;
        transform.rotation = _baseRotation;
        transform.localScale = _baseScale;

        if (spriteRenderer != null)
            spriteRenderer.color = tint;

        _playerTransform = playerTransform;
        _onCollected = onCollected;
        _pulseTimer = 0f;
        _currentMagnetSpeed = magnetSpeed;
        _isCollected = false;
    }
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        // Prefab'in Transform Scale ve Rotation'ini temel al — pulse/spin bunlarin etrafinda oynar.
        _baseScale = transform.localScale;
        _baseRotation = transform.rotation;

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (_isCollected) return;

        AnimateVisual();
        HandleMagnet();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_isCollected) return;
        if (other.GetComponent<player>() == null) return;

        Collect();
    }

    private void OnDisable()
    {
        // Pool'a donerken stale referans tutma
        _onCollected = null;
        _playerTransform = null;
    }
    #endregion

    #region Private Methods
    /// <summary>Nabiz (Sin salinim) + spin. Alloc yok — her kare cagrilir.</summary>
    private void AnimateVisual()
    {
        _pulseTimer += Time.deltaTime * pulseSpeed;
        float factor = 1f + Mathf.Sin(_pulseTimer) * pulseAmount;
        transform.localScale = new Vector3(_baseScale.x * factor, _baseScale.y * factor, _baseScale.z);

        transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
    }

    /// <summary>Oyuncu magnet yaricapindaysa ona dogru hizlanarak akar; yeterince yakinsa toplar.</summary>
    private void HandleMagnet()
    {
        if (_playerTransform == null) return;

        float sqrDist = ((Vector2)_playerTransform.position - (Vector2)transform.position).sqrMagnitude;

        if (sqrDist > magnetRadius * magnetRadius)
        {
            _currentMagnetSpeed = magnetSpeed;
            return;
        }

        if (sqrDist <= collectDistance * collectDistance)
        {
            Collect();
            return;
        }

        _currentMagnetSpeed += magnetAcceleration * Time.deltaTime;
        transform.position = Vector3.MoveTowards(
            transform.position, _playerTransform.position, _currentMagnetSpeed * Time.deltaTime);
    }

    /// <summary>Bir kez toplanir; tekrar tetiklenmesini engeller ve UltimateManager'a haber verir.</summary>
    private void Collect()
    {
        if (_isCollected) return;
        _isCollected = true;
        _onCollected?.Invoke(this);
    }
    #endregion
}

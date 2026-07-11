using System;
using UnityEngine;

/// <summary>
/// Dusman olunce birakilan toplanabilir "core". Gorsel canlilik koddan gelir: hafif nabiz
/// (scale pulse) + donme (spin). Oyuncu magnet yaricapina girince ona dogru hizlanarak akar,
/// degince toplanir ve pool'a doner. Pooled — yasam dongusunu CoreManager yonetir.
/// Trigger collider ister; oyuncuda Rigidbody2D oldugu icin temas algilanir.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class CoreItem : MonoBehaviour
{
    #region Serialized Fields
    [Header("Nabiz (Pulse) Ayarlari")]
    [Tooltip("Nabizda temel boyutun ne kadar orani buyuyup kuculecegi (0.12 = ±%12). Temel boyut = prefab'in Transform Scale'i.")]
    [SerializeField] private float pulseAmount = 0.12f;

    [Tooltip("Nabiz hizi — buyudukce daha hizli 'atar'.")]
    [SerializeField] private float pulseSpeed = 4f;

    [Header("Donme (Spin) Ayarlari")]
    [Tooltip("Saniyede derece. Simetrik daire sprite'ta gorunmez; elmas/kristal sprite'a gecersen canlilik katar.")]
    [SerializeField] private float spinSpeed = 90f;

    [Header("Magnet Ayarlari")]
    [Tooltip("Oyuncu bu yaricapa girince core ona dogru akmaya baslar.")]
    [SerializeField] private float magnetRadius = 2.5f;

    [Tooltip("Magnet baslangic hizi (birim/sn). Cekilirken magnetAcceleration ile artar.")]
    [SerializeField] private float magnetSpeed = 6f;

    [Tooltip("Magnet suresince ivme — oyuncuya yaklastikca hizlanir, kacan core hissi olmaz.")]
    [SerializeField] private float magnetAcceleration = 18f;

    [Tooltip("Bu mesafenin altina inince toplanmis sayilir — hizli oyuncuda trigger kacsa bile garanti.")]
    [SerializeField] private float collectDistance = 0.35f;
    #endregion

    #region Private Fields
    private Transform _playerTransform;
    private Action<CoreItem> _onCollected;
    private Vector3 _baseScale = Vector3.one;
    private Quaternion _baseRotation = Quaternion.identity;
    private float _pulseTimer;
    private float _currentMagnetSpeed;
    private bool _isCollected;
    #endregion

    #region Public Methods
    /// <summary>Core'u verilen konumda etkinlestirir; magnet/pulse durumunu sifirlar.</summary>
    /// <param name="position">Spawn dunya konumu.</param>
    /// <param name="playerTransform">Magnet hedefi (oyuncu). null olabilir — magnet devre disi kalir.</param>
    /// <param name="onCollected">Toplaninca pool'a donmesi icin CoreManager callback'i.</param>
    public void Play(Vector3 position, Transform playerTransform, Action<CoreItem> onCollected)
    {
        transform.position = position;
        transform.rotation = _baseRotation;
        transform.localScale = _baseScale;

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
        // Bir kez yakalanir; sonra AnimateVisual her kare ezdigi icin tekrar okunamaz.
        _baseScale = transform.localScale;
        _baseRotation = transform.rotation;
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
        // Temel boyutun etrafinda oransal salinim — x/y olceklenir, z korunur.
        float factor = 1f + Mathf.Sin(_pulseTimer) * pulseAmount;
        transform.localScale = new Vector3(_baseScale.x * factor, _baseScale.y * factor, _baseScale.z);

        transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
    }

    /// <summary>Oyuncu magnet yaricapindaysa ona dogru hizlanarak akar; yeterince yakinsa toplar.</summary>
    private void HandleMagnet()
    {
        if (_playerTransform == null) return;

        // sqrMagnitude ile karsilastir — karekok maliyetinden kacin
        float sqrDist = ((Vector2)_playerTransform.position - (Vector2)transform.position).sqrMagnitude;

        if (sqrDist > magnetRadius * magnetRadius)
        {
            _currentMagnetSpeed = magnetSpeed; // yaricap disinda hizi bastan al
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

    /// <summary>Bir kez toplanir; tekrar tetiklenmesini engeller ve CoreManager'a haber verir.</summary>
    private void Collect()
    {
        if (_isCollected) return;
        _isCollected = true;
        _onCollected?.Invoke(this);
    }
    #endregion
}

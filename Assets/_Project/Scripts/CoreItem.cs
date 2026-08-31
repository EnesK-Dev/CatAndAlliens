using System;
using UnityEngine;

/// <summary>
/// Dusman olunce birakilan toplanabilir "core" (coin). Gorsel canlilik koddan gelir:
/// 4 kareli sprite animasyonu (donen/parlayan yildiz) + spawn aninda MERKEZDEN DISARI
/// firlayip yavaslayan "patlama" fazi. Patlama bitince oyuncu magnet yaricapina girince
/// ona dogru hizlanarak akar, degince toplanir ve pool'a doner. Pooled — yasam dongusunu
/// CoreManager yonetir. Trigger collider ister; oyuncuda Rigidbody2D oldugu icin temas algilanir.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class CoreItem : MonoBehaviour
{
    #region Serialized Fields
    [Header("Referanslar")]
    [Tooltip("Bos birakilirsa Awake'te otomatik bulunur. Kareler bunun sprite'ini degistirir.")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Kare Animasyonu (Coin Flip)")]
    [Tooltip("Sirayla oynatilan sprite kareleri (part_star_0..3). Bos ise animasyon calismaz, sprite sabit kalir.")]
    [SerializeField] private Sprite[] frames;

    [Tooltip("Saniyede kac kare oynatilsin. Ornek 10 -> her 0.1sn'de bir kare degisir.")]
    [SerializeField] private float frameRate = 10f;

    [Header("Patlama (Burst) Ayarlari")]
    [Tooltip("Spawn aninda core merkezden disariya bu hiz araliginda firlar (birim/sn).")]
    [SerializeField] private float burstSpeedMin = 3.5f;
    [SerializeField] private float burstSpeedMax = 6.5f;

    [Tooltip("Firlama ne kadar surer (sn). Bu sure boyunca magnet devre disi — once disari sacilir, sonra oyuncuya cekilir.")]
    [SerializeField] private float burstDuration = 0.32f;

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
    private float _currentMagnetSpeed;
    private bool _isCollected;

    // Kare animasyonu durumu
    private float _frameTimer;
    private int _frameIndex;

    // Patlama durumu
    private Vector2 _burstDirection;
    private float _burstSpeed;
    private float _burstTimer;
    #endregion

    #region Public Methods
    /// <summary>Core'u verilen konumda etkinlestirir; patlama/magnet durumunu sifirlar.</summary>
    /// <param name="position">Spawn dunya konumu (patlamanin merkezi).</param>
    /// <param name="playerTransform">Magnet hedefi (oyuncu). null olabilir — magnet devre disi kalir.</param>
    /// <param name="onCollected">Toplaninca pool'a donmesi icin CoreManager callback'i.</param>
    public void Play(Vector3 position, Transform playerTransform, Action<CoreItem> onCollected)
    {
        transform.position = position;
        transform.localScale = _baseScale;

        _playerTransform = playerTransform;
        _onCollected = onCollected;
        _currentMagnetSpeed = magnetSpeed;
        _isCollected = false;

        // Kare animasyonunu bastan baslat
        _frameTimer = 0f;
        _frameIndex = 0;
        if (spriteRenderer != null && frames != null && frames.Length > 0)
            spriteRenderer.sprite = frames[0];

        // Patlama: rastgele bir dis yon + rastgele hiz -> her core farkli savrulur (merkezden disari)
        float angle = UnityEngine.Random.value * Mathf.PI * 2f;
        _burstDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        _burstSpeed = UnityEngine.Random.Range(burstSpeedMin, burstSpeedMax);
        _burstTimer = 0f;
    }
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        // Prefab'in Transform Scale'ini temel al — reuse'da geri yukleriz.
        _baseScale = transform.localScale;
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (_isCollected) return;

        AnimateFrames();

        // Once patlama fazi (disari sacilma), bitince magnet (oyuncuya cekilme)
        if (_burstTimer < burstDuration)
            HandleBurst();
        else
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
    /// <summary>4 kareyi frameRate hizinda dongusel oynatir. Alloc yok — her kare cagrilir.</summary>
    private void AnimateFrames()
    {
        if (spriteRenderer == null || frames == null || frames.Length == 0 || frameRate <= 0f)
            return;

        _frameTimer += Time.deltaTime * frameRate;
        if (_frameTimer >= 1f)
        {
            _frameTimer -= 1f;
            _frameIndex = (_frameIndex + 1) % frames.Length;
            spriteRenderer.sprite = frames[_frameIndex];
        }
    }

    /// <summary>
    /// Spawn sonrasi kisa "patlama": core merkezden disariya firlar ve lineer olarak yavaslar
    /// (ease-out his). Bu faz boyunca magnet kapali — once dagilir, sonra oyuncuya cekilir.
    /// </summary>
    private void HandleBurst()
    {
        _burstTimer += Time.deltaTime;
        float t = Mathf.Clamp01(_burstTimer / burstDuration); // 0 -> 1
        float speed = _burstSpeed * (1f - t);                 // basta hizli, sonda 0 (yavaslayarak durur)
        transform.position += (Vector3)(_burstDirection * (speed * Time.deltaTime));
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
        SfxManager.Play(SfxId.CoinCollect); // core (coin) toplama sesi
        _onCollected?.Invoke(this);
    }
    #endregion
}

using UnityEngine;

/// <summary>
/// BoomerangEnemy'nin fırlattığı saldırı. Fırlatıldığı andaki oyuncu konumu MERKEZ olacak
/// şekilde tek bir daire (orbit) çizer, tam turu tamamlayınca fırlatan düşmana geri döner.
/// Oyuncu merkezde (hareketsiz) kalırsa daire onu hiç kesmez — sadece dairenin gectigi
/// yola girecek sekilde hareket ederse vurulur.
/// </summary>
public class BoomerangProjectile : MonoBehaviour
{
    #region Serialized Fields
    [Header("Yorunge (Orbit) Ayarlari")]
    [Tooltip("Daire etrafinda donme hizi (derece/saniye). Kucuk = yavas/zayif atis.")]
    [SerializeField] private float orbitAngularSpeedDegreesPerSecond = 150f;

    [Tooltip("Kac derece donunce geri donus fazina gecer. 360 = tam tur.")]
    [SerializeField] private float orbitSweepDegrees = 360f;

    [Tooltip("Yaricap, firlatma anindaki dusman-oyuncu mesafesinden hesaplanir; bu alt sinirdir.")]
    [SerializeField] private float minOrbitRadius = 1.5f;

    [Tooltip("Yaricap, firlatma anindaki dusman-oyuncu mesafesinden hesaplanir; bu ust sinirdir.")]
    [SerializeField] private float maxOrbitRadius = 6f;

    [Header("Geri Donus (Yakalama) Ayarlari")]
    [Tooltip("Yorunge bitince firlatan dusmana donerken kullanilan hiz.")]
    [SerializeField] private float catchSpeed = 5f;

    [SerializeField] private float catchDistance = 0.3f;

    [Header("Donme Animasyonu (kare bazli)")]
    [SerializeField] private Sprite[] spinFrames;
    [SerializeField] private float spinFrameRate = 24f;

    [Header("Yon Ayarlari")]
    [Tooltip("Bumerang gittigi yone dogru doner. Sprite'in varsayilan yonune gore duzeltme (derece).")]
    [SerializeField] private float facingAngleOffsetDegrees = 0f;

    [Header("Hasar Ayarlari")]
    [SerializeField] private float damage = 1f;

    [Header("Guvenlik")]
    [SerializeField] private float maxLifetime = 8f;
    #endregion

    #region Private Fields
    private enum FlightPhase { Orbiting, Returning }

    private FlightPhase _phase;
    private Transform _owner;
    private Vector2 _lockedCenterPosition;
    private Vector2 _lastKnownOwnerPosition;
    private float _orbitRadius;
    private float _currentAngleDegrees;
    private float _orbitDirection;
    private float _sweptDegrees;
    private bool _hasHit;

    private SpriteRenderer _spriteRenderer;
    private float _spinFrameDuration;
    private float _spinFrameTimer;
    private int _spinFrameIndex;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _spinFrameDuration = spinFrameRate > 0f ? 1f / spinFrameRate : 0f;

        // Guvenlik agi: herhangi bir sebeple faz makinesi hicbir zaman Destroy tetiklemezse diye
        Destroy(gameObject, maxLifetime);
    }

    private void Update()
    {
        if (_owner != null)
            _lastKnownOwnerPosition = _owner.position;

        Vector2 previousPosition = transform.position;

        switch (_phase)
        {
            case FlightPhase.Orbiting:
                UpdateOrbiting();
                break;
            case FlightPhase.Returning:
                UpdateReturning();
                break;
        }

        UpdateFacingRotation(previousPosition);
        UpdateSpinAnimation();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_hasHit) return;

        player cat = other.GetComponent<player>();
        if (cat == null) return;

        cat.TakeDamage(damage);
        _hasHit = true;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Bumerangi başlatır. Merkez konum (oyuncunun fırlatma anındaki konumu) kilitlenir;
    /// yarıçap o anki düşman-oyuncu mesafesinden hesaplanır. Oyuncu merkezde hareketsiz
    /// kalırsa daire onu hiç kesmez.
    /// </summary>
    public void Initialize(Transform ownerTransform, Vector2 lockedCenterPosition)
    {
        _owner = ownerTransform;
        _lockedCenterPosition = lockedCenterPosition;
        _lastKnownOwnerPosition = ownerTransform != null ? (Vector2)ownerTransform.position : (Vector2)transform.position;

        Vector2 offsetFromCenter = (Vector2)transform.position - lockedCenterPosition;
        _orbitRadius = Mathf.Clamp(offsetFromCenter.magnitude, minOrbitRadius, maxOrbitRadius);
        _currentAngleDegrees = Mathf.Atan2(offsetFromCenter.y, offsetFromCenter.x) * Mathf.Rad2Deg;

        _orbitDirection = Random.value < 0.5f ? 1f : -1f;
        _sweptDegrees = 0f;
        _phase = FlightPhase.Orbiting;
        _hasHit = false;

        // Baslangic pozisyonunu hesaplanan yaricapa gore hizala - Instantiate noktasi tam
        // dusmanin ustunde oldugu icin cemberin kenarina aninda tasi (ani sicrama gorunmez, ilk kare).
        SnapToOrbitPosition();
    }
    #endregion

    #region Private Methods
    private void UpdateOrbiting()
    {
        float step = orbitAngularSpeedDegreesPerSecond * Time.deltaTime;
        _currentAngleDegrees += step * _orbitDirection;
        _sweptDegrees += step;

        SnapToOrbitPosition();

        if (_sweptDegrees >= orbitSweepDegrees)
            _phase = FlightPhase.Returning;
    }

    private void SnapToOrbitPosition()
    {
        float radians = _currentAngleDegrees * Mathf.Deg2Rad;
        Vector2 offset = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * _orbitRadius;
        transform.position = _lockedCenterPosition + offset;
    }

    private void UpdateReturning()
    {
        Vector2 returnTarget = _owner != null ? (Vector2)_owner.position : _lastKnownOwnerPosition;
        transform.position = Vector2.MoveTowards(transform.position, returnTarget, catchSpeed * Time.deltaTime);

        if (Vector2.Distance(transform.position, returnTarget) <= catchDistance)
            Destroy(gameObject);
    }

    /// <summary>Bumerangi bir onceki kareye gore hareket yonune dondurur - yorunge boyunca tegete hizalanir.</summary>
    private void UpdateFacingRotation(Vector2 previousPosition)
    {
        Vector2 delta = (Vector2)transform.position - previousPosition;
        if (delta.sqrMagnitude < 0.000001f) return; // hareket yoksa (durdugu an) mevcut aciyi koru

        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg + facingAngleOffsetDegrees;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    /// <summary>Update icinde alloc yapmadan spin kareleri arasinda ilerler (EnemyBullet ile ayni desen).</summary>
    private void UpdateSpinAnimation()
    {
        if (_spriteRenderer == null || _spinFrameDuration <= 0f) return;
        if (spinFrames == null || spinFrames.Length == 0) return;

        _spinFrameTimer += Time.deltaTime;
        if (_spinFrameTimer < _spinFrameDuration) return;

        _spinFrameTimer -= _spinFrameDuration;
        _spinFrameIndex = (_spinFrameIndex + 1) % spinFrames.Length;
        _spriteRenderer.sprite = spinFrames[_spinFrameIndex];
    }
    #endregion
}

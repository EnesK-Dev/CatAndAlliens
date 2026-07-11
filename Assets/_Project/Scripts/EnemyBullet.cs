using UnityEngine;

/// <summary>
/// 3. uzaylı tipi tarafından fırlatılan mermi.
/// Ateş anındaki hedef konuma doğru sabit hızla hareket eder.
/// Animator olmadan, kod ile sprite kareleri arasında geçiş yaparak animasyon oynatır.
/// </summary>
public class EnemyBullet : MonoBehaviour
{
    #region Serialized Fields
    [Header("Hareket Ayarlari")]
    [SerializeField] private float speed = 6f;
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private float damage = 1f;

    [Header("Animasyon Ayarlari")]
    [SerializeField] private Sprite[] animationFrames;
    [SerializeField] private float frameRate = 12f;
    #endregion

    #region Private Fields
    private Rigidbody2D _rb;
    private SpriteRenderer _spriteRenderer;
    private float _frameDuration;
    private float _frameTimer;
    private int _currentFrame;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();

        // frameRate 0 ise sıfıra bölmeyi engelle; animasyon kapalı sayılır
        _frameDuration = frameRate > 0f ? 1f / frameRate : 0f;
    }

    private void Update()
    {
        if (_spriteRenderer == null || _frameDuration <= 0f) return;
        if (animationFrames == null || animationFrames.Length == 0) return;

        // Update içinde heap allocation yapmadan manuel zamanlayıcı ile kare ilerlet
        _frameTimer += Time.deltaTime;
        if (_frameTimer < _frameDuration) return;

        _frameTimer -= _frameDuration;
        _currentFrame = (_currentFrame + 1) % animationFrames.Length;
        _spriteRenderer.sprite = animationFrames[_currentFrame];
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        player cat = other.GetComponent<player>();
        if (cat == null) return;

        cat.TakeDamage(damage);
        Destroy(gameObject);
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Mermiyi başlatır; hedef konuma yön hesaplar ve hız atar.
    /// BurstShooterEnemy her ateşlemede bu metodu çağırır.
    /// </summary>
    public void Initialize(Vector2 targetPosition)
    {
        Vector2 direction = (targetPosition - (Vector2)transform.position).normalized;
        if (_rb != null)
            _rb.linearVelocity = direction * speed;

        Destroy(gameObject, lifetime);
    }
    #endregion
}

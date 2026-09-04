using System;
using UnityEngine;

/// <summary>
/// Oyuncunun firlatigi bumerang. Ileri gider (outDistance), sonra oyuncuya GERI doner; giderken ve
/// donerken dusmanlara hasar verir (pierce — yok olmaz). Oyuncuya yeterince yaklasinca "tutulur":
/// onReturned cagrilir ve yok olur (silah o zaman yenisini atabilir). Dinamik Rigidbody2D + trigger.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerBoomerang : MonoBehaviour
{
    #region Private Fields
    private Rigidbody2D _rb;
    private Transform _owner;
    private Vector2 _dir;
    private float _speed;
    private float _damage;
    private float _outDistance;
    private float _spin;
    private float _catchDistance;
    private Action _onReturned;

    private Vector2 _startPos;
    private bool _returning;
    private bool _finished;
    #endregion

    #region Public Methods
    /// <summary>Bumerangi baslatir: sahibi, yon, gidis mesafesi, hiz, hasar, donme hizi, tutulma mesafesi, geri-donunce callback.</summary>
    public void Launch(Transform owner, Vector2 direction, float outDistance, float speed, float damage,
                       float spin, float catchDistance, Action onReturned)
    {
        _rb = GetComponent<Rigidbody2D>();
        _owner = owner;
        _dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
        _outDistance = outDistance;
        _speed = speed;
        _damage = damage;
        _spin = spin;
        _catchDistance = catchDistance;
        _onReturned = onReturned;

        _startPos = transform.position;
        _returning = false;
        _finished = false;

        EnsureTrail(); // arkasina alev gibi iz (koddan TrailRenderer)
    }
    #endregion

    #region Unity Callbacks
    private void Update()
    {
        // Gorsel donme
        transform.Rotate(0f, 0f, _spin * Time.deltaTime);
    }

    private void FixedUpdate()
    {
        if (_finished) return;

        if (!_returning)
        {
            _rb.linearVelocity = _dir * _speed;
            if (Vector2.Distance(transform.position, _startPos) >= _outDistance)
                _returning = true; // gidis bitti — geri don
        }
        else
        {
            Vector2 target = _owner != null ? (Vector2)_owner.position : _startPos;
            Vector2 toOwner = target - (Vector2)transform.position;
            _rb.linearVelocity = toOwner.normalized * _speed;

            if (toOwner.magnitude <= _catchDistance) // tutuldu
            {
                _finished = true;
                _rb.linearVelocity = Vector2.zero;
                _onReturned?.Invoke();
                Destroy(gameObject);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        EnemyDamage.Apply(other, _damage * ComboManager.Multiplier); // pierce; combo carpani vurus aninda
    }
    #endregion

    #region Trail (alev izi)
    private const float TrailTime = 0.22f;        // iz ne kadar geride kalir (sn)
    private const float TrailStartWidth = 0.45f;  // izin baslangic kalinligi (uca dogru 0'a iner)
    private static Material _trailMat;            // tum bumeranglar tek material paylasir (alloc/leak yok)

    /// <summary>Bumerangin arkasina koddan alev gibi iz ekler (TrailRenderer): sicak sari->turuncu->saydam, uca dogru incelir.</summary>
    private void EnsureTrail()
    {
        var tr = GetComponent<TrailRenderer>();
        if (tr == null) tr = gameObject.AddComponent<TrailRenderer>();

        if (_trailMat == null)
        {
            Shader sh = Shader.Find("Sprites/Default")
                        ?? Shader.Find("Universal Render Pipeline/Unlit")
                        ?? Shader.Find("Unlit/Color");
            _trailMat = new Material(sh);
        }
        tr.sharedMaterial = _trailMat;

        tr.time = TrailTime;
        tr.startWidth = TrailStartWidth;
        tr.endWidth = 0f;
        tr.minVertexDistance = 0.05f;
        tr.numCapVertices = 4;
        tr.numCornerVertices = 2;
        tr.autodestruct = false;
        tr.emitting = true;
        tr.Clear(); // yeni atista eski iz kalmasin

        // Alev gradyani: sari -> turuncu -> koyu kirmizi + solma
        var grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1.0f, 0.92f, 0.35f), 0f),
                new GradientColorKey(new Color(1.0f, 0.50f, 0.12f), 0.5f),
                new GradientColorKey(new Color(0.70f, 0.12f, 0.0f), 1f),
            },
            new[]
            {
                new GradientAlphaKey(0.85f, 0f),
                new GradientAlphaKey(0.45f, 0.5f),
                new GradientAlphaKey(0.0f, 1f),
            });
        tr.colorGradient = grad;

        // Sprite'in ARKASINDA cizilsin
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            tr.sortingLayerID = sr.sortingLayerID;
            tr.sortingOrder = sr.sortingOrder - 1;
        }
    }
    #endregion
}

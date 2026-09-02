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
}

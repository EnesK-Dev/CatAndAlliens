using UnityEngine;

/// <summary>
/// Oyuncunun silahlarinin firlattigi mermi. Verilen yonde sabit hizla gider, dusmana degince
/// EnemyDamage ile hasar verir. pierce=false ise ilk vurusta yok olur, true ise gecer.
/// Trigger collider + Rigidbody2D ister. Gorsel sprite sonradan degistirilebilir.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerProjectile : MonoBehaviour
{
    #region Private Fields
    private Rigidbody2D _rb;
    private float _damage;
    private bool _pierce;
    #endregion

    #region Public Methods
    /// <summary>Mermiyi baslatir: yonu, hizi, hasari, omru ve delme durumunu ayarlar.</summary>
    public void Launch(Vector2 direction, float speed, float damage, float lifetime, bool pierce = false)
    {
        _rb = GetComponent<Rigidbody2D>();
        _damage = damage;
        _pierce = pierce;

        Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        _rb.linearVelocity = dir * speed;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

        Destroy(gameObject, lifetime);
    }
    #endregion

    #region Unity Callbacks
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (EnemyDamage.Apply(other, _damage) && !_pierce)
            Destroy(gameObject);
    }
    #endregion
}

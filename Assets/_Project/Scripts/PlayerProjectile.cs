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
    #region Serialized Fields
    [Header("Knockback (vurus hissi)")]
    [Tooltip("Vurunca dusmanin ITILME hizi (mermi gidis yonunde). Burst shot icin HAFIF tut.")]
    [SerializeField] private float knockbackSpeed = 2.5f;
    [Tooltip("Itmenin suresi (sn) — sonra sonumlenir.")]
    [SerializeField] private float knockbackDuration = 0.1f;
    #endregion

    #region Private Fields
    private Rigidbody2D _rb;
    private float _damage;
    private bool _pierce;
    private Vector2 _travelDir = Vector2.right;
    private float _despawnAt; // bu ana gelince havuza doner (omru bitince)
    #endregion

    #region Public Methods
    /// <summary>Mermiyi baslatir: yonu, hizi, hasari, omru ve delme durumunu ayarlar. Havuzdan tekrar kullanimda da her seyi sifirlar.</summary>
    public void Launch(Vector2 direction, float speed, float damage, float lifetime, bool pierce = false)
    {
        if (_rb == null) _rb = GetComponent<Rigidbody2D>();
        _damage = damage;
        _pierce = pierce;

        Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        _travelDir = dir;
        _rb.linearVelocity = dir * speed;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

        _despawnAt = Time.time + lifetime; // Destroy yerine zamanli despawn (havuz)
    }
    #endregion

    #region Unity Callbacks
    private void Update()
    {
        if (Time.time >= _despawnAt) PoolManager.Despawn(gameObject); // omru bitti -> havuza
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Vurunca dusmani mermi gidis yonunde HAFIFCE it (burst shot hissi)
        if (EnemyDamage.Apply(other, _damage * ComboManager.Multiplier, _travelDir, knockbackSpeed, knockbackDuration) && !_pierce)
            PoolManager.Despawn(gameObject); // Destroy yerine havuza
    }
    #endregion
}

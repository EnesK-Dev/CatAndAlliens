using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Oyuncunun etrafinda donen orbital top. Konumunu OrbitalWeapon her kare gunceller. Degdigi dusmana
/// SUREKLI (hitCooldown araligiyla, dusman basina) hasar verir. Trigger collider + kinematik Rigidbody2D.
/// </summary>
public class OrbitalOrb : MonoBehaviour
{
    #region Serialized Fields
    [Header("Knockback (vurus hissi)")]
    [Tooltip("Yildiz degince dusmanin ITILME hizi (orbtan disa dogru = vurdugu yon).")]
    [SerializeField] private float knockbackSpeed = 5f;
    [Tooltip("Itmenin suresi (sn) — sonra sonumlenir.")]
    [SerializeField] private float knockbackDuration = 0.15f;
    #endregion

    #region Private Fields
    private float _damage;
    private float _hitCooldown = 0.4f;
    private readonly Dictionary<Collider2D, float> _lastHit = new Dictionary<Collider2D, float>();
    #endregion

    #region Public Methods
    /// <summary>Hasar ve dusman-basi vurus araligini ayarlar (seviye degisince yeniden cagrilir).</summary>
    public void Configure(float damage, float hitCooldown)
    {
        _damage = damage;
        _hitCooldown = hitCooldown;
    }
    #endregion

    #region Unity Callbacks
    private void OnTriggerStay2D(Collider2D other)
    {
        if (_lastHit.TryGetValue(other, out float last) && Time.time - last < _hitCooldown)
            return;

        // Yildiz degince dusmani orbtan DISA (vurdugu yon) it
        Vector2 knockDir = (Vector2)other.transform.position - (Vector2)transform.position;
        if (EnemyDamage.Apply(other, _damage * ComboManager.Multiplier, knockDir, knockbackSpeed, knockbackDuration)) // combo carpani vurus aninda
            _lastHit[other] = Time.time;
    }
    #endregion
}

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
    private SpriteRenderer _sr; private Color _baseColor; private bool _baseCached;
    private float _scale = 1f; private Coroutine _pop;
    private static readonly Color HotColor = new Color(1f, 0.35f, 0.12f, 1f);
    #endregion

    #region Public Methods
    /// <summary>Hasar ve dusman-basi vurus araligini ayarlar (seviye degisince yeniden cagrilir).</summary>
    public void Configure(float damage, float hitCooldown, float scale, float hotTint)
    {
        _damage = damage;
        _hitCooldown = hitCooldown;
        if (!_baseCached) { _sr = GetComponentInChildren<SpriteRenderer>(); if (_sr != null) _baseColor = _sr.color; _baseCached = true; }
        if (scale > 0f) { _scale = scale; if (_pop == null) transform.localScale = Vector3.one * scale; } // hasarla buyu
        if (_sr != null) _sr.color = Color.Lerp(_baseColor, HotColor, Mathf.Clamp01(hotTint)); // hasarla isin
    }

    /// <summary>Juice: level atlayinca kisa "pop" (zipla) — unscaled (panel timeScale=0'da da oynar).</summary>
    public void Pop()
    {
        if (_pop != null) StopCoroutine(_pop);
        _pop = StartCoroutine(PopRoutine());
    }

    private System.Collections.IEnumerator PopRoutine()
    {
        float t = 0f, dur = 0.30f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = t / dur;
            float punch = 1f + 0.65f * Mathf.Sin(k * Mathf.PI); // 1 -> 1.65 -> 1 (daha belirgin)
            transform.localScale = Vector3.one * (_scale * punch);
            yield return null;
        }
        transform.localScale = Vector3.one * _scale;
        _pop = null;
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
        {
            _lastHit[other] = Time.time;
            ComboManager.RegisterWeaponHit("orbital", 0.25f); // combo: sadece orbital combo silahiysa
            SfxManager.Play(SfxId.OrbitalHit); // klip atanmazsa sessiz
        }
    }
    #endregion
}

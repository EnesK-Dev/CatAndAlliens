using UnityEngine;

/// <summary>
/// Boss lazeri (isin). GORSEL: mevcut animasyonlu LaserVisual (Laser.prefab) — iki dunya noktasi arasina
/// UpdateLaser ile cizilir. HASAR: bu objenin BoxCollider2D'si (trigger), oyuncuya degince hasar (cooldown'lu).
/// Isin origin'den verilen aciyla 'length' kadar uzanir. BossLaserAttack tarafindan havuzlanir/kontrol edilir.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class BossLaser : MonoBehaviour
{
    #region Private Fields
    private BoxCollider2D _box;
    private LaserVisual _visual;
    private float _damage = 2f;
    private float _damageCooldown = 0.5f;
    private float _lastHitTime = -99f;
    private float _width = 0.9f;
    #endregion

    #region Public Methods
    /// <summary>Lazeri kurar (bir kez): animasyonlu gorsel prefab'i orneklenir, collider hazirlanir.</summary>
    public void Setup(LaserVisual visualPrefab, float damage, float damageCooldown, float width)
    {
        _box = GetComponent<BoxCollider2D>();
        _box.isTrigger = true;
        _box.offset = new Vector2(0.5f, 0f); // beam ileri dogru (root'ta sprite yok, sadece collider)
        _box.size = new Vector2(1f, 1f);

        _damage = damage;
        _damageCooldown = damageCooldown;
        _width = width;

        if (visualPrefab != null)
        {
            _visual = Instantiate(visualPrefab);
            _visual.SetChargeMode(false, width); // ates modu + kalinligi collider'a yaklastir (bir kez)
            _visual.Hide();
        }
    }

    /// <summary>Isini origin'den, verilen dunya acisinda, verilen uzunlukta konumlandirir (gorsel + collider).</summary>
    public void SetBeam(Vector2 origin, float angleDeg, float length)
    {
        length = Mathf.Max(0.001f, length);

        // Collider (hasar alani)
        transform.position = origin;
        transform.rotation = Quaternion.Euler(0f, 0f, angleDeg);
        transform.localScale = new Vector3(length, _width, 1f);

        // Gorsel (animasyonlu lazer) — origin'den uca
        if (_visual != null)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector2 end = origin + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * length;
            _visual.Show();
            _visual.UpdateLaser(origin, end);
        }
    }

    /// <summary>Isini gizler (havuza donerken). Collider objesi ayrica SetActive(false) yapilir.</summary>
    public void HideVisual()
    {
        if (_visual != null) _visual.Hide();
    }
    #endregion

    #region Unity Callbacks
    private void OnDisable()
    {
        if (_visual != null) _visual.Hide();
    }

    private void OnDestroy()
    {
        if (_visual != null) Destroy(_visual.gameObject);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (Time.time < _lastHitTime + _damageCooldown) return;
        player cat = other.GetComponent<player>();
        if (cat == null) return;
        cat.TakeDamage(_damage);
        _lastHitTime = Time.time;
    }
    #endregion
}

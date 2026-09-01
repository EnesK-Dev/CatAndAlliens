using UnityEngine;

/// <summary>
/// Silah 2 — Boomerang. Menzilli: en yakin dusmana bumerang firlatir; ileri gidip geri doner, iki
/// yonde de hasar verir. Ayni anda ucabilecek bumerang sayisi currentCount ile sinirlidir —
/// TUTULMADAN (geri donmeden) yenisi atilmaz. Seviye: sayi (max 3), hiz, hasar artar.
/// </summary>
public class BoomerangWeapon : WeaponBase
{
    #region Serialized Fields
    [Header("Boomerang")]
    [SerializeField] private PlayerBoomerang boomerangPrefab;
    [SerializeField] private LayerMask enemyLayers;

    [Tooltip("Bu menzildeki en yakin dusmana nisan alir (yoksa yukari atar).")]
    [SerializeField] private float detectRange = 10f;

    [Tooltip("Bumerang bu kadar ileri gidip geri doner.")]
    [SerializeField] private float outDistance = 5f;

    [SerializeField] private float spin = 720f;
    [SerializeField] private float catchDistance = 0.6f;

    [Tooltip("Birden fazla bumerang atarken atislar arasi bekleme (ust uste cikmasin).")]
    [SerializeField] private float throwStagger = 0.25f;

    [Header("Seviye 1 Degerleri")]
    [SerializeField] private float baseSpeed = 10f;
    [SerializeField] private float baseDamage = 12f;

    [Header("Seviye Basi (upgrade)")]
    [Tooltip("Seviye -> ayni anda ucan bumerang sayisi (max 3). Ornek [1,2,3,3,3].")]
    [SerializeField] private int[] countByLevel = { 1, 2, 3, 3, 3 };
    [SerializeField] private float speedPerLevel = 1.5f;
    [SerializeField] private float damagePerLevel = 6f;
    #endregion

    #region Private Fields
    private int _inFlight;
    private float _nextThrowTime;
    private readonly Collider2D[] _hitBuffer = new Collider2D[32];
    #endregion

    #region Unity Callbacks
    private void Update()
    {
        if (boomerangPrefab == null) return;
        if (_inFlight >= CurrentCount()) return;    // hepsi havada — tutulmadan atma
        if (Time.time < _nextThrowTime) return;

        Throw(AimDirection());
        _nextThrowTime = Time.time + throwStagger;
    }
    #endregion

    #region Private Methods
    private int CurrentCount()
    {
        if (countByLevel == null || countByLevel.Length == 0) return 1;
        return countByLevel[Mathf.Clamp(Level - 1, 0, countByLevel.Length - 1)];
    }
    private float CurrentSpeed() => baseSpeed + speedPerLevel * Mathf.Max(0, Level - 1);
    private float CurrentDamage() => baseDamage + damagePerLevel * Mathf.Max(0, Level - 1);

    private Vector2 AimDirection()
    {
        Transform t = FindNearestEnemy();
        if (t != null)
        {
            Vector2 d = (Vector2)t.position - (Vector2)transform.position;
            if (d.sqrMagnitude > 0.0001f) return d.normalized;
        }
        return Vector2.up; // dusman yoksa varsayilan yon
    }

    private Transform FindNearestEnemy()
    {
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, detectRange, _hitBuffer, enemyLayers);
        Transform closest = null;
        float closestSqr = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Collider2D c = _hitBuffer[i];
            if (c == null) continue;
            float d = ((Vector2)c.transform.position - (Vector2)transform.position).sqrMagnitude;
            if (d < closestSqr) { closestSqr = d; closest = c.transform; }
        }
        return closest;
    }

    private void Throw(Vector2 dir)
    {
        PlayerBoomerang b = Instantiate(boomerangPrefab, transform.position, Quaternion.identity);
        _inFlight++;
        b.Launch(transform, dir, outDistance, CurrentSpeed(), CurrentDamage(), spin, catchDistance, OnBoomerangReturned);
    }

    private void OnBoomerangReturned()
    {
        _inFlight = Mathf.Max(0, _inFlight - 1); // tutuldu — yenisi atilabilir
    }
    #endregion
}

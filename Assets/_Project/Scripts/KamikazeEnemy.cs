using UnityEngine;

/// <summary>
/// Kamikaze dusman: oyuncuyu kovalar (EnemyController tabani) ve OLUNCE alan hasari verir (patlama).
/// Nuke/ulti ile olurse patlamaz (zincir onlenir — suppressed). Patlamanin GORSELI icin death frame'leri
/// (Inspector'daki Death Frames) patlama kareleri olarak atanabilir. Hasar player.TakeDamage uzerinden
/// gider (dash/pause immunitesi otomatik gecerli).
/// </summary>
public class KamikazeEnemy : EnemyController
{
    #region Serialized Fields
    [Header("Kamikaze Patlama")]
    [Tooltip("Patlamanin oyuncuya hasar verdigi yaricap (dunya birimi).")]
    [SerializeField] private float explosionRadius = 2f;

    [Tooltip("Patlamanin oyuncuya hasari (yarim-kalp birimi: 2 = 1 tam kalp).")]
    [SerializeField] private float explosionDamage = 3f;

    [Tooltip("Oyuncu layer'i — patlama sadece bunu vurur.")]
    [SerializeField] private LayerMask explosionPlayerLayer;

    [Tooltip("Kamikaze'ye OZEL olum/patlama kareleri. Atanirsa olunce bunlar oynar; bos ise base Death Frames kullanilir.")]
    [SerializeField] private Sprite[] explosionFrames;

    [Tooltip("Oyuncuya bu mesafeye gelince KENDINI PATLATIR (patlama alan hasari + yok olur). explosionRadius'tan kucuk olmali ki oyuncu patlamada olsun.")]
    [SerializeField] private float detonateDistance = 1.2f;
    #endregion

    #region Private Fields
    private static readonly Collider2D[] _overlap = new Collider2D[8];
    private bool _detonated;
    #endregion

    #region Unity Callbacks
    protected override void Update()
    {
        base.Update(); // taban: kovalama yonu + facing

        if (_detonated || EnemyFreeze.IsFrozen) return;
        Transform p = PlayerTransform;
        if (p == null) return;

        // Oyuncuya yeterince yaklasti -> kendini patlat (patlama OnDeath'te alan hasari verir, explosionFrames oynar, yok olur)
        if (Vector2.Distance(transform.position, p.position) <= detonateDistance)
        {
            _detonated = true;
            KillSelf(true);
        }
    }
    #endregion

    #region Overrides
    /// <summary>Olunce alan hasari (patlama). Nuke/ulti ile olduysa (suppressed) patlamaz.</summary>
    protected override void OnDeath(bool suppressed)
    {
        if (suppressed) return;

        SfxManager.Play(SfxId.KamikazeExplode); // patlama sesi

        int n = Physics2D.OverlapCircleNonAlloc(transform.position, explosionRadius, _overlap, explosionPlayerLayer);
        for (int i = 0; i < n; i++)
        {
            player cat = _overlap[i].GetComponent<player>();
            if (cat != null) cat.TakeDamage(explosionDamage);
        }
    }

    /// <summary>Kamikaze'ye ozel patlama kareleri atanmissa onlari oynat; yoksa base death frames.</summary>
    protected override Sprite[] GetDeathFrames()
        => (explosionFrames != null && explosionFrames.Length > 0) ? explosionFrames : base.GetDeathFrames();
    #endregion
}

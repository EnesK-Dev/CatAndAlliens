using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kamikaze boss (caster) saldiri modulu. Boss durur (BossController centerMode) ve 3 atak yapar; hepsi
/// KIRMIZI YUVARLAK telegraph -> 03 patlama efekti + alan hasari deseni:
///  Atak1: mapte buyuk kirmizi daireler, icinde birden fazla 03 patlar.
///  Atak2: bosstan disariya genisleyen halkalar; aralarinda GUVENLI BOSLUK var (oyuncu orada bekler), sonra 03 ile dolar.
///  Atak3: rastgele kirmizi daireler + 03 patlamalari.
/// Daireler ve patlamalar havuzlu (kod ile uretilir; prefab gerekmez). Gorsel: red_circle + 03 kareleri Inspector'dan.
/// </summary>
[RequireComponent(typeof(BossController))]
public class KamikazeBossAttack : MonoBehaviour
{
    #region Serialized Fields
    [Header("Gorsel")]
    [Tooltip("Kirmizi yuvarlak telegraph sprite'i (red_circle).")]
    [SerializeField] private Sprite redCircleSprite;
    [Tooltip("Patlama kareleri (03 spritesheet'inin dilimleri).")]
    [SerializeField] private Sprite[] explosionFrames;
    [SerializeField] private float explosionFrameRate = 14f;
    [Tooltip("Bir 03 patlamasinin dunya boyutu.")]
    [SerializeField] private float explosionSize = 1.6f;
    [Tooltip("03 patlama efektine uygulanan renk (kirmizi).")]
    [SerializeField] private Color explosionColor = new Color(1f, 0.28f, 0.2f, 1f);
    [SerializeField] private Color circleColor = new Color(1f, 0.2f, 0.2f, 0.8f);
    [SerializeField] private int circleSortingOrder = 3;
    [SerializeField] private int explosionSortingOrder = 6;

    [Header("Zamanlama")]
    [SerializeField] private float firstAttackDelay = 2f;
    [SerializeField] private float betweenAttacks = 2.5f;
    [Tooltip("Kirmizi daire telegraph suresi — bu surede oyuncu kacar.")]
    [SerializeField] private float warningTime = 1.1f;

    [Header("Hasar / Alan")]
    [SerializeField] private float damage = 3f;
    [SerializeField] private LayerMask playerLayer;
    [Tooltip("Oyuncu merkezli tehdit alani (atak1/atak3 daireleri burada).")]
    [SerializeField] private Vector2 areaHalfExtent = new Vector2(11f, 7.5f);

    [Header("Atak 1 — Buyuk daireler + coklu 03")]
    [SerializeField] private int atk1CircleCount = 4;
    [SerializeField] private float atk1Radius = 3f;
    [SerializeField] private int atk1InnerExplosions = 4;

    [Header("Atak 2 — Bosstan genisleyen halka (bosluklu)")]
    [SerializeField] private int atk2Rings = 3;
    [SerializeField] private float atk2FirstRadius = 3f;
    [SerializeField] private float atk2RingGap = 2.6f;
    [Tooltip("Halka basina MINIMUM daire (ic halkalar icin taban).")]
    [SerializeField] private int atk2PerRing = 12;
    [Tooltip("Halkadaki daireler arasi YAY mesafesi (dunya birimi). Kucuk = daha cok daire, halka daha DOLU gorunur. Sayi yaricapla orantili artar (dis halka daha cok daire).")]
    [SerializeField] private float atk2CircleSpacing = 1.8f;
    [Tooltip("Guvenli bosluk yari-genisligi (derece). Oyuncu bu koridorda bekler.")]
    [SerializeField] private float atk2GapHalfWidth = 32f;
    [SerializeField] private float atk2CircleRadius = 1.1f;
    [SerializeField] private float atk2RingDelay = 0.45f;

    [Header("Atak 3 — Random")]
    [SerializeField] private int atk3Count = 12;
    [SerializeField] private float atk3Radius = 1.6f;
    [SerializeField] private float atk3Interval = 0.18f;
    [SerializeField] private int atk3InnerExplosions = 2;
    #endregion

    #region Private Fields
    private BossController _boss;
    private Transform _player;
    private Coroutine _loop;
    private readonly List<SpriteRenderer> _circlePool = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> _explosionPool = new List<SpriteRenderer>();
    private static readonly Collider2D[] _overlap = new Collider2D[8];
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        _boss = GetComponent<BossController>();
        player p = FindFirstObjectByType<player>();
        if (p != null) _player = p.transform;
    }

    private void OnEnable() { _loop = StartCoroutine(AttackLoop()); }
    private void OnDisable() { if (_loop != null) StopCoroutine(_loop); if (_boss != null) _boss.SetExternalAttacking(false); HideAll(); }
    #endregion

    #region Attack Loop
    private IEnumerator AttackLoop()
    {
        yield return new WaitForSeconds(firstAttackDelay);
        int last = -1;
        while (!Dead())
        {
            int pick = Random.Range(0, 3);
            if (pick == last) pick = (pick + 1) % 3;
            last = pick;

            _boss.SetExternalAttacking(true); // atak sirasinda boss YURUMESIN (sabit dur)
            switch (pick)
            {
                case 0: yield return Attack1_BigCircles(); break;
                case 1: yield return Attack2_ExpandingRings(); break;
                default: yield return Attack3_Random(); break;
            }
            _boss.SetExternalAttacking(false); // atak bitti -> boss yine yurusun
            if (Dead()) break;
            yield return new WaitForSeconds(betweenAttacks);
        }
        if (_boss != null) _boss.SetExternalAttacking(false);
        HideAll();
    }
    #endregion

    #region Attacks
    private IEnumerator Attack1_BigCircles()
    {
        for (int i = 0; i < atk1CircleCount; i++)
        {
            Vector2 pos = PlayerCenter() + RandomInArea();
            StartCoroutine(BlastRoutine(pos, atk1Radius, atk1InnerExplosions));
        }
        yield return new WaitForSeconds(warningTime + ExplosionDuration() + 0.2f);
    }

    private IEnumerator Attack2_ExpandingRings()
    {
        float gapCenter = Random.Range(0f, 360f); // guvenli koridor yonu (halkalar boyunca sabit)
        for (int ring = 0; ring < atk2Rings; ring++)
        {
            if (Dead()) yield break;
            float r = atk2FirstRadius + ring * atk2RingGap;
            // Daire sayisi yaricapla orantili — dis halkalar daha COK daire (yay mesafesi sabit) -> halka dolu gorunur
            int count = Mathf.Max(atk2PerRing, Mathf.RoundToInt((2f * Mathf.PI * r) / Mathf.Max(0.3f, atk2CircleSpacing)));
            float step = 360f / count;
            for (int k = 0; k < count; k++)
            {
                float ang = k * step;
                if (Mathf.Abs(Mathf.DeltaAngle(ang, gapCenter)) <= atk2GapHalfWidth) continue; // guvenli bosluk
                float rad = ang * Mathf.Deg2Rad;
                Vector2 pos = (Vector2)transform.position + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * r;
                StartCoroutine(BlastRoutine(pos, atk2CircleRadius, 1));
            }
            yield return new WaitForSeconds(atk2RingDelay);
        }
        yield return new WaitForSeconds(warningTime + ExplosionDuration());
    }

    private IEnumerator Attack3_Random()
    {
        for (int i = 0; i < atk3Count; i++)
        {
            if (Dead()) yield break;
            Vector2 pos = PlayerCenter() + RandomInArea();
            StartCoroutine(BlastRoutine(pos, atk3Radius, atk3InnerExplosions));
            yield return new WaitForSeconds(atk3Interval);
        }
        yield return new WaitForSeconds(warningTime + ExplosionDuration());
    }
    #endregion

    #region Blast (telegraph -> patlama)
    /// <summary>Bir noktada: kirmizi daire telegraph -> alan hasari + coklu 03 patlama.</summary>
    private IEnumerator BlastRoutine(Vector2 center, float radius, int innerExplosions)
    {
        SpriteRenderer circle = GetCircle();
        circle.transform.position = center;
        float spriteW = redCircleSprite != null ? redCircleSprite.bounds.size.x : 1f;
        // red_circle YUMUSAK gradyan (kenarlari seffafa doner) -> gorunur "belirgin kirmizi" kabaca bounds
        // yaricapinin ~%50'si. Bu yuzden: patlamalari merkeze topla (spread), HASAR'i gorunur cekirdege (coverR)
        // esitle ve sprite'i 2x buyut. Sonuc: patlamalar kirmizinin icinde, gorunur kirmizinin DISINDA hasar YOK.
        float spread = radius * 0.5f;                     // patlama sacilimi (daha siki)
        float coverR = spread + explosionSize * 0.5f;     // patlama gorsellerinin ulastigi yaricap = hasar
        const float RedCoreFraction = 0.5f;               // gorunur kirmizinin sprite bounds'una orani
        float boundsR = coverR / RedCoreFraction;         // glow bounds yaricapi (buyur ki cekirdek coverR olsun)
        circle.transform.localScale = Vector3.one * (spriteW > 0.001f ? (boundsR * 2f) / spriteW : 1f);
        circle.gameObject.SetActive(true);

        float t = 0f;
        while (t < warningTime)
        {
            if (Dead()) { circle.gameObject.SetActive(false); yield break; }
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 12f);
            Color c = circleColor; c.a = circleColor.a * Mathf.Lerp(0.45f, 1f, pulse);
            circle.color = c;
            t += Time.deltaTime;
            yield return null;
        }
        circle.gameObject.SetActive(false); // telegraph bitti

        // Alan hasari (bir kez)
        DamagePlayer(center, coverR); // hasar = gorunur kirmizi cekirdek (patlamalari kapsar, glow'un ICINDE)
        SfxManager.Play(SfxId.KamikazeExplode);

        // Icinde birden fazla 03 patlamasi
        for (int i = 0; i < innerExplosions; i++)
        {
            Vector2 p = center + Random.insideUnitCircle * spread;
            StartCoroutine(ExplosionRoutine(p));
        }
    }

    private IEnumerator ExplosionRoutine(Vector2 pos)
    {
        if (explosionFrames == null || explosionFrames.Length == 0) yield break;
        SpriteRenderer e = GetExplosion();
        e.transform.position = pos;
        float sw = explosionFrames[0].bounds.size.x;
        e.transform.localScale = Vector3.one * (sw > 0.001f ? explosionSize / sw : 1f);
        e.color = explosionColor; // 03 patlamasi kirmizi
        e.gameObject.SetActive(true);

        float ft = 1f / Mathf.Max(1f, explosionFrameRate);
        for (int i = 0; i < explosionFrames.Length; i++)
        {
            e.sprite = explosionFrames[i];
            yield return new WaitForSeconds(ft);
        }
        e.gameObject.SetActive(false);
    }

    private void DamagePlayer(Vector2 center, float radius)
    {
        int n = Physics2D.OverlapCircleNonAlloc(center, radius, _overlap, playerLayer);
        for (int i = 0; i < n; i++)
        {
            player cat = _overlap[i].GetComponent<player>();
            if (cat != null) cat.TakeDamage(damage);
        }
    }
    #endregion

    #region Helpers / Pool
    private bool Dead() => _boss == null || _boss.IsDying;
    private float ExplosionDuration() => (explosionFrames != null && explosionFrames.Length > 0)
        ? explosionFrames.Length / Mathf.Max(1f, explosionFrameRate) : 0.3f;
    private Vector2 PlayerCenter() => _player != null ? (Vector2)_player.position : (Vector2)transform.position;
    private Vector2 RandomInArea() => new Vector2(Random.Range(-areaHalfExtent.x, areaHalfExtent.x), Random.Range(-areaHalfExtent.y, areaHalfExtent.y));

    private SpriteRenderer GetCircle()
    {
        foreach (var c in _circlePool) if (c != null && !c.gameObject.activeSelf) return c;
        var go = new GameObject("KamikazeCircle");
        go.transform.SetParent(transform);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = redCircleSprite;
        sr.color = circleColor;
        sr.sortingOrder = circleSortingOrder;
        go.SetActive(false);
        _circlePool.Add(sr);
        return sr;
    }

    private SpriteRenderer GetExplosion()
    {
        foreach (var e in _explosionPool) if (e != null && !e.gameObject.activeSelf) return e;
        var go = new GameObject("KamikazeExplosion");
        go.transform.SetParent(transform);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = explosionSortingOrder;
        go.SetActive(false);
        _explosionPool.Add(sr);
        return sr;
    }

    private void HideAll()
    {
        foreach (var c in _circlePool) if (c != null) c.gameObject.SetActive(false);
        foreach (var e in _explosionPool) if (e != null) e.gameObject.SetActive(false);
    }
    #endregion
}

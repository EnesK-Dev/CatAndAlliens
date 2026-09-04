using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lazer boss (Boss 2) saldiri modulu. Boss ortada SABIT durur (BossController centerMode) ve sirayla
/// 3 lazer atagi yapar. Isinlar uzun sure ekranda kalir (kucuk dusmanlarin lazerinden farkli).
/// Atak 1: tek lazer, buyuyunce oyuncuyu takip eder (kacilir). Atak 2: 4 yonden (+x/-x/+y/-y) lazer
/// cikip doner (saat yonu / tersi). Atak 3: oyuncuyu 2 lazer arasina alir, lazerler birbirine yaklasir
/// (tek kacis: dash ile arasindan gecmek — dash immunitesi). BossLaser building-block'unu havuzlar.
/// </summary>
[RequireComponent(typeof(BossController))]
public class BossLaserAttack : MonoBehaviour
{
    #region Serialized Fields
    [Header("Lazer Gorsel/Hasar")]
    [Tooltip("Animasyonlu lazer gorseli (Laser.prefab / LaserVisual). Bos ise gorsel cizilmez, sadece collider.")]
    [SerializeField] private LaserVisual laserVisualPrefab;

    [Tooltip("Gorsel isin kalinligi (LaserVisual).")]
    [SerializeField] private float laserWidth = 0.9f;

    [Tooltip("Collider kalinligi = gorsel kalinlik * bu. 1'den kucuk = HASAR ALANI gorselden ince (kenardan siyirinca vurmaz). Collider'i kucultmek icin bunu dusur.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float colliderWidthScale = 0.6f;
    [Tooltip("Isin uzunlugu — ekrani kaplayacak kadar buyuk olmali.")]
    [SerializeField] private float laserLength = 20f;
    [SerializeField] private float laserDamage = 2f;
    [SerializeField] private float laserHitCooldown = 0.5f;

    [Header("Zamanlama")]
    [SerializeField] private float firstAttackDelay = 2f;
    [SerializeField] private float betweenAttacks = 2.5f;

    [Tooltip("Ates ONCESI ince 'sarj' isininin (telegraph) suresi — mini-lazer gibi uyari. Bu surede hasar YOK.")]
    [SerializeField] private float telegraphDuration = 0.6f;

    [Header("Atak 1 — Takip Eden Lazer")]
    [SerializeField] private float atk1GrowTime = 0.6f;
    [SerializeField] private float atk1FollowTime = 4.5f;
    [Tooltip("Takip donus hizi (derece/sn). Dusuk = daha kolay kacilir.")]
    [SerializeField] private float atk1TurnSpeed = 55f;

    [Header("Atak 2 — 4 Yon Donen")]
    [SerializeField] private float atk2Duration = 6f;
    [SerializeField] private float atk2RotSpeed = 45f;

    [Header("Atak 3 — Copstik (2 lazer bossdan, kapanan)")]
    [Tooltip("Baslangic YARI-aci (derece) — bossdan cikan iki isinin oyuncuyu ortaya alan kama genisligi.")]
    [SerializeField] private float atk3Spread = 28f;
    [Tooltip("Kapaninca ulasilan yari-aci (derece). Kucuk = neredeyse tam kapanir (copstik).")]
    [SerializeField] private float atk3CloseSpread = 4f;

    [Tooltip("Kama KAPANMA hizi (derece/sn) — SABIT hiz. Buyuk = daha hizli kapanir.")]
    [SerializeField] private float atk3CloseSpeed = 14f;
    [Tooltip("Kapanirken merkezin oyuncuyu takip hizi (derece/sn). 0 = takip yok (sadece kapanir).")]
    [SerializeField] private float atk3CenterTrackSpeed = 35f;

    [Tooltip("(Kullanilmiyor — eski hold tabanli kapanmadan kaldi.)")]
    [SerializeField] private float atk3CloseTime = 2.5f;
    [Tooltip("(Kullanilmiyor — artik hold yok, direkt kapanip biter.)")]
    [SerializeField] private float atk3HoldTime = 0.7f;
    #endregion

    #region Private Fields
    private BossController _boss;
    private int _bossSortingOrder = 1; // boss sprite sorting order — lazer bunun ALTINA (arkasina) cizilir
    private Transform _player;
    private readonly List<BossLaser> _pool = new List<BossLaser>();
    private Coroutine _loop;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        _boss = GetComponent<BossController>();
        var bossSr = GetComponent<SpriteRenderer>();
        if (bossSr != null) _bossSortingOrder = bossSr.sortingOrder;
        player p = FindFirstObjectByType<player>();
        if (p != null) _player = p.transform;
    }

    private void OnEnable() { _loop = StartCoroutine(AttackLoop()); }

    private void OnDisable()
    {
        if (_loop != null) StopCoroutine(_loop);
        HideAll();
    }
    #endregion

    #region Attack Loop
    private IEnumerator AttackLoop()
    {
        yield return new WaitForSeconds(firstAttackDelay);

        int last = -1;
        while (_boss != null && !_boss.IsDying)
        {
            int pick = Random.Range(0, 3);
            if (pick == last) pick = (pick + 1) % 3; // ayni atagi ust uste tekrarlama
            last = pick;

            switch (pick)
            {
                case 0: yield return Attack1_Follow(); break;
                case 1: yield return Attack2_FourWayRotate(Random.value < 0.5f); break; // cw/ccw rastgele
                case 2: yield return Attack3_Pincer(); break;
            }
            if (_boss == null || _boss.IsDying) break;
            yield return new WaitForSeconds(betweenAttacks);
        }
        HideAll();
    }
    #endregion

    #region Attacks
    /// <summary>Tek lazer buyur, sonra oyuncuyu (sinirli donus hiziyla) takip eder. Oyuncu isinin disina kacar.</summary>
    private IEnumerator Attack1_Follow()
    {
        BossLaser laser = GetLaser();
        laser.BeginCharge(); // ince telegraph — henuz hasar yok
        Vector2 origin = transform.position;
        float angle = AngleToPlayer(origin);

        // TELEGRAPH: ince isin oyuncuyu nisan alir (mini-lazer gibi uyari)
        float ch = 0f;
        while (ch < telegraphDuration)
        {
            if (Dead()) { laser.gameObject.SetActive(false); yield break; }
            origin = transform.position;
            angle = Mathf.MoveTowardsAngle(angle, AngleToPlayer(origin), atk1TurnSpeed * Time.deltaTime);
            laser.SetBeam(origin, angle, laserLength);
            ch += Time.deltaTime;
            yield return null;
        }

        laser.Fire(); // tam isin + hasar
        SfxManager.Play(SfxId.LaserFire);

        float t = 0f;
        while (t < atk1FollowTime)
        {
            if (Dead()) break;
            origin = transform.position;
            float target = AngleToPlayer(origin);
            angle = Mathf.MoveTowardsAngle(angle, target, atk1TurnSpeed * Time.deltaTime);
            laser.SetBeam(origin, angle, laserLength);
            t += Time.deltaTime;
            yield return null;
        }
        laser.gameObject.SetActive(false);
    }

    /// <summary>Merkezden 4 yon (+x/-x/+y/-y) lazer cikar ve hep birlikte doner (cw/ccw). Oyuncu bosluklarda kalir.</summary>
    private IEnumerator Attack2_FourWayRotate(bool clockwise)
    {
        var lasers = new BossLaser[4];
        for (int i = 0; i < 4; i++) { lasers[i] = GetLaser(); lasers[i].BeginCharge(); }

        float baseAngle = 0f;
        float dir = clockwise ? -1f : 1f;

        // TELEGRAPH: ince 4 isin belirir (hasar yok)
        float ch = 0f;
        while (ch < telegraphDuration)
        {
            if (Dead()) { DeactivateAll(lasers); yield break; }
            Set4(lasers, transform.position, baseAngle, laserLength);
            ch += Time.deltaTime;
            yield return null;
        }

        foreach (var l in lasers) l.Fire(); // tam isin + hasar
        SfxManager.Play(SfxId.LaserFire);

        float t = 0f;
        while (t < atk2Duration)
        {
            if (Dead()) break;
            baseAngle += dir * atk2RotSpeed * Time.deltaTime;
            Set4(lasers, transform.position, baseAngle, laserLength);
            t += Time.deltaTime;
            yield return null;
        }
        DeactivateAll(lasers);
    }

    /// <summary>
    /// Copstik: BOSSDAN cikan 2 isin oyuncuyu ortaya alan bir kama olusturur, sonra aralarindaki aci
    /// KAPANIR (copstik gibi) — oyuncu sikisir. Isinlar bossdan cikar (mapte spawn olmaz). Kacis: dash.
    /// </summary>
    private IEnumerator Attack3_Pincer()
    {
        if (_player == null) yield break;

        BossLaser a = GetLaser(); a.BeginCharge();
        BossLaser b = GetLaser(); b.BeginCharge();

        Vector2 origin = transform.position;
        float centerAngle = AngleToPlayer(origin);

        // TELEGRAPH: ince kama (2 isin) oyuncuyu ortaya alir (hasar yok)
        float ct = 0f;
        while (ct < telegraphDuration)
        {
            if (Dead()) { a.gameObject.SetActive(false); b.gameObject.SetActive(false); yield break; }
            origin = transform.position;
            centerAngle = AngleToPlayer(origin);
            a.SetBeam(origin, centerAngle + atk3Spread, laserLength);
            b.SetBeam(origin, centerAngle - atk3Spread, laserLength);
            ct += Time.deltaTime;
            yield return null;
        }

        a.Fire(); b.Fire(); // tam isin + hasar
        SfxManager.Play(SfxId.LaserFire);

        // KAPANMA: kama SABIT HIZLA (deg/sn) direkt kapanir — bekleme/hold YOK. Kacis: dash ile isinlarin arasindan gec.
        float s = atk3Spread;
        while (s > atk3CloseSpread + 0.01f)
        {
            if (Dead()) break;
            origin = transform.position;
            centerAngle = Mathf.MoveTowardsAngle(centerAngle, AngleToPlayer(origin), atk3CenterTrackSpeed * Time.deltaTime);
            s = Mathf.MoveTowards(s, atk3CloseSpread, atk3CloseSpeed * Time.deltaTime); // sabit hizli kapanma
            a.SetBeam(origin, centerAngle + s, laserLength);
            b.SetBeam(origin, centerAngle - s, laserLength);
            yield return null;
        }

        a.gameObject.SetActive(false);
        b.gameObject.SetActive(false);
    }
    #endregion

    #region Helpers
    private bool Dead() => _boss == null || _boss.IsDying;

    private float AngleToPlayer(Vector2 origin)
    {
        if (_player == null) return 0f;
        Vector2 d = (Vector2)_player.position - origin;
        if (d.sqrMagnitude < 0.0001f) return 0f;
        return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
    }

    private void Set4(BossLaser[] ls, Vector2 origin, float baseAngle, float len)
    {
        for (int i = 0; i < 4; i++) ls[i].SetBeam(origin, baseAngle + i * 90f, len);
    }

    private void DeactivateAll(BossLaser[] ls)
    {
        foreach (var l in ls) if (l != null) l.gameObject.SetActive(false);
    }

    private BossLaser GetLaser()
    {
        foreach (var l in _pool)
            if (l != null && !l.gameObject.activeSelf) { l.gameObject.SetActive(true); return l; }

        var go = new GameObject("BossLaser");
        var laser = go.AddComponent<BossLaser>(); // RequireComponent BoxCollider2D'yi otomatik ekler
        laser.Setup(laserVisualPrefab, laserDamage, laserHitCooldown, laserWidth, colliderWidthScale, _bossSortingOrder);
        _pool.Add(laser);
        return laser;
    }

    private void HideAll()
    {
        foreach (var l in _pool) if (l != null) l.gameObject.SetActive(false);
    }
    #endregion
}

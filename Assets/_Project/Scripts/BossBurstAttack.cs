using System.Collections;
using UnityEngine;

/// <summary>
/// Burst boss (Boss 3) saldiri modulu. Boss ortada SABIT durur (BossController centerMode) ve sirayla
/// 3 mermi atagi yapar. Mermiler mevcut BurstBullet prefab'indan (EnemyBullet) uretilir.
/// Atak 1: oyuncuyu TAKIP eden seri atislar (her atis anlik konuma nisan alir), buyuk mermi, 7'li seriler.
/// Atak 2: 360 derece, her yone bir mermi (15-20), ayni anda (birkac ring).
/// Atak 3: her yone SUREKLI atis ama tek bir "guvenli koridor" acik (o yone ates yok) — oyuncu orada kurtulur.
/// </summary>
[RequireComponent(typeof(BossController))]
public class BossBurstAttack : MonoBehaviour
{
    #region Serialized Fields
    [Header("Mermi")]
    [Tooltip("Uretilecek mermi prefab'i (BurstBullet / EnemyBullet).")]
    [SerializeField] private EnemyBullet bulletPrefab;

    [Tooltip("Ring/koridor mermilerinin olcegi (1 = prefab).")]
    [SerializeField] private float bulletScale = 1.2f;

    [Tooltip("Atak 1 takip mermilerinin olcegi (daha buyuk).")]
    [SerializeField] private float bigBulletScale = 2f;

    [Header("Zamanlama")]
    [SerializeField] private float firstAttackDelay = 2f;
    [SerializeField] private float betweenAttacks = 2.5f;

    [Header("Atak 1 — Takip Eden Seri")]
    [SerializeField] private int atk1Bursts = 3;
    [SerializeField] private int atk1PerBurst = 7;
    [SerializeField] private float atk1ShotInterval = 0.12f;
    [SerializeField] private float atk1BurstGap = 0.6f;

    [Header("Atak 2 — 360 Ring")]
    [Tooltip("Ring basina mermi (her yone bir tane). 15-20 arasi.")]
    [SerializeField] private int atk2Count = 18;
    [SerializeField] private int atk2Rings = 3;
    [SerializeField] private float atk2RingGap = 0.5f;

    [Header("Atak 3 — Guvenli Koridor")]
    [Tooltip("Ring basina mermi (koridor haric).")]
    [SerializeField] private int atk3Count = 16;
    [SerializeField] private float atk3Duration = 5f;
    [SerializeField] private float atk3FireInterval = 0.32f;
    [Tooltip("Ates ETMEDIGI guvenli koridorun yari-genisligi (derece).")]
    [SerializeField] private float atk3CorridorHalfWidth = 26f;
    [Tooltip("Koridorun donme hizi (derece/sn). 0 = sabit koridor.")]
    [SerializeField] private float atk3CorridorRotSpeed = 28f;

    [Tooltip("Guvenli koridoru gosteren OK — boss'tan bu kadar uzakta (dunya birimi). Boss renginde.")]
    [SerializeField] private float corridorArrowDistance = 2.5f;
    [Tooltip("Koridor okunun boyutu.")]
    [SerializeField] private float corridorArrowSize = 1.3f;
    #endregion

    #region Private Fields
    private BossController _boss;
    private Transform _player;
    private Coroutine _loop;
    private Transform _corridorArrow; // guvenli koridor yon oku (runtime mesh, boss renginde)
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        _boss = GetComponent<BossController>();
        player p = FindFirstObjectByType<player>();
        if (p != null) _player = p.transform;
    }

    private void OnEnable() { _loop = StartCoroutine(AttackLoop()); }
    private void OnDisable() { if (_loop != null) StopCoroutine(_loop); HideCorridorArrow(); }
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
                case 0: yield return Attack1_Following(); break;
                case 1: yield return Attack2_Ring360(); break;
                case 2: yield return Attack3_SafeCorridor(); break;
            }
            if (Dead()) break;
            yield return new WaitForSeconds(betweenAttacks);
        }
    }
    #endregion

    #region Attacks
    /// <summary>Oyuncuyu takip eden seri atislar: her atis anlik oyuncu konumuna nisan alir (kacinca akis takip eder).</summary>
    private IEnumerator Attack1_Following()
    {
        for (int b = 0; b < atk1Bursts; b++)
        {
            for (int i = 0; i < atk1PerBurst; i++)
            {
                if (Dead()) yield break;
                FireAtPlayer(bigBulletScale);
                SfxManager.Play(SfxId.BurstShot);
                yield return new WaitForSeconds(atk1ShotInterval);
            }
            if (Dead()) yield break;
            yield return new WaitForSeconds(atk1BurstGap);
        }
    }

    /// <summary>360 derece: her yone bir mermi (atk2Count), birkac ring ard arda.</summary>
    private IEnumerator Attack2_Ring360()
    {
        for (int r = 0; r < atk2Rings; r++)
        {
            if (Dead()) yield break;
            float offset = r * (180f / Mathf.Max(1, atk2Count)); // ring'ler hafif kaysin (araya girsin)
            FireRing(atk2Count, offset, bulletScale, -1f, 0f);
            SfxManager.Play(SfxId.BurstShot);
            yield return new WaitForSeconds(atk2RingGap);
        }
    }

    /// <summary>Her yone SUREKLI atis, ama tek bir guvenli koridor (o yone ates yok). Koridor yavas doner; oyuncu orada durur.</summary>
    private IEnumerator Attack3_SafeCorridor()
    {
        EnsureCorridorArrow();
        float corridor = Random.Range(0f, 360f);
        float t = 0f, sinceFire = atk3FireInterval; // ilk karede ates
        if (_corridorArrow != null) _corridorArrow.gameObject.SetActive(true);

        while (t < atk3Duration)
        {
            if (Dead()) { HideCorridorArrow(); yield break; }

            corridor += atk3CorridorRotSpeed * Time.deltaTime; // koridor SMOOTH doner (kare-kare)
            UpdateCorridorArrow(corridor);                      // oku guvenli yone (boss renginde) yerlestir

            sinceFire += Time.deltaTime;
            if (sinceFire >= atk3FireInterval)
            {
                sinceFire -= atk3FireInterval;
                FireRing(atk3Count, 0f, bulletScale, corridor, atk3CorridorHalfWidth);
                SfxManager.Play(SfxId.BurstShot);
            }

            t += Time.deltaTime;
            yield return null;
        }
        HideCorridorArrow();
    }
    #endregion

    #region Helpers
    private bool Dead() => _boss == null || _boss.IsDying;

    /// <summary>Oyuncunun ANLIK konumuna bir mermi atar (takip hissi).</summary>
    private void FireAtPlayer(float scale)
    {
        if (bulletPrefab == null) return;
        Vector2 origin = transform.position;
        Vector2 target = _player != null ? (Vector2)_player.position : origin + Vector2.right;
        SpawnBullet(origin, target, scale);
    }

    /// <summary>
    /// Merkezden 'count' yone mermi atar. corridorAngle >= 0 ise o yondeki (yari-genislik) koridora ates edilmez.
    /// </summary>
    private void FireRing(int count, float offsetDeg, float scale, float corridorAngle, float corridorHalf)
    {
        if (bulletPrefab == null || count <= 0) return;
        Vector2 origin = transform.position;
        float step = 360f / count;
        for (int i = 0; i < count; i++)
        {
            float ang = offsetDeg + i * step;
            if (corridorAngle >= 0f && Mathf.Abs(Mathf.DeltaAngle(ang, corridorAngle)) <= corridorHalf)
                continue; // guvenli koridor — ates yok
            float rad = ang * Mathf.Deg2Rad;
            Vector2 target = origin + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * 5f;
            SpawnBullet(origin, target, scale);
        }
    }

    private void SpawnBullet(Vector2 origin, Vector2 target, float scale)
    {
        EnemyBullet b = Instantiate(bulletPrefab, origin, Quaternion.identity);
        if (scale != 1f) b.transform.localScale *= scale;
        b.Initialize(target); // hedefe dogru hiz verir
    }

    /// <summary>Guvenli koridor yon okunu (koddan mesh ucgen, BOSS RENGINDE) bir kez olusturur — Sandevistan telegraph'i gibi.</summary>
    private void EnsureCorridorArrow()
    {
        if (_corridorArrow != null) return;

        var go = new GameObject("CorridorArrow");
        go.transform.SetParent(transform, false);
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();

        float len = corridorArrowSize, halfW = corridorArrowSize * 0.7f;
        var mesh = new Mesh();
        mesh.vertices = new[] { new Vector3(len, 0f, 0f), new Vector3(0f, halfW, 0f), new Vector3(0f, -halfW, 0f) };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 1 }; // cift tarafli
        Color c = _boss != null ? _boss.BaseColor : Color.white;
        mesh.colors = new[] { c, c, c };
        mesh.RecalculateBounds();
        mf.sharedMesh = mesh;

        Shader sh = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        var mat = new Material(sh) { color = c };
        mr.sharedMaterial = mat;
        var sr = _boss != null ? _boss.GetComponent<SpriteRenderer>() : null;
        if (sr != null) mr.sortingLayerID = sr.sortingLayerID;
        mr.sortingOrder = 4; // mermilerin/karakterin ustunde gorunur

        go.SetActive(false);
        _corridorArrow = go.transform;
    }

    /// <summary>Oku guvenli koridor yonune (aci) yerlestirir — boss'tan corridorArrowDistance uzakta, o yone bakar.</summary>
    private void UpdateCorridorArrow(float angleDeg)
    {
        if (_corridorArrow == null) return;
        float rad = angleDeg * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        _corridorArrow.position = (Vector2)transform.position + dir * corridorArrowDistance;
        _corridorArrow.rotation = Quaternion.Euler(0f, 0f, angleDeg);
    }

    private void HideCorridorArrow()
    {
        if (_corridorArrow != null) _corridorArrow.gameObject.SetActive(false);
    }
    #endregion
}

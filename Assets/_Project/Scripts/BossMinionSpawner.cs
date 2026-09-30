using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss'un periyodik olarak KENDI TURUNDEN minion spawnlamasi. Minion boss'un merkezinde dogar,
/// oyuncuya bakan "yasak koni" HARIC bir yone knockback ile firlatilir ("bosstan cikiyor" hissi),
/// sonra kendi AI'sina donup oyuncuyu kovalar. BossController OPSIYONEL: varsa (birthPause>0)
/// firlatma aninda kisa bir dogum-duraklamasi yapar; SplitterBoss gibi EnemyController-tabanli
/// bosslarda da calisir. Ayni anda canli minion sayisi maxAliveMinions ile sinirli (perf + denge).
/// </summary>
public class BossMinionSpawner : MonoBehaviour
{
    #region Serialized Fields
    [Header("Minion")]
    [Tooltip("Bu boss'un KENDI turunden dusman prefab'i (dash->Enemy, lazer->EliteEnemy vb.).")]
    [SerializeField] private GameObject minionPrefab;

    [Header("Zamanlama")]
    [Tooltip("Boss spawn olduktan sonra ilk dalgaya kadar sn.")]
    [SerializeField] private float firstDelay = 4f;
    [Tooltip("Dalgalar arasi sn.")]
    [SerializeField] private float interval = 6f;
    [Tooltip("Bir dalgada en az kac minion.")]
    [SerializeField] private int minPerWave = 1;
    [Tooltip("Bir dalgada en fazla kac minion.")]
    [SerializeField] private int maxPerWave = 2;
    [Tooltip("Ayni anda canli tutulacak MAX minion. Asilirsa dalga atlanir (ekran/zorluk patlamasin).")]
    [SerializeField] private int maxAliveMinions = 6;

    [Header("Firlatma (bosstan cikma efekti)")]
    [Tooltip("Minion boss'tan disari itilme hizi.")]
    [SerializeField] private float launchSpeed = 22f;
    [Tooltip("Itmenin suresi (sn); sonra minion kendi AI'sina doner.")]
    [SerializeField] private float launchDuration = 0.5f;
    [Tooltip("Oyuncu yonunden bu kadar (derece) UZAKTA at -> tam ustune gelmesin (kucuk = daha ortalanmis, oyuncuya yakin).")]
    [SerializeField] private float safeConeDegrees = 20f;
    [Tooltip("Oyuncu yonu etrafinda firlatma konisinin YARI-genisligi (derece). Minion oyuncu yonunde +-[safeCone, spread] aciyla atilir.")]
    [SerializeField] private float spreadDegrees = 60f;

    [Tooltip("Minion boss merkezinden firlatma yonunde bu kadar UZAKTA dogar (baslangicta ic ice/boss icinde durmasin).")]
    [SerializeField] private float spawnOffset = 1.5f;

    [Header("Dogum Gorseli")]
    [Tooltip("Minion bu olcek carpaniyla dogup 1'e buyur (icinden cikma hissi). 1 = pop yok.")]
    [Range(0.1f, 1f)] [SerializeField] private float spawnStartScale = 0.4f;
    [SerializeField] private float scalePopDuration = 0.22f;
    [Tooltip("BossController VARSA firlatma aninda bu kadar sn hareketi durdurur (dogum telegraph'i). 0 = durdurma (varsayilan; diger atak modulleriyle cakismasin).")]
    [SerializeField] private float birthPause = 0f;

    [Header("Sandevistan Izi (firlatirken hayalet iz - BOSSUN KENDI RENGINDE)")]
    [Tooltip("Firlatma boyunca minion'un arkasinda hayalet iz birak (Cyberpunk Sandevistan). Kapatmak icin false.")]
    [SerializeField] private bool launchAfterImage = true;
    [Range(0f, 1f)] [Tooltip("Hayalet baslangic saydamligi.")]
    [SerializeField] private float afterImageAlpha = 0.55f;
    [Tooltip("Her hayaletin solma suresi (sn).")]
    [SerializeField] private float afterImageFadeTime = 0.35f;
    [Tooltip("Kac saniyede bir hayalet birakilsin (kucuk = daha sik/yogun iz).")]
    [SerializeField] private float afterImageInterval = 0.045f;
    #endregion

    #region Private Fields
    private BossController _boss;   // opsiyonel (SplitterBoss'ta null)
    private Transform _player;
    private Coroutine _loop;
    private readonly List<GameObject> _alive = new List<GameObject>();
    #endregion

    #region Unity Callbacks
    private void OnEnable()
    {
        _boss = GetComponent<BossController>();
        if (_player == null)
        {
            var p = FindFirstObjectByType<player>();
            if (p != null) _player = p.transform;
        }
        _loop = StartCoroutine(SpawnLoop());
    }

    private void OnDisable()
    {
        if (_loop != null) StopCoroutine(_loop);
        _loop = null;
        _alive.Clear(); // minion referanslarini birak; minion'lar sahnede kendi hayatlarini surdurur
    }
    #endregion

    #region Private Methods
    /// <summary>Ilk gecikme sonrasi periyodik dalga dongusu.</summary>
    private IEnumerator SpawnLoop()
    {
        yield return new WaitForSeconds(firstDelay);
        while (true)
        {
            if (minionPrefab != null && CountAlive() < maxAliveMinions)
                yield return SpawnWave();
            yield return new WaitForSeconds(interval);
        }
    }

    /// <summary>Bir dalga: min-max arasi minion dogur; boss varsa kisa dogum-duraklamasi.</summary>
    private IEnumerator SpawnWave()
    {
        int n = Random.Range(minPerWave, maxPerWave + 1);
        bool pause = _boss != null && birthPause > 0f;
        if (pause) _boss.SetExternalAttacking(true);

        for (int i = 0; i < n; i++)
        {
            if (CountAlive() >= maxAliveMinions) break;
            SpawnOne();
        }

        if (pause)
        {
            yield return new WaitForSeconds(birthPause);
            if (_boss != null) _boss.SetExternalAttacking(false);
        }
    }

    /// <summary>Tek minion: boss merkezinde dogur, yasak-koni disi yone firlat, scale pop.</summary>
    private void SpawnOne()
    {
        Vector2 dir = PickLaunchDirection();
        Vector3 spawnPos = transform.position + (Vector3)(dir * spawnOffset); // boss'un biraz disinda dogsun
        var go = Instantiate(minionPrefab, spawnPos, Quaternion.identity);
        _alive.Add(go);

        // Knockback ile firlat: AI'yi gecici ezer (tip fark etmez: EnemyController veya BurstShooter).
        var ec = go.GetComponent<EnemyController>();
        if (ec != null) ec.ApplyKnockback(dir, launchSpeed, launchDuration);
        else { var bs = go.GetComponent<BurstShooterEnemy>(); if (bs != null) bs.ApplyKnockback(dir, launchSpeed, launchDuration); }

        // Sandevistan izi: firlatma boyunca minion'un o anki karesinden hayalet kopyalar birak.
        if (launchAfterImage)
        {
            var sr = go.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) StartCoroutine(LaunchTrail(sr, launchDuration, BossColor()));
        }

        // Dogum pop'u: prefab'in tam olcegini yakalayip kucukten buyut.
        if (spawnStartScale < 1f && scalePopDuration > 0f)
        {
            Vector3 full = go.transform.localScale;
            go.transform.localScale = full * spawnStartScale;
            StartCoroutine(ScalePop(go.transform, full));
        }
    }

    /// <summary>Oyuncu YONUNE dogru at: playerAngle +- [safeCone, spread]. safeCone tam ustune gelmesini onler,
    /// spread koninin yari-genisligi. Oyuncu yoksa tam rastgele.</summary>
    private Vector2 PickLaunchDirection()
    {
        float ang;
        if (_player != null && ((Vector2)_player.position - (Vector2)transform.position).sqrMagnitude > 0.0001f)
        {
            Vector2 toPlayer = (Vector2)_player.position - (Vector2)transform.position;
            float playerAng = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg;
            float lo = Mathf.Max(0f, safeConeDegrees);
            float hi = spreadDegrees > lo ? spreadDegrees : lo + 40f; // spread gecersiz/0 (serialize tuzagi) -> makul koni
            float off = Random.Range(lo, hi) * (Random.value < 0.5f ? 1f : -1f); // oyuncu yonu +- [safeCone, spread]
            ang = playerAng + off;
        }
        else ang = Random.Range(0f, 360f);
        float r = ang * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
    }

    /// <summary>Minion olcegini kucukten hedefe buyutur (icinden cikma juice). UNSCALED degil; oyunla akar.</summary>
    private IEnumerator ScalePop(Transform t, Vector3 target)
    {
        float el = 0f;
        while (el < scalePopDuration && t != null)
        {
            el += Time.deltaTime;
            t.localScale = Vector3.Lerp(target * spawnStartScale, target, Mathf.SmoothStep(0f, 1f, el / scalePopDuration));
            yield return null;
        }
        if (t != null) t.localScale = target;
    }

    /// <summary>Firlatma suresi boyunca minion'un SpriteRenderer'indan periyodik hayalet iz (AfterImage) uretir.</summary>
    private IEnumerator LaunchTrail(SpriteRenderer sr, float duration, Color tint)
    {
        float el = 0f, tick = afterImageInterval; // ilk kare hemen bir hayalet
        while (el < duration && sr != null)
        {
            el += Time.deltaTime; tick += Time.deltaTime;
            if (tick >= afterImageInterval)
            {
                tick = 0f;
                AfterImage.Spawn(sr, tint, afterImageAlpha, afterImageFadeTime, sr.sortingOrder - 1);
            }
            yield return null;
        }
    }

    /// <summary>Bu boss'un kendine ozgu rengi (BossController varsa BaseColor; SplitterBoss gibi enemy-tabanlida EnemyController.BaseColor; yoksa sprite rengi).</summary>
    private Color BossColor()
    {
        var bc = GetComponent<BossController>(); if (bc != null) return bc.BaseColor;
        var ec = GetComponent<EnemyController>(); if (ec != null) return ec.BaseColor;
        var sr = GetComponentInChildren<SpriteRenderer>(); return sr != null ? sr.color : Color.white;
    }

    /// <summary>Yok olmus (Destroy) minion'lari listeden temizler ve canli sayisini doner.</summary>
    private int CountAlive()
    {
        for (int i = _alive.Count - 1; i >= 0; i--)
            if (_alive[i] == null) _alive.RemoveAt(i);
        return _alive.Count;
    }
    #endregion
}

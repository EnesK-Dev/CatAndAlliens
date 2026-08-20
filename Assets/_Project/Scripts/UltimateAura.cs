using System.Collections;
using UnityEngine;

/// <summary>
/// Ultimate aktifken kedinin ARKASINDA donen enerji aurasi. Iki SpriteRenderer ayni 24 kareyi
/// ZIT yonde oynatir (biri 0->24 ileri, digeri 24->0 geri) — ust uste binince daha "auralI",
/// surekli iceri/disari enerji akiyormus hissi verir. player.OnUltimateActiveChanged'i dinler:
/// true -> auralar acilir ve loop baslar, false -> durur ve gizlenir. Player'in child'i olarak
/// kediyi otomatik takip eder. Gorsel bir bilesen — buff mekanigine karismaz (gevsek bagli).
/// </summary>
public class UltimateAura : MonoBehaviour
{
    #region Serialized Fields
    [Header("Renderer'lar (kedinin arkasinda, order kediden kucuk)")]
    [Tooltip("Kareleri ILERI oynatan renderer (0 -> son).")]
    [SerializeField] private SpriteRenderer forwardRenderer;

    [Tooltip("Kareleri GERI oynatan renderer (son -> 0).")]
    [SerializeField] private SpriteRenderer reverseRenderer;

    [Tooltip("Sadece belirli kare ARALIGINDA gidip gelen ekstra katman (yogun dis halka icin).")]
    [SerializeField] private SpriteRenderer rangeRenderer;

    [Tooltip("Range renderer'in gidip gelecegi kare araligi (1 tabanli, dahil). Orn. 18-24.")]
    [SerializeField] private int rangeStartFrame = 18;
    [SerializeField] private int rangeEndFrame = 24;

    [Header("Animasyon")]
    [Tooltip("Ultimate/ kareleri SIRAYLA (1..24).")]
    [SerializeField] private Sprite[] frames;

    [Tooltip("Saniyedeki kare sayisi. Dusuk = yavas aura.")]
    [SerializeField] private float frameRate = 12f;

    [Tooltip("Acik: 0->son->0 yumusak salinim (snap yok, pulse hissi olmaz). Kapali: bastan sarma loop.")]
    [SerializeField] private bool pingPong = true;

    [Header("Player")]
    [Tooltip("Bos birakilirsa parent'tan (kedi) otomatik alinir.")]
    [SerializeField] private player playerRef;
    #endregion

    #region Private Fields
    private Coroutine _animRoutine;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        if (playerRef == null)
            playerRef = GetComponentInParent<player>();

        SetRenderersVisible(false); // baslangicta gizli
    }

    private void OnEnable()
    {
        if (playerRef != null)
            playerRef.OnUltimateActiveChanged += HandleUltimateActive;
    }

    private void OnDisable()
    {
        if (playerRef != null)
            playerRef.OnUltimateActiveChanged -= HandleUltimateActive;

        StopAura(); // cleanup: coroutine dursun, renderer'lar gizlensin
    }
    #endregion

    #region Private Methods
    /// <summary>Ult basladiginda aurayi baslatir, bittiginde durdurur.</summary>
    private void HandleUltimateActive(bool active)
    {
        if (active) StartAura();
        else StopAura();
    }

    private void StartAura()
    {
        if (_animRoutine != null) StopCoroutine(_animRoutine);
        SetRenderersVisible(true);
        _animRoutine = StartCoroutine(AnimateRoutine());
    }

    private void StopAura()
    {
        if (_animRoutine != null)
        {
            StopCoroutine(_animRoutine);
            _animRoutine = null;
        }
        SetRenderersVisible(false);
    }

    /// <summary>Iki renderer'i zit yonde, loop halinde oynatir. Alloc yok (index dongusu).</summary>
    private IEnumerator AnimateRoutine()
    {
        if (frames == null || frames.Length == 0) yield break;

        int n = frames.Length;
        float dt = 1f / Mathf.Max(1f, frameRate);
        int fwd = 0, fwdDir = 1;
        int rev = n - 1, revDir = -1;

        // Range katmani: 1 tabanli araligi index'e cevir ve sinirla
        int lo = Mathf.Clamp(rangeStartFrame - 1, 0, n - 1);
        int hi = Mathf.Clamp(rangeEndFrame - 1, 0, n - 1);
        if (hi < lo) { int t = lo; lo = hi; hi = t; }
        int rng = lo, rngDir = 1;

        while (true)
        {
            if (forwardRenderer != null && frames[fwd] != null) forwardRenderer.sprite = frames[fwd];
            if (reverseRenderer != null && frames[rev] != null) reverseRenderer.sprite = frames[rev];
            if (rangeRenderer != null && frames[rng] != null) rangeRenderer.sprite = frames[rng];

            if (pingPong)
            {
                Advance(ref fwd, ref fwdDir, n);
                Advance(ref rev, ref revDir, n);
            }
            else
            {
                fwd = (fwd + 1) % n;         // wrap: sona gelince basa zipla (snap = pulse)
                rev = (rev - 1 + n) % n;
            }
            AdvanceRange(ref rng, ref rngDir, lo, hi); // range katmani her zaman gidip gelir
            yield return new WaitForSeconds(dt);
        }
    }

    /// <summary>Belirli [lo, hi] araliginda ping-pong ilerletme (18<->24 gibi).</summary>
    private static void AdvanceRange(ref int idx, ref int dir, int lo, int hi)
    {
        if (hi <= lo) { idx = lo; return; }
        idx += dir;
        if (idx >= hi) { idx = hi; dir = -1; }
        else if (idx <= lo) { idx = lo; dir = 1; }
    }

    /// <summary>Ping-pong ilerletme: uca gelince yon degistir — snap olmadan gidip gelir.</summary>
    private static void Advance(ref int idx, ref int dir, int n)
    {
        idx += dir;
        if (idx >= n - 1) { idx = n - 1; dir = -1; }
        else if (idx <= 0) { idx = 0; dir = 1; }
    }

    private void SetRenderersVisible(bool visible)
    {
        if (forwardRenderer != null) forwardRenderer.enabled = visible;
        if (reverseRenderer != null) reverseRenderer.enabled = visible;
        if (rangeRenderer != null) rangeRenderer.enabled = visible;
    }
    #endregion
}

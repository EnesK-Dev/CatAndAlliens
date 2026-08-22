using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ultimate "ekran-temizleme" sinemasinin yonetmeni (director). player.OnUltimateActiveChanged'i
/// dinler; aktif olunca ~2sn'lik zamanli bir sinema oynatir:
///  1) CHARGE: poz + slow-mo + yavas zoom-in; alevler kademeli buyur/yogunlasir (aura intensity),
///     turuncu ekran dolgusu belirir, dusmanlar oyuncuya EMILIR (vacuum, hizlanarak).
///  2) IMPACT: zaman normale doner, tam-ekran beyaz flash, dusmanlar DROPSUZ silinir.
///  3) RECOVERY: beyaz+turuncu solar, kamera zoom-out, poz biter.
/// Tum sekans UNSCALED time ile surer (slow-mo kendi zamanlamasini yavaslatmasin). God-manager degil —
/// tek sorumluluk: ultimate sinemasi; gevsek bagli (event dinler, referanslari otomatik bulur).
/// </summary>
public class UltimateCinematic : MonoBehaviour
{
    #region Serialized Fields
    [Header("Referanslar (bos = otomatik bul)")]
    [SerializeField] private player playerRef;
    [SerializeField] private Camera cam;
    [SerializeField] private UltimateParticleAura aura;
    [SerializeField] private UltimateScreenFX screenFX;

    [Header("Slow-Mo")]
    [Tooltip("CHARGE boyunca Time.timeScale (0.4 = %40 hiz). IMPACT'te 1'e doner.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float slowMoTimeScale = 0.4f;

    [Header("Kamera Zoom (orthographic size)")]
    [Tooltip("Zoom-in carpani: taban ortho * bu. 1'in altinda = yakinlasir.")]
    [Range(0.5f, 1f)]
    [SerializeField] private float zoomInFactor = 0.82f;

    [Header("Zamanlama (saniye, unscaled) — toplam ~3sn")]
    [Tooltip("CHARGE fazi: zoom-in + alev kademeli buyume + dusman emilmesi. Buyut = tum buildup yavaslar.")]
    [SerializeField] private float chargeDuration = 2.4f;
    [Tooltip("Alev yogunlugunun zamanla artis EGRISI. Yatay=zaman(0-1), dikey=yogunluk(0-1). Ease-in " +
             "(basta yatay, sonda dik) = once normal aura, sonra patlama. Editor'de suruklenerek sekillendirilir.")]
    [SerializeField] private AnimationCurve chargeCurve =
        new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 3f, 0f));
    [Tooltip("IMPACT'te beyaz ekranin tam parlak kalma suresi.")]
    [SerializeField] private float whiteHold = 0.15f;
    [Tooltip("RECOVERY: beyaz fade + kamera geri acilma suresi.")]
    [SerializeField] private float recoveryDuration = 0.5f;

    [Header("Dusman Vacuum (charge boyunca oyuncuya cekme)")]
    [Tooltip("CHARGE basindaki cekme hizi (yavas baslar).")]
    [SerializeField] private float pullSpeedStart = 1.5f;
    [Tooltip("CHARGE sonundaki cekme hizi (hizlanarak icine cekilir).")]
    [SerializeField] private float pullSpeedEnd = 16f;
    #endregion

    #region Private Fields
    private Coroutine _sequence;
    private float _baseOrthoSize = 5f; // Sahnenin varsayilan zoom'u — restore hedefi (Awake'te okunur)
    private readonly List<Transform> _pulled = new List<Transform>(); // emilen dusman transformlari (alloc'suz yeniden kullanilir)
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        if (playerRef == null) playerRef = FindFirstObjectByType<player>();
        if (cam == null) cam = Camera.main;
        if (aura == null) aura = FindFirstObjectByType<UltimateParticleAura>();
        if (screenFX == null) screenFX = FindFirstObjectByType<UltimateScreenFX>();
        if (cam != null && cam.orthographic) _baseOrthoSize = cam.orthographicSize;
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

        if (_sequence != null)
        {
            StopCoroutine(_sequence);
            _sequence = null;
        }
        RestoreState(); // yarida kesilse bile oyunu donuk/zoom'da/beyazda birakma
    }
    #endregion

    #region Private Methods
    private void HandleUltimateActive(bool active)
    {
        if (!active) return;

        if (_sequence != null)
        {
            StopCoroutine(_sequence);
            RestoreState();
        }
        _sequence = StartCoroutine(PlaySequence());
    }

    private IEnumerator PlaySequence()
    {
        // --- Hazirlik ---
        if (playerRef != null) playerRef.EnterUltimatePose();
        Time.timeScale = Mathf.Clamp01(slowMoTimeScale);
        if (screenFX != null) screenFX.ResetFX();
        if (aura != null) aura.SetIntensity(0f); // baseline (senin ayarin) — buradan yukari rampa
        BeginVacuumGather(); // dusmanlari emilebilir yap + listeye al

        Vector3 playerPos = playerRef != null ? playerRef.transform.position : transform.position;
        float zoomedSize = _baseOrthoSize * zoomInFactor;

        // --- 1) CHARGE ---
        float t = 0f;
        while (t < chargeDuration)
        {
            t += Time.unscaledDeltaTime;
            float n = Mathf.Clamp01(t / chargeDuration);

            if (cam != null && cam.orthographic)
                cam.orthographicSize = Mathf.Lerp(_baseOrthoSize, zoomedSize, Mathf.SmoothStep(0f, 1f, n));
            // Alev yogunlugu egriyle sekillenir (ease-in = once aura, sonra patlama). Bos egri gelirse dogrusal.
            float intensity = (chargeCurve != null && chargeCurve.length > 0) ? chargeCurve.Evaluate(n) : n;
            if (aura != null) aura.SetIntensity(intensity);

            // Dusmanlari oyuncuya cek — hizlanarak (vacuum). Player pozda sabit, playerPos degismez.
            float pullSpeed = Mathf.Lerp(pullSpeedStart, pullSpeedEnd, n);
            float step = pullSpeed * Time.unscaledDeltaTime;
            for (int i = 0; i < _pulled.Count; i++)
            {
                Transform e = _pulled[i];
                if (e != null)
                    e.position = Vector3.MoveTowards(e.position, playerPos, step);
            }

            yield return null;
        }

        // --- 2) IMPACT ---
        Time.timeScale = 1f;
        if (screenFX != null) screenFX.SetWhite(1f); // tam ekran beyaz (her seyin ustunde)
        VaporizeAllEnemies();                        // dropsuz sil (emilenler + stragglerlar)
        _pulled.Clear();
        yield return WaitUnscaled(whiteHold);

        // --- 3) RECOVERY ---
        if (playerRef != null) playerRef.ExitUltimatePose();
        float r = 0f;
        while (r < recoveryDuration)
        {
            r += Time.unscaledDeltaTime;
            float n = Mathf.Clamp01(r / recoveryDuration);

            if (cam != null && cam.orthographic)
                cam.orthographicSize = Mathf.Lerp(zoomedSize, _baseOrthoSize, Mathf.SmoothStep(0f, 1f, n));
            if (screenFX != null) screenFX.SetWhite(1f - n); // beyaz solar

            yield return null;
        }

        RestoreState();
        _sequence = null;
    }

    /// <summary>Sahnedeki uc dusman tipini de emilebilir yap (BeginUltimateVacuum) ve transformlarini listele.</summary>
    private void BeginVacuumGather()
    {
        _pulled.Clear();

        var chasers = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        for (int i = 0; i < chasers.Length; i++)
            if (chasers[i] != null) { chasers[i].BeginUltimateVacuum(); _pulled.Add(chasers[i].transform); }

        var shooters = FindObjectsByType<BurstShooterEnemy>(FindObjectsSortMode.None);
        for (int i = 0; i < shooters.Length; i++)
            if (shooters[i] != null) { shooters[i].BeginUltimateVacuum(); _pulled.Add(shooters[i].transform); }

        var boomerangs = FindObjectsByType<BoomerangEnemy>(FindObjectsSortMode.None);
        for (int i = 0; i < boomerangs.Length; i++)
            if (boomerangs[i] != null) { boomerangs[i].BeginUltimateVacuum(); _pulled.Add(boomerangs[i].transform); }
    }

    /// <summary>
    /// Sahnedeki uc dusman tipini de tek seferde bulup DROPSUZ yok eder. Update'te DEGIL — sadece
    /// IMPACT aninda bir kez calisir, o yuzden FindObjectsByType maliyeti kabul edilebilir.
    /// </summary>
    private void VaporizeAllEnemies()
    {
        var chasers = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        for (int i = 0; i < chasers.Length; i++)
            if (chasers[i] != null) chasers[i].Vaporize();

        var shooters = FindObjectsByType<BurstShooterEnemy>(FindObjectsSortMode.None);
        for (int i = 0; i < shooters.Length; i++)
            if (shooters[i] != null) shooters[i].Vaporize();

        var boomerangs = FindObjectsByType<BoomerangEnemy>(FindObjectsSortMode.None);
        for (int i = 0; i < boomerangs.Length; i++)
            if (boomerangs[i] != null) boomerangs[i].Vaporize();
    }

    private IEnumerator WaitUnscaled(float seconds)
    {
        float t = 0f;
        while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
    }

    /// <summary>Sinema durumunu guvenli varsayilana dondur: normal hiz, taban zoom, poz kapali, overlay temiz.</summary>
    private void RestoreState()
    {
        Time.timeScale = 1f;
        if (cam != null && cam.orthographic) cam.orthographicSize = _baseOrthoSize;
        if (playerRef != null) playerRef.ExitUltimatePose();
        if (screenFX != null) screenFX.ResetFX();
    }
    #endregion
}

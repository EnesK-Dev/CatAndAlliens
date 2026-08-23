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
    [SerializeField] private CameraShake cameraShake;

    [Header("Slow-Mo")]
    [Tooltip("CHARGE boyunca Time.timeScale (0.4 = %40 hiz). IMPACT'te 1'e doner.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float slowMoTimeScale = 0.4f;

    [Header("Kamera Zoom (orthographic size)")]
    [Tooltip("Zoom-in carpani: taban ortho * bu. 1'in altinda = yakinlasir.")]
    [Range(0.5f, 1f)]
    [SerializeField] private float zoomInFactor = 0.82f;

    [Header("Charge Ekran Sarsintisi (charge dolduca artan, ease'li)")]
    [Tooltip("CHARGE sonundaki (doruk) sarsinti siddeti (dunya birimi). Buyut = ekran daha cok titrer.")]
    [SerializeField] private float chargeShakeMagnitudeMax = 0.25f;
    [Tooltip("Sarsintinin zamanla artis EGRISI. Yatay=zaman(0-1), dikey=siddet(0-1). Ease-in " +
             "(basta yatay, sonda dik) = once hafif titreme, charge dolarken siddetlenir.")]
    [SerializeField] private AnimationCurve chargeShakeCurve =
        new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 3f, 0f));

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

    [Header("Dusman Toplanma Dizilimi (kedinin etrafina, ust uste binmeden)")]
    [Tooltip("En ic yaricap: dusmanlar bundan yakina gelmez -> kedinin ICINE girmezler.")]
    [SerializeField] private float gatherMinRadius = 0.7f;
    [Tooltip("Toplanma sikligi: her dusman biraz daha disa dizilir. Buyut = daha genis, seyrek disk (ust uste binmezler).")]
    [SerializeField] private float gatherSpacing = 0.42f;
    #endregion

    #region Private Fields
    private Coroutine _sequence;
    private float _baseOrthoSize = 5f; // Sahnenin varsayilan zoom'u — restore hedefi (Awake'te okunur)
    private readonly List<Transform> _pulled = new List<Transform>(); // emilen dusman transformlari (alloc'suz yeniden kullanilir)
    private readonly List<Vector3> _pullTargets = new List<Vector3>(); // her dusmanin kedinin etrafindaki hedef noktasi (_pulled ile ayni index)

    // Altin aci (~137.5°) radyan cinsinden — dusmanlari kedinin etrafina ust uste binmeden (phyllotaxis) dagitir.
    private const float GoldenAngleRad = 2.399963f;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        if (playerRef == null) playerRef = FindFirstObjectByType<player>();
        if (cam == null) cam = Camera.main;
        if (aura == null) aura = FindFirstObjectByType<UltimateParticleAura>();
        if (screenFX == null) screenFX = FindFirstObjectByType<UltimateScreenFX>();
        if (cameraShake == null && cam != null) cameraShake = cam.GetComponent<CameraShake>();
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

        Vector3 playerPos = playerRef != null ? playerRef.transform.position : transform.position;
        float zoomedSize = _baseOrthoSize * zoomInFactor;

        BeginVacuumGather(playerPos); // dusmanlari emilebilir yap + kedinin etrafinda hedef nokta ata
        if (cameraShake != null) cameraShake.BeginSustainedShake(); // charge boyunca ekran sarsintisi

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

            // Ekran sarsintisi charge dolduca artar (ease'li). Curve bos gelirse dogrusal.
            if (cameraShake != null)
            {
                float shakeN = (chargeShakeCurve != null && chargeShakeCurve.length > 0) ? chargeShakeCurve.Evaluate(n) : n;
                cameraShake.SetSustainedMagnitude(chargeShakeMagnitudeMax * shakeN);
            }

            // Dusmanlari kedinin ETRAFINDAKI hedef noktalarina cek — hizlanarak (vacuum). Kedinin/birbirinin
            // icine girmesinler diye tek noktaya degil, phyllotaxis diskine dizilirler. Player pozda sabit.
            float pullSpeed = Mathf.Lerp(pullSpeedStart, pullSpeedEnd, n);
            float step = pullSpeed * Time.unscaledDeltaTime;
            for (int i = 0; i < _pulled.Count; i++)
            {
                Transform e = _pulled[i];
                if (e != null)
                    e.position = Vector3.MoveTowards(e.position, _pullTargets[i], step);
            }

            yield return null;
        }

        // --- 2) IMPACT ---
        Time.timeScale = 1f;
        if (cameraShake != null) cameraShake.EndSustainedShake(); // sarsintiyi durdur, kamerayi yerine al
        if (screenFX != null) screenFX.SetWhite(1f); // tam ekran beyaz (her seyin ustunde)
        SfxManager.Play(SfxId.UltimateImpact); // beyaz flash / patlama sesi (aktivasyondan ayri)
        VaporizeAllEnemies();                        // dropsuz + aninda sil (emilenler + stragglerlar)
        _pulled.Clear();
        _pullTargets.Clear();
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

    /// <summary>
    /// Sahnedeki uc dusman tipini de emilebilir yap (BeginUltimateVacuum), transformlarini listele ve
    /// her birine kedinin ETRAFINDA bir hedef nokta ata. Hedefler phyllotaxis (ayciyegi/altin aci) diski
    /// ile dagitilir: kedinin icine (minRadius) ve birbirinin icine girmezler, alevin altinda toplanirlar.
    /// </summary>
    private void BeginVacuumGather(Vector3 center)
    {
        _pulled.Clear();
        _pullTargets.Clear();

        var chasers = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        for (int i = 0; i < chasers.Length; i++)
            if (chasers[i] != null) { chasers[i].BeginUltimateVacuum(); _pulled.Add(chasers[i].transform); }

        var shooters = FindObjectsByType<BurstShooterEnemy>(FindObjectsSortMode.None);
        for (int i = 0; i < shooters.Length; i++)
            if (shooters[i] != null) { shooters[i].BeginUltimateVacuum(); _pulled.Add(shooters[i].transform); }

        var boomerangs = FindObjectsByType<BoomerangEnemy>(FindObjectsSortMode.None);
        for (int i = 0; i < boomerangs.Length; i++)
            if (boomerangs[i] != null) { boomerangs[i].BeginUltimateVacuum(); _pulled.Add(boomerangs[i].transform); }

        // Her dusman icin diskteki hedef: yaricap = minRadius + spacing*sqrt(index), aci = index*altinAci.
        // sqrt + altin aci = esit yogunluklu, ust uste binmeyen dagilim (ayciyegi cekirdek dizilimi).
        for (int i = 0; i < _pulled.Count; i++)
        {
            float radius = gatherMinRadius + gatherSpacing * Mathf.Sqrt(i);
            float angle = i * GoldenAngleRad;
            Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            _pullTargets.Add(center + offset);
        }
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
        if (cameraShake != null) cameraShake.EndSustainedShake(); // sarsinti yarida kalmis olabilir — temizle
        if (playerRef != null) playerRef.ExitUltimatePose();
        if (screenFX != null) screenFX.ResetFX();
    }
    #endregion
}

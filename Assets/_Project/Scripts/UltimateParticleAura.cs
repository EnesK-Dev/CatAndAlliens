using UnityEngine;

/// <summary>
/// Ultimate aktifken kedinin etrafinda procedural alev/enerji aurasi (Super Saiyan tarzi).
/// Sprite frame DEGIL, Unity ParticleSystem kullanir — tamamen kod/Inspector ile ayarlanabilir.
/// player.OnUltimateActiveChanged'i dinler: true -> particle'lar Play, false -> Stop (emit keser,
/// mevcut parcaciklar dogal soner). Gorsel bir bilesen, buff mekanigine karismaz (gevsek bagli).
/// </summary>
public class UltimateParticleAura : MonoBehaviour
{
    #region Serialized Fields
    [Tooltip("Bos birakilirsa parent'tan (kedi) otomatik alinir.")]
    [SerializeField] private player playerRef;

    [Tooltip("Ult acilinca oynatilacak particle sistemleri (ana aura + kivilcim).")]
    [SerializeField] private ParticleSystem[] systems;

    [Header("Charge Ramp — doruktaki KAT (baseline'IN kaci kati)")]
    [Tooltip("Parcacik SAYISI (emission) kati. PERF: yuksek = CPU yuku. Ilimli tut (2-4).")]
    [SerializeField] private float rateRampMax = 3.0f;
    [Tooltip("Parcacik BOYUTU (start size) kati. PERF: cok buyuk additive = overdraw (GPU).")]
    [SerializeField] private float sizeRampMax = 2.0f;
    [Tooltip("Dikey hiz/yukselis (velocityOverLifetime Y + startSpeed) kati.")]
    [SerializeField] private float speedRampMax = 2.0f;

    [Header("Charge Ramp — EKRANI KAPLAMA (Transform Scale, ucuz)")]
    [Tooltip("Transform Scale X kati: alevler YATAY buyuyup ekrani kaplar. Sayi ARTMADAN kapladigi icin CPU'ya UCUZ.")]
    [SerializeField] private float scaleXRampMax = 6.0f;
    [Tooltip("Transform Scale Y kati: alevler DIKEY buyuyup ekrani kaplar.")]
    [SerializeField] private float scaleYRampMax = 6.0f;
    #endregion

    #region Private Fields
    // Senin editor'de ayarladigin TABAN carpanlar (Awake'te bir kez okunur). Ramp bunlarin USTUNE
    // uygulanir (taban * kat) — boylece baseline'i EZMEDEN buyutur. Onceki hata: carpanlari 1'e eziyordu.
    private float[] _baseRate;
    private float[] _baseSize;
    private float[] _baseSpeed;
    private float[] _baseVelX;
    private float[] _baseVelY;
    private Vector3[] _baseScale; // her sistemin taban Transform localScale'i (ekrani kaplama rampi bunu buyutur)
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        if (playerRef == null)
            playerRef = GetComponentInParent<player>();
        CacheBaseMultipliers();
    }

    /// <summary>Editor'de ayarlanmis taban carpanlari bir kez okur; ramp bunlarin uzerine uygulanir.</summary>
    private void CacheBaseMultipliers()
    {
        int n = systems != null ? systems.Length : 0;
        _baseRate = new float[n];
        _baseSize = new float[n];
        _baseSpeed = new float[n];
        _baseVelX = new float[n];
        _baseVelY = new float[n];
        _baseScale = new Vector3[n];

        for (int i = 0; i < n; i++)
        {
            var ps = systems[i];
            if (ps == null)
            {
                _baseRate[i] = _baseSize[i] = _baseSpeed[i] = _baseVelX[i] = _baseVelY[i] = 1f;
                _baseScale[i] = Vector3.one;
                continue;
            }
            _baseRate[i] = ps.emission.rateOverTimeMultiplier;
            _baseSize[i] = ps.main.startSizeMultiplier;
            _baseSpeed[i] = ps.main.startSpeedMultiplier;
            _baseVelX[i] = ps.velocityOverLifetime.xMultiplier;
            _baseVelY[i] = ps.velocityOverLifetime.yMultiplier;
            _baseScale[i] = ps.transform.localScale;
        }
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

        StopAura(); // cleanup
    }
    #endregion

    #region Private Methods
    private void HandleUltimateActive(bool active)
    {
        if (active) PlayAura();
        else StopAura();
    }

    private void PlayAura()
    {
        if (systems == null) return;
        foreach (var ps in systems)
        {
            if (ps == null) continue;
            // Slow-mo (Time.timeScale<1) alevleri seyreltmesin — dunya yavasken alevler tam hizda coussun
            var main = ps.main;
            main.useUnscaledTime = true;
            ps.Clear();
            ps.Play();
        }
    }

    private void StopAura()
    {
        if (systems == null) return;
        foreach (var ps in systems)
        {
            if (ps == null) continue;
            // Emit'i kes ama mevcut parcaciklar dogal sonsun (aniden kaybolma olmaz)
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        // Carpanlari + Transform scale'i TABANA geri al (kat=1) — sonraki ultimate temiz baslasin
        ApplyRamp(1f, 1f, 1f, 1f, 1f);
    }

    /// <summary>
    /// CHARGE ramp: 0-1 arasi 't' ile particle yogunlugunu (emission) ve boyutunu (start size) buyutur.
    /// "Multiplier" property'leri kullanilir — editor'deki taban curve'e CARPAR, mutlak set eder (birikmez).
    /// UltimateCinematic her kare cagirir; alevler kademeli buyuyup yogunlasir (charge hissi).
    /// </summary>
    public void SetIntensity(float t01)
    {
        t01 = Mathf.Clamp01(t01);
        // Kat: t=0 -> 1 (baseline), t=1 -> RampMax. Taban carpanla CARPILIR (ezilmez).
        ApplyRamp(Mathf.Lerp(1f, rateRampMax, t01),
                  Mathf.Lerp(1f, sizeRampMax, t01),
                  Mathf.Lerp(1f, speedRampMax, t01),
                  Mathf.Lerp(1f, scaleXRampMax, t01),
                  Mathf.Lerp(1f, scaleYRampMax, t01));
    }

    private void ApplyRamp(float rateFactor, float sizeFactor, float speedFactor, float scaleXFactor, float scaleYFactor)
    {
        if (systems == null || _baseRate == null) return;
        for (int i = 0; i < systems.Length; i++)
        {
            var ps = systems[i];
            if (ps == null) continue;

            var em = ps.emission;
            em.rateOverTimeMultiplier = _baseRate[i] * rateFactor;   // parcacik sayisi

            var main = ps.main;
            main.startSizeMultiplier = _baseSize[i] * sizeFactor;    // parcacik boyutu
            main.startSpeedMultiplier = _baseSpeed[i] * speedFactor; // startSpeed kullanan sistemler

            // Alev hareketi (yukari flicker) — X ve Y ayni oranda; yayilma artik SCALE'den gelir
            var vel = ps.velocityOverLifetime;
            if (vel.enabled)
            {
                vel.xMultiplier = _baseVelX[i] * speedFactor;
                vel.yMultiplier = _baseVelY[i] * speedFactor;
            }

            // EKRANI KAPLAMA: Transform Scale X/Y buyur (Scaling Mode Local -> alan + boyut birlikte buyur).
            // Sayi artmadan kapladigi icin CPU'ya ucuz; bedel sadece kisa suren GPU overdraw.
            Vector3 bs = _baseScale[i];
            ps.transform.localScale = new Vector3(bs.x * scaleXFactor, bs.y * scaleYFactor, bs.z);
        }
    }
    #endregion
}

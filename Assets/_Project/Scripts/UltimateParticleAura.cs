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
    [Tooltip("t=0 = tam senin editor ayarin (baseline). t=1 = baseline * bu kat. Yogunluk (emission).")]
    [SerializeField] private float rateRampMax = 4.0f;
    [Tooltip("Boyut / kapladigi alan (start size).")]
    [SerializeField] private float sizeRampMax = 3.0f;
    [Tooltip("Hiz + yayilim (startSpeed + velocityOverLifetime X/Y). Buyuk = daha genise/hizli coussun.")]
    [SerializeField] private float speedRampMax = 2.5f;
    #endregion

    #region Private Fields
    // Senin editor'de ayarladigin TABAN carpanlar (Awake'te bir kez okunur). Ramp bunlarin USTUNE
    // uygulanir (taban * kat) — boylece baseline'i EZMEDEN buyutur. Onceki hata: carpanlari 1'e eziyordu.
    private float[] _baseRate;
    private float[] _baseSize;
    private float[] _baseSpeed;
    private float[] _baseVelX;
    private float[] _baseVelY;
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

        for (int i = 0; i < n; i++)
        {
            var ps = systems[i];
            if (ps == null)
            {
                _baseRate[i] = _baseSize[i] = _baseSpeed[i] = _baseVelX[i] = _baseVelY[i] = 1f;
                continue;
            }
            _baseRate[i] = ps.emission.rateOverTimeMultiplier;
            _baseSize[i] = ps.main.startSizeMultiplier;
            _baseSpeed[i] = ps.main.startSpeedMultiplier;
            _baseVelX[i] = ps.velocityOverLifetime.xMultiplier;
            _baseVelY[i] = ps.velocityOverLifetime.yMultiplier;
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
        // Carpanlari TABANA geri al (kat=1) — senin ayarini ezmeden sonraki ultimate temiz baslasin
        ApplyRamp(1f, 1f, 1f);
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
                  Mathf.Lerp(1f, speedRampMax, t01));
    }

    private void ApplyRamp(float rateFactor, float sizeFactor, float speedFactor)
    {
        if (systems == null || _baseRate == null) return;
        for (int i = 0; i < systems.Length; i++)
        {
            var ps = systems[i];
            if (ps == null) continue;

            var em = ps.emission;
            em.rateOverTimeMultiplier = _baseRate[i] * rateFactor;   // yogunluk

            var main = ps.main;
            main.startSizeMultiplier = _baseSize[i] * sizeFactor;    // boyut / kapladigi alan
            main.startSpeedMultiplier = _baseSpeed[i] * speedFactor; // startSpeed kullanan sistemler

            // velocityOverLifetime kullanan sistemler (alev yukari yelpaze) icin X/Y hizini olcekle
            var vel = ps.velocityOverLifetime;
            if (vel.enabled)
            {
                vel.xMultiplier = _baseVelX[i] * speedFactor;
                vel.yMultiplier = _baseVelY[i] * speedFactor;
            }
        }
    }
    #endregion
}

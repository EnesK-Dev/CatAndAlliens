using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Oyunun tum ses efektlerini (SFX) calan merkezi servis. DamagePopupManager deseni: sahnede tek
/// obje, static erisim, sahnede yoksa sessizce hicbir sey yapmaz (fake-null guvenli). Tek sorumluluk:
/// SES CALMAK — oyun mantigina karismaz. Ozellikler:
///  - AudioSource POOL (ust uste binen sesler birbirini kesmez; round-robin ses kanallari).
///  - VARYANT + PITCH randomize (her ses icin birden cok klip + hafif pitch kaymasi -> tekrar sikici olmaz).
///  - S-rank LOOP: ayri, kesintisiz bir kaynak (S'e girince baslar, S'ten dusunce durur).
/// Bir kisim ses EVENT'le baglanir (hasar alma, olum, rank atlama); gerisi ilgili yerden Play() cagrisiyla.
/// Not: Global ac/kapa AudioManager (AudioListener.volume) tarafindan yonetilir — burada ekstra is gerekmez.
/// </summary>
public class SfxManager : MonoBehaviour
{
    #region Nested Types
    /// <summary>Tek bir ses turunun verisi: klip varyantlari + ses seviyesi + pitch araligi.</summary>
    [System.Serializable]
    public class SfxEntry
    {
        [Tooltip("Bu girisin hangi aksiyona ait oldugu.")]
        public SfxId id;

        [Tooltip("Klip varyantlari. Birden fazla verilirse her calista rastgele biri secilir (tekrar hissi kirilir).")]
        public AudioClip[] clips;

        [Range(0f, 1f)]
        [Tooltip("Bu sesin seviyesi.")]
        public float volume = 1f;

        [Tooltip("Pitch (hiz/ton) rastgelelik araligi. X=min, Y=max. 1 = orijinal. Ornek (0.95, 1.05) = hafif varyasyon.")]
        public Vector2 pitchRange = new Vector2(0.95f, 1.05f);
    }
    #endregion

    #region Serialized Fields
    [Header("Ses Tablosu")]
    [Tooltip("Her aksiyon icin klip(ler) + seviye + pitch. Ayni id'yi bir kez ekle.")]
    [SerializeField] private SfxEntry[] entries;

    [Header("Pool")]
    [Tooltip("Ayni anda calabilecek SES KANALI sayisi. Cok ses ust uste gelirse en eskisi kesilir. 6-10 uygun.")]
    [SerializeField] private int voiceCount = 8;

    [Header("S-Rank Loop (ayri kaynak)")]
    [Tooltip("S-rank'ta kalindigi surece arkada donen ses (loop). Tek klip yeter.")]
    [SerializeField] private SfxEntry sRankLoop = new SfxEntry { id = SfxId.SRankLoop, volume = 0.6f, pitchRange = Vector2.one };

    [Header("Oyun Muzigi (ayri kaynak, loop)")]
    [Tooltip("Oyun sahnesi acilinca arkada donen muzik. MainMenu'de bos birak (ya da menu muzigi ata).")]
    [SerializeField] private SfxEntry gameplayMusic = new SfxEntry { id = SfxId.GameplayMusic, volume = 0.5f, pitchRange = Vector2.one };
    [Tooltip("Sahne acilir acilmaz muzigi otomatik baslat.")]
    [SerializeField] private bool playMusicOnStart = true;
    #endregion

    #region Private Fields
    private static SfxManager _instance;
    private readonly Dictionary<SfxId, SfxEntry> _map = new Dictionary<SfxId, SfxEntry>();
    private AudioSource[] _voices;
    private int _nextVoice;
    private AudioSource _loopSource;   // S-rank loop
    private AudioSource _musicSource;  // oyun muzigi (loop)

    // Ulti penceresi: aktifken SADECE Ultimate + UltimateImpact duyulur; muzik/S-loop duraklar, geri geri acilir.
    private player _playerRef;
    private bool _ultimateActive;
    private bool _musicWasPlaying;
    private bool _loopWasPlaying;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        // Tek instance — ikinci kopya olusursa (sahne gecisi vb.) kendini yok et
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        BuildMap();
        BuildVoices();
    }

    private void Start()
    {
        // Sahne acilir acilmaz oyun muzigini baslat (klip atanmissa). MainMenu'de klip bos -> calmaz.
        if (playMusicOnStart) PlayMusicInternal();

        // Ulti penceresini dinle (instance event) — aktifken diger sesleri sustururuz.
        _playerRef = FindFirstObjectByType<player>();
        if (_playerRef != null) _playerRef.OnUltimateActiveChanged += HandleUltimateActive;
    }

    private void OnEnable()
    {
        // Static event'ler — sahnede yoksa bile guvenli. OnDisable'da mutlaka cikilir (leak olmasin).
        player.OnPlayerDamaged += HandlePlayerDamaged;
        ComboManager.OnRankChanged += HandleRankChanged;
    }

    private void OnDisable()
    {
        player.OnPlayerDamaged -= HandlePlayerDamaged;
        ComboManager.OnRankChanged -= HandleRankChanged;
    }

    private void OnDestroy()
    {
        if (_playerRef != null) _playerRef.OnUltimateActiveChanged -= HandleUltimateActive;
        if (_instance == this) _instance = null;
    }
    #endregion

    #region Public Methods (static API)
    /// <summary>Verilen aksiyonun sesini bir kez calar (varyant + pitch rastgele). Sahnede manager yoksa sessizdir.</summary>
    public static void Play(SfxId id)
    {
        if (_instance != null) _instance.PlayInternal(id);
    }

    /// <summary>S-rank loop sesini baslatir (zaten calıyorsa dokunmaz).</summary>
    public static void StartSRankLoop()
    {
        if (_instance != null) _instance.StartLoopInternal();
    }

    /// <summary>S-rank loop sesini durdurur.</summary>
    public static void StopSRankLoop()
    {
        if (_instance != null) _instance.StopLoopInternal();
    }

    /// <summary>Oyun muzigini baslatir (zaten calıyorsa dokunmaz).</summary>
    public static void PlayMusic()
    {
        if (_instance != null) _instance.PlayMusicInternal();
    }

    /// <summary>Oyun muzigini durdurur.</summary>
    public static void StopMusic()
    {
        if (_instance != null && _instance._musicSource != null && _instance._musicSource.isPlaying)
            _instance._musicSource.Stop();
    }
    #endregion

    #region Private Methods
    /// <summary>entries dizisini id -> entry sozlugune cevirir (calma aninda O(1), alloc yok).</summary>
    private void BuildMap()
    {
        _map.Clear();
        if (entries == null) return;
        for (int i = 0; i < entries.Length; i++)
        {
            var e = entries[i];
            if (e == null) continue;
            _map[e.id] = e; // ayni id iki kez verilirse sonuncusu kazanir
        }
    }

    /// <summary>Pool'daki AudioSource kanallarini child obje olarak olusturur.</summary>
    private void BuildVoices()
    {
        int n = Mathf.Max(1, voiceCount);
        _voices = new AudioSource[n];
        for (int i = 0; i < n; i++)
        {
            var go = new GameObject("SfxVoice_" + i);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f; // 2D oyun — konumdan bagimsiz, hep ayni seviyede
            _voices[i] = src;
        }

        var loopGo = new GameObject("SfxLoop");
        loopGo.transform.SetParent(transform, false);
        _loopSource = loopGo.AddComponent<AudioSource>();
        _loopSource.playOnAwake = false;
        _loopSource.loop = true;
        _loopSource.spatialBlend = 0f;

        var musicGo = new GameObject("Music");
        musicGo.transform.SetParent(transform, false);
        _musicSource = musicGo.AddComponent<AudioSource>();
        _musicSource.playOnAwake = false;
        _musicSource.loop = true;
        _musicSource.spatialBlend = 0f;
    }

    private void PlayInternal(SfxId id)
    {
        // Ulti penceresinde SADECE ulti sesleri gecer — geri kalan tum efektler bastirilir.
        if (_ultimateActive && id != SfxId.Ultimate && id != SfxId.UltimateImpact) return;

        if (!_map.TryGetValue(id, out var e) || e.clips == null || e.clips.Length == 0) return;

        AudioClip clip = e.clips.Length == 1 ? e.clips[0] : e.clips[Random.Range(0, e.clips.Length)];
        if (clip == null) return;

        // Round-robin kanal sec — mevcut ses kesilse bile 8 kanal genelde yeterli
        var src = _voices[_nextVoice];
        _nextVoice = (_nextVoice + 1) % _voices.Length;

        src.clip = clip;
        src.volume = e.volume;
        src.pitch = Random.Range(e.pitchRange.x, e.pitchRange.y);
        src.Play();
    }

    private void StartLoopInternal()
    {
        if (sRankLoop == null || sRankLoop.clips == null || sRankLoop.clips.Length == 0) return;
        AudioClip clip = sRankLoop.clips[0];
        if (clip == null) return;
        if (_loopSource.isPlaying && _loopSource.clip == clip) return; // zaten calıyor

        _loopSource.clip = clip;
        _loopSource.volume = sRankLoop.volume;
        _loopSource.pitch = sRankLoop.pitchRange.x <= 0f ? 1f : sRankLoop.pitchRange.x;
        _loopSource.Play();
    }

    private void StopLoopInternal()
    {
        if (_loopSource != null && _loopSource.isPlaying) _loopSource.Stop();
    }

    private void PlayMusicInternal()
    {
        if (gameplayMusic == null || gameplayMusic.clips == null || gameplayMusic.clips.Length == 0) return;
        AudioClip clip = gameplayMusic.clips[0];
        if (clip == null) return;
        if (_musicSource.isPlaying && _musicSource.clip == clip) return; // zaten calıyor

        _musicSource.clip = clip;
        _musicSource.volume = gameplayMusic.volume;
        _musicSource.pitch = gameplayMusic.pitchRange.x <= 0f ? 1f : gameplayMusic.pitchRange.x;
        _musicSource.Play();
    }

    // ---- Event handler'lar ----

    /// <summary>Oyuncu hasar aldiginda (static event) hasar alma sesini calar.</summary>
    private void HandlePlayerDamaged(float amount) => PlayInternal(SfxId.PlayerHurt);

    /// <summary>
    /// Ulti aktif olunca (true) muzik + S-loop duraklar ve suren tek-atis sesler kesilir — sadece Ultimate +
    /// UltimateImpact duyulur. Ulti bitince (false) muzik geri acilir; S-loop yalnizca hala S-rank'taysa devam eder.
    /// </summary>
    private void HandleUltimateActive(bool active)
    {
        _ultimateActive = active;

        if (active)
        {
            // Suren tek-atis sesleri kes (temiz sessizlik)
            if (_voices != null)
                for (int i = 0; i < _voices.Length; i++)
                    if (_voices[i] != null) _voices[i].Stop();

            _musicWasPlaying = _musicSource != null && _musicSource.isPlaying;
            _loopWasPlaying = _loopSource != null && _loopSource.isPlaying;
            if (_musicWasPlaying) _musicSource.Pause();
            if (_loopWasPlaying) _loopSource.Pause();
        }
        else
        {
            if (_musicWasPlaying && _musicSource != null) _musicSource.UnPause();
            if (_loopWasPlaying && _loopSource != null)
            {
                // S-loop sadece hala S-rank'taysa devam etsin (ulti sirasinda rank dusmus olabilir)
                if (ComboManager.RankIndex >= ComboManager.MaxRankIndex) _loopSource.UnPause();
                else _loopSource.Stop();
            }
        }
    }

    /// <summary>
    /// Rank degisince: yukseldiyse "rank atlama" sesi; S-rank'a girildiyse loop baslar, S'ten dusuldiyse durur.
    /// </summary>
    private void HandleRankChanged(int oldIndex, int newIndex)
    {
        if (newIndex > oldIndex) PlayInternal(SfxId.RankUp);

        int maxRank = ComboManager.MaxRankIndex;
        if (newIndex >= maxRank) StartLoopInternal();      // S-rank'a girildi / hala S
        else if (oldIndex >= maxRank) StopLoopInternal();  // S-rank'tan dusuldu
    }
    #endregion
}

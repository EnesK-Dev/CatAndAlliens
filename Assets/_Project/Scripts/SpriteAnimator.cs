using UnityEngine;

/// <summary>
/// Hafif sprite kare oynatici — Animator'a alternatif. Bir Sprite[] dizisini frameRate
/// hizinda dongusel oynatir. State machine/parametre/transition YOK; enemy basina maliyeti
/// cok dusuktur (VS-tarzi cok sayida dusman icin uygun). Update'te alloc yok.
///
/// ONEMLI: Olum gibi durumlarda SpriteRenderer disaridan (script'ten) ezilecekse bu component
/// enabled=false yapilmalidir; aksi halde her kare kendi karesini geri yazar (Animator'daki
/// ayni "sprite ezme" tuzagi). Enemy Die() icinde bu kapatilir.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteAnimator : MonoBehaviour
{
    #region Serialized Fields
    [Header("Referanslar")]
    [Tooltip("Bos birakilirsa Awake'te otomatik alinir.")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Animasyon")]
    [Tooltip("Sirayla oynatilacak kareler.")]
    [SerializeField] private Sprite[] frames;

    [Tooltip("Saniyede kac kare oynasin (fps).")]
    [SerializeField] private float frameRate = 5f;

    [Tooltip("Son kareden basa donsun mu. Kapaliysa son karede durur.")]
    [SerializeField] private bool loop = true;

    [Tooltip("Etkinlesince rastgele bir kareden baslasin — ayni anda dogan dusmanlar tek vucut gibi senkron kipirdamasin.")]
    [SerializeField] private bool randomizeStartFrame = true;
    #endregion

    #region Private Fields
    private float _timer;
    private int _index;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        // Pool/yeniden kullanim icin durumu sifirla. randomizeStartFrame ile senkronizasyonu kir.
        _timer = 0f;
        _index = (randomizeStartFrame && frames != null && frames.Length > 0)
            ? Random.Range(0, frames.Length)
            : 0;
        ApplyCurrentFrame();
    }

    private void Update()
    {
        if (frames == null || frames.Length == 0 || frameRate <= 0f)
            return;

        // Loop kapaliysa ve sona geldiysek bosuna sayma
        if (!loop && _index >= frames.Length - 1)
            return;

        _timer += Time.deltaTime * frameRate;
        if (_timer >= 1f)
        {
            _timer -= 1f;
            _index++;
            if (_index >= frames.Length)
                _index = loop ? 0 : frames.Length - 1;
            ApplyCurrentFrame();
        }
    }
    #endregion

    #region Public Methods
    /// <summary>Kare setini kod tarafindan degistirir (ornegin state'e gore) ve bastan baslatir.</summary>
    public void SetFrames(Sprite[] newFrames, bool newLoop = true)
    {
        frames = newFrames;
        loop = newLoop;
        _timer = 0f;
        _index = 0;
        ApplyCurrentFrame();
    }
    #endregion

    #region Private Methods
    private void ApplyCurrentFrame()
    {
        if (spriteRenderer != null && frames != null && frames.Length > 0)
            spriteRenderer.sprite = frames[Mathf.Clamp(_index, 0, frames.Length - 1)];
    }
    #endregion
}

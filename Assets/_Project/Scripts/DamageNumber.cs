using System;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Dusmana vurulunca ustunde beliren hasar sayisi. Yukari suzulur, solar ve
/// omru bitince havuza geri doner. DamagePopupManager tarafindan pool'lanir.
/// </summary>
public class DamageNumber : MonoBehaviour
{
    #region Serialized Fields
    [Header("Referanslar")]
    [SerializeField] private TMP_Text label;

    [Header("Animasyon Ayarlari")]
    [SerializeField] private float lifetime = 0.7f;          // toplam gorunme suresi (sn)
    [SerializeField] private float floatDistance = 1f;       // omru boyunca yukari kayacagi mesafe (dunya birimi)
    [SerializeField] private float horizontalJitter = 0.3f;  // saga/sola rastgele kayma — sayilar ust uste binmesin
    [SerializeField] private float fadeStartPercent = 0.4f;  // omrun bu yuzdesinden sonra solmaya baslar
    [SerializeField] private float popScale = 1.2f;          // dogarken kisa "zipla" olcegi
    #endregion

    #region Private Fields
    private Coroutine _playRoutine;
    private Action<DamageNumber> _onComplete;
    private Color _baseColor;
    private Vector3 _baseScale;
    #endregion

    #region Unity Callbacks
    private void OnDisable()
    {
        // Havuza donerken calisan coroutine kalirsa bir sonraki spawn'da cakisir
        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Sayiyi verilen dunya konumunda baslatir. Bitince onComplete geri cagrilir
    /// (manager bunu havuza dondurmek icin kullanir).
    /// </summary>
    public void Play(Vector3 worldPosition, float damage, Action<DamageNumber> onComplete)
    {
        _onComplete = onComplete;

        if (label != null)
        {
            _baseColor = label.color;
            // Alloc yapmadan int yazar — mobilde her vurusta cop uretmemek icin SetText
            label.SetText("{0}", Mathf.RoundToInt(damage));
        }

        // Yatayda kucuk rastgele kayma ver, dikeyde spawn noktasindan basla
        float jitter = UnityEngine.Random.Range(-horizontalJitter, horizontalJitter);
        transform.position = worldPosition + new Vector3(jitter, 0f, 0f);
        _baseScale = transform.localScale;

        if (_playRoutine != null)
            StopCoroutine(_playRoutine);
        _playRoutine = StartCoroutine(PlayRoutine());
    }
    #endregion

    #region Private Methods
    private IEnumerator PlayRoutine()
    {
        Vector3 startPosition = transform.position;
        float elapsed = 0f;

        while (elapsed < lifetime)
        {
            float t = elapsed / lifetime;               // 0 -> 1 normalize omur
            float easeUp = 1f - (1f - t) * (1f - t);    // ease-out: basta hizli, sonda yavas yukselir

            transform.position = startPosition + Vector3.up * (floatDistance * easeUp);
            transform.localScale = _baseScale * PopScaleAt(t);
            ApplyAlpha(t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        _playRoutine = null;
        _onComplete?.Invoke(this);
    }

    /// <summary>Dogar dogmaz kisa bir buyume-kuculme "zipla" efekti dondurur.</summary>
    private float PopScaleAt(float t)
    {
        // Ilk %20'de popScale'e ciker sonra 1'e oturur
        const float popEnd = 0.2f;
        if (t >= popEnd) return 1f;
        float p = t / popEnd;
        return Mathf.Lerp(popScale, 1f, p);
    }

    private void ApplyAlpha(float t)
    {
        if (label == null) return;

        float alpha = 1f;
        if (t > fadeStartPercent)
        {
            float fadeT = (t - fadeStartPercent) / (1f - fadeStartPercent);
            alpha = 1f - fadeT;
        }

        Color c = _baseColor;
        c.a = alpha;
        label.color = c;
    }
    #endregion
}

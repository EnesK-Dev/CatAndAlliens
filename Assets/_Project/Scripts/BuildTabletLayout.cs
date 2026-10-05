using UnityEngine;

/// <summary>
/// Build ekraninda SADECE tablet (kare) ekran oranlarinda UI'i uyarlar:
///  - scaleTarget'i tabletScale ile uniform kuculltur (icerik topluca kucuilur),
///  - items[]'daki elemanlara tabletDelta ekler (ince konum duzeltmesi, ornek: baslik cakismasi).
/// Telefon (genis) oranda hicbir sey degismez — temel konumlar/olcek aynen korunur.
/// aspect (width/height) &lt; tabletAspectThreshold ise cihaz "tablet" sayilir.
/// Temel konumlar ilk aktiflesmede saklanir; Apply her cagrida base + (tablet? delta:0) uygular (idempotent).
/// </summary>
public class BuildTabletLayout : MonoBehaviour
{
    #region Types
    /// <summary>Bir UI elemani ve ona SADECE tablette uygulanacak kaydirma (px).</summary>
    [System.Serializable]
    public struct Item
    {
        [Tooltip("Kaydirilacak UI elemani.")]
        public RectTransform target;

        [Tooltip("SADECE tablette eklenen kaydirma (anchoredPosition delta, px).")]
        public Vector2 tabletDelta;
    }
    #endregion

    #region Serialized Fields
    [Tooltip("Ekran orani (en/boy) bunun ALTINDA ise tablet. 19.5:9=2.17, 16:9=1.78, 16:10=1.6, 4:3=1.33.")]
    [SerializeField] private float tabletAspectThreshold = 1.7f;

    [Tooltip("Tablette topluca kuculecek icerik kokunun RectTransform'u (ornek: scroll Content).")]
    [SerializeField] private RectTransform scaleTarget;

    [Tooltip("scaleTarget'in tablette alacagi uniform olcek (1 = degisme, 0.85 = %85).")]
    [SerializeField] private float tabletScale = 1f;

    [Tooltip("Tablette ince konum duzeltmesi gereken elemanlar (ornek: BUILD basligi).")]
    [SerializeField] private Item[] items;
    #endregion

    #region Private Fields
    private Vector2[] _basePositions;
    private bool _captured;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        Capture();
        Apply();
    }

    private void OnEnable()
    {
        if (_captured) Apply();
    }
    #endregion

    #region Private Methods
    /// <summary>Elemanlarin TEMEL (telefon) konumlarini bir kez yakalar.</summary>
    private void Capture()
    {
        int n = items != null ? items.Length : 0;
        _basePositions = new Vector2[n];
        for (int i = 0; i < n; i++)
            if (items[i].target != null) _basePositions[i] = items[i].target.anchoredPosition;
        _captured = true;
    }

    /// <summary>Tablet ise olcek + delta uygular, telefon ise temel hali aynen korur.</summary>
    private void Apply()
    {
        bool tablet = IsTablet();

        if (scaleTarget != null)
        {
            float s = tablet ? tabletScale : 1f;
            scaleTarget.localScale = new Vector3(s, s, 1f);
        }

        if (items != null && _basePositions != null)
            for (int i = 0; i < items.Length; i++)
            {
                var t = items[i].target;
                if (t == null) continue;
                t.anchoredPosition = _basePositions[i] + (tablet ? items[i].tabletDelta : Vector2.zero);
            }
    }

    /// <summary>Mevcut ekran oranina gore cihaz tablet mi?</summary>
    private bool IsTablet()
    {
        float h = Mathf.Max(1, Screen.height);
        return (Screen.width / h) < tabletAspectThreshold;
    }
    #endregion
}

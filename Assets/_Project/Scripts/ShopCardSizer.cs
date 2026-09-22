using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Satir-tabanli shop/build duzeninde kart boyutunu, icerik genisligine gore bir satira 'perRow' kart
/// sigacak sekilde ayarlar (responsive). Icindeki tum kart LayoutElement'lerine preferred/min boyut yazar;
/// yukseklik cellAspect ile oranlanir. Ekran/panel degisince yeniden hesaplar. Bir VerticalLayoutGroup
/// (satirlarin parent'i) uzerinde durur.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ShopCardSizer : MonoBehaviour
{
    #region Serialized Fields
    [Tooltip("Bir satira kac kart sigsin (referans genislik). Az kartli satirlar ortalanir.")]
    [SerializeField] private int perRow = 5;
    [Tooltip("Kart yuksekligi / genisligi (ornek 460/305 = 1.508).")]
    [SerializeField] private float cellAspect = 1.508f;
    [Tooltip("Satir icindeki (HorizontalLayoutGroup) kartlar arasi bosluk — satir prefabiyla ayni olmali.")]
    [SerializeField] private float rowSpacing = 24f;
    #endregion

    #region Private Fields
    private RectTransform _rt;
    private VerticalLayoutGroup _vlg;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        _rt = GetComponent<RectTransform>();
        _vlg = GetComponent<VerticalLayoutGroup>();
    }

    private void OnEnable() { Apply(); }
    private void OnRectTransformDimensionsChange() { Apply(); }
    #endregion

    #region Public Methods
    /// <summary>Tum alt kart LayoutElement'lerini mevcut genislige gore boyutlandirir.</summary>
    public void Apply()
    {
        if (_rt == null) _rt = GetComponent<RectTransform>();
        if (_vlg == null) _vlg = GetComponent<VerticalLayoutGroup>();
        if (perRow < 1) return;
        float w = _rt.rect.width;
        if (w <= 0f) return;

        float padL = _vlg != null ? _vlg.padding.left : 0;
        float padR = _vlg != null ? _vlg.padding.right : 0;
        float avail = w - padL - padR;
        float cardW = (avail - rowSpacing * (perRow - 1)) / perRow;
        if (cardW <= 0f) return;
        float cardH = cardW * cellAspect;

        var les = GetComponentsInChildren<LayoutElement>(true);
        for (int i = 0; i < les.Length; i++)
        {
            var le = les[i];
            if (le.gameObject.name.Contains("Divider")) continue; // ayirici cizgiyi kart boyutuna getirme
            le.preferredWidth = cardW;
            le.preferredHeight = cardH;
            le.minWidth = cardW;
            le.minHeight = cardH;
        }
    }
    #endregion
}

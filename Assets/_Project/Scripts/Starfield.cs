using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Menu arkasina SABIT BEYAZ KARE yildizlar serper (oyun icindeki uzay gorunumu gibi). UI tabanli:
/// bu objenin (tam ekran RectTransform) altina kucuk beyaz Image kareleri rastgele konumlara yerlestirir.
/// Sprite gerekmez — sprite'siz UI Image zaten dolu beyaz karedir. Statik (hareket/parildama yok).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class Starfield : MonoBehaviour
{
    #region Serialized Fields
    [Tooltip("Kac yildiz serpilsin.")]
    [SerializeField] private int starCount = 140;

    [Tooltip("Yildiz kare boyutu araligi (piksel).")]
    [SerializeField] private Vector2 sizeRange = new Vector2(2f, 5f);

    [Tooltip("Yildiz rengi (beyaz).")]
    [SerializeField] private Color starColor = Color.white;

    [Tooltip("Bazi yildizlar daha soluk olsun (alpha alt siniri) — derinlik hissi.")]
    [Range(0f, 1f)]
    [SerializeField] private float minAlpha = 0.35f;
    #endregion

    #region Unity Callbacks
    private void Start()
    {
        BuildStars();
    }
    #endregion

    #region Private Methods
    private void BuildStars()
    {
        var parent = (RectTransform)transform;
        for (int i = 0; i < starCount; i++)
        {
            var go = new GameObject("Star", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);

            // Tam ekrana rastgele dagit (anchor 0-1)
            Vector2 anchor = new Vector2(Random.value, Random.value);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            float s = Random.Range(sizeRange.x, sizeRange.y);
            rt.sizeDelta = new Vector2(s, s);
            rt.anchoredPosition = Vector2.zero;

            var img = go.GetComponent<Image>();
            Color c = starColor;
            c.a = Random.Range(minAlpha, 1f); // rastgele soluk/parlak -> derinlik
            img.color = c;
            img.raycastTarget = false; // dokunmayi yutmasin
        }
    }
    #endregion
}

using UnityEngine;

/// <summary>
/// Dash sirasinda geride birakilan saydam "hayalet" kopya (Cyberpunk Edgerunners Sandevistan hissi).
/// Kaynak SpriteRenderer'in o anki karesini/yonunu/olcegini kopyalar, verilen renkle saydam basar,
/// sonra kendini soldurup yok eder. Havuz yok — boss az sayida uretir; her biri kisa omurlu.
/// </summary>
public class AfterImage : MonoBehaviour
{
    #region Private Fields
    private SpriteRenderer _sr;
    private Color _startColor;
    private float _fadeTime = 0.35f;
    private float _elapsed;
    #endregion

    #region Public API
    /// <summary>Verilen kaynak SpriteRenderer'dan bir hayalet olusturur ve fadeTime boyunca soldurur.</summary>
    public static void Spawn(SpriteRenderer source, Color tint, float alpha, float fadeTime, int sortingOrder)
    {
        if (source == null || source.sprite == null) return;

        var go = new GameObject("AfterImage");
        go.transform.position = source.transform.position;
        go.transform.rotation = source.transform.rotation;
        go.transform.localScale = source.transform.lossyScale;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = source.sprite;
        sr.flipX = source.flipX;
        sr.flipY = source.flipY;
        sr.sortingLayerID = source.sortingLayerID;
        sr.sortingOrder = sortingOrder;

        Color c = tint;
        c.a = alpha;
        sr.color = c;

        var ai = go.AddComponent<AfterImage>();
        ai._sr = sr;
        ai._startColor = c;
        ai._fadeTime = Mathf.Max(0.01f, fadeTime);
    }
    #endregion

    #region Unity Callbacks
    private void Update()
    {
        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _fadeTime);

        Color c = _startColor;
        c.a = Mathf.Lerp(_startColor.a, 0f, t);
        _sr.color = c;

        if (t >= 1f) Destroy(gameObject);
    }
    #endregion
}

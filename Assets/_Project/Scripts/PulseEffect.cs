using UnityEngine;

/// <summary>
/// Basit nabiz (pulse) efekti: takildigi objenin localScale'ini sinusla min-max arasinda salinim yaptirir.
/// Combo silah slotunu vurgulamak icin kullanilir (oyuncu ozel oldugunu anlasin). Unscaled time — panel
/// timeScale=0 olsa bile calisir. OnDisable'da orijinal olceye doner (kalinti kalmaz).
/// </summary>
public class PulseEffect : MonoBehaviour
{
    #region Serialized Fields
    [Tooltip("En kucuk olcek carpani.")]
    [SerializeField] private float minScale = 1.0f;
    [Tooltip("En buyuk olcek carpani.")]
    [SerializeField] private float maxScale = 1.12f;
    [Tooltip("Nabiz hizi (rad/sn). Buyuk = hizli.")]
    [SerializeField] private float speed = 3.5f;
    #endregion

    #region Private Fields
    private Vector3 _baseScale = Vector3.one;
    private bool _captured;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        if (!_captured) { _baseScale = transform.localScale; _captured = true; }
    }

    private void Update()
    {
        float t = (Mathf.Sin(Time.unscaledTime * speed) + 1f) * 0.5f; // 0..1
        transform.localScale = _baseScale * Mathf.Lerp(minScale, maxScale, t);
    }

    private void OnDisable()
    {
        if (_captured) transform.localScale = _baseScale; // kalinti kalmasin
    }
    #endregion
}

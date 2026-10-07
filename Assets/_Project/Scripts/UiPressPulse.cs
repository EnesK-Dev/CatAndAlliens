using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Bir UI elemanina basinca (pointer down) aninda hafif buyuyup (pop) yumusak sekilde 1'e geri doner.
/// "Basildi" hissini verir. Build ekranindaki katalog/stat kutularina eklenir. Pointer down'da calistigi
/// icin, tiklamayla eleman yeniden olusturulsa (Rebuild) bile pop anlik gorunur. Zaman timeScale'den
/// bagimsiz (unscaled) — menu/pause sirasinda da calisir.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class UiPressPulse : MonoBehaviour, IPointerDownHandler
{
    #region Serialized Fields
    [Tooltip("Basinca aninda ulasilan olcek (pop buyuklugu).")]
    [SerializeField] private float punchScale = 1.15f;

    [Tooltip("1'e geri donus hizi (buyuk = daha hizli yerine oturur).")]
    [SerializeField] private float settleSpeed = 12f;
    #endregion

    #region Private Fields
    private RectTransform _rt;
    #endregion

    #region Unity Callbacks
    private void Awake() { _rt = transform as RectTransform; }

    private void OnDisable() { if (_rt != null) _rt.localScale = Vector3.one; }

    private void Update()
    {
        if (_rt == null) return;
        if (Mathf.Abs(_rt.localScale.x - 1f) > 0.001f)
        {
            float s = Mathf.Lerp(_rt.localScale.x, 1f, Time.unscaledDeltaTime * settleSpeed);
            _rt.localScale = new Vector3(s, s, 1f);
        }
    }
    #endregion

    #region Public Methods
    /// <summary>Basilinca aninda pop'a buyut; Update yumusakca 1'e dondurur.</summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (_rt != null) _rt.localScale = new Vector3(punchScale, punchScale, 1f);
    }
    #endregion
}

using System.Collections;
using UnityEngine;

/// <summary>
/// Player oldugunde olum animasyonu boyunca kamerayi oyuncunun oldugu noktaya
/// yaklastirir (zoom + pan). player.cs'in static OnPlayerDeathStarted event'ini dinler.
/// </summary>
public class CameraDeathZoom : MonoBehaviour
{
    #region Serialized Fields
    [Header("Zoom Ayarlari")]
    [Tooltip("Hedef orthographic size - kucuk deger = daha fazla yakinlasma.")]
    [SerializeField] private float targetOrthographicSize = 2.5f;

    [Tooltip("Zoom suresi - player.cs'teki deathAnimDuration ile es zamanli olmasi icin ona yakin tut.")]
    [SerializeField] private float zoomDuration = 0.8f;

    [SerializeField] private AnimationCurve zoomCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    #endregion

    #region Private Fields
    private Camera _camera;
    private Coroutine _zoomRoutine;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        _camera = GetComponent<Camera>();
    }

    private void OnEnable()
    {
        player.OnPlayerDeathStarted += HandlePlayerDeathStarted;
    }

    private void OnDisable()
    {
        player.OnPlayerDeathStarted -= HandlePlayerDeathStarted;

        if (_zoomRoutine != null)
        {
            StopCoroutine(_zoomRoutine);
            _zoomRoutine = null;
        }
    }
    #endregion

    #region Private Methods
    private void HandlePlayerDeathStarted(Vector3 deathPosition)
    {
        if (_camera == null) return;

        if (_zoomRoutine != null)
            StopCoroutine(_zoomRoutine);

        _zoomRoutine = StartCoroutine(ZoomRoutine(deathPosition));
    }

    private IEnumerator ZoomRoutine(Vector3 deathPosition)
    {
        float startSize = _camera.orthographicSize;
        Vector3 startPosition = transform.position;
        Vector3 targetPosition = new Vector3(deathPosition.x, deathPosition.y, startPosition.z);

        float elapsed = 0f;
        while (elapsed < zoomDuration)
        {
            float t = zoomCurve.Evaluate(elapsed / zoomDuration);
            _camera.orthographicSize = Mathf.Lerp(startSize, targetOrthographicSize, t);
            transform.position = Vector3.Lerp(startPosition, targetPosition, t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        _camera.orthographicSize = targetOrthographicSize;
        transform.position = targetPosition;
        _zoomRoutine = null;
    }
    #endregion
}

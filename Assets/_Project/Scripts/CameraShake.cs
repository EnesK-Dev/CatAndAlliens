using System.Collections;
using UnityEngine;

/// <summary>
/// Kamerayi kisa sureligine rastgele sarsar. Vurus ve hasar geri bildirimi icin kullanilir.
/// Ust uste gelen sarsintilarda GUCLU olan kazanir — zayif bir sarsinti devam eden guclu
/// olani kesmez (vurus sarsintisi hasar sarsintisini bozmasin diye).
/// </summary>
public class CameraShake : MonoBehaviour
{
    #region Private Fields

    // Sallanti bittikten sonra donulecek orijinal lokal pozisyon
    private Vector3 originalPosition;
    private Coroutine shakeRoutine;
    private float activeMagnitude;

    #endregion

    #region Unity Callbacks

    private void Start()
    {
        originalPosition = transform.localPosition;
    }

    private void OnDisable()
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            shakeRoutine = null;
        }

        activeMagnitude = 0f;
        transform.localPosition = originalPosition;
    }

    #endregion

    #region Public Methods

    /// <summary>Kamerayi verilen sure ve siddette sarsar.</summary>
    /// <param name="duration">Sarsinti suresi (saniye).</param>
    /// <param name="magnitude">Sarsinti siddeti (dunya birimi cinsinden sapma).</param>
    public void TriggerShake(float duration, float magnitude)
    {
        // Devam eden sarsinti daha guclüyse yenisini yok say
        if (shakeRoutine != null && magnitude < activeMagnitude)
            return;

        if (shakeRoutine != null)
            StopCoroutine(shakeRoutine);

        activeMagnitude = magnitude;
        shakeRoutine = StartCoroutine(ShakeRoutine(duration, magnitude));
    }

    #endregion

    #region Private Methods

    private IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // Sona dogru sonumleme — sarsinti aniden kesilmez, yumusakca durur
            float damper = 1f - (elapsed / duration);
            float x = Random.Range(-1f, 1f) * magnitude * damper;
            float y = Random.Range(-1f, 1f) * magnitude * damper;

            transform.localPosition = new Vector3(
                originalPosition.x + x,
                originalPosition.y + y,
                originalPosition.z);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = originalPosition;
        shakeRoutine = null;
        activeMagnitude = 0f;
    }

    #endregion
}

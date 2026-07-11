using System.Collections;
using UnityEngine;

/// <summary>
/// Pençe vuruşu görselini bir SpriteRenderer üzerinde frame frame oynatır.
/// attackPointObject aktif olunca (SetActive true) OnEnable ile animasyon baştan başlar,
/// pasif olunca (SetActive false) OnDisable ile durur. Animator Controller gerektirmez.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class ClawSlashVisual : MonoBehaviour
{
    #region Serialized Fields
    [Tooltip("Pençe animasyonunun sırayla oynatılacak frame'leri (1..N)")]
    [SerializeField] private Sprite[] frames;

    [Tooltip("Saniyede oynatılacak frame sayısı")]
    [SerializeField] private float frameRate = 24f;

    [Tooltip("Animasyon bitince baştan dönsün mü? (false = son frame'de kalır)")]
    [SerializeField] private bool loop = false;
    #endregion

    #region Private Fields
    private SpriteRenderer spriteRenderer;
    private Coroutine playRoutine;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        if (frames == null || frames.Length == 0 || frameRate <= 0f) return;
        playRoutine = StartCoroutine(PlayFrames());
    }

    private void OnDisable()
    {
        // Coroutine cleanup — obje pasif olurken oynatmayı bırak
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }
    }
    #endregion

    #region Private Methods
    private IEnumerator PlayFrames()
    {
        WaitForSeconds wait = new WaitForSeconds(1f / frameRate);

        do
        {
            for (int i = 0; i < frames.Length; i++)
            {
                spriteRenderer.sprite = frames[i];
                yield return wait;
            }
        }
        while (loop);

        playRoutine = null;
    }
    #endregion
}

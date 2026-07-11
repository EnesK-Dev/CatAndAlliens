using System;
using System.Collections;
using UnityEngine;

public class BombWarning : MonoBehaviour
{
    #region Serialized Fields
    [Header("Görsel Referansları")]
    [SerializeField] private SpriteRenderer warningSpriteRenderer;
    [SerializeField] private SpriteRenderer explosionSpriteRenderer;

    [Header("Patlama Animasyon Ayarları")]
    [SerializeField] private Sprite[] explosionFrames;
    [SerializeField] private float explosionFrameRate = 12f;
    #endregion

    #region Private Fields
    private float warningDuration;
    private float explosionDuration;
    private float explosionRadius;
    private float explosionDamage;
    private LayerMask playerLayer;
    private Action onFinished;
    private Coroutine bombCoroutine;
    #endregion

    #region Unity Callbacks
    private void OnDisable()
    {
        StopBombCoroutine();
    }
    #endregion

    #region Public Methods
    /// <summary>Pool'dan alınmadan önce çalışma değerlerini ayarlar. BombRainSystem tarafından çağrılır.</summary>
    public void Initialize(float warningDur, float explosionDur, float radius, float damage, LayerMask layer, Action onComplete)
    {
        warningDuration = warningDur;
        explosionDuration = explosionDur;
        explosionRadius = radius;
        explosionDamage = damage;
        playerLayer = layer;
        onFinished = onComplete;
    }

    /// <summary>Bombayı verilen pozisyonda başlatır: uyarı → patlama → pool'a dön.</summary>
    public void Activate(Vector2 position)
    {
        transform.position = position;
        gameObject.SetActive(true);
        StopBombCoroutine();
        bombCoroutine = StartCoroutine(BombSequence());
    }
    #endregion

    #region Private Methods
    private void StopBombCoroutine()
    {
        if (bombCoroutine != null)
        {
            StopCoroutine(bombCoroutine);
            bombCoroutine = null;
        }
    }

    private IEnumerator BombSequence()
    {
        Debug.Log($"[BombWarning] Uyarı başladı: {transform.position}, süre: {warningDuration}s");
        SetVisuals(warning: true, explosion: false);
        yield return new WaitForSeconds(warningDuration);

        Debug.Log($"[BombWarning] Patlama başladı. Frame sayısı: {explosionFrames?.Length ?? 0}, FPS: {explosionFrameRate}");
        SetVisuals(warning: false, explosion: true);
        ApplyExplosionDamage();
        yield return PlayExplosionVisual();

        SetVisuals(warning: false, explosion: false);
        // bombCoroutine'i önce null'la: SetActive(false) → OnDisable → StopCoroutine zincirini kırar
        bombCoroutine = null;
        onFinished?.Invoke();
        gameObject.SetActive(false);
    }

    private IEnumerator PlayExplosionVisual()
    {
        if (explosionFrames != null && explosionFrames.Length > 0 && explosionSpriteRenderer != null)
        {
            WaitForSeconds wait = new WaitForSeconds(1f / explosionFrameRate);
            for (int i = 0; i < explosionFrames.Length; i++)
            {
                explosionSpriteRenderer.sprite = explosionFrames[i];
                yield return wait;
            }
        }
        else
        {
            yield return new WaitForSeconds(explosionDuration);
        }
    }

    private void SetVisuals(bool warning, bool explosion)
    {
        if (warningSpriteRenderer != null)
            warningSpriteRenderer.enabled = warning;
        else if (warning)
            Debug.LogWarning("[BombWarning] Warning Sprite Renderer atanmamış!", this);

        if (explosionSpriteRenderer != null)
            explosionSpriteRenderer.enabled = explosion;
        else if (explosion)
            Debug.LogWarning("[BombWarning] Explosion Sprite Renderer atanmamış!", this);
    }

    private void ApplyExplosionDamage()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius, playerLayer);
        foreach (Collider2D hit in hits)
        {
            player playerComponent = hit.GetComponent<player>();
            if (playerComponent != null)
                playerComponent.TakeDamage(explosionDamage);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
    #endregion
}

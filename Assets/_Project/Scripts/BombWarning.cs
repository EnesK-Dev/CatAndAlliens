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

    // Bombardiman modu: patlama dusmanlara da hasar verir (alan temizleme). 0/bos ise sadece player.
    private LayerMask enemyLayers;
    private float enemyExplosionDamage;
    private bool _playerHitThisBomb; // patlama basina oyuncuya SADECE 1 kez hasar

    // Alloc'suz overlap tamponu (mobil: patlama sik olabilir).
    private static readonly Collider2D[] _overlapBuffer = new Collider2D[24];
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
        enemyLayers = default; // varsayilan: dusman hasari yok (BombardmentDirector ayrica acar)
        enemyExplosionDamage = 0f;
    }

    /// <summary>Bombardiman modu: patlama ayrica DUSMANLARA da hasar versin (alan temizleme). Initialize sonrasi cagrilir.</summary>
    public void ConfigureEnemyDamage(LayerMask enemies, float enemyDamage)
    {
        enemyLayers = enemies;
        enemyExplosionDamage = enemyDamage;
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
        GameLog.Log($"[BombWarning] Uyarı başladı: {transform.position}, süre: {warningDuration}s", this);
        SetVisuals(warning: true, explosion: false);
        SfxManager.Play(SfxId.BombWarning); // bomba dusme uyari sesi
        yield return new WaitForSeconds(warningDuration);

        GameLog.Log($"[BombWarning] Patlama başladı. Frame sayısı: {explosionFrames?.Length ?? 0}, FPS: {explosionFrameRate}", this);
        SetVisuals(warning: false, explosion: true);
        SfxManager.Play(SfxId.BombExplosion); // bomba patlama sesi
        yield return PlayExplosionWithDamage(); // patlama animasyonu BOYUNCA hasar penceresi acik (girene de vurur)

        SetVisuals(warning: false, explosion: false);
        // bombCoroutine'i önce null'la: SetActive(false) → OnDisable → StopCoroutine zincirini kırar
        bombCoroutine = null;
        onFinished?.Invoke();
        gameObject.SetActive(false);
    }

    /// <summary>Patlama karelerini oynatirken HER KARE hasar kontrolu yapar — animasyon suresince alana
    /// giren oyuncu da hasar alir (oyuncuya bomba basina 1 kez), dusmanlar surekli temizlenir.</summary>
    private IEnumerator PlayExplosionWithDamage()
    {
        _playerHitThisBomb = false;
        int frameCount = (explosionFrames != null) ? explosionFrames.Length : 0;
        float frameTime = explosionFrameRate > 0f ? 1f / explosionFrameRate : 0.05f;
        float totalDur = frameCount > 0 ? frameCount * frameTime : Mathf.Max(0.05f, explosionDuration);
        if (frameCount > 0 && explosionSpriteRenderer != null) explosionSpriteRenderer.sprite = explosionFrames[0];

        float elapsed = 0f, frameTimer = 0f;
        int idx = 0;
        while (elapsed < totalDur)
        {
            DamageTick();
            float dt = Time.deltaTime;
            elapsed += dt;
            if (frameCount > 0 && explosionSpriteRenderer != null)
            {
                frameTimer += dt;
                while (frameTimer >= frameTime && idx < frameCount - 1) { frameTimer -= frameTime; idx++; explosionSpriteRenderer.sprite = explosionFrames[idx]; }
            }
            yield return null;
        }
    }

    private void SetVisuals(bool warning, bool explosion)
    {
        if (warningSpriteRenderer != null)
            warningSpriteRenderer.enabled = warning;
        else if (warning)
            GameLog.Warning("[BombWarning] Warning Sprite Renderer atanmamış!", this);

        if (explosionSpriteRenderer != null)
            explosionSpriteRenderer.enabled = explosion;
        else if (explosion)
            GameLog.Warning("[BombWarning] Explosion Sprite Renderer atanmamış!", this);
    }

    /// <summary>Patlama alaninda hasar kontrolu (her kare cagrilir). Oyuncu bomba basina 1 kez, dusman surekli.</summary>
    private void DamageTick()
    {
        // Oyun duraklatildiysa (upgrade kart paneli / olum, timeScale=0) HIC hasar verme.
        if (Time.timeScale == 0f) return;

        // Oyuncu: patlama BOYUNCA alana girerse hasar alir — ama bomba basina SADECE 1 kez.
        if (!_playerHitThisBomb)
        {
            int pc = Physics2D.OverlapCircleNonAlloc(transform.position, explosionRadius, _overlapBuffer, playerLayer);
            for (int i = 0; i < pc; i++)
            {
                player playerComponent = _overlapBuffer[i].GetComponent<player>();
                if (playerComponent != null) { playerComponent.TakeDamage(explosionDamage); _playerHitThisBomb = true; break; }
            }
        }

        // Bombardiman modu: dusmanlara da hasar (alani temizler, alana gireni de). BOSS bombadan hasar ALMAZ.
        if (enemyExplosionDamage > 0f && enemyLayers.value != 0)
        {
            int ec = Physics2D.OverlapCircleNonAlloc(transform.position, explosionRadius, _overlapBuffer, enemyLayers);
            for (int i = 0; i < ec; i++)
                EnemyDamage.ApplyNuke(_overlapBuffer[i], enemyExplosionDamage); // core birakir, ultFood BIRAKMAZ; boss'a degmez
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
    #endregion
}

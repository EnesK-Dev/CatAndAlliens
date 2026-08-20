using UnityEngine;

/// <summary>
/// Lazeri 3 parçayla (StartCap, Body, EndCap) görselleştirir.
/// Body, SpriteRenderer Tiled moduyla uzunluğa göre döşenir.
/// </summary>
public class LaserVisual : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField] private Transform startCap;
    [SerializeField] private Transform body;
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private Transform endCap;

    [Tooltip("StartCap ve EndCap sprite'ının Unity birim cinsinden genişliği (px / PPU)")]
    [SerializeField] private float capSizeUnits = 1f;
    #endregion

    #region Private Fields
    private static readonly Vector3 ChargeModeScale = new Vector3(1f, 0.12f, 1f);
    private Animator[] cachedAnimators;
    private SpriteRenderer startCapRenderer;
    private SpriteRenderer endCapRenderer;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        cachedAnimators = GetComponentsInChildren<Animator>();

        startCapRenderer = startCap.GetComponent<SpriteRenderer>();
        endCapRenderer   = endCap.GetComponent<SpriteRenderer>();

        // Body altta, cap'ler üste çizilir — şeffaf kenarlar body'yi gizlemez
        bodyRenderer.sortingOrder   = 0;
        startCapRenderer.sortingOrder = 1;
        endCapRenderer.sortingOrder   = 1;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Lazer görselini iki dünya koordinatı arasına konumlandırır ve boyutlandırır.
    /// Her frame çağrılabilir.
    /// </summary>
    public void UpdateLaser(Vector2 startWorld, Vector2 endWorld)
    {
        Vector2 dir = endWorld - startWorld;
        float length = dir.magnitude;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        transform.SetPositionAndRotation(
            new Vector3(startWorld.x, startWorld.y, 0f),
            Quaternion.Euler(0f, 0f, angle)
        );

        // Body tüm uzunluğu kaplar — sprite'ların şeffaf kenarları sorun çıkarmaz
        body.localPosition = new Vector3(length * 0.5f, 0f, 0f);
        if (bodyRenderer != null)
            bodyRenderer.size = new Vector2(length, 1f);

        // Cap'ler body'nin üstüne bindirilir, başlangıç ve bitiş noktalarına yerleşir
        startCap.localPosition = new Vector3(capSizeUnits * 0.5f, 0f, 0f);
        endCap.localPosition   = new Vector3(length - capSizeUnits * 0.5f, 0f, 0f);
    }

    /// <summary>
    /// Şarj modunda lazeri ince gösterir; ateş moduna geçince animasyonları baştan oynatır.
    /// </summary>
    /// <param name="fireWidthMultiplier">Ateş modunda kalınlık çarpanı (1 = normal). Zorlukla büyür.</param>
    public void SetChargeMode(bool isCharging, float fireWidthMultiplier = 1f)
    {
        transform.localScale = isCharging ? ChargeModeScale : new Vector3(1f, fireWidthMultiplier, 1f);

        if (!isCharging)
            ResetAnimations();
    }

    /// <summary>Lazer görselini etkinleştirir.</summary>
    public void Show() => gameObject.SetActive(true);

    /// <summary>Lazer görselini devre dışı bırakır.</summary>
    public void Hide() => gameObject.SetActive(false);
    #endregion

    #region Private Methods
    private void ResetAnimations()
    {
        for (int i = 0; i < cachedAnimators.Length; i++)
            cachedAnimators[i].Play(0, -1, 0f);
    }
    #endregion
}

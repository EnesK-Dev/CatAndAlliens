using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dash butonunun cooldown gostergesi. player.DashCooldownNormalized'i (0->1) okur ve bir Filled
/// (dikey, alttan) Image'in fillAmount'unu gunceller: buton asagidan yukari dolar. Dolunca (hazir)
/// renk NETLESIR (readyColor), cooldown'dayken sonuk (chargingColor). Sadece gorsel — dash mantigi player'da.
/// </summary>
public class DashButtonUI : MonoBehaviour
{
    #region Serialized Fields
    [Tooltip("Bos birakilirsa sahnede otomatik bulunur.")]
    [SerializeField] private player playerRef;

    [Tooltip("Cooldown dolgusu. Image Type=Filled, Fill Method=Vertical, Fill Origin=Bottom olmali.")]
    [SerializeField] private Image fillImage;

    [Tooltip("Cooldown dolarken (hazir degilken) dolgu rengi — sonuk.")]
    [SerializeField] private Color chargingColor = new Color(0.35f, 0.72f, 1f, 0.45f);

    [Tooltip("Dash hazir olunca dolgu rengi — net/parlak.")]
    [SerializeField] private Color readyColor = new Color(0.35f, 0.72f, 1f, 1f);
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        if (playerRef == null)
            playerRef = FindFirstObjectByType<player>();
    }

    private void Update()
    {
        if (playerRef == null || fillImage == null) return;

        fillImage.fillAmount = playerRef.DashCooldownNormalized; // 0 (yeni atildi) -> 1 (hazir)
        fillImage.color = playerRef.IsDashReady ? readyColor : chargingColor;
    }
    #endregion
}

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dash butonunun cooldown gostergesi. player.DashCooldownNormalized'i (0=yeni atildi -> 1=hazir) okur ve
/// butonun ustundeki koyu "ortu" (Filled, Vertical, Origin=Top) Image'in fillAmount'unu 1-hazir olarak
/// gunceller: cooldown boyunca buton karartilir, dolarken ortu ALTTAN yukari cekilip ikon belirir, hazir
/// olunca ortu tamamen kaybolur (buton NETLESIR). Sadece gorsel — dash mantigi player.cs'te.
/// </summary>
public class DashButtonUI : MonoBehaviour
{
    #region Serialized Fields
    [Tooltip("Bos birakilirsa sahnede otomatik bulunur.")]
    [SerializeField] private player playerRef;

    [Tooltip("Cooldown ortusu. Image Type=Filled, Fill Method=Vertical, Fill Origin=Top olmali. Butonun USTUNDE (son child).")]
    [SerializeField] private Image fillImage;

    [Tooltip("Cooldown ortusunun rengi (koyu + yari saydam onerilir, orn. siyah alpha 0.6).")]
    [SerializeField] private Color cooldownColor = new Color(0f, 0f, 0f, 0.6f);
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

        float ready = playerRef.DashCooldownNormalized; // 0 (yeni atildi) -> 1 (hazir)
        fillImage.color = cooldownColor;
        fillImage.fillAmount = 1f - ready;   // dolarken ortu azalir (alttan yukari acilir)
        fillImage.enabled = ready < 1f;      // hazir olunca ortuyu tamamen gizle
    }
    #endregion
}

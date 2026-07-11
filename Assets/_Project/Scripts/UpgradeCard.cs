using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Upgrade panelindeki TEK bir kartin gorseli ve tiklama davranisi. Kendi mantigini tutmaz;
/// UpgradeSelectionUI'dan Bind(...) ile beslenir ve tiklaninca verilen callback'i cagirir.
/// Sahnede panel altinda sabit 3 kart olarak durur (pooling gerekmez — hep 3 tane).
/// </summary>
public class UpgradeCard : MonoBehaviour
{
    #region Serialized Fields
    [Tooltip("Buff ikonu — hangi stat'i arttirdigi gorseli.")]
    [SerializeField] private Image iconImage;

    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;

    [Tooltip("Kart secilince ulasilacak seviye — 'Lv.N' olarak yazilir.")]
    [SerializeField] private TMP_Text levelText;

    [SerializeField] private Button selectButton;
    #endregion

    #region Private Fields
    private int _optionIndex;
    private Action<int> _onSelected;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        if (selectButton != null)
            selectButton.onClick.AddListener(HandleClick);
    }

    private void OnDestroy()
    {
        if (selectButton != null)
            selectButton.onClick.RemoveListener(HandleClick);
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Karti bir upgrade secenegine baglar: gorselleri doldurur ve tiklaninca hangi indeksin
    /// secildigini callback ile bildirecegini ayarlar.
    /// </summary>
    /// <param name="optionIndex">UpgradeSelectionUI icindeki upgrade dizisinin indeksi.</param>
    /// <param name="icon">Buff ikonu sprite'i.</param>
    /// <param name="title">Kart basligi (Ingilizce, orn. "Damage Up").</param>
    /// <param name="description">Kisa aciklama (Ingilizce).</param>
    /// <param name="displayLevel">Kart secilince ulasilacak seviye (Lv.N).</param>
    /// <param name="onSelected">Tiklaninca cagrilacak callback; parametre optionIndex.</param>
    public void Bind(int optionIndex, Sprite icon, string title, string description, int displayLevel, Action<int> onSelected)
    {
        _optionIndex = optionIndex;
        _onSelected = onSelected;

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null; // ikon yoksa bos kutu gorunmesin
        }

        if (titleText != null) titleText.text = title;
        if (descriptionText != null) descriptionText.text = description;
        if (levelText != null) levelText.SetText("Lv.{0}", displayLevel); // alloc yok (TMP)
    }
    #endregion

    #region Private Methods
    private void HandleClick()
    {
        _onSelected?.Invoke(_optionIndex);
    }
    #endregion
}

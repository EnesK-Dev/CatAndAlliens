using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UIImage = UnityEngine.UI.Image;

/// <summary>
/// Shop'ta TEK bir kart satiri. ShopUI tarafindan Bind(...) ile beslenir; Buy'a basilinca callback cagirir.
/// Kendi mantigini tutmaz (tek-sorumluluk). Sahip olunan kart icin Buy pasif + OWNED rozeti.
/// </summary>
public class ShopItemUI : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private Button buyButton;
    [SerializeField] private GameObject ownedBadge;
    [SerializeField] private UIImage iconImage;
    [SerializeField] private UIImage frameImage;   // kart cerceve (kategori rengi)
    [SerializeField] private TMP_Text ownedText;   // "x3" sahip olunan adet
    #endregion

    #region Private Fields
    private string _cardId;
    private Action<string> _onBuy;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        if (buyButton != null) buyButton.onClick.AddListener(HandleBuy);
    }
    private void OnDestroy()
    {
        if (buyButton != null) buyButton.onClick.RemoveListener(HandleBuy);
    }
    #endregion

    #region Public Methods
    /// <summary>Karti satira baglar. owned=sahipse Buy kapali + OWNED; degilse maliyet + (canAfford ise) aktif.</summary>
    public void Bind(CardDefinition card, int ownedCount, bool canAfford, Action<string> onBuy)
    {
        _cardId = card != null ? card.id : "";
        _onBuy = onBuy;

        if (nameText != null) nameText.text = card != null ? card.displayName : "?";
        if (descText != null) descText.text = card != null ? card.description : "";
        if (iconImage != null)
        {
            iconImage.sprite = card != null ? card.icon : null;
            iconImage.enabled = card != null && card.icon != null;
        }
        if (frameImage != null && card != null && card.frameSprite != null) frameImage.sprite = card.frameSprite;
        if (ownedText != null) ownedText.text = "x" + ownedCount;
        if (ownedBadge != null) ownedBadge.SetActive(false);
        if (costText != null) costText.text = card != null ? card.cost.ToString() : "0";
        if (buyButton != null) buyButton.interactable = canAfford; // tekrarlanabilir alim
    }
    #endregion

    #region Private Methods
    private void HandleBuy()
    {
        SfxManager.Play(SfxId.ButtonClick);
        _onBuy?.Invoke(_cardId);
    }
    #endregion
}

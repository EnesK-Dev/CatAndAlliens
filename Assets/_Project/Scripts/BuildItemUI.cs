using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Build ekraninda TEK bir sahip-olunan kart satiri. BuildUI Bind(...) ile besler; [+]/[-] ile aktif slota
/// kart ekler/cikarir (callback). Tek-sorumluluk: gorsel + tiklama; kural/limit BuildUI'da.
/// </summary>
public class BuildItemUI : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text countText;   // "x2 / 20" (slotta kac / izin verilen max)
    [SerializeField] private Button addButton;
    [SerializeField] private Button removeButton;
    #endregion

    #region Private Fields
    private string _cardId;
    private Action<string> _onAdd, _onRemove;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        if (addButton != null) addButton.onClick.AddListener(Add);
        if (removeButton != null) removeButton.onClick.AddListener(Remove);
    }
    private void OnDestroy()
    {
        if (addButton != null) addButton.onClick.RemoveListener(Add);
        if (removeButton != null) removeButton.onClick.RemoveListener(Remove);
    }
    #endregion

    #region Public Methods
    /// <summary>Satiri baglar. inSlot=aktif slotta kac tane, maxAllowed=izin verilen ust sinir, slotFull=slot 20 dolu mu.</summary>
    public void Bind(CardDefinition card, int inSlot, int maxAllowed, bool slotFull, Action<string> onAdd, Action<string> onRemove)
    {
        _cardId = card != null ? card.id : "";
        _onAdd = onAdd; _onRemove = onRemove;
        if (nameText != null) nameText.text = card != null ? card.displayName : "?";
        if (countText != null) countText.text = "x" + inSlot + " / " + maxAllowed;
        if (addButton != null) addButton.interactable = inSlot < maxAllowed && !slotFull;
        if (removeButton != null) removeButton.interactable = inSlot > 0;
    }
    #endregion

    #region Private Methods
    private void Add()    { SfxManager.Play(SfxId.ButtonClick); _onAdd?.Invoke(_cardId); }
    private void Remove() { SfxManager.Play(SfxId.ButtonClick); _onRemove?.Invoke(_cardId); }
    #endregion
}

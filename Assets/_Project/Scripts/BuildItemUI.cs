using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UIImage = UnityEngine.UI.Image;

/// <summary>
/// Build ekraninda TEK bir sahip-olunan KART. BuildUI Bind(...) ile besler; [+]/[-] ile aktif slota
/// kart ekler/cikarir. Kart gorseli shop kartiyla ayni (cerceve+ikon+baslik); slotta kac tane
/// dizildigi arka "kart tepesi" dilimleriyle (stack) gosterilir — ayni kart eklendikce ust uste biner.
/// Tek-sorumluluk: gorsel + tiklama; kural/limit BuildUI'da.
/// </summary>
public class BuildItemUI : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descText;
    [SerializeField] private TMP_Text countText;    // "2 / 20" (slotta kac / izin verilen max)
    [SerializeField] private UIImage iconImage;
    [SerializeField] private UIImage frameImage;     // kategori rengi (runtime)
    [SerializeField] private Transform stackParent;  // slottaki adet kadar dilim acilir
    [SerializeField] private CanvasGroup cardGroup;  // slotta 0 ise soluk (gri/inactif)
    [SerializeField] private Button addButton;
    [SerializeField] private Button removeButton;
    [Tooltip("Silah ekli DEGILKEN gosterilen EQUIP butonu (ekli olunca +/- gosterilir).")]
    [SerializeField] private Button equipButton;
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
        if (equipButton != null) equipButton.onClick.AddListener(Add);
        if (cardGroup == null)
        {
            var body = transform.Find("Body");
            var target = body != null ? body.gameObject : gameObject;
            cardGroup = target.GetComponent<CanvasGroup>();
            if (cardGroup == null) cardGroup = target.AddComponent<CanvasGroup>();
        }
    }
    private void OnDestroy()
    {
        if (addButton != null) addButton.onClick.RemoveListener(Add);
        if (removeButton != null) removeButton.onClick.RemoveListener(Remove);
        if (equipButton != null) equipButton.onClick.RemoveListener(Add);
    }
    #endregion

    #region Public Methods
    /// <summary>Karti baglar. inSlot=aktif slotta kac tane, maxAllowed=izin verilen ust sinir, slotFull=slot 20 dolu mu.</summary>
    public void Bind(CardDefinition card, int inSlot, int maxAllowed, bool slotFull, Action<string> onAdd, Action<string> onRemove)
    {
        _cardId = card != null ? card.id : "";
        _onAdd = onAdd; _onRemove = onRemove;

        if (nameText != null) nameText.text = card != null ? card.displayName : "?";
        if (descText != null) descText.text = card != null ? card.description : "";
        if (iconImage != null)
        {
            iconImage.sprite = card != null ? card.icon : null;
            iconImage.enabled = card != null && card.icon != null;
        }
        if (frameImage != null && card != null && card.frameSprite != null) frameImage.sprite = card.frameSprite;
        if (frameImage != null) frameImage.color = card != null ? card.frameTint : Color.white;
        if (countText != null) countText.text = inSlot + " / " + maxAllowed;
        if (addButton != null) addButton.interactable = inSlot < maxAllowed && !slotFull;
        if (removeButton != null) removeButton.interactable = inSlot > 0;
        if (cardGroup != null) cardGroup.alpha = inSlot > 0 ? 1f : 0.5f; // slotta yoksa soluk (Body; butonlar disinda)

        // Slotta hic yoksa EQUIP; en az 1 varsa +/-  (tum kartlar).
        bool showEquip = inSlot == 0;
        if (equipButton != null)
        {
            equipButton.gameObject.SetActive(showEquip);
            equipButton.interactable = inSlot < maxAllowed && !slotFull;
        }
        if (addButton != null) addButton.gameObject.SetActive(!showEquip);
        if (removeButton != null) removeButton.gameObject.SetActive(!showEquip);

        UpdateStack(inSlot, card != null ? card.frameSprite : null, card != null ? card.frameTint : Color.white);
    }
    #endregion

    #region Private Methods
    /// <summary>Slottaki adet kadar arka "kart tepesi" dilimini acar (en yakindan baslar).</summary>
    private void UpdateStack(int inSlot, Sprite frame, Color tint)
    {
        if (stackParent == null) return;
        int n = stackParent.childCount;
        int show = Mathf.Clamp(inSlot, 0, n);
        for (int i = 0; i < n; i++)
        {
            var child = stackParent.GetChild(i);
            bool on = i >= n - show; // son 'show' cocuk = karta en yakin dilimler
            child.gameObject.SetActive(on);
            if (on)
            {
                var img = child.GetComponent<UIImage>();
                if (img != null) { if (frame != null) img.sprite = frame; img.color = tint; }
            }
        }
    }

    private void Add()    { SfxManager.Play(SfxId.ButtonClick); _onAdd?.Invoke(_cardId); }
    private void Remove() { SfxManager.Play(SfxId.ButtonClick); _onRemove?.Invoke(_cardId); }
    #endregion
}

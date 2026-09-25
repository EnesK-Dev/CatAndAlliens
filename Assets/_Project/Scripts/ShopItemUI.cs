using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UIImage = UnityEngine.UI.Image;

/// <summary>
/// Shop'ta TEK bir kart. ShopUI tarafindan Bind(...) ile beslenir; Buy'a basilinca callback cagirir.
/// Buy geri bildirimi: kart yukari ziplar (Body animasyonu) + (atanmissa) satin alma sesi.
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
    [SerializeField] private TMP_Text ownedText;   // (eski) "x3" — artik kullanilmiyor, yerine stack gorseli

    [Header("Owned Stack (sahip olunan adet gorseli)")]
    [Tooltip("Kartin arkasindaki 'kart tepesi' dilimlerinin parent'i. Sahip olunan adet kadar dilim acilir.")]
    [SerializeField] private Transform stackParent;
    [Tooltip("Kilitli/sold-out iken karti soluklastirmak icin (bos ise Awake'te Body'ye eklenir).")]
    [SerializeField] private CanvasGroup cardGroup;
    [Tooltip("Max adet alininca gosterilen capraz 'SOLD OUT' overlay (kapali baslar).")]
    [SerializeField] private GameObject soldOutOverlay;

    [Header("Buy Feedback")]
    [Tooltip("Buy'a basinca yukari ziplayacak govde (tum kart gorselleri bunun altinda).")]
    [SerializeField] private RectTransform bodyRoot;
    [Tooltip("Satin alma sesi. SIMDILIK BOS birakilabilir; ileride klip atanir. Bos ise varsayilan tik sesi calar.")]
    [SerializeField] private AudioClip buySound;
    [Tooltip("Karti yukari kaldirma miktari (piksel).")]
    [SerializeField] private float riseHeight = 55f;
    [Tooltip("Yukari cikma suresi (sn).")]
    [SerializeField] private float riseTime = 0.10f;
    [Tooltip("Geri inme suresi (sn).")]
    [SerializeField] private float settleTime = 0.20f;
    #endregion

    #region Private Fields
    private string _cardId;
    private Action<string> _onBuy;
    private Coroutine _feedbackRoutine;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        if (buyButton != null) buyButton.onClick.AddListener(HandleBuy);
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
        if (buyButton != null) buyButton.onClick.RemoveListener(HandleBuy);
    }

    private void OnDisable()
    {
        // Panel kapaninca animasyonu durdur ve govdeyi yerine dondur.
        if (_feedbackRoutine != null) { StopCoroutine(_feedbackRoutine); _feedbackRoutine = null; }
        if (bodyRoot != null) bodyRoot.anchoredPosition = Vector2.zero;
    }
    #endregion

    #region Public Methods
    /// <summary>Karti satira baglar. owned=sahipse Buy kapali + OWNED; degilse maliyet + (canAfford ise) aktif.</summary>
    public void Bind(CardDefinition card, int ownedCount, bool canAfford, bool locked, bool soldOut, Action<string> onBuy, int displayCost = -1)
    {
        _cardId = card != null ? card.id : "";
        _onBuy = onBuy;
        if (cardGroup != null) cardGroup.alpha = (locked || soldOut) ? 0.45f : 1f; // kilitli/sold-out soluk
        if (soldOutOverlay != null) soldOutOverlay.SetActive(soldOut);

        if (nameText != null) nameText.text = card != null ? card.displayName : "?";
        if (descText != null) descText.text = card != null ? card.description : "";
        if (iconImage != null)
        {
            iconImage.sprite = card != null ? card.icon : null;
            iconImage.enabled = card != null && card.icon != null;
        }
        if (frameImage != null && card != null && card.frameSprite != null) frameImage.sprite = card.frameSprite;
        if (frameImage != null) frameImage.color = card != null ? card.frameTint : Color.white;
        UpdateStack(ownedCount, card != null ? card.frameSprite : null, card != null ? card.frameTint : Color.white);
        if (ownedBadge != null) ownedBadge.SetActive(false);
        if (costText != null) costText.text = (displayCost >= 0 ? displayCost : (card != null ? card.cost : 0)).ToString(); // dinamik (artan) fiyat
        if (buyButton != null) buyButton.interactable = canAfford; // tekrarlanabilir alim
    }
    #endregion

    #region Private Methods
    /// <summary>Sahip olunan adet kadar arka "kart tepesi" dilimini acar (en yakindan baslar). Ikon/yazi yok.</summary>
    private void UpdateStack(int ownedCount, Sprite frame, Color tint)
    {
        if (stackParent == null) return;
        int n = stackParent.childCount;
        int show = Mathf.Clamp(ownedCount, 0, n);
        for (int i = 0; i < n; i++)
        {
            var child = stackParent.GetChild(i);
            bool on = i >= n - show; // son 'show' cocuk = en kucuk offsetli (karta en yakin) dilimler
            child.gameObject.SetActive(on);
            if (on)
            {
                var img = child.GetComponent<UIImage>();
                if (img != null) { if (frame != null) img.sprite = frame; img.color = tint; }
            }
        }
    }

    private void HandleBuy()
    {
        PlayBuySound();

        // Gorsel geri bildirim: kart yukari zipla.
        if (bodyRoot != null && isActiveAndEnabled)
        {
            if (_feedbackRoutine != null) StopCoroutine(_feedbackRoutine);
            _feedbackRoutine = StartCoroutine(RiseRoutine());
        }

        _onBuy?.Invoke(_cardId);
    }

    /// <summary>Atanmis satin alma sesini calar; atanmamissa varsayilan tik sesine duser.</summary>
    private void PlayBuySound()
    {
        if (buySound != null)
        {
            var cam = Camera.main;
            Vector3 pos = cam != null ? cam.transform.position : Vector3.zero;
            AudioSource.PlayClipAtPoint(buySound, pos); // AudioListener.volume (mute) burada da gecerli
        }
        else
        {
            SfxManager.Play(SfxId.ButtonClick); // klip atanana kadar gecici tik
        }
    }

    /// <summary>Body'yi 0 -> yukari (riseHeight) -> 0 yolunda oynatir (zipla geri bildirimi).</summary>
    private IEnumerator RiseRoutine()
    {
        float t = 0f;
        while (t < riseTime)
        {
            t += Time.unscaledDeltaTime;
            float k = riseTime > 0f ? Mathf.Clamp01(t / riseTime) : 1f;
            k = 1f - (1f - k) * (1f - k); // ease-out (hizli cik)
            bodyRoot.anchoredPosition = new Vector2(0f, riseHeight * k);
            yield return null;
        }

        t = 0f;
        while (t < settleTime)
        {
            t += Time.unscaledDeltaTime;
            float k = settleTime > 0f ? Mathf.Clamp01(t / settleTime) : 1f;
            k = k * k; // ease-in (yumusak in)
            bodyRoot.anchoredPosition = new Vector2(0f, riseHeight * (1f - k));
            yield return null;
        }

        bodyRoot.anchoredPosition = Vector2.zero;
        _feedbackRoutine = null;
    }
    #endregion
}

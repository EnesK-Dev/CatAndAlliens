using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ekranin sol üstünde kalp ikonlarini gösterir.
/// Player'in OnHealthChanged event'ini dinler; hasar aninda UI güncellenir.
/// </summary>
public class HeartUI : MonoBehaviour
{
    #region Serialized Fields

    [Header("Referanslar")]
    [SerializeField] private player playerRef;

    [Header("Kalp Sprite'lari")]
    [SerializeField] private Sprite fullHeartSprite;
    [SerializeField] private Sprite halfHeartSprite;
    [SerializeField] private Sprite emptyHeartSprite;

    [Header("Düzen Ayarlari")]
    [SerializeField] private Vector2 startPosition = new Vector2(20f, -20f); // Sol üst köşeden offset
    [SerializeField] private float heartSpacing = 36f;                        // Kalpler arasi piksel mesafe
    [SerializeField] private float heartSize = 32f;                           // Her kalp ikonunun boyutu

    #endregion

    #region Private Fields

    private const int TotalHearts = 9;

    private Image[] heartImages;
    private float maxHealth;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        CreateHeartImages();
    }

    private void Start()
    {
        if (playerRef == null)
        {
            Debug.LogError("[HeartUI] Player referansi atanmadi! Inspector'dan ata.");
            return;
        }

        maxHealth = playerRef.GetMaxHealth();
        playerRef.OnHealthChanged += RefreshHearts;

        // Baslangic durumunu hemen çiz
        RefreshHearts(playerRef.GetCurrentHealth());
    }

    private void OnDestroy()
    {
        if (playerRef != null)
            playerRef.OnHealthChanged -= RefreshHearts;
    }

    #endregion

    #region Public Methods

    /// <summary>Mevcut can degerine göre kalp ikonlarini günceller.</summary>
    public void RefreshHearts(float currentHealth)
    {
        // currentHealth = yarım-kalp cinsinden (ör. 18 = tam dolu, 9 = yarısı)
        for (int i = 0; i < TotalHearts; i++)
        {
            float halfHeartsForThisSlot = currentHealth - (i * 2f);

            if (halfHeartsForThisSlot >= 2f)
                heartImages[i].sprite = fullHeartSprite;
            else if (halfHeartsForThisSlot >= 1f)
                heartImages[i].sprite = halfHeartSprite;
            else
                heartImages[i].sprite = emptyHeartSprite;
        }
    }

    #endregion

    #region Private Methods

    private void CreateHeartImages()
    {
        // HeartUI'in kendi RectTransform'unu Canvas'i tam kaplayacak sekilde ger
        RectTransform selfRect = GetComponent<RectTransform>();
        selfRect.anchorMin = Vector2.zero;
        selfRect.anchorMax = Vector2.one;
        selfRect.offsetMin = Vector2.zero;
        selfRect.offsetMax = Vector2.zero;

        heartImages = new Image[TotalHearts];

        for (int i = 0; i < TotalHearts; i++)
        {
            GameObject heartObj = new GameObject($"Heart_{i + 1}");
            heartObj.transform.SetParent(transform, false);

            RectTransform rect = heartObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f); // Sol üst
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(heartSize, heartSize);
            rect.anchoredPosition = new Vector2(
                startPosition.x + i * heartSpacing,
                startPosition.y
            );

            Image img = heartObj.AddComponent<Image>();
            img.preserveAspect = true;
            heartImages[i] = img;
        }
    }

    #endregion
}

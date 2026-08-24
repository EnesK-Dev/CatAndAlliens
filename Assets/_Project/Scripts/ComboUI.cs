using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Combo sisteminin GORSEL katmani (FAZ 2-3). ComboManager'in static event'lerini dinler,
/// hiçbir gameplay karari vermez — sadece gosterir:
///  - Ekranda rank harfi + "x{sayi}", rank rengine boyanir, her vuruste scale-punch.
///  - Rank yukselince ekran ortasinda pop yazi + rank renginde tam-ekran flash.
///  - En yuksek rank'te (S) ekran flash'i nabiz gibi hafif pulse eder (sustained his).
/// Tum efektler kod-tabanli (asset yok) — placeholder; sonra sprite/particle ile desteklenecek.
/// </summary>
public class ComboUI : MonoBehaviour
{
    #region Serialized Fields
    [Header("Referanslar")]
    [Tooltip("Buyuk rank harfi (E/D/C/B/A/S).")]
    [SerializeField] private TMP_Text rankLabel;
    [Tooltip("Ardisik vurus sayisi (x12).")]
    [SerializeField] private TMP_Text countLabel;
    [Tooltip("Widget'in tamami — combo 0'ken soluklastirmak icin.")]
    [SerializeField] private CanvasGroup rootGroup;
    [Tooltip("Rank yukselince ekran ortasinda beliren pop yazi (baslangicta pasif).")]
    [SerializeField] private TMP_Text rankUpLabel;
    [Tooltip("Tam ekran flash Image'i (rank-up ve S-pulse icin).")]
    [SerializeField] private Image flashImage;

    [Header("Vurus Punch")]
    [Tooltip("Her vuruste rank harfinin siçrayacagi olcek.")]
    [SerializeField] private float hitPunchScale = 1.25f;
    [Tooltip("Punch'in 1'e geri donme hizi (buyuk = hizli toparlar).")]
    [SerializeField] private float punchReturnSpeed = 9f;
    [Tooltip("Combo 0'ken widget'in solukluk seviyesi.")]
    [Range(0f, 1f)]
    [SerializeField] private float idleAlpha = 0.25f;

    [Header("Rank-Up Efekti")]
    [Tooltip("Rank-up flash'inin en yogun saydamsizligi.")]
    [Range(0f, 1f)]
    [SerializeField] private float rankUpFlashAlpha = 0.35f;
    [Tooltip("Pop yazinin baslangic (buyuk) olcegi — 1'e dogru kuculur.")]
    [SerializeField] private float rankUpPopScale = 1.7f;
    [Tooltip("Pop yazinin ekranda kalma suresi (sn).")]
    [SerializeField] private float rankUpShowTime = 0.8f;

    [Header("S-Tier Sustained Pulse")]
    [Tooltip("S rank'teyken flash'in nabiz tepe saydamsizligi (dusuk tutulmali).")]
    [Range(0f, 1f)]
    [SerializeField] private float sPulseAlpha = 0.12f;
    [Tooltip("S-pulse nabiz hizi.")]
    [SerializeField] private float sPulseSpeed = 3f;
    #endregion

    #region Private Fields
    private RectTransform _rankRect;
    private float _punch = 1f;
    private int _currentRank;
    private bool _atMax;
    private Coroutine _rankUpRoutine;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        if (rankLabel != null) _rankRect = rankLabel.rectTransform;
        HideRankUp();
        SetFlashAlpha(0f);
        if (flashImage != null) flashImage.raycastTarget = false; // dokunmayi yutmasin
    }

    private void OnEnable()
    {
        ComboManager.OnComboChanged += HandleComboChanged;
        ComboManager.OnRankChanged += HandleRankChanged;
        // Baslangic durumunu hemen ciz (gec enable olsak bile dogru gorunsun)
        HandleComboChanged(ComboManager.Count, ComboManager.RankIndex);
    }

    private void OnDisable()
    {
        ComboManager.OnComboChanged -= HandleComboChanged;
        ComboManager.OnRankChanged -= HandleRankChanged;

        if (_rankUpRoutine != null)
        {
            StopCoroutine(_rankUpRoutine);
            _rankUpRoutine = null;
        }
    }

    private void Update()
    {
        // Rank harfi punch'ini yumusakca 1'e dondur (procedural sicrama sonrasi toparlanma)
        if (_rankRect != null)
        {
            _punch = Mathf.Lerp(_punch, 1f, Time.deltaTime * punchReturnSpeed);
            _rankRect.localScale = Vector3.one * _punch;
        }

        // S-tier sustained pulse — rank-up flash calismiyorken devreye girer
        if (_atMax && _rankUpRoutine == null)
        {
            float pulse = sPulseAlpha * (0.5f + 0.5f * Mathf.Sin(Time.time * sPulseSpeed));
            SetFlashColorAlpha(ComboManager.RankColorAt(_currentRank), pulse);
        }
    }
    #endregion

    #region Private Methods
    /// <summary>Combo sayisi her degistiginde (vurus veya decay): metin, renk, punch ve solukluk guncellenir.</summary>
    private void HandleComboChanged(int count, int rank)
    {
        _currentRank = rank;
        _atMax = rank >= ComboManager.MaxRankIndex && count > 0;

        Color c = ComboManager.RankColorAt(rank);

        if (rankLabel != null)
        {
            rankLabel.text = ComboManager.RankLabel;
            rankLabel.color = c;
        }
        if (countLabel != null)
        {
            countLabel.SetText("x{0}", count); // alloc yok
            countLabel.color = c;
        }
        if (rootGroup != null)
            rootGroup.alpha = count > 0 ? 1f : idleAlpha;

        // Her vuruste kucuk siçrama (decay'de de tetiklenir ama zararsiz — his olarak "canli" durur)
        _punch = hitPunchScale;

        if (!_atMax) SetFlashAlpha(0f); // S'ten dustuysek pulse'u kapat
    }

    /// <summary>Rank degisince: yukseliste pop + flash tetikle. Duste sessiz (widget rengi zaten degisti).</summary>
    private void HandleRankChanged(int oldRank, int newRank)
    {
        _atMax = newRank >= ComboManager.MaxRankIndex;

        if (newRank > oldRank)
        {
            if (_rankUpRoutine != null) StopCoroutine(_rankUpRoutine);
            _rankUpRoutine = StartCoroutine(RankUpRoutine(newRank));
        }
        else if (!_atMax)
        {
            SetFlashAlpha(0f);
        }
    }

    private IEnumerator RankUpRoutine(int rank)
    {
        Color c = ComboManager.RankColorAt(rank);

        if (rankUpLabel != null)
        {
            rankUpLabel.gameObject.SetActive(true);
            rankUpLabel.text = "RANK " + ComboManager.RankLabel + "!";
        }
        SetFlashColorAlpha(c, rankUpFlashAlpha);

        float t = 0f;
        while (t < rankUpShowTime)
        {
            float n = t / rankUpShowTime;

            if (rankUpLabel != null)
            {
                // Ilk yarida buyukten 1'e otur, ikinci yarida sol
                float scale = Mathf.Lerp(rankUpPopScale, 1f, Mathf.Clamp01(n * 2f));
                rankUpLabel.rectTransform.localScale = Vector3.one * scale;

                Color lc = c;
                lc.a = 1f - Mathf.Clamp01((n - 0.5f) * 2f);
                rankUpLabel.color = lc;
            }

            SetFlashColorAlpha(c, Mathf.Lerp(rankUpFlashAlpha, 0f, n));

            t += Time.deltaTime;
            yield return null;
        }

        HideRankUp();
        _rankUpRoutine = null;
        if (!_atMax) SetFlashAlpha(0f);
    }

    private void HideRankUp()
    {
        if (rankUpLabel != null)
            rankUpLabel.gameObject.SetActive(false);
    }

    private void SetFlashAlpha(float a)
    {
        if (flashImage == null) return;
        Color col = flashImage.color;
        col.a = a;
        flashImage.color = col;
    }

    private void SetFlashColorAlpha(Color c, float a)
    {
        if (flashImage == null) return;
        c.a = a;
        flashImage.color = c;
    }
    #endregion
}

using System.Text;
using UnityEngine;
using TMPro;

/// <summary>
/// Ekranin SOL kenarinda kedinin GUNCEL stat'larini yazili gosterir (oyuncu takip edebilsin).
/// Sadece OKUR: player getter'larindan degerleri alir, hicbir sey degistirmez. Performans icin
/// her karede degil, YALNIZCA stat degistiginde yenilenir (can degisimi + in-run upgrade secimi).
/// God manager degil — tek sorumluluk: stat'lari metne dokmek.
/// </summary>
public class LiveStatsUI : MonoBehaviour
{
    #region Serialized Fields
    [Tooltip("Stat'larin yazilacagi TMP metni (sol panel). Bos ise bu objedeki TMP_Text kullanilir.")]
    [SerializeField] private TMP_Text statsText;

    [Tooltip("Bos birakilirsa Awake'te otomatik bulunur.")]
    [SerializeField] private player playerRef;
    #endregion

    #region Private Fields
    private readonly StringBuilder _sb = new StringBuilder(128);
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        if (statsText == null) statsText = GetComponent<TMP_Text>();
        if (playerRef == null) playerRef = FindFirstObjectByType<player>();
    }

    private void OnEnable()
    {
        // Can degisimi (hasar/heal/maxHP) ve in-run stat upgrade secimi -> paneli yenile.
        if (playerRef != null) playerRef.OnHealthChanged += HandleHealthChanged;
        UpgradeSelectionUI.OnUpgradeSelected += HandleUpgradeSelected;
        Refresh(); // panel her acildiginda guncel degerlerle gorunsun
    }

    private void OnDisable()
    {
        if (playerRef != null) playerRef.OnHealthChanged -= HandleHealthChanged;
        UpgradeSelectionUI.OnUpgradeSelected -= HandleUpgradeSelected;
    }

    private void Start()
    {
        // DeckApplier run basinda stat'lari Start'ta uygular; sirali garanti olmadigi icin
        // ilk frame sonunda bir kez yenile (dogru baslangic degerleri gorunsun).
        StartCoroutine(RefreshEndOfFrame());
    }
    #endregion

    #region Private Methods
    private System.Collections.IEnumerator RefreshEndOfFrame()
    {
        yield return null; // bir frame bekle (DeckApplier.Start bitsin)
        Refresh();
    }

    private void HandleHealthChanged(float _) => Refresh();
    private void HandleUpgradeSelected(UpgradeSelectionUI.UpgradeType _) => Refresh();

    /// <summary>Guncel stat'lari metne yazar. Sadece event'lerde cagrilir (Update'te DEGIL) — alloc dert degil.</summary>
    private void Refresh()
    {
        if (statsText == null || playerRef == null) return;

        const string O = "<color=#FF8A00>"; // turuncu etiket
        const string C = "</color>";
        int hpNow = Mathf.CeilToInt(playerRef.GetCurrentHealth() * 0.5f);
        int hpMax = Mathf.CeilToInt(playerRef.GetMaxHealth() * 0.5f);

        _sb.Clear();
        _sb.Append("<b>").Append(O).Append("STATS").Append(C).Append("</b>\n\n"); // baslik turuncu + altinda bosluk
        _sb.Append(O).Append("DMG").Append(C).Append("  ").Append(Mathf.RoundToInt(playerRef.GetDamage())).Append('\n');
        _sb.Append(O).Append("SPD").Append(C).Append("  ").Append(playerRef.GetAttacksPerSecond().ToString("0.0")).Append("/s\n");
        _sb.Append(O).Append("RNG").Append(C).Append("  ").Append(playerRef.GetAttackRange().ToString("0.0")).Append('\n');
        _sb.Append(O).Append("HP").Append(C).Append("   ").Append(hpNow).Append('/').Append(hpMax).Append('\n');
        _sb.Append(O).Append("MOVE").Append(C).Append(" ").Append(playerRef.GetMoveSpeed().ToString("0.0")).Append('\n');
        _sb.Append(O).Append("DASH").Append(C).Append(" ").Append(playerRef.GetDashCooldownEffective().ToString("0.0")).Append('s');

        statsText.SetText(_sb);
    }
    #endregion
}

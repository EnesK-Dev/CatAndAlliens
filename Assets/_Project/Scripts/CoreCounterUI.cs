using UnityEngine;
using TMPro;

/// <summary>
/// Ekranin saginda toplam core sayisini gosterir. CoreManager'in static OnCoreCountChanged
/// event'ini dinler; baslangicta guncel toplami hemen okur (HeartUI ile ayni pattern).
/// </summary>
public class CoreCounterUI : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField] private TMP_Text countText;
    #endregion

    #region Unity Callbacks
    private void Start()
    {
        CoreManager.OnCoreCountChanged += HandleCoreCountChanged;
        HandleCoreCountChanged(CoreManager.TotalCores); // baslangic durumunu hemen ciz
    }

    private void OnDestroy()
    {
        CoreManager.OnCoreCountChanged -= HandleCoreCountChanged;
    }
    #endregion

    #region Private Methods
    private void HandleCoreCountChanged(int total)
    {
        // Guncel core / sonraki upgrade esigi (hedef): "12/20". Bir sonraki karta ne kadar kaldigini gosterir.
        if (countText == null) return;
        int next = CoreManager.NextThreshold;
        if (next > 0) countText.SetText("{0}/{1}", total, next); // alloc yok
        else countText.SetText("{0}", total);
    }
    #endregion
}

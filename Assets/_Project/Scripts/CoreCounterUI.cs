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
        // Sadece guncel core sayisi (eski esik/slash sistemi kaldirildi).
        if (countText != null)
            countText.SetText("{0}", total); // alloc yok
    }
    #endregion
}

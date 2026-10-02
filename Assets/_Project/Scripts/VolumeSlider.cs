using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bir UI Slider'i muzik veya efekt ses seviyesine baglar (VolumeSettings). Acilista mevcut degeri gosterir,
/// oynatilinca kaydeder. Tek-sorumluluk: sadece slider &lt;-&gt; ses ayari koprusu.
/// </summary>
[RequireComponent(typeof(Slider))]
public class VolumeSlider : MonoBehaviour
{
    #region Serialized Fields
    public enum Channel { Music, Sfx }

    [Tooltip("Bu slider hangi kanali ayarlar: Music veya Sfx.")]
    [SerializeField] private Channel channel = Channel.Music;

    [Tooltip("Bos ise ayni objeden alinir.")]
    [SerializeField] private Slider slider;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        if (slider == null) slider = GetComponent<Slider>();
    }

    private void OnEnable()
    {
        if (slider == null) return;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        // Kayitli degeri notify etmeden goster (Apply tetiklenmesin)
        slider.SetValueWithoutNotify(channel == Channel.Music ? VolumeSettings.Music : VolumeSettings.Sfx);
        slider.onValueChanged.AddListener(Apply);
    }

    private void OnDisable()
    {
        if (slider != null) slider.onValueChanged.RemoveListener(Apply);
    }
    #endregion

    #region Private Methods
    private void Apply(float value)
    {
        if (channel == Channel.Music) VolumeSettings.Music = value;
        else VolumeSettings.Sfx = value;
    }
    #endregion
}

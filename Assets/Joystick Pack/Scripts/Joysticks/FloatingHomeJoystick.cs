using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Floating + her zaman GORUNUR joystick. Klasik FloatingJoystick gibidir (dokundugun yerde belirir),
/// ama bosta GIZLENMEZ — bir "ev" konumunda gorunur durur ki oyuncu ilk elini attiginda joystigi gorsun.
/// Sol alana basinca parmagin altina tasinir; birakinca eve doner (yine gorunur). Dokunma alani kok
/// RectTransform'un kapladigi bolgedir (sol yari), yani serbest-alan davranisi surer.
/// </summary>
public class FloatingHomeJoystick : Joystick
{
    private Vector2 _home; // background'un sahnedeki baslangic (ev) konumu

    protected override void Start()
    {
        base.Start();
        _home = background.anchoredPosition;      // sahnede ayarlanan konum = ev
        background.gameObject.SetActive(true);     // HER ZAMAN gorunur (floating gizleme yok)
    }

    public override void OnPointerDown(PointerEventData eventData)
    {
        background.anchoredPosition = ScreenPointToAnchoredPosition(eventData.position); // parmagin altina
        base.OnPointerDown(eventData);
    }

    public override void OnPointerUp(PointerEventData eventData)
    {
        base.OnPointerUp(eventData);               // handle merkeze, input sifir
        background.anchoredPosition = _home;         // eve don — gorunur kalir
    }
}

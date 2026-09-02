using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Bombardiman ucagi = haritanin ustunde ucan ELIPTIK GOLGE. Verilen yol boyunca (duz cizgi her yonde
/// ya da oyuncu uzerinden gecen YARIM DAIRE yay/swoop) ilerler ve altina araliklarla 2 bomba YAN YANA
/// birakir (ucus yonune dik). Golge sadece gorseldir; hasari birakilan bombalar verir. Havuzludur —
/// BombardmentDirector yonetir. Coroutine icinde SetActive(false) oncesi referans null'lanir.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class BombPlane : MonoBehaviour
{
    #region Private Fields
    private SpriteRenderer _sr;
    private Coroutine _routine;
    #endregion

    #region Unity Callbacks
    private void Awake() => _sr = GetComponent<SpriteRenderer>();

    private void OnDisable()
    {
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
    }
    #endregion

    #region Public Methods
    /// <summary>Golge gorselini bir kez ayarlar: sprite, sorting, renk (yari saydam) ve dunya boyutu.</summary>
    public void Configure(Sprite shadow, int sortingOrder, Color color, Vector2 worldSize)
    {
        if (_sr == null) _sr = GetComponent<SpriteRenderer>();
        _sr.sprite = shadow;
        _sr.sortingOrder = sortingOrder;
        _sr.color = color;

        if (shadow != null)
        {
            Vector2 spriteWorld = shadow.bounds.size; // PPU dahil sprite'in dunya boyutu
            transform.localScale = new Vector3(
                spriteWorld.x > 0f ? worldSize.x / spriteWorld.x : 1f,
                spriteWorld.y > 0f ? worldSize.y / spriteWorld.y : 1f, 1f);
        }
    }

    /// <summary>DUZ CIZGI: start->end boyunca ucar, araliklarla altina 2 bomba yan yana birakir.</summary>
    public void FlyLine(Vector2 start, Vector2 end, float speed, float bombInterval, float sideGap, Action<Vector2> drop, Action onDone)
    {
        gameObject.SetActive(true);
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(LineRoutine(start, end, speed, bombInterval, sideGap, drop, onDone));
    }

    /// <summary>YAY (swoop): center etrafinda startAngle'dan sweepDeg kadar donerek gecer — oyuncu uzerinden gecen yarim daire.</summary>
    public void FlyArc(Vector2 center, float radius, float startAngleDeg, float sweepDeg, float speed, float bombInterval, float sideGap, Action<Vector2> drop, Action onDone)
    {
        gameObject.SetActive(true);
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(ArcRoutine(center, radius, startAngleDeg, sweepDeg, speed, bombInterval, sideGap, drop, onDone));
    }
    #endregion

    #region Private Methods
    private IEnumerator LineRoutine(Vector2 start, Vector2 end, float speed, float bombInterval, float sideGap, Action<Vector2> drop, Action onDone)
    {
        Vector2 delta = end - start;
        float total = delta.magnitude;
        Vector2 dir = total > 0.001f ? delta / total : Vector2.right;
        Vector2 perp = new Vector2(-dir.y, dir.x); // ucus yonune dik (yan yana bombalar)

        transform.position = start;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

        float travelled = 0f, sinceBomb = 0f;
        DropPair(drop, start, perp, sideGap);
        while (travelled < total)
        {
            float step = speed * Time.deltaTime;
            transform.position += (Vector3)(dir * step);
            travelled += step;
            sinceBomb += Time.deltaTime;
            if (sinceBomb >= bombInterval) { sinceBomb -= bombInterval; DropPair(drop, transform.position, perp, sideGap); }
            yield return null;
        }
        Finish(onDone);
    }

    private IEnumerator ArcRoutine(Vector2 center, float radius, float startAngleDeg, float sweepDeg, float speed, float bombInterval, float sideGap, Action<Vector2> drop, Action onDone)
    {
        float angSpeed = radius > 0.01f ? (speed / radius) * Mathf.Rad2Deg : 90f; // sabit dogrusal hiz
        float sign = Mathf.Sign(sweepDeg);
        float ang = startAngleDeg;
        float swept = 0f, target = Mathf.Abs(sweepDeg), sinceBomb = 0f;

        while (swept < target)
        {
            float rad = ang * Mathf.Deg2Rad;
            Vector2 radial = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)); // merkezden disari = ucus yonune dik
            Vector2 pos = center + radial * radius;
            transform.position = pos;
            // Teget yonune (ucus yonu) bak
            Vector2 tangent = new Vector2(-radial.y, radial.x) * sign;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg);

            sinceBomb += Time.deltaTime;
            if (sinceBomb >= bombInterval) { sinceBomb -= bombInterval; DropPair(drop, pos, radial, sideGap); }

            float d = angSpeed * Time.deltaTime;
            ang += d * sign; swept += d;
            yield return null;
        }
        Finish(onDone);
    }

    /// <summary>Uçus yonune DIK iki bomba yan yana birakir (sideGap<=0 ise tek bomba).</summary>
    private static void DropPair(Action<Vector2> drop, Vector2 center, Vector2 perpUnit, float sideGap)
    {
        if (drop == null) return;
        if (sideGap > 0.001f)
        {
            Vector2 o = perpUnit * (sideGap * 0.5f);
            drop(center + o);
            drop(center - o);
        }
        else drop(center);
    }

    private void Finish(Action onDone)
    {
        _routine = null;
        gameObject.SetActive(false);
        onDone?.Invoke();
    }
    #endregion
}

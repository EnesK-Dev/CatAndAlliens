using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bir GridLayoutGroup'un hucre boyutunu, mevcut genislige gore sabit SUTUN sayisi sigacak
/// sekilde otomatik ayarlar (responsive). Padding + spacing hesaba katilir; yukseklik
/// cellAspect ile oranlanir. Ekran/panel boyutu degisince (OnRectTransformDimensionsChange)
/// yeniden hesaplar. Boylece iPad gibi dar oranlarda kartlar tasmaz.
/// </summary>
[RequireComponent(typeof(GridLayoutGroup))]
[RequireComponent(typeof(RectTransform))]
public class GridColumnFitter : MonoBehaviour
{
    #region Serialized Fields
    [Tooltip("Bir satirda kac sutun olsun.")]
    [SerializeField] private int columns = 5;
    [Tooltip("Hucre yuksekligi / genisligi orani (ornek 460/305 = 1.508).")]
    [SerializeField] private float cellAspect = 1.508f;
    #endregion

    #region Private Fields
    private GridLayoutGroup _grid;
    private RectTransform _rt;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        _grid = GetComponent<GridLayoutGroup>();
        _rt = GetComponent<RectTransform>();
    }

    private void OnEnable() { Fit(); }

    private void OnRectTransformDimensionsChange() { Fit(); }
    #endregion

    #region Private Methods
    /// <summary>Genisligi olcup hucreyi sutun sayisina gore boyutlandirir.</summary>
    private void Fit()
    {
        if (_grid == null || _rt == null || columns < 1) return;
        float w = _rt.rect.width;
        if (w <= 0f) return;
        float pad = _grid.padding.left + _grid.padding.right;
        float spacing = _grid.spacing.x * (columns - 1);
        float cellW = (w - pad - spacing) / columns;
        if (cellW <= 0f) return;
        _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        _grid.constraintCount = columns;
        _grid.cellSize = new Vector2(cellW, cellW * cellAspect);
    }
    #endregion
}

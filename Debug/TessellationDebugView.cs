using Appegy.Lattice;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Root debug component: holds all settings, creates Tessellation,
/// and propagates changes to GridRenderer and CellHighlighter.
/// </summary>
[ExecuteAlways]
public class TessellationDebugView : MonoBehaviour
{
    public enum TessellationType
    {
        Square4,
        Square8,
        HexPointyOddAll,
        HexPointyOddEven,
        HexPointyOddOdd,
        HexPointyEvenAll,
        HexPointyEvenEven,
        HexPointyEvenOdd,
        HexFlatOddAll,
        HexFlatOddEven,
        HexFlatOddOdd,
        HexFlatEvenAll,
        HexFlatEvenEven,
        HexFlatEvenOdd
    }

    [Header("Tessellation")]
    [SerializeField] private TessellationType _type = TessellationType.Square4;
    [SerializeField] private float _inscribedRadius = 0.5f;

    [Header("Grid Size")]
    [SerializeField] [Range(1, 100)] private int _width = 10;
    [SerializeField] [Range(1, 100)] private int _height = 10;

    [Header("Grid Appearance")]
    [SerializeField] [Range(0.001f, 0.2f)] private float _lineWidth = 0.02f;
    [SerializeField] private Color _lineColor = Color.white;

    [Header("Highlight")]
    [SerializeField] private bool _enableHighlighter = true;
    [SerializeField] private Color _hoveredColor = new(0.91f, 0.40f, 0.35f, 0.25f);
    [SerializeField] private Color _neighborColor = new(0.91f, 0.66f, 0.24f, 0.19f);

    private TessellationGridRenderer _gridRenderer;
    private TessellationCellHighlighter _cellHighlighter;

    public Tessellation Tessellation { get; private set; }
    public int Width => _width;
    public int Height => _height;
    public float LineWidth => _lineWidth;
    public Color LineColor => _lineColor;
    public Color HoveredColor => _hoveredColor;
    public Color NeighborColor => _neighborColor;
    public Vector2 GridCenter { get; private set; }

    public TessellationType Type
    {
        get => _type;
        set
        {
            _type = value;
            Rebuild();
        }
    }

    public float InscribedRadius
    {
        get => _inscribedRadius;
        set
        {
            _inscribedRadius = value;
            Rebuild();
        }
    }

    public bool EnableHighlighter
    {
        get => _enableHighlighter;
        set => _enableHighlighter = value;
    }

    public void Configure(TessellationType type, float inscribedRadius, int width, int height)
    {
        _type = type;
        _inscribedRadius = inscribedRadius;
        _width = width;
        _height = height;
        Rebuild();
    }

    public void SetGridSize(int width, int height)
    {
        _width = width;
        _height = height;
        Rebuild();
    }

    private void OnEnable()
    {
        EnsureChildren();
        Rebuild();
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        EditorApplication.delayCall += RebuildSafe;
#endif
    }

    private void RebuildSafe()
    {
        if (this == null) return;
        Rebuild();
    }

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        EnsureChildren();

        Tessellation = CreateTessellation();

        // Calculate grid center from bounding box
        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        var corners = Tessellation.CornersCount;

        for (var y = 0; y < _height; y++)
        {
            for (var x = 0; x < _width; x++)
            {
                for (var c = 0; c < corners; c++)
                {
                    var p = Tessellation.GetCornerPoint((x, y), c);
                    var v = new Vector2(p.X, p.Y);
                    min = Vector2.Min(min, v);
                    max = Vector2.Max(max, v);
                }
            }
        }

        GridCenter = (min + max) * 0.5f;
        var gridSize = max - min;

        _gridRenderer.Rebuild(this);
        if (_enableHighlighter)
        {
            _cellHighlighter.gameObject.SetActive(true);
            _cellHighlighter.Init(this, gridSize);
        }
        else
        {
            _cellHighlighter.gameObject.SetActive(false);
        }
    }

    private void EnsureChildren()
    {
        if (_gridRenderer == null)
            _gridRenderer = GetOrAddChild<TessellationGridRenderer>("GridMesh");
        if (_cellHighlighter == null)
            _cellHighlighter = GetOrAddChild<TessellationCellHighlighter>("CellHighlight");
    }

    private T GetOrAddChild<T>(string childName) where T : MonoBehaviour
    {
        // Try to find existing child
        for (var i = 0; i < transform.childCount; i++)
        {
            var existing = transform.GetChild(i).GetComponent<T>();
            if (existing != null) return existing;
        }

        // Create new child
        var go = new GameObject(childName);
        go.transform.SetParent(transform, false);
        go.hideFlags = HideFlags.DontSave;
        return go.AddComponent<T>();
    }

    private Tessellation CreateTessellation()
    {
        return _type switch
        {
            TessellationType.Square4 => new SquareTessellation(_inscribedRadius, false),
            TessellationType.Square8 => new SquareTessellation(_inscribedRadius, true),
            TessellationType.HexPointyOddAll => new HexagonalTessellation(_inscribedRadius, HexagonalGridType.PointyOdd, HexNeighborMode.All),
            TessellationType.HexPointyOddEven => new HexagonalTessellation(_inscribedRadius, HexagonalGridType.PointyOdd, HexNeighborMode.Even),
            TessellationType.HexPointyOddOdd => new HexagonalTessellation(_inscribedRadius, HexagonalGridType.PointyOdd, HexNeighborMode.Odd),
            TessellationType.HexPointyEvenAll => new HexagonalTessellation(_inscribedRadius, HexagonalGridType.PointyEven, HexNeighborMode.All),
            TessellationType.HexPointyEvenEven => new HexagonalTessellation(_inscribedRadius, HexagonalGridType.PointyEven, HexNeighborMode.Even),
            TessellationType.HexPointyEvenOdd => new HexagonalTessellation(_inscribedRadius, HexagonalGridType.PointyEven, HexNeighborMode.Odd),
            TessellationType.HexFlatOddAll => new HexagonalTessellation(_inscribedRadius, HexagonalGridType.FlatOdd, HexNeighborMode.All),
            TessellationType.HexFlatOddEven => new HexagonalTessellation(_inscribedRadius, HexagonalGridType.FlatOdd, HexNeighborMode.Even),
            TessellationType.HexFlatOddOdd => new HexagonalTessellation(_inscribedRadius, HexagonalGridType.FlatOdd, HexNeighborMode.Odd),
            TessellationType.HexFlatEvenAll => new HexagonalTessellation(_inscribedRadius, HexagonalGridType.FlatEven, HexNeighborMode.All),
            TessellationType.HexFlatEvenEven => new HexagonalTessellation(_inscribedRadius, HexagonalGridType.FlatEven, HexNeighborMode.Even),
            TessellationType.HexFlatEvenOdd => new HexagonalTessellation(_inscribedRadius, HexagonalGridType.FlatEven, HexNeighborMode.Odd),
            _ => new SquareTessellation(_inscribedRadius, false)
        };
    }
}

using Appegy.Tessera;
using Unity.Mathematics;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
///     Root debug component: holds settings, builds an <see cref="ITessellation" />,
///     and propagates changes to GridRenderer and CellHighlighter.
/// </summary>
[ExecuteAlways]
public class GridDebugView : MonoBehaviour
{
    public enum GridKind
    {
        Square,
        HexPointyOdd,
        HexPointyEven,
        HexFlatOdd,
        HexFlatEven,
        Voronoi
    }

    [Header("Grid")]
    [SerializeField] private GridKind _kind = GridKind.Square;
    [SerializeField] private float _inscribedRadius = 0.5f;

    [Header("Grid Size")]
    [SerializeField] [Range(1, 100)] private int _width = 10;
    [SerializeField] [Range(1, 100)] private int _height = 10;

    [Header("Voronoi")]
    [SerializeField] [Range(4, 500)] private int _cellCount = 64;
    [SerializeField] private int _voronoiSeed = 0;
    [SerializeField] [Range(0, 16)] private int _relaxationIterations = 3;

    [Header("Grid Appearance")]
    [SerializeField] [Range(0.001f, 0.2f)] private float _lineWidth = 0.02f;
    [SerializeField] private Color _lineColor = Color.white;

    [Header("Highlight")]
    [SerializeField] private bool _enableHighlighter = true;
    [SerializeField] private Color _hoveredColor = new(0.91f, 0.40f, 0.35f, 0.25f);
    [SerializeField] private Color _neighborColor = new(0.91f, 0.66f, 0.24f, 0.19f);
    private CellHighlighter _cellHighlighter;

    private GridRenderer _gridRenderer;

    public ITessellation Grid { get; private set; }
    public int Width => _width;
    public int Height => _height;
    public float LineWidth => _lineWidth;
    public Color LineColor => _lineColor;
    public Color HoveredColor => _hoveredColor;
    public Color NeighborColor => _neighborColor;
    public Vector2 GridCenter { get; private set; }

    public GridKind Kind
    {
        get => _kind;
        set
        {
            _kind = value;
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

    public void Configure(GridKind kind, float inscribedRadius, int width, int height)
    {
        _kind = kind;
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

    private void RebuildSafe()
    {
        if (this == null) return;
        Rebuild();
    }

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        EnsureChildren();

        Grid = CreateGrid();

        var bounds = Grid.Bounds;
        GridCenter = new Vector2(bounds.Center.x, bounds.Center.y);
        var gridSize = new Vector2(bounds.Size.x, bounds.Size.y);

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
            _gridRenderer = GetOrAddChild<GridRenderer>("GridMesh");
        if (_cellHighlighter == null)
            _cellHighlighter = GetOrAddChild<CellHighlighter>("CellHighlight");
    }

    private T GetOrAddChild<T>(string childName) where T : MonoBehaviour
    {
        for (var i = 0; i < transform.childCount; i++)
        {
            var existing = transform.GetChild(i).GetComponent<T>();
            if (existing != null) return existing;
        }

        var go = new GameObject(childName);
        go.transform.SetParent(transform, false);
        go.hideFlags = HideFlags.DontSave;
        return go.AddComponent<T>();
    }

    private ITessellation CreateGrid()
    {
        switch (_kind)
        {
            case GridKind.Square:
                return new SquareGrid(_width, _height, _inscribedRadius * 2f);
            case GridKind.HexPointyOdd:
                return new HexagonalGrid(_width, _height, _inscribedRadius, HexagonalGridType.PointyOdd);
            case GridKind.HexPointyEven:
                return new HexagonalGrid(_width, _height, _inscribedRadius, HexagonalGridType.PointyEven);
            case GridKind.HexFlatOdd:
                return new HexagonalGrid(_width, _height, _inscribedRadius, HexagonalGridType.FlatOdd);
            case GridKind.HexFlatEven:
                return new HexagonalGrid(_width, _height, _inscribedRadius, HexagonalGridType.FlatEven);
            case GridKind.Voronoi:
            {
                var size = new float2(_width * _inscribedRadius * 2f, _height * _inscribedRadius * 2f);
                var bounds = new Bounds2(float2.zero, size);
                return new VoronoiGrid(bounds, _cellCount, _voronoiSeed, _relaxationIterations);
            }
            default:
                return new SquareGrid(_width, _height, _inscribedRadius * 2f);
        }
    }
}
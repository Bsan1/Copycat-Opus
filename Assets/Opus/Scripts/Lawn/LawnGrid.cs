using UnityEngine;

namespace Opus.Lawn
{
    /// <summary>
    /// A regular cell grid laid over the lawn (in the XZ plane). Cell (0,0) is at the smallest X/Z corner.
    /// All lawn data (height, dryness, patch id) is kept in the cells of this grid.
    /// </summary>
    public readonly struct LawnGrid
    {
        public readonly Vector2 Min;
        public readonly float CellSize;
        public readonly int Width;
        public readonly int Height;

        public LawnGrid(Vector2 min, Vector2 size, float cellSize)
        {
            Min = min;
            CellSize = cellSize;
            Width = Mathf.Max(1, Mathf.CeilToInt(size.x / cellSize - 1e-4f));
            Height = Mathf.Max(1, Mathf.CeilToInt(size.y / cellSize - 1e-4f));
        }

        public int CellCount => Width * Height;
        public Vector2 Size => new Vector2(Width * CellSize, Height * CellSize);
        public Vector2 Max => Min + Size;

        /// <summary>The rectangle sent to the shader: (minX, minZ, width, depth).</summary>
        public Vector4 ShaderRect => new Vector4(Min.x, Min.y, Size.x, Size.y);

        public int Index(int x, int z) => z * Width + x;

        public Vector2 CellCenter(int x, int z) => Min + new Vector2((x + 0.5f) * CellSize, (z + 0.5f) * CellSize);

        public Vector2 CellCenter(int index) => CellCenter(index % Width, index / Width);

        public bool Contains(Vector2 world) => TryGetCell(world, out _, out _);

        public bool TryGetCell(Vector2 world, out int x, out int z)
        {
            x = Mathf.FloorToInt((world.x - Min.x) / CellSize);
            z = Mathf.FloorToInt((world.y - Min.y) / CellSize);
            return x >= 0 && z >= 0 && x < Width && z < Height;
        }

        /// <summary>The range of cells intersecting the given world rectangle (clipped to the bounds, inclusive).</summary>
        public bool TryGetCellRange(Vector2 worldMin, Vector2 worldMax, out Vector2Int from, out Vector2Int to)
        {
            from = new Vector2Int(Mathf.FloorToInt((worldMin.x - Min.x) / CellSize), Mathf.FloorToInt((worldMin.y - Min.y) / CellSize));
            to = new Vector2Int(Mathf.FloorToInt((worldMax.x - Min.x) / CellSize), Mathf.FloorToInt((worldMax.y - Min.y) / CellSize));
            from = Vector2Int.Max(from, Vector2Int.zero);
            to = Vector2Int.Min(to, new Vector2Int(Width - 1, Height - 1));
            return from.x <= to.x && from.y <= to.y;
        }
    }
}

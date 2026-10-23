using UnityEngine;

namespace Opus.Lawn
{
    /// <summary>
    /// An area of the garden separated by fences (XZ rectangle). Patches only take cells of their own area;
    /// the first area is unlocked from the start, the next ones open through a gate when the previous one is finished.
    /// </summary>
    public readonly struct LawnArea
    {
        public readonly int Id;
        public readonly string Name;
        public readonly Rect Bounds;

        public LawnArea(int id, string name, float minX, float minZ, float maxX, float maxZ)
        {
            Id = id;
            Name = name;
            Bounds = Rect.MinMaxRect(minX, minZ, maxX, maxZ);
        }

        /// <summary>Bottom/left edge included, top/right edge excluded (neighbouring areas must not overlap).</summary>
        public bool Contains(Vector2 point) =>
            point.x >= Bounds.xMin && point.x < Bounds.xMax && point.y >= Bounds.yMin && point.y < Bounds.yMax;

        public float AreaSquareMeters => Bounds.width * Bounds.height;
    }
}

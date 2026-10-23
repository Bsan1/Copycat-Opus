using UnityEngine;

namespace Opus.World
{
    /// <summary>Material kind of a surface: which painted texture, at what scale, how much grime.</summary>
    public enum SurfaceKind
    {
        /// <summary>General painted surface (brush marks, light speckle). Everything given a flat colour gets this.</summary>
        Paint,
        /// <summary>Horizontal lap siding (house and shed walls).</summary>
        Siding,
        /// <summary>Vertical boards with joints between them (doors, veranda, compost).</summary>
        Planks,
        /// <summary>Vinyl fence: very slightly wavy, flat.</summary>
        Vinyl,
        /// <summary>Shingle rows: the top projection follows the slope, the rows run parallel to the ridge.</summary>
        Shingles,
        Brick,
        Concrete,
        Stone,
        Bark,
        Soil,
        Rubber,
        Metal,
        Plastic,
        Foliage,
        Fabric,
        Glass,
        /// <summary>Large ground (outer soil): no grime or contact shadow, large-scale speckle.</summary>
        Ground,
        /// <summary>Small items on the ground: stay clean and bright (no grime or contact shadow) so they stand out.</summary>
        Prop,
        /// <summary>Plants and weeds: no grime, soft light, a light contact shadow.</summary>
        Plant,
    }

    /// <summary>
    /// Colour + material kind (+ pattern scale). A plain <see cref="Color"/> automatically becomes <see cref="SurfaceKind.Paint"/>.
    /// </summary>
    public readonly struct Surface : System.IEquatable<Surface>
    {
        public readonly SurfaceKind Kind;
        public readonly Color Color;
        readonly float scale;

        public Surface(SurfaceKind kind, Color color, float scale = 1f)
        {
            Kind = kind;
            Color = color;
            this.scale = scale;
        }

        /// <summary>Pattern scale multiplier (1 = the kind's default; 0.5 = boards twice as dense).</summary>
        public float Scale => scale > 0f ? scale : 1f;

        public static implicit operator Surface(Color color) => new Surface(SurfaceKind.Paint, color);

        public bool Equals(Surface other) => Kind == other.Kind && Color == other.Color && Mathf.Approximately(Scale, other.Scale);
        public override bool Equals(object obj) => obj is Surface other && Equals(other);
        public override int GetHashCode() => ((int)Kind * 397) ^ Color.GetHashCode() ^ Scale.GetHashCode();

        public override string ToString() => Mathf.Approximately(Scale, 1f)
            ? $"{Kind}_{ColorUtility.ToHtmlStringRGB(Color)}"
            : $"{Kind}_{ColorUtility.ToHtmlStringRGB(Color)}_x{Scale:0.##}";
    }

    public static class SurfaceExtensions
    {
        /// <summary>Pairs a colour with a material kind: <c>Palette.ShedBlue.As(SurfaceKind.Siding)</c>.</summary>
        public static Surface As(this Color color, SurfaceKind kind, float scale = 1f) => new Surface(kind, color, scale);
    }
}

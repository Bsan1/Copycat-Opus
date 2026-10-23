using UnityEngine;

namespace Opus.Core
{
    /// <summary>
    /// Starting colours measured from the reference video (PLAN.md §1.5). The values are sRGB;
    /// Unity converts them itself when it hands them to a material in the Linear colour space.
    /// </summary>
    public static class Palette
    {
        // Grass
        public static readonly Color TallGrass = Hex("#A89E31");
        public static readonly Color TallGrassTip = Hex("#B9B161");
        public static readonly Color CutGrass = Hex("#ABBF4F");
        public static readonly Color DryGrass = Hex("#AC9558");
        public static readonly Color DryCut = Hex("#AC8B66");
        public static readonly Color WateredGrass = Hex("#95B24B");
        public static readonly Color CoolGrass = Hex("#8A9087");

        // Atmosphere
        public static readonly Color SkyZenith = Hex("#9586BA");
        public static readonly Color SkyHorizon = Hex("#C9B7C1");
        public static readonly Color SkyGround = Hex("#C8BABE");
        public static readonly Color Fog = Hex("#D6CAD2");
        public static readonly Color CloudLight = Hex("#EADDE2");
        public static readonly Color CloudShadow = Hex("#AA97BF");
        public static readonly Color HorizonHaze = Hex("#D1C2CB");
        // Post-process split toning: lavender shadows, warm highlights
        public static readonly Color GradeShadows = Hex("#7E70AA");
        public static readonly Color GradeHighlights = Hex("#F2D9B8");
        public static readonly Color Sun = Hex("#FFF0D8");
        // Ambient and shadow tones (converted correctly by ShaderColors; values are the look matched with the reference in U1–U5)
        public static readonly Color AmbientSky = Hex("#E6E1F0");
        public static readonly Color AmbientGround = Hex("#C5CCA7");
        public static readonly Color ShadowTint = Hex("#D2CDE3");

        // Garden props
        public static readonly Color FenceWhite = Hex("#E7E3DD");
        public static readonly Color HouseGray = Hex("#A8ADB6");
        public static readonly Color RoofDark = Hex("#5E6070");
        public static readonly Color ShedBlue = Hex("#8094AA");
        public static readonly Color TrimWhite = Hex("#F4F1EC");
        public static readonly Color DeckWood = Hex("#C49A7A");
        public static readonly Color Soil = Hex("#5A4636");
        public static readonly Color FlowerPurple = Hex("#7A4FB0");
        public static readonly Color Hosta = Hex("#9CC25A");
        public static readonly Color TrunkBrown = Hex("#6E4B3A");
        public static readonly Color CanopyGreen = Hex("#8FB04A");
        public static readonly Color PineGreen = Hex("#4E7A4A");
        public static readonly Color OuterGround = Hex("#B4B08A");

        // Interface
        public static readonly Color Highlight = Hex("#81CAC8");
        public static readonly Color PatchOutline = Hex("#E0753A");
        public static readonly Color Paper = Hex("#F3ECDF");
        public static readonly Color Ink = Hex("#4B4741");
        public static readonly Color InkSoft = Hex("#8C857B");
        public static readonly Color Tape = Hex("#A9C2AE");

        public static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out Color color) ? color : Color.magenta;
        }
    }
}

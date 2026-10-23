using Opus.Core;
using UnityEngine;

namespace Opus.Lawn
{
    /// <summary>
    /// The colours (sRGB) of a grass variety: base, middle, tip and mown; and a height multiplier.
    /// The "quilt" look in the reference comes from neighbouring patches being different varieties.
    /// </summary>
    public readonly struct GrassVariety
    {
        public readonly string Name;
        public readonly Color Base;
        public readonly Color Mid;
        public readonly Color Tip;
        public readonly Color Cut;
        public readonly float HeightScale;

        public GrassVariety(string name, string baseHex, string midHex, string tipHex, string cutHex, float heightScale)
        {
            Name = name;
            Base = Palette.Hex(baseHex);
            Mid = Palette.Hex(midHex);
            Tip = Palette.Hex(tipHex);
            Cut = Palette.Hex(cutHex);
            HeightScale = heightScale;
        }

        // Tones measured from the reference frames (PLAN.md §1.5); fine-tuned by screen comparison.
        public static readonly GrassVariety Mustard = new GrassVariety("Mustard", "#6A6620", "#A09C30", "#B3B84A", "#B9C452", 1.25f);
        public static readonly GrassVariety Golden = new GrassVariety("Golden", "#606427", "#9EA041", "#B6BA60", "#B6C157", 1.0f);
        public static readonly GrassVariety Olive = new GrassVariety("Olive", "#4E5C22", "#8E9C3A", "#C2C97C", "#A9C04E", 0.9f);
        public static readonly GrassVariety Cool = new GrassVariety("Gray-blue", "#4A5C44", "#839C68", "#A2B57F", "#A6B86A", 1.15f);
        public static readonly GrassVariety Fresh = new GrassVariety("Fresh", "#56772A", "#9DBA45", "#CEDE82", "#B4CC5C", 0.8f);
        public static readonly GrassVariety Straw = new GrassVariety("Straw", "#78683E", "#AC9558", "#D3C08C", "#B8A16E", 1.05f);

        public static readonly GrassVariety[] All = { Mustard, Golden, Olive, Cool, Fresh, Straw };

        /// <summary>Fresh grass colours a watered dry spot greens up to (in the reference it turns a vivid green when watered).</summary>
        public static readonly Color FreshBase = Palette.Hex("#4C7826");
        public static readonly Color FreshMid = Palette.Hex("#86B03E");
        public static readonly Color FreshTip = Palette.Hex("#C2DC6A");

        /// <summary>Colours every variety tends towards at dryness 1 (watering with the hose returns it to its own colour).</summary>
        public static readonly Color DryBase = Palette.Hex("#6E5D37");
        public static readonly Color DryMid = Palette.Hex("#9C824A");
        public static readonly Color DryTip = Palette.Hex("#B89F63");
    }
}

using UnityEngine;

namespace Opus.Lawn
{
    /// <summary>A patch's name and pill colour as shown in the interface.</summary>
    public readonly struct PatchInfo
    {
        public readonly string Name;
        public readonly Color Color;

        public PatchInfo(string name, string colorHex)
        {
            Name = name;
            Color = Core.Palette.Hex(colorHex);
        }
    }
}

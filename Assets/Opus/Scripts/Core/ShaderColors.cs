using UnityEngine;

namespace Opus.Core
{
    /// <summary>
    /// Global shader colours. Unlike material colours, <see cref="Shader.SetGlobalColor"/> does not convert sRGB → linear;
    /// in the Linear colour space the hex values come out light and washed out. All global colours are set
    /// from here, so the hex values in Palette are what ends up on screen.
    /// </summary>
    public static class ShaderColors
    {
        public static void SetGlobal(int id, Color srgb)
        {
            Shader.SetGlobalColor(id, ToShader(srgb));
        }

        /// <summary>The value sent to the shader by colour space (linear in a Linear project).</summary>
        public static Color ToShader(Color srgb)
        {
            return QualitySettings.activeColorSpace == ColorSpace.Linear ? srgb.linear : srgb;
        }
    }
}

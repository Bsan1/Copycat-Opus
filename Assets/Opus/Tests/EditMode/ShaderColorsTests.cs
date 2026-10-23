using NUnit.Framework;
using Opus.Core;
using UnityEngine;

namespace Opus.Tests
{
    public class ShaderColorsTests
    {
        [Test]
        public void GlobalColorsAreLinearizedInLinearProjects()
        {
            Assert.AreEqual(ColorSpace.Linear, QualitySettings.activeColorSpace);
            Color orange = Palette.PatchOutline;
            Color sent = ShaderColors.ToShader(orange);
            Assert.AreEqual(orange.linear.r, sent.r, 1e-5f);
            Assert.Less(sent.g, orange.g, "The linear value must be smaller than the sRGB value");
        }
    }
}

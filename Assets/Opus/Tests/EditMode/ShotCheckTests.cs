using NUnit.Framework;
using Opus.Core;
using UnityEngine;

namespace Opus.Tests
{
    public class ShotCheckTests
    {
        [Test]
        public void UniformImageIsBlank()
        {
            var pixels = new Color32[10000];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(120, 130, 140, 255);
            Assert.IsTrue(ShotCheck.LooksBlank(pixels));
        }

        [Test]
        public void VariedImageIsNotBlank()
        {
            var pixels = new Color32[10000];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = i % 2 == 0 ? new Color32(0, 0, 0, 255) : new Color32(255, 255, 255, 255);
            Assert.IsFalse(ShotCheck.LooksBlank(pixels));
        }

        [Test]
        public void EmptyImageIsBlank()
        {
            Assert.IsTrue(ShotCheck.LooksBlank(new Color32[0]));
            Assert.IsTrue(ShotCheck.LooksBlank(null));
        }
    }
}

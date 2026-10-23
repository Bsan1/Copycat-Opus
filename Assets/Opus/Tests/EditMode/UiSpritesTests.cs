using NUnit.Framework;
using Opus.UI;
using UnityEngine;

namespace Opus.Tests
{
    public class UiSpritesTests
    {
        [Test]
        public void SmoothStepIsClampedBetweenZeroAndOne()
        {
            Assert.AreEqual(0f, UiSprites.SmoothStep(-1f, 1f, -5f));
            Assert.AreEqual(1f, UiSprites.SmoothStep(-1f, 1f, 5f));
            Assert.AreEqual(0.5f, UiSprites.SmoothStep(-1f, 1f, 0f), 1e-5f);
        }

        [Test]
        public void CardSpriteIsOpaqueInsideAndClearOutside()
        {
            Texture2D texture = UiSprites.RoundedRect(14).texture;
            int size = texture.width;
            Color32 center = texture.GetPixel(size / 2, size / 2);
            Color32 corner = texture.GetPixel(0, 0);
            Assert.AreEqual(255, center.a, "The middle of the card must be fully opaque.");
            Assert.AreEqual(0, corner.a, "Outside the corner must be fully transparent.");
        }
    }
}

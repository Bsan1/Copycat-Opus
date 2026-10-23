using NUnit.Framework;
using Opus.World;
using UnityEngine;

namespace Opus.Tests
{
    public class VisibilityTests
    {
        const float Line = 18f, ZMin = -1.15f, ZMax = 1.15f;

        static Bounds Rect(float x0, float z0, float x1, float z1) =>
            new Bounds(new Vector3((x0 + x1) * 0.5f, 0.4f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 1f, z1 - z0));

        [Test]
        public void RectStraightBehindTheOpeningIsVisible()
        {
            Assert.IsTrue(YardVisibility.SeesThroughOpening(new Vector2(10f, 0f), Rect(22f, -2f, 26f, 2f), Line, ZMin, ZMax));
        }

        [Test]
        public void RectBehindSolidFenceIsHidden()
        {
            // Seen from the eye (10, 0) the (22..26, 8..12) rectangle crosses the fence line between z ≈ 5..10: outside the opening
            Assert.IsFalse(YardVisibility.SeesThroughOpening(new Vector2(10f, 0f), Rect(22f, 8f, 26f, 12f), Line, ZMin, ZMax));
        }

        [Test]
        public void ObliqueViewThroughOpeningIsVisible()
        {
            // The eye is south and close to the fence: a diagonal look north-east through the opening
            Assert.IsTrue(YardVisibility.SeesThroughOpening(new Vector2(15f, -4f), Rect(22f, 6f, 26f, 10f), Line, ZMin, ZMax));
        }

        [Test]
        public void WorksFromTheOtherSideToo()
        {
            Assert.IsTrue(YardVisibility.SeesThroughOpening(new Vector2(27f, 0f), Rect(6f, -2f, 10f, 2f), Line, ZMin, ZMax));
            Assert.IsFalse(YardVisibility.SeesThroughOpening(new Vector2(27f, 0f), Rect(6f, -14f, 10f, -10f), Line, ZMin, ZMax));
        }

        [Test]
        public void EyeInTheOpeningSeesEverything()
        {
            Assert.IsTrue(YardVisibility.SeesThroughOpening(new Vector2(17.8f, 0.2f), Rect(22f, 10f, 26f, 14f), Line, ZMin, ZMax));
        }

        [Test]
        public void RectStraddlingTheLineUsesOnlyItsFarPart()
        {
            // A chunk slightly overlapping the bounds (17.5..22.5): only the part on the other side of the line is projected
            Assert.IsFalse(YardVisibility.SeesThroughOpening(new Vector2(10f, 0f), Rect(17.5f, 6f, 22.5f, 10f), Line, ZMin, ZMax));
        }
    }
}

using NUnit.Framework;
using Opus.Lawn;
using Opus.Player;
using Opus.Tools;
using UnityEngine;

namespace Opus.Tests
{
    public class TrimmerTests
    {
        static readonly Vector3 Feet = new Vector3(2f, 0f, 3f);
        static readonly Vector3 Eye = Feet + Vector3.up * PlayerController.EyeHeight;

        static float Reach(Vector3 forward)
        {
            Vector3 aim = ToolAim.GroundPoint(Eye, forward.normalized, Feet, TrimmerTool.MinReach, TrimmerTool.MaxReach);
            Assert.AreEqual(0f, aim.y, 1e-5f, "The head touches the ground.");
            return Vector2.Distance(new Vector2(aim.x, aim.z), new Vector2(Feet.x, Feet.z));
        }

        [Test]
        public void HeadLandsWhereTheGazeMeetsTheGround()
        {
            // 50 degrees down: the gaze meets the ground at eye height / tan(50) = 1.36 m, inside the reach (MaxReach 1.4 m)
            float down = 50f * Mathf.Deg2Rad;
            float expected = PlayerController.EyeHeight / Mathf.Tan(down);
            Assert.Less(expected, TrimmerTool.MaxReach);
            Assert.AreEqual(expected, Reach(new Vector3(0f, -Mathf.Sin(down), Mathf.Cos(down))), 1e-3f);
        }

        [Test]
        public void ReachIsClampedNearAndFar()
        {
            Assert.AreEqual(TrimmerTool.MinReach, Reach(new Vector3(0f, -1f, 0.01f)), 1e-3f, "Looking down at the base gives the nearest");
            Assert.AreEqual(TrimmerTool.MaxReach, Reach(new Vector3(0f, -0.1f, 1f)), 1e-3f, "Looking far gives the farthest");
            Assert.AreEqual(TrimmerTool.MaxReach, Reach(new Vector3(0f, 0.3f, 1f)), 1e-3f, "Looking up gives the farthest");
        }

        [Test]
        public void AimFollowsTheViewDirection()
        {
            Vector3 aim = ToolAim.GroundPoint(Eye, new Vector3(1f, -1f, 0f).normalized, Feet, TrimmerTool.MinReach, TrimmerTool.MaxReach);
            Assert.Greater(aim.x, Feet.x + 1f);
            Assert.AreEqual(Feet.z, aim.z, 1e-3f);
        }

        [Test]
        public void TrimmingIsBetweenMowingAndWalking()
        {
            Assert.Greater(MovementProfile.Trimming.ForwardSpeed, MovementProfile.Mowing.ForwardSpeed);
            Assert.Less(MovementProfile.Trimming.ForwardSpeed, MovementProfile.Walking.ForwardSpeed);
        }

        [Test]
        public void WeedIsRemovedOnlyWhenTouchedLongEnough()
        {
            WeedState weed = WeedState.From(new WeedSpot(0f, 0f), 1);
            Assert.IsFalse(weed.ApplyDamage(new Vector3(2f, 0f, 0f), 0.2f, 1f), "A distant trimmer does not touch");
            Assert.AreEqual(1f, weed.Fraction);

            Assert.IsFalse(weed.ApplyDamage(new Vector3(0.3f, 0f, 0f), 0.2f, WeedState.WeedHealth * 0.5f));
            Assert.AreEqual(0.5f, weed.Fraction, 1e-4f);
            Assert.IsTrue(weed.ApplyDamage(new Vector3(0.3f, 0f, 0f), 0.2f, WeedState.WeedHealth));
            Assert.IsTrue(weed.Removed);
            Assert.IsFalse(weed.ApplyDamage(Vector3.zero, 0.2f, 1f), "A pulled weed is not counted twice");
        }

        [Test]
        public void OvergrowthTakesLongerAndUsesItsEllipse()
        {
            WeedState vine = WeedState.From(new WeedSpot(0f, 0f, 0.8f, 0.2f), 1);
            Assert.Greater(WeedState.OvergrowthHealth, WeedState.WeedHealth * 2f);
            Assert.IsTrue(vine.Touches(new Vector3(0.9f, 0f, 0f), 0.2f), "Touches from far along its long axis");
            Assert.IsFalse(vine.Touches(new Vector3(0f, 0f, 0.5f), 0.2f), "The same distance along its short axis does not touch");
            Assert.IsFalse(vine.ApplyDamage(Vector3.zero, 0.2f, WeedState.WeedHealth));
            Assert.IsFalse(vine.Removed);
        }
    }
}

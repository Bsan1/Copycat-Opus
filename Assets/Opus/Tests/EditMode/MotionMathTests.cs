using NUnit.Framework;
using Opus.Player;
using UnityEngine;

namespace Opus.Tests
{
    public class MotionMathTests
    {
        const float Dt = 1f / 120f;

        static Vector2 Simulate(Vector2 start, Vector2 move, MovementProfile profile, float seconds, out float distance)
        {
            Vector2 velocity = start;
            Vector2 desired = MotionMath.DesiredLocalVelocity(move, profile);
            distance = 0f;
            for (float t = 0f; t < seconds - 1e-5f; t += Dt)
            {
                velocity = MotionMath.Approach(velocity, desired, profile.Acceleration, profile.Deceleration, Dt);
                distance += velocity.magnitude * Dt;
            }
            return velocity;
        }

        [Test]
        public void MowerIsSlowerAndHeavierThanWalking()
        {
            MovementProfile mowing = MovementProfile.Mowing, walking = MovementProfile.Walking;
            Assert.Less(mowing.ForwardSpeed, walking.ForwardSpeed);
            Assert.Less(mowing.Acceleration, walking.Acceleration);
            Assert.Greater(mowing.TurnSmoothTime, 0f);
            Assert.Greater(mowing.MinPitch, 0f, "With the mower the camera must always look down.");
            Assert.IsTrue(mowing.EquipPitch >= mowing.MinPitch && mowing.EquipPitch <= mowing.MaxPitch);
        }

        [Test]
        public void MowerReachesTopSpeedInAboutThreeQuartersOfASecond()
        {
            MovementProfile mowing = MovementProfile.Mowing;
            Vector2 half = Simulate(Vector2.zero, Vector2.up, mowing, 0.5f, out _);
            Assert.AreEqual(mowing.Acceleration * 0.5f, half.y, 0.05f);

            Vector2 full = Simulate(Vector2.zero, Vector2.up, mowing, 1f, out _);
            Assert.AreEqual(mowing.ForwardSpeed, full.y, 1e-4f);
        }

        [Test]
        public void ReleasingInputCoastsForStoppingDistance()
        {
            MovementProfile mowing = MovementProfile.Mowing;
            Vector2 stopped = Simulate(new Vector2(0f, mowing.ForwardSpeed), Vector2.zero, mowing, 2f, out float coast);
            Assert.AreEqual(Vector2.zero, stopped);
            Assert.AreEqual(MotionMath.StoppingDistance(mowing.ForwardSpeed, mowing.Deceleration), coast, 0.03f);
        }

        [Test]
        public void ReversingUsesBrakeRate()
        {
            MovementProfile mowing = MovementProfile.Mowing;
            Vector2 next = MotionMath.Approach(new Vector2(0f, 2f), new Vector2(0f, -1f), mowing.Acceleration, mowing.Deceleration, 0.1f);
            Assert.AreEqual(2f - mowing.Deceleration * 0.1f, next.y, 1e-4f);
        }

        [Test]
        public void StrafeAndBackwardAreSlowerWithMower()
        {
            MovementProfile mowing = MovementProfile.Mowing;
            Assert.AreEqual(mowing.StrafeSpeed, MotionMath.DesiredLocalVelocity(Vector2.right, mowing).x, 1e-5f);
            Assert.AreEqual(-mowing.BackwardSpeed, MotionMath.DesiredLocalVelocity(Vector2.down, mowing).y, 1e-5f);
            Assert.LessOrEqual(MotionMath.DesiredLocalVelocity(new Vector2(1f, 1f), mowing).magnitude, mowing.ForwardSpeed + 1e-4f);
        }

        [Test]
        public void SmoothAngleLimitsLagAndNeverTurnsTheWrongWay()
        {
            float current = 0f, target = 400f, velocity = 0f;
            float next = MotionMath.SmoothAngle(current, ref target, ref velocity, 0.09f, 150f, 120f, Dt);
            Assert.AreEqual(120f, target, 1e-4f, "The target must stay at most 120° ahead.");
            Assert.Greater(next, current, "A clockwise target must be turned to clockwise.");
            Assert.LessOrEqual(next - current, 150f * Dt + 1e-3f, "The maximum turning speed must not be exceeded.");
        }

        [Test]
        public void SmoothAngleWithoutSmoothingIsInstant()
        {
            float target = 37f, velocity = 5f;
            Assert.AreEqual(37f, MotionMath.SmoothAngle(0f, ref target, ref velocity, 0f, 0f, 120f, Dt));
            Assert.AreEqual(0f, velocity);
        }
    }
}

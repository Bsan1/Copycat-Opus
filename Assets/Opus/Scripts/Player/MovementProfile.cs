using System;
using UnityEngine;

namespace Opus.Player
{
    /// <summary>The movement feel, changing with the tool in hand. Speeds in m/s, angles in degrees (pitch: positive down).</summary>
    [Serializable]
    public struct MovementProfile
    {
        public float ForwardSpeed;
        public float BackwardSpeed;
        public float StrafeSpeed;
        public float Acceleration;
        public float Deceleration;
        /// <summary>Delay of the gaze following the mouse (s); 0 = instant.</summary>
        public float TurnSmoothTime;
        /// <summary>Highest turning speed (degrees/s); 0 = unlimited.</summary>
        public float MaxTurnSpeed;
        public float MinPitch;
        public float MaxPitch;
        /// <summary>The pitch the camera heads to when the tool is taken in hand; NaN = unchanged.</summary>
        public float EquipPitch;
        public float BobAmplitude;
        /// <summary>Step frequency at full speed (Hz).</summary>
        public float BobFrequency;
        /// <summary>Vertical field of view (degrees); transitions smoothly when the tool changes.</summary>
        public float FieldOfView;

        public static MovementProfile Walking => new MovementProfile
        {
            ForwardSpeed = 3.4f,
            BackwardSpeed = 2.6f,
            StrafeSpeed = 3.0f,
            Acceleration = 14f,
            Deceleration = 18f,
            TurnSmoothTime = 0f,
            MaxTurnSpeed = 0f,
            MinPitch = -80f,
            MaxPitch = 85f,
            EquipPitch = float.NaN,
            BobAmplitude = 0.028f,
            BobFrequency = 1.9f,
            FieldOfView = 60f,
        };

        /// <summary>
        /// Pushing the mower: slow, heavy start, short coasting, delayed turning, a camera looking down and a little
        /// narrower. The field of view and pitch were chosen by side-by-side comparison with the reference frames (scenario u1_framing).
        /// </summary>
        public static MovementProfile Mowing => new MovementProfile
        {
            ForwardSpeed = 2.1f,
            BackwardSpeed = 1.2f,
            StrafeSpeed = 0.9f,
            Acceleration = 2.8f,
            Deceleration = 4.5f,
            TurnSmoothTime = 0.09f,
            MaxTurnSpeed = 150f,
            MinPitch = 15f,
            MaxPitch = 60f,
            EquipPitch = 40f,
            BobAmplitude = 0.008f,
            BobFrequency = 1.6f,
            FieldOfView = 52f,
        };

        /// <summary>Hose: easy walking, a camera looking slightly down; the weight of the hose slows down a little.</summary>
        public static MovementProfile Watering => new MovementProfile
        {
            ForwardSpeed = 2.8f,
            BackwardSpeed = 2.2f,
            StrafeSpeed = 2.4f,
            Acceleration = 10f,
            Deceleration = 13f,
            TurnSmoothTime = 0.03f,
            MaxTurnSpeed = 0f,
            MinPitch = -20f,
            MaxPitch = 70f,
            EquipPitch = 26f,
            BobAmplitude = 0.02f,
            BobFrequency = 1.9f,
            FieldOfView = 58f,
        };

        /// <summary>Trimmer: a little slower than walking, a slightly delayed gaze, a camera looking down to see the head.</summary>
        public static MovementProfile Trimming => new MovementProfile
        {
            ForwardSpeed = 2.5f,
            BackwardSpeed = 1.9f,
            StrafeSpeed = 2.1f,
            Acceleration = 9f,
            Deceleration = 12f,
            TurnSmoothTime = 0.04f,
            MaxTurnSpeed = 0f,
            MinPitch = 8f,
            MaxPitch = 65f,
            EquipPitch = 45f,          // the pitch of the fitted trimmer framing (u16_trimmer_tuning, video 0:07)
            BobAmplitude = 0.016f,
            BobFrequency = 1.8f,
            FieldOfView = 56f,
        };
    }

    /// <summary>The pure maths of movement (needs no Unity objects; the EditMode tests check this).</summary>
    public static class MotionMath
    {
        /// <summary>Desired local velocity from the input (X: sideways, Y: forward).</summary>
        public static Vector2 DesiredLocalVelocity(Vector2 move, in MovementProfile profile)
        {
            move = Vector2.ClampMagnitude(move, 1f);
            float forward = move.y >= 0f ? move.y * profile.ForwardSpeed : move.y * profile.BackwardSpeed;
            return new Vector2(move.x * profile.StrafeSpeed, forward);
        }

        /// <summary>Moves the velocity towards the target: with acceleration when speeding up, with the brake value when slowing/turning.</summary>
        public static Vector2 Approach(Vector2 current, Vector2 desired, float acceleration, float deceleration, float deltaTime)
        {
            bool speedingUp = desired.sqrMagnitude > current.sqrMagnitude && Vector2.Dot(desired, current) >= 0f;
            float maxStep = (speedingUp ? acceleration : deceleration) * deltaTime;
            Vector2 delta = desired - current;
            return delta.magnitude <= maxStep ? desired : current + delta.normalized * maxStep;
        }

        /// <summary>
        /// Moves the angle smoothly towards the target. Angles are kept unwrapped (can exceed 360); the target can stay at most
        /// <paramref name="maxLag"/> degrees ahead, so a fast mouse move does not turn the other way.
        /// </summary>
        public static float SmoothAngle(float current, ref float target, ref float velocity, float smoothTime, float maxSpeed, float maxLag, float deltaTime)
        {
            if (smoothTime <= 0f)
            {
                velocity = 0f;
                return target;
            }
            target = current + Mathf.Clamp(target - current, -maxLag, maxLag);
            return Mathf.SmoothDamp(current, target, ref velocity, smoothTime, maxSpeed > 0f ? maxSpeed : Mathf.Infinity, deltaTime);
        }

        /// <summary>Stopping distance: v² / 2a.</summary>
        public static float StoppingDistance(float speed, float deceleration) => speed * speed / (2f * deceleration);
    }
}

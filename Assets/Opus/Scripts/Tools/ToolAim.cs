using UnityEngine;

namespace Opus.Tools
{
    /// <summary>Shared calculation of the tools that aim at the ground (trimmer, hose).</summary>
    public static class ToolAim
    {
        /// <summary>
        /// Where the gaze ray meets the ground (y = 0); the horizontal distance from the feet is clamped to [min, max].
        /// Looking at the horizon or up goes to the farthest point.
        /// </summary>
        public static Vector3 GroundPoint(Vector3 eye, Vector3 forward, Vector3 feet, float minReach, float maxReach)
        {
            var flatForward = new Vector3(forward.x, 0f, forward.z);
            flatForward = flatForward.sqrMagnitude > 1e-6f ? flatForward.normalized : Vector3.forward;

            Vector3 hit = forward.y < -1e-3f
                ? eye + forward * (-eye.y / forward.y)
                : feet + flatForward * maxReach;
            Vector3 flat = hit - feet;
            flat.y = 0f;
            float distance = flat.magnitude;
            flat = distance > 1e-4f ? flat / distance : flatForward;
            Vector3 point = feet + flat * Mathf.Clamp(distance, minReach, maxReach);
            point.y = 0f;
            return point;
        }
    }
}

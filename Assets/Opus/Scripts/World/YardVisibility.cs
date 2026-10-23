using System.Collections.Generic;
using Opus.Lawn;
using UnityEngine;

namespace Opus.World
{
    /// <summary>
    /// Occlusion between areas: while the eye is below the fence top (1.9 m), the ground content (grass, flowers) of another area can only
    /// be seen through gate openings. Every area counts as enclosed by a fence (or the house), so the line of sight must pass through
    /// one of the open gates of both the eye's area and the target's area. Whether a rectangle falls into the "shadow" of a gate
    /// opening as seen from the eye is found in 2D by projecting its corners onto the fence line. These two are necessary
    /// conditions: everything that passes counts as visible (the safe side).
    /// </summary>
    public static class YardVisibility
    {
        /// <summary>For measurements and comparisons with occlusion off.</summary>
        public static bool Enabled = true;
        /// <summary>Above this eye height (looking over the fence) there is no occlusion.</summary>
        public const float MaxEyeHeight = 2.05f;
        /// <summary>Margin added to the edges of the opening (bending blades, the open part of the gate leaf).</summary>
        public const float OpeningMargin = 0.35f;

        /// <summary>True if <paramref name="bounds"/> on the ground is certainly not visible from the eye.</summary>
        public static bool IsHidden(Vector3 eye, Bounds bounds)
        {
            if (!Enabled || eye.y > MaxEyeHeight)
                return false;
            LawnArea? eyeArea = YardBuilder.AreaAt(eye);
            LawnArea? boundsArea = YardBuilder.AreaAt(bounds.center);
            if (!eyeArea.HasValue || !boundsArea.HasValue || eyeArea.Value.Id == boundsArea.Value.Id)
                return false;

            IReadOnlyList<Gate> gates = YardBuilder.Gates;
            int eyeId = eyeArea.Value.Id, boundsId = boundsArea.Value.Id;
            // If one of the areas has no open gate at all, the other is fully hidden
            if (!AnyOpenGate(gates, eyeId) || !AnyOpenGate(gates, boundsId))
                return true;

            // Blades of grass next to the fence bend through the thin board to the other side: a chunk touching the eye's area is visible
            Rect eyeRect = eyeArea.Value.Bounds;
            if (bounds.min.x < eyeRect.xMax && bounds.max.x > eyeRect.xMin && bounds.min.z < eyeRect.yMax && bounds.max.z > eyeRect.yMin)
                return false;

            var eye2 = new Vector2(eye.x, eye.z);
            return !ThroughAnyGate(gates, eyeId, eye2, bounds) || !ThroughAnyGate(gates, boundsId, eye2, bounds);
        }

        static bool AnyOpenGate(IReadOnlyList<Gate> gates, int area)
        {
            for (int i = 0; i < gates.Count; i++)
                if (gates[i].Progress > 0f && gates[i].Def.Connects(area))
                    return true;
            return false;
        }

        static bool ThroughAnyGate(IReadOnlyList<Gate> gates, int area, Vector2 eye, Bounds bounds)
        {
            for (int i = 0; i < gates.Count; i++)
            {
                Gate gate = gates[i];
                if (gate.Progress > 0f && gate.Def.Connects(area) && SeesThrough(eye, bounds, gate.Def))
                    return true;
            }
            return false;
        }

        /// <summary>Can the line of sight pass through this gate's opening (with its margins)?</summary>
        public static bool SeesThrough(Vector2 eye, Bounds bounds, GateDef gate)
        {
            float min = gate.OpeningMin - OpeningMargin, max = gate.OpeningMax + OpeningMargin;
            if (gate.Axis == FenceAxis.AlongZ)
                return SeesThroughOpening(eye, bounds, gate.Line, min, max);
            // A fence along X: swap x and z and use the same calculation
            var swapped = new Bounds(new Vector3(bounds.center.z, bounds.center.y, bounds.center.x), new Vector3(bounds.size.z, bounds.size.y, bounds.size.x));
            return SeesThroughOpening(new Vector2(eye.y, eye.x), swapped, gate.Line, min, max);
        }

        /// <summary>
        /// The eye is on one side of the line x = <paramref name="lineX"/>; does the projection of the rectangle (the part on the
        /// other side of the line) onto the line overlap the [zMin, zMax] opening? If the eye is right at the line (in the opening) everything counts as visible.
        /// </summary>
        public static bool SeesThroughOpening(Vector2 eye, Bounds bounds, float lineX, float zMin, float zMax)
        {
            float side = Mathf.Sign(lineX - eye.x);
            float gap = Mathf.Abs(lineX - eye.x);
            // Inside the opening or just in front of it: the projection slides to extreme angles, count everything as visible
            if (gap < 0.6f && eye.y > zMin - 1f && eye.y < zMax + 1f)
                return true;
            if (gap < 1e-3f)
                return true;

            // The part of the rectangle on the other side of the line
            float xNear = side > 0f ? Mathf.Max(bounds.min.x, lineX) : Mathf.Min(bounds.max.x, lineX);
            float xFar = side > 0f ? bounds.max.x : bounds.min.x;
            if ((xFar - lineX) * side <= 0f)
                return false;

            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                float x = i < 2 ? xNear : xFar;
                float z = (i & 1) == 0 ? bounds.min.z : bounds.max.z;
                float atLine = eye.y + (z - eye.y) * ((lineX - eye.x) / (x - eye.x));
                lo = Mathf.Min(lo, atLine);
                hi = Mathf.Max(hi, atLine);
            }
            return hi >= zMin && lo <= zMax;
        }
    }
}

using Opus.Core;
using UnityEngine;

namespace Opus.World
{
    /// <summary>
    /// The fence gate between areas. While closed its body blocks the way; <see cref="Open"/> swings it smoothly
    /// around the hinge towards the new area (the collision box turns with the gate).
    /// </summary>
    public sealed class Gate : MonoBehaviour
    {
        /// <summary>Opening angle of the leaf (into the new area, away from the player).</summary>
        public const float OpenAngle = 100f;
        public const float OpenSeconds = 1.6f;

        Transform leaf;
        float progress;
        bool opening;
        float openSign = 1f;

        public bool IsOpen => progress >= 1f;
        public bool IsOpening => opening && progress < 1f;
        public float Progress => progress;
        /// <summary>The gate in the definition (which areas, fence line, opening).</summary>
        public GateDef Def { get; private set; }
        /// <summary>Middle of the opening, 1.2 m high (the creak plays from here).</summary>
        public Vector3 OpeningCenter => transform.TransformPoint(new Vector3(0f, 1.2f, Def.Width * 0.5f));

        /// <param name="toAreaCenter">Centre (XZ) of the area the gate opens into: the leaf swings to that side.</param>
        public static Gate Create(Transform parent, GateDef def, Vector2 toAreaCenter)
        {
            var go = new GameObject("Gate");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = def.Hinge;
            var gate = go.AddComponent<Gate>();
            gate.Def = def;
            // The model is built along local +Z. On a fence running along X it turns 90° (local +Z → world +X, local +X → world -Z).
            if (def.Axis == FenceAxis.AlongZ)
            {
                gate.openSign = toAreaCenter.x >= def.Line ? 1f : -1f;
            }
            else
            {
                go.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                gate.openSign = toAreaCenter.y <= def.Line ? 1f : -1f;
            }
            gate.Build(def.Width);
            return gate;
        }

        /// <summary>
        /// The leaf is lower than the fence and a little above the ground (bottom gap), with a frame and a Z brace on both faces; above it a
        /// two-post arch. So it reads as a "gateway" from afar too. Only the body board collides.
        /// </summary>
        void Build(float width)
        {
            leaf = new GameObject("GateLeaf").transform;
            leaf.SetParent(transform, false);
            Color dark = Palette.Hex("#3A3B40");
            Color seam = Palette.Hex("#CEC9C1");
            Surface trim = Palette.TrimWhite.As(SurfaceKind.Vinyl);
            Surface metal = dark.As(SurfaceKind.Metal);
            const float bottom = 0.1f, top = 1.6f;
            float w = width - 0.08f, z0 = 0.04f, mid = z0 + w * 0.5f, h = top - bottom;

            PrimitiveKit.Box(leaf, "Board", new Vector3(0f, bottom + h * 0.5f, mid), new Vector3(0.05f, h, w), Palette.FenceWhite.As(SurfaceKind.Vinyl));
            for (int s = 1; s < 6; s++)
                PrimitiveKit.Box(leaf, "Seam", new Vector3(0f, bottom + h * 0.5f, z0 + w * s / 6f), new Vector3(0.056f, h - 0.1f, 0.012f), seam, collider: false);
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * 0.035f;
                PrimitiveKit.Box(leaf, "TopRail", new Vector3(x, top - 0.05f, mid), new Vector3(0.03f, 0.1f, w), trim, collider: false);
                PrimitiveKit.Box(leaf, "MiddleRail", new Vector3(x, bottom + h * 0.5f, mid), new Vector3(0.03f, 0.08f, w), trim, collider: false);
                PrimitiveKit.Box(leaf, "BottomRail", new Vector3(x, bottom + 0.05f, mid), new Vector3(0.03f, 0.1f, w), trim, collider: false);
                PrimitiveKit.Box(leaf, "Post", new Vector3(x, bottom + h * 0.5f, z0 + 0.05f), new Vector3(0.03f, h, 0.1f), trim, collider: false);
                PrimitiveKit.Box(leaf, "Post", new Vector3(x, bottom + h * 0.5f, z0 + w - 0.05f), new Vector3(0.03f, h, 0.1f), trim, collider: false);
                // Z brace: in the lower and upper half, from the hinge end to the latch side
                float bx = side * 0.05f;
                PrimitiveKit.Tube(leaf, "Brace", new Vector3(bx, bottom + 0.14f, z0 + 0.12f), new Vector3(bx, bottom + h * 0.5f - 0.06f, z0 + w - 0.12f), 0.022f, trim);
                PrimitiveKit.Tube(leaf, "Brace", new Vector3(bx, bottom + h * 0.5f + 0.06f, z0 + 0.12f), new Vector3(bx, top - 0.12f, z0 + w - 0.12f), 0.022f, trim);
                // Hinges and latch
                foreach (float y in new[] { bottom + 0.18f, top - 0.18f })
                    PrimitiveKit.Box(leaf, "Hinge", new Vector3(side * 0.053f, y, z0 + 0.14f), new Vector3(0.008f, 0.05f, 0.3f), metal, collider: false);
                PrimitiveKit.Box(leaf, "Latch", new Vector3(side * 0.06f, 1.05f, z0 + w - 0.08f), new Vector3(0.025f, 0.05f, 0.14f), metal, collider: false);
                // Ring handle (below the latch, on both faces)
                var ring = new Vector3(side * 0.075f, 0.93f, z0 + w - 0.1f);
                for (int i = 0; i < 8; i++)
                {
                    float a0 = i * Mathf.PI / 4f, a1 = (i + 1) * Mathf.PI / 4f;
                    PrimitiveKit.Tube(leaf, "Ring", ring + new Vector3(0f, Mathf.Sin(a0), Mathf.Cos(a0)) * 0.045f,
                        ring + new Vector3(0f, Mathf.Sin(a1), Mathf.Cos(a1)) * 0.045f, 0.007f, metal);
                }
            }

            // Arch: two tall posts, a beam on top and caps (taller than the fence posts)
            const float postHeight = 2.4f;
            foreach (float z in new[] { 0f, width })
            {
                PrimitiveKit.Box(transform, "ArchPost", new Vector3(0f, postHeight * 0.5f, z), new Vector3(0.17f, postHeight, 0.17f), trim, collider: false);
                PrimitiveKit.Box(transform, "PoleCap", new Vector3(0f, postHeight + 0.17f, z), new Vector3(0.22f, 0.06f, 0.22f), trim, collider: false);
                // Same pyramid cap as the fence posts
                PrimitiveKit.Cone(transform, "PoleTop", new Vector3(0f, postHeight + 0.2f, z), 0.15f, 0.11f, trim, 4)
                    .transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                PrimitiveKit.Box(transform, "PoleSkirt", new Vector3(0f, 0.08f, z), new Vector3(0.22f, 0.16f, 0.22f), trim, collider: false);
            }
            PrimitiveKit.Box(transform, "Beam", new Vector3(0f, postHeight + 0.06f, width * 0.5f), new Vector3(0.12f, 0.16f, width + 0.6f), trim, collider: false);
            PrimitiveKit.Box(transform, "BeamTop", new Vector3(0f, postHeight + 0.17f, width * 0.5f), new Vector3(0.2f, 0.06f, width + 0.75f), trim, collider: false);
            // An arc under the beam (with uprights): the arch look
            const int arc = 10;
            Vector3 previous = Vector3.zero;
            for (int i = 0; i <= arc; i++)
            {
                float t = i / (float)arc;
                var p = new Vector3(0f, postHeight - 0.32f + Mathf.Sin(t * Mathf.PI) * 0.26f, t * width);
                if (i > 0)
                    PrimitiveKit.Tube(transform, "Arc", previous, p, 0.03f, trim);
                if (i > 0 && i < arc && i % 2 == 0)
                    PrimitiveKit.Tube(transform, "ArcPost", p, new Vector3(0f, postHeight - 0.02f, p.z), 0.018f, trim);
                previous = p;
            }
        }

        public void Open(bool instant = false)
        {
            opening = true;
            if (instant)
                SetProgress(1f);
        }

        /// <summary>Restoring from a save: opens or closes the gate instantly.</summary>
        public void SetOpenInstant(bool open)
        {
            opening = open;
            SetProgress(open ? 1f : 0f);
        }

        void Update()
        {
            if (opening && progress < 1f)
                SetProgress(progress + Time.deltaTime / OpenSeconds);
        }

        void SetProgress(float value)
        {
            progress = Mathf.Clamp01(value);
            float eased = progress * progress * (3f - 2f * progress);
            leaf.localRotation = Quaternion.Euler(0f, OpenAngle * openSign * eased, 0f);
        }
    }
}

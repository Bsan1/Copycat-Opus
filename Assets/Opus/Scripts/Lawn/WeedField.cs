using System;
using System.Collections.Generic;
using Opus.Core;
using Opus.World;
using UnityEngine;

namespace Opus.Lawn
{
    public enum WeedKind
    {
        /// <summary>A single plant taller than the grass, with broad leaves and flowers.</summary>
        Weed,
        /// <summary>A cluster of brown leaves hugging the base of an object.</summary>
        Overgrowth,
    }

    /// <summary>Placement data. For ivy, <see cref="Extent"/> is the cluster's XZ half size.</summary>
    public readonly struct WeedSpot
    {
        public readonly Vector3 Position;
        public readonly WeedKind Kind;
        public readonly float Scale;
        public readonly Vector2 Extent;

        public WeedSpot(float x, float z, float scale = 1f)
        {
            Position = new Vector3(x, 0f, z);
            Kind = WeedKind.Weed;
            Scale = scale;
            Extent = Vector2.one * 0.18f * scale;
        }

        public WeedSpot(float x, float z, float halfX, float halfZ)
        {
            Position = new Vector3(x, 0f, z);
            Kind = WeedKind.Overgrowth;
            Scale = 1f;
            Extent = new Vector2(halfX, halfZ);
        }
    }

    /// <summary>Numeric state of a weed/ivy (independent of the visuals; the tests check this).</summary>
    public sealed class WeedState
    {
        /// <summary>Time to pull with the trimmer (s): weeds go quickly, ivy slowly.</summary>
        public const float WeedHealth = 0.35f;
        public const float OvergrowthHealth = 1.4f;

        public Vector3 Position;
        public Vector2 Extent;
        public WeedKind Kind;
        public int Patch;
        public float Health;
        public float MaxHealth;

        public bool Removed => Health <= 0f;
        public float Fraction => MaxHealth <= 0f ? 0f : Mathf.Clamp01(Health / MaxHealth);

        public static WeedState From(WeedSpot spot, int patch)
        {
            float health = spot.Kind == WeedKind.Weed ? WeedHealth : OvergrowthHealth;
            return new WeedState
            {
                Position = spot.Position,
                Extent = spot.Extent,
                Kind = spot.Kind,
                Patch = patch,
                Health = health,
                MaxHealth = health,
            };
        }

        /// <summary>Does the trimmer circle (centre, radius) touch the weed's area (an ellipse)?</summary>
        public bool Touches(Vector3 center, float radius)
        {
            Vector2 d = new Vector2(center.x - Position.x, center.z - Position.z);
            // Grow the ellipse by the radius and check whether the point is inside
            float ex = Extent.x + radius, ez = Extent.y + radius;
            return (d.x * d.x) / (ex * ex) + (d.y * d.y) / (ez * ez) <= 1f;
        }

        /// <returns>True if this damage pulled it out.</returns>
        public bool ApplyDamage(Vector3 center, float radius, float amount)
        {
            if (Removed || amount <= 0f || !Touches(center, radius))
                return false;
            Health = Mathf.Max(0f, Health - amount);
            return Removed;
        }
    }

    /// <summary>
    /// Weeds and ivy. Only the trimmer pulls them (<see cref="Damage"/>); the mower and the player only flatten them
    /// when passing over. A pulled weed shrinks away, ivy thins out leaf by leaf.
    /// </summary>
    public sealed class WeedField : MonoBehaviour
    {
        const float RemoveSeconds = 0.18f;

        sealed class Visual
        {
            public Transform Root;
            /// <summary>Model merged per material; for ivy the parts are the leaves (in pulling order).</summary>
            public BakedParts Baked;
            public Transform Marker;
            public float RemoveTimer;
            public float Squash = 1f;
        }

        public static readonly Color MarkerColor = Palette.Hex("#E0753A");

        public static readonly Color LeafDark = Palette.Hex("#3E5C2B");
        public static readonly Color LeafMid = Palette.Hex("#557A34");
        public static readonly Color Thistle = Palette.Hex("#8E5BB5");
        public static readonly Color Dandelion = Palette.Hex("#E6C434");
        public static readonly Color VineBrown = Palette.Hex("#6B5130");
        public static readonly Color VineOlive = Palette.Hex("#7A6C3A");

        readonly List<WeedState> states = new List<WeedState>();
        readonly List<Visual> visuals = new List<Visual>();
        readonly List<IGrassPusher> pushers = new List<IGrassPusher>();

        public IReadOnlyList<WeedState> States => states;
        public int RemainingWeeds { get; private set; }
        public int RemainingOvergrowth { get; private set; }
        public int TotalWeeds { get; private set; }
        public int TotalOvergrowth { get; private set; }

        /// <summary>When a weed or ivy has been pulled out completely.</summary>
        public event Action<WeedState> Removed;

        public static WeedField Create(Transform parent, IEnumerable<WeedSpot> spots, PatchLayout layout)
        {
            var go = new GameObject("Weeds");
            go.transform.SetParent(parent, false);
            var field = go.AddComponent<WeedField>();
            var random = new System.Random(23);
            foreach (WeedSpot spot in spots)
            {
                field.states.Add(WeedState.From(spot, layout.PatchAt(new Vector2(spot.Position.x, spot.Position.z))));
                Visual visual = spot.Kind == WeedKind.Weed
                    ? BuildWeed(go.transform, spot, random)
                    : BuildOvergrowth(go.transform, spot, random);
                visual.Marker = PrimitiveKit.Sphere(go.transform, "Marker", spot.Position, Vector3.one * 0.09f, MarkerColor.As(SurfaceKind.Prop)).transform;
                visual.Marker.gameObject.SetActive(false);
                field.visuals.Add(visual);
            }
            field.Recount();
            field.TotalWeeds = field.RemainingWeeds;
            field.TotalOvergrowth = field.RemainingOvergrowth;
            return field;
        }

        public void AddPusher(IGrassPusher pusher) => pushers.Add(pusher);

        public int TotalInPatch(int patch)
        {
            int count = 0;
            foreach (WeedState state in states)
                if (state.Patch == patch) count++;
            return count;
        }

        public int RemainingInPatch(int patch)
        {
            int count = 0;
            foreach (WeedState state in states)
                if (state.Patch == patch && !state.Removed) count++;
            return count;
        }

        /// <summary>Each weed's remaining health for the save (in WeedSpots order).</summary>
        public float[] CaptureHealth()
        {
            var health = new float[states.Count];
            for (int i = 0; i < states.Count; i++)
                health[i] = Mathf.Max(0f, states[i].Health);
            return health;
        }

        /// <summary>Restores the health values; pulled ones are hidden instantly, ivy leaves by remaining health.</summary>
        /// <returns>False if the count does not match (state unchanged).</returns>
        public bool RestoreHealth(float[] health)
        {
            if (health == null || health.Length != states.Count)
                return false;
            for (int i = 0; i < states.Count; i++)
            {
                WeedState state = states[i];
                Visual visual = visuals[i];
                state.Health = Mathf.Clamp(health[i], 0f, state.MaxHealth);
                visual.RemoveTimer = 0f;
                visual.Squash = 1f;
                visual.Root.localScale = Vector3.one;
                visual.Root.gameObject.SetActive(!state.Removed);
                if (state.Kind == WeedKind.Overgrowth)
                    UpdateOvergrowthLeaves(visual, state.Fraction);
            }
            Recount();
            return true;
        }

        /// <summary>Night: each pulled weed comes back with probability <paramref name="chance"/> (in the allowed patches).</summary>
        /// <returns>Number of weeds that came back.</returns>
        public int RegrowSome(float chance, System.Random random, Func<int, bool> patchAllowed = null)
        {
            int back = 0;
            for (int i = 0; i < states.Count; i++)
            {
                WeedState state = states[i];
                if (!state.Removed || (patchAllowed != null && !patchAllowed(state.Patch)))
                    continue;
                if (random.NextDouble() >= chance)
                    continue;
                state.Health = state.MaxHealth;
                Visual visual = visuals[i];
                visual.RemoveTimer = 0f;
                visual.Squash = 1f;
                visual.Root.localScale = Vector3.one;
                visual.Root.gameObject.SetActive(true);
                if (state.Kind == WeedKind.Overgrowth)
                    UpdateOvergrowthLeaves(visual, 1f);
                back++;
            }
            if (back > 0)
                Recount();
            return back;
        }

        /// <summary>Pulls all weeds in the patch (for scenarios and the developer shortcut).</summary>
        public void RemoveAllInPatch(int patch)
        {
            for (int i = 0; i < states.Count; i++)
            {
                WeedState state = states[i];
                if (state.Patch != patch || state.Removed)
                    continue;
                state.Health = 0f;
                visuals[i].RemoveTimer = RemoveSeconds;
                Removed?.Invoke(state);
            }
            Recount();
        }

        /// <summary>
        /// Orange markers: hover above the remaining weeds in the active patch; with <paramref name="showAll"/>
        /// (show remaining) above all remaining weeds.
        /// </summary>
        public void UpdateMarkers(int activePatch, bool showAll)
        {
            float bob = Mathf.Sin(Time.timeSinceLevelLoad * 3f) * 0.04f;
            for (int i = 0; i < states.Count; i++)
            {
                WeedState state = states[i];
                bool show = !state.Removed && (showAll || state.Patch == activePatch);
                Transform marker = visuals[i].Marker;
                if (marker.gameObject.activeSelf != show)
                    marker.gameObject.SetActive(show);
                if (show)
                    marker.position = state.Position + Vector3.up * (MarkerHeight(state) + bob);
            }
        }

        static float MarkerHeight(WeedState state) => state.Kind == WeedKind.Weed ? 1.05f : 1.15f;

        /// <summary>Trimmer damage: <paramref name="amount"/> (s) to every weed touching the circle.</summary>
        /// <param name="touched">Weeds touched in this frame (not yet pulled or just pulled); for the debris.</param>
        /// <returns>Number of newly pulled ones.</returns>
        public int Damage(Vector3 center, float radius, float amount, List<WeedState> touched = null)
        {
            int removed = 0;
            for (int i = 0; i < states.Count; i++)
            {
                WeedState state = states[i];
                if (state.Removed || !state.Touches(center, radius))
                    continue;
                touched?.Add(state);
                if (state.ApplyDamage(center, radius, amount))
                {
                    removed++;
                    visuals[i].RemoveTimer = RemoveSeconds;
                    Removed?.Invoke(state);
                }
                else if (state.Kind == WeedKind.Overgrowth)
                {
                    UpdateOvergrowthLeaves(visuals[i], state.Fraction);
                }
            }
            if (removed > 0)
                Recount();
            return removed;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < states.Count; i++)
            {
                Visual visual = visuals[i];
                if (!visual.Root.gameObject.activeSelf)
                    continue;

                if (states[i].Removed)
                {
                    visual.RemoveTimer -= dt;
                    float t = Mathf.Clamp01(visual.RemoveTimer / RemoveSeconds);
                    visual.Root.localScale = Vector3.one * t;
                    if (t <= 0f)
                        visual.Root.gameObject.SetActive(false);
                    continue;
                }

                if (states[i].Kind == WeedKind.Weed)
                {
                    // Flattens while the mower/player passes over, then slowly straightens up.
                    float target = 1f;
                    Vector3 p = states[i].Position;
                    foreach (IGrassPusher pusher in pushers)
                    {
                        if (pusher == null || !pusher.TryGetGrassPush(out Vector4 push))
                            continue;
                        float distance = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(push.x, push.y));
                        float influence = Mathf.Clamp01(1f - distance / (push.z + 0.15f)) * push.w;
                        target = Mathf.Min(target, Mathf.Lerp(1f, 0.25f, Mathf.Clamp01(influence * 2.5f)));
                    }
                    float rate = target < visual.Squash ? 12f : 2.5f;
                    visual.Squash = Mathf.Lerp(visual.Squash, target, 1f - Mathf.Exp(-dt * rate));
                    visual.Root.localScale = new Vector3(1f, visual.Squash, 1f);
                }
            }
        }

        void Recount()
        {
            RemainingWeeds = 0;
            RemainingOvergrowth = 0;
            foreach (WeedState state in states)
            {
                if (state.Removed) continue;
                if (state.Kind == WeedKind.Weed) RemainingWeeds++;
                else RemainingOvergrowth++;
            }
        }

        static void UpdateOvergrowthLeaves(Visual visual, float fraction)
        {
            visual.Baked.ShowFirst(Mathf.CeilToInt(visual.Baked.PartCount * fraction));
        }

        // A bushy, broad-leaved weed taller than the grass: leaves along each of 3-4 stems, flowers at the tips.
        // Darker green than the grass so it stands out in long grass too (in the reference at flowerbed/fence bases).
        static Visual BuildWeed(Transform parent, WeedSpot spot, System.Random random)
        {
            float Next() => (float)random.NextDouble();
            using PrimitiveKit.DefaultKindScope plants = PrimitiveKit.DefaultKind(SurfaceKind.Plant);
            Transform root = new GameObject("Weed").transform;
            root.SetParent(parent, false);
            root.localPosition = spot.Position;
            root.localRotation = Quaternion.Euler(0f, Next() * 360f, 0f);
            Transform body = new GameObject("Plant").transform;
            body.SetParent(root, false);
            body.localScale = Vector3.one * spot.Scale;

            int rosette = 7 + random.Next(3);
            for (int i = 0; i < rosette; i++)
            {
                float yaw = i * 360f / rosette + Next() * 20f;
                Leaf(body, yaw, 0.05f, 0.06f, new Vector3(0.13f, 0.02f, 0.32f), 35f + Next() * 20f, Next() < 0.5f ? LeafDark : LeafMid);
            }

            bool thistle = Next() < 0.5f;
            int stems = 3 + random.Next(2);
            for (int s = 0; s < stems; s++)
            {
                float height = Mathf.Lerp(0.55f, 0.8f, Next());
                float lean = Mathf.Lerp(8f, 22f, Next());
                Transform stem = new GameObject("Stem").transform;
                stem.SetParent(body, false);
                stem.localRotation = Quaternion.Euler(lean, s * 360f / stems + Next() * 40f, 0f);
                PrimitiveKit.Tube(stem, "Body", Vector3.zero, new Vector3(0f, height, 0f), 0.011f, LeafDark);
                for (int i = 0; i < 4; i++)
                {
                    float y = height * (0.25f + i * 0.19f);
                    Leaf(stem, i * 137f + Next() * 40f, y, 0.02f, new Vector3(0.1f, 0.016f, 0.22f) * (1f - i * 0.12f), 35f + Next() * 20f,
                        Next() < 0.6f ? LeafMid : LeafDark);
                }
                PrimitiveKit.Sphere(stem, "Flower", new Vector3(0f, height + 0.02f, 0f), Vector3.one * Mathf.Lerp(0.06f, 0.085f, Next()),
                    thistle ? Thistle : Dandelion);
            }
            return new Visual { Root = root, Baked = MeshBaker.Bake(root, name: "WeedModel") };
        }

        static void Leaf(Transform parent, float yaw, float height, float outward, Vector3 size, float tilt, Color color)
        {
            Transform pivot = new GameObject("LeafAxis").transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = new Vector3(0f, height, 0f);
            pivot.localRotation = Quaternion.Euler(-tilt, yaw, 0f);
            PrimitiveKit.Leaf(pivot, "Leaf", new Vector3(0f, 0f, outward + size.z * 0.45f), new Vector3(size.x, size.y * 2f, size.z), color);
        }

        // A dense heap of leaves hugging a wall's base: overlapping leaves near the surface of a half ellipsoid (mound).
        static Visual BuildOvergrowth(Transform parent, WeedSpot spot, System.Random random)
        {
            float Next() => (float)random.NextDouble();
            using PrimitiveKit.DefaultKindScope plants = PrimitiveKit.DefaultKind(SurfaceKind.Plant);
            Transform root = new GameObject("Ivy").transform;
            root.SetParent(parent, false);
            root.localPosition = spot.Position;

            const float height = 0.75f;
            int count = Mathf.Clamp(Mathf.RoundToInt(spot.Extent.x * spot.Extent.y * 2400f), 120, 420);
            var leaves = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                // A point by direction on a hemisphere, scaled to the ellipsoid; most near the surface (so it looks full)
                float azimuth = Next() * Mathf.PI * 2f;
                float elevation = Mathf.Asin(Mathf.Sqrt(Next())); // so it does not thin out towards the top
                float shell = Mathf.Lerp(0.65f, 1f, Mathf.Sqrt(Next()));
                var direction = new Vector3(Mathf.Cos(azimuth) * Mathf.Cos(elevation), Mathf.Sin(elevation), Mathf.Sin(azimuth) * Mathf.Cos(elevation));
                var local = new Vector3(direction.x * spot.Extent.x, direction.y * height, direction.z * spot.Extent.y) * shell;
                float s = Mathf.Lerp(0.07f, 0.12f, Next());
                Color color = Next() < 0.55f ? VineBrown : Next() < 0.7f ? VineOlive : Color.Lerp(VineBrown, Color.black, 0.35f);
                GameObject leaf = PrimitiveKit.Leaf(root, "Leaf", local, new Vector3(s * 0.8f, s * 0.25f, s * 1.2f), color);
                // Leaf face pointing outwards, a little random
                leaf.transform.localRotation = Quaternion.LookRotation(direction + new Vector3(Next() - 0.5f, Next() - 0.5f, Next() - 0.5f) * 0.8f) * Quaternion.Euler(90f, 0f, 0f);
                leaves[i] = leaf.transform;
            }
            // While being pulled, the leaves disappear in random order
            for (int i = leaves.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (leaves[i], leaves[j]) = (leaves[j], leaves[i]);
            }
            return new Visual { Root = root, Baked = MeshBaker.Bake(root, leaves, "IvyModel") };
        }
    }
}

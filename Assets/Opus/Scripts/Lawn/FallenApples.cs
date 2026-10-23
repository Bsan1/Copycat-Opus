using System;
using System.Collections.Generic;
using Opus.Core;
using Opus.World;
using UnityEngine;

namespace Opus.Lawn
{
    /// <summary>
    /// Apples that drop under the fruit trees every morning. They are half hidden in the long grass; walking over them
    /// collects them (points). They only drop under trees in unlocked areas; when a new area unlocks, under its trees too.
    /// </summary>
    public sealed class FallenApples : MonoBehaviour
    {
        public const float PickRadius = 0.6f;
        /// <summary>Distance of an apple from the trunk (m).</summary>
        public const float MinDrop = 0.55f, MaxDrop = 1.6f;
        static readonly Color[] Colors = { Palette.Hex("#C8322E"), Palette.Hex("#D9472F"), Palette.Hex("#B52A2A"), Palette.Hex("#E0602F") };
        static readonly Color Stem = Palette.Hex("#5A4232");

        public readonly struct Tree
        {
            public readonly Vector3 Position;
            public readonly int Area;

            public Tree(Vector3 position, int area)
            {
                Position = position;
                Area = area;
            }
        }

        sealed class Apple
        {
            public Vector3 Position;
            public int Area;
            public Transform Visual;
        }

        readonly List<Apple> active = new List<Apple>();
        readonly List<Tree> trees = new List<Tree>();
        LawnState lawn;

        public int Count => active.Count;
        public IReadOnlyList<Tree> Trees => trees;
        /// <summary>An apple was collected (position).</summary>
        public event Action<Vector3> Picked;

        public static FallenApples Create(Transform parent, LawnState lawn, IEnumerable<Tree> trees)
        {
            var go = new GameObject("FallenApples");
            go.transform.SetParent(parent, false);
            var apples = go.AddComponent<FallenApples>();
            apples.lawn = lawn;
            apples.trees.AddRange(trees);
            return apples;
        }

        /// <summary>The fruit trees in the definition (with their areas).</summary>
        public static IEnumerable<Tree> TreesOf(YardDefinition definition)
        {
            foreach (PropPlacement prop in definition.Props)
                if (prop.Kind == PropKind.FruitTree)
                    yield return new Tree(prop.Position, definition.AreaAt(prop.Position)?.Id ?? 0);
        }

        /// <summary>The day's apples: the previous day's leftovers are removed, new ones drop under the trees in unlocked areas.</summary>
        public int SpawnForDay(int day, Func<int, bool> areaOpen)
        {
            Clear();
            int added = 0;
            var areas = new HashSet<int>();
            foreach (Tree tree in trees)
                if (areaOpen(tree.Area)) areas.Add(tree.Area);
            foreach (int area in areas)
                added += SpawnForArea(day, area);
            return added;
        }

        /// <summary>Drops apples under an area's trees (1–2 apples under more than half of the trees).</summary>
        public int SpawnForArea(int day, int area)
        {
            var random = new System.Random(day * 7919 + area * 131 + 5);
            float Next() => (float)random.NextDouble();
            int added = 0;
            foreach (Tree tree in trees)
            {
                if (tree.Area != area || Next() < 0.35f)
                    continue;
                int count = 1 + (Next() < 0.4f ? 1 : 0);
                for (int i = 0, attempts = 0; i < count && attempts < 12; attempts++)
                {
                    float a = Next() * Mathf.PI * 2f;
                    float r = Mathf.Lerp(MinDrop, MaxDrop, Mathf.Sqrt(Next()));
                    var position = tree.Position + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    if (lawn.Layout.PatchAt(new Vector2(position.x, position.z)) == PatchLayout.NoGrass
                        || active.Exists(apple => (apple.Position - position).sqrMagnitude < 0.25f))
                        continue;
                    Spawn(position, area, Next() * 360f, Colors[random.Next(Colors.Length)]);
                    i++;
                    added++;
                }
            }
            return added;
        }

        void Spawn(Vector3 position, int area, float yaw, Color color)
        {
            Transform root = new GameObject("Apple").transform;
            root.SetParent(transform, false);
            root.localPosition = position + Vector3.up * (lawn.Grid.TryGetCell(new Vector2(position.x, position.z), out int cx, out int cz)
                ? RestingHeight(lawn.HeightAt(lawn.Grid.Index(cx, cz))) : 0f);
            root.localRotation = Quaternion.Euler(Mathf.Lerp(-25f, 25f, yaw / 360f), yaw, 0f);
            PrimitiveKit.Sphere(root, "Fruit", new Vector3(0f, 0.045f, 0f), new Vector3(0.1f, 0.092f, 0.1f), color.As(SurfaceKind.Prop));
            PrimitiveKit.Tube(root, "Stem", new Vector3(0f, 0.085f, 0f), new Vector3(0.008f, 0.115f, 0.004f), 0.005f, Stem.As(SurfaceKind.Prop));
            active.Add(new Apple { Position = position, Area = area, Visual = root });
        }

        /// <summary>
        /// The apple rises with the height of the grass under it: it rests on top of long grass so it does not get lost in it, and
        /// drops to the ground once the grass is mown (readability before realism).
        /// </summary>
        public static float RestingHeight(byte grassHeight) => 0.01f + grassHeight / 255f * 0.3f;

        void Update()
        {
            float step = Time.deltaTime * 0.8f;
            foreach (Apple apple in active)
            {
                if (!lawn.Grid.TryGetCell(new Vector2(apple.Position.x, apple.Position.z), out int x, out int z))
                    continue;
                float target = RestingHeight(lawn.HeightAt(lawn.Grid.Index(x, z)));
                Vector3 p = apple.Visual.localPosition;
                p.y = Mathf.MoveTowards(p.y, target, step);
                apple.Visual.localPosition = p;
            }
        }

        /// <summary>Collects the apples near the feet; returns how many were collected.</summary>
        public int PickNear(Vector3 feet)
        {
            int picked = 0;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Apple apple = active[i];
                float dx = apple.Position.x - feet.x, dz = apple.Position.z - feet.z;
                if (dx * dx + dz * dz > PickRadius * PickRadius)
                    continue;
                DestroySafe(apple.Visual.gameObject);
                active.RemoveAt(i);
                picked++;
                Picked?.Invoke(apple.Position);
            }
            return picked;
        }

        public Vector3[] Capture()
        {
            var positions = new Vector3[active.Count];
            for (int i = 0; i < active.Count; i++)
                positions[i] = active[i].Position;
            return positions;
        }

        /// <summary>Restoring from a save: the apples where they were saved (colours are derived from the position).</summary>
        public void Restore(Vector3[] positions)
        {
            Clear();
            if (positions == null)
                return;
            foreach (Vector3 position in positions)
            {
                int area = 0;
                float best = float.MaxValue;
                foreach (Tree tree in trees)
                {
                    float d = (tree.Position - position).sqrMagnitude;
                    if (d < best) { best = d; area = tree.Area; }
                }
                int hash = Mathf.Abs(Mathf.RoundToInt(position.x * 97f) ^ Mathf.RoundToInt(position.z * 61f));
                Spawn(position, area, hash % 360, Colors[hash % Colors.Length]);
            }
        }

        public void Clear()
        {
            foreach (Apple apple in active)
                DestroySafe(apple.Visual.gameObject);
            active.Clear();
        }

        static void DestroySafe(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
    }
}

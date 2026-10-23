using System;
using System.Collections.Generic;
using Opus.Core;
using Opus.World;
using UnityEngine;

namespace Opus.Lawn
{
    /// <summary>An item in the collection: id, name and a small model builder.</summary>
    public sealed class FindDef
    {
        public string Id;
        public string Name;
        public Action<Transform> Build;
    }

    /// <summary>
    /// Small items lost in the grass (§3-F; no toys, only ordinary garden finds). The items are hidden in the long
    /// grass; once the grass over them is mown/trimmed they pop out and are collected. Now and then a small sparkle on the grass
    /// gives a hint. Each item enters the collection once; new items are hidden every day (at most
    /// <see cref="MaxActive"/>).
    /// </summary>
    public sealed class LostItems : MonoBehaviour
    {
        public const int MaxActive = 3;
        const float PopSeconds = 0.9f;

        static readonly Color Brass = Palette.Hex("#C9A443");
        static readonly Color Gold = Palette.Hex("#E2BE4A");
        static readonly Color Leather = Palette.Hex("#C77B3A");
        static readonly Color Dark = Palette.Hex("#2C2D32");
        static readonly Color Bark = Palette.Hex("#7A5232");
        static readonly Color Shell = Palette.Hex("#D8C3A0");
        static readonly Color Red = Palette.Hex("#C8373A");
        static readonly Color Cream = Palette.Hex("#F1E9D8");
        static readonly Color Steel = Palette.Hex("#B9BEC6");
        static readonly Color Pencil = Palette.Hex("#E9B83C");
        static readonly Color Teal = Palette.Hex("#4FA3A5");
        static readonly Color Feather = Palette.Hex("#6C8CC2");

        // The ids are save keys from the Turkish build, kept so existing collections still load.
        public static readonly FindDef[] Catalog =
        {
            new FindDef { Id = "eldiven", Name = "Garden glove", Build = BuildGlove },
            new FindDef { Id = "anahtar", Name = "Old key", Build = BuildKey },
            new FindDef { Id = "para", Name = "Coin", Build = BuildCoin },
            new FindDef { Id = "gozluk", Name = "Sunglasses", Build = BuildGlasses },
            new FindDef { Id = "tohum", Name = "Seed packet", Build = BuildSeedPacket },
            new FindDef { Id = "kozalak", Name = "Pine cone", Build = BuildCone },
            new FindDef { Id = "salyangoz", Name = "Snail shell", Build = BuildSnailShell },
            new FindDef { Id = "tuy", Name = "Bird feather", Build = BuildFeather },
            new FindDef { Id = "makas", Name = "Pruning shears", Build = BuildShears },
            new FindDef { Id = "kapak", Name = "Bottle cap", Build = BuildCap },
            new FindDef { Id = "dugme", Name = "Coat button", Build = BuildButton },
            new FindDef { Id = "kalem", Name = "Pencil", Build = BuildPencil },
        };

        sealed class Item
        {
            public FindDef Def;
            public Vector3 Position;
            public int Cell;
            public Transform Visual;
            public Transform Glint;
            public bool Found;
            public float FoundTime;
            public float GlintPhase;
        }

        [Serializable]
        public sealed class SavedItem
        {
            public string id;
            public Vector3 position;
        }

        readonly List<Item> active = new List<Item>();
        readonly HashSet<string> collected = new HashSet<string>();
        LawnState lawn;

        public IReadOnlyCollection<string> Collected => collected;
        public int ActiveCount
        {
            get
            {
                int n = 0;
                foreach (Item item in active) if (!item.Found) n++;
                return n;
            }
        }
        public bool IsComplete => collected.Count >= Catalog.Length;
        /// <summary>Found (item, position).</summary>
        public event Action<FindDef, Vector3> Found;

        public static LostItems Create(Transform parent, LawnState lawn)
        {
            var go = new GameObject("LostItems");
            go.transform.SetParent(parent, false);
            var items = go.AddComponent<LostItems>();
            items.lawn = lawn;
            return items;
        }

        public static FindDef DefOf(string id) => Array.Find(Catalog, d => d.Id == id);

        /// <summary>Hides items not yet hidden or found in the long grass of the unlocked areas, from a seed.</summary>
        /// <returns>Number of newly hidden items.</returns>
        public int HideForDay(int day, Func<int, bool> patchAllowed)
        {
            var random = new System.Random(day * 6151 + 3);
            var pool = new List<FindDef>();
            foreach (FindDef def in Catalog)
                if (!collected.Contains(def.Id) && !active.Exists(i => !i.Found && i.Def == def))
                    pool.Add(def);
            int added = 0;
            int attempts = 0;
            while (ActiveCount < MaxActive && pool.Count > 0 && attempts++ < 400)
            {
                int cell = random.Next(lawn.Grid.CellCount);
                int patch = lawn.Layout.PatchIds[cell];
                if (patch == PatchLayout.NoGrass || !patchAllowed(patch) || lawn.IsCut(cell) || !SurroundedByGrass(cell))
                    continue;
                Vector2 c = lawn.Grid.CellCenter(cell);
                var position = new Vector3(c.x, 0f, c.y);
                if (active.Exists(i => !i.Found && Vector3.Distance(i.Position, position) < 3f))
                    continue;
                FindDef def = pool[random.Next(pool.Count)];
                pool.Remove(def);
                Spawn(def, position, (float)random.NextDouble() * 360f);
                added++;
            }
            return added;
        }

        bool SurroundedByGrass(int cell)
        {
            LawnGrid grid = lawn.Grid;
            int x = cell % grid.Width, z = cell / grid.Width;
            // Grass within 0.8 m around: not hidden at an obstacle's base (so the mower housing does not bump into it and miss)
            for (int dz = -8; dz <= 8; dz += 2)
            for (int dx = -8; dx <= 8; dx += 2)
            {
                int nx = x + dx, nz = z + dz;
                if (nx < 0 || nz < 0 || nx >= grid.Width || nz >= grid.Height || !lawn.IsGrass(grid.Index(nx, nz)))
                    return false;
            }
            return true;
        }

        void Spawn(FindDef def, Vector3 position, float yaw)
        {
            if (!lawn.Grid.TryGetCell(new Vector2(position.x, position.z), out int x, out int z))
                return;
            var root = new GameObject("Item_" + def.Id).transform;
            root.SetParent(transform, false);
            root.position = position;
            root.rotation = Quaternion.Euler(0f, yaw, 0f);
            using (PrimitiveKit.DefaultKind(SurfaceKind.Prop))
                def.Build(root);
            Transform glint = PrimitiveKit.Sphere(transform, "Sparkle", position + Vector3.up * 0.47f, Vector3.one * 0.05f, Palette.Hex("#FFF6D8").As(SurfaceKind.Prop)).transform;
            glint.gameObject.SetActive(false);
            active.Add(new Item
            {
                Def = def, Position = position, Cell = lawn.Grid.Index(x, z), Visual = root, Glint = glint,
                GlintPhase = (position.x * 0.37f + position.z * 0.71f) % 4f,
            });
        }

        /// <summary>Counts hidden items whose grass has been mown as found (every frame; tests call it too).</summary>
        /// <returns>Number of items found in this call.</returns>
        public int CheckFound()
        {
            int found = 0;
            foreach (Item item in active)
            {
                if (item.Found || !lawn.IsCut(item.Cell))
                    continue;
                item.Found = true;
                item.FoundTime = Time.time;
                item.Glint.gameObject.SetActive(false);
                collected.Add(item.Def.Id);
                found++;
                Found?.Invoke(item.Def, item.Position);
            }
            return found;
        }

        void Update()
        {
            CheckFound();
            float now = Time.time;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Item item = active[i];
                if (!item.Found)
                {
                    // Now and then a sparkle: a short blink every 4 s
                    float phase = (now + item.GlintPhase) % 4f;
                    bool on = phase < 0.35f;
                    if (item.Glint.gameObject.activeSelf != on)
                        item.Glint.gameObject.SetActive(on);
                    if (on)
                        item.Glint.localScale = Vector3.one * (0.05f * Mathf.Sin(phase / 0.35f * Mathf.PI));
                    continue;
                }

                // A found item pops up, spins and shrinks away
                float t = (now - item.FoundTime) / PopSeconds;
                if (t >= 1f)
                {
                    DestroySafe(item.Visual.gameObject);
                    DestroySafe(item.Glint.gameObject);
                    active.RemoveAt(i);
                    continue;
                }
                float height = Mathf.Sin(Mathf.Min(1f, t * 1.4f) * Mathf.PI) * 0.55f + 0.1f;
                item.Visual.position = item.Position + Vector3.up * height;
                item.Visual.rotation = Quaternion.Euler(0f, t * 540f, 0f);
                item.Visual.localScale = Vector3.one * (1.4f - Mathf.Max(0f, t - 0.6f) * 3.4f);
            }
        }

        static void DestroySafe(UnityEngine.Object obj)
        {
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        public string[] CaptureCollected()
        {
            var ids = new List<string>(collected);
            ids.Sort();
            return ids.ToArray();
        }

        public SavedItem[] CaptureActive()
        {
            var list = new List<SavedItem>();
            foreach (Item item in active)
                if (!item.Found) list.Add(new SavedItem { id = item.Def.Id, position = item.Position });
            return list.ToArray();
        }

        /// <summary>Restoring from a save: the collection and the hidden items.</summary>
        public void Restore(string[] collectedIds, SavedItem[] hidden)
        {
            foreach (Item item in active)
            {
                DestroySafe(item.Visual.gameObject);
                DestroySafe(item.Glint.gameObject);
            }
            active.Clear();
            collected.Clear();
            if (collectedIds != null)
                foreach (string id in collectedIds)
                    if (DefOf(id) != null) collected.Add(id);
            if (hidden != null)
                foreach (SavedItem saved in hidden)
                {
                    FindDef def = DefOf(saved.id);
                    if (def != null && !collected.Contains(def.Id))
                        Spawn(def, saved.position, (saved.position.x * 53f + saved.position.z * 17f) % 360f);
                }
        }

        /// <summary>Positions of the hidden items (for scenarios and tests).</summary>
        public List<Vector3> HiddenPositions()
        {
            var list = new List<Vector3>();
            foreach (Item item in active)
                if (!item.Found) list.Add(item.Position);
            return list;
        }

        // ---------- Models (small enough to stay inside the grass on the ground) ----------

        static void BuildGlove(Transform root)
        {
            PrimitiveKit.Box(root, "Palm", new Vector3(0f, 0.025f, 0f), new Vector3(0.1f, 0.03f, 0.12f), Leather, collider: false);
            for (int f = 0; f < 4; f++)
                PrimitiveKit.Box(root, "Finger", new Vector3(-0.036f + f * 0.024f, 0.022f, 0.1f), new Vector3(0.02f, 0.022f, 0.08f - Mathf.Abs(f - 1.5f) * 0.01f), Leather, collider: false);
            PrimitiveKit.Box(root, "Thumb", new Vector3(0.07f, 0.022f, 0.03f), new Vector3(0.022f, 0.022f, 0.06f), Leather, 40f, collider: false);
            PrimitiveKit.Box(root, "Wrist", new Vector3(0f, 0.027f, -0.08f), new Vector3(0.11f, 0.034f, 0.05f), Teal, collider: false);
        }

        static void BuildKey(Transform root)
        {
            for (int s = 0; s < 8; s++)
            {
                float a = s * Mathf.PI / 4f;
                PrimitiveKit.Box(root, "Ring", new Vector3(Mathf.Cos(a) * 0.03f, 0.01f, Mathf.Sin(a) * 0.03f - 0.06f), new Vector3(0.016f, 0.008f, 0.026f), Brass, -a * Mathf.Rad2Deg, collider: false);
            }
            PrimitiveKit.Box(root, "Shank", new Vector3(0f, 0.01f, 0.02f), new Vector3(0.012f, 0.008f, 0.1f), Brass, collider: false);
            PrimitiveKit.Box(root, "Tooth", new Vector3(0.014f, 0.01f, 0.06f), new Vector3(0.018f, 0.008f, 0.012f), Brass, collider: false);
            PrimitiveKit.Box(root, "Tooth", new Vector3(0.012f, 0.01f, 0.04f), new Vector3(0.014f, 0.008f, 0.01f), Brass, collider: false);
        }

        static void BuildCoin(Transform root)
        {
            PrimitiveKit.Cylinder(root, "Coin", new Vector3(0f, 0.004f, 0f), 0.028f, 0.008f, Gold, collider: false);
            PrimitiveKit.Cylinder(root, "Emboss", new Vector3(0f, 0.012f, 0f), 0.016f, 0.002f, Brass, collider: false);
        }

        static void BuildGlasses(Transform root)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                PrimitiveKit.Cylinder(root, "Lens", new Vector3(side * 0.04f, 0.03f, 0f), 0.028f, 0.006f, Dark, collider: false)
                    .transform.localRotation = Quaternion.Euler(80f, 0f, 0f);
                PrimitiveKit.Box(root, "Arm", new Vector3(side * 0.07f, 0.012f, -0.05f), new Vector3(0.006f, 0.006f, 0.11f), Dark, collider: false);
            }
            PrimitiveKit.Box(root, "Bridge", new Vector3(0f, 0.04f, 0f), new Vector3(0.025f, 0.006f, 0.006f), Dark, collider: false);
        }

        static void BuildSeedPacket(Transform root)
        {
            PrimitiveKit.Box(root, "Packet", new Vector3(0f, 0.006f, 0f), new Vector3(0.09f, 0.008f, 0.13f), Cream, 8f, collider: false);
            PrimitiveKit.Box(root, "Picture", new Vector3(0f, 0.011f, 0.015f), new Vector3(0.07f, 0.002f, 0.07f), Palette.Hex("#D9604A"), 8f, collider: false);
            PrimitiveKit.Box(root, "Text", new Vector3(0f, 0.011f, -0.045f), new Vector3(0.06f, 0.002f, 0.015f), Teal, 8f, collider: false);
        }

        static void BuildCone(Transform root)
        {
            for (int i = 0; i < 5; i++)
            {
                float t = i / 4f;
                PrimitiveKit.Sphere(root, "Washer", new Vector3(0f, 0.035f, -0.04f + t * 0.08f), new Vector3(0.06f, 0.055f, 0.03f) * Mathf.Lerp(1f, 0.55f, t), Bark);
            }
        }

        static void BuildSnailShell(Transform root)
        {
            for (int i = 0; i < 6; i++)
            {
                float a = i * 1.1f;
                float r = 0.03f * (1f - i * 0.13f);
                PrimitiveKit.Sphere(root, "Curl", new Vector3(Mathf.Cos(a) * (0.022f - i * 0.003f), 0.03f + i * 0.004f, Mathf.Sin(a) * (0.022f - i * 0.003f)), Vector3.one * r * 2f,
                    Color.Lerp(Shell, Bark, i % 2 == 0 ? 0f : 0.35f));
            }
        }

        static void BuildFeather(Transform root)
        {
            PrimitiveKit.Leaf(root, "Feather", new Vector3(0f, 0.012f, 0f), new Vector3(0.05f, 0.008f, 0.17f), Feather).transform.localRotation = Quaternion.Euler(0f, 0f, 12f);
            PrimitiveKit.Box(root, "Pencil", new Vector3(0f, 0.012f, -0.01f), new Vector3(0.004f, 0.004f, 0.19f), Cream, collider: false);
        }

        static void BuildShears(Transform root)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                PrimitiveKit.Box(root, "Blade", new Vector3(side * 0.008f, 0.012f, 0.06f), new Vector3(0.012f, 0.008f, 0.08f), Steel, side * 6f, collider: false);
                PrimitiveKit.Box(root, "Handle", new Vector3(side * 0.03f, 0.016f, -0.05f), new Vector3(0.02f, 0.018f, 0.11f), Red, side * -14f, collider: false);
            }
            PrimitiveKit.Cylinder(root, "Pin", new Vector3(0f, 0.008f, 0.015f), 0.01f, 0.016f, Dark, collider: false);
        }

        static void BuildCap(Transform root)
        {
            PrimitiveKit.Cylinder(root, "Lid", new Vector3(0f, 0f, 0f), 0.017f, 0.009f, Red, collider: false);
            PrimitiveKit.Cylinder(root, "Liner", new Vector3(0f, 0.009f, 0f), 0.013f, 0.001f, Steel, collider: false);
        }

        static void BuildButton(Transform root)
        {
            PrimitiveKit.Cylinder(root, "Button", new Vector3(0f, 0f, 0f), 0.02f, 0.006f, Bark, collider: false);
            for (int h = 0; h < 4; h++)
                PrimitiveKit.Cylinder(root, "Hole", new Vector3((h % 2 - 0.5f) * 0.012f, 0.006f, (h / 2 - 0.5f) * 0.012f), 0.0025f, 0.001f, Dark, collider: false);
        }

        static void BuildPencil(Transform root)
        {
            PrimitiveKit.Box(root, "Body", new Vector3(0f, 0.007f, 0f), new Vector3(0.014f, 0.014f, 0.15f), Pencil, collider: false);
            PrimitiveKit.Box(root, "Eraser", new Vector3(0f, 0.007f, -0.083f), new Vector3(0.014f, 0.014f, 0.018f), Palette.Hex("#E79AA0"), collider: false);
            PrimitiveKit.Cone(root, "Tip", new Vector3(0f, 0.007f, 0.075f), 0.007f, 0.025f, Shell).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }
    }
}

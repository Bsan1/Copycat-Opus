using Opus.Core;
using UnityEngine;

namespace Opus.World
{
    /// <summary>
    /// Props of the orchard and the vegetable garden: apple tree, raised vegetable beds (cabbage, tomato, lettuce, carrot),
    /// wheelbarrow, rain barrel, bean trellis, apple crate. Only the bodies collide (an invisible box or
    /// cylinder); the details are visual and each group is merged into a static batch (YardBuilder).
    /// </summary>
    public static class GardenProps
    {
        static readonly Color Wood = Palette.Hex("#8A6748");
        static readonly Color WoodDark = Palette.Hex("#6E513A");
        static readonly Color WoodLight = Palette.Hex("#A2805C");
        static readonly Color Soil = Palette.Hex("#4A3A2E");
        static readonly Color TrunkBrown = Palette.Hex("#6B4A33");
        static readonly Color[] Canopy = { Palette.Hex("#7FA84A"), Palette.Hex("#8DB04E"), Palette.Hex("#6E9A3E") };
        static readonly Color CanopyShade = Palette.Hex("#4F7236");
        static readonly Color[] Apples = { Palette.Hex("#C8322E"), Palette.Hex("#D9472F"), Palette.Hex("#B52A2A"), Palette.Hex("#E0602F") };
        static readonly Color[] Cabbage = { Palette.Hex("#93B79E"), Palette.Hex("#7FA88A"), Palette.Hex("#A3C2A6") };
        static readonly Color[] Lettuce = { Palette.Hex("#A8CF62"), Palette.Hex("#94C056"), Palette.Hex("#B9D873") };
        static readonly Color CarrotLeaf = Palette.Hex("#6F9E3C");
        static readonly Color CarrotTop = Palette.Hex("#E07B2E");
        static readonly Color TomatoLeaf = Palette.Hex("#5E8A38");
        static readonly Color Tomato = Palette.Hex("#D2382F");
        static readonly Color Stake = Palette.Hex("#A57C52");
        static readonly Color Twine = Palette.Hex("#C9B48A");
        static readonly Color BeanLeaf = Palette.Hex("#6E9E42");
        static readonly Color BeanPod = Palette.Hex("#8DB64E");
        static readonly Color BeanFlower = Palette.Hex("#E8E2D6");
        static readonly Color TrayGreen = Palette.Hex("#4E7A5E");
        static readonly Color MetalDark = Palette.Hex("#3A3B40");
        static readonly Color Rubber = Palette.Hex("#2A2A2E");
        static readonly Color Hoop = Palette.Hex("#4A4B50");

        public const float TreeColliderRadius = 0.16f;
        public const float BedHeight = 0.35f;

        // ---------- Apple tree ----------

        /// <summary>
        /// Short, slightly leaning trunk, roots, four main branches, a round crown (dark underside) and red apples on the branches.
        /// The colliding trunk is an invisible cylinder (the mower passes between them).
        /// </summary>
        public static Transform BuildFruitTree(Transform root, Vector3 at, int variant)
        {
            Transform tree = Group(root, "AppleTree");
            var random = new System.Random(101 + variant * 17);
            float Next() => (float)random.NextDouble();
            Surface bark = TrunkBrown.As(SurfaceKind.Bark, 0.7f);

            GameObject trunk = PrimitiveKit.Cylinder(tree, "Body", at, TreeColliderRadius, 1.6f, bark);
            trunk.GetComponent<MeshRenderer>().enabled = false;

            float leanAngle = Next() * Mathf.PI * 2f;
            var lean = new Vector3(Mathf.Cos(leanAngle), 0f, Mathf.Sin(leanAngle)) * Mathf.Lerp(0.06f, 0.16f, Next());
            Vector3 middle = at + Vector3.up * 0.75f + lean * 0.5f;
            Vector3 crown = at + Vector3.up * 1.4f + lean;
            PrimitiveKit.Tube(tree, "BodySlice", at, middle + Vector3.up * 0.05f, 0.13f, bark);
            PrimitiveKit.Tube(tree, "BodySlice", middle, crown + Vector3.up * 0.05f, 0.105f, bark);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + Next() * 0.6f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                PrimitiveKit.Tube(tree, "Root", at + dir * 0.06f + Vector3.up * 0.22f, at + dir * Mathf.Lerp(0.3f, 0.42f, Next()) + Vector3.up * 0.02f, 0.06f, bark);
            }

            Vector3 canopy = crown + Vector3.up * 1.05f;
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + Next() * 0.8f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 end = crown + dir * Mathf.Lerp(0.7f, 1.0f, Next()) + Vector3.up * Mathf.Lerp(0.45f, 0.8f, Next());
                PrimitiveKit.Tube(tree, "Branch", crown - Vector3.up * 0.1f, end, 0.06f, bark);
                PrimitiveKit.Tube(tree, "Twig", end, end + dir * 0.35f + Vector3.up * 0.3f, 0.035f, bark);
            }

            // Crown: a big sphere in the middle, six clusters around it; the lower ones are dark
            Surface Leaves(float height01) =>
                Color.Lerp(CanopyShade, Canopy[random.Next(Canopy.Length)], Mathf.Lerp(0.35f, 1f, height01)).As(SurfaceKind.Foliage, 1.8f);
            PrimitiveKit.Sphere(tree, "Crown", canopy, new Vector3(2.0f, 1.55f, 2.0f), Leaves(0.7f));
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f + Next() * 0.5f;
                float up = Mathf.Lerp(-0.4f, 0.35f, Next());
                var p = canopy + new Vector3(Mathf.Cos(a) * 0.8f, up, Mathf.Sin(a) * 0.8f);
                float s = Mathf.Lerp(0.95f, 1.35f, Next());
                PrimitiveKit.Sphere(tree, "Crown", p, new Vector3(s, s * 0.85f, s), Leaves(Mathf.InverseLerp(-0.4f, 0.35f, up)));
            }

            // Apples on the branches: near the crown surface, mostly on the sides and underside (so they are visible)
            int apples = 12 + random.Next(6);
            for (int i = 0; i < apples; i++)
            {
                float a = Next() * Mathf.PI * 2f;
                float y = Mathf.Lerp(-0.5f, 0.45f, Next());
                float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y)) * 1.08f;
                var p = canopy + new Vector3(Mathf.Cos(a) * ring, y * 0.82f, Mathf.Sin(a) * ring);
                PrimitiveKit.Sphere(tree, "Apple", p, Vector3.one * Mathf.Lerp(0.08f, 0.1f, Next()), Apples[random.Next(Apples.Length)]);
            }
            return tree;
        }

        // ---------- Vegetable bed ----------

        /// <summary>
        /// Raised wooden bed (long along Z): two rows of side boards, corner posts, an edge cap, soil and
        /// vegetables by variant (0 cabbage, 1 tomato, 2 lettuce, 3 carrot). The colliding body is an invisible box.
        /// </summary>
        public static Transform BuildRaisedBed(Transform root, Vector3 at, Vector2 size, int variant)
        {
            Transform bed = Group(root, "VegetableBed");
            float w = size.x, l = size.y;
            const float h = BedHeight, t = 0.06f;
            Surface boards = Wood.As(SurfaceKind.Siding, 1.1f);
            GameObject body = PrimitiveKit.Box(bed, "Body", at + Vector3.up * (h * 0.5f), new Vector3(w, h, l), boards);
            body.GetComponent<MeshRenderer>().enabled = false;

            foreach (float side in new[] { -1f, 1f })
            {
                PrimitiveKit.Box(bed, "SideBoard", at + new Vector3(side * (w * 0.5f - t * 0.5f), h * 0.5f, 0f), new Vector3(t, h, l), boards, collider: false);
                PrimitiveKit.Box(bed, "EndBoard", at + new Vector3(0f, h * 0.5f, side * (l * 0.5f - t * 0.5f)), new Vector3(w - t * 2f, h, t), boards, collider: false);
                PrimitiveKit.Box(bed, "Title", at + new Vector3(side * (w * 0.5f - t * 0.5f), h + 0.012f, 0f), new Vector3(t + 0.04f, 0.025f, l + 0.04f),
                    WoodLight.As(SurfaceKind.Planks, 0.5f), collider: false);
            }
            foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
                PrimitiveKit.Box(bed, "Post", at + new Vector3(sx * (w * 0.5f - 0.03f), (h + 0.06f) * 0.5f, sz * (l * 0.5f - 0.03f)),
                    new Vector3(0.09f, h + 0.06f, 0.09f), WoodDark.As(SurfaceKind.Planks, 0.5f), collider: false);
            PrimitiveKit.Box(bed, "Soil", at + new Vector3(0f, h - 0.04f, 0f), new Vector3(w - t * 2f, 0.05f, l - t * 2f), Soil.As(SurfaceKind.Soil), collider: false);

            var random = new System.Random(211 + variant * 31);
            float Next() => (float)random.NextDouble();
            Vector3 top = at + Vector3.up * (h - 0.015f);
            using (PrimitiveKit.DefaultKind(SurfaceKind.Plant))
            {
                switch (variant % 4)
                {
                    case 0:
                        Grid(2, 5, (p, i) => CabbagePlant(bed, p, random));
                        break;
                    case 1:
                        Grid(2, 4, (p, i) => TomatoPlant(bed, p, random));
                        break;
                    case 2:
                        Grid(3, 7, (p, i) => LettucePlant(bed, p, random));
                        break;
                    default:
                        Grid(3, 9, (p, i) => CarrotPlant(bed, p, random));
                        break;
                }
            }
            return bed;

            void Grid(int columns, int rows, System.Action<Vector3, int> plant)
            {
                float usableW = w - 0.4f, usableL = l - 0.5f;
                for (int r = 0; r < rows; r++)
                for (int c = 0; c < columns; c++)
                {
                    float x = columns == 1 ? 0f : (c / (columns - 1f) - 0.5f) * usableW;
                    float z = rows == 1 ? 0f : (r / (rows - 1f) - 0.5f) * usableL;
                    plant(top + new Vector3(x + (Next() - 0.5f) * 0.06f, 0f, z + (Next() - 0.5f) * 0.08f), r * columns + c);
                }
            }
        }

        static void CabbagePlant(Transform parent, Vector3 at, System.Random random)
        {
            float Next() => (float)random.NextDouble();
            Color color = Cabbage[random.Next(Cabbage.Length)];
            PrimitiveKit.Sphere(parent, "Cabbage", at + Vector3.up * 0.1f, new Vector3(0.24f, 0.2f, 0.24f), color);
            for (int i = 0; i < 6; i++)
            {
                Transform pivot = Pivot(parent, at + Vector3.up * 0.03f, Quaternion.Euler(-Mathf.Lerp(15f, 35f, Next()), i * 60f + Next() * 20f, 0f));
                PrimitiveKit.Leaf(pivot, "CabbageLeaf", new Vector3(0f, 0f, 0.13f), new Vector3(0.2f, 0.03f, 0.24f), Color.Lerp(color, Cabbage[0], 0.4f));
            }
        }

        static void TomatoPlant(Transform parent, Vector3 at, System.Random random)
        {
            float Next() => (float)random.NextDouble();
            PrimitiveKit.Tube(parent, "Stake", at, at + Vector3.up * 1.05f, 0.012f, Stake.As(SurfaceKind.Planks, 0.3f));
            for (int i = 0; i < 4; i++)
            {
                var p = at + new Vector3((Next() - 0.5f) * 0.18f, Mathf.Lerp(0.25f, 0.85f, i / 3f), (Next() - 0.5f) * 0.18f);
                float s = Mathf.Lerp(0.2f, 0.28f, Next());
                PrimitiveKit.Sphere(parent, "TomatoLeaf", p, new Vector3(s, s * 0.8f, s), TomatoLeaf);
            }
            int fruits = 3 + random.Next(3);
            for (int i = 0; i < fruits; i++)
            {
                float a = Next() * Mathf.PI * 2f;
                var p = at + new Vector3(Mathf.Cos(a) * 0.12f, Mathf.Lerp(0.3f, 0.8f, Next()), Mathf.Sin(a) * 0.12f);
                PrimitiveKit.Sphere(parent, "Tomato", p, Vector3.one * Mathf.Lerp(0.06f, 0.08f, Next()), Color.Lerp(Tomato, Apples[3], Next() * 0.4f));
            }
        }

        static void LettucePlant(Transform parent, Vector3 at, System.Random random)
        {
            float Next() => (float)random.NextDouble();
            Color color = Lettuce[random.Next(Lettuce.Length)];
            for (int i = 0; i < 6; i++)
            {
                Transform pivot = Pivot(parent, at, Quaternion.Euler(-Mathf.Lerp(30f, 60f, Next()), i * 60f + Next() * 25f, 0f));
                PrimitiveKit.Leaf(pivot, "Lettuce", new Vector3(0f, 0f, 0.07f), new Vector3(0.11f, 0.025f, 0.15f), color);
            }
        }

        static void CarrotPlant(Transform parent, Vector3 at, System.Random random)
        {
            float Next() => (float)random.NextDouble();
            PrimitiveKit.Cone(parent, "CarrotTip", at - Vector3.up * 0.01f, 0.025f, 0.03f, CarrotTop, 6);
            for (int i = 0; i < 5; i++)
            {
                Transform pivot = Pivot(parent, at, Quaternion.Euler(-Mathf.Lerp(55f, 80f, Next()), i * 72f + Next() * 30f, 0f));
                PrimitiveKit.Leaf(pivot, "CarrotLeaf", new Vector3(0f, 0f, 0.13f), new Vector3(0.04f, 0.01f, 0.26f), CarrotLeaf);
            }
        }

        // ---------- Wheelbarrow ----------

        /// <summary>Green metal tray, one wheel, two handles, legs; a little soil inside. The colliding body is an invisible box.</summary>
        public static Transform BuildWheelbarrow(Transform root, Vector3 at, float yaw)
        {
            Transform cart = Group(root, "Wheelbarrow");
            cart.localPosition = at;
            cart.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Surface tray = TrayGreen.As(SurfaceKind.Metal);
            Surface frame = MetalDark.As(SurfaceKind.Metal);

            GameObject body = PrimitiveKit.Box(cart, "Body", new Vector3(0f, 0.38f, -0.15f), new Vector3(0.66f, 0.76f, 1.55f), tray);
            body.GetComponent<MeshRenderer>().enabled = false;

            PrimitiveKit.Box(cart, "TrayBottom", new Vector3(0f, 0.42f, -0.05f), new Vector3(0.48f, 0.04f, 0.66f), tray, collider: false);
            foreach (float side in new[] { -1f, 1f })
                PrimitiveKit.Box(cart, "TraySide", new Vector3(side * 0.29f, 0.56f, -0.05f), new Vector3(0.04f, 0.3f, 0.82f), tray, collider: false)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, -side * 14f);
            PrimitiveKit.Box(cart, "TrayFront", new Vector3(0f, 0.57f, 0.36f), new Vector3(0.64f, 0.32f, 0.04f), tray, collider: false)
                .transform.localRotation = Quaternion.Euler(-28f, 0f, 0f);
            PrimitiveKit.Box(cart, "TrayBack", new Vector3(0f, 0.56f, -0.47f), new Vector3(0.64f, 0.3f, 0.04f), tray, collider: false)
                .transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
            PrimitiveKit.Sphere(cart, "Soil", new Vector3(0f, 0.55f, -0.05f), new Vector3(0.46f, 0.14f, 0.62f), Soil.As(SurfaceKind.Soil, 0.5f));

            PrimitiveKit.Tube(cart, "Wheel", new Vector3(-0.06f, 0.2f, 0.62f), new Vector3(0.06f, 0.2f, 0.62f), 0.2f, Rubber.As(SurfaceKind.Rubber));
            PrimitiveKit.Tube(cart, "Hub", new Vector3(-0.075f, 0.2f, 0.62f), new Vector3(0.075f, 0.2f, 0.62f), 0.07f, frame);
            foreach (float side in new[] { -1f, 1f })
            {
                var axle = new Vector3(side * 0.08f, 0.2f, 0.62f);
                var handle = new Vector3(side * 0.28f, 0.52f, -0.95f);
                PrimitiveKit.Tube(cart, "Chassis", axle, handle, 0.022f, frame);
                PrimitiveKit.Tube(cart, "Handle", handle, handle + new Vector3(side * 0.01f, 0.03f, -0.22f), 0.03f, WoodLight.As(SurfaceKind.Planks, 0.3f));
                PrimitiveKit.Tube(cart, "Leg", new Vector3(side * 0.22f, 0.4f, -0.52f), new Vector3(side * 0.25f, 0.01f, -0.58f), 0.02f, frame);
            }
            return cart;
        }

        // ---------- Rain barrel ----------

        /// <summary>Wooden barrel on a stone plinth, three iron hoops, a lid and a tap. The barrel itself collides.</summary>
        public static Transform BuildRainBarrel(Transform root, Vector3 at)
        {
            Transform barrel = Group(root, "RainBarrel");
            PrimitiveKit.Box(barrel, "Plinth", at + Vector3.up * 0.06f, new Vector3(0.72f, 0.12f, 0.72f), Palette.Hex("#A9A49B").As(SurfaceKind.Concrete), collider: false);
            PrimitiveKit.Cylinder(barrel, "Body", at + Vector3.up * 0.12f, 0.35f, 0.9f, Wood.As(SurfaceKind.Planks, 0.45f));
            foreach (float y in new[] { 0.24f, 0.57f, 0.9f })
                PrimitiveKit.Cylinder(barrel, "Hoop", at + Vector3.up * y, 0.36f, 0.035f, Hoop.As(SurfaceKind.Metal), collider: false);
            PrimitiveKit.Cylinder(barrel, "Lid", at + Vector3.up * 1.02f, 0.33f, 0.03f, WoodDark.As(SurfaceKind.Planks, 0.45f), collider: false);
            PrimitiveKit.Tube(barrel, "Tap", at + new Vector3(0f, 0.25f, -0.33f), at + new Vector3(0f, 0.25f, -0.42f), 0.018f, Palette.Hex("#C9A443").As(SurfaceKind.Metal));
            return barrel;
        }

        // ---------- Bean trellis ----------

        /// <summary>
        /// Two posts along X and a top beam; vertical strings between them, bean leaves wound around the strings, white flowers
        /// and hanging beans.
        /// </summary>
        public static Transform BuildBeanTrellis(Transform root, Vector3 at, float width, int variant)
        {
            Transform trellis = Group(root, "BeanTrellis");
            Surface post = WoodDark.As(SurfaceKind.Planks, 0.5f);
            float half = width * 0.5f;
            foreach (float side in new[] { -1f, 1f })
                PrimitiveKit.Box(trellis, "Post", at + new Vector3(side * half, 0.95f, 0f), new Vector3(0.08f, 1.9f, 0.08f), post);
            PrimitiveKit.Box(trellis, "TopBeam", at + new Vector3(0f, 1.86f, 0f), new Vector3(width + 0.12f, 0.07f, 0.07f), post, collider: false);
            PrimitiveKit.Box(trellis, "LowerBeam", at + new Vector3(0f, 0.14f, 0f), new Vector3(width, 0.04f, 0.04f), post, collider: false);

            var random = new System.Random(307 + variant * 13);
            float Next() => (float)random.NextDouble();
            for (float x = -half + 0.25f; x < half - 0.1f; x += 0.25f)
            {
                PrimitiveKit.Tube(trellis, "Rope", at + new Vector3(x, 0.14f, 0f), at + new Vector3(x, 1.83f, 0f), 0.004f, Twine.As(SurfaceKind.Fabric));
                using (PrimitiveKit.DefaultKind(SurfaceKind.Plant))
                {
                    float reach = Mathf.Lerp(1.2f, 1.8f, Next());
                    for (float y = 0.2f; y < reach; y += Mathf.Lerp(0.08f, 0.13f, Next()))
                    {
                        Transform pivot = Pivot(trellis, at + new Vector3(x, y, 0f), Quaternion.Euler(Mathf.Lerp(-30f, 30f, Next()), Next() * 360f, Mathf.Lerp(-20f, 20f, Next())));
                        PrimitiveKit.Leaf(pivot, "BeanLeaf", new Vector3(0f, 0f, 0.08f), new Vector3(0.16f, 0.02f, 0.19f), Color.Lerp(BeanLeaf, CarrotLeaf, Next()));
                        if (Next() < 0.22f)
                            PrimitiveKit.Sphere(trellis, "BeanFlower", at + new Vector3(x + (Next() - 0.5f) * 0.1f, y + 0.04f, -0.05f), Vector3.one * 0.035f, BeanFlower);
                        else if (Next() < 0.3f)
                            PrimitiveKit.Sphere(trellis, "Bean", at + new Vector3(x + (Next() - 0.5f) * 0.1f, y - 0.06f, -0.04f), new Vector3(0.022f, 0.14f, 0.022f), BeanPod);
                    }
                }
            }
            return trellis;
        }

        // ---------- Apple crate ----------

        /// <summary>Slatted wooden crate full of apples. The colliding body is an invisible box.</summary>
        public static Transform BuildAppleCrate(Transform root, Vector3 at, float yaw, int variant)
        {
            Transform crate = Group(root, "AppleCrate");
            crate.localPosition = at;
            crate.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Surface slat = WoodLight.As(SurfaceKind.Planks, 0.4f);
            const float w = 0.62f, d = 0.44f, h = 0.4f;
            GameObject body = PrimitiveKit.Box(crate, "Body", new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), slat);
            body.GetComponent<MeshRenderer>().enabled = false;
            PrimitiveKit.Box(crate, "Base", new Vector3(0f, 0.02f, 0f), new Vector3(w, 0.03f, d), slat, collider: false);
            foreach (float y in new[] { 0.1f, 0.3f })
            foreach (float side in new[] { -1f, 1f })
            {
                PrimitiveKit.Box(crate, "Slat", new Vector3(0f, y, side * (d * 0.5f - 0.012f)), new Vector3(w, 0.09f, 0.022f), slat, collider: false);
                PrimitiveKit.Box(crate, "Slat", new Vector3(side * (w * 0.5f - 0.012f), y, 0f), new Vector3(0.022f, 0.09f, d), slat, collider: false);
            }
            foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
                PrimitiveKit.Box(crate, "Corner", new Vector3(sx * (w * 0.5f - 0.02f), h * 0.5f, sz * (d * 0.5f - 0.02f)), new Vector3(0.04f, h, 0.04f),
                    WoodDark.As(SurfaceKind.Planks, 0.4f), collider: false);

            var random = new System.Random(401 + variant);
            float Next() => (float)random.NextDouble();
            for (int i = 0; i < 16; i++)
            {
                var p = new Vector3((Next() - 0.5f) * (w - 0.12f), Mathf.Lerp(0.34f, 0.42f, Next()), (Next() - 0.5f) * (d - 0.12f));
                PrimitiveKit.Sphere(crate, "Apple", p, Vector3.one * Mathf.Lerp(0.085f, 0.1f, Next()), Apples[random.Next(Apples.Length)]);
            }
            return crate;
        }

        static Transform Pivot(Transform parent, Vector3 position, Quaternion rotation)
        {
            Transform pivot = new GameObject("Axis").transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = position;
            pivot.localRotation = rotation;
            return pivot;
        }

        static Transform Group(Transform parent, string name)
        {
            Transform t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }
    }
}

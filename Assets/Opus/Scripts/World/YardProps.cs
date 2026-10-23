using System.Collections.Generic;
using Opus.Core;
using UnityEngine;

namespace Opus.World
{
    /// <summary>
    /// Garden props (after the reference): vinyl fence with pyramid caps; the house, shed and tree are Blender models (house: siding,
    /// windows, deck, railing, lattice skirt, porch; a hand-built grill); a mulched, latticed flowerbed with a white curb;
    /// a grooved birdbath with a stone bird; a compost bin with a lid and a pitchfork. Only the main bodies collide
    /// (their sizes are fixed by tests); the rest is visual and each group is merged into a static batch.
    /// </summary>
    public static class YardProps
    {
        static readonly Color FenceSeam = Palette.Hex("#CEC9C1");
        static readonly Color GrillBlack = Palette.Hex("#2B2C30");
        static readonly Color Soil = Palette.Hex("#4A3A2E");
        static readonly Color[] Stones = { Palette.Hex("#DAD6CF"), Palette.Hex("#C8C3BB"), Palette.Hex("#E4E0D9") };
        static readonly Color[] Salvia = { Palette.Hex("#7A4FB0"), Palette.Hex("#8C62C6"), Palette.Hex("#6A44A0") };
        static readonly Color[] HostaGreens = { Palette.Hex("#9CC25A"), Palette.Hex("#B5D36B"), Palette.Hex("#86AE4B") };
        static readonly Color Fern = Palette.Hex("#A9CA4E");
        static readonly Color CurbTop = Palette.Hex("#EEEBE5");
        static readonly Color[] Mulch = { Palette.Hex("#3E3027"), Palette.Hex("#5A4434"), Palette.Hex("#6B5240") };
        static readonly Color[] Marigold = { Palette.Hex("#E8902E"), Palette.Hex("#F0B23A"), Palette.Hex("#D9672A") };
        static readonly Color GrassBlade = Palette.Hex("#A8BE62");
        static readonly Color Straw = Palette.Hex("#D2BE7A");

        // ---------- Fence ----------

        static Surface Vinyl(Color color) => color.As(SurfaceKind.Vinyl);

        public static Transform BuildFence(Transform root, (Vector3 from, Vector3 to)[] segments)
        {
            Transform fence = Group(root, "Fence");
            foreach ((Vector3 from, Vector3 to) in segments)
                FenceLine(fence, from, to);
            return fence;
        }

        // ---------- Side yard props ----------

        static readonly Color StoneBath = Palette.Hex("#C9C4BC");
        static readonly Color Water = Palette.Hex("#9FB7C4");
        static readonly Color CompostWood = Palette.Hex("#8A6A4C");
        // Compost slats: one row of the lap siding pattern is exactly one slat (17 cm spacing)
        static readonly Surface Slat = CompostWood.As(SurfaceKind.Siding, 1.0625f);
        static readonly Color CompostDark = Palette.Hex("#5A4636");
        static readonly Color CompostMid = Palette.Hex("#6E5844");
        static readonly Color StoneDark = Palette.Hex("#AFA9A0");
        static readonly Color StoneLight = Palette.Hex("#DAD5CD");
        static readonly Color Moss = Palette.Hex("#7E9A4E");
        static readonly Color[] DryLeaves = { Palette.Hex("#B9873E"), Palette.Hex("#9C6B33"), Palette.Hex("#C9A24E") };
        static readonly Color[] Scraps = { Palette.Hex("#E08A3A"), Palette.Hex("#8FB44A"), Palette.Hex("#D9C25A") };
        static readonly Color ForkHandle = Palette.Hex("#A57C52");
        static readonly Color ForkMetal = Palette.Hex("#8C9097");

        /// <summary>
        /// Stone birdbath: a stepped plinth, a grooved stem (collides), a neck, a bowl with a ring rim and water in it, a small stone
        /// bird figure on the rim, moss patches on the plinth.
        /// </summary>
        public static Transform BuildBirdbath(Transform root, Vector3 at)
        {
            Transform bath = Group(root, "Birdbath");
            PrimitiveKit.Cylinder(bath, "Base", at, 0.32f, 0.06f, StoneBath.As(SurfaceKind.Stone), collider: false);
            PrimitiveKit.Cylinder(bath, "Base2", at + Vector3.up * 0.06f, 0.24f, 0.06f, StoneDark.As(SurfaceKind.Stone), collider: false);
            PrimitiveKit.Cylinder(bath, "Leg", at, 0.12f, 0.75f, StoneBath.As(SurfaceKind.Stone));
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI * 2f / 10f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                PrimitiveKit.Box(bath, "Groove", at + dir * 0.118f + Vector3.up * 0.4f, new Vector3(0.025f, 0.5f, 0.03f), StoneDark.As(SurfaceKind.Stone), -a * Mathf.Rad2Deg, collider: false);
            }
            PrimitiveKit.Cylinder(bath, "Neck", at + Vector3.up * 0.64f, 0.17f, 0.08f, StoneBath.As(SurfaceKind.Stone), collider: false);
            PrimitiveKit.Cylinder(bath, "Bowl", at + Vector3.up * 0.72f, 0.42f, 0.08f, StoneBath.As(SurfaceKind.Stone), collider: false);
            PrimitiveKit.Cylinder(bath, "Water", at + Vector3.up * 0.79f, 0.39f, 0.03f, Water.As(SurfaceKind.Glass), collider: false);
            // Rim: a ring around the bowl (in pieces so the middle stays open)
            const int rim = 20;
            for (int i = 0; i < rim; i++)
            {
                float a = i * Mathf.PI * 2f / rim;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                PrimitiveKit.Box(bath, "Nozzle", at + dir * 0.43f + Vector3.up * 0.825f, new Vector3(0.07f, 0.05f, 0.15f), StoneLight.As(SurfaceKind.Stone), -a * Mathf.Rad2Deg, collider: false);
            }
            // A stone bird sitting on the rim
            Vector3 bird = at + new Vector3(0.43f, 0.85f, 0f);
            PrimitiveKit.Sphere(bath, "BirdBody", bird + Vector3.up * 0.05f, new Vector3(0.09f, 0.08f, 0.13f), StoneLight.As(SurfaceKind.Stone));
            PrimitiveKit.Sphere(bath, "BirdHead", bird + new Vector3(0f, 0.11f, 0.06f), Vector3.one * 0.065f, StoneLight.As(SurfaceKind.Stone));
            PrimitiveKit.Cone(bath, "Beak", bird + new Vector3(0f, 0.11f, 0.09f), 0.012f, 0.035f, StoneDark.As(SurfaceKind.Stone)).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            PrimitiveKit.Box(bath, "Tail", bird + new Vector3(0f, 0.07f, -0.08f), new Vector3(0.05f, 0.012f, 0.07f), StoneLight.As(SurfaceKind.Stone), collider: false)
                .transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
            // Moss
            var random = new System.Random(17);
            for (int i = 0; i < 9; i++)
            {
                float a = (float)random.NextDouble() * Mathf.PI * 2f;
                PrimitiveKit.Sphere(bath, "Moss", at + new Vector3(Mathf.Cos(a) * 0.27f, 0.05f, Mathf.Sin(a) * 0.27f), new Vector3(0.09f, 0.03f, 0.07f), Moss.As(SurfaceKind.Plant));
            }
            return bath;
        }

        /// <summary>
        /// Slatted wooden compost bin (the colliding body is invisible), lumpy compost and scraps inside, a lid leaning at the back,
        /// a pitchfork beside it.
        /// </summary>
        public static Transform BuildCompostBin(Transform root, Vector3 at)
        {
            Transform bin = Group(root, "Compost");
            const float width = 1.6f, depth = 1.2f, height = 0.9f;
            GameObject body = PrimitiveKit.Box(bin, "Body", at + Vector3.up * (height * 0.5f), new Vector3(width, height, depth), CompostDark.As(SurfaceKind.Soil));
            body.GetComponent<MeshRenderer>().enabled = false;
            PrimitiveKit.Box(bin, "Fill", at + Vector3.up * 0.32f, new Vector3(width - 0.06f, 0.64f, depth - 0.06f), CompostDark.As(SurfaceKind.Soil), collider: false);
            for (float y = 0.08f; y < height; y += 0.17f)
            {
                PrimitiveKit.Box(bin, "Slat", at + new Vector3(0f, y, -depth * 0.5f - 0.02f), new Vector3(width + 0.04f, 0.12f, 0.03f), Slat, collider: false);
                PrimitiveKit.Box(bin, "Slat", at + new Vector3(0f, y, depth * 0.5f + 0.02f), new Vector3(width + 0.04f, 0.12f, 0.03f), Slat, collider: false);
                foreach (float side in new[] { -1f, 1f })
                    PrimitiveKit.Box(bin, "Slat", at + new Vector3(side * (width * 0.5f + 0.02f), y, 0f), new Vector3(0.03f, 0.12f, depth + 0.04f), Slat, collider: false);
            }
            foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
                PrimitiveKit.Box(bin, "Pole", at + new Vector3(sx * width * 0.5f, height * 0.5f + 0.05f, sz * depth * 0.5f), new Vector3(0.09f, height + 0.1f, 0.09f), CompostWood.As(SurfaceKind.Planks, 0.6f), collider: false);

            // Inside: lumpy compost, dry leaves and colourful scraps
            var random = new System.Random(29);
            float Next() => (float)random.NextDouble();
            for (int i = 0; i < 26; i++)
            {
                var p = at + new Vector3((Next() - 0.5f) * (width - 0.3f), Mathf.Lerp(0.66f, 0.76f, Next()), (Next() - 0.5f) * (depth - 0.3f));
                PrimitiveKit.Sphere(bin, "Clump", p, new Vector3(Mathf.Lerp(0.16f, 0.3f, Next()), 0.12f, Mathf.Lerp(0.16f, 0.3f, Next())), (Next() < 0.6f ? CompostDark : CompostMid).As(SurfaceKind.Soil, 0.5f));
            }
            for (int i = 0; i < 18; i++)
            {
                var p = at + new Vector3((Next() - 0.5f) * (width - 0.3f), 0.8f, (Next() - 0.5f) * (depth - 0.3f));
                if (Next() < 0.6f)
                    PrimitiveKit.Leaf(bin, "DryLeaf", p, new Vector3(0.07f, 0.012f, 0.1f), DryLeaves[random.Next(DryLeaves.Length)].As(SurfaceKind.Plant))
                        .transform.localRotation = Quaternion.Euler(Next() * 20f - 10f, Next() * 360f, 0f);
                else
                    PrimitiveKit.Sphere(bin, "Debris", p, new Vector3(0.06f, 0.035f, 0.05f), Scraps[random.Next(Scraps.Length)].As(SurfaceKind.Plant));
            }

            // Lid leaning at the back: slats on two beams
            Transform lid = Group(bin, "Lid");
            lid.localPosition = at + new Vector3(0f, height, depth * 0.5f + 0.05f);
            lid.localRotation = Quaternion.Euler(-72f, 0f, 0f);
            for (int i = 0; i < 5; i++)
                PrimitiveKit.Box(lid, "LidSlat", new Vector3(0f, 0.015f, 0.08f + i * 0.23f), new Vector3(width, 0.03f, 0.16f), CompostWood.As(SurfaceKind.Siding, 1.3f), collider: false);
            foreach (float side in new[] { -1f, 1f })
                PrimitiveKit.Box(lid, "LidBeam", new Vector3(side * (width * 0.5f - 0.1f), -0.02f, 0.55f), new Vector3(0.07f, 0.05f, 1.1f), CompostWood.As(SurfaceKind.Siding, 1.3f), collider: false);

            // Pitchfork leaning beside it: handle, clamp, four tines (touching the ground)
            Vector3 foot = at + new Vector3(-width * 0.5f - 0.32f, 0f, -0.25f);
            Vector3 handleTop = at + new Vector3(-width * 0.5f - 0.06f, 1.25f, -0.15f);
            Vector3 neck = foot + (handleTop - foot).normalized * 0.32f;
            PrimitiveKit.Tube(bin, "PitchforkHandle", neck, handleTop, 0.016f, ForkHandle.As(SurfaceKind.Planks, 0.4f));
            PrimitiveKit.Tube(bin, "PitchforkClamp", neck - (handleTop - foot).normalized * 0.06f, neck + (handleTop - foot).normalized * 0.02f, 0.022f, ForkMetal.As(SurfaceKind.Metal));
            Vector3 across = Vector3.Cross((handleTop - foot).normalized, Vector3.forward).normalized;
            for (int t = 0; t < 4; t++)
            {
                Vector3 offset = across * ((t - 1.5f) * 0.05f);
                PrimitiveKit.Tube(bin, "PitchforkTine", neck + offset * 0.6f, foot + offset + Vector3.up * 0.01f, 0.006f, ForkMetal.As(SurfaceKind.Metal));
            }
            return bin;
        }

        static void FenceLine(Transform parent, Vector3 from, Vector3 to)
        {
            Vector3 span = to - from;
            float length = span.magnitude;
            int panels = Mathf.Max(1, Mathf.RoundToInt(length / 2.4f));
            float panelLength = length / panels;
            float yaw = Mathf.Atan2(span.x, span.z) * Mathf.Rad2Deg;
            Vector3 along = span / length;

            for (int i = 0; i < panels; i++)
            {
                Vector3 center = from + span * ((i + 0.5f) / panels);
                PrimitiveKit.Box(parent, "Board", center + Vector3.up * 0.95f, new Vector3(0.06f, 1.8f, panelLength - 0.1f), Vinyl(Palette.FenceWhite), yaw);
                PrimitiveKit.Box(parent, "TopRail", center + Vector3.up * 1.86f, new Vector3(0.1f, 0.08f, panelLength), Vinyl(Palette.TrimWhite), yaw, collider: false);
                PrimitiveKit.Box(parent, "BottomRail", center + Vector3.up * 0.12f, new Vector3(0.09f, 0.06f, panelLength), Vinyl(Palette.TrimWhite), yaw, collider: false);
                // Board joints: thin vertical lines on the panel
                for (int s = 1; s < 8; s++)
                {
                    Vector3 at = center + along * ((s / 8f - 0.5f) * (panelLength - 0.1f)) + Vector3.up * 0.97f;
                    PrimitiveKit.Box(parent, "Seam", at, new Vector3(0.068f, 1.62f, 0.012f), FenceSeam, yaw, collider: false);
                }
            }
            for (int i = 0; i <= panels; i++)
            {
                Vector3 at = from + span * ((float)i / panels);
                PrimitiveKit.Box(parent, "Pole", at + Vector3.up * 1.0f, new Vector3(0.14f, 2.0f, 0.14f), Vinyl(Palette.TrimWhite), yaw, collider: false);
                PrimitiveKit.Box(parent, "PoleCap", at + Vector3.up * 2.03f, new Vector3(0.18f, 0.06f, 0.18f), Vinyl(Palette.TrimWhite), yaw, collider: false);
                // Pyramid cap (a four-sided cone, corners at the post's corners)
                PrimitiveKit.Cone(parent, "PoleTop", at + Vector3.up * 2.06f, 0.125f, 0.09f, Vinyl(Palette.TrimWhite), 4).transform.localRotation = Quaternion.Euler(0f, yaw + 45f, 0f);
                PrimitiveKit.Box(parent, "PoleSkirt", at + Vector3.up * 0.06f, new Vector3(0.18f, 0.12f, 0.18f), Vinyl(Palette.TrimWhite), yaw, collider: false);
            }
        }

        // ---------- House ----------

        /// <summary>The house parts are measured from this body centre; when built elsewhere the group is shifted.</summary>
        static readonly Vector3 HouseModelCenter = new Vector3(0f, 0f, 20.5f);

        /// <summary>Sizes of the house model (tools/blender/models/house.py): body, eave, deck top.</summary>
        public const float HouseWidth = 16f, HouseDepth = 10f, HouseEave = 6.25f, DeckTop = 0.5f;
        const float DeckDepth = 2.8f;
        static readonly Vector2 DeckX = new Vector2(-4.2f, 7.8f);
        static readonly Vector2 StepX = new Vector2(-0.25f, 1.46f);

        /// <summary>Door colour in the model (house.py DOOR); colour variants replace this surface.</summary>
        public static readonly Color HouseModelDoor = Palette.Hex("#94817F");

        public readonly struct HouseColors
        {
            public readonly Color Siding, Door;

            public HouseColors(string siding, string door)
            {
                Siding = Palette.Hex(siding);
                Door = Palette.Hex(door);
            }
        }

        /// <summary>
        /// House colour variants (siding and door): 0 our house, the model's own colours (light grey, rose-brown door);
        /// 1 cream with a brick-red door; 2 sage green with a blue door; 3 beige with a green door.
        /// </summary>
        public static readonly HouseColors?[] HouseVariants =
        {
            null,
            new HouseColors("#D6CAB2", "#9A5A47"),
            new HouseColors("#A3B39E", "#5E6E8A"),
            new HouseColors("#B4A493", "#4E6B4A"),
        };

        /// <summary>
        /// House: a model generated from code in Blender (tools/blender/models/house.py: lap siding, recessed windows,
        /// doors, shingle rows, gutters and downspouts, deck on the garden side, railing, lattice skirt and porch).
        /// The colliders are invisible boxes: body, deck and two steps.
        /// </summary>
        /// <param name="at">Centre of the base of the house body (the garden facade is 5 m south, the deck in front of it).</param>
        public static Transform BuildHouse(Transform root, Vector3 at, int variant = 0)
        {
            Transform house = Group(root, "House");
            house.localPosition = at - HouseModelCenter;
            Vector3 center = HouseModelCenter;
            float facade = center.z - HouseDepth * 0.5f;
            Hidden(house, "Body", center + new Vector3(0f, HouseEave * 0.5f, 0f), new Vector3(HouseWidth, HouseEave, HouseDepth));
            Hidden(house, "Veranda", new Vector3((DeckX.x + DeckX.y) * 0.5f, DeckTop * 0.5f, facade - DeckDepth * 0.5f),
                new Vector3(DeckX.y - DeckX.x, DeckTop, DeckDepth));
            // Steps: three equal risers (deck, two steps), each step 29 cm
            for (int i = 1; i <= 2; i++)
            {
                float top = DeckTop * (3 - i) / 3f;
                float front = facade - DeckDepth - 0.045f - 0.29f * i;
                Hidden(house, "Step", new Vector3((StepX.x + StepX.y) * 0.5f, top * 0.5f, front + 0.145f), new Vector3(StepX.y - StepX.x, top, 0.29f));
            }

            HouseColors? colors = HouseVariants[Mathf.Abs(variant) % HouseVariants.Length];
            Transform model = ModelLibrary.Whole("house", house, "HouseModel", colors.HasValue ? Recolor(colors.Value) : null);
            model.localPosition = center;
            // The grill is at the east end of the deck (as in the video), not blocking the glass door
            BuildGrill(house, new Vector3(7.0f, DeckTop, facade - 1.9f));
            return house;
        }

        static System.Func<Surface, Surface> Recolor(HouseColors colors) => surface =>
            surface.Kind == SurfaceKind.Siding ? new Surface(SurfaceKind.Siding, colors.Siding, surface.Scale)
            : surface.Kind == SurfaceKind.Paint && surface.Color == HouseModelDoor ? new Surface(SurfaceKind.Paint, colors.Door, surface.Scale)
            : surface;

        /// <summary>Invisible collision box (its look is in the model).</summary>
        static void Hidden(Transform parent, string name, Vector3 center, Vector3 size)
        {
            GameObject box = PrimitiveKit.Box(parent, name, center, size, Palette.TrimWhite);
            box.GetComponent<MeshRenderer>().enabled = false;
        }

        /// <summary>Clips the segment a–b to the minX..maxX strip (flowerbed lattice).</summary>
        static bool Clip(ref Vector2 a, ref Vector2 b, float minX, float maxX)
        {
            if ((a.x < minX && b.x < minX) || (a.x > maxX && b.x > maxX))
                return false;
            Vector2 d = b - a;
            if (Mathf.Abs(d.x) < 1e-5f) return true;
            if (a.x < minX) a += d * ((minX - a.x) / d.x);
            if (a.x > maxX) a += d * ((maxX - a.x) / d.x);
            d = b - a;
            if (b.x < minX) b = a + d * ((minX - a.x) / d.x);
            if (b.x > maxX) b = a + d * ((maxX - a.x) / d.x);
            return (b - a).sqrMagnitude > 0.01f;
        }

        static void BuildGrill(Transform parent, Vector3 deckPoint)
        {
            Vector3 body = deckPoint + Vector3.up * 0.85f;
            PrimitiveKit.Box(parent, "Grill", body, new Vector3(0.62f, 0.3f, 0.42f), GrillBlack.As(SurfaceKind.Metal), collider: false);
            PrimitiveKit.Sphere(parent, "GrillLid", body + Vector3.up * 0.15f, new Vector3(0.6f, 0.32f, 0.4f), GrillBlack.As(SurfaceKind.Metal));
            PrimitiveKit.Box(parent, "Shelf", body + new Vector3(0.45f, 0f, 0f), new Vector3(0.3f, 0.03f, 0.36f), GrillBlack.As(SurfaceKind.Metal), collider: false);
            foreach (float x in new[] { -0.26f, 0.26f })
            foreach (float z in new[] { -0.16f, 0.16f })
                PrimitiveKit.Tube(parent, "Leg", deckPoint + new Vector3(x, 0f, z), deckPoint + new Vector3(x, 0.72f, z), 0.018f, GrillBlack.As(SurfaceKind.Metal));
        }

        // ---------- Shed ----------

        /// <summary>
        /// Garden shed: a model generated from code in Blender (tools/blender/models/shed.py: lap siding boards,
        /// corner and rake boards, shingle rows, a framed double door, hinges, a recessed window). The colliding body
        /// is an invisible box (sizes fixed by tests). The door faces -Z (the garden), the window -X.
        /// </summary>
        public static Transform BuildShed(Transform root, Vector3 at)
        {
            Transform shed = Group(root, "Shed");
            const float width = 2.6f, height = 2.2f, depth = 2.6f;
            GameObject body = PrimitiveKit.Box(shed, "Body", at + new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth), Palette.ShedBlue.As(SurfaceKind.Siding));
            body.GetComponent<MeshRenderer>().enabled = false;
            Transform model = ModelLibrary.Whole("shed", shed, "ShedModel");
            model.localPosition = at;
            return shed;
        }

        // ---------- Flowerbed ----------

        public static Transform BuildFlowerbed(Transform root, Vector3 center, Vector2 size)
        {
            Transform bed = Group(root, "Flowerbed");
            // Collision edge (the measured tests rely on this box); its look is the curb
            GameObject edge = PrimitiveKit.Box(bed, "Edge", center + new Vector3(0f, 0.14f, 0f), new Vector3(size.x + 0.4f, 0.28f, size.y + 0.4f), Palette.TrimWhite);
            edge.GetComponent<MeshRenderer>().enabled = false;
            PrimitiveKit.Box(bed, "Soil", center + new Vector3(0f, 0.15f, 0f), new Vector3(size.x + 0.1f, 0.3f, size.y + 0.1f), Soil.As(SurfaceKind.Soil), collider: false);

            var random = new System.Random(7);
            float Next() => (float)random.NextDouble();

            // White curb: short blocks along a rounded rectangle and a top cap
            List<Vector2> path = RoundedRectPath(size.x * 0.5f + 0.14f, size.y * 0.5f + 0.14f, 0.35f, 0.24f);
            for (int i = 0; i < path.Count; i++)
            {
                Vector2 p = path[i], q = path[(i + 1) % path.Count];
                Vector2 mid = (p + q) * 0.5f, dir = q - p;
                float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                float length = dir.magnitude + 0.02f;
                PrimitiveKit.Box(bed, "Curb", center + new Vector3(mid.x, 0.13f, mid.y), new Vector3(0.14f, 0.26f, length), Stones[random.Next(Stones.Length)].As(SurfaceKind.Concrete), yaw, collider: false);
                PrimitiveKit.Box(bed, "CurbTop", center + new Vector3(mid.x, 0.27f, mid.y), new Vector3(0.17f, 0.03f, length), CurbTop.As(SurfaceKind.Concrete), yaw, collider: false);
            }

            // Mulch: dark and light brown flat lumps
            for (int i = 0; i < 70; i++)
            {
                var p = new Vector3((Next() - 0.5f) * size.x, 0.3f, (Next() - 0.5f) * size.y);
                PrimitiveKit.Sphere(bed, "Mulch", center + p, new Vector3(Mathf.Lerp(0.1f, 0.18f, Next()), 0.03f, Mathf.Lerp(0.07f, 0.12f, Next())), Mulch[random.Next(Mulch.Length)].As(SurfaceKind.Soil, 0.5f))
                    .transform.localRotation = Quaternion.Euler(0f, Next() * 180f, 0f);
            }

            // Plants: purple spikes, hostas, ferns, marigolds, ornamental grasses (in order, with some randomness)
            int plants = 18;
            for (int i = 0; i < plants; i++)
            {
                float u = (i + 0.5f + (Next() - 0.5f) * 0.6f) / plants;
                var p = new Vector3(center.x - size.x * 0.5f + u * size.x, 0.3f, center.z + (Next() - 0.5f) * (size.y - 0.5f));
                using (PrimitiveKit.DefaultKind(SurfaceKind.Plant))
                {
                    switch (i % 5)
                    {
                        case 0: SalviaClump(bed, p, random); break;
                        case 1: Hosta(bed, p, random); break;
                        case 2: FernClump(bed, p, random); break;
                        case 3: Marigolds(bed, p, random); break;
                        default: OrnamentalGrass(bed, p, random); break;
                    }
                }
            }

            // A white lattice at the back with ivy climbing on it (leaves + purple flowers)
            var latticeCenter = new Vector3(center.x, 1.0f, center.z + size.y * 0.5f + 0.35f);
            LatticePanel(bed, latticeCenter, size.x * 0.9f, 1.5f);
            for (int i = 0; i < 60; i++)
            {
                var p = latticeCenter + new Vector3((Next() - 0.5f) * size.x * 0.85f, (Next() - 0.5f) * 1.3f * Mathf.Sqrt(Next()), -0.04f);
                if (Next() < 0.82f)
                    PrimitiveKit.Leaf(bed, "Ivy", p, new Vector3(0.07f, 0.012f, 0.1f), HostaGreens[random.Next(HostaGreens.Length)].As(SurfaceKind.Plant))
                        .transform.localRotation = Quaternion.Euler(-70f + Next() * 30f, Next() * 360f, 0f);
                else
                    PrimitiveKit.Sphere(bed, "IvyFlower", p + new Vector3(0f, 0f, -0.02f), Vector3.one * 0.06f, Salvia[random.Next(Salvia.Length)].As(SurfaceKind.Plant));
            }
            return bed;
        }

        /// <summary>Points about <paramref name="step"/> apart around a rounded rectangle (XZ).</summary>
        static List<Vector2> RoundedRectPath(float hx, float hz, float r, float step)
        {
            var points = new List<Vector2>();
            void Line(Vector2 a, Vector2 b)
            {
                int n = Mathf.Max(1, Mathf.RoundToInt(Vector2.Distance(a, b) / step));
                for (int i = 0; i < n; i++) points.Add(Vector2.Lerp(a, b, i / (float)n));
            }
            void Arc(Vector2 c, float from)
            {
                const int n = 3;
                for (int i = 0; i < n; i++)
                {
                    float a = (from + 90f * i / n) * Mathf.Deg2Rad;
                    points.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                }
            }
            Line(new Vector2(-hx + r, -hz), new Vector2(hx - r, -hz));
            Arc(new Vector2(hx - r, -hz + r), -90f);
            Line(new Vector2(hx, -hz + r), new Vector2(hx, hz - r));
            Arc(new Vector2(hx - r, hz - r), 0f);
            Line(new Vector2(hx - r, hz), new Vector2(-hx + r, hz));
            Arc(new Vector2(-hx + r, hz - r), 90f);
            Line(new Vector2(-hx, hz - r), new Vector2(-hx, -hz + r));
            Arc(new Vector2(-hx + r, -hz + r), 180f);
            return points;
        }

        /// <summary>Framed white garden lattice (diagonal slats), in the XY plane.</summary>
        static void LatticePanel(Transform parent, Vector3 center, float width, float height)
        {
            float hw = width * 0.5f, hh = height * 0.5f;
            PrimitiveKit.Box(parent, "LatticeFrame", center + new Vector3(0f, hh, 0f), new Vector3(width + 0.08f, 0.06f, 0.05f), Palette.TrimWhite, collider: false);
            PrimitiveKit.Box(parent, "LatticeFrame", center + new Vector3(0f, -hh, 0f), new Vector3(width + 0.08f, 0.06f, 0.05f), Palette.TrimWhite, collider: false);
            foreach (float side in new[] { -1f, 1f })
                PrimitiveKit.Box(parent, "LatticePost", center + new Vector3(side * (hw + 0.04f), -0.25f, 0f), new Vector3(0.07f, height + 0.5f, 0.07f), Palette.TrimWhite, collider: false);
            const float spacing = 0.3f;
            for (float x0 = -hw - height; x0 < hw; x0 += spacing)
            {
                foreach (float dir in new[] { 1f, -1f })
                {
                    // (x0, -hh) → (x0 + height, hh) and its mirror; clip to the rectangle
                    var a = new Vector2(dir > 0 ? x0 : x0 + height, -hh);
                    var b = new Vector2(dir > 0 ? x0 + height : x0, hh);
                    if (!Clip(ref a, ref b, -hw, hw))
                        continue;
                    PrimitiveKit.Tube(parent, "LatticeSlat", center + new Vector3(a.x, a.y, 0f), center + new Vector3(b.x, b.y, 0f), 0.012f, Palette.TrimWhite);
                }
            }
        }

        static void Marigolds(Transform parent, Vector3 at, System.Random random)
        {
            float Next() => (float)random.NextDouble();
            int stems = 6 + random.Next(4);
            for (int s = 0; s < stems; s++)
            {
                var root = at + new Vector3((Next() - 0.5f) * 0.4f, 0f, (Next() - 0.5f) * 0.3f);
                float height = Mathf.Lerp(0.15f, 0.3f, Next());
                PrimitiveKit.Tube(parent, "Stem", root, root + Vector3.up * height, 0.008f, Palette.Hosta);
                PrimitiveKit.Sphere(parent, "Velvet", root + Vector3.up * height, new Vector3(0.09f, 0.05f, 0.09f), Marigold[random.Next(Marigold.Length)]);
                PrimitiveKit.Leaf(parent, "Leaf", root + Vector3.up * height * 0.4f, new Vector3(0.05f, 0.01f, 0.12f), HostaGreens[2])
                    .transform.localRotation = Quaternion.Euler(-30f, Next() * 360f, 0f);
            }
        }

        static void OrnamentalGrass(Transform parent, Vector3 at, System.Random random)
        {
            float Next() => (float)random.NextDouble();
            int blades = 16 + random.Next(8);
            for (int b = 0; b < blades; b++)
            {
                Transform pivot = new GameObject("WeedAxis").transform;
                pivot.SetParent(parent, false);
                pivot.localPosition = at;
                pivot.localRotation = Quaternion.Euler(-Mathf.Lerp(55f, 85f, Next()), Next() * 360f, 0f);
                float length = Mathf.Lerp(0.45f, 0.75f, Next());
                PrimitiveKit.Leaf(pivot, "OrnamentalGrass", new Vector3(0f, 0f, length * 0.5f), new Vector3(0.035f, 0.01f, length), Next() < 0.6f ? GrassBlade : Straw);
            }
        }

        static void SalviaClump(Transform parent, Vector3 at, System.Random random)
        {
            float Next() => (float)random.NextDouble();
            int stems = 7 + random.Next(5);
            for (int s = 0; s < stems; s++)
            {
                var root = at + new Vector3((Next() - 0.5f) * 0.45f, 0f, (Next() - 0.5f) * 0.35f);
                float height = Mathf.Lerp(0.35f, 0.6f, Next());
                var lean = new Vector3((Next() - 0.5f) * 0.12f, 0f, (Next() - 0.5f) * 0.12f);
                PrimitiveKit.Tube(parent, "Stem", root, root + lean + Vector3.up * height * 0.55f, 0.008f, Palette.Hosta);
                Color color = Salvia[random.Next(Salvia.Length)];
                for (int k = 0; k < 5; k++)
                {
                    float t = 0.45f + k * 0.12f;
                    float r = Mathf.Lerp(0.07f, 0.035f, k / 4f);
                    PrimitiveKit.Sphere(parent, "Spike", root + lean * t * 1.8f + Vector3.up * height * t, new Vector3(r, r * 1.4f, r), color);
                }
            }
        }

        static void Hosta(Transform parent, Vector3 at, System.Random random)
        {
            float Next() => (float)random.NextDouble();
            int leaves = 9 + random.Next(4);
            for (int l = 0; l < leaves; l++)
            {
                Transform pivot = new GameObject("HostaAxis").transform;
                pivot.SetParent(parent, false);
                pivot.localPosition = at + Vector3.up * 0.02f;
                pivot.localRotation = Quaternion.Euler(-Mathf.Lerp(25f, 55f, Next()), l * 360f / leaves + Next() * 20f, 0f);
                float s = Mathf.Lerp(0.22f, 0.32f, Next());
                PrimitiveKit.Leaf(pivot, "Leaf", new Vector3(0f, 0f, s * 0.5f), new Vector3(s * 0.7f, s * 0.12f, s), HostaGreens[random.Next(HostaGreens.Length)]);
            }
        }

        static void FernClump(Transform parent, Vector3 at, System.Random random)
        {
            float Next() => (float)random.NextDouble();
            int fronds = 8 + random.Next(4);
            for (int f = 0; f < fronds; f++)
            {
                Transform pivot = new GameObject("FernAxis").transform;
                pivot.SetParent(parent, false);
                pivot.localPosition = at;
                pivot.localRotation = Quaternion.Euler(-Mathf.Lerp(35f, 65f, Next()), f * 360f / fronds + Next() * 25f, 0f);
                float length = Mathf.Lerp(0.35f, 0.5f, Next());
                PrimitiveKit.Leaf(pivot, "Fern", new Vector3(0f, 0f, length * 0.5f), new Vector3(length * 0.28f, 0.02f, length), Fern);
            }
        }

        // ---------- Tree ----------

        /// <summary>
        /// Garden tree with a swing: a model generated from code in Blender (tools/blender/models/tree.py: buttressed trunk and
        /// roots, branches, leaf clusters, a tire swing on a rope wound around a branch). The colliding trunk is an invisible cylinder
        /// (sizes fixed by tests).
        /// </summary>
        public static Transform BuildTree(Transform root, Vector3 at)
        {
            Transform tree = Group(root, "Tree");
            GameObject trunkCollider = PrimitiveKit.Cylinder(tree, "Body", at, 0.35f, 3.6f, Palette.TrunkBrown);
            trunkCollider.GetComponent<MeshRenderer>().enabled = false;
            Transform model = ModelLibrary.Whole("tree", tree, "TreeModel");
            model.localPosition = at;
            // Rope and tire: a textured model (tools/blender/models/tire_swing.py), measured from the tree's origin
            Transform swing = ModelLibrary.Whole("tire_swing", tree, "TireSwing");
            swing.localPosition = at;
            return tree;
        }

        static Transform Group(Transform parent, string name)
        {
            Transform t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }
    }
}

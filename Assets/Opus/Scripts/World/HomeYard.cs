using System.Collections.Generic;
using Opus.Lawn;
using UnityEngine;

namespace Opus.World
{
    /// <summary>
    /// Definition of the home garden. Sizes in metres; the house is to the north (+Z). Area 1 "Back yard": X -18..18, Z -15..15. Area 2
    /// "Side yard": X 18..30, Z -15..15; the fence between them (X = 18, Z = 0) has a gate. Area 3 "Orchard": X -30..30,
    /// Z -29..-15 (two rows of apple trees); its gate is in the side yard's south fence. Area 4 "Vegetable garden": X -30..-18,
    /// Z -15..15 (raised beds with narrow grass paths between them); its gate is in the orchard's north fence.
    /// Unlock order 1 → 2 → 3 → 4. Seeds and weeds of new areas are appended to the end of the lists (so old saves migrate).
    /// </summary>
    public static class HomeYard
    {
        /// <summary>Save key from the Turkish build ("house"), kept so existing saves still load.</summary>
        public const string Id = "ev";
        /// <summary>Raised when areas, patches, weeds or fences change (saves migrate area by area). 2: orchard and vegetable garden (U14).</summary>
        public const int Version = 2;

        public const float HalfWidth = 18f;
        public const float HalfDepth = 15f;
        public const float AnnexMaxX = 30f;
        public const float WestMinX = -30f;
        public const float OrchardMinZ = -29f;
        /// <summary>Gate: hinge (south end of the opening) and opening width.</summary>
        public static readonly Vector3 GateHinge = new Vector3(HalfWidth, 0f, -0.8f);
        public const float GateWidth = 1.6f;
        /// <summary>From the side yard to the orchard (in the south fence, hinge at the west end).</summary>
        public static readonly Vector3 OrchardGateHinge = new Vector3(23.2f, 0f, -HalfDepth);
        /// <summary>From the orchard to the vegetable garden (in the vegetable garden's south fence).</summary>
        public static readonly Vector3 VegetableGateHinge = new Vector3(-24.8f, 0f, -HalfDepth);

        /// <summary>Fruit trees: two rows, the rows staggered (the mower passes between them).</summary>
        public static readonly Vector3[] FruitTrees =
        {
            new Vector3(-26f, 0f, -19.8f), new Vector3(-19f, 0f, -19.8f), new Vector3(-12f, 0f, -19.8f), new Vector3(-5f, 0f, -19.8f),
            new Vector3(2f, 0f, -19.8f), new Vector3(9f, 0f, -19.8f), new Vector3(16f, 0f, -19.8f),
            new Vector3(-22.5f, 0f, -25.2f), new Vector3(-15.5f, 0f, -25.2f), new Vector3(-8.5f, 0f, -25.2f), new Vector3(-1.5f, 0f, -25.2f),
            new Vector3(5.5f, 0f, -25.2f), new Vector3(12.5f, 0f, -25.2f), new Vector3(19.5f, 0f, -25.2f), new Vector3(26.5f, 0f, -25.2f),
        };

        /// <summary>Vegetable beds (centre; 1.4 × 5 m, along Z): three columns, two rows, 1.8 m grass paths between them.</summary>
        public static readonly Vector3[] RaisedBeds =
        {
            new Vector3(-27.2f, 0f, -3.5f), new Vector3(-24f, 0f, -3.5f), new Vector3(-20.8f, 0f, -3.5f),
            new Vector3(-27.2f, 0f, 4.5f), new Vector3(-24f, 0f, 4.5f), new Vector3(-20.8f, 0f, 4.5f),
        };
        public static readonly Vector2 RaisedBedSize = new Vector2(1.4f, 5f);

        public static readonly Vector3 HouseCenter = new Vector3(0f, 0f, 20.5f);
        public static readonly Vector3 ShedPosition = new Vector3(-13f, 0f, 10f);
        public static readonly Vector3 FlowerbedCenter = new Vector3(12.5f, 0f, 13.6f);
        public static readonly Vector2 FlowerbedSize = new Vector2(8f, 1.4f);
        public static readonly Vector3 TreePosition = new Vector3(12f, 0f, -5f);
        public static readonly Vector3 BirdbathPosition = new Vector3(24f, 0f, -3.5f);
        public static readonly Vector3 CompostPosition = new Vector3(27.6f, 0f, 12.6f);

        public static YardDefinition Create()
        {
            const float x = HalfWidth, z = HalfDepth;
            return new YardDefinition
            {
                Id = Id,
                Version = Version,
                CellSize = 0.1f,
                FirstArea = 1,
                Areas = new[]
                {
                    new LawnArea(1, "Back yard", -HalfWidth, -HalfDepth, HalfWidth, HalfDepth),
                    new LawnArea(2, "Side yard", HalfWidth, -HalfDepth, AnnexMaxX, HalfDepth),
                    new LawnArea(3, "Orchard", WestMinX, OrchardMinZ, AnnexMaxX, -HalfDepth),
                    new LawnArea(4, "Vegetable garden", WestMinX, -HalfDepth, -HalfWidth, HalfDepth),
                },
                Gates = new[]
                {
                    new GateDef(1, 2, GateHinge, GateWidth, FenceAxis.AlongZ),
                    new GateDef(2, 3, OrchardGateHinge, GateWidth, FenceAxis.AlongX),
                    new GateDef(3, 4, VegetableGateHinge, GateWidth, FenceAxis.AlongX),
                },

                // Variety order: GrassVariety.All (0 mustard, 1 golden, 2 olive, 3 gray-blue, 4 fresh, 5 straw)
                PatchSeeds = new[]
                {
                    new PatchSeed(-13f, -11f, 0), new PatchSeed(-5f, -12f, 3), new PatchSeed(4f, -11f, 1), new PatchSeed(13f, -11f, 2),
                    new PatchSeed(-14f, -3f, 4), new PatchSeed(-7f, -4f, 0), new PatchSeed(1f, -3f, 2), new PatchSeed(9f, -2f, 3),
                    new PatchSeed(15f, 3f, 1), new PatchSeed(-12f, 6f, 4, 1f), new PatchSeed(-4f, 5f, 1), new PatchSeed(4f, 6f, 0),
                    new PatchSeed(11f, 8f, 5), new PatchSeed(-1f, 12f, 3), new PatchSeed(-15f, 12f, 2), new PatchSeed(7f, 13f, 1),
                    // Side yard
                    new PatchSeed(21.5f, -11f, 2, area: 2), new PatchSeed(27.5f, -8.5f, 0, area: 2), new PatchSeed(21.5f, -1.5f, 4, area: 2),
                    new PatchSeed(27.5f, 2.5f, 1, area: 2), new PatchSeed(21.5f, 8f, 3, area: 2), new PatchSeed(27f, 12f, 5, area: 2),
                    // Orchard: long, wavy meadow (straw, golden, mustard)
                    new PatchSeed(-26f, -21f, 5, area: 3), new PatchSeed(-18f, -25f, 1, area: 3), new PatchSeed(-10f, -19.5f, 0, area: 3),
                    new PatchSeed(-3f, -25f, 5, area: 3), new PatchSeed(4f, -20f, 1, area: 3), new PatchSeed(11f, -25.5f, 2, area: 3),
                    new PatchSeed(18f, -20f, 5, area: 3), new PatchSeed(26f, -23f, 0, area: 3),
                    // Vegetable garden: fresh grass between the beds; the west path starts dry
                    new PatchSeed(-24f, -10.5f, 4, area: 4), new PatchSeed(-28.6f, 1f, 2, 1f, area: 4), new PatchSeed(-22.4f, 1f, 3, area: 4),
                    new PatchSeed(-19.3f, 5f, 1, area: 4), new PatchSeed(-24f, 11f, 4, area: 4),
                },
                PatchInfos = new[]
                {
                    new PatchInfo("Southwest corner", "#C94F6D"), new PatchInfo("South fence", "#4FA3A5"), new PatchInfo("South lawn", "#E0853A"), new PatchInfo("Southeast corner", "#8E6CB8"),
                    new PatchInfo("West strip", "#5C8FC9"), new PatchInfo("West lawn", "#D9A93A"), new PatchInfo("Middle lawn", "#9AA543"), new PatchInfo("Tree front", "#B85C3A"),
                    new PatchInfo("East fence", "#4FA3A5"), new PatchInfo("Shed front", "#C94F6D"), new PatchInfo("Middle of the yard", "#8E6CB8"), new PatchInfo("North lawn", "#5C8FC9"),
                    new PatchInfo("East lawn", "#E0853A"), new PatchInfo("Veranda front", "#9AA543"), new PatchInfo("Behind the shed", "#D9A93A"), new PatchInfo("Flowerbed edge", "#B85C3A"),
                    new PatchInfo("Side south corner", "#5C8FC9"), new PatchInfo("Side south fence", "#C94F6D"), new PatchInfo("Gate front", "#E0853A"),
                    new PatchInfo("Around the birdbath", "#4FA3A5"), new PatchInfo("Side strip", "#9AA543"), new PatchInfo("Compost front", "#8E6CB8"),
                    new PatchInfo("West meadow", "#D9A93A"), new PatchInfo("Southwest trees", "#4FA3A5"), new PatchInfo("North row", "#C94F6D"),
                    new PatchInfo("South row", "#5C8FC9"), new PatchInfo("Middle trees", "#E0853A"), new PatchInfo("Apple path", "#8E6CB8"),
                    new PatchInfo("East trees", "#9AA543"), new PatchInfo("Crate corner", "#B85C3A"),
                    new PatchInfo("Vegetable entrance", "#5C8FC9"), new PatchInfo("West path", "#D9A93A"), new PatchInfo("Between the beds", "#C94F6D"),
                    new PatchInfo("East path", "#4FA3A5"), new PatchInfo("Bean trellis", "#9AA543"),
                },
                DrySpots = new[]
                {
                    new DrySpot(5f, -6f, 1.2f), new DrySpot(-3f, 0f, 1.0f), new DrySpot(10f, 4f, 1.4f), new DrySpot(-8f, 10f, 0.9f),
                    new DrySpot(25.5f, -4.5f, 1.2f),
                    // Orchard
                    new DrySpot(0.5f, -22.5f, 1.3f), new DrySpot(22f, -27f, 1.0f),
                    // Vegetable garden (on the paths; the ground under the beds has no grass anyway)
                    new DrySpot(-24f, -7.6f, 1.0f), new DrySpot(-19.2f, -1f, 0.9f), new DrySpot(-25.6f, 9.6f, 0.9f),
                },
                // Weeds (most at the fence/flowerbed/shed/tree bases the mower cannot reach, a few in the open) and ivy
                // around the base of the shed. Inner faces of the main area's fences: |x| = 17.97, z = -14.97.
                WeedSpots = new[]
                {
                    new WeedSpot(-17.4f, -14.4f, 1.1f), new WeedSpot(-6f, -14.55f), new WeedSpot(6.5f, -14.55f, 0.9f),
                    new WeedSpot(17.5f, -9f), new WeedSpot(17.55f, 5f, 1.15f), new WeedSpot(-17.5f, 2f, 0.95f),
                    new WeedSpot(-17.45f, -8f), new WeedSpot(10f, 12.3f), new WeedSpot(14.5f, 12.3f, 0.9f),
                    new WeedSpot(-11.25f, 8.45f, 0.9f), new WeedSpot(12.45f, -5.5f), new WeedSpot(3f, -2f, 1.2f),
                    new WeedSpot(-5f, 7f),
                    // Ivy at the base of the shed (body x -14.5..-11.5, z 8.7..11.3)
                    new WeedSpot(-12.3f, 8.55f, 0.75f, 0.22f), new WeedSpot(-11.35f, 9.9f, 0.22f, 0.8f),
                    new WeedSpot(-14.0f, 8.55f, 0.45f, 0.2f),
                    // Side yard
                    new WeedSpot(29.5f, -14.5f, 1.05f), new WeedSpot(18.5f, 9f), new WeedSpot(29.55f, 6f, 0.95f),
                    new WeedSpot(24.6f, -2.5f, 0.9f), new WeedSpot(27f, 11.4f, 0.6f, 0.2f),
                    // Orchard: at the tree bases and along the fences, ivy on the south fence
                    new WeedSpot(-29.5f, -28.5f, 1.0f), new WeedSpot(-11.55f, -19.6f, 0.9f), new WeedSpot(5.95f, -25f),
                    new WeedSpot(29.5f, -22f, 1.05f), new WeedSpot(10f, -28.5f), new WeedSpot(-20f, -15.5f, 0.9f),
                    new WeedSpot(-4f, -28.75f, 0.9f, 0.2f),
                    // Vegetable garden: at the ends of and between the beds, at the fence bases; ivy beside one bed and at the trellis base
                    new WeedSpot(-29.5f, -14.5f), new WeedSpot(-18.45f, -8f, 0.95f), new WeedSpot(-24f, 0.5f, 1.1f),
                    new WeedSpot(-27.2f, -6.4f), new WeedSpot(-20.8f, 7.4f, 0.9f), new WeedSpot(-25.6f, 3f), new WeedSpot(-29.5f, 9f),
                    new WeedSpot(-21.75f, 4.5f, 0.18f, 1.6f), new WeedSpot(-24f, 13.35f, 1.2f, 0.2f),
                },
                FenceLines = new[]
                {
                    // Back yard (cut while the gate opening in the east fence is built; the house gap to the north)
                    (new Vector3(-x, 0f, -z), new Vector3(x, 0f, -z)),
                    (new Vector3(x, 0f, -z), new Vector3(x, 0f, z)),
                    (new Vector3(-x, 0f, -z), new Vector3(-x, 0f, z)),
                    (new Vector3(-x, 0f, z), new Vector3(-8.5f, 0f, z)),
                    (new Vector3(8.5f, 0f, z), new Vector3(x, 0f, z)),
                    // Side yard
                    (new Vector3(x, 0f, -z), new Vector3(AnnexMaxX, 0f, -z)),
                    (new Vector3(AnnexMaxX, 0f, -z), new Vector3(AnnexMaxX, 0f, z)),
                    (new Vector3(x, 0f, z), new Vector3(AnnexMaxX, 0f, z)),
                    // Vegetable garden (its east is the back yard's west fence; the gate in its south leads to the orchard)
                    (new Vector3(WestMinX, 0f, -z), new Vector3(WestMinX, 0f, z)),
                    (new Vector3(WestMinX, 0f, z), new Vector3(-x, 0f, z)),
                    (new Vector3(WestMinX, 0f, -z), new Vector3(-x, 0f, -z)),
                    // Orchard (its north is the other areas' south fences)
                    (new Vector3(WestMinX, 0f, OrchardMinZ), new Vector3(AnnexMaxX, 0f, OrchardMinZ)),
                    (new Vector3(WestMinX, 0f, OrchardMinZ), new Vector3(WestMinX, 0f, -z)),
                    (new Vector3(AnnexMaxX, 0f, OrchardMinZ), new Vector3(AnnexMaxX, 0f, -z)),
                },
                Props = Props(),
                SpawnFeet = new Vector3(0f, 0.05f, -12f),
                SpawnYaw = 0f,
            };
        }

        static PropPlacement[] Props()
        {
            var props = new List<PropPlacement>
            {
                new PropPlacement(PropKind.House, HouseCenter),
                new PropPlacement(PropKind.Shed, ShedPosition),
                new PropPlacement(PropKind.Flowerbed, FlowerbedCenter, FlowerbedSize),
                new PropPlacement(PropKind.Tree, TreePosition),
                new PropPlacement(PropKind.Birdbath, BirdbathPosition),
                new PropPlacement(PropKind.Compost, CompostPosition),
            };
            for (int i = 0; i < FruitTrees.Length; i++)
                props.Add(new PropPlacement(PropKind.FruitTree, FruitTrees[i], variant: i));
            props.Add(new PropPlacement(PropKind.AppleCrate, new Vector3(27.6f, 0f, -17.3f), yaw: 15f));
            for (int i = 0; i < RaisedBeds.Length; i++)
                props.Add(new PropPlacement(PropKind.RaisedBed, RaisedBeds[i], RaisedBedSize, variant: i));
            props.Add(new PropPlacement(PropKind.BeanTrellis, new Vector3(-24f, 0f, 13.8f), new Vector2(8f, 0f)));
            props.Add(new PropPlacement(PropKind.RainBarrel, new Vector3(-29.1f, 0f, 11.8f)));
            props.Add(new PropPlacement(PropKind.Wheelbarrow, new Vector3(-20.4f, 0f, -11.4f), yaw: -35f));
            return props.ToArray();
        }

        /// <summary>
        /// The definition reduced to the given areas only (order preserved). The first version of the garden (saves up to
        /// U13: areas 1 and 2) equals <c>Restrict(Create(), 1, 2)</c>; old unstamped saves migrate this way.
        /// </summary>
        public static YardDefinition Restrict(YardDefinition source, params int[] areaIds)
        {
            var keep = new HashSet<int>(areaIds);
            var seeds = new List<PatchSeed>();
            var infos = new List<PatchInfo>();
            for (int i = 0; i < source.PatchSeeds.Length; i++)
            {
                if (!keep.Contains(source.PatchSeeds[i].Area)) continue;
                seeds.Add(source.PatchSeeds[i]);
                if (i < source.PatchInfos.Length) infos.Add(source.PatchInfos[i]);
            }
            var weeds = new List<WeedSpot>();
            foreach (WeedSpot spot in source.WeedSpots)
                if (source.AreaAt(spot.Position) is LawnArea area && keep.Contains(area.Id))
                    weeds.Add(spot);
            var areas = new List<LawnArea>();
            foreach (LawnArea area in source.Areas)
                if (keep.Contains(area.Id)) areas.Add(area);
            var gates = new List<GateDef>();
            foreach (GateDef gate in source.Gates)
                if (keep.Contains(gate.From) && keep.Contains(gate.To)) gates.Add(gate);
            return new YardDefinition
            {
                Id = source.Id,
                Version = 1,
                CellSize = source.CellSize,
                FirstArea = source.FirstArea,
                Areas = areas.ToArray(),
                Gates = gates.ToArray(),
                PatchSeeds = seeds.ToArray(),
                PatchInfos = infos.ToArray(),
                DrySpots = source.DrySpots,
                WeedSpots = weeds.ToArray(),
                SpawnFeet = source.SpawnFeet,
                SpawnYaw = source.SpawnYaw,
            };
        }

        /// <summary>The garden up to U13 (areas 1 and 2): geometry of old unstamped saves.</summary>
        public static YardDefinition FirstVersion() => Restrict(Create(), 1, 2);
    }
}

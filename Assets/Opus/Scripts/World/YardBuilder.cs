using System.Collections.Generic;
using Opus.Core;
using Opus.Lawn;
using UnityEngine;

namespace Opus.World
{
    /// <summary>
    /// Builds the active garden definition (<see cref="YardDefinition"/>; by default <see cref="HomeYard"/>) into the scene and serves it
    /// to the rest of the game: areas, lawn grid, patches, dry spots, weeds, built gates. The models are
    /// in YardProps, the scenery outside the garden in Visual.PaintedBackdrop.
    /// </summary>
    public static class YardBuilder
    {
        static readonly List<Gate> gates = new List<Gate>();

        /// <summary>The active garden definition.</summary>
        public static YardDefinition Definition { get; private set; } = HomeYard.Create();

        /// <summary>
        /// Switches to another definition (tests, client gardens). Built gates are forgotten; the scene must be rebuilt
        /// with <see cref="Build"/>.
        /// </summary>
        public static void Use(YardDefinition definition)
        {
            Definition = definition ?? HomeYard.Create();
            gates.Clear();
            LawnGround = null;
        }

        public static LawnArea[] Areas => Definition.Areas;
        public static PatchSeed[] PatchSeeds => Definition.PatchSeeds;
        public static PatchInfo[] PatchInfos => Definition.PatchInfos;
        public static DrySpot[] DrySpots => Definition.DrySpots;
        public static WeedSpot[] WeedSpots => Definition.WeedSpots;
        public static Vector3 SpawnFeet => Definition.SpawnFeet;
        public static float SpawnYaw => Definition.SpawnYaw;

        /// <summary>XZ rectangle covering all areas (the scenery is built outside it).</summary>
        public static Rect YardRect => Definition.Bounds;

        /// <summary>The grid covered by the lawn: the union of the areas.</summary>
        public static LawnGrid LawnGrid => Definition.Grid;

        /// <summary>The ground under the lawn (its material switches to Opus/LawnGround when the lawn is built).</summary>
        public static Renderer LawnGround { get; private set; }

        /// <summary>The built gates (in definition order).</summary>
        public static IReadOnlyList<Gate> Gates => gates;

        static readonly Dictionary<string, PatchLayout> LayoutCache = new Dictionary<string, PatchLayout>();

        /// <summary>
        /// The patch layout used by the game and the tests (split into areas). Generating it takes ~0.3 s for the big garden, so
        /// it is generated once per definition (id, version, size); every call gets an independent copy (cells under
        /// obstacles are cleared while the lawn is built).
        /// </summary>
        public static PatchLayout GenerateLayout()
        {
            YardDefinition d = Definition;
            LawnGrid grid = d.Grid;
            string key = $"{d.Id}:{d.Version}:{grid.Width}x{grid.Height}:{d.PatchSeeds.Length}:{d.DrySpots.Length}:{d.Areas.Length}";
            if (!LayoutCache.TryGetValue(key, out PatchLayout prototype))
                LayoutCache[key] = prototype = d.GenerateLayout();
            return prototype.Clone();
        }

        /// <summary>The first gate opening into the area (null for the first area and for an area without a gate).</summary>
        public static Gate GateTo(int area)
        {
            foreach (Gate gate in gates)
                if (gate.Def.To == area) return gate;
            return null;
        }

        /// <summary>All gates opening into the area.</summary>
        public static IEnumerable<Gate> GatesTo(int area)
        {
            foreach (Gate gate in gates)
                if (gate.Def.To == area) yield return gate;
        }

        public static string AreaName(int area) => Definition.AreaName(area);

        public static LawnArea? AreaAt(Vector3 position) => Definition.AreaAt(position);

        /// <summary>
        /// Builds the garden: grounds, fences (with the gate openings cut out), props and gates. The collision boxes (fence
        /// boards, house, veranda, shed body, flowerbed edge, tree trunk) have the sizes the measured tests rely on;
        /// the details are visual only and each group is merged into a static batch once built.
        /// </summary>
        public static Transform Build(Transform parent)
        {
            YardDefinition definition = Definition;
            gates.Clear();
            Transform root = new GameObject("Garden").transform;
            root.SetParent(parent, false);

            LawnGrid grid = definition.Grid;
            Vector2 gridCenter = grid.Min + grid.Size * 0.5f;
            LawnGround = PrimitiveKit.Box(root, "LawnGround", new Vector3(gridCenter.x, -0.1f, gridCenter.y), new Vector3(grid.Size.x, 0.2f, grid.Size.y), Palette.TallGrass)
                .GetComponent<Renderer>();
            PrimitiveKit.Box(root, "OuterGround", new Vector3(gridCenter.x, -0.15f, gridCenter.y), new Vector3(260f, 0.2f, 260f), Palette.OuterGround.As(SurfaceKind.Ground));

            Batch(YardProps.BuildFence(root, definition.FenceSegments().ToArray()));
            foreach (PropPlacement prop in definition.Props)
                Batch(BuildProp(root, prop));
            foreach (GateDef def in definition.Gates)
                gates.Add(Gate.Create(root, def, definition.AreaById(def.To)?.Bounds.center ?? Vector2.zero));
            return root;
        }

        /// <summary>Builds a single garden prop (the model showroom uses it too).</summary>
        public static Transform BuildProp(Transform root, PropPlacement prop)
        {
            switch (prop.Kind)
            {
                case PropKind.House: return YardProps.BuildHouse(root, prop.Position, prop.Variant);
                case PropKind.Shed: return YardProps.BuildShed(root, prop.Position);
                case PropKind.Flowerbed: return YardProps.BuildFlowerbed(root, prop.Position, prop.Size);
                case PropKind.Tree: return YardProps.BuildTree(root, prop.Position);
                case PropKind.Birdbath: return YardProps.BuildBirdbath(root, prop.Position);
                case PropKind.Compost: return YardProps.BuildCompostBin(root, prop.Position);
                case PropKind.FruitTree: return GardenProps.BuildFruitTree(root, prop.Position, prop.Variant);
                case PropKind.RaisedBed: return GardenProps.BuildRaisedBed(root, prop.Position, prop.Size, prop.Variant);
                case PropKind.Wheelbarrow: return GardenProps.BuildWheelbarrow(root, prop.Position, prop.Yaw);
                case PropKind.RainBarrel: return GardenProps.BuildRainBarrel(root, prop.Position);
                case PropKind.BeanTrellis: return GardenProps.BuildBeanTrellis(root, prop.Position, prop.Size.x, prop.Variant);
                case PropKind.AppleCrate: return GardenProps.BuildAppleCrate(root, prop.Position, prop.Yaw, prop.Variant);
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(prop), prop.Kind, "Unknown prop kind");
            }
        }

        /// <summary>Meshes larger than this stay out of static batching (<see cref="Batchable"/>).</summary>
        public const int MaxBatchVertices = 64000;

        /// <summary>Merges a static group into a few large meshes per material (for hundreds of small parts).</summary>
        static void Batch(Transform group)
        {
            if (!Application.isPlaying)
                return;
            GameObject[] parts = Batchable(group);
            if (parts.Length > 0)
                StaticBatchingUtility.Combine(parts, group.gameObject);
        }

        /// <summary>
        /// The parts of a group that go into its static batch: every mesh up to <see cref="MaxBatchVertices"/> vertices. A bigger mesh is
        /// already one draw per material and came out of the batch with its triangles scrambled (the house joined into one
        /// 100k-triangle mesh, 21 October 2026).
        /// </summary>
        public static GameObject[] Batchable(Transform group)
        {
            var parts = new List<GameObject>();
            foreach (MeshFilter filter in group.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null && filter.sharedMesh.vertexCount <= MaxBatchVertices)
                    parts.Add(filter.gameObject);
            return parts.ToArray();
        }
    }
}

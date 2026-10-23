using System;
using System.Collections.Generic;
using Opus.Lawn;
using UnityEngine;

namespace Opus.World
{
    /// <summary>Direction of the fence line the gate sits in.</summary>
    public enum FenceAxis
    {
        /// <summary>The fence runs along Z (x constant); the gate leaves an opening from the hinge towards +Z.</summary>
        AlongZ,
        /// <summary>The fence runs along X (z constant); the gate leaves an opening from the hinge towards +X.</summary>
        AlongX,
    }

    /// <summary>
    /// The gate between two areas: when the <see cref="From"/> area is finished the <see cref="To"/> area unlocks and the gate swings
    /// towards it. The opening is [OpeningMin, OpeningMax] along the fence line.
    /// </summary>
    public readonly struct GateDef
    {
        public readonly int From;
        public readonly int To;
        public readonly Vector3 Hinge;
        public readonly float Width;
        public readonly FenceAxis Axis;

        public GateDef(int from, int to, Vector3 hinge, float width, FenceAxis axis = FenceAxis.AlongZ)
        {
            From = from;
            To = to;
            Hinge = hinge;
            Width = width;
            Axis = axis;
        }

        /// <summary>Constant coordinate of the fence line (AlongZ: x, AlongX: z).</summary>
        public float Line => Axis == FenceAxis.AlongZ ? Hinge.x : Hinge.z;
        /// <summary>Start of the opening along the line (AlongZ: z, AlongX: x).</summary>
        public float OpeningMin => Axis == FenceAxis.AlongZ ? Hinge.z : Hinge.x;
        public float OpeningMax => OpeningMin + Width;
        /// <summary>Middle of the opening (on the ground).</summary>
        public Vector3 OpeningCenter => Hinge + (Axis == FenceAxis.AlongZ ? Vector3.forward : Vector3.right) * (Width * 0.5f);

        public bool Connects(int area) => From == area || To == area;
        public int Other(int area) => From == area ? To : From;
    }

    public enum PropKind
    {
        House,
        Shed,
        Flowerbed,
        Tree,
        Birdbath,
        Compost,
        /// <summary>Apple tree (apples drop under it every day).</summary>
        FruitTree,
        /// <summary>Raised vegetable bed (Size: width × length; Variant: vegetable kind).</summary>
        RaisedBed,
        Wheelbarrow,
        RainBarrel,
        /// <summary>Bean trellis (Size.x: width), along X.</summary>
        BeanTrellis,
        AppleCrate,
    }

    /// <summary>Placement of a garden prop (body centre for the house; size for flowerbeds and beds; rotation and variant).</summary>
    public readonly struct PropPlacement
    {
        public readonly PropKind Kind;
        public readonly Vector3 Position;
        public readonly Vector2 Size;
        /// <summary>Rotation around the Y axis (degrees; props that do not support it ignore it).</summary>
        public readonly float Yaw;
        /// <summary>Variant/seed: props of the same kind should not look like copies of each other.</summary>
        public readonly int Variant;

        public PropPlacement(PropKind kind, Vector3 position, Vector2 size = default, float yaw = 0f, int variant = 0)
        {
            Kind = kind;
            Position = position;
            Size = size;
            Yaw = yaw;
            Variant = variant;
        }
    }

    /// <summary>
    /// All the data of a garden: areas, gates between areas, lawn patches (seed + name), dry spots, weeds,
    /// fence lines, props and the spawn point. <see cref="YardBuilder"/> builds it; a save matches it by definition id and
    /// version. Plain data: creates no game objects, can be built freely in tests.
    /// </summary>
    public sealed class YardDefinition
    {
        /// <summary>The id saves are matched by (versions of the same garden share the id). "bahce" is a save key from the Turkish build, kept so existing saves still load.</summary>
        public string Id = "bahce";
        public int Version = 1;
        public float CellSize = 0.1f;
        public int FirstArea = 1;
        public LawnArea[] Areas = Array.Empty<LawnArea>();
        public GateDef[] Gates = Array.Empty<GateDef>();
        public PatchSeed[] PatchSeeds = Array.Empty<PatchSeed>();
        /// <summary>Same order as <see cref="PatchSeeds"/>: patch name and pill colour in the interface.</summary>
        public PatchInfo[] PatchInfos = Array.Empty<PatchInfo>();
        public DrySpot[] DrySpots = Array.Empty<DrySpot>();
        public WeedSpot[] WeedSpots = Array.Empty<WeedSpot>();
        /// <summary>Fence lines; gate openings are cut automatically when built (<see cref="FenceSegments"/>).</summary>
        public (Vector3 from, Vector3 to)[] FenceLines = Array.Empty<(Vector3, Vector3)>();
        public PropPlacement[] Props = Array.Empty<PropPlacement>();
        public Vector3 SpawnFeet;
        public float SpawnYaw;

        /// <summary>XZ rectangle covering all areas (used by the lawn grid and the scenery).</summary>
        public Rect Bounds
        {
            get
            {
                if (Areas.Length == 0)
                    return Rect.zero;
                Rect r = Areas[0].Bounds;
                foreach (LawnArea area in Areas)
                    r = Rect.MinMaxRect(Mathf.Min(r.xMin, area.Bounds.xMin), Mathf.Min(r.yMin, area.Bounds.yMin),
                        Mathf.Max(r.xMax, area.Bounds.xMax), Mathf.Max(r.yMax, area.Bounds.yMax));
                return r;
            }
        }

        public LawnGrid Grid
        {
            get
            {
                Rect r = Bounds;
                return new LawnGrid(r.min, r.size, CellSize);
            }
        }

        public PatchLayout GenerateLayout() => PatchLayout.Generate(Grid, PatchSeeds, DrySpots, Areas);

        public LawnArea? AreaAt(Vector3 position)
        {
            var point = new Vector2(position.x, position.z);
            foreach (LawnArea area in Areas)
                if (area.Contains(point)) return area;
            return null;
        }

        public LawnArea? AreaById(int id)
        {
            foreach (LawnArea area in Areas)
                if (area.Id == id) return area;
            return null;
        }

        public string AreaName(int id) => AreaById(id)?.Name ?? "Garden";

        /// <summary>Areas unlocked when an area is finished (in gate list order).</summary>
        public IReadOnlyList<int> AreasOpenedBy(int area)
        {
            var result = new List<int>();
            foreach (GateDef gate in Gates)
                if (gate.From == area && !result.Contains(gate.To))
                    result.Add(gate.To);
            return result;
        }

        /// <summary>Fence lines with the gate openings removed (line order preserved).</summary>
        public List<(Vector3 from, Vector3 to)> FenceSegments()
        {
            var segments = new List<(Vector3, Vector3)>();
            foreach ((Vector3 from, Vector3 to) line in FenceLines)
                CutGates(line.from, line.to, 0, segments);
            return segments;
        }

        void CutGates(Vector3 from, Vector3 to, int firstGate, List<(Vector3, Vector3)> output)
        {
            const float Epsilon = 0.01f, MinLength = 0.05f;
            for (int g = firstGate; g < Gates.Length; g++)
            {
                GateDef gate = Gates[g];
                bool alongZ = gate.Axis == FenceAxis.AlongZ;
                float lineFrom = alongZ ? from.x : from.z, lineTo = alongZ ? to.x : to.z;
                if (Mathf.Abs(lineFrom - gate.Line) > Epsilon || Mathf.Abs(lineTo - gate.Line) > Epsilon)
                    continue;
                float a0 = alongZ ? from.z : from.x, a1 = alongZ ? to.z : to.x;
                float lo = Mathf.Min(a0, a1), hi = Mathf.Max(a0, a1);
                if (gate.OpeningMax <= lo || gate.OpeningMin >= hi)
                    continue;

                // Depending on the line's direction: first up to the opening, then after the opening
                bool rising = a1 >= a0;
                float firstEnd = rising ? gate.OpeningMin : gate.OpeningMax;
                float secondStart = rising ? gate.OpeningMax : gate.OpeningMin;
                Vector3 At(float a) => alongZ ? new Vector3(from.x, from.y, a) : new Vector3(a, from.y, from.z);
                if (Mathf.Abs(firstEnd - a0) > MinLength && (rising ? firstEnd > a0 : firstEnd < a0))
                    CutGates(from, At(firstEnd), g + 1, output);
                if (Mathf.Abs(a1 - secondStart) > MinLength && (rising ? a1 > secondStart : a1 < secondStart))
                    CutGates(At(secondStart), to, g + 1, output);
                return;
            }
            output.Add((from, to));
        }

        /// <summary>
        /// Consistency check: unique ids, no overlapping areas, gates on the shared edge of two areas, seeds and
        /// weeds inside their own areas, every area reachable from the first area through gates, spawn in the first area. Empty list = valid.
        /// </summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            var ids = new HashSet<int>();
            for (int i = 0; i < Areas.Length; i++)
            {
                if (!ids.Add(Areas[i].Id) || Areas[i].Id <= 0 || Areas[i].Id > 254)
                    errors.Add($"Invalid or duplicate area id: {Areas[i].Id}");
                for (int j = i + 1; j < Areas.Length; j++)
                    if (Areas[i].Bounds.Overlaps(Areas[j].Bounds))
                        errors.Add($"Areas overlap: {Areas[i].Id} and {Areas[j].Id}");
            }
            if (!ids.Contains(FirstArea))
                errors.Add($"The first area ({FirstArea}) is not defined");
            if (AreaAt(SpawnFeet)?.Id != FirstArea)
                errors.Add("The spawn point is not in the first area");
            if (PatchSeeds.Length != PatchInfos.Length)
                errors.Add($"Patch info count ({PatchInfos.Length}) does not match the seed count ({PatchSeeds.Length})");
            if (PatchSeeds.Length == 0 || PatchSeeds.Length > 254)
                errors.Add("1..254 patches are supported");

            foreach (GateDef gate in Gates)
            {
                LawnArea? from = AreaById(gate.From), to = AreaById(gate.To);
                if (!from.HasValue || !to.HasValue)
                {
                    errors.Add($"Gate connects to an undefined area: {gate.From} → {gate.To}");
                    continue;
                }
                if (!OnSharedEdge(gate, from.Value.Bounds, to.Value.Bounds))
                    errors.Add($"Gate {gate.From} → {gate.To} is not on the shared edge of two areas");
            }

            foreach (PatchSeed seed in PatchSeeds)
                if (AreaAt(new Vector3(seed.Position.x, 0f, seed.Position.y))?.Id != seed.Area)
                    errors.Add($"Seed ({seed.Position}) is not in its own area ({seed.Area})");
            foreach (WeedSpot spot in WeedSpots)
                if (!AreaAt(spot.Position).HasValue)
                    errors.Add($"Weed ({spot.Position}) is not in any area");

            // Reachability: through the gate chain from the first area
            var reached = new HashSet<int> { FirstArea };
            var queue = new Queue<int>();
            queue.Enqueue(FirstArea);
            while (queue.Count > 0)
            {
                int area = queue.Dequeue();
                foreach (int next in AreasOpenedBy(area))
                    if (reached.Add(next)) queue.Enqueue(next);
            }
            foreach (LawnArea area in Areas)
                if (!reached.Contains(area.Id))
                    errors.Add($"Area {area.Id} cannot be unlocked through a gate from the first area");
            return errors;
        }

        static bool OnSharedEdge(GateDef gate, Rect a, Rect b)
        {
            const float Epsilon = 0.01f;
            if (gate.Axis == FenceAxis.AlongZ)
            {
                bool shared = (Mathf.Abs(a.xMax - gate.Line) < Epsilon && Mathf.Abs(b.xMin - gate.Line) < Epsilon)
                              || (Mathf.Abs(a.xMin - gate.Line) < Epsilon && Mathf.Abs(b.xMax - gate.Line) < Epsilon);
                float lo = Mathf.Max(a.yMin, b.yMin), hi = Mathf.Min(a.yMax, b.yMax);
                return shared && gate.OpeningMin >= lo - Epsilon && gate.OpeningMax <= hi + Epsilon;
            }
            else
            {
                bool shared = (Mathf.Abs(a.yMax - gate.Line) < Epsilon && Mathf.Abs(b.yMin - gate.Line) < Epsilon)
                              || (Mathf.Abs(a.yMin - gate.Line) < Epsilon && Mathf.Abs(b.yMax - gate.Line) < Epsilon);
                float lo = Mathf.Max(a.xMin, b.xMin), hi = Mathf.Min(a.xMax, b.xMax);
                return shared && gate.OpeningMin >= lo - Epsilon && gate.OpeningMax <= hi + Epsilon;
            }
        }
    }
}

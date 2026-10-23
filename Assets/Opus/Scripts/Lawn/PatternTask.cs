using System.Collections.Generic;
using UnityEngine;

namespace Opus.Lawn
{
    public enum PatternKind
    {
        StripesNorthSouth,
        StripesEastWest,
        Diagonal,
        Checker,
    }

    /// <summary>
    /// Pattern task (§3-E): mowing the requested stripe pattern in a patch. Every cell's last mowing direction (LawnState b/a)
    /// is compared with the direction the pattern wants at that point; the task completes once enough of the patch is mown the right way.
    /// Patterns: north-south or east-west stripes (1.2 m), diagonal stripes, checker (2.4 m squares).
    /// </summary>
    public sealed class PatternTask
    {
        public const float BandWidth = 1.2f;
        public const float CheckerSize = 2.4f;
        /// <summary>The cell direction must match the requested direction by more than this angle's cosine.</summary>
        public const float MatchDot = 0.5f;

        readonly int[] cells;

        public PatternKind Kind { get; }
        public int Patch { get; }
        public int Reward { get; }
        public Vector2 Origin { get; }
        public float Score { get; private set; }
        public bool Completed { get; private set; }
        public float Goal => Kind == PatternKind.Checker ? 0.7f : 0.8f;
        public string Name => NameOf(Kind);
        public int CellCount => cells.Length;

        public PatternTask(PatternKind kind, int patch, LawnState lawn, bool completed = false)
        {
            Kind = kind;
            Patch = patch;
            Reward = RewardOf(kind);
            Completed = completed;
            var list = new List<int>();
            Vector2 sum = Vector2.zero;
            byte[] ids = lawn.Layout.PatchIds;
            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i] != patch) continue;
                list.Add(i);
                sum += lawn.Grid.CellCenter(i);
            }
            cells = list.ToArray();
            Vector2 centroid = cells.Length > 0 ? sum / cells.Length : Vector2.zero;
            // The bands should start in the middle of the patch (snap to the grid so no half band is left at the edge)
            float step = kind == PatternKind.Checker ? CheckerSize : BandWidth;
            Origin = new Vector2(Mathf.Round(centroid.x / step) * step, Mathf.Round(centroid.y / step) * step);
        }

        public static string NameOf(PatternKind kind) => kind switch
        {
            PatternKind.StripesNorthSouth => "North-south stripes",
            PatternKind.StripesEastWest => "East-west stripes",
            PatternKind.Diagonal => "Diagonal stripes",
            _ => "Checker",
        };

        public static int RewardOf(PatternKind kind) => kind switch
        {
            PatternKind.Diagonal => 220,
            PatternKind.Checker => 300,
            _ => 150,
        };

        /// <summary>The mowing direction the pattern wants at this point (unit).</summary>
        public Vector2 Desired(Vector2 p)
        {
            Vector2 d = p - Origin;
            switch (Kind)
            {
                case PatternKind.StripesNorthSouth:
                    return Even(Mathf.FloorToInt(d.x / BandWidth)) ? Vector2.up : Vector2.down;
                case PatternKind.StripesEastWest:
                    return Even(Mathf.FloorToInt(d.y / BandWidth)) ? Vector2.right : Vector2.left;
                case PatternKind.Diagonal:
                {
                    var along = new Vector2(1f, 1f).normalized;
                    var across = new Vector2(1f, -1f).normalized;
                    return Even(Mathf.FloorToInt(Vector2.Dot(d, across) / BandWidth)) ? along : -along;
                }
                default:
                    return Even(Mathf.FloorToInt(d.x / CheckerSize) + Mathf.FloorToInt(d.y / CheckerSize)) ? Vector2.up : Vector2.down;
            }
        }

        /// <summary>Is the cell in the requested band's "A" direction (for the guide tint)?</summary>
        public bool IsBandA(Vector2 p)
        {
            Vector2 want = Desired(p);
            return want == Vector2.up || want == Vector2.right || want.x + want.y > 0f;
        }

        static bool Even(int value) => (value & 1) == 0;

        /// <summary>Computes the fraction of the patch mown in the right direction; completes once the target is reached.</summary>
        /// <returns>True if it was completed in this call.</returns>
        public bool Evaluate(LawnState lawn)
        {
            if (cells.Length == 0)
                return false;
            int matched = 0;
            foreach (int i in cells)
            {
                if (!lawn.IsCut(i))
                    continue;
                Color32 c = lawn.CellAt(i);
                var dir = new Vector2(c.b / 255f * 2f - 1f, c.a / 255f * 2f - 1f);
                if (dir.sqrMagnitude < 0.09f)
                    continue;
                if (Vector2.Dot(dir.normalized, Desired(lawn.Grid.CellCenter(i))) > MatchDot)
                    matched++;
            }
            Score = matched / (float)cells.Length;
            if (Completed || Score < Goal)
                return false;
            Completed = true;
            return true;
        }

        /// <summary>
        /// Guide texture (per cell: 0 no pattern, 255 band "A", 128 band "B"); the shader lightly tints the requested
        /// bands over the long grass.
        /// </summary>
        public void WriteGuide(LawnState lawn, byte[] guide)
        {
            System.Array.Clear(guide, 0, guide.Length);
            if (Completed)
                return;
            foreach (int i in cells)
                guide[i] = IsBandA(lawn.Grid.CellCenter(i)) ? (byte)255 : (byte)128;
        }

        /// <summary>Picks the day's task from a seed: one of the large enough patches in the unlocked areas.</summary>
        public static PatternTask ForDay(int day, LawnState lawn, LawnTasks tasks)
        {
            var random = new System.Random(day * 31337 + 5);
            var candidates = new List<PatchProgress>();
            foreach (PatchProgress p in tasks.Patches)
                if (tasks.IsAreaUnlocked(p.Area) && p.AreaSquareMeters >= 30f)
                    candidates.Add(p);
            if (candidates.Count == 0)
                return null;
            PatchProgress chosen = candidates[random.Next(candidates.Count)];
            // Easy patterns in the first days; checker from the third day on
            int kinds = day >= 3 ? 4 : 3;
            var kind = (PatternKind)random.Next(kinds);
            return new PatternTask(kind, chosen.Id, lawn);
        }
    }
}

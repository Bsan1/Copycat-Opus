using System;
using System.Collections.Generic;
using Opus.Lawn;
using Opus.World;
using UnityEngine;

namespace Opus.Core
{
    /// <summary>
    /// Stamps a save with the garden definition and migrates a save belonging to another version of the same garden area by area: the grass,
    /// weeds, patch states and hidden items of areas whose size and patch seeds have not changed are kept; new or changed
    /// areas come from the starting state. Day, points, upgrades, collection and unlocked areas are always kept.
    /// </summary>
    public static class SaveMigration
    {
        const float Epsilon = 1e-3f;

        /// <summary>Writes the garden's id, version and geometry into the save.</summary>
        public static void Stamp(SaveData data, YardDefinition definition, LawnGrid grid)
        {
            data.yardId = definition.Id;
            data.yardVersion = definition.Version;
            data.gridMinX = grid.Min.x;
            data.gridMinZ = grid.Min.y;
            data.cellSize = grid.CellSize;
            data.areas = new SavedArea[definition.Areas.Length];
            for (int i = 0; i < definition.Areas.Length; i++)
            {
                Rect r = definition.Areas[i].Bounds;
                data.areas[i] = new SavedArea { id = definition.Areas[i].Id, minX = r.xMin, minZ = r.yMin, maxX = r.xMax, maxZ = r.yMax };
            }
            data.patchSeeds = new SavedSeed[definition.PatchSeeds.Length];
            for (int i = 0; i < definition.PatchSeeds.Length; i++)
            {
                PatchSeed seed = definition.PatchSeeds[i];
                data.patchSeeds[i] = new SavedSeed { x = seed.Position.x, z = seed.Position.y, variety = seed.Variety, area = seed.Area };
            }
            data.weedPositions = new Vector2[definition.WeedSpots.Length];
            for (int i = 0; i < definition.WeedSpots.Length; i++)
                data.weedPositions[i] = new Vector2(definition.WeedSpots[i].Position.x, definition.WeedSpots[i].Position.z);
        }

        /// <summary>
        /// Migrates the <paramref name="old"/> save onto <paramref name="fresh"/> (stamped), this garden's starting state.
        /// </summary>
        /// <returns>False and a readable reason if it cannot be migrated.</returns>
        public static bool TryMigrate(SaveData old, SaveData fresh, out SaveData migrated, out string error)
        {
            migrated = null;
            if (string.IsNullOrEmpty(old.yardId) || old.areas == null || old.areas.Length == 0)
            {
                error = "The save belongs to an old version of this garden and carries no migration info.";
                return false;
            }
            if (old.yardId != fresh.yardId)
            {
                error = $"The save belongs to another garden ({old.yardId}).";
                return false;
            }
            if (Mathf.Abs(old.cellSize - fresh.cellSize) > 1e-5f)
            {
                error = "The lawn grid in the save has a different size.";
                return false;
            }
            byte[] oldCells = SaveSystem.Unpack(old.lawn), freshCells = SaveSystem.Unpack(fresh.lawn);
            const int stride = LawnState.SnapshotBytesPerCell;
            if (oldCells != null && oldCells.Length == old.gridWidth * old.gridHeight * LawnState.LegacySnapshotBytesPerCell)
                oldCells = ExpandLegacy(oldCells);
            if (oldCells == null || oldCells.Length != old.gridWidth * old.gridHeight * stride
                || freshCells == null || freshCells.Length != fresh.gridWidth * fresh.gridHeight * stride)
            {
                error = "The lawn data in the save is corrupt.";
                return false;
            }

            HashSet<int> kept = KeptAreas(old, fresh);
            if (kept.Count == 0)
            {
                error = "None of the areas in the save match this garden.";
                return false;
            }

            // Lawn: every cell in a kept area takes the cell at the same world point in the old grid
            var cells = (byte[])freshCells.Clone();
            float size = fresh.cellSize;
            for (int z = 0; z < fresh.gridHeight; z++)
            {
                for (int x = 0; x < fresh.gridWidth; x++)
                {
                    var center = new Vector2(fresh.gridMinX + (x + 0.5f) * size, fresh.gridMinZ + (z + 0.5f) * size);
                    if (!kept.Contains(AreaAt(fresh.areas, center)))
                        continue;
                    int ox = Mathf.FloorToInt((center.x - old.gridMinX) / size), oz = Mathf.FloorToInt((center.y - old.gridMinZ) / size);
                    if (ox < 0 || oz < 0 || ox >= old.gridWidth || oz >= old.gridHeight)
                        continue;
                    Buffer.BlockCopy(oldCells, (oz * old.gridWidth + ox) * stride, cells, (z * fresh.gridWidth + x) * stride, stride);
                }
            }

            // Patch ids: the seed at the same place in a kept area
            var patchMap = new Dictionary<int, int>();
            for (int i = 0; i < old.patchSeeds.Length; i++)
            {
                SavedSeed seed = old.patchSeeds[i];
                if (!kept.Contains(seed.area))
                    continue;
                for (int j = 0; j < fresh.patchSeeds.Length; j++)
                {
                    SavedSeed other = fresh.patchSeeds[j];
                    if (other.area == seed.area && Mathf.Abs(other.x - seed.x) < Epsilon && Mathf.Abs(other.z - seed.z) < Epsilon)
                    {
                        patchMap[i + 1] = j + 1;
                        break;
                    }
                }
            }

            // Weeds: a weed at the same place in a kept area takes its old health
            var weedHealth = (float[])fresh.weedHealth.Clone();
            if (old.weedHealth != null && old.weedPositions != null && old.weedHealth.Length == old.weedPositions.Length)
            {
                for (int j = 0; j < fresh.weedPositions.Length && j < weedHealth.Length; j++)
                {
                    Vector2 p = fresh.weedPositions[j];
                    if (!kept.Contains(AreaAt(fresh.areas, p)))
                        continue;
                    for (int i = 0; i < old.weedPositions.Length; i++)
                    {
                        if ((old.weedPositions[i] - p).sqrMagnitude < Epsilon * Epsilon)
                        {
                            weedHealth[j] = old.weedHealth[i];
                            break;
                        }
                    }
                }
            }

            var freshAreaIds = new HashSet<int>();
            foreach (SavedArea area in fresh.areas) freshAreaIds.Add(area.id);
            var unlocked = new List<int>(fresh.unlockedAreas ?? Array.Empty<int>());
            foreach (int area in old.unlockedAreas ?? Array.Empty<int>())
                if (freshAreaIds.Contains(area) && !unlocked.Contains(area)) unlocked.Add(area);
            unlocked.Sort();

            var hidden = new List<LostItems.SavedItem>();
            foreach (LostItems.SavedItem item in old.hiddenFinds ?? Array.Empty<LostItems.SavedItem>())
                if (kept.Contains(AreaAt(fresh.areas, new Vector2(item.position.x, item.position.z))))
                    hidden.Add(item);

            var apples = new List<Vector3>();
            foreach (Vector3 apple in old.apples ?? Array.Empty<Vector3>())
                if (kept.Contains(AreaAt(fresh.areas, new Vector2(apple.x, apple.z))))
                    apples.Add(apple);

            bool playerInYard = AreaAt(fresh.areas, new Vector2(old.playerFeet.x, old.playerFeet.z)) != 0;
            bool patternKept = old.pattern > 0 && patchMap.ContainsKey(old.patternPatch);
            migrated = new SaveData
            {
                version = SaveData.CurrentVersion,
                savedAt = old.savedAt,
                playSeconds = old.playSeconds,
                gridWidth = fresh.gridWidth,
                gridHeight = fresh.gridHeight,
                patchCount = fresh.patchCount,
                layoutHash = fresh.layoutHash,
                playerFeet = playerInYard ? old.playerFeet : fresh.playerFeet,
                playerYaw = playerInYard ? old.playerYaw : fresh.playerYaw,
                playerPitch = playerInYard ? old.playerPitch : fresh.playerPitch,
                toolSlot = old.toolSlot,
                sprayMode = old.sprayMode,
                unlockedAreas = unlocked.ToArray(),
                discoveredPatches = Remap(old.discoveredPatches, patchMap),
                finishedPatches = Remap(old.finishedPatches, patchMap),
                everFinishedPatches = Remap(old.everFinishedPatches, patchMap),
                weedHealth = weedHealth,
                lawn = SaveSystem.Pack(cells),
                day = old.day,
                points = old.points,
                upgrades = old.upgrades,
                collected = old.collected,
                hiddenFinds = hidden.ToArray(),
                pattern = patternKept ? old.pattern : 0,
                patternPatch = patternKept ? patchMap[old.patternPatch] : 0,
                patternCompleted = patternKept && old.patternCompleted,
                apples = apples.ToArray(),
                applesDay = old.applesDay,
                jobRecords = old.jobRecords,
                yardId = fresh.yardId,
                yardVersion = fresh.yardVersion,
                gridMinX = fresh.gridMinX,
                gridMinZ = fresh.gridMinZ,
                cellSize = fresh.cellSize,
                areas = fresh.areas,
                patchSeeds = fresh.patchSeeds,
                weedPositions = fresh.weedPositions,
            };
            error = null;
            return true;
        }

        /// <summary>
        /// Stamps an unstamped (pre-U13) save with the geometry of the garden version it belongs to. False if the grid size does not
        /// match that version (the save belongs to another garden).
        /// </summary>
        public static bool StampLegacy(SaveData old, YardDefinition firstVersion)
        {
            if (!string.IsNullOrEmpty(old.yardId))
                return true;
            LawnGrid grid = firstVersion.Grid;
            if (old.gridWidth != grid.Width || old.gridHeight != grid.Height || old.patchCount != firstVersion.PatchSeeds.Length
                || (old.weedHealth != null && old.weedHealth.Length != firstVersion.WeedSpots.Length))
                return false;
            Stamp(old, firstVersion, grid);
            return true;
        }

        /// <summary>U8 snapshot (6 bytes per cell, no height) → 7 bytes: mown is short, the rest long.</summary>
        static byte[] ExpandLegacy(byte[] legacy)
        {
            const int from = LawnState.LegacySnapshotBytesPerCell, to = LawnState.SnapshotBytesPerCell;
            int cells = legacy.Length / from;
            var expanded = new byte[cells * to];
            for (int i = 0; i < cells; i++)
            {
                Buffer.BlockCopy(legacy, i * from, expanded, i * to, from);
                expanded[i * to + 6] = (legacy[i * from] & 1) != 0 ? LawnState.Cut : LawnState.Tall;
            }
            return expanded;
        }

        /// <summary>Areas whose id, rectangle and patch seeds are the same in both saves.</summary>
        public static HashSet<int> KeptAreas(SaveData old, SaveData fresh)
        {
            var kept = new HashSet<int>();
            foreach (SavedArea a in old.areas)
            {
                foreach (SavedArea b in fresh.areas)
                {
                    if (a.id != b.id) continue;
                    bool sameRect = Mathf.Abs(a.minX - b.minX) < Epsilon && Mathf.Abs(a.minZ - b.minZ) < Epsilon
                                    && Mathf.Abs(a.maxX - b.maxX) < Epsilon && Mathf.Abs(a.maxZ - b.maxZ) < Epsilon;
                    if (sameRect && SameSeeds(old.patchSeeds, fresh.patchSeeds, a.id))
                        kept.Add(a.id);
                }
            }
            return kept;
        }

        static bool SameSeeds(SavedSeed[] a, SavedSeed[] b, int area)
        {
            var left = new List<SavedSeed>();
            var right = new List<SavedSeed>();
            foreach (SavedSeed s in a) if (s.area == area) left.Add(s);
            foreach (SavedSeed s in b) if (s.area == area) right.Add(s);
            if (left.Count != right.Count)
                return false;
            for (int i = 0; i < left.Count; i++)
                if (Mathf.Abs(left[i].x - right[i].x) > Epsilon || Mathf.Abs(left[i].z - right[i].z) > Epsilon || left[i].variety != right[i].variety)
                    return false;
            return true;
        }

        /// <summary>Id of the area containing the point (bottom/left edge included, top/right excluded; 0 if none).</summary>
        static int AreaAt(SavedArea[] areas, Vector2 p)
        {
            foreach (SavedArea area in areas)
                if (p.x >= area.minX && p.x < area.maxX && p.y >= area.minZ && p.y < area.maxZ)
                    return area.id;
            return 0;
        }

        static int[] Remap(int[] ids, Dictionary<int, int> map)
        {
            var result = new List<int>();
            foreach (int id in ids ?? Array.Empty<int>())
                if (map.TryGetValue(id, out int mapped)) result.Add(mapped);
            result.Sort();
            return result.ToArray();
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Opus.Lawn
{
    /// <summary>
    /// Draws the grass. The garden is split into 4×4 m chunks; for every visible chunk the tuft mesh is instanced on the GPU
    /// N times (Graphics.RenderMeshPrimitives). The tuft positions are generated in the shader from the instance number with
    /// the R2 low-discrepancy sequence: since every prefix of the sequence is evenly spread, drawing the "first K instances"
    /// by distance lowers the density without gaps (density LOD). There is no per-tuft data on the CPU.
    /// Distant chunks are also drawn with a light tuft mesh (mesh LOD: 8 blades × 2 segments, the first eight blades of the
    /// near mesh slightly widened); the switch is per chunk, with a delayed band so it does not flicker.
    /// </summary>
    public sealed class GrassField : MonoBehaviour
    {
        public const float ChunkSize = 4f;
        public const float TuftsPerSquareMeter = 90f;
        public const float TallHeight = 0.42f;
        public const float CutHeight = 0.05f;
        public const float BladeWidth = 0.034f;
        public const float FullDensityDistance = 12f;
        public const float FarDensityDistance = 40f;
        public const float FarDensityFraction = 0.14f;
        public const int MaxPushers = 4;
        public const int NearBlades = 12;
        public const int FarBlades = 8;
        public const int FarSegments = 2;
        /// <summary>Width multiplier covering the blades missing from the far mesh.</summary>
        public const float FarWidthScale = 1.4f;
        /// <summary>Beyond this distance (the chunk's nearest point) the far mesh is used.</summary>
        public const float FarMeshDistance = 18f;
        /// <summary>Half width of the transition band: far at FarMeshDistance + band, near at FarMeshDistance - band.</summary>
        public const float FarMeshHysteresis = 1.5f;
        /// <summary>At low and medium grass density the far mesh kicks in this much earlier.</summary>
        public const float ReducedDensityLodScale = 0.65f;

        /// <summary>Mesh LOD (can be turned off for measurements and comparisons).</summary>
        public static bool MeshLodEnabled = true;

        static readonly int ChunkDataId = Shader.PropertyToID("_ChunkData");
        static readonly int GrassParamsId = Shader.PropertyToID("_OpusGrassParams");
        static readonly int GrassLodId = Shader.PropertyToID("_OpusGrassLod");
        static readonly int WindId = Shader.PropertyToID("_OpusWind");
        static readonly int WindNoiseId = Shader.PropertyToID("_OpusWindNoise");
        static readonly int PushersId = Shader.PropertyToID("_OpusPushers");
        static readonly int PusherCountId = Shader.PropertyToID("_OpusPusherCount");

        struct Chunk
        {
            public Bounds Bounds;
            public MaterialPropertyBlock Properties;
            public int MaxTufts;
            /// <summary>Whether the last chosen mesh was the far one (remembered per chunk for the delayed switch).</summary>
            public bool Far;
        }

        readonly List<IGrassPusher> pushers = new List<IGrassPusher>();
        readonly Vector4[] pusherData = new Vector4[MaxPushers];
        readonly Plane[] frustum = new Plane[6];
        Chunk[] chunks;
        Mesh tuftMesh;
        Mesh farTuftMesh;
        Material material;
        Texture2D windNoise;

        public LawnState Lawn { get; private set; }
        public Camera ViewCamera { get; set; }
        /// <summary>Quality setting: 1 = full density.</summary>
        public float Density { get; set; } = 1f;
        public Vector2 WindDirection { get; set; } = new Vector2(0.8f, 0.6f).normalized;
        public float WindStrength { get; set; } = 0.32f;
        public float WindSpeed { get; set; } = 1.1f;
        /// <summary>Occlusion test (eye, chunk bounds → not visible?); if null, only the view frustum.</summary>
        public System.Func<Vector3, Bounds, bool> IsHidden { get; set; }

        public int DrawnChunks { get; private set; }
        /// <summary>Number of chunks drawn with the far mesh in the last frame.</summary>
        public int DrawnFarChunks { get; private set; }
        public int DrawnTufts { get; private set; }
        /// <summary>Number of triangles drawn in the last frame (tufts × mesh triangles).</summary>
        public long DrawnTriangles { get; private set; }
        public int TotalChunks => chunks?.Length ?? 0;
        /// <summary>Number of chunks in the view frustum skipped by the occlusion test in the last frame.</summary>
        public int OccludedChunks { get; private set; }
        bool[] drawnFlags;

        public static GrassField Create(LawnState lawn, Transform parent)
        {
            var go = new GameObject("Grass");
            go.transform.SetParent(parent, false);
            var field = go.AddComponent<GrassField>();
            field.Lawn = lawn;
            field.Initialize();
            return field;
        }

        public void AddPusher(IGrassPusher pusher) => pushers.Add(pusher);

        void Initialize()
        {
            Shader shader = Shader.Find("Opus/Grass");
            if (shader == null)
            {
                Debug.LogError("[Opus] Opus/Grass shader not found.");
                enabled = false;
                return;
            }
            material = new Material(shader) { name = "Grass", enableInstancing = true };
            tuftMesh = TuftMesh.Build(blades: NearBlades);
            farTuftMesh = TuftMesh.Build(blades: FarBlades, segments: FarSegments, widthScale: FarWidthScale);
            windNoise = BuildWindNoise(128, 11);
            BuildChunks();
        }

        void BuildChunks()
        {
            LawnGrid grid = Lawn.Grid;
            int countX = Mathf.CeilToInt(grid.Size.x / ChunkSize);
            int countZ = Mathf.CeilToInt(grid.Size.y / ChunkSize);
            var list = new List<Chunk>(countX * countZ);
            int maxTufts = Mathf.CeilToInt(TuftsPerSquareMeter * ChunkSize * ChunkSize);

            for (int cz = 0; cz < countZ; cz++)
            {
                for (int cx = 0; cx < countX; cx++)
                {
                    Vector2 origin = grid.Min + new Vector2(cx * ChunkSize, cz * ChunkSize);
                    if (!HasGrass(origin))
                        continue;

                    var properties = new MaterialPropertyBlock();
                    // seed: an integer-valued float (converted to uint in the shader)
                    properties.SetVector(ChunkDataId, new Vector4(origin.x, origin.y, cz * countX + cx + 1, maxTufts));
                    // Widen the bounds a little for blade bending and wind
                    var bounds = new Bounds(
                        new Vector3(origin.x + ChunkSize * 0.5f, TallHeight, origin.y + ChunkSize * 0.5f),
                        new Vector3(ChunkSize + 1f, TallHeight * 2.5f + 0.5f, ChunkSize + 1f));
                    list.Add(new Chunk { Bounds = bounds, Properties = properties, MaxTufts = maxTufts });
                }
            }
            chunks = list.ToArray();
        }

        bool HasGrass(Vector2 origin)
        {
            LawnGrid grid = Lawn.Grid;
            if (!grid.TryGetCellRange(origin, origin + Vector2.one * ChunkSize, out Vector2Int from, out Vector2Int to))
                return false;
            for (int z = from.y; z <= to.y; z++)
                for (int x = from.x; x <= to.x; x++)
                    if (Lawn.Layout.PatchIds[grid.Index(x, z)] != PatchLayout.NoGrass)
                        return true;
            return false;
        }

        /// <summary>
        /// Whether the chunk is drawn with the far mesh this frame: it only switches back from the previous choice after
        /// crossing the other end of the band. At the low density setting the switch is closer.
        /// </summary>
        public static bool ChooseFar(bool wasFar, float distance, float density)
        {
            float lod = FarMeshDistance * (density < 0.99f ? ReducedDensityLodScale : 1f);
            return wasFar ? distance > lod - FarMeshHysteresis : distance > lod + FarMeshHysteresis;
        }

        /// <summary>Drawn density fraction by distance (the same formula as in the shader).</summary>
        public static float DensityAt(float distance)
        {
            float t = Mathf.Clamp01((distance - FullDensityDistance) / (FarDensityDistance - FullDensityDistance));
            t = t * t * (3f - 2f * t);
            return Mathf.Lerp(1f, FarDensityFraction, t);
        }

        void LateUpdate()
        {
            if (chunks == null || ViewCamera == null)
                return;

            Lawn.Tick(Time.deltaTime);
            Lawn.Apply();
            SetGlobals();

            GeometryUtility.CalculateFrustumPlanes(ViewCamera, frustum);
            Vector3 eye = ViewCamera.transform.position;
            var renderParams = new RenderParams(material)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
                layer = 0,
            };

            DrawnChunks = 0;
            DrawnFarChunks = 0;
            DrawnTufts = 0;
            DrawnTriangles = 0;
            OccludedChunks = 0;
            int nearTriangles = (int)(tuftMesh.GetIndexCount(0) / 3), farTriangles = (int)(farTuftMesh.GetIndexCount(0) / 3);
            if (drawnFlags == null || drawnFlags.Length != chunks.Length)
                drawnFlags = new bool[chunks.Length];
            for (int i = 0; i < chunks.Length; i++)
            {
                Chunk chunk = chunks[i];
                drawnFlags[i] = false;
                if (!GeometryUtility.TestPlanesAABB(frustum, chunk.Bounds))
                    continue;
                if (IsHidden != null && IsHidden(eye, chunk.Bounds))
                {
                    OccludedChunks++;
                    continue;
                }
                Vector3 closest = chunk.Bounds.ClosestPoint(eye);
                float distance = Vector2.Distance(new Vector2(closest.x, closest.z), new Vector2(eye.x, eye.z));
                int count = Mathf.Min(chunk.MaxTufts, Mathf.CeilToInt(chunk.MaxTufts * Density * DensityAt(distance)) + 8);
                if (count <= 0)
                    continue;

                bool far = MeshLodEnabled && ChooseFar(chunk.Far, distance, Density);
                chunks[i].Far = far;
                renderParams.worldBounds = chunk.Bounds;
                renderParams.matProps = chunk.Properties;
                Graphics.RenderMeshPrimitives(renderParams, far ? farTuftMesh : tuftMesh, 0, count);
                DrawnChunks++;
                if (far) DrawnFarChunks++;
                DrawnTufts += count;
                DrawnTriangles += (long)count * (far ? farTriangles : nearTriangles);
                drawnFlags[i] = true;
            }
        }

        /// <summary>Number of chunks drawn in the last frame whose centre lies in this XZ rectangle.</summary>
        public int CountDrawnIn(Rect area)
        {
            int count = 0;
            if (drawnFlags == null)
                return 0;
            for (int i = 0; i < chunks.Length; i++)
                if (drawnFlags[i] && area.Contains(new Vector2(chunks[i].Bounds.center.x, chunks[i].Bounds.center.z)))
                    count++;
            return count;
        }

        void SetGlobals()
        {
            Shader.SetGlobalVector(GrassParamsId, new Vector4(TallHeight, CutHeight, BladeWidth, Density));
            Shader.SetGlobalVector(GrassLodId, new Vector4(FullDensityDistance, FarDensityDistance, FarDensityFraction, ChunkSize));
            Shader.SetGlobalVector(WindId, new Vector4(WindDirection.x, WindDirection.y, WindStrength, WindSpeed));
            Shader.SetGlobalTexture(WindNoiseId, windNoise);

            int count = 0;
            foreach (IGrassPusher pusher in pushers)
            {
                if (count >= MaxPushers) break;
                if (pusher != null && pusher.TryGetGrassPush(out Vector4 push))
                    pusherData[count++] = push;
            }
            for (int i = count; i < MaxPushers; i++)
                pusherData[i] = Vector4.zero;
            Shader.SetGlobalVectorArray(PushersId, pusherData);
            Shader.SetGlobalFloat(PusherCountId, count);
        }

        /// <summary>Tileable (edges join) two-octave value noise; for the wind waves.</summary>
        static Texture2D BuildWindNoise(int size, int seed)
        {
            var random = new System.Random(seed);
            float[] Lattice(int cells)
            {
                var values = new float[cells * cells];
                for (int i = 0; i < values.Length; i++) values[i] = (float)random.NextDouble();
                return values;
            }
            float Sample(float[] lattice, int cells, float u, float v)
            {
                float x = u * cells, y = v * cells;
                int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
                float fx = x - x0, fy = y - y0;
                fx = fx * fx * (3f - 2f * fx);
                fy = fy * fy * (3f - 2f * fy);
                int x1 = (x0 + 1) % cells, y1 = (y0 + 1) % cells;
                x0 %= cells; y0 %= cells;
                float a = Mathf.Lerp(lattice[y0 * cells + x0], lattice[y0 * cells + x1], fx);
                float b = Mathf.Lerp(lattice[y1 * cells + x0], lattice[y1 * cells + x1], fx);
                return Mathf.Lerp(a, b, fy);
            }

            float[] coarse = Lattice(8), fine = Lattice(24);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size, v = (float)y / size;
                    float n = Sample(coarse, 8, u, v) * 0.7f + Sample(fine, 24, u, v) * 0.3f;
                    byte b = (byte)Mathf.RoundToInt(n * 255f);
                    pixels[y * size + x] = new Color32(b, b, b, 255);
                }
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "WindNoise", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}

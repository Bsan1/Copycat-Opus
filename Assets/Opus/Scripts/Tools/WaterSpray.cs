using System.Collections.Generic;
using UnityEngine;

namespace Opus.Tools
{
    /// <summary>
    /// Hose water (as in the video, 0:19-0:23): a short, puffy, cloud-like light blue-white jet comes out of the nozzle
    /// (whiter on top, bluish underneath, lumpy edges); it travels 30-45 cm, slows down, gathers and disperses. Where the water
    /// lands the grass darkens (LawnState) and thin, faint rings spread outwards and fade.
    /// The jet is soft "puffs" thrown from the front of the nozzle: simulated in C# (slowed by drag, growing, fading),
    /// drawn as camera-facing quads in a single dynamic mesh (Opus/WaterPuff: a soft circle with lumpy edges).
    /// </summary>
    public sealed class WaterSpray : MonoBehaviour
    {
        public const int MaxPuffs = 96;
        const int RippleCount = 4;

        struct Puff
        {
            public Vector3 Position, Velocity;
            public float Age, Life, Size0, Size1, Seed;
        }

        readonly List<Puff> puffs = new List<Puff>(MaxPuffs);
        readonly List<int> order = new List<int>(MaxPuffs);
        readonly System.Random random = new System.Random(57);
        Mesh mesh;
        MeshRenderer meshRenderer;
        Vector3[] vertices;
        Vector2[] uvs;
        Vector2[] data;
        float emitBudget;
        float wobble;

        GroundRing aimRing;
        readonly GroundRing[] ripples = new GroundRing[RippleCount];
        readonly float[] rippleAge = new float[RippleCount];
        readonly Vector3[] rippleCenter = new Vector3[RippleCount];
        float rippleTimer;

        /// <summary>Number of puffs alive right now (for tests and scenarios).</summary>
        public int AliveCount => puffs.Count;
        /// <summary>Distance of the farthest puff from the nozzle (m): the visible length of the jet.</summary>
        public float Reach { get; private set; }
        public int ActiveRipples { get; private set; }

        public static WaterSpray Create(string name)
        {
            var spray = new GameObject(name).AddComponent<WaterSpray>();
            spray.Build();
            return spray;
        }

        void Build()
        {
            vertices = new Vector3[MaxPuffs * 4];
            uvs = new Vector2[MaxPuffs * 4];
            data = new Vector2[MaxPuffs * 4];
            var triangles = new int[MaxPuffs * 6];
            for (int i = 0; i < MaxPuffs; i++)
            {
                int v = i * 4, t = i * 6;
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
            mesh = new Mesh { name = "WaterCloud" };
            mesh.MarkDynamic();
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.uv2 = data;
            mesh.triangles = triangles;
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            Shader shader = Shader.Find("Opus/WaterPuff");
            if (shader != null)
                meshRenderer.sharedMaterial = new Material(shader) { name = "WaterCloud" };
            else
                Debug.LogError("[Opus] Opus/WaterPuff shader not found.");
            meshRenderer.enabled = false;

            aimRing = GroundRing.Create("TargetRing", 0.018f, 0f);
            aimRing.transform.SetParent(transform, false);
            for (int i = 0; i < RippleCount; i++)
            {
                ripples[i] = GroundRing.Create("WaterRing", 0.011f, 0f);
                ripples[i].transform.SetParent(transform, false);
                rippleAge[i] = float.MaxValue;
            }
        }

        float Next() => (float)random.NextDouble();

        /// <summary>
        /// Every frame: sprays the jet from the nozzle (level 0..1), advances the puffs, spreads and draws the rings at the target.
        /// </summary>
        public void Tick(Vector3 tip, Vector3 forward, Vector3 target, SprayMode mode, float level, float deltaTime)
        {
            // Jet: thrown from the front of the nozzle; its direction sways slowly (the water ripples and curls as in the video)
            wobble += deltaTime;
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            side = side.sqrMagnitude > 1e-6f ? side.normalized : Vector3.right;
            Vector3 up = Vector3.Cross(forward, side);
            float size0 = mode.StreamStartRadius * 0.72f;
            float size1 = Mathf.Max(size0 * 1.5f, mode.StreamEndRadius * 0.33f);
            float speed = 2.9f + (0.13f - mode.StreamEndRadius) * 12f;
            float spread = mode.StreamEndRadius * 1.1f;

            emitBudget += 150f * level * deltaTime;
            while (emitBudget >= 1f && puffs.Count < MaxPuffs)
            {
                emitBudget -= 1f;
                Vector3 direction = (forward + side * (Mathf.Sin(wobble * 6.3f) * 0.07f) + up * (Mathf.Sin(wobble * 4.1f + 1f) * 0.05f)).normalized;
                Vector3 jitter = new Vector3(Next() - 0.5f, Next() - 0.5f, Next() - 0.5f);
                puffs.Add(new Puff
                {
                    Position = tip + direction * 0.012f + jitter * 0.006f,
                    Velocity = direction * speed * Mathf.Lerp(0.88f, 1.12f, Next()) + jitter * spread,
                    Life = Mathf.Lerp(0.3f, 0.38f, Next()),
                    Size0 = size0 * Mathf.Lerp(0.85f, 1.15f, Next()),
                    Size1 = size1 * Mathf.Lerp(0.8f, 1.2f, Next()),
                    Seed = Next() * 10f,
                });
            }
            if (level <= 0.01f)
                emitBudget = 0f;

            float drag = Mathf.Exp(-2.9f * deltaTime);
            float reach = 0f;
            for (int i = puffs.Count - 1; i >= 0; i--)
            {
                Puff p = puffs[i];
                p.Age += deltaTime;
                if (p.Age >= p.Life)
                {
                    puffs.RemoveAt(i);
                    continue;
                }
                p.Velocity = p.Velocity * drag + Physics.gravity * (0.3f * deltaTime);
                p.Position += p.Velocity * deltaTime;
                puffs[i] = p;
                // The jet's visible length: almost faded (very faint) puffs do not count
                if (Alpha(p) >= 0.15f)
                    reach = Mathf.Max(reach, Vector3.Distance(p.Position, tip));
            }
            Reach = reach;
            Draw();

            // On the ground: a thin faint ring showing the target and rings spreading outwards as the water lands
            aimRing.Show(target, mode.Radius, 0.14f + 0.06f * level);
            rippleTimer -= deltaTime;
            if (level > 0.3f && rippleTimer <= 0f)
            {
                rippleTimer = 0.6f;
                int slot = 0;
                for (int i = 1; i < RippleCount; i++)
                    if (rippleAge[i] > rippleAge[slot]) slot = i;
                rippleAge[slot] = 0f;
                rippleCenter[slot] = target;
            }
            ActiveRipples = 0;
            const float rippleLife = 1.9f;
            for (int i = 0; i < RippleCount; i++)
            {
                rippleAge[i] += deltaTime;
                float t = rippleAge[i] / rippleLife;
                if (t >= 1f)
                {
                    ripples[i].Hide();
                    continue;
                }
                ActiveRipples++;
                float eased = 1f - (1f - t) * (1f - t);
                ripples[i].Show(rippleCenter[i], mode.Radius * Mathf.Lerp(0.4f, 2.2f, eased), 0.3f * Mathf.Pow(1f - t, 1.4f) * Mathf.Clamp01(t * 8f));
            }
        }

        static float Alpha(Puff p) => Mathf.Clamp01(p.Age / 0.03f) * (1f - Mathf.SmoothStep(0.5f, 1f, p.Age / p.Life)) * 0.95f;

        void Draw()
        {
            if (puffs.Count == 0)
            {
                if (meshRenderer.enabled) meshRenderer.enabled = false;
                return;
            }
            if (!meshRenderer.enabled) meshRenderer.enabled = true;

            // Old (distant) ones first: the near ones stay on top
            order.Clear();
            for (int i = 0; i < puffs.Count; i++) order.Add(i);
            order.Sort((a, b) => puffs[b].Age.CompareTo(puffs[a].Age));

            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), max = -min;
            for (int k = 0; k < MaxPuffs; k++)
            {
                int v = k * 4;
                if (k >= order.Count)
                {
                    for (int c = 0; c < 4; c++) { vertices[v + c] = Vector3.zero; data[v + c] = Vector2.zero; }
                    continue;
                }
                Puff p = puffs[order[k]];
                float t = p.Age / p.Life;
                float size = Mathf.Lerp(p.Size0, p.Size1, Mathf.Pow(Mathf.SmoothStep(0f, 1f, t), 0.7f));
                float alpha = Alpha(p);
                for (int c = 0; c < 4; c++)
                {
                    vertices[v + c] = p.Position;
                    data[v + c] = new Vector2(size, alpha);
                }
                // Corners -1..1; the seed is added to the corner uv as an offset (the shader separates it with frac, the lump pattern differs per puff)
                float seed = Mathf.Floor(p.Seed) * 4f;
                uvs[v] = new Vector2(-1f + seed, -1f); uvs[v + 1] = new Vector2(-1f + seed, 1f);
                uvs[v + 2] = new Vector2(1f + seed, 1f); uvs[v + 3] = new Vector2(1f + seed, -1f);
                min = Vector3.Min(min, p.Position - Vector3.one * size);
                max = Vector3.Max(max, p.Position + Vector3.one * size);
            }
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.uv2 = data;
            mesh.bounds = new Bounds((min + max) * 0.5f, max - min + Vector3.one * 0.1f);
        }

        /// <summary>When the water stops (tool put away): hides the jet and the rings immediately.</summary>
        public void Clear()
        {
            puffs.Clear();
            emitBudget = 0f;
            Reach = 0f;
            if (meshRenderer != null && meshRenderer.enabled) meshRenderer.enabled = false;
            aimRing?.Hide();
            for (int i = 0; i < RippleCount; i++)
            {
                rippleAge[i] = float.MaxValue;
                ripples[i]?.Hide();
            }
            ActiveRipples = 0;
        }
    }
}

using UnityEngine;
using UnityEngine.Rendering;

namespace Opus.Lawn
{
    /// <summary>
    /// Clippings flung from mown grass and the hose's water splashes: small spinning leaf pieces that slow down in the
    /// air, stop when they land and shrink away. Pieces are emitted one by one from outside with <see cref="Emit"/>.
    /// Simulated in C#, drawn with GPU instancing (colour per piece as an instance property). In the ParticleSystem's mesh mode
    /// the vertex colour stream came through broken (red pieces were drawn black) and after a scene reload the instanced drawing
    /// turned into long coloured streaks; that is why the particle system is not used.
    /// </summary>
    public sealed class Clippings : MonoBehaviour
    {
        public const int MaxParticles = 900;
        public const string MaterialResourcePath = "Materials/Clippings";
        const float Gravity = 9.81f * 0.9f;
        const float Drag = 1.6f;
        const float SpeedLimit = 20f;
        const float GroundHeight = 0.02f;
        const float Bounce = 0.05f;
        /// <summary>Touching the ground drops this much of the lifetime (so they do not lie around for long).</summary>
        const float GroundLifeLoss = 0.35f;

        static readonly int ColorId = Shader.PropertyToID("_InstanceColor");

        struct Piece
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public Quaternion Rotation;
            public Vector3 Spin;
            public Color Color;
            public float Size;
            public float Life;
            public float Age;
            public bool Grounded;
        }

        readonly Piece[] pieces = new Piece[MaxParticles];
        readonly Matrix4x4[] matrices = new Matrix4x4[MaxParticles];
        readonly Vector4[] colors = new Vector4[MaxParticles];
        int count;
        int next;
        Mesh mesh;
        Material material;
        MaterialPropertyBlock block;
        System.Random random;

        public int AliveCount => count;
        public int EmittedTotal { get; private set; }

        public static Clippings Create(Transform parent)
        {
            var go = new GameObject("Clippings");
            go.transform.SetParent(parent, false);
            var clippings = go.AddComponent<Clippings>();
            clippings.Build();
            return clippings;
        }

        void Build()
        {
            mesh = BuildBladeQuad();
            block = new MaterialPropertyBlock();
            random = new System.Random(57);
            // Opus/Clipping: reads the instance colour, shaded with the garden's light (made by Setup.CreateClippingMaterial).
            Material template = Resources.Load<Material>(MaterialResourcePath);
            if (template == null)
            {
                Debug.LogError($"[Opus] Clipping material not found: Resources/{MaterialResourcePath}");
                Shader shader = Shader.Find("Opus/Clipping");
                if (shader != null) template = new Material(shader);
            }
            if (template != null)
                material = new Material(template) { name = "Clippings_Scene", enableInstancing = true };
        }

        public void Emit(Vector3 position, Vector3 velocity, Color color, float size, float lifetime)
        {
            float Next() => (float)random.NextDouble();
            // When full, replace the oldest piece (ring buffer)
            int index;
            if (count < MaxParticles)
            {
                index = count++;
            }
            else
            {
                index = next;
                next = (next + 1) % MaxParticles;
            }
            pieces[index] = new Piece
            {
                Position = position,
                Velocity = velocity,
                Rotation = Quaternion.Euler(Next() * 360f, Next() * 360f, Next() * 360f),
                Spin = new Vector3(Mathf.Lerp(-14f, 14f, Next()), Mathf.Lerp(-10f, 10f, Next()), Mathf.Lerp(-14f, 14f, Next())) * Mathf.Rad2Deg,
                Color = color,
                Size = size,
                Life = Mathf.Max(0.05f, lifetime),
            };
            EmittedTotal++;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;
            float drag = Mathf.Exp(-Drag * dt);
            for (int i = 0; i < count; i++)
            {
                ref Piece p = ref pieces[i];
                p.Age += dt;
                if (p.Age >= p.Life)
                {
                    // Move the last one into this slot (order does not matter)
                    pieces[i] = pieces[--count];
                    if (next > count) next = 0;
                    i--;
                    continue;
                }
                if (p.Grounded)
                    continue;
                p.Velocity.y -= Gravity * dt;
                p.Velocity *= drag;
                if (p.Velocity.sqrMagnitude > SpeedLimit * SpeedLimit)
                    p.Velocity = p.Velocity.normalized * SpeedLimit;
                p.Position += p.Velocity * dt;
                p.Rotation *= Quaternion.Euler(p.Spin * dt);
                if (p.Position.y <= GroundHeight)
                {
                    p.Position.y = GroundHeight;
                    if (Mathf.Abs(p.Velocity.y) * Bounce < 0.05f)
                    {
                        p.Grounded = true;
                        p.Age = Mathf.Max(p.Age, p.Life * GroundLifeLoss);
                    }
                    else
                    {
                        p.Velocity = new Vector3(p.Velocity.x * 0.1f, -p.Velocity.y * Bounce, p.Velocity.z * 0.1f);
                    }
                }
            }
        }

        void LateUpdate()
        {
            if (count == 0 || material == null)
                return;
            for (int i = 0; i < count; i++)
            {
                Piece p = pieces[i];
                float t = p.Age / p.Life;
                // Shrink away over the last part
                float scale = p.Size * (t < 0.7f ? 1f : Mathf.Clamp01(1f - (t - 0.7f) / 0.3f));
                matrices[i] = Matrix4x4.TRS(p.Position, p.Rotation, Vector3.one * scale);
                Color linear = p.Color.linear;
                colors[i] = new Vector4(linear.r, linear.g, linear.b, 1f);
            }
            var renderParams = new RenderParams(material)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                matProps = block,
            };
            block.SetVectorArray(ColorId, colors);
            Graphics.RenderMeshInstanced(renderParams, mesh, 0, matrices, count);
        }

        /// <summary>Leaf piece: a narrow quad tapering towards the tip (1 unit long).</summary>
        static Mesh BuildBladeQuad()
        {
            var mesh = new Mesh { name = "ClippingPiece" };
            mesh.SetVertices(new[]
            {
                new Vector3(-0.22f, -0.5f, 0f), new Vector3(0.22f, -0.5f, 0f),
                new Vector3(0.14f, 0.5f, 0f), new Vector3(-0.08f, 0.5f, 0f),
            });
            mesh.SetNormals(new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back });
            mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

using UnityEngine;

namespace Opus.Tools
{
    /// <summary>
    /// Target ring on the ground (unit-radius ring mesh; scaled to the watering radius): thin, dashed or solid. The dashes and showing
    /// above the grass are in the Opus/GroundRing shader: vertices are pulled towards the camera, the screen position does not change.
    /// </summary>
    public sealed class GroundRing : MonoBehaviour
    {
        const int Segments = 96;
        /// <summary>Height above the ground: on top of mown grass.</summary>
        const float Lift = 0.06f;

        static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        static readonly int DashesId = Shader.PropertyToID("_Dashes");

        MeshRenderer meshRenderer;
        MaterialPropertyBlock properties;
        float innerRadius = 0.955f;
        float dashes = 26f;

        /// <param name="thickness">Line thickness as a fraction of the radius.</param>
        /// <param name="dashCount">Number of dashes; 0 = a plain solid line (water ripples).</param>
        public static GroundRing Create(string name, float thickness = 0.045f, float dashCount = 26f)
        {
            var go = new GameObject(name);
            var ring = go.AddComponent<GroundRing>();
            ring.innerRadius = 1f - thickness;
            ring.dashes = dashCount;
            ring.Build();
            return ring;
        }

        void Build()
        {
            var vertices = new Vector3[(Segments + 1) * 2];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[Segments * 6];
            for (int i = 0; i <= Segments; i++)
            {
                float u = (float)i / Segments;
                float angle = u * Mathf.PI * 2f;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[i * 2] = direction * innerRadius;
                vertices[i * 2 + 1] = direction;
                uvs[i * 2] = new Vector2(u, 0f);
                uvs[i * 2 + 1] = new Vector2(u, 1f);
            }
            for (int i = 0, k = 0; i < Segments; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                triangles[k++] = a; triangles[k++] = b; triangles[k++] = c;
                triangles[k++] = c; triangles[k++] = b; triangles[k++] = d;
            }
            var mesh = new Mesh { name = "TargetRing", vertices = vertices, uv = uvs, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            Shader shader = Shader.Find("Opus/GroundRing");
            if (shader != null)
                meshRenderer.sharedMaterial = new Material(shader) { name = "TargetRing" };
            else
                Debug.LogError("[Opus] Opus/GroundRing shader not found.");
            properties = new MaterialPropertyBlock();
        }

        public void Show(Vector3 groundPoint, float radius, float opacity)
        {
            bool visible = opacity > 0.01f;
            if (meshRenderer.enabled != visible)
                meshRenderer.enabled = visible;
            if (!visible)
                return;
            transform.SetPositionAndRotation(new Vector3(groundPoint.x, Lift, groundPoint.z), Quaternion.identity);
            transform.localScale = Vector3.one * radius;
            properties.SetFloat(OpacityId, opacity);
            properties.SetFloat(DashesId, dashes);
            meshRenderer.SetPropertyBlock(properties);
        }

        public void Hide()
        {
            if (meshRenderer.enabled)
                meshRenderer.enabled = false;
        }
    }
}

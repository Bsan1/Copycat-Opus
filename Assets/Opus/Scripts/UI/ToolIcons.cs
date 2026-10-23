using System.Collections.Generic;
using Opus.Core;
using Opus.Tools;
using Opus.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Opus.UI
{
    /// <summary>
    /// Toolbar icons: each tool's simple model is drawn once in a single colour (ink) into a texture with a transparent
    /// background. When the models change, the icons stay up to date by themselves.
    /// </summary>
    public static class ToolIcons
    {
        public const int Size = 160;
        static readonly Vector3 StudioOrigin = new Vector3(0f, -300f, 0f);
        static readonly Color Ink = Palette.Hex("#5E5A54");

        /// <summary>Slot number → icon texture.</summary>
        public static Dictionary<int, Texture> RenderAll()
        {
            var icons = new Dictionary<int, Texture>();
            Material ink = new Material(PrimitiveKit.FlatMaterialFor(Ink)) { name = "IconInk" };
            ink.SetFloat("_Wrap", 0.85f);
            ink.SetFloat("_RimStrength", 0f);

            // View directions: the mower, trimmer and hose from the side (like the drawings in the reference), the hand from the front.
            icons[Tool.MowerSlot] = Render(root => MowerModel.Build(root),
                new Vector3(1f, -0.45f, 0.35f), ink);
            icons[Tool.TrimmerSlot] = Render(root =>
                {
                    TrimmerModel model = TrimmerModel.Build(root);
                    model.Root.localRotation = Quaternion.Euler(-35f, 0f, 0f);
                    model.Head.localRotation = Quaternion.Euler(35f, 0f, 0f);
                },
                new Vector3(1f, -0.2f, 0.1f), ink);
            icons[Tool.HoseSlot] = Render(root => HoseModel.Build(root).Root.localRotation = Quaternion.Euler(-25f, 0f, 0f),
                new Vector3(1f, -0.15f, 0.2f), ink);
            icons[Tool.HandSlot] = Render(BuildHand, new Vector3(0.05f, -0.1f, 1f), ink);
            Object.Destroy(ink);
            return icons;
        }

        /// <param name="viewDirection">Direction the camera looks in; the model is framed automatically from its bounds.</param>
        static Texture Render(System.Action<Transform> build, Vector3 viewDirection, Material ink)
        {
            var root = new GameObject("IconStudio").transform;
            root.position = StudioOrigin;
            build(root);
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(StudioOrigin, Vector3.one);
            foreach (Renderer renderer in renderers)
            {
                // In merged models every submesh has its own material: make them all ink
                var inks = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
                for (int i = 0; i < inks.Length; i++) inks[i] = ink;
                renderer.sharedMaterials = inks;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                bounds.Encapsulate(renderer.bounds);
            }

            var cameraObject = new GameObject("IconCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 20f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            Frame(camera, bounds, viewDirection.normalized);
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderShadows = false;
            data.renderPostProcessing = false;

            var target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { name = "ToolIcon", antiAliasing = 4 };
            target.Create();
            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;
            var request = new RenderPipeline.StandardRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(camera, request))
                RenderPipeline.SubmitRenderRequest(camera, request);
            else
                Debug.LogWarning("[Opus] Could not draw the icon: render requests are not supported.");
            RenderSettings.fog = fog;

            // Destroy right away: Destroy would wait for the end of the frame and the next icon would be drawn on top of this model.
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(root.gameObject);
            return target;
        }

        /// <summary>Places the orthographic camera so that the bounding box corners fit the image with a 10% margin.</summary>
        static void Frame(Camera camera, Bounds bounds, Vector3 viewDirection)
        {
            camera.transform.rotation = Quaternion.LookRotation(viewDirection, Vector3.up);
            camera.transform.position = bounds.center - viewDirection * (bounds.extents.magnitude + 2f);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 local = camera.transform.InverseTransformPoint(corner);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
            Vector2 center = (min + max) * 0.5f;
            camera.transform.position += camera.transform.right * center.x + camera.transform.up * center.y;
            camera.orthographicSize = Mathf.Max(max.x - min.x, max.y - min.y) * 0.5f * 1.1f;
        }

        /// <summary>Open hand (palm, four fingers, thumb): only for the icon.</summary>
        static void BuildHand(Transform root)
        {
            Color c = Color.white;
            PrimitiveKit.Box(root, "Palm", new Vector3(0f, 0.06f, 0f), new Vector3(0.09f, 0.1f, 0.025f), c, collider: false);
            float[] lengths = { 0.07f, 0.085f, 0.08f, 0.062f };
            for (int i = 0; i < 4; i++)
            {
                float x = -0.033f + i * 0.022f;
                float tilt = (i - 1.5f) * 6f * Mathf.Deg2Rad;
                var from = new Vector3(x, 0.105f, 0f);
                var to = from + new Vector3(Mathf.Sin(tilt), Mathf.Cos(tilt), 0f) * lengths[i];
                PrimitiveKit.Tube(root, "Finger", from, to, 0.0095f, c);
                PrimitiveKit.Sphere(root, "Fingertip", to, Vector3.one * 0.019f, c);
            }
            var thumbFrom = new Vector3(0.04f, 0.04f, 0f);
            var thumbTo = new Vector3(0.085f, 0.085f, 0f);
            PrimitiveKit.Tube(root, "Thumb", thumbFrom, thumbTo, 0.011f, c);
            PrimitiveKit.Sphere(root, "ThumbTip", thumbTo, Vector3.one * 0.021f, c);
            PrimitiveKit.Box(root, "Wrist", new Vector3(0f, -0.01f, 0f), new Vector3(0.07f, 0.05f, 0.022f), c, collider: false);
        }
    }
}

using System.IO;
using System.Linq;
using Opus.Visual;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Opus.Editor
{
    /// <summary>
    /// Model showroom scene (Assets/Opus/Scenes/ModelShowroom.unity): for inspecting every model in the editor, without pressing Play,
    /// in the game's light. Only the <see cref="ModelShowroom"/> object, the game camera and the light/fog settings are saved
    /// in the scene; the models are built when the scene opens. Not part of the build (only Main.unity is built).
    /// Command line: tools/Showroom.ps1 (builds the scene, captures the editor view into Builds/Shots/model_showroom).
    /// </summary>
    public static class ModelShowroomScene
    {
        public const string ScenePath = "Assets/Opus/Scenes/ModelShowroom.unity";
        public const string SkyPath = "Assets/Opus/Scenes/ModelShowroom_Sky.mat";
        const string ShotFolder = "Builds/Shots/model_showroom";

        [MenuItem("Opus/Build the model showroom scene")]
        static void CreateFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                CreateInternal();
        }

        public static void Create() => BatchUtil.Run("Model showroom scene", CreateInternal);

        public static void Capture() => BatchUtil.Run("Model showroom shots", CaptureInternal);

        static void CreateInternal()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Atmosphere.ApplyRenderSettings();
            AssetDatabase.DeleteAsset(SkyPath);
            Material sky = Atmosphere.CreateSkyMaterial();
            AssetDatabase.CreateAsset(sky, SkyPath);
            RenderSettings.skybox = sky;

            var showroom = new GameObject("ModelShowroom").AddComponent<ModelShowroom>();
            if (showroom.Items.Count == 0)
                showroom.Rebuild();

            // The game's camera for the Game view (colour grading on), facing the row of models
            Bounds all = showroom.Items[0].Bounds;
            foreach (ModelShowroom.Item item in showroom.Items)
                all.Encapsulate(item.Bounds);
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 600f;
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            camera.transform.position = new Vector3(all.center.x * 0.35f, 9f, all.min.z - 22f);
            camera.transform.LookAt(new Vector3(all.center.x * 0.35f, 1.5f, all.center.z));

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("Could not save the scene: " + ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Opus] Model showroom: {showroom.Items.Count} models, {ScenePath}");
        }

        /// <summary>Opens the scene and captures every model from two sides as it looks in the editor (light, colour grading, fog).</summary>
        static void CaptureInternal()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ModelShowroom showroom = Object.FindFirstObjectByType<ModelShowroom>();
            if (showroom == null)
                throw new System.InvalidOperationException("No ModelShowroom in the scene");
            if (showroom.Items.Count == 0)
                showroom.Rebuild();

            string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, ShotFolder);
            Directory.CreateDirectory(folder);
            foreach (string old in Directory.GetFiles(folder, "*.png"))
                File.Delete(old);

            var cameraObject = new GameObject("CaptureCamera") { hideFlags = HideFlags.DontSave };
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.02f;
            camera.farClipPlane = 600f;
            cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            var target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            camera.targetTexture = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                for (int i = 0; i < showroom.Items.Count; i++)
                {
                    ModelShowroom.Item item = showroom.Items[i];
                    foreach ((string side, Vector3 direction) in new[] { ("garden_side", new Vector3(-0.6f, 0.4f, -1f)), ("far_side", new Vector3(0.7f, 0.45f, 1f)) })
                    {
                        float radius = Mathf.Max(item.Bounds.extents.magnitude, 0.1f);
                        float distance = radius / Mathf.Sin(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;
                        camera.transform.position = item.Bounds.center + direction.normalized * distance;
                        camera.transform.LookAt(item.Bounds.center);
                        camera.Render();
                        RenderTexture.active = target;
                        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                        image.Apply();
                        RenderTexture.active = null;
                        string file = Path.Combine(folder, $"{i + 1:00}_{Slug(item.Name)}_{side}.png");
                        File.WriteAllBytes(file, image.EncodeToPNG());
                    }
                }
            }
            finally
            {
                camera.targetTexture = null;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(cameraObject);
            }
            Debug.Log($"[Opus] Model showroom: {showroom.Items.Count * 2} shots, {ShotFolder}");
        }

        static string Slug(string name)
        {
            return new string(name.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        }
    }
}

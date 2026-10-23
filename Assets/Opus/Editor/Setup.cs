using System;
using System.IO;
using Opus.Core;
using Opus.Visual;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Opus.Editor
{
    /// <summary>
    /// Sets the project up without opening the editor. Used in two separate Unity runs:
    /// 1) ConfigureProject: player/input/URP settings, TMP essential resources (the input setting needs a restart).
    /// 2) GenerateContent: the font asset and the code-first main scene. Build.Windows calls it itself too.
    /// </summary>
    public static class Setup
    {
        public const string SettingsFolder = "Assets/Opus/Settings";
        public const string PipelineAssetPath = SettingsFolder + "/OpusURP.asset";
        public const string RendererAssetPath = SettingsFolder + "/OpusURP_Renderer.asset";
        public const string ScenePath = "Assets/Opus/Scenes/Main.unity";
        public const string FontSourcePath = "Assets/Opus/Fonts/PatrickHand-Regular.ttf";
        public const string FontAssetPath = "Assets/Opus/Resources/Fonts/PatrickHand SDF.asset";
        public const string TmpEssentialsFolder = "Assets/TextMesh Pro";
        /// <summary>Clipping material: URP's particle shader (part of the build because it is in Resources).</summary>
        public const string ClippingMaterialPath = "Assets/Opus/Resources/Materials/Clippings.mat";
        /// <summary>
        /// GPU-instanced Opus/Primitive material. The build strips instancing shader variants no material asks for;
        /// this asset gets the variants into the exe (the flowers instance it).
        /// </summary>
        public const string InstancedMaterialPath = "Assets/Opus/Resources/Materials/PrimitiveInstanced.mat";
        public const int InputSystemOnly = 1;

        /// <summary>Characters written into the font atlas in advance (the rest are added dynamically at runtime).</summary>
        public const string Charset =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
            "·–—…‘’“”";

        [MenuItem("Opus/1 Configure the project")]
        public static void ConfigureProject() => BatchUtil.Run("Project setup", ConfigureProjectInternal);

        [MenuItem("Opus/2 Generate content (font, scene)")]
        public static void GenerateContent() => BatchUtil.Run("Content generation", GenerateContentInternal);

        internal static void ConfigureProjectInternal()
        {
            ConfigurePlayer();
            ConfigureInputHandling();
            ConfigureLayers();
            ConfigurePipeline();
            ImportTmpEssentials();
            AssetDatabase.SaveAssets();
        }

        internal static void GenerateContentInternal()
        {
            if (!AssetDatabase.IsValidFolder(TmpEssentialsFolder))
                throw new InvalidOperationException("TMP Essential Resources missing; run Opus.Editor.Setup.ConfigureProject first.");
            CreateFontAsset();
            CreateClippingMaterial();
            CreateInstancedMaterial();
            CreateSurfaceTextures();
            ModelPrefabs.BuildAll();   // one prefab per model, with its materials as assets (after the surface textures they use)
            CreateScene();
            AssetDatabase.SaveAssets();
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Baris";
            PlayerSettings.productName = "Copycat Opus";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.SplashScreen.show = false;
            // So the scenarios can split the frame time into CPU/GPU (FrameTimingManager)
            PlayerSettings.enableFrameTimingStats = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        }

        static void ConfigureInputHandling()
        {
            UnityEngine.Object[] settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settings == null || settings.Length == 0)
                throw new InvalidOperationException("Could not read ProjectSettings.asset.");

            var serialized = new SerializedObject(settings[0]);
            SerializedProperty handler = serialized.FindProperty("activeInputHandler");
            if (handler == null)
                throw new InvalidOperationException("activeInputHandler setting not found.");
            if (handler.intValue != InputSystemOnly)
            {
                handler.intValue = InputSystemOnly;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[Opus] Input: new Input System only (active from the next launch).");
            }
        }

        /// <summary>Gives the layer numbers used in code (Opus.Core.Layers) the names shown in the editor.</summary>
        static void ConfigureLayers()
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
                throw new InvalidOperationException("Could not read TagManager.asset.");
            var serialized = new SerializedObject(assets[0]);
            SerializedProperty layers = serialized.FindProperty("layers");
            layers.GetArrayElementAtIndex(Layers.Player).stringValue = "Player";
            layers.GetArrayElementAtIndex(Layers.Tool).stringValue = "Tool";
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigurePipeline()
        {
            EnsureFolder(SettingsFolder);
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
            if (pipeline == null)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                    "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
                renderer.renderingMode = RenderingMode.Forward;
                AssetDatabase.CreateAsset(renderer, RendererAssetPath);

                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelineAssetPath);
            }

            pipeline.supportsHDR = true;
            pipeline.msaaSampleCount = 4;
            pipeline.renderScale = 1f;
            pipeline.shadowDistance = 70f;
            pipeline.shadowCascadeCount = 2;
            pipeline.supportsCameraDepthTexture = true;

            var serialized = new SerializedObject(pipeline);
            SetBool(serialized, "m_MainLightShadowsSupported", true);
            SetBool(serialized, "m_SoftShadowsSupported", true);
            SetInt(serialized, "m_MainLightShadowmapResolution", 4096);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);

            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);
        }

        // AssetDatabase.ImportPackage and -importPackage work asynchronously; with -quit they stop halfway.
        // So the package is unpacked to disk with UnityPackageExtractor and imported synchronously.
        static void ImportTmpEssentials()
        {
            if (AssetDatabase.IsValidFolder(TmpEssentialsFolder))
                return;

            PackageInfo ugui = PackageInfo.FindForAssetPath("Packages/com.unity.ugui/package.json");
            if (ugui == null)
                throw new InvalidOperationException("The com.unity.ugui package is not resolved.");
            string package = Path.Combine(ugui.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
            if (!File.Exists(package))
                throw new FileNotFoundException("TMP Essential Resources package not found.", package);

            int written = UnityPackageExtractor.Extract(package, Directory.GetCurrentDirectory());
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (!AssetDatabase.IsValidFolder(TmpEssentialsFolder))
                throw new InvalidOperationException("The TMP essential resources were unpacked but could not be imported.");
            Debug.Log($"[Opus] TMP essential resources imported ({written} files).");
        }

        static void CreateFontAsset()
        {
            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (fontAsset == null)
            {
                var font = AssetDatabase.LoadAssetAtPath<Font>(FontSourcePath);
                if (font == null)
                    throw new FileNotFoundException("Font file missing.", FontSourcePath);

                EnsureFolder(Path.GetDirectoryName(FontAssetPath).Replace('\\', '/'));
                fontAsset = TMP_FontAsset.CreateFontAsset(font, 72, 8, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, false);
                if (fontAsset == null)
                    throw new InvalidOperationException("Could not create the TMP font asset.");

                fontAsset.name = "PatrickHand SDF";
                Shader fullSdf = Shader.Find("TextMeshPro/Distance Field");
                if (fullSdf != null)
                    fontAsset.material.shader = fullSdf;

                AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
                fontAsset.atlasTexture.name = "PatrickHand SDF Atlas";
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
                fontAsset.material.name = "PatrickHand SDF Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            if (!fontAsset.TryAddCharacters(Charset, out string missing))
                Debug.LogWarning($"[Opus] Characters missing from the font: {missing}");
            EditorUtility.SetDirty(fontAsset);
            EditorUtility.SetDirty(fontAsset.atlasTexture);
        }

        /// <summary>
        /// Material of the clipping particles: Opus/Clipping, reading the vertex colour, double-sided, shaded with the garden's light.
        /// </summary>
        static void CreateClippingMaterial()
        {
            Shader shader = Shader.Find("Opus/Clipping");
            if (shader == null)
                throw new InvalidOperationException("Opus/Clipping shader not found.");

            var material = AssetDatabase.LoadAssetAtPath<Material>(ClippingMaterialPath);
            if (material == null)
            {
                EnsureFolder(Path.GetDirectoryName(ClippingMaterialPath).Replace('\\', '/'));
                material = new Material(shader) { name = "Clippings" };
                AssetDatabase.CreateAsset(material, ClippingMaterialPath);
            }
            material.shader = shader;
            material.SetFloat("_Wrap", 0.6f);
            // The build only keeps the instancing variant if a material asks for it (the clippings are drawn with GPU instancing)
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
        }

        static void CreateInstancedMaterial()
        {
            Shader shader = Shader.Find(Opus.World.PrimitiveKit.ShaderName);
            if (shader == null)
                throw new InvalidOperationException("Opus/Primitive shader not found.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(InstancedMaterialPath);
            if (material == null)
            {
                EnsureFolder(Path.GetDirectoryName(InstancedMaterialPath).Replace('\\', '/'));
                material = new Material(shader) { name = "PrimitiveInstanced" };
                AssetDatabase.CreateAsset(material, InstancedMaterialPath);
            }
            material.shader = shader;
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
        }

        /// <summary>
        /// Writes the painted surface patterns into Resources as PNGs (linear, tiling, mipmapped). The version is kept in the importer;
        /// if the generation code has not changed, the existing files are left alone.
        /// </summary>
        static void CreateSurfaceTextures()
        {
            string folder = SurfaceTextureFolder;
            EnsureFolder(folder);
            string stamp = "v" + SurfaceTextures.Version;
            int written = 0;
            foreach (string name in SurfaceTextures.Names)
            {
                string path = $"{folder}/{name}.png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null && importer.userData == stamp)
                    continue;

                int size = SurfaceTextures.SizeOf(name);
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
                texture.SetPixels32(SurfaceTextures.Generate(name));
                texture.Apply(false, false);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

                importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 8;
                importer.maxTextureSize = size;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.userData = stamp;
                importer.SaveAndReimport();
                written++;
            }
            if (written > 0)
                Debug.Log($"[Opus] Surface patterns written: {written}");
        }

        public const string SurfaceTextureFolder = "Assets/Opus/Resources/" + SurfaceTextures.ResourceFolder;

        static void CreateScene()
        {
            EnsureFolder("Assets/Opus/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Bootstrap").AddComponent<GameBootstrap>();

            // The fog setting must be saved in the scene; otherwise the build strips the fog shader variants.
            Atmosphere.ApplyRenderSettings();
            RenderSettings.skybox = null;

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("Could not save the scene: " + ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void SetBool(SerializedObject serialized, string name, bool value)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null) Debug.LogWarning($"[Opus] URP setting not found: {name}");
            else property.boolValue = value;
        }

        static void SetInt(SerializedObject serialized, string name, int value)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null) Debug.LogWarning($"[Opus] URP setting not found: {name}");
            else property.intValue = value;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}

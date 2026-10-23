using Opus.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Opus.Visual
{
    /// <summary>The sun, ambient light (shader globals), fog and the lavender sky.</summary>
    public static class Atmosphere
    {
        public const string SkyShaderName = "Opus/Sky";

        static readonly int SkyAmbientId = Shader.PropertyToID("_OpusSkyAmbient");
        static readonly int GroundAmbientId = Shader.PropertyToID("_OpusGroundAmbient");
        static readonly int ShadowTintId = Shader.PropertyToID("_OpusShadowTint");

        public const float FogStart = 12f;
        public const float FogEnd = 100f;

        public static Light Sun { get; private set; }
        public static Material Sky { get; private set; }

        public static void Apply()
        {
            ApplyRenderSettings();
            SetShaderGlobals();
            Material sky = CreateSkyMaterial();
            if (sky != null)
            {
                RenderSettings.skybox = sky;
                Sky = sky;
            }
            CreateSun();
        }

        /// <summary>Ambient and shadow colours (read by Opus/Surface and the other shaders).</summary>
        public static void SetShaderGlobals()
        {
            ShaderColors.SetGlobal(SkyAmbientId, Palette.AmbientSky);
            ShaderColors.SetGlobal(GroundAmbientId, Palette.AmbientGround);
            ShaderColors.SetGlobal(ShadowTintId, Palette.ShadowTint);
        }

        /// <summary>Lavender sky material (null if the shader is missing).</summary>
        public static Material CreateSkyMaterial()
        {
            Shader skyShader = Shader.Find(SkyShaderName);
            if (skyShader == null)
            {
                Debug.LogError($"[Opus] {SkyShaderName} shader not found.");
                return null;
            }
            var sky = new Material(skyShader) { name = "Sky" };
            sky.SetColor("_ZenithColor", Palette.SkyZenith);
            sky.SetColor("_HorizonColor", Palette.SkyHorizon);
            sky.SetColor("_GroundColor", Palette.SkyGround);
            sky.SetColor("_CloudColor", Palette.CloudLight);
            sky.SetColor("_CloudShadow", Palette.CloudShadow);
            sky.SetColor("_HazeColor", Palette.HorizonHaze);
            return sky;
        }

        /// <summary>
        /// Also written into the scene file: Unity strips unused fog variants from builds,
        /// so the fog setting has to be saved in the scene.
        /// </summary>
        public static void ApplyRenderSettings()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Palette.Fog;
            RenderSettings.fogStartDistance = FogStart;
            RenderSettings.fogEndDistance = FogEnd;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Palette.AmbientSky;
        }

        /// <summary>The game's sun: colour, intensity, soft shadow, direction.</summary>
        public static void ConfigureSun(Light light)
        {
            light.type = LightType.Directional;
            light.color = Palette.Sun;
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.9f;
            light.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
        }

        static void CreateSun()
        {
            var go = new GameObject("Sun");
            var light = go.AddComponent<Light>();
            ConfigureSun(light);
            RenderSettings.sun = light;
            Sun = light;
        }
    }
}

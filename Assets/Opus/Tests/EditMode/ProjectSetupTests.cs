using System.Linq;
using NUnit.Framework;
using Opus.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Opus.Tests
{
    /// <summary>Checks that the project settings set up without opening the editor are in place.</summary>
    public class ProjectSetupTests
    {
        [Test]
        public void UrpIsDefaultPipeline()
        {
            Assert.IsInstanceOf<UniversalRenderPipelineAsset>(GraphicsSettings.defaultRenderPipeline);
        }

        [Test]
        public void ColorSpaceIsLinear()
        {
            Assert.AreEqual(ColorSpace.Linear, PlayerSettings.colorSpace);
        }

        [Test]
        public void OnlyNewInputSystemIsActive()
        {
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            Assert.AreEqual(Setup.InputSystemOnly, settings.FindProperty("activeInputHandler").intValue);
        }

        [TestCase("Opus/Primitive")]
        [TestCase("Opus/Surface")]
        [TestCase("Opus/Clipping")]
        [TestCase("Opus/Sky")]
        [TestCase("Opus/Grass")]
        [TestCase("Opus/LawnGround")]
        [TestCase("Opus/WaterPuff")]
        [TestCase("Opus/GroundRing")]
        [TestCase("Opus/PaintedCard")]
        public void ShaderIsFoundWithoutErrors(string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            Assert.IsNotNull(shader, shaderName);
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader), $"{shaderName} has compile errors.");
        }

        [Test]
        public void HandFontHasInterfaceGlyphs()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Setup.FontAssetPath);
            Assert.IsNotNull(font, Setup.FontAssetPath);
            string missing = new string("·–—…‘’“”".Where(c => !font.HasCharacter(c, false, true)).ToArray());
            Assert.IsEmpty(missing, "Missing characters: " + missing);
        }

        [Test]
        public void MainSceneIsTheOnlyBuildScene()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            Assert.AreEqual(1, scenes.Length);
            Assert.AreEqual(Setup.ScenePath, scenes[0].path);
            Assert.IsTrue(scenes[0].enabled);
        }
    }
}

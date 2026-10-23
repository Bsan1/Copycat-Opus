using Opus.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Opus.Visual
{
    /// <summary>
    /// The soft, slightly washed-out look of the reference: soft low-threshold bloom, low contrast, a little desaturation,
    /// lavender shadow / warm light split toning and a light vignette. The global Volume is built from code.
    /// </summary>
    public static class PostFx
    {
        public static Volume Volume { get; private set; }

        public static Volume Create(Transform parent)
        {
            var go = new GameObject("PostProcess");
            go.transform.SetParent(parent, false);
            Volume volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;

            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "GardenView";

            Bloom bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.3f);
            bloom.scatter.Override(0.75f);
            bloom.tint.Override(Palette.Hex("#FFF4EC"));

            ColorAdjustments color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(-0.25f);
            color.contrast.Override(-6f);
            color.saturation.Override(10f);

            SplitToning split = profile.Add<SplitToning>(true);
            split.shadows.Override(Palette.GradeShadows);
            split.highlights.Override(Palette.GradeHighlights);
            split.balance.Override(15f);

            Vignette vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.16f);
            vignette.smoothness.Override(0.55f);
            vignette.color.Override(Palette.Hex("#5B5470"));

            Tonemapping tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.None);

            volume.sharedProfile = profile;
            Volume = volume;
            return volume;
        }
    }
}

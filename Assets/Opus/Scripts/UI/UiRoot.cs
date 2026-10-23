using Opus.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Opus.UI
{
    /// <summary>
    /// Interface canvas. "Screen Space – Camera" + URP Overlay camera: the interface is drawn into the world camera's
    /// target (the screen or the scenario RenderTexture), so it shows in the automated screenshots too.
    /// Its content is built by <see cref="Hud"/>.
    /// </summary>
    public sealed class UiRoot : MonoBehaviour
    {
        public RectTransform Root { get; private set; }

        public static UiRoot Create(Camera uiCamera)
        {
            var go = new GameObject("Interface") { layer = Layers.UI };

            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = uiCamera;
            canvas.planeDistance = 1f;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            UiRoot root = go.AddComponent<UiRoot>();
            root.Root = (RectTransform)go.transform;
            return root;
        }
    }
}

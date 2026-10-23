using TMPro;
using UnityEngine;

namespace Opus.UI
{
    public static class UiFonts
    {
        /// <summary>Path under Resources; the editor setup (Opus.Editor.Setup) generates this file.</summary>
        public const string HandResourcePath = "Fonts/PatrickHand SDF";

        static TMP_FontAsset hand;

        /// <summary>The interface's handwritten font (Patrick Hand, OFL).</summary>
        public static TMP_FontAsset Hand
        {
            get
            {
                if (hand == null)
                {
                    hand = Resources.Load<TMP_FontAsset>(HandResourcePath);
                    if (hand == null)
                    {
                        Debug.LogError($"[Opus] Font not found: Resources/{HandResourcePath}");
                        hand = TMP_Settings.defaultFontAsset;
                    }
                }
                return hand;
            }
        }
    }
}

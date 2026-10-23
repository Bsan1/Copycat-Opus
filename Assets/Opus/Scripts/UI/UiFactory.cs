using Opus.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Opus.UI
{
    /// <summary>Building blocks of the paper card interface from the reference.</summary>
    public static class UiFactory
    {
        public const int CardRadius = 14;

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = Layers.UI;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>
        /// A shadowed paper card with tape. The returned object's content is stacked vertically and its height fits the content.
        /// </summary>
        public static RectTransform Card(Transform parent, string name, float width, RectOffset padding, bool tape = true)
        {
            RectTransform card = Rect(parent, name);
            card.sizeDelta = new Vector2(width, 100f);

            RectTransform shadow = Rect(card, "Shadow");
            Stretch(shadow, new Vector2(-10f, -16f), new Vector2(10f, 4f));
            Ignore(shadow);
            Image shadowImage = shadow.gameObject.AddComponent<Image>();
            shadowImage.sprite = UiSprites.SoftShadow(CardRadius, 12);
            shadowImage.type = Image.Type.Sliced;
            shadowImage.color = new Color(0.25f, 0.2f, 0.3f, 0.22f);
            shadowImage.raycastTarget = false;

            RectTransform paper = Rect(card, "Paper");
            Stretch(paper, Vector2.zero, Vector2.zero);
            Ignore(paper);
            Image paperImage = paper.gameObject.AddComponent<Image>();
            paperImage.sprite = UiSprites.RoundedRect(CardRadius);
            paperImage.type = Image.Type.Sliced;
            paperImage.color = Palette.Paper;
            paperImage.raycastTarget = false;

            if (tape)
            {
                RectTransform tapeRect = Rect(card, "Band");
                tapeRect.anchorMin = tapeRect.anchorMax = new Vector2(0.5f, 1f);
                tapeRect.sizeDelta = new Vector2(78f, 22f);
                tapeRect.anchoredPosition = new Vector2(0f, 2f);
                tapeRect.localRotation = Quaternion.Euler(0f, 0f, -2f);
                Ignore(tapeRect);
                Image tapeImage = tapeRect.gameObject.AddComponent<Image>();
                tapeImage.sprite = UiSprites.Tape();
                tapeImage.color = Palette.Tape;
                tapeImage.raycastTarget = false;
            }

            VerticalLayoutGroup layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = padding;
            layout.spacing = 4f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = card.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return card;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string value, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
        {
            RectTransform rect = Rect(parent, name);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = UiFonts.Hand;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            text.text = value;
            return text;
        }

        /// <summary>Thin divider line (under a heading inside a card).</summary>
        public static Image Divider(Transform parent)
        {
            RectTransform rect = Rect(parent, "Divider");
            LayoutElement element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 2f;
            element.minHeight = 2f;
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.12f);
            image.raycastTarget = false;
            return image;
        }

        public static void Anchor(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
        }

        static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        static void Ignore(RectTransform rect)
        {
            rect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Opus.UI
{
    /// <summary>9-sliced sprites generated from code for the paper card look.</summary>
    public static class UiSprites
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>Sharp-edged rounded rectangle (card background).</summary>
        public static Sprite RoundedRect(int radius) => Rounded($"rr{radius}", radius, 0.75f);

        /// <summary>Soft, blurry-edged rounded rectangle (card shadow).</summary>
        public static Sprite SoftShadow(int radius, int blur) => Rounded($"sh{radius}_{blur}", radius, blur);

        /// <summary>Filled circle with a softened edge (not sliced; width/height can differ for an ellipse).</summary>
        public static Sprite Circle() => Disc("circle", 128, 0f);

        /// <summary>Ring (for radial fill): thickness as a fraction of the radius.</summary>
        public static Sprite Ring(float thickness) => Disc($"ring{thickness:0.00}", 128, thickness);

        /// <summary>Five-pointed star with slightly rounded points (job rating); edges smoothed with 4×4 sampling.</summary>
        public static Sprite Star()
        {
            const string key = "star";
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null)
                return cached;
            const int size = 128;
            var points = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float angle = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float radius = i % 2 == 0 ? size * 0.48f : size * 0.21f;
                points[i] = new Vector2(size * 0.5f + Mathf.Cos(angle) * radius, size * 0.52f + Mathf.Sin(angle) * radius);
            }
            var tex = NewTexture(size, size, key);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int inside = 0;
                    for (int sy = 0; sy < 4; sy++)
                        for (int sx = 0; sx < 4; sx++)
                            if (InPolygon(points, new Vector2(x + (sx + 0.5f) / 4f, y + (sy + 0.5f) / 4f))) inside++;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(inside * 255 / 16));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = key;
            Cache[key] = sprite;
            return sprite;
        }

        static bool InPolygon(Vector2[] polygon, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                if ((polygon[i].y > p.y) != (polygon[j].y > p.y)
                    && p.x < (polygon[j].x - polygon[i].x) * (p.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x)
                    inside = !inside;
            }
            return inside;
        }

        static Sprite Disc(string key, int size, float ringThickness)
        {
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null)
                return cached;
            var tex = NewTexture(size, size, key);
            var pixels = new Color32[size * size];
            float radius = size * 0.5f - 1f;
            float inner = ringThickness > 0f ? radius * (1f - ringThickness) : -1f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                    float alpha = 1f - SmoothStep(radius - 0.75f, radius + 0.75f, d);
                    if (inner > 0f)
                        alpha *= SmoothStep(inner - 0.75f, inner + 0.75f, d);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = key;
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>Washi tape: translucent, finely striped, slightly torn edges.</summary>
        public static Sprite Tape()
        {
            const string key = "tape";
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null)
                return cached;

            const int w = 96, h = 28;
            var tex = NewTexture(w, h, key);
            var pixels = new Color32[w * h];
            var random = new System.Random(3);
            var tornLeft = new int[h];
            var tornRight = new int[h];
            for (int y = 0; y < h; y++)
            {
                tornLeft[y] = random.Next(0, 4);
                tornRight[y] = random.Next(0, 4);
            }
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool inside = x >= tornLeft[y] && x < w - tornRight[y];
                    bool stripe = ((x + y) / 5) % 2 == 0;
                    byte a = inside ? (byte)(stripe ? 200 : 170) : (byte)0;
                    byte v = stripe ? (byte)255 : (byte)238;
                    pixels[y * w + x] = new Color32(v, v, v, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = key;
            Cache[key] = sprite;
            return sprite;
        }

        static Sprite Rounded(string key, int radius, float softness)
        {
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null)
                return cached;

            int pad = Mathf.CeilToInt(softness) + 1;
            int border = radius + pad;
            int size = border * 2 + 2;
            var tex = NewTexture(size, size, key);
            var pixels = new Color32[size * size];

            float half = size * 0.5f;
            float innerHalf = half - pad - radius;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Signed distance to the rounded rectangle (negative inside).
                    float qx = Mathf.Abs(x + 0.5f - half) - innerHalf;
                    float qy = Mathf.Abs(y + 0.5f - half) - innerHalf;
                    float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                    float distance = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    float alpha = 1f - SmoothStep(-softness, softness, distance);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            sprite.name = key;
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>GLSL smoothstep: a smooth 0→1 transition of x over a..b (Mathf.SmoothStep works differently).</summary>
        public static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        static Texture2D NewTexture(int w, int h, string name)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
        }
    }
}

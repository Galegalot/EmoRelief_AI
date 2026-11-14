using System.Collections.Generic;
using UnityEngine;

namespace EmoRelief
{
    // Paints persistent marks (bruises, cuts, splats, drips) into the face's base colour map.
    public sealed class SkinPainter
    {
        public const int Width = 512;
        public const int Height = 256;
        // Approximate texels per world unit on the front of the face.
        public const float UnitX = Width / (2f * Mathf.PI);
        public const float UnitY = Height / (Mathf.PI * 1.26f);

        public Texture2D Texture { get; }
        public int ActiveDrips => drips.Count;

        private sealed class Drip
        {
            public float X;
            public float Y;
            public float Remaining;
            public float Speed;
            public float Radius;
            public float Phase;
            public Color Color;
            public float Opacity;
        }

        private readonly Color32[] pixels = new Color32[Width * Height];
        private readonly List<Drip> drips = new List<Drip>();
        private readonly System.Random random = new System.Random(19);
        private Color32 skin;
        private bool dirty;

        public SkinPainter()
        {
            Texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                name = "Face damage map",
                wrapModeU = TextureWrapMode.Repeat,
                wrapModeV = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
        }

        public void Clear(Color tone)
        {
            skin = tone;
            for (var index = 0; index < pixels.Length; index++) pixels[index] = skin;
            drips.Clear();
            dirty = true;
            Flush();
        }

        public int CountMarked(int threshold = 24)
        {
            var count = 0;
            foreach (var pixel in pixels)
                if (Mathf.Abs(pixel.r - skin.r) + Mathf.Abs(pixel.g - skin.g) + Mathf.Abs(pixel.b - skin.b) > threshold) count++;
            return count;
        }

        // Offset a UV by a distance measured in face units (x right, y up).
        public static Vector2 Offset(Vector2 uv, float x, float y) => uv + new Vector2(x * UnitX / Width, -y * UnitY / Height);

        public float Random(float min, float max) => min + (float)random.NextDouble() * (max - min);

        public void Blob(Vector2 uv, float radiusX, float radiusY, Color color, float opacity, float hardness = 0f, float angle = 0f)
        {
            var cx = uv.x * Width;
            var cy = uv.y * Height;
            var reach = Mathf.Max(radiusX, radiusY);
            var spanX = Mathf.CeilToInt(reach * UnitX) + 1;
            var spanY = Mathf.CeilToInt(reach * UnitY) + 1;
            var cos = Mathf.Cos(angle * Mathf.Deg2Rad);
            var sin = Mathf.Sin(angle * Mathf.Deg2Rad);
            var target = (Color32)color;
            var top = Mathf.Max(0, Mathf.FloorToInt(cy - spanY));
            var bottom = Mathf.Min(Height - 1, Mathf.CeilToInt(cy + spanY));
            for (var y = top; y <= bottom; y++)
            {
                var wy = -(y - cy) / UnitY;
                for (var x = Mathf.FloorToInt(cx - spanX); x <= Mathf.CeilToInt(cx + spanX); x++)
                {
                    var wx = (x - cx) / UnitX;
                    var rx = (wx * cos + wy * sin) / radiusX;
                    var ry = (-wx * sin + wy * cos) / radiusY;
                    var distance = Mathf.Sqrt(rx * rx + ry * ry);
                    if (distance >= 1f) continue;
                    var alpha = opacity * (hardness >= 1f ? 1f : 1f - Mathf.SmoothStep(hardness, 1f, distance));
                    var index = y * Width + ((x % Width) + Width) % Width;
                    pixels[index] = Color32.Lerp(pixels[index], target, alpha);
                }
            }
            dirty = true;
        }

        public void Ring(Vector2 uv, float radiusX, float radiusY, float thickness, Color color, float opacity)
        {
            const int steps = 22;
            for (var step = 0; step < steps; step++)
            {
                var angle = step * Mathf.PI * 2f / steps;
                var lower = Mathf.Sin(angle) < 0f ? 1.25f : 0.8f;
                Blob(Offset(uv, Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY * lower), thickness, thickness * lower, color, opacity * 0.5f);
            }
        }

        public void Stroke(Vector2 uv, float angle, float length, float width, Color color, float opacity, float taper = 1f)
        {
            var steps = Mathf.Max(3, Mathf.CeilToInt(length * UnitX / 1.2f));
            var direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            for (var step = 0; step <= steps; step++)
            {
                var t = (float)step / steps;
                var along = (t - 0.5f) * length;
                var thickness = width * Mathf.Lerp(1f, Mathf.Sin(t * Mathf.PI) * 0.8f + 0.2f, taper);
                Blob(Offset(uv, direction.x * along, direction.y * along), thickness, thickness, color, opacity, 0.45f);
            }
        }

        public void Cut(Vector2 uv, float angle, float length, bool bleed)
        {
            Stroke(uv, angle, length * 1.08f, 0.035f, new Color(0.86f, 0.33f, 0.33f), 0.55f);
            Stroke(uv, angle, length, 0.014f, bleed ? new Color(0.42f, 0.02f, 0.05f) : new Color(0.62f, 0.2f, 0.24f), 0.95f);
            if (!bleed) return;
            var direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            for (var drop = 0; drop < 2; drop++)
            {
                var along = Random(-0.4f, 0.4f) * length;
                AddDrip(Offset(uv, direction.x * along, direction.y * along), BloodColor, 0.016f, Random(0.25f, 0.6f));
            }
        }

        public void Scrape(Vector2 uv, float angle, float length, float width)
        {
            var direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            var normal = new Vector2(-direction.y, direction.x);
            Blob(uv, length * 0.55f, width * 0.7f, new Color(0.9f, 0.42f, 0.4f), 0.35f, 0f, angle);
            for (var line = 0; line < 7; line++)
            {
                var offset = Random(-0.5f, 0.5f) * width;
                var shift = Random(-0.15f, 0.15f) * length;
                var center = Offset(uv, normal.x * offset + direction.x * shift, normal.y * offset + direction.y * shift);
                Stroke(center, angle + Random(-4f, 4f), length * Random(0.45f, 0.95f), Random(0.005f, 0.011f), new Color(0.7f, 0.12f, 0.14f), 0.6f);
            }
        }

        public void HandPrint(Vector2 uv, float side, float strength)
        {
            var color = new Color(0.93f, 0.2f, 0.22f);
            var opacity = 0.32f * strength;
            Blob(uv, 0.16f, 0.17f, color, opacity, 0.35f);
            for (var finger = 0; finger < 4; finger++)
            {
                var spread = (finger - 1.5f) * 9f * side;
                var angle = 90f + spread;
                var radians = angle * Mathf.Deg2Rad;
                var lift = 0.29f - Mathf.Abs(finger - 1.5f) * 0.03f;
                var center = Offset(uv, (finger - 1.5f) * 0.075f + Mathf.Cos(radians) * lift, Mathf.Sin(radians) * lift);
                Blob(center, 0.042f, 0.15f, color, opacity, 0.4f, spread);
            }
            Blob(Offset(uv, side * 0.2f, -0.03f), 0.045f, 0.12f, color, opacity, 0.4f, side * -50f);
        }

        public void Splat(Vector2 uv, float radius, Color color, float opacity, bool drip)
        {
            Blob(uv, radius, radius * Random(0.8f, 1.05f), color, opacity, 0.7f, Random(0f, 180f));
            var satellites = 7 + random.Next(5);
            for (var index = 0; index < satellites; index++)
            {
                var angle = Random(0f, Mathf.PI * 2f);
                var distance = radius * Random(0.9f, 1.8f);
                var size = radius * Random(0.1f, 0.3f);
                Blob(Offset(uv, Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance), size, size, color, opacity, 0.7f);
                Stroke(Offset(uv, Mathf.Cos(angle) * distance * 0.6f, Mathf.Sin(angle) * distance * 0.6f), angle * Mathf.Rad2Deg, distance * 0.7f, size * 0.45f, color, opacity * 0.9f);
            }
            if (!drip) return;
            for (var index = 0; index < 2 + random.Next(2); index++)
                AddDrip(Offset(uv, Random(-0.7f, 0.7f) * radius, -radius * 0.5f), color, radius * 0.16f, Random(0.25f, 0.7f), opacity);
        }

        public void AddDrip(Vector2 uv, Color color, float radius, float length, float opacity = 0.95f)
        {
            if (drips.Count > 40) drips.RemoveAt(0);
            drips.Add(new Drip
            {
                X = uv.x * Width,
                Y = uv.y * Height,
                Remaining = length,
                Speed = Random(0.25f, 0.45f),
                Radius = radius,
                Phase = Random(0f, 6f),
                Color = color,
                Opacity = opacity
            });
        }

        public void Tick(float deltaTime)
        {
            for (var index = drips.Count - 1; index >= 0; index--)
            {
                var drip = drips[index];
                var move = drip.Speed * deltaTime;
                drip.Speed = Mathf.Max(0.07f, drip.Speed * Mathf.Exp(-deltaTime * 0.7f));
                drip.Remaining -= move;
                drip.Phase += deltaTime * 3f;
                drip.Y += move * UnitY;
                drip.X += Mathf.Sin(drip.Phase) * move * UnitX * 0.25f;
                var uv = new Vector2(drip.X / Width, drip.Y / Height);
                var bead = drip.Remaining <= 0f ? 1.5f : 1f;
                Blob(uv, drip.Radius * bead, drip.Radius * bead * 1.2f, drip.Color, drip.Opacity, 0.6f);
                if (drip.Remaining <= 0f || drip.Y > Height - 3) drips.RemoveAt(index);
            }
            Flush();
        }

        public void Flush()
        {
            if (!dirty) return;
            dirty = false;
            Texture.SetPixels32(pixels);
            Texture.Apply(false);
        }

        public static readonly Color BloodColor = new Color(0.62f, 0.02f, 0.06f);
        public static readonly Color BruiseColor = new Color(0.36f, 0.13f, 0.38f);
        public static readonly Color RednessColor = new Color(0.9f, 0.3f, 0.3f);
    }
}

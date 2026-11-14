using System;
using System.Collections.Generic;
using UnityEngine;

namespace EmoRelief
{
    // Mobile-game style IMGUI widgets: glossy pills, round icon buttons, panels, outlined text and vector icons.
    public sealed class UiKit
    {
        public static readonly Color Ink = new Color(0.11f, 0.04f, 0.2f);
        public static readonly Color Night = new Color(0.22f, 0.04f, 0.12f);
        public static readonly Color Purple = new Color(0.45f, 0.2f, 0.85f);
        public static readonly Color Magenta = new Color(0.93f, 0.18f, 0.62f);
        public static readonly Color Pink = new Color(1f, 0.6f, 0.84f);
        public static readonly Color Orange = new Color(1f, 0.52f, 0.12f);
        public static readonly Color Yellow = new Color(1f, 0.84f, 0.2f);
        public static readonly Color Green = new Color(0.3f, 0.85f, 0.35f);
        public static readonly Color Cyan = new Color(0.2f, 0.78f, 0.95f);
        public static readonly Color Red = new Color(0.95f, 0.22f, 0.25f);
        public static readonly Color Grey = new Color(0.45f, 0.42f, 0.52f);
        public static readonly Color White = new Color(1f, 0.98f, 1f);

        public Font Display { get; }
        public Font Body { get; }
        public Font ThaiFont { get; }
        public Action Click;
        public bool Pressed;

        private readonly GUIStyle text;
        private readonly GUIStyle hidden;
        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        private readonly Dictionary<string, GUIStyle> slices = new Dictionary<string, GUIStyle>();
        private readonly Dictionary<string, string> wrapCache = new Dictionary<string, string>();
        private readonly Dictionary<string, Vector2> lineMetrics = new Dictionary<string, Vector2>();
        private readonly GUIStyle measure = new GUIStyle { wordWrap = false, richText = false, clipping = TextClipping.Overflow };

        public UiKit()
        {
            Display = Resources.Load<Font>("Fonts/LilitaOne-Regular");
            Body = Resources.Load<Font>("Fonts/Barlow-Regular");
            // Bundled Mitr derivative with positional Thai glyphs; also covers Latin for mixed strings.
            var thai = Resources.Load<Font>("Fonts/EmoThai-Regular");
            ThaiFont = thai != null ? thai : Body;
            ThaiText.PositionalGlyphs = thai != null && thai.HasCharacter('\uE001') && thai.HasCharacter('\uE030');
            if (!ThaiText.PositionalGlyphs) Debug.LogWarning("[UiKit] EmoThai-Regular font not imported; Thai tone marks will not be repositioned.");
            text = new GUIStyle { font = Display, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Overflow, richText = false };
            hidden = new GUIStyle();
        }

        public static bool HasThai(string value) => ThaiText.HasThai(value);

        // ---------- Text ----------

        private Font PickFont(string value, bool body) => HasThai(value) ? ThaiFont : body ? Body : Display;

        public void Text(Rect area, string value, int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter, float outline = 0f, Color? outlineColor = null, bool body = false, bool wrap = false, Color? highlight = null)
        {
            if (string.IsNullOrEmpty(value)) return;
            var font = PickFont(value, body);
            value = ThaiText.Shape(value);
            // IMGUI wraps only at spaces, which breaks Thai mid-syllable; wrap it ourselves.
            if (wrap && (font == ThaiFont || value.IndexOf(ThaiText.WordBreak) >= 0))
            {
                value = Wrap(value, area.width, size, font);
                wrap = false;
            }
            else value = ThaiText.StripBreaks(value);
            text.font = font;
            text.fontSize = size;
            text.alignment = anchor;
            text.wordWrap = wrap;
            if (outline > 0f)
            {
                text.normal.textColor = outlineColor ?? Ink;
                GUI.Label(new Rect(area.x, area.y + outline * 1.6f, area.width, area.height), value, text);
                for (var step = 0; step < 8; step++)
                {
                    var angle = step * Mathf.PI / 4f;
                    GUI.Label(new Rect(area.x + Mathf.Cos(angle) * outline, area.y + Mathf.Sin(angle) * outline, area.width, area.height), value, text);
                }
            }
            text.normal.textColor = color;
            GUI.Label(area, value, text);
            if (!highlight.HasValue) return;
            // Two-tone fill: redraw the upper half in a lighter colour.
            var content = new GUIContent(value);
            var height = text.CalcHeight(content, area.width);
            var top = anchor == TextAnchor.UpperLeft || anchor == TextAnchor.UpperCenter || anchor == TextAnchor.UpperRight ? area.y : anchor == TextAnchor.LowerLeft || anchor == TextAnchor.LowerCenter || anchor == TextAnchor.LowerRight ? area.yMax - height : area.y + (area.height - height) * 0.5f;
            GUI.BeginGroup(new Rect(area.x, area.y, area.width, top - area.y + height * 0.52f));
            text.normal.textColor = highlight.Value;
            GUI.Label(new Rect(0f, 0f, area.width, area.height), value, text);
            GUI.EndGroup();
        }

        /// <summary>
        /// Draws text guaranteed to stay inside <paramref name="area"/>: wraps (Thai-aware), steps the
        /// font size down to <paramref name="minSize"/>, then truncates with an ellipsis as a last resort.
        /// </summary>
        public void FitText(Rect area, string value, int maxSize, int minSize, Color color, TextAnchor anchor = TextAnchor.UpperLeft, bool body = true, int maxLines = 0)
        {
            if (string.IsNullOrEmpty(value) || area.width < 4f || area.height < 4f) return;
            var font = PickFont(value, body);
            var shaped = ThaiText.Shape(value);
            var size = maxSize;
            string wrapped = null;
            for (; size >= minSize; size--)
            {
                wrapped = Wrap(shaped, area.width, size, font);
                var lines = CountLines(wrapped);
                if ((maxLines <= 0 || lines <= maxLines) && BlockHeight(font, size, lines) <= area.height) break;
            }
            if (size < minSize)
            {
                size = minSize;
                var metrics = LineMetrics(font, size);
                var fit = Mathf.Max(1, Mathf.FloorToInt((area.height - metrics.x) / Mathf.Max(1f, metrics.y)) + 1);
                if (maxLines > 0) fit = Mathf.Min(fit, maxLines);
                wrapped = Truncate(Wrap(shaped, area.width, size, font), fit, area.width, size, font);
            }
            text.font = font;
            text.fontSize = size;
            text.alignment = anchor;
            text.wordWrap = false;
            text.normal.textColor = color;
            GUI.Label(area, wrapped, text);
        }

        public float CalcTextHeight(string value, float width, int size, bool body = false)
        {
            if (string.IsNullOrEmpty(value)) return 0f;
            var font = PickFont(value, body);
            return BlockHeight(font, size, CountLines(Wrap(ThaiText.Shape(value), width, size, font)));
        }

        public float TextWidth(string value, int size, bool body = false)
        {
            if (string.IsNullOrEmpty(value)) return 0f;
            return Measure(ThaiText.StripBreaks(ThaiText.Shape(value)), size, PickFont(value, body));
        }

        private static int CountLines(string value)
        {
            var count = 1;
            foreach (var c in value)
                if (c == '\n') count++;
            return count;
        }

        // x = height of one line, y = spacing added by each further line.
        private Vector2 LineMetrics(Font font, int size)
        {
            var key = font.GetInstanceID() + ":" + size;
            if (lineMetrics.TryGetValue(key, out var metrics)) return metrics;
            measure.font = font;
            measure.fontSize = size;
            var one = measure.CalcHeight(new GUIContent("Ag\u0E1B\u0E35\u0E48"), 4000f);
            var two = measure.CalcHeight(new GUIContent("Ag\nAg"), 4000f);
            metrics = new Vector2(one, Mathf.Max(1f, two - measure.CalcHeight(new GUIContent("Ag"), 4000f)));
            lineMetrics[key] = metrics;
            return metrics;
        }

        private float BlockHeight(Font font, int size, int lines)
        {
            var metrics = LineMetrics(font, size);
            return metrics.x + (lines - 1) * metrics.y;
        }

        private float Measure(string value, int size, Font font)
        {
            measure.font = font;
            measure.fontSize = size;
            return measure.CalcSize(new GUIContent(value)).x;
        }

        /// <summary>Greedy line wrap at spaces, server word marks and safe Thai syllable boundaries.</summary>
        private string Wrap(string value, float width, int size, Font font)
        {
            var key = font.GetInstanceID() + "|" + size + "|" + Mathf.RoundToInt(width) + "|" + value;
            if (wrapCache.TryGetValue(key, out var cached)) return cached;
            if (wrapCache.Count > 600) wrapCache.Clear();
            var lines = new List<string>();
            foreach (var paragraph in value.Replace("\r", "").Split('\n'))
            {
                var line = "";
                foreach (var token in Tokens(paragraph))
                {
                    var candidate = line + token;
                    if (line.Length == 0 || Measure(candidate.TrimEnd(), size, font) <= width)
                    {
                        line = candidate;
                        continue;
                    }
                    lines.Add(line.TrimEnd());
                    line = token.TrimStart();
                }
                // A single token wider than the line is split between clusters.
                while (line.Length > 0 && Measure(line.TrimEnd(), size, font) > width)
                {
                    var cut = FitPrefix(line, width, size, font);
                    lines.Add(line.Substring(0, cut).TrimEnd());
                    line = line.Substring(cut).TrimStart();
                }
                lines.Add(line.TrimEnd());
            }
            // Re-check lines produced before the overflow loop as well.
            for (var index = 0; index < lines.Count; index++)
            {
                if (Measure(lines[index], size, font) <= width || lines[index].Length < 2) continue;
                var cut = FitPrefix(lines[index], width, size, font);
                lines.Insert(index + 1, lines[index].Substring(cut).TrimStart());
                lines[index] = lines[index].Substring(0, cut).TrimEnd();
            }
            var result = string.Join("\n", lines);
            wrapCache[key] = result;
            return result;
        }

        private static IEnumerable<string> Tokens(string paragraph)
        {
            var token = new System.Text.StringBuilder();
            for (var index = 0; index < paragraph.Length; index++)
            {
                var c = paragraph[index];
                if (c == ThaiText.WordBreak)
                {
                    if (token.Length > 0) { yield return token.ToString(); token.Clear(); }
                    continue;
                }
                if (token.Length > 0)
                {
                    var previous = token[token.Length - 1];
                    if ((previous == ' ' && c != ' ') || (previous != ' ' && c != ' ' && ThaiText.CanBreakBetween(previous, c)))
                    {
                        yield return token.ToString();
                        token.Clear();
                    }
                }
                token.Append(c);
            }
            if (token.Length > 0) yield return token.ToString();
        }

        private int FitPrefix(string line, float width, int size, Font font)
        {
            var best = 0;
            for (var index = 1; index <= line.Length; index++)
            {
                if (index < line.Length && ThaiText.CannotStartLine(line[index])) continue;
                if (Measure(line.Substring(0, index), size, font) > width) break;
                best = index;
            }
            if (best > 0) return best;
            // Always make progress: take the first whole cluster even if it overflows.
            var first = 1;
            while (first < line.Length && ThaiText.CannotStartLine(line[first])) first++;
            return first;
        }

        private string Truncate(string wrapped, int maxLines, float width, int size, Font font)
        {
            var lines = wrapped.Split('\n');
            if (lines.Length <= maxLines) return wrapped;
            var last = lines[maxLines - 1].TrimEnd();
            while (last.Length > 0 && Measure(last + "...", size, font) > width)
            {
                var cut = last.Length - 1;
                while (cut > 0 && ThaiText.CannotStartLine(last[cut])) cut--;
                last = last.Substring(0, cut).TrimEnd();
            }
            lines[maxLines - 1] = last + "...";
            return string.Join("\n", lines, 0, maxLines);
        }

        // ---------- Shapes ----------

        public void Fill(Rect area, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        public void Image(Rect area, Texture2D texture, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(area, texture, ScaleMode.StretchToFill, true);
            GUI.color = previous;
        }

        public void Panel(Rect area, Color color, int radius = 22, Color? border = null, float borderWidth = 0f)
        {
            var style = Slice("panel" + radius, () => RoundRect(Color.white, Color.white, Color.white, radius, 0, false, false), radius);
            if (border.HasValue && borderWidth > 0f)
                Draw(new Rect(area.x - borderWidth, area.y - borderWidth, area.width + borderWidth * 2f, area.height + borderWidth * 2f), style, border.Value);
            Draw(area, style, color);
        }

        public void Glass(Rect area, int radius = 26)
        {
            Panel(new Rect(area.x, area.y + 6f, area.width, area.height), new Color(0f, 0f, 0f, 0.35f), radius);
            Panel(area, new Color(1f, 0.3f, 0.15f, 0.35f), radius, new Color(1f, 0.55f, 0.25f, 0.6f), 2f);
            Panel(area, new Color(Night.r, Night.g, Night.b, 0.86f), radius);
        }

        public void Bar(Rect area, float value, Color fill, Color back, bool segments = false)
        {
            Panel(area, back, Mathf.RoundToInt(area.height * 0.5f));
            var width = Mathf.Max(area.height, area.width * Mathf.Clamp01(value));
            if (value > 0.001f) Draw(new Rect(area.x, area.y, width, area.height), GlossStyle(fill, Mathf.RoundToInt(area.height * 0.5f)), Color.white);
            if (!segments) return;
            for (var tick = 1; tick < 10; tick++)
                Fill(new Rect(area.x + area.width * tick / 10f - 1f, area.y + 3f, 2f, area.height - 6f), new Color(0f, 0f, 0f, 0.2f));
        }

        // ---------- Buttons ----------

        public bool Button(Rect area, string label, Color color, int size = 26, Texture2D icon = null, bool enabled = true)
        {
            var hover = enabled && area.Contains(Event.current.mousePosition);
            var down = hover && Pressed;
            var shift = down ? 3f : 0f;
            var face = enabled ? color : Grey;
            Draw(new Rect(area.x, area.y + 5f, area.width, area.height), Slice("shadow", () => RoundRect(Color.white, Color.white, Color.white, 24, 0, false, false), 24), new Color(0f, 0f, 0f, 0.35f));
            var drawn = new Rect(area.x, area.y + shift, area.width, area.height - shift);
            Draw(drawn, GlossStyle(hover && !down ? Color.Lerp(face, Color.white, 0.12f) : face, 24), Color.white);
            var content = drawn;
            if (icon != null)
            {
                var iconSize = drawn.height * 0.56f;
                var iconRect = new Rect(drawn.x + drawn.height * 0.3f, drawn.y + (drawn.height - iconSize) * 0.5f - 2f, iconSize, iconSize);
                Image(new Rect(iconRect.x + 2f, iconRect.y + 3f, iconRect.width, iconRect.height), icon, new Color(0f, 0f, 0f, 0.3f));
                Image(iconRect, icon, White);
                content = new Rect(drawn.x + drawn.height * 0.5f, drawn.y, drawn.width - drawn.height * 0.5f, drawn.height);
            }
            Text(new Rect(content.x, content.y - 3f, content.width, content.height), label, size, White, TextAnchor.MiddleCenter, 2.2f, Shade(face, 0.45f));
            return Clicked(area, enabled);
        }

        public bool Round(Rect area, Texture2D icon, Color color, bool active = false, string label = null, bool enabled = true, bool slashed = false)
        {
            var hover = enabled && area.Contains(Event.current.mousePosition);
            var down = hover && Pressed;
            var face = enabled ? color : Grey;
            if (active) Image(new Rect(area.x - 7f, area.y - 7f, area.width + 14f, area.height + 14f), Tex("glow", () => Glow(Color.white)), new Color(Yellow.r, Yellow.g, Yellow.b, 0.85f));
            Image(new Rect(area.x, area.y + 5f, area.width, area.height), Tex("disc", Disc), new Color(0f, 0f, 0f, 0.35f));
            var drawn = down ? new Rect(area.x + 2f, area.y + 3f, area.width - 4f, area.height - 4f) : area;
            Image(drawn, Tex("orb" + ColorUtility.ToHtmlStringRGB(face) + (hover ? "h" : ""), () => Orb(hover ? Color.Lerp(face, Color.white, 0.12f) : face)), Color.white);
            if (active) Image(drawn, Tex("ring", () => Ring(Color.white)), Yellow);
            if (icon != null)
            {
                var inset = drawn.width * 0.24f;
                var iconRect = new Rect(drawn.x + inset, drawn.y + inset - 2f, drawn.width - inset * 2f, drawn.height - inset * 2f);
                Image(new Rect(iconRect.x + 2f, iconRect.y + 3f, iconRect.width, iconRect.height), icon, new Color(0f, 0f, 0f, 0.3f));
                Image(iconRect, icon, White);
                if (slashed) Image(iconRect, Icon("slash"), Red);
            }
            if (!string.IsNullOrEmpty(label))
                Text(new Rect(area.x - 30f, area.yMax - 4f, area.width + 60f, 26f), label, 17, White, TextAnchor.MiddleCenter, 2f);
            return Clicked(area, enabled);
        }

        public void Badge(Rect area, Texture2D icon, Color color)
        {
            Image(new Rect(area.x, area.y + 4f, area.width, area.height), Tex("disc", Disc), new Color(0f, 0f, 0f, 0.35f));
            Image(area, Tex("orb" + ColorUtility.ToHtmlStringRGB(color), () => Orb(color)), Color.white);
            var inset = area.width * 0.22f;
            Image(new Rect(area.x + inset, area.y + inset - 2f, area.width - inset * 2f, area.height - inset * 2f), icon, White);
        }

        public bool Chip(Rect area, string label, bool selected, Color color, int size = 20, Texture2D icon = null)
        {
            var hover = area.Contains(Event.current.mousePosition);
            Panel(area, selected ? color : new Color(1f, 1f, 1f, hover ? 0.16f : 0.08f), 16, selected ? Yellow : new Color(1f, 1f, 1f, 0.25f), selected ? 3f : 1.5f);
            var content = area;
            if (icon != null)
            {
                var iconSize = Mathf.Min(area.height * 0.62f, 40f);
                Image(new Rect(area.x + 10f, area.y + (area.height - iconSize) * 0.5f, iconSize, iconSize), icon, White);
                content = new Rect(area.x + iconSize + 12f, area.y, area.width - iconSize - 16f, area.height);
            }
            Text(new Rect(content.x, content.y - 2f, content.width, content.height), label, size, White, TextAnchor.MiddleCenter, selected ? 2f : 0f, Shade(color, 0.4f));
            return Clicked(area, true);
        }

        private bool Clicked(Rect area, bool enabled)
        {
            if (!enabled || !GUI.Button(area, GUIContent.none, hidden)) return false;
            Click?.Invoke();
            return true;
        }

        public static Color Shade(Color color, float amount) => Color.Lerp(color, Ink, amount);

        // ---------- Texture builders ----------

        private void Draw(Rect area, GUIStyle style, Color tint)
        {
            if (Event.current.type != EventType.Repaint) return;
            var previous = GUI.color;
            GUI.color = tint;
            style.Draw(area, false, false, false, false);
            GUI.color = previous;
        }

        private GUIStyle GlossStyle(Color color, int radius) =>
            Slice("gloss" + ColorUtility.ToHtmlStringRGBA(color) + radius, () => RoundRect(Color.Lerp(color, Color.white, 0.3f), Shade(color, 0.12f), Shade(color, 0.6f), radius, 3, true, true), radius);

        private GUIStyle Slice(string key, Func<Texture2D> build, int border)
        {
            if (slices.TryGetValue(key, out var style)) return style;
            style = new GUIStyle { border = new RectOffset(border, border, border, border) };
            style.normal.background = Tex(key, build);
            slices[key] = style;
            return style;
        }

        public Texture2D Tex(string key, Func<Texture2D> build)
        {
            if (textures.TryGetValue(key, out var texture)) return texture;
            texture = build();
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.name = key;
            textures[key] = texture;
            return texture;
        }

        private static Texture2D New(int size) => new Texture2D(size, size, TextureFormat.RGBA32, false);

        private static float RoundBox(Vector2 p, Vector2 half, float radius)
        {
            var q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - half + Vector2.one * radius;
            return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
        }

        private static Texture2D RoundRect(Color top, Color bottom, Color border, int radius, int borderWidth, bool lip, bool gloss)
        {
            var size = radius * 2 + 16;
            var texture = New(size);
            var half = new Vector2(size * 0.5f - 1f, size * 0.5f - 1f);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var d = RoundBox(new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f), half, radius);
                    var alpha = Mathf.Clamp01(0.5f - d);
                    var t = (float)y / size;
                    var color = Color.Lerp(bottom, top, t);
                    if (gloss && y > size * 0.58f) color = Color.Lerp(color, Color.white, 0.16f);
                    if (lip && y < radius * 0.42f) color = Shade(color, 0.28f);
                    if (borderWidth > 0 && d > -borderWidth) color = border;
                    color.a *= alpha;
                    texture.SetPixel(x, y, color);
                }
            }
            texture.Apply();
            return texture;
        }

        private static Texture2D Orb(Color color)
        {
            const int size = 128;
            var texture = New(size);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f - 64f, y + 0.5f - 64f);
                    var d = p.magnitude - 62f;
                    var alpha = Mathf.Clamp01(0.5f - d);
                    var t = (y + 0.5f) / size;
                    var c = Color.Lerp(Shade(color, 0.25f), Color.Lerp(color, Color.white, 0.25f), t);
                    var shine = (new Vector2(p.x, p.y - 30f) / new Vector2(44f, 22f)).magnitude;
                    if (shine < 1f) c = Color.Lerp(c, Color.white, (1f - shine) * 0.35f);
                    if (d > -6f) c = Shade(color, 0.62f);
                    else if (d > -9f) c = Color.Lerp(color, Color.white, 0.45f);
                    c.a = alpha;
                    texture.SetPixel(x, y, c);
                }
            }
            texture.Apply();
            return texture;
        }

        private static Texture2D Disc()
        {
            const int size = 64;
            var texture = New(size);
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(31f - new Vector2(x - 31.5f, y - 31.5f).magnitude)));
            texture.Apply();
            return texture;
        }

        private static Texture2D Ring(Color color)
        {
            const int size = 128;
            var texture = New(size);
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var d = Mathf.Abs(new Vector2(x + 0.5f - 64f, y + 0.5f - 64f).magnitude - 59f) - 3.5f;
                    texture.SetPixel(x, y, new Color(color.r, color.g, color.b, Mathf.Clamp01(0.5f - d)));
                }
            texture.Apply();
            return texture;
        }

        private static Texture2D Glow(Color color)
        {
            const int size = 64;
            var texture = New(size);
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var d = new Vector2(x - 31.5f, y - 31.5f).magnitude / 32f;
                    texture.SetPixel(x, y, new Color(color.r, color.g, color.b, Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d) * 1.4f));
                }
            texture.Apply();
            return texture;
        }

        public Texture2D Gradient(Color top, Color bottom) => Tex("gradient" + ColorUtility.ToHtmlStringRGB(top) + ColorUtility.ToHtmlStringRGB(bottom), () =>
        {
            var texture = new Texture2D(1, 128, TextureFormat.RGBA32, false);
            for (var y = 0; y < 128; y++) texture.SetPixel(0, y, Color.Lerp(bottom, top, y / 127f));
            texture.Apply();
            return texture;
        });

        // Bezel for the phone mock-up: opaque frame with a transparent rounded screen hole.
        public Texture2D Frame(int outerRadius, int innerRadius, int bezel) => Tex("frame" + outerRadius + "_" + innerRadius + "_" + bezel, () =>
        {
            var size = outerRadius * 2 + 32;
            var texture = New(size);
            var center = new Vector2(size * 0.5f, size * 0.5f);
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f) - center;
                    var outer = RoundBox(p, center - Vector2.one, outerRadius);
                    var inner = RoundBox(p, center - Vector2.one * (bezel + 1), innerRadius);
                    var alpha = Mathf.Clamp01(0.5f - outer) * Mathf.Clamp01(0.5f + inner);
                    var edge = Mathf.Clamp01(1f - Mathf.Abs(outer + 2.5f) / 2.5f);
                    var rim = Mathf.Clamp01(1f - Mathf.Abs(inner - 1.5f) / 1.5f);
                    var color = Color.Lerp(new Color(0.07f, 0.07f, 0.09f), new Color(0.45f, 0.45f, 0.52f), edge * 0.8f);
                    color = Color.Lerp(color, new Color(0.02f, 0.02f, 0.03f), rim);
                    color.a = alpha;
                    texture.SetPixel(x, y, color);
                }
            texture.Apply();
            return texture;
        });

        public GUIStyle FrameStyle(int outerRadius, int innerRadius, int bezel)
        {
            var key = "framestyle" + outerRadius + "_" + innerRadius + "_" + bezel;
            if (slices.TryGetValue(key, out var style)) return style;
            var border = outerRadius + 16;
            style = new GUIStyle { border = new RectOffset(border, border, border, border) };
            style.normal.background = Frame(outerRadius, innerRadius, bezel);
            slices[key] = style;
            return style;
        }

        public void DrawFrame(Rect area, int outerRadius, int innerRadius, int bezel)
        {
            if (Event.current.type == EventType.Repaint) FrameStyle(outerRadius, innerRadius, bezel).Draw(area, false, false, false, false);
        }

        // ---------- Vector icons ----------

        public Texture2D Icon(string name) => Tex("icon_" + name, () => Paint(Shape(name)));

        private static Texture2D Paint(Func<Vector2, float> sdf)
        {
            const int size = 128;
            var texture = New(size);
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var p = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - sdf(p) * size * 0.5f)));
                }
            texture.Apply();
            return texture;
        }

        private static float Circle(Vector2 p, float x, float y, float r) => (p - new Vector2(x, y)).magnitude - r;
        private static float Ellipse(Vector2 p, float x, float y, float rx, float ry) => ((p - new Vector2(x, y)) / new Vector2(rx, ry)).magnitude * Mathf.Min(rx, ry) - Mathf.Min(rx, ry);
        private static float Box(Vector2 p, float x, float y, float hx, float hy, float r, float angle = 0f)
        {
            var q = p - new Vector2(x, y);
            var c = Mathf.Cos(-angle * Mathf.Deg2Rad);
            var s = Mathf.Sin(-angle * Mathf.Deg2Rad);
            q = new Vector2(q.x * c - q.y * s, q.x * s + q.y * c);
            return RoundBox(q, new Vector2(hx, hy), r);
        }

        private static float Line(Vector2 p, float ax, float ay, float bx, float by, float r)
        {
            var a = new Vector2(ax, ay);
            var ba = new Vector2(bx, by) - a;
            var pa = p - a;
            var h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude - r;
        }

        private static float Polygon(Vector2 p, params float[] xy)
        {
            var count = xy.Length / 2;
            var d = float.MaxValue;
            var sign = 1f;
            for (int i = 0, j = count - 1; i < count; j = i, i++)
            {
                var vi = new Vector2(xy[i * 2], xy[i * 2 + 1]);
                var vj = new Vector2(xy[j * 2], xy[j * 2 + 1]);
                var e = vj - vi;
                var w = p - vi;
                var b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, b.sqrMagnitude);
                var c1 = p.y >= vi.y;
                var c2 = p.y < vj.y;
                var c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) sign = -sign;
            }
            return sign * Mathf.Sqrt(d);
        }

        private static float Arc(Vector2 p, float x, float y, float r, float width, float cut) =>
            Mathf.Max(Mathf.Abs(Circle(p, x, y, r)) - width, cut);

        private static float Min(params float[] values)
        {
            var result = float.MaxValue;
            foreach (var value in values) result = Mathf.Min(result, value);
            return result;
        }

        private static float Cut(float shape, params float[] holes) => Mathf.Max(shape, -Min(holes));

        private static Func<Vector2, float> Shape(string name)
        {
            switch (name)
            {
                case "punch":
                    return p => Cut(Min(Box(p, 0.06f, 0.2f, 0.5f, 0.48f, 0.42f), Line(p, -0.5f, 0.28f, -0.5f, -0.12f, 0.17f), Box(p, 0.04f, -0.6f, 0.4f, 0.24f, 0.08f)),
                        Box(p, 0.04f, -0.33f, 0.5f, 0.035f, 0f), Line(p, -0.28f, 0.3f, -0.28f, 0.0f, 0.03f));
                case "slap":
                    return p => Min(Box(p, 0.02f, -0.28f, 0.38f, 0.36f, 0.2f), Line(p, -0.27f, 0f, -0.3f, 0.62f, 0.095f), Line(p, -0.09f, 0f, -0.09f, 0.8f, 0.095f),
                        Line(p, 0.1f, 0f, 0.12f, 0.74f, 0.095f), Line(p, 0.28f, -0.05f, 0.34f, 0.52f, 0.085f), Line(p, -0.32f, -0.32f, -0.72f, 0.12f, 0.1f));
                case "egg": return p => Ellipse(p, 0f, -0.05f, 0.5f, 0.66f);
                case "slingshot":
                    return p => Min(Line(p, 0f, -0.85f, 0f, -0.1f, 0.11f), Line(p, 0f, -0.1f, -0.45f, 0.55f, 0.1f), Line(p, 0f, -0.1f, 0.45f, 0.55f, 0.1f),
                        Line(p, -0.45f, 0.52f, 0f, 0.12f, 0.035f), Line(p, 0.45f, 0.52f, 0f, 0.12f, 0.035f), Circle(p, 0f, 0.12f, 0.15f));
                case "bat":
                    return p => Min(Line(p, -0.62f, -0.62f, 0f, 0f, 0.075f), Line(p, -0.05f, -0.05f, 0.55f, 0.55f, 0.17f), Circle(p, -0.68f, -0.68f, 0.11f));
                case "dart":
                    return p => Min(Line(p, 0.8f, 0.8f, 0.35f, 0.35f, 0.03f), Line(p, 0.38f, 0.38f, 0f, 0f, 0.11f), Line(p, 0f, 0f, -0.4f, -0.4f, 0.035f),
                        Polygon(p, -0.35f, -0.35f, -0.85f, -0.4f, -0.68f, -0.68f), Polygon(p, -0.35f, -0.35f, -0.4f, -0.85f, -0.68f, -0.68f));
                case "pause": return p => Min(Box(p, -0.25f, 0f, 0.14f, 0.55f, 0.06f), Box(p, 0.25f, 0f, 0.14f, 0.55f, 0.06f));
                case "home": return p => Cut(Min(Polygon(p, -0.8f, 0.05f, 0f, 0.78f, 0.8f, 0.05f), Box(p, 0f, -0.35f, 0.52f, 0.42f, 0.06f)), Box(p, 0f, -0.52f, 0.15f, 0.26f, 0.05f));
                case "sound":
                    return p => Min(Box(p, -0.52f, 0f, 0.16f, 0.24f, 0.04f), Polygon(p, -0.42f, 0.24f, 0.05f, 0.62f, 0.05f, -0.62f, -0.42f, -0.24f),
                        Arc(p, 0.05f, 0f, 0.42f, 0.07f, 0.28f - p.x), Arc(p, 0.05f, 0f, 0.7f, 0.07f, 0.4f - p.x));
                case "voice":
                    return p => Cut(Min(Box(p, 0f, 0.18f, 0.78f, 0.52f, 0.32f), Polygon(p, -0.45f, -0.2f, -0.65f, -0.78f, -0.05f, -0.3f)),
                        Circle(p, -0.35f, 0.18f, 0.1f), Circle(p, 0f, 0.18f, 0.1f), Circle(p, 0.35f, 0.18f, 0.1f));
                case "blood": return p => Min(Circle(p, 0f, -0.22f, 0.48f), Polygon(p, -0.42f, -0.02f, 0f, 0.82f, 0.42f, -0.02f));
                case "shake":
                    return p => Min(Cut(Box(p, 0f, 0f, 0.32f, 0.58f, 0.1f), Box(p, 0f, 0.02f, 0.22f, 0.44f, 0.03f)), Line(p, -0.55f, 0.3f, -0.55f, -0.3f, 0.05f), Line(p, 0.55f, 0.3f, 0.55f, -0.3f, 0.05f),
                        Line(p, -0.75f, 0.18f, -0.75f, -0.18f, 0.05f), Line(p, 0.75f, 0.18f, 0.75f, -0.18f, 0.05f));
                case "phone": return p => Min(Cut(Box(p, 0f, 0f, 0.82f, 0.46f, 0.14f), Box(p, 0.04f, 0f, 0.64f, 0.34f, 0.04f)), Circle(p, -0.73f, 0f, 0.05f));
                case "gear":
                    return p =>
                    {
                        var teeth = float.MaxValue;
                        for (var tooth = 0; tooth < 8; tooth++) teeth = Mathf.Min(teeth, Box(p, 0f, 0f, 0.13f, 0.78f, 0.04f, tooth * 22.5f));
                        return Cut(Min(Circle(p, 0f, 0f, 0.58f), teeth), Circle(p, 0f, 0f, 0.24f));
                    };
                case "back": return p => Min(Line(p, 0.22f, 0.58f, -0.3f, 0f, 0.13f), Line(p, -0.3f, 0f, 0.22f, -0.58f, 0.13f));
                case "next": return p => Min(Line(p, -0.22f, 0.58f, 0.3f, 0f, 0.13f), Line(p, 0.3f, 0f, -0.22f, -0.58f, 0.13f));
                case "dice":
                    return p => Cut(Box(p, 0f, 0f, 0.7f, 0.7f, 0.18f), Circle(p, -0.35f, 0.35f, 0.11f), Circle(p, 0.35f, -0.35f, 0.11f), Circle(p, 0f, 0f, 0.11f), Circle(p, 0.35f, 0.35f, 0.11f), Circle(p, -0.35f, -0.35f, 0.11f));
                case "check": return p => Min(Line(p, -0.6f, 0f, -0.15f, -0.45f, 0.13f), Line(p, -0.15f, -0.45f, 0.65f, 0.45f, 0.13f));
                case "slash": return p => Line(p, -0.8f, 0.8f, 0.8f, -0.8f, 0.09f);
                case "close": return p => Min(Line(p, -0.55f, 0.55f, 0.55f, -0.55f, 0.13f), Line(p, -0.55f, -0.55f, 0.55f, 0.55f, 0.13f));
                case "star":
                    return p =>
                    {
                        var points = new float[20];
                        for (var index = 0; index < 10; index++)
                        {
                            var angle = Mathf.PI * 0.5f + index * Mathf.PI / 5f;
                            var radius = index % 2 == 0 ? 0.9f : 0.4f;
                            points[index * 2] = Mathf.Cos(angle) * radius;
                            points[index * 2 + 1] = Mathf.Sin(angle) * radius - 0.05f;
                        }
                        return Polygon(p, points);
                    };
                case "heart": return p => Min(Circle(p, -0.32f, 0.22f, 0.38f), Circle(p, 0.32f, 0.22f, 0.38f), Polygon(p, -0.68f, 0.1f, 0f, -0.8f, 0.68f, 0.1f));
                case "power": return p => Min(Arc(p, 0f, -0.05f, 0.6f, 0.11f, 0.18f - Mathf.Abs(p.x) + p.y * 0.5f), Line(p, 0f, 0.75f, 0f, 0.05f, 0.11f));
                case "timer": return p => Min(Mathf.Abs(Circle(p, 0f, 0f, 0.68f)) - 0.1f, Line(p, 0f, 0f, 0f, 0.42f, 0.08f), Line(p, 0f, 0f, 0.32f, -0.12f, 0.08f));
                case "robot":
                    return p => Cut(Min(Box(p, 0f, -0.1f, 0.66f, 0.52f, 0.24f), Line(p, 0f, 0.4f, 0f, 0.72f, 0.06f), Circle(p, 0f, 0.8f, 0.12f), Box(p, -0.75f, -0.1f, 0.1f, 0.22f, 0.06f), Box(p, 0.75f, -0.1f, 0.1f, 0.22f, 0.06f)),
                        Circle(p, -0.27f, 0f, 0.14f), Circle(p, 0.27f, 0f, 0.14f), Arc(p, 0f, -0.18f, 0.24f, 0.05f, p.y + 0.22f));
                case "info": return p => Cut(Circle(p, 0f, 0f, 0.82f), Line(p, 0f, -0.45f, 0f, 0.05f, 0.1f), Circle(p, 0f, 0.36f, 0.11f));
                case "ring": return p => Mathf.Abs(Circle(p, 0f, 0f, 0.86f)) - 0.05f;
                case "lock": return p => Min(Cut(Box(p, 0f, -0.25f, 0.55f, 0.45f, 0.1f), Circle(p, 0f, -0.2f, 0.11f)), Mathf.Max(Mathf.Abs(Circle(p, 0f, 0.25f, 0.33f)) - 0.1f, -p.y + 0.2f));
                case "poop": return p => Min(Ellipse(p, 0f, -0.55f, 0.75f, 0.28f), Ellipse(p, 0.02f, -0.15f, 0.56f, 0.24f), Ellipse(p, 0.04f, 0.2f, 0.38f, 0.2f), Polygon(p, -0.12f, 0.32f, 0.2f, 0.85f, 0.25f, 0.3f));
                case "foot":
                    return p => Min(Ellipse(p, 0f, -0.25f, 0.42f, 0.62f), Circle(p, -0.3f, 0.5f, 0.17f), Circle(p, -0.03f, 0.6f, 0.13f), Circle(p, 0.2f, 0.57f, 0.11f), Circle(p, 0.38f, 0.48f, 0.09f), Circle(p, 0.5f, 0.35f, 0.08f));
            }
            if (name.StartsWith("mood", StringComparison.Ordinal)) return Mood(name[4] - '0');
            return p => Circle(p, 0f, 0f, 0.7f);
        }

        // 0 angry, 1 frustrated, 2 stressed, 3 sad, 4 anxious, 5 calm
        private static Func<Vector2, float> Mood(int mood)
        {
            return p =>
            {
                var face = Circle(p, 0f, 0f, 0.86f);
                var eyes = Min(Circle(p, -0.3f, 0.12f, 0.11f), Circle(p, 0.3f, 0.12f, 0.11f));
                float mouth, brows = float.MaxValue;
                switch (mood)
                {
                    case 0:
                        mouth = Arc(p, 0f, -0.7f, 0.36f, 0.07f, (-0.5f) - p.y);
                        brows = Min(Line(p, -0.5f, 0.42f, -0.14f, 0.3f, 0.07f), Line(p, 0.5f, 0.42f, 0.14f, 0.3f, 0.07f));
                        break;
                    case 1:
                        mouth = Line(p, -0.3f, -0.38f, 0.3f, -0.42f, 0.07f);
                        brows = Min(Line(p, -0.48f, 0.36f, -0.14f, 0.36f, 0.07f), Line(p, 0.48f, 0.36f, 0.14f, 0.36f, 0.07f));
                        break;
                    case 2:
                        mouth = Min(Line(p, -0.36f, -0.4f, -0.12f, -0.3f, 0.06f), Line(p, -0.12f, -0.3f, 0.12f, -0.42f, 0.06f), Line(p, 0.12f, -0.42f, 0.36f, -0.3f, 0.06f));
                        brows = Min(Line(p, -0.48f, 0.34f, -0.16f, 0.44f, 0.06f), Line(p, 0.48f, 0.34f, 0.16f, 0.44f, 0.06f));
                        break;
                    case 3:
                        mouth = Arc(p, 0f, -0.66f, 0.32f, 0.07f, (-0.48f) - p.y);
                        brows = Min(Line(p, -0.48f, 0.32f, -0.16f, 0.44f, 0.06f), Line(p, 0.48f, 0.32f, 0.16f, 0.44f, 0.06f));
                        eyes = Min(eyes, Ellipse(p, 0.34f, -0.08f, 0.06f, 0.1f));
                        break;
                    case 4:
                        mouth = Mathf.Abs(Ellipse(p, 0f, -0.4f, 0.14f, 0.17f)) - 0.05f;
                        brows = Min(Line(p, -0.48f, 0.38f, -0.16f, 0.46f, 0.06f), Line(p, 0.48f, 0.38f, 0.16f, 0.46f, 0.06f));
                        break;
                    default:
                        mouth = Arc(p, 0f, -0.12f, 0.36f, 0.07f, p.y + 0.22f);
                        break;
                }
                return Cut(face, eyes, mouth, brows);
            };
        }

        public void Dispose()
        {
            foreach (var texture in textures.Values) ArtKit.Destroy(texture);
            textures.Clear();
            slices.Clear();
        }
    }
}

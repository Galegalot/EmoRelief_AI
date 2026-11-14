using UnityEngine;

namespace EmoRelief
{
    /// <summary>
    /// Module 4: EmoTipUIManager
    /// Home-screen setup bar (language + location), the AI loading overlay, and the
    /// AI EMO Tips panel on the results screen. Every block of AI text goes through
    /// UiKit.FitText, so long or unexpected replies shrink/wrap instead of overflowing.
    /// </summary>
    public sealed class EmoTipUIManager
    {
        private static readonly Color CardFill = new Color(0.1f, 0.05f, 0.16f, 0.92f);
        private static readonly Color Muted = new Color(1f, 1f, 1f, 0.62f);
        private static readonly Color StepCyan = UiKit.Cyan;
        private static readonly Color[] StepColors = { UiKit.Cyan, UiKit.Green, UiKit.Yellow };

        private readonly UiKit ui;
        private int tab;
        private GUIStyle fieldStyle;
        private GUIStyle overlayStyle;

        public EmoTipUIManager(UiKit kit)
        {
            ui = kit;
        }

        public bool IsLoading { get; private set; }
        public EmoAIResponse CurrentResponse { get; private set; }

        /// <summary>Results tab: 0 = healing steps, 1 = places.</summary>
        public int ActiveTab
        {
            get => tab;
            set => tab = Mathf.Clamp(value, 0, 1);
        }

        public void StartLoading()
        {
            IsLoading = true;
            tab = 0;
        }

        public void SetResponse(EmoAIResponse response)
        {
            CurrentResponse = response;
            IsLoading = false;
        }

        public void Clear()
        {
            CurrentResponse = null;
            IsLoading = false;
            tab = 0;
        }

        // =====================================================================
        // Home screen: language + location
        // =====================================================================

        public void DrawSetupBar(Rect area)
        {
            ui.Panel(area, new Color(0f, 0f, 0f, 0.55f), 20, new Color(1f, 1f, 1f, 0.18f), 1.5f);
            var toggle = new Rect(area.xMax - 150f, area.y + 6f, 142f, area.height - 12f);
            var label = new Rect(area.x + 16f, area.y, 150f, area.height);
            var field = new Rect(label.xMax + 4f, area.y + 6f, toggle.x - label.xMax - 16f, area.height - 12f);

            ui.Text(label, "YOUR LOCATION:", 17, UiKit.Yellow, TextAnchor.MiddleLeft, 0f);
            LocationField(field);

            var half = toggle.width * 0.5f;
            ui.Panel(toggle, new Color(1f, 1f, 1f, 0.08f), 14);
            if (Segment(new Rect(toggle.x, toggle.y, half, toggle.height), "EN", !EmoSettings.IsThai, UiKit.Purple)) EmoSettings.Language = EmoSettings.English;
            if (Segment(new Rect(toggle.x + half, toggle.y, half, toggle.height), "ไทย", EmoSettings.IsThai, UiKit.Purple)) EmoSettings.Language = EmoSettings.Thai;
        }

        private void LocationField(Rect field)
        {
            if (fieldStyle == null)
            {
                fieldStyle = new GUIStyle(GUI.skin.textField)
                {
                    font = ui.ThaiFont,
                    fontSize = 16,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(12, 12, 2, 2),
                    clipping = TextClipping.Clip
                };
                foreach (var state in new[] { fieldStyle.normal, fieldStyle.hover, fieldStyle.focused, fieldStyle.active, fieldStyle.onNormal, fieldStyle.onFocused })
                {
                    state.background = null;
                    // IMGUI cannot shape Thai, so the field text is invisible and redrawn shaped on top.
                    state.textColor = Color.clear;
                }
                overlayStyle = new GUIStyle(fieldStyle);
                overlayStyle.normal.textColor = UiKit.White;
            }

            const string control = "emo.location";
            var focused = GUI.GetNameOfFocusedControl() == control;
            ui.Panel(field, new Color(0.05f, 0.02f, 0.1f, 0.9f), 12, focused ? UiKit.Cyan : new Color(1f, 1f, 1f, 0.25f), focused ? 2f : 1.2f);
            GUI.SetNextControlName(control);
            var typed = GUI.TextField(field, EmoSettings.Location, EmoSettings.MaxLocationLength, fieldStyle);
            if (typed != EmoSettings.Location) EmoSettings.Location = typed;
            if (Event.current.type != EventType.Repaint) return;

            if (string.IsNullOrEmpty(typed))
            {
                if (!focused) ui.Text(new Rect(field.x + 12f, field.y, field.width - 24f, field.height), "City or province", 15, new Color(1f, 1f, 1f, 0.38f), TextAnchor.MiddleLeft, 0f, null, true);
                return;
            }
            var scroll = 0f;
            if (focused && GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl) is TextEditor editor) scroll = editor.scrollOffset.x;
            GUI.BeginGroup(field);
            GUI.Label(new Rect(-scroll, 0f, field.width + scroll, field.height), ThaiText.Shape(typed), overlayStyle);
            GUI.EndGroup();
        }

        private bool Segment(Rect area, string label, bool selected, Color color)
        {
            var inner = new Rect(area.x + 3f, area.y + 3f, area.width - 6f, area.height - 6f);
            if (selected) ui.Panel(inner, color, 12, UiKit.Yellow, 2f);
            ui.Text(inner, label, 17, selected ? UiKit.White : Muted, TextAnchor.MiddleCenter, selected ? 1.6f : 0f, UiKit.Shade(color, 0.5f));
            return !selected && GUI.Button(area, GUIContent.none, GUIStyle.none) && Click();
        }

        private bool Click()
        {
            ui.Click?.Invoke();
            return true;
        }

        // =====================================================================
        // Loading overlay
        // =====================================================================

        public void DrawLoadingScreen(float width, float height, float clock)
        {
            ui.Fill(new Rect(0f, 0f, width, height), new Color(0.05f, 0.01f, 0.06f, 0.9f));
            var panel = new Rect(width * 0.5f - 290f, height * 0.5f - 150f, 580f, 300f);
            ui.Glass(panel, 28);

            var center = new Vector2(panel.center.x, panel.y + 92f);
            ui.Image(new Rect(center.x - 80f, center.y - 80f, 160f, 160f), ui.Tex("emo.loadglow", Glow), new Color(UiKit.Magenta.r, UiKit.Magenta.g, UiKit.Magenta.b, 0.35f + 0.2f * Mathf.Sin(clock * 6f)));
            const int dots = 12;
            for (var index = 0; index < dots; index++)
            {
                var angle = (index * 360f / dots - clock * 300f) * Mathf.Deg2Rad;
                var fade = (float)index / dots;
                var size = 6f + fade * 9f;
                var position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 46f;
                var tint = Color.Lerp(UiKit.Magenta, UiKit.Yellow, fade);
                tint.a = 0.25f + fade * 0.75f;
                ui.Image(new Rect(position.x - size * 0.5f, position.y - size * 0.5f, size, size), ui.Icon("dot"), tint);
            }
            ui.Badge(new Rect(center.x - 26f, center.y - 26f, 52f, 52f), ui.Icon("robot"), UiKit.Magenta);

            var ellipsis = new string('.', 1 + (int)(clock * 2.5f) % 3);
            var title = EmoSettings.Pick("Analyzing your emotions and preparing your EMO Tips", "กำลังประเมินสภาวะอารมณ์ และสรุป EMO Tips จาก AI");
            ui.FitText(new Rect(panel.x + 30f, panel.y + 160f, panel.width - 60f, 56f), title + ellipsis, 22, 15, UiKit.Yellow, TextAnchor.MiddleCenter, false, 2);
            var detail = EmoSettings.Pick("Reading 7 signals from your fight", "กำลังอ่านข้อมูลการต่อสู้ 7 ด้าน");
            if (!string.IsNullOrWhiteSpace(EmoSettings.Location))
                detail += EmoSettings.Pick("  -  finding places near ", "  -  หาสถานที่ใกล้ ") + EmoSettings.Location.Trim();
            ui.FitText(new Rect(panel.x + 30f, panel.y + 218f, panel.width - 60f, 24f), detail, 15, 11, UiKit.Pink, TextAnchor.MiddleCenter, true, 1);

            var track = new Rect(panel.x + 70f, panel.y + 254f, panel.width - 140f, 10f);
            ui.Panel(track, new Color(0f, 0f, 0f, 0.5f), 5);
            var sweep = Mathf.Repeat(clock * 0.6f, 1.4f) - 0.4f;
            var start = Mathf.Clamp01(sweep);
            var end = Mathf.Clamp01(sweep + 0.4f);
            if (end > start) ui.Panel(new Rect(track.x + track.width * start, track.y, track.width * (end - start), track.height), UiKit.Cyan, 5);
        }

        // =====================================================================
        // Results: AI EMO Tips panel
        // =====================================================================

        public void DrawTipsPanel(Rect area)
        {
            var reply = CurrentResponse;
            if (reply == null) return;
            ui.Glass(area, 22);
            const float pad = 12f;
            var x = area.x + pad;
            var w = area.width - pad * 2f;
            var y = area.y + pad;

            // Header: title + source on the left, anger gauge on the right.
            const float headerHeight = 54f;
            var gauge = new Rect(area.xMax - pad - 210f, y, 210f, headerHeight);
            ui.Badge(new Rect(x, y + 3f, 48f, 48f), ui.Icon("robot"), UiKit.Magenta);
            var titleWidth = gauge.x - x - 66f;
            ui.FitText(new Rect(x + 58f, y + 1f, titleWidth, 30f), "AI EMO TIPS", 26, 18, UiKit.Yellow, TextAnchor.MiddleLeft, false, 1);
            var source = new Rect(x + 58f, y + 32f, 92f, 20f);
            ui.Panel(source, reply.IsOffline ? UiKit.Orange : UiKit.Green, 10);
            ui.Text(source, reply.IsOffline ? "OFFLINE" : "LIVE AI", 13, UiKit.Ink, TextAnchor.MiddleCenter, 0f);
            ui.FitText(new Rect(source.xMax + 8f, y + 32f, titleWidth - 100f, 20f), "Based on how you fought", 13, 10, Muted, TextAnchor.MiddleLeft, true, 1);
            DrawGauge(gauge, reply.anger_score);
            y += headerHeight + 8f;

            // Therapist message.
            const float adviceHeight = 88f;
            var advice = new Rect(x, y, w, adviceHeight);
            ui.Panel(advice, new Color(0.3f, 0.06f, 0.2f, 0.85f), 16, new Color(1f, 0.6f, 0.84f, 0.45f), 1.5f);
            ui.Panel(new Rect(advice.x + 8f, advice.y + 10f, 4f, advice.height - 20f), UiKit.Pink, 2);
            ui.Text(new Rect(advice.x + 20f, advice.y + 6f, w - 30f, 18f), "FROM YOUR AI THERAPIST", 13, UiKit.Pink, TextAnchor.MiddleLeft, 0f);
            ui.FitText(new Rect(advice.x + 20f, advice.y + 26f, w - 34f, advice.height - 32f), reply.player_relief_advice, 16, 11, UiKit.White, TextAnchor.UpperLeft);
            y += adviceHeight + 8f;

            // Segmented tabs.
            const float tabHeight = 32f;
            var tabs = new Rect(x, y, w, tabHeight);
            ui.Panel(tabs, new Color(0f, 0f, 0f, 0.35f), 16);
            var half = w * 0.5f;
            if (Tab(new Rect(tabs.x, tabs.y, half, tabHeight), "HEALING STEPS", tab == 0, UiKit.Green)) tab = 0;
            if (Tab(new Rect(tabs.x + half, tabs.y, half, tabHeight), "PLACES TO UNWIND", tab == 1, UiKit.Cyan)) tab = 1;
            y += tabHeight + 8f;

            // Cards fill the rest, leaving one line for the safety note.
            const float footer = 18f;
            var cardHeight = area.yMax - pad - footer - 4f - y;
            var cardWidth = (w - 16f) / 3f;
            for (var index = 0; index < 3; index++)
            {
                var card = new Rect(x + index * (cardWidth + 8f), y, cardWidth, cardHeight);
                if (tab == 0) StepCard(card, index, reply.healing_methods[index]);
                else PlaceCard(card, reply.recommended_places[index]);
            }
            ui.FitText(new Rect(x, area.yMax - pad - footer, w, footer), "Not a medical assessment. Need to talk to someone? Call the Thai mental health hotline 1323.", 12, 9, Muted, TextAnchor.MiddleLeft, true, 1);
        }

        private void DrawGauge(Rect area, float score)
        {
            score = Mathf.Clamp(score, 1f, 10f);
            string status;
            Color tone;
            if (score <= 4f) { status = "LOW"; tone = UiKit.Green; }
            else if (score <= 7f) { status = "MEDIUM"; tone = UiKit.Orange; }
            else { status = "HIGH"; tone = UiKit.Red; }
            ui.Panel(area, new Color(0f, 0f, 0f, 0.5f), 14, tone, 2f);
            ui.Text(new Rect(area.x + 12f, area.y + 4f, 90f, 18f), "ANGER SCORE", 13, Muted, TextAnchor.MiddleLeft, 0f);
            ui.Text(new Rect(area.x + 12f, area.y + 18f, 64f, 30f), score.ToString("0.0"), 30, tone, TextAnchor.MiddleLeft, 2f);
            ui.Text(new Rect(area.x + 68f, area.y + 24f, 36f, 24f), "/10", 16, UiKit.White, TextAnchor.MiddleLeft, 0f);
            var pill = new Rect(area.xMax - 84f, area.y + 8f, 74f, 22f);
            ui.Panel(pill, tone, 11);
            ui.Text(pill, status, 14, UiKit.Ink, TextAnchor.MiddleCenter, 0f);
            ui.Bar(new Rect(area.xMax - 98f, area.y + 36f, 88f, 10f), score / 10f, tone, new Color(1f, 1f, 1f, 0.12f));
        }

        private bool Tab(Rect area, string label, bool selected, Color color)
        {
            var inner = new Rect(area.x + 3f, area.y + 3f, area.width - 6f, area.height - 6f);
            if (selected) ui.Panel(inner, color, 13);
            ui.FitText(new Rect(inner.x + 8f, inner.y, inner.width - 16f, inner.height), label, 16, 11, selected ? UiKit.Ink : Muted, TextAnchor.MiddleCenter, false, 1);
            return !selected && GUI.Button(area, GUIContent.none, GUIStyle.none) && Click();
        }

        private void StepCard(Rect card, int index, string text)
        {
            var color = StepColors[index];
            ui.Panel(card, CardFill, 14, color, 1.5f);
            var dot = new Rect(card.x + 10f, card.y + 10f, 26f, 26f);
            ui.Panel(dot, color, 13);
            ui.Text(dot, (index + 1).ToString(), 16, UiKit.Ink, TextAnchor.MiddleCenter, 0f);
            ui.Text(new Rect(dot.xMax + 8f, dot.y, card.width - 54f, dot.height), "STEP " + (index + 1), 15, color, TextAnchor.MiddleLeft, 0f);
            ui.FitText(new Rect(card.x + 12f, card.y + 44f, card.width - 24f, card.height - 52f), text, 15, 10, UiKit.White, TextAnchor.UpperLeft);
        }

        private void PlaceCard(Rect card, RecommendedPlace place)
        {
            ui.Panel(card, CardFill, 14, StepCyan, 1.5f);
            var inner = card.width - 20f;
            var y = card.y + 8f;
            ui.FitText(new Rect(card.x + 10f, y, inner, 38f), place.name, 17, 11, UiKit.Yellow, TextAnchor.UpperLeft, false, 2);
            y += 40f;
            if (!string.IsNullOrWhiteSpace(place.category))
            {
                var pillWidth = Mathf.Min(inner, ui.TextWidth(place.category, 12, true) + 18f);
                var pill = new Rect(card.x + 10f, y, pillWidth, 18f);
                ui.Panel(pill, new Color(UiKit.Cyan.r, UiKit.Cyan.g, UiKit.Cyan.b, 0.22f), 9);
                ui.FitText(new Rect(pill.x + 8f, pill.y, pill.width - 14f, pill.height), place.category, 12, 9, UiKit.Cyan, TextAnchor.MiddleLeft, true, 1);
                y += 24f;
            }
            var rest = card.yMax - 8f - y;
            var activityHeight = Mathf.Floor(rest * 0.5f);
            ui.FitText(new Rect(card.x + 10f, y, inner, activityHeight), place.recommended_activity, 13, 9, UiKit.White, TextAnchor.UpperLeft);
            ui.FitText(new Rect(card.x + 10f, y + activityHeight + 2f, inner, rest - activityHeight - 2f), place.reason, 12, 9, Muted, TextAnchor.UpperLeft);
        }

        private static Texture2D Glow()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var d = Mathf.Clamp01(1f - new Vector2(x - 31.5f, y - 31.5f).magnitude / 32f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, d * d));
                }
            texture.Apply();
            return texture;
        }
    }
}

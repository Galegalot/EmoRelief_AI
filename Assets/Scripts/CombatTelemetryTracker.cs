using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace EmoRelief
{
    [Serializable]
    public class PauseTrackingData
    {
        public int pause_count;
        public float total_pause_duration;
        public string style;
    }

    public class CombatTelemetryData
    {
        public float total_fight_duration;
        public float effective_fighting_time;
        public float max_attack_speed;
        public float max_speed_duration;
        public Dictionary<string, int> tools_used = new Dictionary<string, int>();
        public string primary_tool_used;
        public PauseTrackingData pause_tracking = new PauseTrackingData();
        public string location = EmoSettings.DefaultLocation;
        public string language = EmoSettings.English;

        // JsonUtility cannot serialise dictionaries, so the payload is written by hand.
        public string ToJson()
        {
            var culture = CultureInfo.InvariantCulture;
            var json = new StringBuilder("{\n");
            json.AppendFormat(culture, "  \"total_fight_duration\": {0:0.0},\n", total_fight_duration);
            json.AppendFormat(culture, "  \"effective_fighting_time\": {0:0.0},\n", effective_fighting_time);
            json.AppendFormat(culture, "  \"max_attack_speed\": {0:0.0},\n", max_attack_speed);
            json.AppendFormat(culture, "  \"max_speed_duration\": {0:0.0},\n", max_speed_duration);
            json.Append("  \"tools_used\": {");
            var first = true;
            foreach (var pair in tools_used)
            {
                json.Append(first ? "\n" : ",\n").AppendFormat(culture, "    \"{0}\": {1}", Escape(pair.Key), pair.Value);
                first = false;
            }
            json.Append("\n  },\n");
            json.AppendFormat("  \"primary_tool_used\": \"{0}\",\n", Escape(primary_tool_used));
            json.Append("  \"pause_tracking\": {\n");
            json.AppendFormat(culture, "    \"pause_count\": {0},\n", pause_tracking.pause_count);
            json.AppendFormat(culture, "    \"total_pause_duration\": {0:0.0},\n", pause_tracking.total_pause_duration);
            json.AppendFormat("    \"style\": \"{0}\"\n", Escape(pause_tracking.style));
            json.Append("  },\n");
            json.AppendFormat("  \"location\": \"{0}\",\n", Escape(location));
            json.AppendFormat("  \"language\": \"{0}\"\n", Escape(language));
            return json.Append("}").ToString();
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            var output = new StringBuilder(value.Length + 8);
            foreach (var c in value)
            {
                if (c == '"' || c == '\\') output.Append('\\').Append(c);
                else if (c < ' ') output.AppendFormat("\\u{0:x4}", (int)c);
                else output.Append(c);
            }
            return output.ToString();
        }
    }

    /// <summary>
    /// Module 1: CombatTelemetryTracker
    /// Tracks the 7 combat telemetry attributes during the face-striking fight.
    /// FaceLab feeds it player clicks, landed hits and per-frame ticks (including paused frames).
    /// </summary>
    public sealed class CombatTelemetryTracker
    {
        private static readonly string[] ToolNamesThai = { "หมัดชก", "ตบหน้า", "ปาไข่", "หนังสติ๊ก", "ไม้เบสบอล", "ปาลูกดอก", "ฝ่าเท้าเน่า", "ตบอึ" };
        private static readonly string[] ToolNamesEnglish = { "Punch", "Slap", "Egg", "Slingshot", "Bat", "Dart", "Rotten Feet", "Poop Slap" };

        private const float SpeedWindow = 1.5f;
        private const float IdleThreshold = 3f;
        // Gaps up to this long between hits count as continuous fighting (combo time).
        private const float ComboGap = 1.5f;
        private const float HitSlice = 0.4f;

        private readonly int[] toolHits = new int[ToolNamesEnglish.Length];
        private readonly Queue<float> clicks = new Queue<float>();

        private bool active;
        private float clock;
        private float lastHit;
        private float effective;
        private float peakSpeed;
        private float peakDuration;
        private bool wasPaused;
        private bool idling;
        private float idleStart;
        private int pauseCount;
        private float pauseTotal;

        public float TotalFightDuration => clock;
        public float MaxAttackSpeed => peakSpeed;

        public void StartCombat()
        {
            Array.Clear(toolHits, 0, toolHits.Length);
            clicks.Clear();
            active = true;
            clock = 0f;
            lastHit = 0f;
            effective = 0f;
            peakSpeed = 0f;
            peakDuration = 0f;
            wasPaused = false;
            idling = false;
            pauseCount = 0;
            pauseTotal = 0f;
        }

        /// <summary>A player attack input (tap, click or key), whether or not it lands.</summary>
        public void RecordClick()
        {
            if (active) clicks.Enqueue(clock);
        }

        public void RecordHit(ToolKind tool)
        {
            if (!active) return;
            var index = (int)tool;
            if (index >= 0 && index < toolHits.Length) toolHits[index]++;
            var gap = clock - lastHit;
            effective += gap <= ComboGap ? gap : HitSlice;
            EndIdle();
            lastHit = clock;
        }

        public void Tick(float delta, bool paused)
        {
            if (!active) return;
            clock += delta;
            if (paused)
            {
                if (!wasPaused)
                {
                    // A menu pause replaces any idle streak that was already running.
                    EndIdle();
                    pauseCount++;
                    wasPaused = true;
                }
                pauseTotal += delta;
                return;
            }
            if (wasPaused)
            {
                wasPaused = false;
                lastHit = clock;
            }
            if (!idling && clock - lastHit > IdleThreshold)
            {
                idling = true;
                idleStart = lastHit;
                pauseCount++;
            }

            while (clicks.Count > 0 && clock - clicks.Peek() > SpeedWindow) clicks.Dequeue();
            var speed = clicks.Count / SpeedWindow;
            if (speed > peakSpeed) peakSpeed = speed;
            if (peakSpeed >= 1f && speed >= 0.7f * peakSpeed) peakDuration += delta;
        }

        public CombatTelemetryData FinishCombat(string location, string language)
        {
            EndIdle();
            active = false;
            var thai = language == EmoSettings.Thai;
            var names = thai ? ToolNamesThai : ToolNamesEnglish;
            var tools = new Dictionary<string, int>();
            var primary = 0;
            for (var index = 0; index < toolHits.Length; index++)
            {
                if (toolHits[index] > 0) tools[names[index]] = toolHits[index];
                if (toolHits[index] > toolHits[primary]) primary = index;
            }
            if (tools.Count == 0) tools[names[0]] = 0;
            return new CombatTelemetryData
            {
                total_fight_duration = Mathf.Max(0.5f, clock),
                effective_fighting_time = Mathf.Clamp(effective, 0f, clock),
                max_attack_speed = peakSpeed,
                max_speed_duration = peakDuration,
                tools_used = tools,
                primary_tool_used = names[primary],
                pause_tracking = new PauseTrackingData
                {
                    pause_count = pauseCount,
                    total_pause_duration = pauseTotal,
                    style = pauseCount <= 2
                        ? (thai ? "ลุยแหลกต่อเนื่อง" : "Relentless, non-stop attacker")
                        : (thai ? "ยับยั้งชั่งใจ/คิดเยอะก่อนลุย" : "Restrained, thinks before striking")
                },
                location = EmoSettings.PayloadLocation(location, language),
                language = thai ? EmoSettings.Thai : EmoSettings.English
            };
        }

        private void EndIdle()
        {
            if (!idling) return;
            pauseTotal += clock - idleStart;
            idling = false;
        }
    }
}

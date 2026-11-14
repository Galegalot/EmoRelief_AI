using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;

namespace EmoRelief
{
    [Serializable]
    public class RecommendedPlace
    {
        public string name;
        public string category;
        public string recommended_activity;
        public string reason;
    }

    [Serializable]
    public class EmoAIResponse
    {
        public float anger_score;
        public string player_relief_advice;
        public string[] healing_methods;
        public RecommendedPlace[] recommended_places;
        // Set by the server when the model reply was unusable and it substituted its own tips.
        public bool is_fallback;

        [NonSerialized] public bool ClientFallback;

        public bool IsOffline => ClientFallback || is_fallback;
    }

    /// <summary>
    /// Module 3: EmoAIClient
    /// HTTP bridge to the local Python AI backend (ThaiLLM Pathumma) at http://127.0.0.1:8000/analyze.
    /// Runs on a worker thread via HttpWebRequest (no UnityWebRequest module dependency) and always
    /// completes: on network errors, timeouts or bad JSON it returns offline tips in the chosen language.
    /// </summary>
    public static class EmoAIClient
    {
        public const string AnalyzeEndpoint = "http://127.0.0.1:8000/analyze";
        public const int RequestTimeoutSeconds = 25;

        public static IEnumerator RequestAnalysis(CombatTelemetryData telemetry, Action<EmoAIResponse> onComplete)
        {
            var payload = Encoding.UTF8.GetBytes(telemetry.ToJson());
            EmoAIResponse response = null;
            string error = null;
            var done = false;

            var worker = new Thread(() =>
            {
                try
                {
                    var request = (HttpWebRequest)WebRequest.Create(AnalyzeEndpoint);
                    request.Method = "POST";
                    request.ContentType = "application/json; charset=utf-8";
                    request.Timeout = RequestTimeoutSeconds * 1000;
                    request.ReadWriteTimeout = RequestTimeoutSeconds * 1000;
                    using (var stream = request.GetRequestStream()) stream.Write(payload, 0, payload.Length);
                    using (var reply = (HttpWebResponse)request.GetResponse())
                    using (var reader = new StreamReader(reply.GetResponseStream(), Encoding.UTF8))
                    {
                        var parsed = JsonUtility.FromJson<EmoAIResponse>(reader.ReadToEnd());
                        if (parsed != null && !string.IsNullOrEmpty(parsed.player_relief_advice)) response = parsed;
                        else error = "Reply was missing player_relief_advice.";
                    }
                }
                catch (Exception exception)
                {
                    error = exception.Message;
                }
                finally
                {
                    done = true;
                }
            }) { IsBackground = true };
            worker.Start();

            var deadline = Time.realtimeSinceStartup + RequestTimeoutSeconds + 2f;
            while (!done && Time.realtimeSinceStartup < deadline) yield return null;

            if (response == null)
            {
                Debug.LogWarning("[EmoAIClient] Using offline EMO tips: " + (error ?? "request timed out."));
                response = CreateFallback(telemetry);
            }
            onComplete?.Invoke(Complete(response, telemetry));
        }

        /// <summary>Offline tips in the request language. Places are generic so they suit any location.</summary>
        public static EmoAIResponse CreateFallback(CombatTelemetryData telemetry)
        {
            var thai = telemetry != null && telemetry.language == EmoSettings.Thai;
            var score = 7f;
            if (telemetry != null && telemetry.total_fight_duration > 0f)
            {
                var focus = telemetry.effective_fighting_time / telemetry.total_fight_duration;
                score = Mathf.Clamp(3.5f + telemetry.max_attack_speed * 0.45f + focus * 2.5f, 3f, 9.5f);
            }
            var response = thai ? ThaiFallback() : EnglishFallback();
            response.anger_score = Mathf.Round(score * 10f) / 10f;
            response.ClientFallback = true;
            return response;
        }

        // Fills any gaps in a partial reply so the UI always has three steps and three places.
        private static EmoAIResponse Complete(EmoAIResponse response, CombatTelemetryData telemetry)
        {
            var spare = CreateFallback(telemetry);
            response.anger_score = Mathf.Clamp(response.anger_score <= 0f ? spare.anger_score : response.anger_score, 1f, 10f);
            if (string.IsNullOrWhiteSpace(response.player_relief_advice)) response.player_relief_advice = spare.player_relief_advice;
            var steps = new string[3];
            var places = new RecommendedPlace[3];
            for (var index = 0; index < 3; index++)
            {
                var step = response.healing_methods != null && index < response.healing_methods.Length ? response.healing_methods[index] : null;
                steps[index] = string.IsNullOrWhiteSpace(step) ? spare.healing_methods[index] : step;
                var place = response.recommended_places != null && index < response.recommended_places.Length ? response.recommended_places[index] : null;
                places[index] = place == null || string.IsNullOrWhiteSpace(place.name) ? spare.recommended_places[index] : place;
            }
            response.healing_methods = steps;
            response.recommended_places = places;
            return response;
        }

        private static EmoAIResponse EnglishFallback() => new EmoAIResponse
        {
            player_relief_advice = "Take a slow, deep breath. You let out a lot of pressure in that fight, and that is okay. Your feelings are valid. Now give yourself a moment of kindness.",
            healing_methods = new[]
            {
                "Roll your shoulders and shake out your wrists for 30 seconds to release tension.",
                "Drink a glass of cool water slowly to help your heart rate settle.",
                "Try box breathing: in for 4, hold for 4, out for 6. Repeat 4 times."
            },
            recommended_places = new[]
            {
                new RecommendedPlace { name = "A nearby park", category = "Nature", recommended_activity = "Walk slowly and notice the trees and sky.", reason = "Green spaces lower stress and calm a racing mind." },
                new RecommendedPlace { name = "A quiet cafe", category = "Relax", recommended_activity = "Sit with a cold drink and put your phone away.", reason = "A change of scene breaks the loop of angry thoughts." },
                new RecommendedPlace { name = "Mental health hotline 1323", category = "Support", recommended_activity = "Talk to a counselor if the anger keeps coming back.", reason = "Free, confidential support from trained listeners." }
            }
        };

        private static EmoAIResponse ThaiFallback() => new EmoAIResponse
        {
            player_relief_advice = "หายใจเข้าลึกๆ ช้าๆ นะ คุณได้ปลดปล่อย ความกดดันออกมามาก ซึ่งเป็นเรื่องปกติ ความรู้สึกของคุณ มีความหมายเสมอ ตอนนี้ ลองใจดีกับตัวเอง สักครู่นะ",
            healing_methods = new[]
            {
                "หมุนหัวไหล่ และสะบัดข้อมือ 30 วินาที เพื่อคลาย กล้ามเนื้อที่เกร็ง",
                "ดื่มน้ำเย็น ช้าๆ หนึ่งแก้ว ช่วยให้ หัวใจเต้นช้าลง",
                "หายใจแบบ Box Breathing เข้า 4 กลั้น 4 ออก 6 ทำซ้ำ 4 รอบ"
            },
            recommended_places = new[]
            {
                new RecommendedPlace { name = "สวนสาธารณะ ใกล้บ้าน", category = "ธรรมชาติ", recommended_activity = "เดินช้าๆ มองต้นไม้ และท้องฟ้า", reason = "พื้นที่สีเขียว ช่วยลดความเครียด และทำให้ใจสงบ" },
                new RecommendedPlace { name = "คาเฟ่ เงียบๆ", category = "พักผ่อน", recommended_activity = "นั่งดื่มเครื่องดื่มเย็นๆ และวางโทรศัพท์", reason = "การเปลี่ยนบรรยากาศ ช่วยตัดวงจร ความคิดที่วนเวียน" },
                new RecommendedPlace { name = "สายด่วนสุขภาพจิต 1323", category = "ขอความช่วยเหลือ", recommended_activity = "โทรคุยกับผู้เชี่ยวชาญ หากความโกรธ ยังวนกลับมา", reason = "ปรึกษาฟรี เป็นความลับ และมีคนพร้อมรับฟัง" }
            }
        };
    }
}

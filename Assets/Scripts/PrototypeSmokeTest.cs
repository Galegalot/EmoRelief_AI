using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EmoRelief
{
    public sealed class PrototypeSmokeTest : MonoBehaviour
    {
        private FaceLab lab;
        private string output;
        private int errors;
        private int editorDiagnostics;

        private IEnumerator Start()
        {
            Application.logMessageReceived += CountErrors;
            lab = GetComponent<FaceLab>();
            output = Path.Combine(Application.dataPath, "..", "SmokeScreenshots");
            Directory.CreateDirectory(output);
            foreach (var old in Directory.GetFiles(output, "*.png")) File.Delete(old);
            lab.SetPhoneFrame(true);
            lab.SetVoice(true);
            lab.SetSound(true);
            lab.SetBlood(true);
            SetResolution(1600, 900);
            yield return new WaitForSecondsRealtime(1.5f);
#if UNITY_EDITOR
            var shaderDeadline = Time.realtimeSinceStartup + 30f;
            while (UnityEditor.ShaderUtil.anythingCompiling && Time.realtimeSinceStartup < shaderDeadline)
                yield return null;
#endif
            Check(lab.Stage == Stage.Home, "TC-01 opens on home screen");
            Check(lab.Viewport.width > lab.Viewport.height * 2f, "landscape phone screen");
            yield return Settle(0.6f);
            Capture("01-home-phone.png");

            lab.GoTo(Stage.Before);
            lab.Mood = 0;
            lab.Before = 8;
            yield return Settle(0.8f);
            Check(lab.Mood == 0, "TC-02 mood stored");
            Check(lab.Before == 8, "TC-03 pre-play anger rating stored");
            Capture("02-check-in.png");

            lab.GoTo(Stage.Customize);
            lab.ApplyStyle(FaceStyle.Character(1), 1);
            yield return Settle(0.9f);
            Check(lab.Character.FaceMesh.blendShapeCount == 8, "eight facial blend shapes");
            Check(lab.Character.Style.Sex == 1 && lab.Scene == 1, "face and scene customisation stored");
            Capture("03-customize-female-bar.png");
            var random = new System.Random(5);
            for (var scene = 0; scene < Backdrops.Names.Length; scene++)
            {
                lab.ApplyStyle(FaceStyle.Random(random), scene);
                yield return null;
            }
            lab.ApplyStyle(new FaceStyle { Sex = 0, Shape = 2, Skin = 2, Eyes = 0, Nose = 3, Mouth = 1, Hair = 1, Ears = 1 }, 2);
            yield return Settle(0.8f);
            Capture("04-customize-male-bedroom.png");
            lab.ChooseCharacter(2);
            lab.ApplyStyle(FaceStyle.Character(2), 5);
            yield return Settle(0.8f);
            Capture("04b-lippy-lou-friends.png");
            lab.ChooseCharacter(3);
            yield return Settle(0.8f);
            Capture("04c-bug-eye-bella.png");
            lab.ApplyStyle(new FaceStyle { Sex = 0, Mouth = 6, Skin = 1, Hair = 7 }, 5);
            yield return Settle(0.6f);
            Capture("04d-long-tongue.png");
            lab.ApplyStyle(new FaceStyle { Sex = 0, Mouth = 5, Skin = 3, Hair = 2 }, 5);
            yield return Settle(0.6f);
            Capture("04e-buck-teeth.png");

            lab.ApplyStyle(FaceStyle.Character(0), 4);
            lab.GoTo(Stage.Play);
            yield return Settle(1.2f);
            Capture("05-play-gloves.png");

            lab.SelectTool(ToolKind.Punch);
            Check(lab.Act(new Vector2(-0.55f, -0.15f)), "screen-space face targeting");
            yield return new WaitForSecondsRealtime(0.13f);
            yield return new WaitForEndOfFrame();
            Capture("06-punch-extended.png");
            foreach (var point in new[] { new Vector2(0.55f, -0.15f), new Vector2(0f, -0.1f), new Vector2(-0.43f, 0.33f), new Vector2(0.43f, 0.33f), new Vector2(0f, -0.6f) })
            {
                lab.Act(point);
                yield return new WaitForSecondsRealtime(0.22f);
            }
            yield return new WaitForSecondsRealtime(0.4f);
            Check(lab.ToolHits[0] >= 5, "TC-04 punch connects");
            Check(lab.VoiceLines > 0, "opponent voice plays");

            lab.SelectTool(ToolKind.Slap);
            yield return Settle(0.4f);
            Capture("07-open-hands.png");
            lab.Act(new Vector2(0.62f, -0.25f));
            yield return new WaitForSecondsRealtime(0.17f);
            yield return new WaitForEndOfFrame();
            Capture("08-slap.png");
            yield return new WaitForSecondsRealtime(0.45f);
            lab.Act(new Vector2(-0.62f, -0.25f));
            yield return new WaitForSecondsRealtime(0.6f);
            Check(lab.ToolHits[1] >= 2, "TC-05 slap connects");

            lab.SelectTool(ToolKind.Egg);
            yield return Settle(0.3f);
            Capture("09-egg-in-hand.png");
            lab.Act(new Vector2(-0.2f, 0.5f));
            yield return new WaitForSecondsRealtime(0.9f);
            Check(lab.ToolHits[2] >= 1, "TC-06 egg lands");
            Check(lab.Character.EggsOnFace > 0, "raw egg sticks to the face");
            yield return new WaitForSecondsRealtime(1.6f);
            yield return new WaitForEndOfFrame();
            Capture("09b-egg-dripping.png");

            lab.SelectTool(ToolKind.Slingshot);
            var view = Camera.main;
            var face = view.WorldToScreenPoint(lab.Character.Root.TransformPoint(new Vector3(0.35f, 0.6f, -0.8f)));
            Check(lab.PointerDown(face), "slingshot press");
            lab.PointerDrag((Vector2)face + new Vector2(-30f, -200f));
            yield return new WaitForSecondsRealtime(0.25f);
            yield return new WaitForEndOfFrame();
            Capture("10-slingshot-pull.png");
            lab.PointerUp((Vector2)face + new Vector2(-30f, -200f));
            Check(lab.Flying > 0, "slingshot releases a paintball");
            yield return new WaitForSecondsRealtime(0.5f);
            lab.Act(new Vector2(-0.4f, -0.3f));
            yield return new WaitForSecondsRealtime(0.5f);
            Check(lab.ToolHits[3] >= 1, "TC-07 slingshot hits");

            lab.SelectTool(ToolKind.Bat);
            lab.Act(new Vector2(0.1f, -0.62f));
            yield return new WaitForSecondsRealtime(0.24f);
            yield return new WaitForEndOfFrame();
            Capture("11-bat-swing.png");
            for (var swing = 0; swing < 8 && lab.Character.TeethLost == 0; swing++)
            {
                yield return new WaitForSecondsRealtime(0.75f);
                lab.Act(new Vector2(swing % 2 == 0 ? -0.15f : 0.15f, -0.62f));
            }
            yield return new WaitForSecondsRealtime(0.8f);
            Check(lab.ToolHits[4] >= 1, "TC-08 bat connects");
            Check(lab.Character.TeethLost + lab.Character.TeethChipped > 0, "teeth chip or fall out");

            lab.SelectTool(ToolKind.Dart);
            yield return Settle(0.3f);
            Capture("12-dart-in-hand.png");
            foreach (var point in new[] { new Vector2(0f, -0.12f), new Vector2(-0.5f, 0.75f), new Vector2(0.6f, -0.4f) })
            {
                lab.Act(point);
                yield return new WaitForSecondsRealtime(0.5f);
            }
            Check(lab.ToolHits[5] >= 3 && lab.Character.StuckDarts >= 3, "TC-09 darts stick");

            lab.SelectTool(ToolKind.Punch);
            for (var punch = 0; punch < 80 && (punch < 24 || !lab.PoopReady); punch++)
            {
                lab.Act(punch % 3 == 0 ? new Vector2(0f, -0.15f) : punch % 3 == 1 ? new Vector2(-0.43f, 0.35f) : new Vector2(0.62f, 0.75f));
                yield return new WaitForSecondsRealtime(0.2f);
            }
            yield return new WaitForSecondsRealtime(1.6f);
            yield return new WaitForEndOfFrame();
            Capture("13-damaged-face.png");
            var character = lab.Character;
            Check(character.Skin.CountMarked() > 1500, "persistent bruises, cuts and splats painted");
            Check(character.BlackEye[0] > 0f && character.EyeRed[0] > 0f, "black eye and bloodshot eye");
            Check(Mathf.Abs(character.NoseCrook) > 0f && character.Lumps > 0, "crooked nose and swelling");
            Check(character.Crying, "opponent cries when badly hurt");
            Check(lab.PoopReady, "fast hits fill the rage meter and unlock Poop Slap");
            Check(lab.Score > 0 && lab.Hits >= 20, "gameplay score recorded");

            Check(lab.UsePoopSlap(), "Poop Slap starts");
            yield return Settle(1.8f);
            Capture("14-ultimate-intro.png");
            yield return Settle(2.7f);
            Capture("15-poop-slaps.png");
            yield return Settle(4.6f);
            Check(lab.Ultimates == 1 && lab.Rage == 0f && !lab.UltimateRunning, "ultimate completes and empties the meter");
            Check(lab.Character.Flies > 0 && lab.Character.Stinky, "flies and stink after Poop Slap");
            Capture("16-flies.png");

            Check(lab.UseFeet() && lab.FeetActive > 0f, "Rotten Feet activates");
            lab.Act(new Vector2(0f, -0.2f));
            yield return new WaitForSecondsRealtime(0.17f);
            yield return new WaitForEndOfFrame();
            Capture("17-rotten-feet.png");
            yield return new WaitForSecondsRealtime(0.6f);
            yield return new WaitForEndOfFrame();
            Check(lab.Character.Wailing, "opponent wails with exaggerated tears");
            Capture("18-wailing.png");
            yield return new WaitForSecondsRealtime(5f);
            Check(lab.FeetActive <= 0f && lab.FeetCooldown > 10f && !lab.UseFeet(), "Rotten Feet cooldown");
            yield return new WaitForSecondsRealtime(3f);
            var background = new Vector2(lab.Viewport.x + lab.Viewport.width * 0.68f, lab.Viewport.y + lab.Viewport.height * 0.63f);
            lab.SelectTool(ToolKind.Punch);
            Check(lab.PointerDown(background) && lab.Laughs > 0, "opponent laughs when the player misses");

            lab.GoTo(Stage.After);
            Check(lab.Stage == Stage.After, "TC-10 finish goes to post-play check-in");
            lab.After = 4;
            yield return Settle(0.6f);
            Check(lab.After == 4, "TC-11 post-play rating stored");
            Capture("14-check-in-after.png");
            var savedLanguage = EmoSettings.Language;
            var savedLocation = EmoSettings.Location;
            EmoSettings.Language = EmoSettings.English;
            EmoSettings.Location = "";
            lab.RequestEmoTips();
            yield return Settle(0.6f);
            Check(lab.EmoTips.IsLoading, "AI loading overlay shows while waiting");
            Capture("15a-ai-loading.png");
            yield return WaitForTips();
            Check(lab.Stage == Stage.Result && lab.EmoTips.CurrentResponse != null, "AI tips arrive (or fall back) and open results");
            yield return Settle(0.9f);
            Check(lab.Before - lab.After == 4, "TC-12 difference calculated");
            Check(FaceLab.Tips(lab.Before, lab.After, lab.Mood).Count == 3, "three offline tips");
            Capture("15-result.png");
            lab.EmoTips.ActiveTab = 1;
            yield return Settle(0.4f);
            Capture("15b-result-places.png");

            EmoSettings.Language = EmoSettings.Thai;
            EmoSettings.Location = "เชียงใหม่";
            lab.EmoTips.Clear();
            lab.GoTo(Stage.After);
            lab.RequestEmoTips();
            yield return WaitForTips();
            yield return Settle(0.9f);
            Capture("15c-result-thai.png");
            lab.EmoTips.ActiveTab = 1;
            yield return Settle(0.4f);
            Capture("15d-result-thai-places.png");
            lab.GoTo(Stage.Home);
            yield return Settle(0.6f);
            Capture("15e-home-thai-setup.png");
            EmoSettings.Language = savedLanguage;
            EmoSettings.Location = savedLocation;

            lab.GoTo(Stage.Before);
            Check(lab.Character.TotalDamage == 0f && lab.Character.StuckDarts == 0 && lab.Character.TeethLost == 0, "TC-15 replay clears the face");
            lab.GoTo(Stage.Customize);
            lab.GoTo(Stage.Play);
            Check(lab.Score == 0 && lab.Hits == 0 && lab.Ultimates == 0, "TC-15 replay clears gameplay score");
            Check(!lab.PointerDown(new Vector2(5f, Screen.height * 0.5f)), "presentation area ignores input");

            lab.SetPhoneFrame(false);
            SetResolution(1280, 720);
            yield return Settle(0.8f);
            Check(Mathf.Abs(lab.Viewport.width / lab.Viewport.height - 16f / 9f) < 0.01f, "fullscreen 16:9 layout");
            Capture("16-play-no-frame-16x9.png");
            SetResolution(2400, 1080);
            yield return Settle(0.8f);
            Capture("17-play-no-frame-20x9.png");
            lab.SetPhoneFrame(true);
            Check(errors == 0, "no runtime errors");
            Debug.Log("EMORELIEF_SMOKE_OK: landscape phone flow, mood, six tools, hands and gloves, damage, voice, rage meter, Poop Slap, Rotten Feet, miss laughs, results and replay. Separate editor diagnostics: " + editorDiagnostics);
            Exit(0);
        }

        private static IEnumerator Settle(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            yield return new WaitForEndOfFrame();
        }

        private IEnumerator WaitForTips()
        {
            var deadline = Time.realtimeSinceStartup + EmoAIClient.RequestTimeoutSeconds + 5f;
            while ((lab.EmoTips.IsLoading || lab.Stage != Stage.Result) && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        private static void SetResolution(int width, int height)
        {
#if UNITY_EDITOR
            var assembly = typeof(UnityEditor.Editor).Assembly;
            var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            var sizes = sizesType.GetProperty("instance", flags | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy).GetValue(null);
            var getGroup = sizesType.GetMethod("GetGroup", flags | System.Reflection.BindingFlags.Instance);
            var groupType = getGroup.GetParameters()[0].ParameterType;
            var group = getGroup.Invoke(sizes, new[] { Enum.Parse(groupType, "Standalone") });
            var groupClass = group.GetType();
            var label = "EmoRelief " + width + "x" + height;
            var labels = (string[])groupClass.GetMethod("GetDisplayTexts").Invoke(group, null);
            var index = Array.FindIndex(labels, value => value.StartsWith(label, StringComparison.Ordinal));
            if (index < 0)
            {
                var sizeType = assembly.GetType("UnityEditor.GameViewSize");
                var kindType = assembly.GetType("UnityEditor.GameViewSizeType");
                var size = Activator.CreateInstance(sizeType, flags | System.Reflection.BindingFlags.Instance, null,
                    new object[] { Enum.Parse(kindType, "FixedResolution"), width, height, label }, null);
                groupClass.GetMethod("AddCustomSize").Invoke(group, new[] { size });
                index = (int)groupClass.GetMethod("GetTotalCount").Invoke(group, null) - 1;
            }
            var viewType = assembly.GetType("UnityEditor.GameView");
            var gameView = UnityEditor.EditorWindow.GetWindow(viewType);
            viewType.GetProperty("selectedSizeIndex", flags | System.Reflection.BindingFlags.Instance).SetValue(gameView, index);
            gameView.Focus();
#else
            Screen.SetResolution(width, height, FullScreenMode.Windowed);
#endif
        }

        private static void Exit(int code)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(code);
#else
            Application.Quit(code);
#endif
        }

        private void Capture(string name)
        {
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0f, 0f, Screen.width, Screen.height), 0, 0);
            texture.Apply();
            var pixels = texture.GetPixels32();
            var colors = new HashSet<int>();
            var area = lab.Viewport;
            for (var vertical = (int)(area.y + area.height * 0.25f); vertical < area.y + area.height * 0.75f; vertical += 7)
                for (var horizontal = (int)(area.x + area.width * 0.1f); horizontal < area.x + area.width * 0.9f; horizontal += 7)
                {
                    var pixel = pixels[vertical * Screen.width + horizontal];
                    colors.Add((pixel.r / 8 << 10) | (pixel.g / 8 << 5) | pixel.b / 8);
                }
            File.WriteAllBytes(Path.Combine(output, name), texture.EncodeToPNG());
            Debug.Log("CAPTURE: " + name + " " + Screen.width + "x" + Screen.height + " color bins=" + colors.Count);
            Destroy(texture);
            Check(colors.Count > 45, "nonblank rendering: " + name);
        }

        private void CountErrors(string message, string stack, LogType type)
        {
#if UNITY_EDITOR
            if (stack.Contains("UnityEditor.Search.SearchDatabase") ||
                (message.StartsWith("Curl error 6:", StringComparison.Ordinal) &&
                 (message.Contains("cdp.cloud.unity3d.com") || message.Contains("config.uca.cloud.unity3d.com"))))
            {
                editorDiagnostics++;
                return;
            }
#endif
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        }

        private static void Check(bool condition, string description)
        {
            if (condition)
            {
                Debug.Log("PASS: " + description);
                return;
            }
            Debug.LogError("FAIL: " + description);
            Exit(1);
            throw new InvalidOperationException(description);
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= CountErrors;
        }
    }
}

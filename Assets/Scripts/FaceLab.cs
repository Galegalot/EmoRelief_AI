using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EmoRelief
{
    public enum Stage { Home, Before, Customize, Play, After, Result }

    public sealed class FaceLab : MonoBehaviour
    {
        private const float PhoneAspect = 20f / 9f;
        public static readonly string[] MoodNames = { "Angry", "Frustrated", "Stressed", "Sad", "Anxious", "Calm" };
        private static readonly Color[] MoodColors = { UiKit.Red, UiKit.Orange, new Color(0.95f, 0.72f, 0.1f), new Color(0.25f, 0.5f, 0.95f), new Color(0.6f, 0.35f, 0.9f), UiKit.Green };
        private static readonly string[] ToolIcons = { "punch", "slap", "egg", "slingshot", "bat", "dart", "foot", "poop" };
        private static readonly Color[] ToolColors = { UiKit.Red, UiKit.Magenta, new Color(0.6f, 0.75f, 0.2f), UiKit.Cyan, UiKit.Orange, UiKit.Purple, new Color(0.45f, 0.62f, 0.2f), new Color(0.55f, 0.32f, 0.12f) };
        private static readonly string[] ToolHints =
        {
            "Tap the face to throw a punch",
            "Tap a cheek to slap it",
            "Tap where the egg should land",
            "Press, pull back, release to fire",
            "Tap to swing the bat",
            "Tap to throw - the nose is the bullseye",
            "Tap the face to stomp it with a ROTTEN FOOT!",
            "ULTIMATE in progress!"
        };
        private static readonly string[] MaleNames = { "GRUMPY GUS", "MOODY MAX", "RUDE RICK", "BOSSY BOB", "CRANKY CARL", "SMUG STEVE" };
        private static readonly string[] FemaleNames = { "RUDE RITA", "BOSSY BEA", "SNOOTY SUE", "GRUMPY GINA", "CRANKY KIM", "SMUG SALLY" };
        private static readonly string[] Categories = { "CHARACTER", "SEX", "FACE", "SKIN", "EYES", "NOSE", "MOUTH", "HAIR", "EARS", "SCENE", "EFFECTS" };

        public Stage Stage { get; private set; }
        public FaceCharacter Character { get; private set; }
        public PlayerRig Rig { get; private set; }
        public FaceStyle Style { get; private set; } = FaceStyle.Character(0);
        public string OpponentName { get; private set; } = "BIG BRUNO";
        public int CharacterIndex { get; private set; }
        public int Scene { get; private set; } = 4;
        public int Mood { get; set; } = -1;
        public int Before { get; set; } = 5;
        public int After { get; set; } = 5;
        public int Score { get; private set; }
        public int Hits { get; private set; }
        public int Chain { get; private set; }
        public float PlayTime { get; private set; }
        public bool Paused { get; private set; }
        public bool BloodEnabled { get; private set; } = true;
        public bool SoundOn { get; private set; } = true;
        public bool VoiceOn { get; private set; } = true;
        public bool ShakeOn { get; private set; } = true;
        public bool PhoneFrame { get; private set; } = true;
        public int VoiceLines { get; private set; }
        public Rect Viewport { get; private set; }
        public int Flying => projectiles.Count;
        public float Rage { get; private set; }
        public bool PoopReady => Rage >= 100f;
        public int Ultimates { get; private set; }
        public int FeetUses { get; private set; }
        public float FeetCooldown { get; private set; }
        public float FeetActive { get; private set; }
        public bool UltimateRunning => ultiTime >= 0f;
        public int Laughs { get; private set; }
        public readonly int[] ToolHits = new int[8];

        private sealed class Projectile
        {
            public Transform Body;
            public ToolKind Tool;
            public HitPoint Hit;
            public Vector3 Start;
            public Vector3 Miss;
            public Vector3 Last;
            public float Age;
            public float Duration;
            public float Arc;
            public float Power;
        }

        private sealed class Bit
        {
            public Transform Body;
            public Renderer Renderer;
            public Vector3 Velocity;
            public Vector3 Spin;
            public float Life;
            public float Gravity;
        }

        private sealed class Popup
        {
            public string Text;
            public Vector3 World;
            public float Age;
            public Color Color;
            public float Tilt;
            public bool Screen;
            public float Scale = 1f;
            public float Life = 0.85f;
        }

        private readonly List<Projectile> projectiles = new List<Projectile>();
        private readonly List<Popup> popups = new List<Popup>();
        private readonly Bit[] bits = new Bit[120];
        private readonly ArtKit kit = new ArtKit();
        private readonly Backdrops backdrops = new Backdrops();
        private readonly List<AudioClip> clips = new List<AudioClip>();
        private readonly Dictionary<string, List<AudioClip>> voices = new Dictionary<string, List<AudioClip>>();
        private readonly List<Rect> hud = new List<Rect>();
        private readonly float[] voiceSamples = new float[128];
        private readonly System.Random random = new System.Random(41);
        private UiKit ui;
        private PropKit props;
        private Camera view;
        private Camera border;
        private AudioSource sound;
        private AudioSource voice;
        private AudioClip punchClip, slapClip, splatClip, twangClip, crackClip, thunkClip, tinkClip, whooshClip, clickClip, popClip, bellClip;
        private Material bloodMaterial, toothMaterial, shellMaterial, starMaterial, tearMaterial, stinkMaterial, poopMaterial;
        private Rect screenGui;
        private Vector3 cameraPosition;
        private float cameraPitch;
        private float clock;
        private float shake;
        private float flash;
        private float freeze;
        private float lastHit = -10f;
        private float lastVoice = -10f;
        private float ultiTime = -1f;
        private int ultiSlaps;
        private bool ultiSuccess;
        private ToolKind resumeTool;
        private AudioSource announcer;
        private AudioSource buzz;
        private AudioClip chargeClip, buzzClip, unlockClip;
        private Light rim;
        private int nextBit;
        private bool pulling;
        private Vector2 pullStart;
        private Vector2 pullNow;
        private bool queued;
        private HitPoint queuedHit;
        private bool smoke;
        private bool settingsOpen;
        private int category;
        private CombatTelemetryTracker combatTracker;
        private EmoTipUIManager emoTipUI;

        public EmoTipUIManager EmoTips => emoTipUI;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            smoke = Array.IndexOf(Environment.GetCommandLineArgs(), "-emoreliefSmoke") >= 0;
            LoadSettings();
            view = Camera.main;
            view.fieldOfView = PlayerRig.FieldOfView;
            view.nearClipPlane = 0.05f;
            view.farClipPlane = 90f;
            view.depth = 1f;
            border = new GameObject("Presentation Camera", typeof(Camera)).GetComponent<Camera>();
            border.backgroundColor = UiKit.Ink;
            border.clearFlags = CameraClearFlags.SolidColor;
            border.cullingMask = 0;
            border.depth = 0f;
            border.transform.SetParent(transform, false);
            rim = new GameObject("Soft rim", typeof(Light)).GetComponent<Light>();
            rim.transform.SetParent(transform, false);
            rim.type = LightType.Directional;
            rim.color = new Color(0.85f, 0.75f, 1f);
            rim.intensity = 1.1f;
            rim.transform.rotation = Quaternion.Euler(15f, 155f, 0f);

            ui = new UiKit { Click = () => Play(clickClip, 0.6f, 1f) };
            combatTracker = new CombatTelemetryTracker();
            emoTipUI = new EmoTipUIManager(ui);
            gameObject.AddComponent<PythonProcessManager>();
            props = new PropKit();
            Character = new FaceCharacter { Props = props, Debris = Spawn, BloodEnabled = BloodEnabled };
            Character.Build(transform, Style);
            backdrops.Build(Scene, transform, view);
            Rig = new PlayerRig(transform, props) { Character = Character, Impact = Impact, Release = Launch };
            Rig.SetVisible(false);
            CreateBits();
            CreateAudio();
            LoadVoices();
            UpdateViewport();
            Pose(Stage.Home, out cameraPosition, out cameraPitch);
            if (smoke) gameObject.AddComponent<PrototypeSmokeTest>();
        }

        // ---------- Settings ----------

        private void LoadSettings()
        {
            SoundOn = PlayerPrefs.GetInt("emo.sound", 1) == 1;
            VoiceOn = PlayerPrefs.GetInt("emo.voice", 1) == 1;
            BloodEnabled = PlayerPrefs.GetInt("emo.blood", 1) == 1;
            ShakeOn = PlayerPrefs.GetInt("emo.shake", 1) == 1;
            PhoneFrame = PlayerPrefs.GetInt("emo.phone", 1) == 1;
        }

        private void SaveSettings()
        {
            PlayerPrefs.SetInt("emo.sound", SoundOn ? 1 : 0);
            PlayerPrefs.SetInt("emo.voice", VoiceOn ? 1 : 0);
            PlayerPrefs.SetInt("emo.blood", BloodEnabled ? 1 : 0);
            PlayerPrefs.SetInt("emo.shake", ShakeOn ? 1 : 0);
            PlayerPrefs.SetInt("emo.phone", PhoneFrame ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void SetBlood(bool enabled)
        {
            BloodEnabled = enabled;
            Character.BloodEnabled = enabled;
            SaveSettings();
        }

        public void SetPhoneFrame(bool enabled)
        {
            PhoneFrame = enabled;
            SaveSettings();
            UpdateViewport();
        }

        public void SetVoice(bool enabled)
        {
            VoiceOn = enabled;
            if (!enabled) voice.Stop();
            SaveSettings();
        }

        public void SetSound(bool enabled)
        {
            SoundOn = enabled;
            SaveSettings();
        }

        // ---------- Flow ----------

        public void GoTo(Stage next)
        {
            if (Stage == Stage.Play && next != Stage.Play)
            {
                Rig.SetVisible(false);
                ClearProjectiles();
                pulling = false;
            }
            switch (next)
            {
                case Stage.Home:
                case Stage.Before:
                    Character.Reset();
                    voice.Stop();
                    break;
                case Stage.Play:
                    Rig.SetVisible(true);
                    if (Stage == Stage.After) break;
                    Character.Reset();
                    Score = 0;
                    Hits = 0;
                    Chain = 0;
                    PlayTime = 0f;
                    Rage = 0f;
                    Ultimates = 0;
                    FeetUses = 0;
                    FeetCooldown = 0f;
                    FeetActive = 0f;
                    ultiTime = -1f;
                    Laughs = 0;
                    lastHit = clock;
                    lastVoice = clock - 5f;
                    Array.Clear(ToolHits, 0, ToolHits.Length);
                    SelectTool(ToolKind.Punch);
                    combatTracker.StartCombat();
                    emoTipUI.Clear();
                    break;
                case Stage.After:
                    if (Stage == Stage.Play) After = Before;
                    break;
                case Stage.Result:
                    // Direct jumps (smoke test) skip the AI request; show offline tips instead.
                    if (emoTipUI.CurrentResponse == null)
                        emoTipUI.SetResponse(EmoAIClient.CreateFallback(combatTracker.FinishCombat(EmoSettings.Location, EmoSettings.Language)));
                    break;
            }
            GUIUtility.keyboardControl = 0;
            Paused = false;
            settingsOpen = false;
            Stage = next;
        }

        /// <summary>Sends this round's telemetry to the AI, showing the loading overlay until it answers.</summary>
        public void RequestEmoTips()
        {
            if (emoTipUI.IsLoading) return;
            var telemetry = combatTracker.FinishCombat(EmoSettings.Location, EmoSettings.Language);
            emoTipUI.StartLoading();
            StartCoroutine(EmoAIClient.RequestAnalysis(telemetry, response =>
            {
                emoTipUI.SetResponse(response);
                GoTo(Stage.Result);
            }));
        }

        public void ApplyStyle(FaceStyle style, int scene)
        {
            var sexChanged = style.Sex != Style.Sex;
            Style = style;
            Character.Build(transform, style);
            Rig.Character = Character;
            if (sexChanged || OpponentName == null) RollName();
            if (scene == Scene) return;
            Scene = scene;
            backdrops.Build(scene, transform, view);
        }

        public void ChooseCharacter(int index)
        {
            CharacterIndex = index;
            ApplyStyle(FaceStyle.Character(index), Scene);
            if (index < 4) OpponentName = FaceStyle.CharacterNames[index].ToUpperInvariant();
            else RollName();
        }

        public void RollName()
        {
            var names = Style.Sex == 1 ? FemaleNames : MaleNames;
            OpponentName = names[random.Next(names.Length)];
        }

        public void SelectTool(ToolKind tool)
        {
            if (UltimateRunning) return;
            if (FeetActive > 0f) EndFeet();
            Rig.SetTool(tool);
            pulling = false;
            queued = false;
        }

        public void ResetFace()
        {
            Character.Reset();
            ClearProjectiles();
        }

        public static string Band(int score)
        {
            if (score <= 2) return "Very low";
            if (score <= 4) return "Low";
            if (score <= 6) return "Moderate";
            if (score <= 8) return "High";
            return "Very high";
        }

        private static Color BandColor(int score) => Color.Lerp(UiKit.Green, UiKit.Red, score / 10f);

        public static List<string> Tips(int before, int after) => Tips(before, after, -1);

        public static List<string> Tips(int before, int after, int mood)
        {
            var tips = new List<string>();
            var change = before - after;
            if (after >= 7) tips.Add("Your anger is still high. Step away from the trigger for 10-20 minutes before you reply to anyone, and try box breathing: in 4, hold 4, out 4, hold 4.");
            else if (after >= 5) tips.Add("Some anger is still there. A short walk, cold water on your face, or stretching can help it settle.");
            else tips.Add("You are in a calmer place. Drink some water and take three slow breaths to lock in the calm.");
            switch (mood)
            {
                case 1: tips.Add("Frustrated? Write down what blocked you and one small thing you can control next."); break;
                case 2: tips.Add("Stressed? Break your next task into one step you can finish in five minutes."); break;
                case 3: tips.Add("Feeling sad is okay. Reach out to a friend or family member today."); break;
                case 4: tips.Add("Anxious? Try grounding: name 5 things you see, 4 you can touch and 3 you can hear."); break;
                case 5: tips.Add("You started calm. Notice what keeps you balanced and make time for it."); break;
                default: tips.Add("Name what made you angry in one sentence. Putting it into words often makes it feel smaller."); break;
            }
            if (change > 0) tips.Add("You feel " + change + (change == 1 ? " point" : " points") + " calmer than before. Remember what helped for next time.");
            else if (change == 0) tips.Add("Your rating did not change. Talking to someone you trust may help more than more play.");
            else tips.Add("You feel angrier than before. Take a break from the game and try a calming activity instead.");
            return tips;
        }

        // ---------- Play actions ----------

        public bool Act(Vector2 local, float power = 1f)
        {
            if (Stage != Stage.Play) return false;
            combatTracker.RecordClick();
            var hit = Character.ProbeLocal(local.x, local.y);
            return hit.Valid && Trigger(hit, power);
        }

        private bool Trigger(HitPoint hit, float power)
        {
            if (Paused || UltimateRunning) return false;
            if (Rig.Tool == ToolKind.Slingshot)
            {
                if (!Rig.Ready) return false;
                Rig.FireSling(hit, power, Vector3.zero);
                return true;
            }
            if (Rig.Begin(hit, power))
            {
                if (Rig.Tool != ToolKind.Punch) Play(whooshClip, 0.5f, 0.9f + (float)random.NextDouble() * 0.2f);
                return true;
            }
            queued = true;
            queuedHit = hit;
            return true;
        }

        private void Launch(ToolKind tool, HitPoint hit, float power, Vector3 start, Vector3 miss)
        {
            Transform body;
            float duration, arc;
            switch (tool)
            {
                case ToolKind.Egg: body = props.Egg(transform); duration = 0.32f; arc = 0.7f; break;
                case ToolKind.Dart: body = props.Dart(transform); duration = 0.2f; arc = 0.18f; break;
                default:
                    body = props.Paintball(transform, random.Next(PropKit.PaintColors.Length));
                    duration = Mathf.Lerp(0.3f, 0.13f, power);
                    arc = 0.15f * (1f - power);
                    Play(twangClip, 0.8f, 0.9f + power * 0.3f);
                    break;
            }
            body.position = start;
            projectiles.Add(new Projectile { Body = body, Tool = tool, Hit = hit, Start = start, Miss = miss, Last = start, Duration = duration, Arc = arc, Power = power });
            if (tool != ToolKind.Slingshot) Play(whooshClip, 0.45f, 1.2f);
        }

        private void Impact(ToolKind tool, HitPoint hit, float power, Vector3 direction)
        {
            var result = Character.ApplyHit(tool, hit, power, direction);
            Score += result.Score * (Chain >= 5 ? 2 : 1);
            Hits++;
            ToolHits[(int)tool]++;
            combatTracker.RecordHit(tool);
            if (!UltimateRunning) AddRage(tool, clock - lastHit);
            Chain = clock - lastHit < 1.3f ? Chain + 1 : 1;
            lastHit = clock;
            var world = Character.Root.TransformPoint(hit.Local);
            switch (tool)
            {
                case ToolKind.Punch: shake = 0.45f; freeze = 0.04f; Play(punchClip, 1f, 0.9f + (float)random.NextDouble() * 0.25f); break;
                case ToolKind.Slap: shake = 0.5f; freeze = 0.05f; Play(slapClip, 1f, 0.95f + (float)random.NextDouble() * 0.15f); break;
                case ToolKind.Egg: shake = 0.15f; Play(splatClip, 1f, 1f); break;
                case ToolKind.Slingshot: shake = 0.25f; Play(popClip, 0.8f, 1f); Play(splatClip, 0.5f, 1.4f); break;
                case ToolKind.Bat: shake = 1f; freeze = 0.09f; Play(crackClip, 1f, 0.9f + (float)random.NextDouble() * 0.15f); break;
                case ToolKind.Dart: shake = 0.1f; Play(thunkClip, 1f, 1f); break;
                case ToolKind.Feet: shake = 0.85f; freeze = 0.07f; Play(punchClip, 1f, 0.7f); Play(splatClip, 0.7f, 0.8f); break;
                case ToolKind.Poop: shake = 0.6f; freeze = 0.05f; Play(slapClip, 1f, 0.9f); Play(splatClip, 0.9f, 0.85f); break;
            }
            if (result.Word == "TOOTH!") Play(tinkClip, 0.8f, 1f);
            flash = result.Heavy ? 0.35f : 0.12f;
            if (!string.IsNullOrEmpty(result.Word))
            {
                if (popups.Count > 6) popups.RemoveAt(0);
                popups.Add(new Popup { Text = result.Word, World = world + Vector3.up * 0.3f, Color = Color.Lerp(ToolColors[(int)tool], Color.white, 0.35f), Tilt = (float)random.NextDouble() * 24f - 12f });
            }
            if (tool == ToolKind.Feet)
            {
                if (VoiceOn && (!voice.isPlaying || clock - lastVoice > 1.5f)) Say("wail");
                return;
            }
            if (tool == ToolKind.Poop) return;
            React(tool, result);
        }

        private static readonly float[] RageGain = { 2f, 1.5f, 1f, 1.5f, 3.5f, 1f, 2.5f, 0f };

        private void AddRage(ToolKind tool, float interval)
        {
            if (PoopReady) return;
            var speed = interval < 0.35f ? 1.4f : interval < 0.7f ? 1f : interval < 1.2f ? 0.6f : 0.3f;
            Rage = Mathf.Min(100f, Rage + RageGain[(int)tool] * speed);
            if (!PoopReady) return;
            Play(unlockClip, 1f, 1f);
            popups.Add(new Popup { Text = "POOP SLAP UNLOCKED!", World = Character.Root.position + Vector3.up * 1.8f, Color = UiKit.Yellow });
        }

        public bool UsePoopSlap()
        {
            if (!PoopReady || UltimateRunning || Stage != Stage.Play || Paused) return false;
            if (FeetActive > 0f) EndFeet();
            resumeTool = Rig.Tool;
            Rig.SetTool(ToolKind.Poop);
            ultiTime = 0f;
            ultiSlaps = 0;
            ultiSuccess = false;
            pulling = false;
            queued = false;
            Announce("ulti");
            Play(chargeClip, 0.9f, 1f);
            return true;
        }

        public const int UltiSlapCount = 20;
        private const float UltiStart = 2.6f;

        // Slaps start slow, accelerate to full speed by slap 10, then hold that speed to slap 20.
        private static float SlapGap(int slap) => slap < 9 ? Mathf.Lerp(0.45f, 0.1f, slap / 9f) : 0.1f;

        private static float SlapTime(int slap)
        {
            var time = UltiStart;
            for (var index = 0; index < slap; index++) time += SlapGap(index);
            return time;
        }

        private void UpdateUltimate(float delta)
        {
            ultiTime += delta;
            Rig.Showcase = ultiTime < UltiStart ? Mathf.Clamp01(Mathf.Min(ultiTime / 0.35f, (UltiStart - ultiTime) / 0.3f)) : 0f;
            while (ultiSlaps < UltiSlapCount && ultiTime >= SlapTime(ultiSlaps))
            {
                Rig.SlapSpeed = 0.45f / SlapGap(ultiSlaps) * 0.55f + 0.45f;
                var side = ultiSlaps % 2 == 0 ? 1f : -1f;
                var hit = Character.ProbeLocal(side * (0.45f + (float)random.NextDouble() * 0.2f), -0.3f + (float)random.NextDouble() * 0.5f);
                if (hit.Valid) Rig.Begin(hit, 1.3f, true);
                if (ultiSlaps == 5) Say("hate");
                if (ultiSlaps == 14) Say("cry");
                ultiSlaps++;
            }
            var last = SlapTime(UltiSlapCount - 1);
            if (!ultiSuccess && ultiTime >= last + 0.5f)
            {
                ultiSuccess = true;
                Announce("success");
                Play(bellClip, 0.8f, 1.2f);
            }
            if (ultiTime < last + 2.6f) return;
            ultiTime = -1f;
            Rig.SlapSpeed = 1f;
            Rage = 0f;
            Ultimates++;
            Rig.SetTool(resumeTool);
        }

        public bool UseFeet()
        {
            if (FeetCooldown > 0f || FeetActive > 0f || UltimateRunning || Stage != Stage.Play || Paused) return false;
            resumeTool = Rig.Tool;
            FeetActive = 5f;
            FeetUses++;
            Rig.SetTool(ToolKind.Feet);
            Announce("feet");
            return true;
        }

        private void EndFeet()
        {
            FeetActive = 0f;
            FeetCooldown = 20f;
            Rig.SetTool(resumeTool);
        }

        private void Miss()
        {
            popups.Add(new Popup { Text = "MISS!", World = Character.Root.position + new Vector3((float)random.NextDouble() * 2f - 1f, 1.4f, -1f), Color = UiKit.White });
            if (!VoiceOn || voice.isPlaying || clock - lastVoice < 1.2f) return;
            var pick = random.Next(3);
            Say(pick == 0 ? "laugh" : pick == 1 ? "giggle" : "missed");
            var caption = pick == 0 ? "*HAHAHAHAHA!*" : pick == 1 ? "*HA HA!*" : "\"YOU MISSED! HAHA!\"";
            popups.Add(new Popup { Text = caption, World = Character.Root.position + new Vector3(0.9f, 1.3f, -0.6f), Color = UiKit.Cyan, Scale = 0.5f, Life = 1.8f });
            Laughs++;
        }

        private void Announce(string line)
        {
            if (!SoundOn || !voices.TryGetValue("a_" + line, out var options) || options.Count == 0) return;
            announcer.Stop();
            announcer.clip = options[0];
            announcer.Play();
        }

        private void React(ToolKind tool, HitResult result)
        {
            if (!VoiceOn || voice.isPlaying || clock - lastVoice < 0.9f) return;
            if (random.NextDouble() > (tool == ToolKind.Bat || result.Word == "TOOTH!" ? 0.95f : 0.6f)) return;
            string line;
            var damage = Character.TotalDamage;
            if (tool == ToolKind.Egg && random.NextDouble() < 0.6f) line = "gross";
            else if (Character.Crying) line = random.NextDouble() < 0.55f ? "cry" : "sorry";
            else if (damage > 10f) line = random.NextDouble() < 0.5f ? "sorry" : "pain";
            else if (damage > 4f) line = random.NextDouble() < 0.5f ? "pain" : "hurt";
            else line = "hurt";
            Say(line);
        }

        private void Say(string line)
        {
            if (!VoiceOn) return;
            if (!voices.TryGetValue((Style.Sex == 1 ? "f_" : "m_") + line, out var options) || options.Count == 0) return;
            voice.Stop();
            voice.clip = options[random.Next(options.Count)];
            voice.pitch = (Style.Sex == 1 ? 1.06f : 1.12f) + (float)random.NextDouble() * 0.1f + (line == "cry" ? 0.12f : 0f);
            voice.Play();
            lastVoice = clock;
            VoiceLines++;
        }

        private void ClearProjectiles()
        {
            foreach (var projectile in projectiles) ArtKit.Destroy(projectile.Body.gameObject);
            projectiles.Clear();
            queued = false;
        }

        // ---------- Frame update ----------

        public void UpdateViewport()
        {
            float width = Screen.width, height = Screen.height;
            Rect area;
            if (PhoneFrame)
            {
                var h = Mathf.Min(height * 0.74f, width * 0.86f / (PhoneAspect + 0.14f));
                var w = h * PhoneAspect;
                area = new Rect((width - w) * 0.5f, (height - h) * 0.5f - height * 0.025f, w, h);
            }
            else
            {
                var aspect = Mathf.Clamp(width / height, 16f / 9f, 21f / 9f);
                var w = Mathf.Min(width, height * aspect);
                var h = w / aspect;
                area = new Rect((width - w) * 0.5f, (height - h) * 0.5f, w, h);
            }
            screenGui = area;
            Viewport = new Rect(area.x, height - area.yMax, area.width, area.height);
            view.rect = new Rect(Viewport.x / width, Viewport.y / height, Viewport.width / width, Viewport.height / height);
            PlayerRig.Aspect = area.width / area.height;
        }

        private float DesignWidth => 540f * screenGui.width / Mathf.Max(1f, screenGui.height);

        private void Pose(Stage stage, out Vector3 position, out float pitch)
        {
            float focus, distance, height;
            pitch = 0f;
            switch (stage)
            {
                case Stage.Play: position = PlayerRig.CameraPosition; return;
                case Stage.Home: focus = 0.7f; distance = 9f; height = 0.2f; break;
                case Stage.Customize: focus = 0.47f; distance = 7.8f; height = 0.25f; break;
                // Keep the opponent inside the clear left column (0..ResultPanelLeft) on any aspect ratio.
                case Stage.Result: focus = 185f / DesignWidth; distance = 10.5f; height = 0.15f; break;
                default: focus = 0.77f; distance = 9f; height = 0.15f; break;
            }
            var tan = Mathf.Tan(PlayerRig.FieldOfView * 0.5f * Mathf.Deg2Rad);
            position = new Vector3(-(focus - 0.5f) * 2f * distance * tan * PlayerRig.Aspect, height, -distance);
        }

        private void Update()
        {
            UpdateViewport();
            var delta = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            ui.Pressed = (Mouse.current != null && Mouse.current.leftButton.isPressed) || (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed);
            ReadInput();
            Character.Talk = Mathf.MoveTowards(Character.Talk, VoiceLevel(), delta * 12f);
            // Ticked before the pause early-out so menu pauses are measured too.
            if (Stage == Stage.Play) combatTracker.Tick(delta, Paused);
            if (Paused) return;
            clock += delta;
            var simulation = delta;
            if (freeze > 0f)
            {
                freeze -= delta;
                simulation *= 0.08f;
            }
            if (Stage == Stage.Play)
            {
                PlayTime += delta;
                if (queued && Rig.Ready)
                {
                    queued = false;
                    if (Rig.Begin(queuedHit, 1f) && Rig.Tool != ToolKind.Punch) Play(whooshClip, 0.5f, 1f);
                }
                if (Rig.Tool == ToolKind.Slingshot) UpdateAim();
                Rig.Update(simulation);
                UpdateProjectiles(simulation);
                if (clock - lastHit > 7f && clock - lastVoice > 8f && Character.TotalDamage < 10f) Say("taunt");
                if (UltimateRunning) UpdateUltimate(delta);
                if (FeetActive > 0f)
                {
                    FeetActive -= delta;
                    if (FeetActive <= 0f) EndFeet();
                }
                else FeetCooldown = Mathf.Max(0f, FeetCooldown - delta);
                var idle = clock - lastHit;
                if (!PoopReady && !UltimateRunning && idle > 1f) Rage = Mathf.Max(0f, Rage - (idle > 3f ? 15f : 8f) * delta);
            }
            var buzzing = Stage == Stage.Play && Character.Flies > 0 && SoundOn;
            if (buzzing != buzz.isPlaying) { if (buzzing) buzz.Play(); else buzz.Stop(); }
            Character.Angry = Stage == Stage.Play ? 0.5f : 1f;
            rim.color = Color.Lerp(rim.color, Stage == Stage.Play ? new Color(0.85f, 0.75f, 1f) : new Color(1f, 0.35f, 0.25f), delta * 3f);
            Character.Tick(simulation, clock);
            backdrops.Tick(clock);
            UpdateBits(simulation);
            for (var index = popups.Count - 1; index >= 0; index--)
            {
                popups[index].Age += delta;
                if (popups[index].Age > popups[index].Life) popups.RemoveAt(index);
            }
            if (clock - lastHit > 1.3f) Chain = 0;
            shake = Mathf.MoveTowards(shake, 0f, delta * 4f);
            flash = Mathf.MoveTowards(flash, 0f, delta * 2.5f);

            Pose(Stage, out var targetPosition, out var targetPitch);
            var blend = 1f - Mathf.Exp(-delta * 6f);
            cameraPosition = Vector3.Lerp(cameraPosition, targetPosition, blend);
            cameraPitch = Mathf.Lerp(cameraPitch, targetPitch, blend);
            var jolt = ShakeOn ? shake * shake * 0.09f : 0f;
            view.transform.position = cameraPosition + new Vector3(Mathf.Sin(clock * 93f), Mathf.Cos(clock * 77f), 0f) * jolt;
            view.transform.rotation = Quaternion.Euler(cameraPitch, 0f, ShakeOn ? Mathf.Sin(clock * 60f) * shake * shake * 1.2f : 0f);
        }

        private float VoiceLevel()
        {
            if (voice == null || !voice.isPlaying) return 0f;
            voice.GetOutputData(voiceSamples, 0);
            var sum = 0f;
            foreach (var sample in voiceSamples) sum += sample * sample;
            return Mathf.Clamp01(Mathf.Sqrt(sum / voiceSamples.Length) * 7f);
        }

        private void UpdateAim()
        {
            Vector3 aim;
            if (pulling)
            {
                Rig.Pull = PullPower();
                var ray = view.ScreenPointToRay(AimScreen());
                aim = Character.Probe(ray, out var hit) ? Character.Root.TransformPoint(hit.Local) : ray.GetPoint(8f);
            }
            else aim = Character.Root.position + Vector3.down * 0.1f;
            Rig.Aim = aim;
        }

        private Vector2 AimScreen()
        {
            var aim = pullStart + (pullStart - pullNow) * 0.6f;
            aim.x = Mathf.Clamp(aim.x, Viewport.x, Viewport.xMax);
            aim.y = Mathf.Clamp(aim.y, Viewport.y, Viewport.yMax);
            return aim;
        }

        private float PullPower() => Mathf.Clamp01((pullNow - pullStart).magnitude / (Viewport.height * 0.3f));

        private void UpdateProjectiles(float delta)
        {
            for (var index = projectiles.Count - 1; index >= 0; index--)
            {
                var projectile = projectiles[index];
                projectile.Age += delta;
                var t = projectile.Age / projectile.Duration;
                var target = projectile.Hit.Valid ? Character.Root.TransformPoint(projectile.Hit.Local) : projectile.Miss;
                var position = Vector3.LerpUnclamped(projectile.Start, target, t) + Vector3.up * projectile.Arc * 4f * t * (1f - t);
                projectile.Body.position = position;
                var motion = position - projectile.Last;
                if (projectile.Tool == ToolKind.Dart && motion.sqrMagnitude > 1e-6f) projectile.Body.rotation = Quaternion.LookRotation(motion);
                else projectile.Body.Rotate(new Vector3(400f, 230f, 0f) * delta, Space.Self);
                projectile.Last = position;
                if (t < 1f) continue;
                if (projectile.Hit.Valid) Impact(projectile.Tool, projectile.Hit, projectile.Power, target - projectile.Start);
                else if (t < 2.5f) continue;
                else Miss();
                ArtKit.Destroy(projectile.Body.gameObject);
                projectiles.RemoveAt(index);
            }
        }

        // ---------- Input ----------

        private void ReadInput()
        {
            var keyboard = Keyboard.current;
            // Letters typed into the location field must not trigger hotkeys (M mutes, V voice...).
            if (keyboard != null && GUIUtility.keyboardControl == 0)
            {
                if (keyboard.mKey.wasPressedThisFrame) SetSound(!SoundOn);
                if (keyboard.vKey.wasPressedThisFrame) SetVoice(!VoiceOn);
                if (keyboard.f2Key.wasPressedThisFrame) SetPhoneFrame(!PhoneFrame);
                if (keyboard.f11Key.wasPressedThisFrame)
                    Screen.fullScreenMode = Screen.fullScreen ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;
                if (Stage == Stage.Play)
                {
                    if (keyboard.escapeKey.wasPressedThisFrame) { Paused = !Paused; settingsOpen = false; }
                    if (keyboard.rKey.wasPressedThisFrame) ResetFace();
                    var keys = new[] { keyboard.digit1Key, keyboard.digit2Key, keyboard.digit3Key, keyboard.digit4Key, keyboard.digit5Key, keyboard.digit6Key };
                    for (var index = 0; index < keys.Length; index++)
                        if (keys[index].wasPressedThisFrame) SelectTool((ToolKind)index);
                    if (!Paused)
                    {
                        if (keyboard.aKey.wasPressedThisFrame) Act(new Vector2(-0.6f, -0.15f));
                        if (keyboard.dKey.wasPressedThisFrame) Act(new Vector2(0.6f, -0.15f));
                        if (keyboard.wKey.wasPressedThisFrame) Act(new Vector2(0.1f, 0.85f));
                        if (keyboard.sKey.wasPressedThisFrame) Act(new Vector2(0f, -0.65f));
                        if (keyboard.spaceKey.wasPressedThisFrame) Act(new Vector2(0f, -0.1f));
                    }
                }
            }
            if (Stage != Stage.Play || Paused) return;
            bool pressed, held, released;
            Vector2 position;
            var touch = Touchscreen.current;
            if (touch != null && (touch.primaryTouch.press.isPressed || touch.primaryTouch.press.wasReleasedThisFrame))
            {
                pressed = touch.primaryTouch.press.wasPressedThisFrame;
                held = touch.primaryTouch.press.isPressed;
                released = touch.primaryTouch.press.wasReleasedThisFrame;
                position = touch.primaryTouch.position.ReadValue();
            }
            else if (Mouse.current != null)
            {
                pressed = Mouse.current.leftButton.wasPressedThisFrame;
                held = Mouse.current.leftButton.isPressed;
                released = Mouse.current.leftButton.wasReleasedThisFrame;
                position = Mouse.current.position.ReadValue();
            }
            else return;
            if (pressed) PointerDown(position);
            if (held && pulling) pullNow = position;
            if (released && pulling) PointerUp(position);
        }

        public bool PointerDown(Vector2 position)
        {
            if (Stage != Stage.Play || Paused || !Viewport.Contains(position)) return false;
            var design = ToDesign(position);
            foreach (var area in hud)
                if (area.Contains(design)) return false;
            combatTracker.RecordClick();
            if (Rig.Tool == ToolKind.Slingshot)
            {
                pulling = true;
                pullStart = position;
                pullNow = position;
                return true;
            }
            if (UltimateRunning) return false;
            if (Character.Probe(view.ScreenPointToRay(position), out var hit)) return Trigger(hit, 1f);
            Miss();
            return true;
        }

        public void PointerDrag(Vector2 position)
        {
            if (pulling) pullNow = position;
        }

        public void PointerUp(Vector2 position)
        {
            if (!pulling) return;
            pullNow = position;
            var power = PullPower();
            var screen = AimScreen();
            pulling = false;
            Rig.Pull = 0f;
            if (power < 0.1f) power = 0.45f;
            if (!Rig.Ready) return;
            var ray = view.ScreenPointToRay(screen);
            Character.Probe(ray, out var hit);
            Rig.FireSling(hit, power, ray.GetPoint(14f));
        }

        private float GuiScale => screenGui.height / 540f;

        private Vector2 ToDesign(Vector2 screen) =>
            new Vector2((screen.x - screenGui.x) / GuiScale, (Screen.height - screen.y - screenGui.y) / GuiScale);

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && Stage == Stage.Play && !smoke) Paused = true;
        }

        // ---------- Debris ----------

        private void CreateBits()
        {
            bloodMaterial = kit.Lit("Blood drop", SkinPainter.BloodColor, 0.9f);
            toothMaterial = kit.Lit("Loose tooth", new Color(1f, 0.98f, 0.9f), 0.7f);
            shellMaterial = kit.Lit("Shell piece", new Color(0.98f, 0.9f, 0.76f), 0.4f);
            starMaterial = kit.Unlit("Impact spark", new Color(1f, 0.92f, 0.35f));
            tearMaterial = kit.Lit("Tear", FaceCharacter.TearColor, 0.95f);
            stinkMaterial = kit.Unlit("Stink", new Color(0.55f, 0.85f, 0.25f));
            poopMaterial = kit.Lit("Poop chunk", new Color(0.38f, 0.21f, 0.07f), 0.8f);
            var pool = ArtKit.Group("Debris", transform);
            for (var index = 0; index < bits.Length; index++)
            {
                var body = ArtKit.Ball("Bit", pool, Vector3.zero, Vector3.one * 0.05f, bloodMaterial);
                body.gameObject.SetActive(false);
                bits[index] = new Bit { Body = body, Renderer = body.GetComponent<Renderer>() };
            }
        }

        private void Spawn(Vector3 position, Vector3 velocity, DebrisKind kind, Color color)
        {
            var bit = bits[nextBit];
            nextBit = (nextBit + 1) % bits.Length;
            bit.Body.gameObject.SetActive(true);
            bit.Body.position = position;
            bit.Velocity = velocity;
            bit.Life = kind == DebrisKind.Star ? 0.22f : 1.8f;
            bit.Gravity = kind == DebrisKind.Star ? 0f : 9.5f;
            bit.Spin = new Vector3((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble()) * 720f;
            var size = 0.7f + (float)random.NextDouble() * 0.6f;
            switch (kind)
            {
                case DebrisKind.Tooth:
                    bit.Renderer.sharedMaterial = toothMaterial;
                    bit.Body.localScale = new Vector3(0.08f, 0.12f, 0.06f);
                    break;
                case DebrisKind.Shell:
                    bit.Renderer.sharedMaterial = shellMaterial;
                    bit.Body.localScale = new Vector3(0.14f, 0.03f, 0.1f) * size;
                    break;
                case DebrisKind.Paint:
                    var paint = Array.IndexOf(PropKit.PaintColors, color);
                    bit.Renderer.sharedMaterial = props.Paint[Mathf.Max(0, paint)];
                    bit.Body.localScale = Vector3.one * 0.07f * size;
                    break;
                case DebrisKind.Stink:
                    bit.Renderer.sharedMaterial = stinkMaterial;
                    bit.Body.localScale = Vector3.one * 0.11f * size;
                    bit.Gravity = -1.2f;
                    bit.Life = 1.4f;
                    break;
                case DebrisKind.Poop:
                    bit.Renderer.sharedMaterial = poopMaterial;
                    bit.Body.localScale = new Vector3(0.1f, 0.07f, 0.09f) * size;
                    break;
                case DebrisKind.Tear:
                    bit.Renderer.sharedMaterial = tearMaterial;
                    bit.Body.localScale = new Vector3(0.06f, 0.08f, 0.06f) * size;
                    break;
                case DebrisKind.Star:
                    bit.Renderer.sharedMaterial = starMaterial;
                    bit.Body.localScale = new Vector3(0.04f, 0.04f, 0.16f) * size;
                    bit.Body.rotation = Quaternion.LookRotation(velocity);
                    bit.Spin = Vector3.zero;
                    break;
                default:
                    bit.Renderer.sharedMaterial = bloodMaterial;
                    bit.Body.localScale = Vector3.one * 0.065f * size;
                    break;
            }
        }

        private void UpdateBits(float delta)
        {
            foreach (var bit in bits)
            {
                if (!bit.Body.gameObject.activeSelf) continue;
                bit.Life -= delta;
                bit.Velocity += Vector3.down * bit.Gravity * delta;
                if (bit.Gravity == 0f) bit.Velocity *= Mathf.Exp(-delta * 6f);
                bit.Body.position += bit.Velocity * delta;
                bit.Body.Rotate(bit.Spin * delta, Space.Self);
                if (bit.Gravity == 0f) bit.Body.localScale *= Mathf.Exp(-delta * 5f);
                if (bit.Life <= 0f || bit.Body.position.y < -2.25f) bit.Body.gameObject.SetActive(false);
            }
        }

        // ---------- Audio ----------

        private void CreateAudio()
        {
            sound = gameObject.AddComponent<AudioSource>();
            sound.playOnAwake = false;
            sound.spatialBlend = 0f;
            voice = gameObject.AddComponent<AudioSource>();
            var announcerObject = new GameObject("Announcer", typeof(AudioSource), typeof(AudioEchoFilter), typeof(AudioDistortionFilter));
            announcerObject.transform.SetParent(transform, false);
            announcer = announcerObject.GetComponent<AudioSource>();
            announcer.playOnAwake = false;
            announcer.pitch = 0.82f;
            var echo = announcerObject.GetComponent<AudioEchoFilter>();
            echo.delay = 110f;
            echo.decayRatio = 0.35f;
            echo.wetMix = 0.55f;
            announcerObject.GetComponent<AudioDistortionFilter>().distortionLevel = 0.35f;
            buzz = gameObject.AddComponent<AudioSource>();
            buzz.playOnAwake = false;
            buzz.loop = true;
            buzz.volume = 0.18f;
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
            voice.volume = 1f;
            var low = 0f;
            punchClip = Synth("Punch", 0.28f, (t, n) => Mathf.Sin(2f * Mathf.PI * (150f * t - 170f * t * t)) * Mathf.Exp(-t * 20f) * 0.9f + n * Mathf.Exp(-t * 90f) * 0.45f);
            slapClip = Synth("Slap", 0.18f, (t, n) => { var high = n - low; low = n; return high * Mathf.Exp(-t * 45f) * 0.7f + Mathf.Sin(2f * Mathf.PI * 950f * t) * Mathf.Exp(-t * 80f) * 0.3f; });
            splatClip = Synth("Splat", 0.38f, (t, n) => { low += (n - low) * 0.12f; return low * Mathf.Exp(-t * 10f) * (0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 32f * t)) * 2.2f; });
            twangClip = Synth("Twang", 0.42f, (t, n) => Mathf.Sin(2f * Mathf.PI * (210f + 35f * Mathf.Sin(2f * Mathf.PI * 7f * t)) * t) * Mathf.Exp(-t * 8f) * 0.6f);
            crackClip = Synth("Bat crack", 0.45f, (t, n) => Mathf.Sin(2f * Mathf.PI * 1750f * t) * Mathf.Exp(-t * 110f) * 0.6f + n * Mathf.Exp(-t * 55f) * 0.5f + Mathf.Sin(2f * Mathf.PI * 68f * t) * Mathf.Exp(-t * 12f) * 0.8f);
            thunkClip = Synth("Dart", 0.2f, (t, n) => Mathf.Sin(2f * Mathf.PI * (380f - 220f * t) * t) * Mathf.Exp(-t * 32f) * 0.8f + n * Mathf.Exp(-t * 220f) * 0.3f);
            tinkClip = Synth("Tooth", 0.32f, (t, n) => Mathf.Sin(2f * Mathf.PI * 2600f * t) * Mathf.Exp(-t * 28f) * 0.35f + Mathf.Sin(2f * Mathf.PI * 3900f * t) * Mathf.Exp(-t * 45f) * 0.2f);
            whooshClip = Synth("Whoosh", 0.26f, (t, n) => { low += (n - low) * 0.06f; return low * Mathf.Sin(Mathf.PI * t / 0.26f) * 2.4f; });
            clickClip = Synth("Click", 0.09f, (t, n) => Mathf.Sin(2f * Mathf.PI * (900f - 3000f * t) * t) * Mathf.Exp(-t * 50f) * 0.4f);
            popClip = Synth("Pop", 0.15f, (t, n) => Mathf.Sin(2f * Mathf.PI * (520f - 900f * t) * t) * Mathf.Exp(-t * 35f) * 0.7f);
            bellClip = Synth("Bell", 1.4f, (t, n) => (Mathf.Sin(2f * Mathf.PI * 880f * t) + Mathf.Sin(2f * Mathf.PI * 1318f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 2210f * t) * 0.3f) * Mathf.Exp(-t * 3f) * 0.35f);
            chargeClip = Synth("Charge", 1.8f, (t, n) => (Mathf.Sin(2f * Mathf.PI * (180f * t + 260f * t * t)) * 0.5f + Mathf.Sin(2f * Mathf.PI * (360f * t + 520f * t * t)) * 0.25f) * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 18f * t)) * Mathf.Min(1f, t * 3f) * 0.6f);
            unlockClip = Synth("Unlock", 0.9f, (t, n) => Mathf.Sin(2f * Mathf.PI * (t < 0.15f ? 660f : t < 0.3f ? 880f : 1320f) * t) * Mathf.Exp(-t * 3f) * 0.5f);
            buzzClip = Synth("Fly buzz", 1f, (t, n) => Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 210f * t)) * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 33f * t)) * 0.3f + n * 0.05f);
            buzz.clip = buzzClip;
        }

        private void LoadVoices()
        {
            foreach (var clip in Resources.LoadAll<AudioClip>("Voices"))
            {
                var key = clip.name.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
                if (!voices.TryGetValue(key, out var list)) voices[key] = list = new List<AudioClip>();
                list.Add(clip);
            }
        }

        private AudioClip Synth(string name, float seconds, Func<float, float, float> wave)
        {
            const int rate = 24000;
            var samples = new float[Mathf.CeilToInt(seconds * rate)];
            for (var index = 0; index < samples.Length; index++)
            {
                var t = (float)index / rate;
                var noise = (float)(random.NextDouble() * 2.0 - 1.0);
                samples[index] = Mathf.Clamp(wave(t, noise), -1f, 1f) * Mathf.Min(1f, t * 2000f) * Mathf.Min(1f, (seconds - t) * 200f);
            }
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            clips.Add(clip);
            return clip;
        }

        private void Play(AudioClip clip, float volume, float pitch)
        {
            if (!SoundOn || clip == null) return;
            sound.pitch = pitch;
            sound.PlayOneShot(clip, volume);
        }

        // ---------- GUI ----------

        private void OnGUI()
        {
            var previous = GUI.matrix;
            if (PhoneFrame) DrawPresentation();
            var game = Matrix4x4.TRS(new Vector3(screenGui.x, screenGui.y, 0f), Quaternion.identity, Vector3.one * GuiScale);
            GUI.matrix = game;
            GUI.BeginClip(new Rect(0f, 0f, DesignWidth, 540f));
            var width = DesignWidth;
            if (flash > 0f) ui.Fill(new Rect(0f, 0f, width, 540f), new Color(1f, 0.3f, 0.45f, flash * 0.35f));
            if (emoTipUI.IsLoading)
            {
                emoTipUI.DrawLoadingScreen(width, 540f, clock);
            }
            else
            {
                switch (Stage)
                {
                    case Stage.Home: DrawHome(width); break;
                    case Stage.Before: DrawCheckIn(width, false); break;
                    case Stage.Customize: DrawCustomize(width); break;
                    case Stage.Play: DrawPlay(width, game); break;
                    case Stage.After: DrawCheckIn(width, true); break;
                    case Stage.Result: DrawResult(width); break;
                }
            }
            if (settingsOpen) DrawSettings(width);
            GUI.EndClip();
            GUI.matrix = previous;
            if (PhoneFrame) DrawPhone();
        }

        private void DrawPresentation()
        {
            var full = new Rect(0f, 0f, Screen.width, Screen.height);
            var gradient = ui.Gradient(new Color(0.36f, 0.05f, 0.08f), new Color(0.08f, 0.01f, 0.03f));
            var bezel = screenGui.height * 0.06f;
            var phone = new Rect(screenGui.x - bezel, screenGui.y - bezel, screenGui.width + bezel * 2f, screenGui.height + bezel * 2f);
            var strips = new[]
            {
                new Rect(0f, 0f, full.width, screenGui.y),
                new Rect(0f, screenGui.yMax, full.width, full.height - screenGui.yMax),
                new Rect(0f, screenGui.y, screenGui.x, screenGui.height),
                new Rect(screenGui.xMax, screenGui.y, full.width - screenGui.xMax, screenGui.height)
            };
            foreach (var strip in strips)
            {
                if (strip.width <= 0f || strip.height <= 0f) continue;
                GUI.DrawTextureWithTexCoords(strip, gradient, new Rect(0f, 1f - strip.yMax / full.height, 1f, strip.height / full.height));
                GUI.BeginGroup(strip);
                var glow = ui.Tex("bgglow", () => GlowTexture());
                ui.Image(new Rect(full.width * 0.05f - strip.x, full.height * 0.05f - strip.y, full.height * 0.7f, full.height * 0.7f), glow, new Color(UiKit.Magenta.r, UiKit.Magenta.g, UiKit.Magenta.b, 0.35f));
                ui.Image(new Rect(full.width * 0.65f - strip.x, full.height * 0.35f - strip.y, full.height * 0.8f, full.height * 0.8f), glow, new Color(UiKit.Purple.r, UiKit.Purple.g, UiKit.Purple.b, 0.35f));
                GUI.EndGroup();
            }
            var unit = screenGui.height / 540f;
            ui.Panel(new Rect(phone.x + phone.width * 0.62f, phone.y - 5f * unit, phone.width * 0.09f, 12f * unit), new Color(0.2f, 0.2f, 0.24f), 4);
            ui.Panel(new Rect(phone.x + phone.width * 0.74f, phone.y - 5f * unit, phone.width * 0.05f, 12f * unit), new Color(0.2f, 0.2f, 0.24f), 4);
            ui.Panel(new Rect(phone.x + 8f * unit, phone.y + 14f * unit, phone.width - 16f * unit, phone.height), new Color(0f, 0f, 0f, 0.4f), 40);
            var caption = Mathf.Max(14, Mathf.RoundToInt(unit * 22f));
            ui.Text(new Rect(0f, phone.yMax + 10f * unit, full.width, 40f * unit), "EMORELIEF AI  -  PLAY. RELEASE. UNDERSTAND YOUR EMOTIONS.", caption, UiKit.White, TextAnchor.MiddleCenter, 0f);
            ui.Text(new Rect(0f, phone.yMax + 44f * unit, full.width, 30f * unit), "F2  phone frame     F11  fullscreen     Esc  pause", Mathf.RoundToInt(caption * 0.7f), UiKit.Pink, TextAnchor.MiddleCenter, 0f, null, true);
        }

        private static Texture2D FadeTexture()
        {
            var texture = new Texture2D(128, 1, TextureFormat.RGBA32, false);
            for (var x = 0; x < 128; x++) texture.SetPixel(x, 0, new Color(1f, 1f, 1f, 1f - Mathf.SmoothStep(0.55f, 1f, x / 127f)));
            texture.Apply();
            return texture;
        }

        private static Texture2D GlowTexture()
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

        private void DrawPhone()
        {
            var bezel = Mathf.Round(screenGui.height * 0.06f);
            var phone = new Rect(screenGui.x - bezel, screenGui.y - bezel, screenGui.width + bezel * 2f, screenGui.height + bezel * 2f);
            var outer = Mathf.RoundToInt(bezel * 2.3f);
            ui.DrawFrame(phone, outer, Mathf.RoundToInt(bezel * 1.1f), Mathf.RoundToInt(bezel));
            var hole = bezel * 0.36f;
            ui.Image(new Rect(screenGui.x + bezel * 0.55f, screenGui.center.y - hole * 0.5f, hole, hole), ui.Icon("dot"), new Color(0.02f, 0.02f, 0.03f));
        }

        // ---- shared widgets ----

        private void TopBar(string title, string step, Action back)
        {
            ui.Fill(new Rect(0f, 0f, DesignWidth, 84f), new Color(0f, 0f, 0f, 0.25f));
            if (back != null && ui.Round(new Rect(20f, 14f, 58f, 58f), ui.Icon("back"), UiKit.Purple)) back();
            ui.Text(new Rect(back != null ? 94f : 24f, 10f, 700f, 64f), title, 40, UiKit.White, TextAnchor.MiddleLeft, 3.5f, UiKit.Ink, false, false, new Color(1f, 0.92f, 0.6f));
            if (string.IsNullOrEmpty(step)) return;
            var area = new Rect(DesignWidth - 170f, 24f, 146f, 40f);
            ui.Panel(area, UiKit.Magenta, 20, UiKit.Ink, 3f);
            ui.Text(area, step, 22, UiKit.White, TextAnchor.MiddleCenter, 2f);
        }

        private bool Toggle(Rect area, string label, string icon, bool value)
        {
            ui.Panel(area, new Color(1f, 1f, 1f, 0.07f), 18);
            ui.Image(new Rect(area.x + 14f, area.y + (area.height - 34f) * 0.5f, 34f, 34f), ui.Icon(icon), UiKit.White);
            ui.Text(new Rect(area.x + 60f, area.y, area.width - 160f, area.height), label, 23, UiKit.White, TextAnchor.MiddleLeft, 0f);
            var track = new Rect(area.xMax - 96f, area.y + (area.height - 38f) * 0.5f, 80f, 38f);
            ui.Panel(track, value ? UiKit.Green : UiKit.Grey, 19, UiKit.Ink, 2f);
            ui.Panel(new Rect(value ? track.xMax - 35f : track.x + 3f, track.y + 3f, 32f, 32f), UiKit.White, 16);
            ui.Text(new Rect(track.x + (value ? 8f : 36f), track.y, 40f, track.height), value ? "ON" : "OFF", 15, UiKit.White, TextAnchor.MiddleCenter, 0f);
            return GUI.Button(area, GUIContent.none, GUIStyle.none) && Clicked();
        }

        private bool Clicked()
        {
            Play(clickClip, 0.6f, 1f);
            return true;
        }

        private void DrawSettings(float width)
        {
            ui.Fill(new Rect(0f, 0f, width, 540f), new Color(0f, 0f, 0f, 0.55f));
            var panel = new Rect(width * 0.5f - 260f, 50f, 520f, 450f);
            ui.Glass(panel);
            ui.Text(new Rect(panel.x, panel.y + 10f, panel.width, 56f), "SETTINGS", 40, UiKit.White, TextAnchor.MiddleCenter, 3f, null, false, false, UiKit.Yellow);
            if (ui.Round(new Rect(panel.xMax - 62f, panel.y + 12f, 48f, 48f), ui.Icon("close"), UiKit.Red)) settingsOpen = false;
            var y = panel.y + 78f;
            if (Toggle(new Rect(panel.x + 24f, y, panel.width - 48f, 62f), "Sound effects", "sound", SoundOn)) SetSound(!SoundOn);
            y += 72f;
            if (Toggle(new Rect(panel.x + 24f, y, panel.width - 48f, 62f), "Opponent voice", "voice", VoiceOn)) SetVoice(!VoiceOn);
            y += 72f;
            if (Toggle(new Rect(panel.x + 24f, y, panel.width - 48f, 62f), "Cartoon blood", "blood", BloodEnabled)) SetBlood(!BloodEnabled);
            y += 72f;
            if (Toggle(new Rect(panel.x + 24f, y, panel.width - 48f, 62f), "Screen shake", "shake", ShakeOn)) { ShakeOn = !ShakeOn; SaveSettings(); }
            y += 72f;
            if (Toggle(new Rect(panel.x + 24f, y, panel.width - 48f, 62f), "Phone frame (demo)", "phone", PhoneFrame)) SetPhoneFrame(!PhoneFrame);
        }

        // ---- screens ----

        private void DrawHome(float width)
        {
            ui.Image(new Rect(0f, 0f, width * 0.66f, 540f), ui.Tex("homefade", () => FadeTexture()), new Color(0.3f, 0.01f, 0.05f, 0.93f));
            var center = width * 0.29f;
            var flicker = 0.4f + 0.12f * Mathf.Sin(clock * 9f) + 0.08f * Mathf.Sin(clock * 23f);
            ui.Image(new Rect(center - 330f, -20f, 660f, 380f), ui.Tex("bgglow", () => GlowTexture()), new Color(1f, 0.25f, 0.05f, flicker));
            ui.Image(new Rect(center - 200f, 60f, 400f, 240f), ui.Tex("bgglow", () => GlowTexture()), new Color(1f, 0.75f, 0.1f, flicker * 0.6f));
            ui.Badge(new Rect(center - 52f, 34f, 104f, 104f), ui.Icon("robot"), UiKit.Magenta);
            Logo(new Rect(center - 300f, 128f, 600f, 120f));
            var tagline = new Rect(center - 230f, 248f, 460f, 40f);
            ui.Panel(tagline, new Color(0f, 0f, 0f, 0.5f), 20, UiKit.Orange, 2f);
            ui.Text(tagline, "SMASH THE STRESS  -  FEEL THE RELIEF", 20, UiKit.Yellow, TextAnchor.MiddleCenter, 0f);
            if (ui.Button(new Rect(center - 160f, 300f, 320f, 76f), "PLAY", UiKit.Red, 52, ui.Icon("punch"))) GoTo(Stage.Before);
            emoTipUI.DrawSetupBar(new Rect(center - 290f, 388f, 580f, 46f));
            var steps = new[] { "CHECK IN", "BUILD", "PLAY", "CHECK IN", "TIPS" };
            for (var index = 0; index < steps.Length; index++)
            {
                var chip = new Rect(center - 290f + index * 118f, 444f, 110f, 30f);
                ui.Panel(chip, new Color(1f, 1f, 1f, 0.1f), 15);
                ui.Text(chip, (index + 1) + "  " + steps[index], 15, UiKit.White, TextAnchor.MiddleCenter, 0f);
            }
            ui.Text(new Rect(center - 300f, 480f, 600f, 44f), "Rage it out on a fictional cartoon opponent - then see how you really feel.", 14, new Color(1f, 0.85f, 0.75f), TextAnchor.MiddleCenter, 0f, null, true, true);
            if (ui.Round(new Rect(width - 84f, 16f, 64f, 64f), ui.Icon("gear"), UiKit.Purple)) settingsOpen = true;
            if (Application.platform != RuntimePlatform.WindowsEditor && ui.Round(new Rect(width - 160f, 16f, 64f, 64f), ui.Icon("power"), UiKit.Red)) Application.Quit();
            ui.Text(new Rect(width - 220f, 500f, 200f, 30f), "v1.0", 16, new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleRight, 0f);
        }

        private void Logo(Rect area)
        {
            ui.Text(new Rect(area.x, area.y + 8f, area.width, area.height), "EMORELIEF", 104, new Color(0.35f, 0.02f, 0.04f), TextAnchor.MiddleCenter, 7f, new Color(0.15f, 0f, 0.02f));
            ui.Text(area, "EMORELIEF", 104, UiKit.Orange, TextAnchor.MiddleCenter, 6f, new Color(0.25f, 0f, 0.03f), false, false, UiKit.Yellow);
            var badge = new Rect(area.center.x + 236f, area.y - 26f, 86f, 52f);
            ui.Panel(badge, UiKit.Red, 18, UiKit.Ink, 4f);
            ui.Text(badge, "AI", 40, UiKit.White, TextAnchor.MiddleCenter, 2.5f, UiKit.Ink);
        }

        private void DrawCheckIn(float width, bool after)
        {
            TopBar(after ? "HOW DO YOU FEEL NOW?" : "HOW ARE YOU FEELING?", after ? "STEP 4/4" : "STEP 1/4", () => GoTo(after ? Stage.Play : Stage.Home));
            var panel = new Rect(24f, 98f, Mathf.Min(width * 0.6f, 680f), 340f);
            ui.Glass(panel);
            var x = panel.x + 24f;
            var inner = panel.width - 48f;
            var y = panel.y + 16f;
            if (!after)
            {
                ui.Text(new Rect(x, y, inner, 30f), "PICK YOUR MOOD", 22, UiKit.Pink, TextAnchor.MiddleLeft, 0f);
                y += 36f;
                var chipWidth = (inner - 20f) / 3f;
                for (var index = 0; index < MoodNames.Length; index++)
                {
                    var chip = new Rect(x + index % 3 * (chipWidth + 10f), y + index / 3 * 56f, chipWidth, 48f);
                    if (ui.Chip(chip, MoodNames[index].ToUpperInvariant(), Mood == index, MoodColors[index], 20, ui.Icon("mood" + index))) Mood = index;
                }
                y += 122f;
            }
            else
            {
                var card = new Rect(x, y, inner, 70f);
                ui.Panel(card, new Color(1f, 1f, 1f, 0.08f), 18);
                if (Mood >= 0) ui.Image(new Rect(card.x + 14f, card.y + 11f, 48f, 48f), ui.Icon("mood" + Mood), MoodColors[Mood]);
                ui.Text(new Rect(card.x + 76f, card.y, inner - 90f, 70f), "BEFORE PLAYING:  " + Before + "/10  " + Band(Before).ToUpperInvariant() + (Mood >= 0 ? "   -   " + MoodNames[Mood].ToUpperInvariant() : ""), 22, UiKit.White, TextAnchor.MiddleLeft, 0f);
                y += 100f;
                ui.Text(new Rect(x, y - 20f, inner, 50f), "Rate how angry you feel right now, after playing.", 18, UiKit.Pink, TextAnchor.MiddleLeft, 0f, null, true);
                y += 58f;
            }
            var value = after ? After : Before;
            ui.Text(new Rect(x, y, 200f, 30f), "ANGER LEVEL", 22, UiKit.Pink, TextAnchor.MiddleLeft, 0f);
            ui.Text(new Rect(x + inner - 300f, y - 6f, 300f, 40f), value + "/10  " + Band(value).ToUpperInvariant(), 32, BandColor(value), TextAnchor.MiddleRight, 2.5f, UiKit.Ink);
            y += 40f;
            var segment = inner / 11f;
            for (var number = 0; number <= 10; number++)
            {
                var area = new Rect(x + number * segment + 2f, y, segment - 4f, 50f);
                var on = number <= value;
                ui.Panel(area, on ? BandColor(number) : new Color(1f, 1f, 1f, 0.1f), 12, number == value ? UiKit.White : (Color?)null, number == value ? 3f : 0f);
                ui.Text(area, number.ToString(), 22, on ? UiKit.White : new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleCenter, on ? 2f : 0f);
                if (GUI.Button(area, GUIContent.none, GUIStyle.none)) { value = number; Clicked(); }
            }
            ui.Text(new Rect(x, y + 54f, 200f, 22f), "0  CALM", 15, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleLeft, 0f);
            ui.Text(new Rect(x + inner - 200f, y + 54f, 200f, 22f), "FURIOUS  10", 15, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleRight, 0f);
            if (after) After = value;
            else Before = value;
            ui.Text(new Rect(24f, 448f, panel.width, 30f), "Your own rating - kept separate from the gameplay score.", 15, new Color(1f, 0.85f, 0.95f), TextAnchor.MiddleLeft, 0f, null, true);
            var ready = after || Mood >= 0;
            if (!ready) ui.Text(new Rect(width - 330f, 412f, 306f, 30f), "Pick a mood first", 18, UiKit.Yellow, TextAnchor.MiddleCenter, 2f);
            if (ui.Button(new Rect(width - 330f, 446f, 306f, 76f), after ? "RESULTS" : "NEXT", UiKit.Orange, 38, ui.Icon("next"), ready))
            {
                if (!after) GoTo(Stage.Customize);
                else if (emoTipUI.CurrentResponse == null) RequestEmoTips();
                else GoTo(Stage.Result);
            }
        }

        private int OptionCount(int index)
        {
            switch (index)
            {
                case 0: return FaceStyle.SexNames.Length;
                case 1: return FaceStyle.ShapeNames.Length;
                case 2: return FaceStyle.SkinNames.Length;
                case 3: return FaceStyle.EyeNames.Length;
                case 4: return FaceStyle.NoseNames.Length;
                case 5: return FaceStyle.MouthNames.Length;
                case 6: return FaceStyle.HairNames.Length;
                case 7: return FaceStyle.EarNames.Length;
                case 8: return Backdrops.Names.Length;
                case 10: return FaceStyle.CharacterNames.Length;
                default: return 2;
            }
        }

        private string OptionName(int index, int value)
        {
            switch (index)
            {
                case 0: return FaceStyle.SexNames[value];
                case 1: return FaceStyle.ShapeNames[value];
                case 2: return FaceStyle.SkinNames[value];
                case 3: return FaceStyle.EyeNames[value];
                case 4: return FaceStyle.NoseNames[value];
                case 5: return FaceStyle.MouthNames[value];
                case 6: return FaceStyle.HairNames[value];
                case 7: return FaceStyle.EarNames[value];
                case 8: return Backdrops.Names[value];
                case 10: return FaceStyle.CharacterNames[value];
                default: return value == 0 ? "Cartoon blood" : "No blood";
            }
        }

        private int OptionValue(int index)
        {
            var style = Style;
            switch (index)
            {
                case 0: return style.Sex;
                case 1: return style.Shape;
                case 2: return style.Skin;
                case 3: return style.Eyes;
                case 4: return style.Nose;
                case 5: return style.Mouth;
                case 6: return style.Hair;
                case 7: return style.Ears;
                case 8: return Scene;
                case 10: return CharacterIndex;
                default: return BloodEnabled ? 0 : 1;
            }
        }

        private void SetOption(int index, int value)
        {
            if (index == 9) { SetBlood(value == 0); return; }
            if (index == 10) { ChooseCharacter(value); return; }
            var style = Style;
            var scene = Scene;
            switch (index)
            {
                case 0: style.Sex = value; style.Hair = value == 1 ? 3 : 0; style.Mouth = value == 1 ? 3 : 0; break;
                case 1: style.Shape = value; break;
                case 2: style.Skin = value; break;
                case 3: style.Eyes = value; break;
                case 4: style.Nose = value; break;
                case 5: style.Mouth = value; break;
                case 6: style.Hair = value; break;
                case 7: style.Ears = value; break;
                case 8: scene = value; break;
            }
            ApplyStyle(style, scene);
        }

        private void DrawCustomize(float width)
        {
            TopBar("BUILD YOUR OPPONENT", "STEP 2/4", () => GoTo(Stage.Before));
            for (var index = 0; index < Categories.Length; index++)
                if (ui.Chip(new Rect(18f, 92f + index * 39f, 170f, 35f), Categories[index], category == index, index == 0 ? UiKit.Red : UiKit.Magenta, 18)) category = index;
            var real = category == 0 ? 10 : category - 1;
            var count = OptionCount(real);
            var value = OptionValue(real);
            var focus = width * 0.47f;
            if (ui.Round(new Rect(focus - 250f, 230f, 76f, 76f), ui.Icon("back"), UiKit.Orange)) SetOption(real, (value + count - 1) % count);
            if (ui.Round(new Rect(focus + 174f, 230f, 76f, 76f), ui.Icon("next"), UiKit.Orange)) SetOption(real, (value + 1) % count);
            var plate = new Rect(focus - 170f, 448f, 340f, 66f);
            ui.Panel(plate, new Color(0f, 0f, 0f, 0.5f), 22, UiKit.Yellow, 3f);
            ui.Text(new Rect(plate.x, plate.y - 22f, plate.width, 20f), "TAP NAME TO RENAME", 14, UiKit.White, TextAnchor.MiddleCenter, 2f);
            if (nameStyle == null) nameStyle = new GUIStyle { font = ui.Display, fontSize = 30, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
            nameStyle.normal.textColor = nameStyle.focused.textColor = nameStyle.hover.textColor = UiKit.Yellow;
            var typed = GUI.TextField(new Rect(plate.x + 12f, plate.y + 2f, plate.width - 24f, 34f), OpponentName, 16, nameStyle);
            if (typed != OpponentName) OpponentName = typed.ToUpperInvariant();
            ui.Text(new Rect(plate.x, plate.y + 34f, plate.width, 28f), Categories[category] + ":  " + OptionName(real, OptionValue(real)).ToUpperInvariant(), 18, UiKit.White, TextAnchor.MiddleCenter, 0f);

            var panel = new Rect(width - 344f, 94f, 324f, 336f);
            ui.Glass(panel);
            ui.Text(new Rect(panel.x, panel.y + 6f, panel.width, 36f), Categories[category], 26, UiKit.Yellow, TextAnchor.MiddleCenter, 2f);
            for (var option = 0; option < count; option++)
            {
                var card = real == 10 ? new Rect(panel.x + 14f, panel.y + 48f + option * 47f, 296f, 42f) : new Rect(panel.x + 14f + option % 2 * 152f, panel.y + 48f + option / 2 * 58f, 144f, 50f);
                var selected = option == OptionValue(real);
                if (real == 2)
                {
                    ui.Panel(card, selected ? UiKit.Yellow : new Color(1f, 1f, 1f, 0.15f), 14);
                    ui.Panel(new Rect(card.x + 4f, card.y + 4f, card.width - 8f, card.height - 8f), FaceStyle.SkinColors[option], 11);
                    ui.Text(card, FaceStyle.SkinNames[option].ToUpperInvariant(), 17, UiKit.White, TextAnchor.MiddleCenter, 2f);
                    if (GUI.Button(card, GUIContent.none, GUIStyle.none) && Clicked()) SetOption(real, option);
                }
                else if (ui.Chip(card, OptionName(real, option).ToUpperInvariant(), selected, real == 10 ? UiKit.Red : UiKit.Purple, real == 10 ? 20 : 17, real == 10 ? ui.Icon(FaceStyle.Character(option).Sex == 0 ? "mood0" : "mood1") : null)) SetOption(real, option);
            }
            if (ui.Round(new Rect(width - 344f, 444f, 76f, 76f), ui.Icon("dice"), UiKit.Purple))
            {
                ApplyStyle(FaceStyle.Random(random), random.Next(Backdrops.Names.Length));
                RollName();
            }
            if (ui.Button(new Rect(width - 256f, 444f, 236f, 80f), "FIGHT!", UiKit.Orange, 42, ui.Icon("punch")))
            {
                if (string.IsNullOrWhiteSpace(OpponentName)) RollName();
                GoTo(Stage.Play);
            }
        }

        private GUIStyle nameStyle;

        private void DrawPlay(float width, Matrix4x4 game)
        {
            if (Event.current.type == EventType.Layout) hud.Clear();
            foreach (var popup in popups)
            {
                var screen = view.WorldToScreenPoint(popup.World);
                if (screen.z <= 0f) continue;
                var design = ToDesign(screen);
                var age = popup.Age;
                var size = (age < 0.08f ? Mathf.Lerp(0.4f, 1.3f, age / 0.08f) : Mathf.Lerp(1.3f, 1f, Mathf.Clamp01((age - 0.08f) / 0.12f))) * popup.Scale;
                var alpha = 1f - Mathf.Clamp01((age - popup.Life + 0.35f) / 0.35f);
                var y = design.y - age * (popup.Scale < 1f ? 25f : 60f);
                ui.Text(new Rect(design.x - 300f, y - 60f, 600f, 120f), popup.Text, Mathf.RoundToInt(58f * size), new Color(popup.Color.r, popup.Color.g, popup.Color.b, alpha), TextAnchor.MiddleCenter, 4.5f * size, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, alpha));
            }

            if (pulling)
            {
                var aim = ToDesign(AimScreen());
                var power = PullPower();
                var color = Color.Lerp(UiKit.White, UiKit.Red, power);
                ui.Image(new Rect(aim.x - 22f, aim.y - 22f, 44f, 44f), ui.Icon("close"), new Color(0f, 0f, 0f, 0.5f));
                ui.Image(new Rect(aim.x - 20f, aim.y - 20f, 40f, 40f), ui.Icon("close"), color);
                ui.Text(new Rect(width * 0.5f - 160f, 410f, 320f, 24f), "POWER", 18, UiKit.White, TextAnchor.MiddleCenter, 2f);
                ui.Bar(new Rect(width * 0.5f - 160f, 436f, 320f, 24f), power, color, new Color(0f, 0f, 0f, 0.55f));
            }

            // rage meter card
            var card = new Rect(16f, 12f, 420f, 92f);
            Hud(card);
            ui.Panel(card, new Color(0f, 0f, 0f, 0.5f), 24, Rage >= 70f ? UiKit.Red : (Color?)null, Rage >= 70f ? 2f + Mathf.Sin(clock * 12f) : 0f);
            ui.Badge(new Rect(card.x + 8f, card.y + 8f, 76f, 76f), ui.Icon(Character.Crying ? "mood3" : "mood0"), Style.Sex == 1 ? UiKit.Magenta : UiKit.Cyan);
            ui.Text(new Rect(card.x + 96f, card.y + 4f, 310f, 34f), OpponentName, 26, UiKit.White, TextAnchor.MiddleLeft, 2f);
            string zone;
            Color zoneColor;
            if (PoopReady) { zone = "MAX!  POOP SLAP READY"; zoneColor = UiKit.Yellow; }
            else if (Rage >= 70f) { zone = "RAGE!!"; zoneColor = UiKit.Red; }
            else if (Rage >= 30f) { zone = "HEATED"; zoneColor = UiKit.Orange; }
            else { zone = "CALM"; zoneColor = UiKit.Cyan; }
            var meter = Rage / 100f;
            var fill = PoopReady ? Color.Lerp(UiKit.Yellow, UiKit.White, Mathf.PingPong(clock * 3f, 1f)) : Color.Lerp(UiKit.Cyan, Color.Lerp(UiKit.Orange, UiKit.Red, Mathf.InverseLerp(0.5f, 1f, meter)), Mathf.Clamp01(meter * 2f));
            ui.Bar(new Rect(card.x + 96f, card.y + 42f, 310f, 26f), meter, fill, new Color(0f, 0f, 0f, 0.6f), true);
            ui.Image(new Rect(card.x + 82f, card.y + 36f, 38f, 38f), ui.Icon("star"), zoneColor);
            ui.Text(new Rect(card.x + 96f, card.y + 68f, 310f, 22f), "RAGE METER  -  " + zone, 15, zoneColor, TextAnchor.MiddleLeft, 1.5f);

            // pause
            var pause = new Rect(width * 0.5f - 32f, 14f, 64f, 64f);
            Hud(pause);
            if (ui.Round(pause, ui.Icon("pause"), UiKit.Purple)) Paused = true;

            // score and audio toggles
            var scoreArea = new Rect(width - 362f, 16f, 210f, 56f);
            Hud(scoreArea);
            ui.Panel(scoreArea, new Color(0f, 0f, 0f, 0.45f), 28, UiKit.Yellow, 2f);
            ui.Image(new Rect(scoreArea.x + 8f, scoreArea.y + 6f, 44f, 44f), ui.Icon("star"), UiKit.Yellow);
            ui.Text(new Rect(scoreArea.x + 56f, scoreArea.y, 146f, 56f), Score.ToString("N0"), 34, UiKit.Yellow, TextAnchor.MiddleRight, 2.5f);
            ui.Text(new Rect(width - 362f, 74f, 210f, 22f), "HITS " + Hits + "   TEETH " + Character.TeethLost, 16, UiKit.White, TextAnchor.MiddleRight, 2f);
            var soundButton = new Rect(width - 140f, 16f, 56f, 56f);
            var voiceButton = new Rect(width - 76f, 16f, 56f, 56f);
            Hud(soundButton);
            Hud(voiceButton);
            if (ui.Round(soundButton, ui.Icon("sound"), SoundOn ? UiKit.Purple : UiKit.Grey, false, null, true, !SoundOn)) SetSound(!SoundOn);
            if (ui.Round(voiceButton, ui.Icon("voice"), VoiceOn ? UiKit.Magenta : UiKit.Grey, false, null, true, !VoiceOn)) SetVoice(!VoiceOn);
            ui.Text(new Rect(width - 160f, 74f, 150f, 20f), VoiceOn ? "VOICE ON" : "VOICE OFF", 14, UiKit.White, TextAnchor.MiddleRight, 2f);

            if (Chain >= 3)
            {
                ui.Text(new Rect(width - 380f, 100f, 360f, 44f), "x" + Chain + " COMBO" + (Chain >= 5 ? "  2X" : ""), 32, UiKit.Yellow, TextAnchor.MiddleRight, 3f);
            }
            if (Rage >= 70f && !UltimateRunning)
            {
                var pulse = 0.25f + 0.15f * Mathf.Sin(clock * 10f);
                ui.Image(new Rect(-200f, -120f, 400f, 780f), ui.Tex("bgglow", () => GlowTexture()), new Color(1f, 0.1f, 0.05f, pulse));
                ui.Image(new Rect(width - 200f, -120f, 400f, 780f), ui.Tex("bgglow", () => GlowTexture()), new Color(1f, 0.1f, 0.05f, pulse));
            }

            // ultimates
            var feetArea = new Rect(26f, 150f, 92f, 92f);
            var poopArea = new Rect(20f, 268f, 104f, 104f);
            Hud(feetArea);
            Hud(poopArea);
            var feetReady = FeetCooldown <= 0f && FeetActive <= 0f && !UltimateRunning;
            if (ui.Round(feetArea, ui.Icon("foot"), FeetActive > 0f ? UiKit.Green : feetReady ? ToolColors[6] : UiKit.Grey, FeetActive > 0f || feetReady, null, true, false)) UseFeet();
            var feetLabel = FeetActive > 0f ? FeetActive.ToString("0.0") + "s" : FeetCooldown > 0f ? Mathf.CeilToInt(FeetCooldown) + "s" : "READY";
            if (FeetCooldown > 0f) ui.Image(new Rect(feetArea.x + 26f, feetArea.y + 20f, 40f, 40f), ui.Icon("lock"), new Color(1f, 1f, 1f, 0.85f));
            ui.Text(new Rect(feetArea.x - 20f, feetArea.yMax - 2f, 132f, 24f), "ROTTEN FEET  " + feetLabel, 15, FeetActive > 0f ? UiKit.Green : feetReady ? UiKit.Yellow : UiKit.White, TextAnchor.MiddleCenter, 2f);
            if (FeetCooldown > 0f) ui.Bar(new Rect(feetArea.x, feetArea.y - 12f, feetArea.width, 8f), 1f - FeetCooldown / 20f, UiKit.Green, new Color(0f, 0f, 0f, 0.6f));
            if (PoopReady && !UltimateRunning)
                ui.Image(new Rect(poopArea.x - 30f, poopArea.y - 30f, poopArea.width + 60f, poopArea.height + 60f), ui.Tex("bgglow", () => GlowTexture()), new Color(1f, 0.85f, 0.2f, 0.5f + 0.4f * Mathf.Sin(clock * 8f)));
            if (ui.Round(poopArea, ui.Icon("poop"), PoopReady ? ToolColors[7] : UiKit.Grey, PoopReady, null, true, false)) UsePoopSlap();
            if (!PoopReady) ui.Image(new Rect(poopArea.x + 30f, poopArea.y + 26f, 44f, 44f), ui.Icon("lock"), new Color(1f, 1f, 1f, 0.85f));
            ui.Text(new Rect(poopArea.x - 20f, poopArea.yMax - 2f, 144f, 24f), PoopReady ? "POOP SLAP!" : "POOP SLAP " + Mathf.FloorToInt(Rage) + "%", 15, PoopReady ? UiKit.Yellow : UiKit.White, TextAnchor.MiddleCenter, 2f);
            if (UltimateRunning) DrawUltimate(width);

            // tools
            var anchor = new Vector2(width - 78f, 452f);
            for (var index = 0; index < 6; index++)
            {
                Vector2 center;
                if (index == 0) center = anchor;
                else if (index == 5) center = anchor + Polar(160f, 280f);
                else center = anchor + Polar(new[] { 0f, 178f, 150f, 121f, 92f }[index], 168f);
                var size = index == 0 ? 104f : 80f;
                if (index == (int)Rig.Tool) size += 8f;
                var area = new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size);
                Hud(area);
                if (ui.Round(area, ui.Icon(ToolIcons[index]), ToolColors[index], (int)Rig.Tool == index)) SelectTool((ToolKind)index);
                ui.Text(new Rect(area.xMax - 26f, area.y - 4f, 26f, 26f), (index + 1).ToString(), 16, UiKit.White, TextAnchor.MiddleCenter, 2f);
            }
            var finish = new Rect(18f, 456f, 200f, 70f);
            Hud(finish);
            if (ui.Button(finish, "FINISH", UiKit.Orange, 32, ui.Icon("check"))) GoTo(Stage.After);
            var hint = new Rect(232f, 476f, Mathf.Max(220f, width - 680f), 40f);
            ui.Panel(hint, new Color(0f, 0f, 0f, 0.5f), 20);
            ui.Text(hint, FaceStyle.ToolNames[(int)Rig.Tool].ToUpperInvariant() + "  -  " + ToolHints[(int)Rig.Tool], 18, UiKit.White, TextAnchor.MiddleCenter, 0f);

            if (!Paused) return;
            ui.Fill(new Rect(0f, 0f, width, 540f), new Color(0f, 0f, 0f, 0.6f));
            if (settingsOpen) return;
            var panel = new Rect(width * 0.5f - 220f, 70f, 440f, 410f);
            ui.Glass(panel);
            ui.Text(new Rect(panel.x, panel.y + 12f, panel.width, 60f), "PAUSED", 50, UiKit.White, TextAnchor.MiddleCenter, 3.5f, null, false, false, UiKit.Yellow);
            if (ui.Button(new Rect(panel.x + 50f, panel.y + 92f, 340f, 66f), "RESUME", UiKit.Green, 32, ui.Icon("next"))) Paused = false;
            if (ui.Button(new Rect(panel.x + 50f, panel.y + 170f, 340f, 62f), "RESET FACE", UiKit.Purple, 28, ui.Icon("mood3"))) ResetFace();
            if (ui.Button(new Rect(panel.x + 50f, panel.y + 244f, 340f, 62f), "SETTINGS", UiKit.Purple, 28, ui.Icon("gear"))) settingsOpen = true;
            if (ui.Button(new Rect(panel.x + 50f, panel.y + 318f, 340f, 62f), "QUIT TO HOME", UiKit.Red, 28, ui.Icon("home"))) GoTo(Stage.Home);
        }

        private void DrawUltimate(float width)
        {
            var t = ultiTime;
            var intro = t < UltiStart;
            var outro = ultiSuccess;
            ui.Fill(new Rect(0f, 0f, width, 540f), new Color(0.05f, 0f, 0.12f, intro ? 0.3f : outro ? 0.35f : 0.1f));
            var ring = ui.Tex("ultiglow", () => GlowTexture());
            for (var wave = 0; wave < 3; wave++)
            {
                var phase = (t * 0.9f + wave / 3f) % 1f;
                var size = Mathf.Lerp(40f, 1300f, phase);
                var tint = wave % 2 == 0 ? UiKit.Cyan : UiKit.Magenta;
                ui.Image(new Rect(width * 0.5f - size * 0.5f, 250f - size * 0.5f, size, size), ui.Icon("ring"), new Color(tint.r, tint.g, tint.b, (1f - phase) * 0.8f));
            }
            for (var line = 0; line < 7; line++)
            {
                var y = (t * 420f + line * 97f) % 560f - 10f;
                ui.Fill(new Rect(0f, y, width, 3f), new Color(0.3f, 1f, 1f, 0.35f));
            }
            if (!intro && !outro)
            {
                var punch = 1f + Mathf.Clamp01(1f - (t - SlapTime(Mathf.Max(0, ultiSlaps - 1))) / 0.12f) * 0.35f;
                ui.Text(new Rect(width * 0.5f - 300f, 20f, 600f, 90f), "SLAP x" + ultiSlaps + "!", Mathf.RoundToInt(64f * punch), ultiSlaps >= 10 ? UiKit.Red : UiKit.Yellow, TextAnchor.MiddleCenter, 5f, UiKit.Ink);
                if (ultiSlaps >= 10) ui.Text(new Rect(width * 0.5f - 300f, 92f, 600f, 34f), "MAX SPEED!!", 28, UiKit.Cyan, TextAnchor.MiddleCenter, 3f);
                return;
            }
            var age = intro ? t : t - (SlapTime(UltiSlapCount - 1) + 0.5f);
            var pop = age < 0.18f ? Mathf.Lerp(2.6f, 1f, age / 0.18f) : 1f + Mathf.Sin(age * 14f) * 0.03f;
            var text = intro ? "ULTI POOP SLAP!!" : "POOP SLAP SUCCESS!!";
            var size2 = Mathf.RoundToInt(84f * pop);
            var area = new Rect(width * 0.5f - 600f, intro ? 30f : 170f, 1200f, 150f);
            ui.Image(new Rect(width * 0.5f - 420f, area.y - 50f, 840f, 250f), ring, new Color(1f, 0.8f, 0.2f, 0.55f));
            ui.Text(new Rect(area.x - 6f, area.y, area.width, area.height), text, size2, new Color(0.2f, 1f, 1f, 0.8f), TextAnchor.MiddleCenter);
            ui.Text(new Rect(area.x + 6f, area.y, area.width, area.height), text, size2, new Color(1f, 0.2f, 0.8f, 0.8f), TextAnchor.MiddleCenter);
            ui.Text(area, text, size2, UiKit.Yellow, TextAnchor.MiddleCenter, 7f * pop, UiKit.Ink, false, false, UiKit.White);
            ui.Text(new Rect(area.x, area.yMax - 10f, area.width, 40f), intro ? "ULTIMATE ATTACK - LOADING THE POOP..." : "x" + UltiSlapCount + " COMBO  -  FLIES INCOMING", 26, UiKit.Cyan, TextAnchor.MiddleCenter, 2.5f);
        }

        private void Hud(Rect area)
        {
            if (Event.current.type == EventType.Layout) hud.Add(area);
        }

        private static Vector2 Polar(float degrees, float radius) =>
            new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), -Mathf.Sin(degrees * Mathf.Deg2Rad)) * radius;

        // Everything on the results screen starts right of this line so the opponent stays visible.
        private const float ResultPanelLeft = 356f;

        private void DrawResult(float width)
        {
            TopBar("SESSION RESULTS", Ultimates > 0 ? "ULTI x" + Ultimates : "COMPLETE", null);
            var left = ResultPanelLeft;
            var right = width - 16f;
            emoTipUI.DrawTipsPanel(new Rect(left, 92f, right - left, 376f));

            // Bottom bar: stats chips (as many as fit) + actions.
            const float barY = 476f;
            const float barHeight = 52f;
            var again = new Rect(right - 200f, barY, 200f, barHeight);
            var home = new Rect(again.x - 60f, barY, barHeight, barHeight);
            if (ui.Button(again, "PLAY AGAIN", UiKit.Orange, 26, ui.Icon("punch"))) GoTo(Stage.Before);
            if (ui.Round(home, ui.Icon("home"), UiKit.Purple)) GoTo(Stage.Home);

            var change = Before - After;
            var time = Mathf.FloorToInt(PlayTime);
            var chips = new[]
            {
                ("mood0", Before + " TO " + After, change > 0 ? "CALMER" : change < 0 ? "ANGRIER" : "NO CHANGE", change > 0 ? UiKit.Green : change < 0 ? UiKit.Red : UiKit.Yellow),
                ("star", Score.ToString("N0"), "SCORE", UiKit.Yellow),
                ("punch", Hits.ToString(), "HITS", UiKit.Orange),
                ("shake", combatTracker.MaxAttackSpeed.ToString("0.0") + "/s", "PEAK SPEED", UiKit.Cyan),
                ("timer", time / 60 + ":" + (time % 60).ToString("00"), "FIGHT TIME", UiKit.Pink)
            };
            var available = home.x - 10f - left;
            var count = Mathf.Clamp(Mathf.FloorToInt((available + 8f) / 112f), 1, chips.Length);
            var chipWidth = (available - (count - 1) * 8f) / count;
            for (var index = 0; index < count; index++)
            {
                var chip = new Rect(left + index * (chipWidth + 8f), barY, chipWidth, barHeight);
                ui.Panel(chip, new Color(UiKit.Night.r, UiKit.Night.g, UiKit.Night.b, 0.9f), 16, new Color(1f, 0.55f, 0.25f, 0.5f), 1.5f);
                ui.Image(new Rect(chip.x + 8f, chip.y + 13f, 26f, 26f), ui.Icon(chips[index].Item1), chips[index].Item4);
                ui.FitText(new Rect(chip.x + 40f, chip.y + 5f, chip.width - 46f, 24f), chips[index].Item2, 20, 12, UiKit.White, TextAnchor.MiddleLeft, false, 1);
                ui.FitText(new Rect(chip.x + 40f, chip.y + 29f, chip.width - 46f, 17f), chips[index].Item3, 12, 9, chips[index].Item4, TextAnchor.MiddleLeft, false, 1);
            }
        }

        private void OnDestroy()
        {
            PythonProcessManager.KillProcess();
            ClearProjectiles();
            Rig?.Dispose();
            Character?.Dispose();
            backdrops.Clear();
            props?.Dispose();
            ui?.Dispose();
            kit.Dispose();
            foreach (var clip in clips) Destroy(clip);
        }
    }
}

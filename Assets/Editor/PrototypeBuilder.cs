using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EmoRelief.Editor
{
    public static class PrototypeBuilder
    {
        public const string ScenePath = "Assets/Scenes/FaceLab.unity";

        [MenuItem("EmoRelief/Set Up Prototype")]
        public static void SetUp()
        {
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory("Assets/Settings");
            Directory.CreateDirectory("Assets/Resources/Fonts");
            AssetDatabase.Refresh();

            const string pipelinePath = "Assets/Settings/PortraitPipeline.asset";
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (pipeline == null)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, "Assets/Settings/PortraitRenderer.asset");
                pipeline = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
                var serialized = new SerializedObject(pipeline);
                var renderers = serialized.FindProperty("m_RendererDataList");
                renderers.arraySize = 1;
                renderers.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                pipeline.msaaSampleCount = 4;
                pipeline.supportsHDR = false;
                pipeline.shadowDistance = 15f;
                AssetDatabase.CreateAsset(pipeline, pipelinePath);
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            ConfigurePlayer();
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            settings.FindProperty("activeInputHandler").intValue = 1;
            settings.ApplyModifiedPropertiesWithoutUndo();
            CreateMaterial("StageLit", "Universal Render Pipeline/Lit");
            CreateMaterial("StageUnlit", "Universal Render Pipeline/Unlit");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Portrait Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.075f, 0.07f);
            camera.transform.position = new Vector3(0f, 0f, -7f);
            camera.fieldOfView = 42f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 50f;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.65f, 0.7f, 0.68f);
            var lightObject = new GameObject("Key Light", typeof(Light));
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            light.transform.rotation = Quaternion.Euler(35f, -35f, 0f);
            new GameObject("Face Lab", typeof(FaceLab));

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            if (GraphicsSettings.defaultRenderPipeline == null || PlayerSettings.defaultScreenWidth != 1600)
                throw new InvalidOperationException("Project setup did not persist.");
            Debug.Log("EMORELIEF_SETUP_OK");
        }

        [MenuItem("EmoRelief/Configure Landscape Player")]
        public static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "EmoRelief";
            PlayerSettings.productName = "EmoRelief AI";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            AssetDatabase.SaveAssets();
            Debug.Log("EMORELIEF_PLAYER_OK: landscape 1600x900");
        }

        private static void CreateMaterial(string name, string shaderName)
        {
            var path = "Assets/Resources/" + name + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Missing shader: " + shaderName);
            AssetDatabase.CreateAsset(new Material(shader), path);
        }

        [MenuItem("EmoRelief/Validate Prototype")]
        public static void Validate()
        {
            if (!File.Exists(ScenePath) || Resources.Load<Material>("StageLit") == null)
                throw new InvalidOperationException("Run EmoRelief > Set Up Prototype first.");
            var parent = new GameObject("Validation character");
            // Keep clear of the live game character when validating during Play Mode.
            parent.transform.position = new Vector3(0f, -500f, 0f);
            var character = new FaceCharacter();
            try
            {
                character.Build(parent.transform, FaceStyle.Default);
                if (character.FaceMesh.blendShapeCount != 8) throw new InvalidOperationException("Missing face shapes.");
                for (var region = 0; region < 3; region++)
                {
                    character.Reset();
                    character.React(region);
                    if (character.Face.GetBlendShapeWeight(region) < 99f) throw new InvalidOperationException("Region did not activate.");
                    for (var step = 0; step < 120; step++) character.Tick(1f / 60f, step / 60f);
                    if (character.Contact[region] > 0.001f) throw new InvalidOperationException("Reaction did not recover.");
                }
                for (var repeat = 0; repeat < 100; repeat++) character.React(repeat % 3);
                foreach (var wear in character.Wear)
                    if (wear > FaceCharacter.WearCap) throw new InvalidOperationException("Persistent deformation exceeded its cap.");
                character.Reset();
                for (var shape = 0; shape < 8; shape++)
                    if (character.Face.GetBlendShapeWeight(shape) != 0f) throw new InvalidOperationException("Reset failed.");
                var left = FaceCharacter.Deformation(new Vector3(-0.6f, 0f, -0.75f), 0, false);
                var right = FaceCharacter.Deformation(new Vector3(-0.6f, 0f, -0.75f), 2, false);
                if (left.magnitude <= right.magnitude * 2f) throw new InvalidOperationException("Deformation is not localized.");

                var hit = character.ProbeLocal(0f, -0.6f);
                if (!hit.Valid || FaceCharacter.ZoneOf(hit.Local) != FaceCharacter.ZoneMouth) throw new InvalidOperationException("Face probe failed.");
                // Clean faces already carry painted blush and stubble.
                var baseline = character.Skin.CountMarked();
                foreach (ToolKind tool in Enum.GetValues(typeof(ToolKind)))
                    if (character.ApplyHit(tool, hit, 1f, Vector3.forward).Score <= 0) throw new InvalidOperationException("No score for " + tool);
                if (character.Skin.CountMarked() <= baseline + 500) throw new InvalidOperationException("Damage was not painted.");
                character.Reset();
                if (character.Skin.CountMarked() > baseline * 1.3f + 100 || character.TotalDamage != 0f) throw new InvalidOperationException("Damage reset failed.");
                for (var index = 0; index < 40; index++)
                {
                    var style = FaceStyle.Random(new System.Random(index));
                    character.Build(parent.transform, style);
                }
                if (FaceLab.Tips(8, 4).Count != 3 || FaceLab.Band(8) != "High" || FaceLab.Band(2) != "Very low")
                    throw new InvalidOperationException("Result text rules failed.");
                if (Resources.LoadAll<AudioClip>("Voices").Length < 40) throw new InvalidOperationException("Opponent voice clips missing.");
                if (Resources.Load<Font>("Fonts/LilitaOne-Regular") == null) throw new InvalidOperationException("Display font missing.");
                Debug.Log("EMORELIEF_VALIDATION_OK: eight shapes, regions, recovery, caps, reset, localization, six tools, painting, styles, tips, voices, font.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
                character.Dispose();
            }
        }

        [MenuItem("EmoRelief/Play Prototype")]
        public static void PlayPreview()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
            var gameViewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            var gameView = EditorWindow.GetWindow(gameViewType);
            gameView.Show();
            gameView.Focus();
            EditorApplication.EnterPlaymode();
        }

        // Graphical-editor build entry point for command-line use; batch-mode builds hang in this Unity install.
        public static void BuildWindowsAndExit()
        {
            EditorApplication.delayCall += () =>
            {
                var code = 0;
                try { BuildWindows(); }
                catch (Exception error) { Debug.LogError(error); code = 1; }
                EditorApplication.Exit(code);
            };
        }

        [MenuItem("EmoRelief/Build Windows Demo")]
        public static void BuildWindows()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before building the Windows demo.");
            ConfigurePlayer();
            Validate();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Windows/EmoRelief.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Windows build failed: " + report.summary.result);
            Debug.Log("EMORELIEF_BUILD_OK: " + report.summary.totalSize + " bytes.");
        }
    }
}
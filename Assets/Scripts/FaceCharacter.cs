using System;
using System.Collections.Generic;
using UnityEngine;

namespace EmoRelief
{
    public struct HitPoint
    {
        public bool Valid;
        public Vector3 Local;
        public Vector3 Normal;
        public Vector2 Uv;
    }

    public struct HitResult
    {
        public int Score;
        public string Word;
        public bool Heavy;
    }

    public sealed class FaceCharacter
    {
        public const int FaceLayer = 8;
        public const float WearCap = 0.7f;
        public const int ZoneForehead = 0, ZoneEyeLeft = 1, ZoneEyeRight = 2, ZoneNose = 3, ZoneCheekLeft = 4, ZoneCheekRight = 5, ZoneMouth = 6, ZoneChin = 7;
        private const float HeadHeight = 0.48f;

        public FaceStyle Style { get; private set; }
        public Transform Container { get; private set; }
        public Transform Root { get; private set; }
        public SkinnedMeshRenderer Face { get; private set; }
        public Mesh FaceMesh { get; private set; }
        public SkinPainter Skin { get; } = new SkinPainter();
        public PropKit Props;
        public bool BloodEnabled = true;
        public Action<Vector3, Vector3, DebrisKind, Color> Debris;

        public readonly float[] Contact = new float[3];
        public readonly float[] Wear = new float[3];
        public readonly float[] ZoneDamage = new float[8];
        public readonly float[] EyeRed = new float[2];
        public readonly float[] Droop = new float[2];
        public readonly float[] BlackEye = new float[2];
        public float JawShift { get; private set; }
        public float NoseCrook { get; private set; }
        public float LipSwell { get; private set; }
        public float Dizzy { get; private set; }
        public int TeethLost { get; private set; }
        public int TeethChipped { get; private set; }
        public int StuckDarts => darts.Count;
        public int Lumps => lumps.Count;
        public float TotalDamage
        {
            get
            {
                var total = 0f;
                foreach (var value in ZoneDamage) total += value;
                return total;
            }
        }

        private sealed class Lump
        {
            public Transform Body;
            public Vector3 Local;
            public Vector3 Normal;
            public float Size;
            public float Target;
        }

        private readonly List<Transform> features = new List<Transform>();
        private readonly List<Vector3> origins = new List<Vector3>();
        private readonly List<Lump> lumps = new List<Lump>();

        private sealed class EggGoo
        {
            public Transform Body;
            public float X, Y, Age, Drip;
        }

        private readonly List<EggGoo> eggs = new List<EggGoo>();
        private Material eggWhiteMaterial, yolkMaterial, shineMaterial;
        public int EggsOnFace => eggs.Count;
        private readonly List<Transform> darts = new List<Transform>();
        private readonly Transform[] eyes = new Transform[2];
        private readonly Transform[] gaze = new Transform[2];
        private readonly Transform[] lids = new Transform[2];
        private readonly Transform[] brows = new Transform[2];
        private readonly Transform[] stars = new Transform[5];
        private readonly Material[] eyeWhites = new Material[2];
        private readonly Material[] lidMaterials = new Material[2];
        private readonly float[] goo = new float[2];
        private readonly System.Random random = new System.Random(7);
        private static readonly Color GumColor = new Color(0.86f, 0.38f, 0.45f);
        private Transform[] teeth = new Transform[0];
        private Vector3[] toothScale = new Vector3[0];
        private Vector3[] toothPosition = new Vector3[0];
        private int[] toothState = new int[0];
        private ArtKit kit;
        private MeshCollider faceCollider;
        private Transform nose;
        private Transform mouth;
        private Transform tongueOut;
        private Transform lowerLip;
        private Transform starRing;
        private Vector3 lowerLipScale;
        private Vector3 impulse;
        private Vector4 shape;
        private Color tone;
        private Material noseMaterial;
        private Material lumpMaterial;
        private Material gumMaterial;
        private float eyeRadiusY;
        private Vector3 eyeSize;
        private float baseDroop;
        private float smirk;
        private float hitAge = 10f;
        private float nosebleed;
        private float nosebleedClock;
        private int nostril;
        private float tearClock;
        private Transform body;
        private readonly Vector3[] shoulders = new Vector3[2];
        private readonly Hose[] arms = new Hose[2];
        private readonly List<Transform> flies = new List<Transform>();
        private Material flyMaterial;
        private Material wingMaterial;
        private float wail;
        private float wailClock;
        private float stinkClock;
        public bool Stinky { get; private set; }
        public bool Wailing => wail > 0f;
        public int Flies => flies.Count;
        private Vector4 sculpt;
        private float squash;
        private float squashVelocity;
        public float Angry;
        private readonly Transform[] gloves = new Transform[2];
        private const float UpperArm = 1.2f;
        private const float ForeArm = 1.1f;
        public float Talk;
        public bool Crying => TotalDamage > 16f || Dizzy > 1.3f;

        public void Build(Transform parent, FaceStyle style)
        {
            if (Container != null) Clear();
            Style = style;
            kit = new ArtKit();
            shape = style.ShapeParameters();
            sculpt = style.SculptParameters();
            tone = FaceStyle.SkinColors[Mathf.Clamp(style.Skin, 0, FaceStyle.SkinColors.Length - 1)];
            var female = style.Sex == 1;
            var skin = kit.Lit("Skin", tone, 0.33f);
            var faceMaterial = kit.Lit("Face skin", Color.white, 0.33f);
            faceMaterial.SetTexture("_BaseMap", Skin.Texture);
            noseMaterial = kit.Lit("Nose skin", tone, 0.33f);
            lumpMaterial = kit.Lit("Swelling", Color.Lerp(tone, new Color(0.6f, 0.2f, 0.4f), 0.6f), 0.45f);
            var dark = kit.Lit("Ink", new Color(0.065f, 0.035f, 0.065f), 0.25f);
            var hair = kit.Lit("Hair", female ? new Color(0.2f, 0.09f, 0.06f) : new Color(0.07f, 0.04f, 0.05f), 0.3f);
            var blush = kit.Lit("Cheek", Color.Lerp(tone, new Color(0.95f, 0.3f, 0.35f), 0.45f), 0.3f);
            var shirt = kit.Lit("Tank top", female ? new Color(0.1f, 0.62f, 0.6f) : new Color(0.16f, 0.3f, 0.78f), 0.3f);
            var trunks = kit.Lit("Trunks", female ? new Color(0.45f, 0.18f, 0.62f) : new Color(0.75f, 0.1f, 0.16f), 0.55f);
            var trim = kit.Lit("Trim", new Color(1f, 0.97f, 0.92f), 0.4f);
            var lips = kit.Lit("Lips", female ? new Color(0.78f, 0.2f, 0.36f) : Color.Lerp(tone, new Color(0.62f, 0.22f, 0.24f), 0.45f), 0.55f);
            var mouthInside = kit.Lit("Mouth inside", new Color(0.22f, 0.04f, 0.07f), 0.3f);
            var tooth = kit.Lit("Tooth", new Color(1f, 0.98f, 0.9f), 0.7f);
            gumMaterial = kit.Lit("Gums", GumColor, 0.5f);
            Color irisColor;
            switch (style.Eyes)
            {
                case 1: irisColor = new Color(0.2f, 0.45f, 0.85f); break;
                case 2: irisColor = new Color(0.33f, 0.2f, 0.1f); break;
                case 3: irisColor = new Color(0.5f, 0.42f, 0.18f); break;
                default: irisColor = new Color(0.27f, 0.46f, 0.29f); break;
            }
            var iris = kit.Lit("Iris", irisColor, 0.6f);
            var gloss = kit.Unlit("Catchlight", Color.white);
            flyMaterial = kit.Lit("Fly", new Color(0.08f, 0.08f, 0.1f), 0.9f);
            wingMaterial = kit.Unlit("Fly wing", new Color(0.85f, 0.92f, 1f));
            eggWhiteMaterial = kit.Lit("Raw egg white", new Color(1f, 0.98f, 0.88f), 0.95f);
            yolkMaterial = kit.Lit("Raw yolk", new Color(1f, 0.74f, 0.04f), 0.95f);
            shineMaterial = kit.Unlit("Egg shine", Color.white);

            Container = ArtKit.Group("Character", parent);
            BuildBody(female, skin, shirt, trunks, trim);

            Root = ArtKit.Group("Expressive Head", Container, new Vector3(0f, HeadHeight, 0f));
            FaceMesh = kit.Own(CreateFaceMesh(shape, sculpt));
            var surface = new GameObject("Face surface", typeof(SkinnedMeshRenderer), typeof(MeshCollider));
            surface.layer = FaceLayer;
            surface.transform.SetParent(Root, false);
            Face = surface.GetComponent<SkinnedMeshRenderer>();
            Face.sharedMesh = FaceMesh;
            Face.sharedMaterial = faceMaterial;
            Face.updateWhenOffscreen = true;
            Face.localBounds = new Bounds(Vector3.zero, new Vector3(3.3f, 3.6f, 2.8f));
            faceCollider = surface.GetComponent<MeshCollider>();
            faceCollider.sharedMesh = FaceMesh;

            BuildEars(skin, blush);
            BuildEyes(female, dark, iris, gloss);
            BuildNose(blush);
            BuildMouth(female, lips, mouthInside, tooth);
            BuildHair(hair);

            starRing = ArtKit.Group("Dizzy stars", Container, new Vector3(0f, HeadHeight + shape.z + 0.3f, -0.1f));
            var starMaterial = kit.Unlit("Dizzy star", new Color(1f, 0.88f, 0.2f));
            for (var index = 0; index < stars.Length; index++)
            {
                stars[index] = ArtKit.Box("Star", starRing, Vector3.zero, new Vector3(0.13f, 0.13f, 0.03f), starMaterial, new Vector3(0f, 0f, 45f));
                ArtKit.Box("Star point", stars[index], Vector3.zero, Vector3.one, starMaterial, new Vector3(0f, 0f, 45f));
            }
            Reset();
        }

        private void BuildBody(bool female, Material skin, Material shirt, Material trunks, Material trim)
        {
            body = ArtKit.Group("Body", Container);
            var width = female ? new[] { 0.86f, 0.66f, 0.72f, 0.84f, 0.88f, 0.9f, 0.8f, 0.5f, 0.27f } : new[] { 0.82f, 0.76f, 0.84f, 0.97f, 1.05f, 1.1f, 0.98f, 0.6f, 0.33f };
            var depth = female ? new[] { 0.46f, 0.4f, 0.43f, 0.46f, 0.45f, 0.4f, 0.34f, 0.29f, 0.25f } : new[] { 0.46f, 0.43f, 0.47f, 0.5f, 0.5f, 0.45f, 0.37f, 0.31f, 0.28f };
            var front = female ? new[] { 0f, 0f, 0.02f, 0.11f, 0.09f, 0.02f, 0f, 0f, 0f } : new[] { 0f, 0f, 0.03f, 0.07f, 0.08f, 0.03f, 0f, 0f, 0f };
            var heights = new[] { -3.5f, -3.05f, -2.55f, -2.1f, -1.72f, -1.42f, -1.2f, -1.05f, -0.95f };
            var bulk = 1f + Style.Bulk * 0.3f;
            for (var index = 0; index < width.Length; index++)
            {
                width[index] *= index >= 3 && index <= 6 ? bulk * (1f + Style.Bulk * 0.12f) : bulk;
                depth[index] *= bulk;
            }
            var rings = new List<MeshKit.Ring>();
            var top = new List<MeshKit.Ring>();
            for (var index = 0; index < heights.Length; index++)
            {
                rings.Add(new MeshKit.Ring(heights[index], width[index], depth[index], 0.25f, front[index], index < 6 ? 3f : 2.4f));
                if (index < 5) top.Add(new MeshKit.Ring(heights[index], width[index] * 1.03f, depth[index] * 1.05f, 0.25f, front[index] * 1.05f, 3f));
            }
            top.Add(new MeshKit.Ring(-1.58f, Mathf.Lerp(width[4], width[5], 0.5f) * 1.03f, Mathf.Lerp(depth[4], depth[5], 0.5f) * 1.05f, 0.25f, front[4], 3f));
            MeshKit.Part("Torso", body, kit.Own(MeshKit.Loft("Torso", rings)), skin);
            if (female) MeshKit.Part("Sports top", body, kit.Own(MeshKit.Loft("Sports top", top)), shirt);
            var strap = kit.Own(MeshKit.Capsule("Strap", 0.055f, 0.055f, 1f));
            foreach (var side in female ? new[] { -1f, 1f } : new float[0])
            {
                var x = side * (female ? 0.36f : 0.42f);
                var points = new[] { new Vector3(x, -1.58f, -0.2f), new Vector3(x * 1.05f, -1.08f, 0.2f), new Vector3(x, -1.58f, 0.68f) };
                for (var piece = 0; piece < 2; piece++)
                {
                    var segment = MeshKit.Part("Strap", body, strap, shirt);
                    MeshKit.Aim(segment, points[piece], points[piece + 1], Vector3.up);
                    segment.localScale = new Vector3(1f, 1f, (points[piece + 1] - points[piece]).magnitude);
                }
            }
            var hips = new List<MeshKit.Ring> { new MeshKit.Ring(-4.1f, width[0] * 1.06f, depth[0] * 1.1f, 0.25f, 0f, 3f), new MeshKit.Ring(-3.36f, width[0] * 1.04f, depth[0] * 1.08f, 0.25f, 0f, 3f) };
            MeshKit.Part("Trunks", body, kit.Own(MeshKit.Loft("Trunks", hips)), trunks);
            var band = new List<MeshKit.Ring> { new MeshKit.Ring(-3.42f, width[0] * 1.07f, depth[0] * 1.11f, 0.25f, 0f, 3f), new MeshKit.Ring(-3.22f, width[1] * 1.08f, depth[1] * 1.12f, 0.25f, 0f, 3f) };
            MeshKit.Part("Waistband", body, kit.Own(MeshKit.Loft("Waistband", band)), trim);
            var neck = (female ? 0.24f : 0.32f) * (1f + Style.Bulk * 0.35f);
            var neckRings = new List<MeshKit.Ring> { new MeshKit.Ring(-1.1f, neck * 1.15f, neck, 0.2f, 0f, 2f), new MeshKit.Ring(-0.6f, neck, neck * 0.92f, 0.16f, 0f, 2f), new MeshKit.Ring(0f, neck * 0.95f, neck * 0.9f, 0.14f, 0f, 2f) };
            MeshKit.Part("Neck", body, kit.Own(MeshKit.Loft("Neck", neckRings)), skin);

            var thickness = (female ? 0.78f : 1f) * (1f + Style.Bulk * 0.4f);
            var deltoid = kit.Own(MeshKit.Rounded("Deltoid", new Vector3(0.3f, 0.3f, 0.3f) * thickness, 2f));
            var leather = kit.Lit("Opponent glove", female ? new Color(0.92f, 0.2f, 0.6f) : new Color(0.12f, 0.35f, 0.9f), 0.78f);
            for (var index = 0; index < 2; index++)
            {
                var side = index == 0 ? -1f : 1f;
                var shoulder = new Vector3(side * width[5] * 0.9f, -1.36f, 0.25f);
                shoulders[index] = shoulder;
                MeshKit.Part("Deltoid", body, deltoid, skin, shoulder + new Vector3(side * 0.02f, 0.02f, 0f));
                arms[index] = new Hose("Rubber arm", body, skin, t => (0.27f - 0.1f * t + 0.05f * Mathf.Sin(t * Mathf.PI * 2.2f)) * thickness);
                gloves[index] = Props != null ? Props.Glove(body, -side, leather, trim) : ArtKit.Group("Glove", body);
                gloves[index].localScale = Vector3.one * (female ? 0.95f : 1.15f) * (1f + Style.Bulk * 0.15f);
            }
        }

        private void PoseArms(float clock, float pain, float daze)
        {
            var bounce = Mathf.Sin(clock * 4.2f);
            for (var index = 0; index < 2; index++)
            {
                var side = index == 0 ? -1f : 1f;
                var flinch = Contact[index == 0 ? 0 : 2] + Contact[1] * 0.6f;
                var wrist = new Vector3(side * 0.82f, -2.0f + bounce * 0.05f, -1.05f);
                wrist += new Vector3(side * 0.35f, 0.45f, 0.35f) * flinch;
                wrist += new Vector3(side * 0.25f, -0.9f, 0.6f) * daze;
                wrist += new Vector3(0f, Mathf.Sin(clock * 9f + index) * 0.08f, 0f) * (Crying ? 1f : 0f);
                var shoulder = shoulders[index];
                var elbow = TwoBone(shoulder, wrist, UpperArm, ForeArm, new Vector3(side * 0.8f, -1f, 0.4f));
                var worldShoulder = body.TransformPoint(shoulder);
                var worldElbow = body.TransformPoint(elbow);
                var worldWrist = body.TransformPoint(wrist);
                var whip = Mathf.Sin(clock * 7f + index * 2f) * 0.1f + flinch * 0.35f;
                arms[index].Set(worldShoulder, worldElbow * 2f - (worldShoulder + worldWrist) * 0.5f + body.right * side * whip, worldWrist, body.forward);
                gloves[index].position = worldWrist;
                gloves[index].rotation = Quaternion.LookRotation(worldWrist - worldElbow, body.up) * Quaternion.Euler(-25f, 0f, side * 10f);
            }
            body.localPosition = new Vector3(0f, Mathf.Abs(bounce) * 0.03f, 0f);
            body.localRotation = Quaternion.Euler(-pain * 4f, 0f, Mathf.Sin(clock * 1.3f) * 1.5f + Mathf.Sin(clock * 2.6f) * daze * 3f);
        }

        private static Vector3 TwoBone(Vector3 shoulder, Vector3 wrist, float upper, float lower, Vector3 pole)
        {
            var offset = wrist - shoulder;
            var length = Mathf.Max(offset.magnitude, 0.001f);
            var direction = offset / length;
            if (length >= (upper + lower) * 0.999f) return shoulder + direction * upper;
            var along = (upper * upper - lower * lower + length * length) / (2f * length);
            var height = Mathf.Sqrt(Mathf.Max(0f, upper * upper - along * along));
            return shoulder + direction * along + Vector3.ProjectOnPlane(pole, direction).normalized * height;
        }

        private void BuildEars(Material skin, Material blush)
        {
            Vector3 size;
            switch (Style.Ears)
            {
                case 1: size = new Vector3(0.36f, 0.55f, 0.24f); break;
                case 2: size = new Vector3(0.19f, 0.29f, 0.18f); break;
                default: size = new Vector3(0.27f, 0.41f, 0.22f); break;
            }
            var edge = FaceStyle.HalfWidth(shape, sculpt, 0.07f);
            foreach (var side in new[] { -1f, 1f })
            {
                var ear = ArtKit.Group("Ear", Root, new Vector3(side * (edge - 0.02f + size.x * 0.25f), 0.07f, 0.04f), new Vector3(0f, side * -12f, 0f));
                ArtKit.Ball("Outer ear", ear, Vector3.zero, size, skin);
                ArtKit.Ball("Inner ear", ear, new Vector3(side * size.x * 0.45f, -0.01f, -0.12f), new Vector3(size.x * 0.38f, size.y * 0.56f, 0.08f), blush);
                if (Style.Ears == 3)
                    ArtKit.Ball("Ear point", ear, new Vector3(side * 0.12f, 0.42f, 0.02f), new Vector3(0.1f, 0.3f, 0.12f), skin, new Vector3(0f, 0f, side * -32f));
            }
        }

        private void BuildEyes(bool female, Material dark, Material iris, Material gloss)
        {
            Vector3 white;
            float irisSize;
            switch (Style.Eyes)
            {
                case 1: white = new Vector3(0.37f, 0.41f, 0.25f); irisSize = 1.25f; break;
                case 2: white = new Vector3(0.33f, 0.2f, 0.2f); irisSize = 0.85f; break;
                default: white = new Vector3(0.32f, 0.34f, 0.23f); irisSize = 1f; break;
            }
            eyeRadiusY = white.y;
            eyeSize = white;
            baseDroop = Style.Eyes == 3 ? 0.38f : 0f;
            for (var index = 0; index < 2; index++)
            {
                var side = index == 0 ? -1f : 1f;
                var eye = Feature(index == 0 ? "Left eye" : "Right eye", new Vector3(side * 0.43f, 0.33f, -0.65f));
                eyes[index] = eye;
                eyeWhites[index] = kit.Lit("Eye white", Color.white, 0.6f);
                ArtKit.Ball("Eye white", eye, Vector3.zero, white, eyeWhites[index]);
                var look = ArtKit.Group("Gaze", eye, new Vector3(-side * 0.035f, -0.015f, 0f));
                gaze[index] = look;
                ArtKit.Ball("Iris", look, new Vector3(0f, 0f, -white.z + 0.017f), new Vector3(0.135f, 0.16f, 0.055f) * irisSize, iris);
                ArtKit.Ball("Pupil", look, new Vector3(0f, 0f, -white.z - 0.026f), new Vector3(0.065f, 0.095f, 0.025f) * irisSize, dark);
                ArtKit.Ball("Catchlight", look, new Vector3(-0.025f, 0.035f, -white.z - 0.05f), new Vector3(0.026f, 0.033f, 0.015f) * irisSize, gloss);
                lidMaterials[index] = kit.Lit("Eyelid", tone, 0.33f);
                lids[index] = ArtKit.Ball("Eyelid", eye, new Vector3(0f, white.y, -0.02f), new Vector3(white.x * 1.08f, white.y * 1.05f, white.z * 1.1f), lidMaterials[index]);
                var brow = ArtKit.Group("Brow pivot", eye, new Vector3(0f, white.y + 0.04f, -0.025f));
                brows[index] = brow;
                ArtKit.Ball("Brow", brow, Vector3.zero, new Vector3(0.36f, female ? 0.055f : 0.09f, 0.12f), dark);
                if (female)
                    for (var lash = 0; lash < 3; lash++)
                        ArtKit.Ball("Lash", eye, new Vector3(side * (white.x * 0.55f + lash * 0.07f), white.y * 0.82f - lash * 0.05f, -0.1f), new Vector3(0.07f, 0.022f, 0.03f), dark, new Vector3(0f, 0f, side * (25f + lash * 18f)));
            }
        }

        private void BuildNose(Material blush)
        {
            nose = Feature("Nose", new Vector3(0f, -0.1f, -0.89f));
            Vector3 bridge, tip, tipPosition;
            switch (Style.Nose)
            {
                case 0: bridge = new Vector3(0.15f, 0.3f, 0.2f); tip = new Vector3(0.24f, 0.19f, 0.28f); tipPosition = new Vector3(0f, -0.02f, 0.03f); break;
                case 1: bridge = new Vector3(0.15f, 0.4f, 0.24f); tip = new Vector3(0.2f, 0.18f, 0.42f); tipPosition = new Vector3(0f, 0f, -0.08f); break;
                case 2: bridge = new Vector3(0.22f, 0.34f, 0.24f); tip = new Vector3(0.4f, 0.24f, 0.34f); tipPosition = Vector3.zero; break;
                default: bridge = new Vector3(0.2f, 0.38f, 0.24f); tip = new Vector3(0.33f, 0.23f, 0.37f); tipPosition = Vector3.zero; break;
            }
            ArtKit.Ball("Nose bridge", nose, new Vector3(0f, 0.13f, 0.03f), bridge, noseMaterial);
            ArtKit.Ball("Nose tip", nose, tipPosition, tip, noseMaterial);
            foreach (var side in new[] { -1f, 1f })
                ArtKit.Ball("Nostril", nose, tipPosition + new Vector3(side * tip.x * 0.55f, -tip.y * 0.56f, -tip.z * 0.5f), new Vector3(0.06f, 0.04f, 0.035f), blush);
        }

        private void BuildMouth(bool female, Material lips, Material inside, Material tooth)
        {
            mouth = Feature("Mouth", new Vector3(0f, -0.65f, -0.64f));
            Vector3 opening;
            int count;
            switch (Style.Mouth)
            {
                case 1: opening = new Vector3(0.6f, 0.24f, 0.2f); count = 8; smirk = 0f; break;
                case 2: opening = new Vector3(0.3f, 0.16f, 0.2f); count = 4; smirk = 0f; break;
                case 3: opening = new Vector3(0.45f, 0.18f, 0.2f); count = 6; smirk = 0f; break;
                case 4: opening = new Vector3(0.5f, 0.13f, 0.2f); count = 6; smirk = 0f; break;
                case 5: opening = new Vector3(0.44f, 0.18f, 0.2f); count = 2; smirk = 0f; break;
                case 6: opening = new Vector3(0.5f, 0.24f, 0.2f); count = 6; smirk = 4f; break;
                default: opening = new Vector3(0.47f, 0.2f, 0.2f); count = 6; smirk = -6f; break;
            }
            ArtKit.Ball("Mouth opening", mouth, Vector3.zero, opening, inside);
            ArtKit.Ball("Gums", mouth, new Vector3(0f, opening.y * 0.62f, -0.1f), new Vector3(opening.x * 0.72f, 0.045f, 0.09f), gumMaterial);
            ArtKit.Ball("Tongue", mouth, new Vector3(0.05f, -opening.y * 0.5f, -0.12f), new Vector3(opening.x * 0.45f, 0.055f, 0.075f), gumMaterial);
            teeth = new Transform[count];
            toothScale = new Vector3[count];
            toothPosition = new Vector3[count];
            toothState = new int[count];
            var span = opening.x * 1.3f;
            var width = span / count;
            for (var index = 0; index < count; index++)
            {
                var x = -span * 0.5f + width * (index + 0.5f);
                var curve = x * x * 0.4f;
                toothPosition[index] = new Vector3(x, opening.y * 0.32f + curve, -0.15f - (0.05f - curve));
                toothScale[index] = new Vector3(width * 0.86f, 0.1f, 0.05f);
                if (Style.Mouth == 5)
                {
                    toothPosition[index] = new Vector3((index - 0.5f) * 0.15f, -opening.y * 0.55f, -0.25f);
                    toothScale[index] = new Vector3(0.14f, 0.3f, 0.07f);
                }
                teeth[index] = ArtKit.Box("Tooth", mouth, toothPosition[index], toothScale[index], tooth);
            }
            tongueOut = null;
            if (Style.Mouth == 6)
            {
                tongueOut = ArtKit.Group("Long tongue", mouth, new Vector3(0.04f, -opening.y * 0.55f, -0.2f));
                for (var piece = 0; piece < 5; piece++)
                    ArtKit.Ball("Tongue piece", tongueOut, new Vector3(0f, -0.1f - piece * 0.13f, -0.06f - piece * 0.035f), new Vector3(0.26f - piece * 0.012f, 0.2f, 0.09f), gumMaterial);
                ArtKit.Ball("Tongue groove", tongueOut, new Vector3(0f, -0.35f, -0.17f), new Vector3(0.025f, 0.42f, 0.02f), inside);
            }
            var big = Style.Mouth == 4;
            if (Style.Mouth == 3 || big || female)
                ArtKit.Ball("Upper lip", mouth, new Vector3(0f, opening.y * (big ? 1.25f : 0.95f), big ? -0.14f : -0.08f), new Vector3(opening.x * (big ? 1.25f : 1.05f), big ? 0.17f : 0.06f, big ? 0.2f : 0.12f), lips);
            lowerLipScale = new Vector3(opening.x * (big ? 1.2f : 0.95f), big ? 0.2f : Style.Mouth == 3 ? 0.085f : 0.055f, big ? 0.22f : 0.12f) * 2f;
            lowerLip = ArtKit.Ball("Lower lip", mouth, new Vector3(0f, -opening.y * (big ? 1.35f : 0.95f), big ? -0.13f : -0.07f), lowerLipScale * 0.5f, lips);
        }

        private void BuildHair(Material hair)
        {
            var lift = new Vector3(0f, shape.z - 1.26f, 0f);
            var width = shape.x / 1.08f;
            switch (Style.Hair)
            {
                case 0:
                    ArtKit.Ball("Back hair", Root, new Vector3(0f, 0.55f, 0.22f) + lift, new Vector3(1.01f * width, 0.83f, 0.7f), hair);
                    for (var tuft = 0; tuft < 5; tuft++)
                        ArtKit.Ball("Swept hair", Root, new Vector3((-0.68f + tuft * 0.32f) * width, 1.03f + tuft * 0.055f, -0.27f) + lift, new Vector3(0.35f, 0.32f, 0.53f), hair, new Vector3(8f, -10f, -23f));
                    ArtKit.Ball("Quiff", Root, new Vector3(0.12f, 1.27f, -0.12f) + lift, new Vector3(0.67f, 0.25f, 0.47f), hair);
                    break;
                case 1:
                    ArtKit.Ball("Cap", Root, new Vector3(0f, 0.62f, 0.15f) + lift, new Vector3(1.02f * width, 0.8f, 0.75f), hair);
                    for (var spike = 0; spike < 9; spike++)
                    {
                        var angle = -64f + spike * 16f;
                        var radians = angle * Mathf.Deg2Rad;
                        var z = spike % 2 == 0 ? -0.25f : 0.2f;
                        ArtKit.Ball("Spike", Root, new Vector3(Mathf.Sin(radians) * 0.85f * width, 1.08f + Mathf.Cos(radians) * 0.2f, z) + lift, new Vector3(0.13f, 0.42f, 0.14f), hair, new Vector3(z * 40f, 0f, -angle));
                    }
                    break;
                case 3:
                case 4:
                    var longHair = Style.Hair == 3;
                    ArtKit.Ball("Crown", Root, new Vector3(0f, 0.6f, 0.15f) + lift, new Vector3(1.08f * width, 0.86f, 0.8f), hair);
                    ArtKit.Ball("Back", Root, new Vector3(0f, longHair ? -0.35f : 0.05f, 0.45f), new Vector3(1.05f * width, longHair ? 1.35f : 0.95f, 0.55f), hair);
                    foreach (var side in new[] { -1f, 1f })
                        ArtKit.Ball("Side", Root, new Vector3(side * 0.98f * width, longHair ? -0.45f : 0.05f, 0.2f), new Vector3(0.3f, longHair ? 1.25f : 0.75f, 0.55f), hair, new Vector3(0f, 0f, side * 4f));
                    if (longHair)
                    {
                        ArtKit.Ball("Bangs", Root, new Vector3(-0.35f, 1.0f, -0.42f) + lift, new Vector3(0.62f, 0.26f, 0.35f), hair, new Vector3(0f, 0f, -20f));
                        ArtKit.Ball("Bangs", Root, new Vector3(0.45f, 1.02f, -0.38f) + lift, new Vector3(0.5f, 0.24f, 0.35f), hair, new Vector3(0f, 0f, 22f));
                    }
                    else
                        ArtKit.Ball("Fringe", Root, new Vector3(0f, 0.98f, -0.4f) + lift, new Vector3(0.95f * width, 0.25f, 0.38f), hair);
                    break;
                case 5:
                    for (var crest = 0; crest < 6; crest++)
                        ArtKit.Ball("Mohawk", Root, new Vector3(0f, 1.15f + Mathf.Sin(crest * 0.55f) * 0.12f, -0.55f + crest * 0.24f) + lift, new Vector3(0.12f, 0.36f, 0.2f), hair, new Vector3(-30f + crest * 14f, 0f, 0f));
                    break;
                case 6:
                    ArtKit.Ball("Cap", Root, new Vector3(0f, 0.62f, 0.18f) + lift, new Vector3(1.04f * width, 0.82f, 0.74f), hair);
                    ArtKit.Ball("Bun", Root, new Vector3(0f, 1.42f, 0.35f) + lift, Vector3.one * 0.38f, hair);
                    ArtKit.Ball("Swoop", Root, new Vector3(0.2f, 1.0f, -0.42f) + lift, new Vector3(0.7f, 0.2f, 0.3f), hair, new Vector3(0f, 0f, 12f));
                    break;
                case 7:
                    ArtKit.Ball("Afro", Root, new Vector3(0f, 1.1f, 0.4f) + lift, new Vector3(1.45f * width, 1.0f, 1.15f), hair);
                    break;
            }
        }

        private Transform Feature(string name, Vector3 position)
        {
            var feature = ArtKit.Group(name, Root, position);
            features.Add(feature);
            origins.Add(position);
            return feature;
        }

        public static int ZoneOf(Vector3 point)
        {
            if (point.y > 0.66f) return ZoneForehead;
            if (point.y > 0.08f) return Mathf.Abs(point.x) < 0.16f ? ZoneNose : point.x < 0f ? ZoneEyeLeft : ZoneEyeRight;
            if (point.y > -0.42f) return Mathf.Abs(point.x) < 0.28f ? ZoneNose : point.x < 0f ? ZoneCheekLeft : ZoneCheekRight;
            if (point.y > -0.9f) return Mathf.Abs(point.x) < 0.5f ? ZoneMouth : point.x < 0f ? ZoneCheekLeft : ZoneCheekRight;
            return ZoneChin;
        }

        public bool Probe(Ray ray, out HitPoint point)
        {
            point = default;
            Physics.SyncTransforms();
            var found = false;
            var best = new RaycastHit();
            foreach (var candidate in Physics.RaycastAll(ray, 60f, 1 << FaceLayer))
            {
                if (candidate.collider != faceCollider || (found && candidate.distance >= best.distance)) continue;
                best = candidate;
                found = true;
            }
            if (!found) return false;
            var hit = best;
            point.Valid = true;
            point.Local = Root.InverseTransformPoint(hit.point);
            point.Normal = Root.InverseTransformDirection(hit.normal);
            point.Uv = hit.textureCoord;
            return true;
        }

        public HitPoint ProbeLocal(float x, float y)
        {
            var origin = Root.TransformPoint(new Vector3(x, y, -4f));
            Probe(new Ray(origin, Root.TransformDirection(Vector3.forward)), out var point);
            return point;
        }

        public HitResult ApplyHit(ToolKind tool, HitPoint hit, float power, Vector3 worldDirection)
        {
            var result = new HitResult { Word = "" };
            if (!hit.Valid) return result;
            var point = hit.Local;
            var uv = hit.Uv;
            var zone = ZoneOf(point);
            var side = point.x < 0f ? -1f : 1f;
            var eye = point.x < 0f ? 0 : 1;
            var region = point.x < -0.28f ? 0 : point.x > 0.28f ? 2 : 1;
            var direction = Root.InverseTransformDirection(worldDirection).normalized;
            var swing = Mathf.Abs(direction.x) > 0.05f ? Mathf.Sign(direction.x) : -side;
            var world = Root.TransformPoint(point);
            var outward = Root.TransformDirection(hit.Normal);
            var blunt = 0f;
            var bonus = 0;
            var turn = new Vector3(-6f, -side * 12f - direction.x * 14f, side * 6f);

            switch (tool)
            {
                case ToolKind.Punch:
                    blunt = power;
                    result.Word = Pick("POW!", "WHAM!", "BAM!", "OOF!");
                    break;
                case ToolKind.Slap:
                    blunt = 0.45f * power;
                    Skin.HandPrint(uv, -swing, 1f + Mathf.Min(1.5f, ZoneDamage[zone] * 0.2f));
                    turn = new Vector3(-3f, -swing * 42f, -swing * 12f);
                    result.Word = Pick("SMACK!", "SLAP!", "WHAP!");
                    break;
                case ToolKind.Egg:
                    blunt = 0.25f;
                    Skin.Splat(uv, 0.22f, new Color(0.99f, 0.98f, 0.88f), 0.8f, true);
                    Skin.Splat(SkinPainter.Offset(uv, Skin.Random(-0.03f, 0.03f), Skin.Random(-0.03f, 0.03f)), 0.1f, new Color(1f, 0.72f, 0.06f), 0.95f, true);
                    for (var piece = 0; piece < 9; piece++)
                        Debris?.Invoke(world, Spray(outward, 2.2f), DebrisKind.Shell, Color.white);
                    if (zone == ZoneEyeLeft || zone == ZoneEyeRight) goo[eye] = 2.5f;
                    SpawnEgg(point);
                    turn *= 0.5f;
                    result.Word = Pick("SPLAT!", "SPLOOSH!", "CRACK!");
                    break;
                case ToolKind.Slingshot:
                    blunt = 0.3f + 0.75f * power;
                    break;
                case ToolKind.Bat:
                    blunt = 2.2f * power;
                    turn = new Vector3(-12f, -swing * 38f, -swing * 28f);
                    Skin.Scrape(SkinPainter.Offset(uv, side * 0.05f, 0f), Skin.Random(-20f, 20f), 0.34f, 0.12f);
                    Dizzy = Mathf.Min(2f, Dizzy + 0.5f);
                    result.Word = Pick("THWACK!", "BONK!", "CRACK!", "WHAMMO!");
                    result.Heavy = true;
                    break;
                case ToolKind.Feet:
                    blunt = 1.2f * power;
                    var sole = new Color(0.36f, 0.33f, 0.16f);
                    Skin.Blob(uv, 0.2f, 0.32f, sole, 0.55f, 0.3f, Skin.Random(-15f, 15f));
                    for (var toe = 0; toe < 5; toe++)
                        Skin.Blob(SkinPainter.Offset(uv, (toe - 2f) * 0.085f, 0.38f - Mathf.Abs(toe - 2f) * 0.04f), 0.045f, 0.05f, sole, 0.6f, 0.4f);
                    for (var puff = 0; puff < 10; puff++)
                        Debris?.Invoke(world, Spray(outward, 1.2f, 0.5f), DebrisKind.Stink, Color.green);
                    wail = Mathf.Max(wail, 5f);
                    turn = new Vector3(-18f, -side * 20f, side * 14f);
                    result.Word = Pick("STINKY!", "PEE-YEW!", "FOOT!");
                    result.Heavy = true;
                    break;
                case ToolKind.Poop:
                    blunt = 0.6f * power;
                    var brown = new Color(0.36f, 0.2f, 0.07f);
                    Skin.HandPrint(uv, -swing, 2.2f);
                    Skin.Splat(uv, 0.2f, brown, 0.95f, true);
                    Skin.Splat(SkinPainter.Offset(uv, Skin.Random(-0.15f, 0.15f), Skin.Random(-0.1f, 0.2f)), 0.1f, new Color(0.45f, 0.28f, 0.1f), 0.9f, true);
                    for (var chunk = 0; chunk < 8; chunk++)
                        Debris?.Invoke(world, Spray(outward, 2.4f), DebrisKind.Poop, brown);
                    AddFlies(1);
                    Stinky = true;
                    turn = new Vector3(-3f, -swing * 46f, -swing * 14f);
                    result.Word = Pick("SPLOOT!", "POOP!", "SQUISH!");
                    break;
                case ToolKind.Dart:
                    StickDart(point, direction);
                    Skin.Blob(uv, 0.03f, 0.03f, BloodEnabled ? SkinPainter.BloodColor : SkinPainter.RednessColor, 0.9f, 0.5f);
                    if (BloodEnabled) Skin.AddDrip(uv, SkinPainter.BloodColor, 0.013f, Skin.Random(0.15f, 0.4f));
                    var bullseye = new Vector2(point.x - nose.localPosition.x, point.y - nose.localPosition.y).magnitude;
                    bonus = bullseye < 0.14f ? 50 : bullseye < 0.4f ? 20 : 5;
                    result.Word = bonus == 50 ? "BULLSEYE!" : Pick("THUNK!", "TOK!");
                    ZoneDamage[zone] += 0.3f;
                    Contact[region] = Mathf.Max(Contact[region], 0.35f);
                    if (zone == ZoneEyeLeft || zone == ZoneEyeRight) EyeRed[eye] = Mathf.Min(1f, EyeRed[eye] + 0.3f);
                    Kick(turn * 0.3f);
                    break;
            }

            if (blunt > 0f) Blunt(tool, zone, region, side, eye, blunt, point, hit.Normal, uv, world, outward, direction, ref result);
            if (tool == ToolKind.Slingshot)
            {
                var paint = PropKit.PaintColors[random.Next(PropKit.PaintColors.Length)];
                Skin.Splat(uv, 0.09f + 0.07f * power, paint, 0.93f, true);
                for (var drop = 0; drop < 6; drop++)
                    Debris?.Invoke(world, Spray(outward, 2f), DebrisKind.Paint, paint);
                if (string.IsNullOrEmpty(result.Word)) result.Word = Pick("SPLOTCH!", "PLAP!", "THWIP!");
            }
            if (tool != ToolKind.Dart && tool != ToolKind.Egg) Kick(turn * Mathf.Clamp(blunt, 0.6f, 1.6f));
            if (tool == ToolKind.Egg) Kick(turn);
            Skin.Flush();
            result.Score += Mathf.Max(5, Mathf.RoundToInt(blunt * 12f)) + bonus;
            return result;
        }

        private void Blunt(ToolKind tool, int zone, int region, float side, int eye, float blunt, Vector3 point, Vector3 normal, Vector2 uv, Vector3 world, Vector3 outward, Vector3 direction, ref HitResult result)
        {
            ZoneDamage[zone] += blunt;
            var damage = ZoneDamage[zone];
            var harsh = tool != ToolKind.Slap && tool != ToolKind.Egg && tool != ToolKind.Poop;
            Skin.Blob(uv, 0.18f + 0.05f * blunt, 0.16f + 0.05f * blunt, SkinPainter.RednessColor, Mathf.Min(0.4f, 0.14f * blunt + 0.08f));
            if (harsh)
            {
                var size = Mathf.Min(damage, 5f);
                var bruise = Color.Lerp(new Color(0.66f, 0.15f, 0.3f), SkinPainter.BruiseColor, Mathf.Clamp01(damage / 3f));
                var center = SkinPainter.Offset(uv, Skin.Random(-0.03f, 0.03f), Skin.Random(-0.03f, 0.03f));
                Skin.Blob(center, 0.09f + 0.04f * size, 0.08f + 0.04f * size, bruise, Mathf.Clamp01(0.22f * blunt + 0.07f * damage), 0.2f);
                if (damage > 2f) Skin.Blob(center, 0.05f + 0.02f * size, 0.045f + 0.02f * size, new Color(0.24f, 0.07f, 0.28f), 0.3f, 0.3f);
            }
            React(region, Mathf.Min(1.2f, blunt));
            Dizzy = Mathf.Min(2f, Dizzy + 0.035f * blunt);

            switch (zone)
            {
                case ZoneEyeLeft:
                case ZoneEyeRight:
                    if (!harsh) break;
                    BlackEye[eye] = Mathf.Min(1f, BlackEye[eye] + 0.2f * blunt);
                    var eyeCenter = ProbeLocal(eyes[eye].localPosition.x, eyes[eye].localPosition.y);
                    if (eyeCenter.Valid) Skin.Ring(eyeCenter.Uv, 0.32f, 0.32f, 0.1f, SkinPainter.BruiseColor, Mathf.Min(1f, 0.45f * blunt));
                    EyeRed[eye] = Mathf.Min(1f, EyeRed[eye] + 0.18f * blunt);
                    Droop[eye] = Mathf.Min(0.6f, Droop[eye] + 0.08f * blunt);
                    break;
                case ZoneNose:
                    NoseCrook = Mathf.Clamp(NoseCrook - direction.x * 0.25f * blunt + side * 0.08f * blunt, -1f, 1f);
                    if (harsh && (damage > 1.4f || blunt > 1.5f)) StartNosebleed(3f + 2.5f * blunt);
                    break;
                case ZoneMouth:
                case ZoneChin:
                    LipSwell = Mathf.Min(1.2f, LipSwell + (zone == ZoneMouth ? 0.12f : 0.04f) * blunt);
                    JawShift = Mathf.Clamp(JawShift + (Mathf.Abs(direction.x) > 0.05f ? Mathf.Sign(direction.x) : -side) * 0.05f * blunt, -0.35f, 0.35f);
                    if (harsh && KnockTeeth(point.x, blunt, damage, zone == ZoneMouth ? 1f : 0.4f, direction)) result.Word = "TOOTH!";
                    if (BloodEnabled && harsh && (damage > 1.5f || blunt > 1.5f) && random.NextDouble() < 0.6f)
                    {
                        var corner = ProbeLocal(mouth.localPosition.x + side * 0.28f, mouth.localPosition.y - 0.05f);
                        if (corner.Valid)
                        {
                            Skin.Cut(corner.Uv, Skin.Random(-70f, -110f), 0.08f, true);
                            Skin.AddDrip(corner.Uv, SkinPainter.BloodColor, 0.02f, Skin.Random(0.3f, 0.6f));
                        }
                        gumMaterial.SetColor("_BaseColor", Color.Lerp(GumColor, SkinPainter.BloodColor, Mathf.Clamp01(damage / 5f)));
                    }
                    break;
            }

            if (harsh && (zone == ZoneForehead || zone == ZoneCheekLeft || zone == ZoneCheekRight) && damage > 1.5f) GrowLump(point, normal, 0.015f * blunt);
            if (harsh && ((tool == ToolKind.Bat && random.NextDouble() < 0.65f) || (damage > 3f && random.NextDouble() < 0.22f * blunt)))
                Skin.Cut(SkinPainter.Offset(uv, Skin.Random(-0.06f, 0.06f), Skin.Random(-0.06f, 0.06f)), Skin.Random(-35f, 35f), Skin.Random(0.12f, 0.26f), BloodEnabled);
            else if (harsh && damage > 2.2f && random.NextDouble() < 0.3f)
                Skin.Scrape(uv, Skin.Random(-30f, 30f), 0.22f, 0.08f);

            if (BloodEnabled && harsh && (damage > 1.2f || tool == ToolKind.Bat))
            {
                var drops = Mathf.Min(14, Mathf.RoundToInt(blunt * 3f + damage));
                for (var drop = 0; drop < drops; drop++)
                    Debris?.Invoke(world, Spray(outward, 2.6f), DebrisKind.Blood, SkinPainter.BloodColor);
            }
            for (var spark = 0; spark < (harsh ? 6 : 3); spark++)
                Debris?.Invoke(world + outward * 0.15f, Spray(outward, 3.2f, 0f), DebrisKind.Star, Color.white);
        }

        private bool KnockTeeth(float x, float blunt, float damage, float scale, Vector3 direction)
        {
            var knocked = false;
            var attempts = blunt > 1.5f ? 2 : 1;
            for (var attempt = 0; attempt < attempts; attempt++)
            {
                if (random.NextDouble() > (0.2f * blunt + 0.06f * damage) * scale) continue;
                var best = -1;
                var nearest = float.MaxValue;
                for (var index = 0; index < teeth.Length; index++)
                {
                    if (toothState[index] == 2) continue;
                    var distance = Mathf.Abs(mouth.localPosition.x + toothPosition[index].x - x) + (float)random.NextDouble() * 0.15f;
                    if (distance < nearest)
                    {
                        nearest = distance;
                        best = index;
                    }
                }
                if (best < 0) return knocked;
                var tooth = teeth[best];
                if (toothState[best] == 0 && blunt < 1.5f)
                {
                    toothState[best] = 1;
                    TeethChipped++;
                    tooth.localScale = Vector3.Scale(toothScale[best], new Vector3(0.9f, 0.55f, 1f));
                    tooth.localPosition = toothPosition[best] + new Vector3(0f, toothScale[best].y * 0.22f, 0f);
                    tooth.localRotation = Quaternion.Euler(0f, 0f, random.Next(2) == 0 ? -14f : 14f);
                    Debris?.Invoke(tooth.position, Spray(Root.TransformDirection(-direction) + Vector3.up, 1.5f), DebrisKind.Tooth, Color.white);
                }
                else
                {
                    if (toothState[best] == 1) TeethChipped--;
                    toothState[best] = 2;
                    TeethLost++;
                    tooth.gameObject.SetActive(false);
                    Debris?.Invoke(tooth.position, Root.TransformDirection(new Vector3(direction.x * 2f, 2.4f, -2f)) + Spray(Vector3.up, 1f), DebrisKind.Tooth, Color.white);
                    if (BloodEnabled)
                    {
                        var gap = ProbeLocal(mouth.localPosition.x + toothPosition[best].x, mouth.localPosition.y - 0.2f);
                        if (gap.Valid) Skin.AddDrip(gap.Uv, SkinPainter.BloodColor, 0.018f, Skin.Random(0.3f, 0.5f));
                    }
                }
                knocked = true;
            }
            return knocked;
        }

        private void StartNosebleed(float seconds)
        {
            if (!BloodEnabled) return;
            nosebleed = Mathf.Max(nosebleed, seconds);
            nosebleedClock = 0f;
        }

        private void GrowLump(Vector3 point, Vector3 normal, float amount)
        {
            foreach (var lump in lumps)
            {
                if ((lump.Local - point).sqrMagnitude > 0.09f) continue;
                lump.Target = Mathf.Min(0.12f, lump.Target + amount);
                return;
            }
            if (lumps.Count >= 8) return;
            var body = ArtKit.Ball("Swelling", Root, point, Vector3.one * 0.01f, lumpMaterial);
            body.localRotation = Quaternion.LookRotation(normal);
            lumps.Add(new Lump { Body = body, Local = point, Normal = normal.normalized, Size = 0.01f, Target = 0.05f + amount });
        }

        private void StickDart(Vector3 point, Vector3 direction)
        {
            if (Props == null) return;
            if (darts.Count >= 12)
            {
                ArtKit.Destroy(darts[0].gameObject);
                darts.RemoveAt(0);
            }
            var dart = Props.Dart(Root);
            dart.localRotation = Quaternion.LookRotation(direction.sqrMagnitude > 0f ? direction : Vector3.forward);
            dart.localScale = Vector3.one * 1.3f;
            dart.localPosition = point - dart.localRotation * new Vector3(0f, 0f, 0.1f);
            darts.Add(dart);
        }

        private Vector3 Spray(Vector3 outward, float speed, float gravityLift = 1.2f)
        {
            var scatter = new Vector3((float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.3f, (float)random.NextDouble() - 0.5f) * 1.6f;
            return (outward.normalized + scatter).normalized * speed * (0.6f + (float)random.NextDouble() * 0.6f) + Vector3.up * gravityLift;
        }

        private string Pick(params string[] words) => words[random.Next(words.Length)];

        public void React(int region, float strength = 1f)
        {
            region = Mathf.Clamp(region, 0, 2);
            Contact[region] = Mathf.Clamp01(strength);
            Wear[region] = Mathf.Min(WearCap, Wear[region] + 0.065f * strength);
            Kick(new Vector3(-7f, (region - 1f) * 11f, (region - 1f) * -12f) * strength);
            Apply(0f);
        }

        public void AddFlies(int count)
        {
            if (flyMaterial == null) return;
            for (var index = 0; index < count && flies.Count < 12; index++)
            {
                var fly = ArtKit.Ball("Fly", Container, Root.position, new Vector3(0.05f, 0.04f, 0.06f), flyMaterial);
                ArtKit.Ball("Wing", fly, new Vector3(-0.6f, 0.6f, 0f), new Vector3(0.5f, 0.12f, 0.35f), wingMaterial);
                ArtKit.Ball("Wing", fly, new Vector3(0.6f, 0.6f, 0f), new Vector3(0.5f, 0.12f, 0.35f), wingMaterial);
                flies.Add(fly);
            }
        }

        private void SpawnEgg(Vector3 point)
        {
            if (eggWhiteMaterial == null) return;
            if (eggs.Count >= 6)
            {
                ArtKit.Destroy(eggs[0].Body.gameObject);
                eggs.RemoveAt(0);
            }
            var body = ArtKit.Group("Raw egg", Root);
            ArtKit.Ball("Egg white", body, Vector3.zero, new Vector3(0.3f, 0.27f, 0.04f), eggWhiteMaterial);
            for (var lobe = 0; lobe < 6; lobe++)
            {
                var angle = lobe * Mathf.PI / 3f + Skin.Random(-0.3f, 0.3f);
                var reach = Skin.Random(0.13f, 0.2f);
                ArtKit.Ball("Egg white", body, new Vector3(Mathf.Cos(angle) * reach, Mathf.Sin(angle) * reach, 0.005f), new Vector3(Skin.Random(0.1f, 0.16f), Skin.Random(0.09f, 0.14f), 0.025f), eggWhiteMaterial, new Vector3(0f, 0f, angle * Mathf.Rad2Deg));
            }
            var yolk = ArtKit.Ball("Yolk", body, new Vector3(0f, 0f, -0.05f), new Vector3(0.13f, 0.13f, 0.08f), yolkMaterial);
            ArtKit.Ball("Yolk shine", yolk, new Vector3(-0.3f, 0.35f, -0.75f), new Vector3(0.35f, 0.12f, 0.1f), shineMaterial, new Vector3(0f, 0f, 25f));
            var goo = new EggGoo { Body = body, X = point.x, Y = point.y };
            eggs.Add(goo);
            PlaceEgg(goo);
        }

        // Raw egg sticks, then slides and stretches slowly down the face before shrinking away.
        private bool PlaceEgg(EggGoo goo)
        {
            var hit = ProbeLocal(goo.X, goo.Y);
            if (!hit.Valid || goo.Age > 6f) return false;
            goo.Body.localPosition = hit.Local + hit.Normal * 0.07f;
            goo.Body.localRotation = Quaternion.LookRotation(-hit.Normal, Vector3.up);
            var stretch = Mathf.Clamp01((goo.Age - 0.7f) / 3.5f);
            var fade = 1f - Mathf.Clamp01((goo.Age - 5f) / 1f);
            goo.Body.localScale = new Vector3(1f - stretch * 0.3f, 1f + stretch * 0.9f, 1f) * fade;
            goo.Drip -= Time.deltaTime;
            if (goo.Age > 0.7f && goo.Drip <= 0f && fade > 0.5f)
            {
                goo.Drip = 0.35f;
                Skin.AddDrip(hit.Uv, random.Next(3) == 0 ? new Color(1f, 0.78f, 0.1f) : new Color(1f, 0.97f, 0.78f), 0.03f, Skin.Random(0.25f, 0.5f), 0.55f);
            }
            return true;
        }

        private void UpdateEggs(float deltaTime)
        {
            for (var index = eggs.Count - 1; index >= 0; index--)
            {
                var goo = eggs[index];
                goo.Age += deltaTime;
                if (goo.Age > 0.7f) goo.Y -= deltaTime * (0.05f + (goo.Age - 0.7f) * 0.025f);
                if (PlaceEgg(goo)) continue;
                ArtKit.Destroy(goo.Body.gameObject);
                eggs.RemoveAt(index);
            }
        }

        private void UpdateFlies(float clock)
        {
            for (var index = 0; index < flies.Count; index++)
            {
                var a = clock * (2.2f + index % 3 * 0.9f) + index * 1.7f;
                var b = clock * (3.1f + index % 2) + index;
                flies[index].position = Root.position + new Vector3(Mathf.Cos(a) * (1.3f + index % 3 * 0.2f), 0.4f + Mathf.Sin(b) * 0.9f, Mathf.Sin(a) * 1.1f - 0.2f);
                flies[index].rotation = Quaternion.Euler(Mathf.Sin(clock * 90f + index) * 25f, a * Mathf.Rad2Deg, 0f);
            }
        }

        private void Kick(Vector3 euler)
        {
            squashVelocity += Mathf.Clamp(euler.magnitude * 0.12f, 1.5f, 7f);
            impulse = euler;
            hitAge = 0f;
        }

        public void Tick(float deltaTime, float clock)
        {
            hitAge += deltaTime;
            UpdateEggs(deltaTime);
            squashVelocity += (-170f * squash - 11f * squashVelocity) * deltaTime;
            squash = Mathf.Clamp(squash + squashVelocity * deltaTime, -0.35f, 0.35f);
            for (var region = 0; region < 3; region++)
                Contact[region] *= Mathf.Exp(-deltaTime * 9f);
            Dizzy = Mathf.MoveTowards(Dizzy, 0f, deltaTime * 0.12f);
            for (var index = 0; index < 2; index++) goo[index] = Mathf.Max(0f, goo[index] - deltaTime);
            foreach (var lump in lumps)
            {
                lump.Size = Mathf.MoveTowards(lump.Size, lump.Target, deltaTime * 0.6f);
                lump.Body.localScale = new Vector3(2.2f, 2.2f, 1.3f) * lump.Size;
                lump.Body.localPosition = lump.Local - lump.Normal * lump.Size * 0.55f;
            }
            if (nosebleed > 0f)
            {
                nosebleed -= deltaTime;
                nosebleedClock -= deltaTime;
                if (nosebleedClock <= 0f)
                {
                    nosebleedClock = 0.75f;
                    nostril = 1 - nostril;
                    var side = nostril == 0 ? -1f : 1f;
                    var origin = ProbeLocal(nose.localPosition.x + side * 0.1f, nose.localPosition.y - 0.26f);
                    if (origin.Valid) Skin.AddDrip(origin.Uv, SkinPainter.BloodColor, 0.022f, Skin.Random(0.45f, 0.9f));
                    Debris?.Invoke(nose.TransformPoint(new Vector3(side * 0.12f, -0.2f, -0.15f)), new Vector3(0f, -0.3f, -0.4f), DebrisKind.Blood, SkinPainter.BloodColor);
                }
            }
            Skin.Tick(deltaTime);
            if (wail > 0f)
            {
                wail -= deltaTime;
                wailClock -= deltaTime;
                if (wailClock <= 0f)
                {
                    wailClock = 0.07f;
                    for (var index = 0; index < 2; index++)
                    {
                        var side = index == 0 ? -1f : 1f;
                        Debris?.Invoke(eyes[index].TransformPoint(new Vector3(side * 0.2f, 0f, -0.2f)), Root.TransformDirection(new Vector3(side * Skin.Random(2.2f, 3.4f), Skin.Random(2.2f, 3.6f), -1f)), DebrisKind.Tear, TearColor);
                    }
                    var below = ProbeLocal(eyes[random.Next(2)].localPosition.x, eyes[0].localPosition.y - eyeRadiusY * 0.9f);
                    if (below.Valid && random.Next(3) == 0) Skin.AddDrip(below.Uv, TearColor, 0.03f, Skin.Random(0.6f, 1.1f), 0.5f);
                }
            }
            if (Stinky)
            {
                stinkClock -= deltaTime;
                if (stinkClock <= 0f)
                {
                    stinkClock = 0.25f;
                    Debris?.Invoke(Root.TransformPoint(new Vector3(Skin.Random(-0.6f, 0.6f), Skin.Random(-0.4f, 0.4f), -0.8f)), new Vector3(0f, 0.6f, -0.2f), DebrisKind.Stink, Color.green);
                }
            }
            if (Crying)
            {
                tearClock -= deltaTime;
                if (tearClock <= 0f)
                {
                    tearClock = 0.6f;
                    var eye = eyes[random.Next(2)];
                    var below = ProbeLocal(eye.localPosition.x + Skin.Random(-0.1f, 0.1f), eye.localPosition.y - eyeRadiusY * 0.9f);
                    if (below.Valid) Skin.AddDrip(below.Uv, TearColor, 0.022f, Skin.Random(0.5f, 0.9f), 0.45f);
                    Debris?.Invoke(eye.TransformPoint(new Vector3(0f, -eyeRadiusY, -0.2f)), new Vector3(Skin.Random(-0.6f, 0.6f), 0.6f, -0.8f), DebrisKind.Tear, TearColor);
                }
            }
            Apply(clock);
        }

        private void Apply(float clock)
        {
            if (Face == null) return;
            for (var index = 0; index < 3; index++)
            {
                Face.SetBlendShapeWeight(index, Contact[index] * 100f);
                Face.SetBlendShapeWeight(index + 3, Wear[index] * 100f);
            }
            Face.SetBlendShapeWeight(6, Mathf.Max(0f, -JawShift) / 0.35f * 100f);
            Face.SetBlendShapeWeight(7, Mathf.Max(0f, JawShift) / 0.35f * 100f);
            for (var feature = 0; feature < features.Count; feature++)
            {
                var displacement = Vector3.zero;
                for (var region = 0; region < 3; region++)
                {
                    displacement += Deformation(origins[feature], region, false) * Contact[region];
                    displacement += Deformation(origins[feature], region, true) * Wear[region];
                }
                features[feature].localPosition = origins[feature] + displacement;
            }

            var worried = Mathf.Clamp01(TotalDamage / 10f);
            var pain = Mathf.Max(Contact[0], Mathf.Max(Contact[1], Contact[2]));
            var daze = Mathf.Clamp01(Dizzy);
            PoseArms(clock, pain, daze);
            nose.localPosition += new Vector3(NoseCrook * 0.08f, 0f, 0f);
            nose.localRotation = Quaternion.Euler(0f, NoseCrook * 12f, NoseCrook * -24f);
            nose.localScale = Vector3.one * (1f + Style.NoseBoost);
            noseMaterial.SetColor("_BaseColor", Color.Lerp(tone, new Color(0.7f, 0.2f, 0.32f), Mathf.Clamp01(ZoneDamage[ZoneNose] / 6f) * 0.65f));

            mouth.localPosition += new Vector3(JawShift * 0.45f, -JawShift * JawShift * 0.3f, 0f);
            mouth.localRotation = Quaternion.Euler(0f, 0f, smirk * (1f - worried) + (Contact[0] - Contact[2]) * 19f + JawShift * 40f + Mathf.Sin(clock * 9f) * worried * 2f);
            mouth.localScale = new Vector3(1f - Contact[1] * 0.25f - Talk * 0.12f, 1f + Contact[1] * 1.3f + pain * 0.4f + worried * 0.35f + Talk * 1.1f, 1f) * (1f + Style.LipBoost * 0.5f);
            lowerLip.localScale = Vector3.Scale(lowerLipScale, new Vector3(1f + LipSwell * 0.25f, 1f + LipSwell * 1.6f, 1f + LipSwell * 0.8f));
            if (tongueOut != null) tongueOut.localRotation = Quaternion.Euler(-25f + Mathf.Sin(clock * 5f) * 12f, 0f, Mathf.Sin(clock * 7.5f) * 18f);

            var blinkPhase = clock % 4.1f;
            var blink = blinkPhase > 3.9f ? Mathf.Sin((blinkPhase - 3.9f) / 0.2f * Mathf.PI) : 0f;
            for (var index = 0; index < 2; index++)
            {
                var side = index == 0 ? -1f : 1f;
                var hit = Contact[index == 0 ? 0 : 2];
                var open = 1f - hit * 0.78f - Contact[1] * 0.35f - blink * 0.85f - (goo[index] > 0f ? 0.65f : 0f) - BlackEye[index] * 0.2f;
                eyes[index].localScale = new Vector3(1f + BlackEye[index] * 0.08f, Mathf.Max(0.12f, open), 1f) * (1f + Style.EyeBoost);
                var droop = Mathf.Clamp01(baseDroop + Droop[index] + BlackEye[index] * 0.15f);
                lids[index].gameObject.SetActive(droop > 0.02f);
                lids[index].localPosition = new Vector3(0f, eyeRadiusY * (1f - droop * 0.9f), -0.03f);
                lids[index].localScale = new Vector3(eyeSize.x * 2.16f, Mathf.Max(0.02f, eyeRadiusY * droop * 2.3f), eyeSize.z * 2.4f);
                lidMaterials[index].SetColor("_BaseColor", Color.Lerp(tone, SkinPainter.BruiseColor, BlackEye[index] * 0.75f));
                eyeWhites[index].SetColor("_BaseColor", Color.Lerp(Color.white, new Color(0.95f, 0.42f, 0.42f), EyeRed[index] * 0.85f));
                var spin = clock * 7f + index * 1.3f;
                gaze[index].localPosition = new Vector3(-side * 0.035f + Mathf.Cos(spin) * 0.06f * daze, -0.015f + Mathf.Sin(spin) * 0.05f * daze, 0f);
                brows[index].localRotation = Quaternion.Euler(0f, 0f, side * Mathf.Lerp(Mathf.Lerp(-11f, -32f, Angry), 20f, worried) + side * pain * 12f);
                brows[index].localPosition = new Vector3(0f, eyeRadiusY + 0.04f + (Style.Sex == 1 ? 0.03f : 0f) - pain * 0.05f + worried * 0.03f, -0.025f);
            }

            var response = Mathf.Exp(-hitAge * 6.5f) * Mathf.Cos(hitAge * 16f);
            var wobble = new Vector3(Mathf.Sin(clock * 3.1f) * 7f, Mathf.Sin(clock * 1.7f) * 5f, Mathf.Cos(clock * 2.6f) * 9f) * daze;
            Root.localRotation = Quaternion.Euler(impulse * response + new Vector3(0f, Mathf.Sin(clock * 0.7f) * 2f, Mathf.Sin(clock * 1.1f) * 1.1f) + wobble);
            Root.localPosition = new Vector3(0f, HeadHeight + Mathf.Sin(clock * 1.8f) * 0.025f, Mathf.Max(0f, response) * Mathf.Min(0.4f, impulse.magnitude * 0.007f));
            Root.localScale = new Vector3(1f + squash, 1f - squash * 1.2f, 1f + squash * 0.6f);
            UpdateFlies(clock);

            var showStars = Dizzy > 0.55f;
            starRing.gameObject.SetActive(showStars);
            if (!showStars) return;
            for (var index = 0; index < stars.Length; index++)
            {
                var angle = clock * 3.2f + index * Mathf.PI * 2f / stars.Length;
                stars[index].localPosition = new Vector3(Mathf.Cos(angle) * 1.05f, Mathf.Sin(angle * 2f) * 0.05f, Mathf.Sin(angle) * 0.55f);
                stars[index].localRotation = Quaternion.Euler(0f, 0f, 45f + clock * 200f);
            }
        }

        public void Reset()
        {
            Array.Clear(Contact, 0, Contact.Length);
            Array.Clear(Wear, 0, Wear.Length);
            Array.Clear(ZoneDamage, 0, ZoneDamage.Length);
            Array.Clear(EyeRed, 0, 2);
            Array.Clear(Droop, 0, 2);
            Array.Clear(BlackEye, 0, 2);
            Array.Clear(goo, 0, 2);
            JawShift = 0f;
            NoseCrook = 0f;
            LipSwell = 0f;
            Dizzy = 0f;
            nosebleed = 0f;
            TeethLost = 0;
            TeethChipped = 0;
            impulse = Vector3.zero;
            hitAge = 10f;
            for (var index = 0; index < teeth.Length; index++)
            {
                toothState[index] = 0;
                teeth[index].gameObject.SetActive(true);
                teeth[index].localScale = toothScale[index];
                teeth[index].localPosition = toothPosition[index];
                teeth[index].localRotation = Quaternion.identity;
            }
            foreach (var lump in lumps) ArtKit.Destroy(lump.Body.gameObject);
            lumps.Clear();
            foreach (var dart in darts) ArtKit.Destroy(dart.gameObject);
            darts.Clear();
            foreach (var fly in flies) ArtKit.Destroy(fly.gameObject);
            flies.Clear();
            foreach (var goo in eggs) ArtKit.Destroy(goo.Body.gameObject);
            eggs.Clear();
            Stinky = false;
            wail = 0f;
            squash = 0f;
            squashVelocity = 0f;
            if (gumMaterial != null) gumMaterial.SetColor("_BaseColor", GumColor);
            Skin.Clear(tone);
            PaintBase();
            Skin.Flush();
            Apply(0f);
        }

        public static readonly Color TearColor = new Color(0.6f, 0.85f, 1f);

        // Soft blush and, for male faces, a light stubble shadow.
        private void PaintBase()
        {
            foreach (var side in new[] { -1f, 1f })
            {
                var cheek = ProbeLocal(side * 0.62f, -0.2f);
                if (cheek.Valid) Skin.Blob(cheek.Uv, 0.22f, 0.13f, Color.Lerp(tone, new Color(0.95f, 0.35f, 0.4f), 0.55f), 0.3f);
            }
            if (Style.Sex != 0) return;
            var jaw = ProbeLocal(0f, -0.92f);
            if (!jaw.Valid) return;
            var stubble = Color.Lerp(tone, new Color(0.18f, 0.16f, 0.2f), 0.45f);
            Skin.Blob(jaw.Uv, 0.78f, 0.3f, stubble, 0.22f);
            for (var dot = 0; dot < 260; dot++)
            {
                var x = Skin.Random(-0.72f, 0.72f);
                var y = Skin.Random(-0.3f, 0.3f);
                if (x * x / 0.52f + y * y / 0.09f > 1f) continue;
                Skin.Blob(SkinPainter.Offset(jaw.Uv, x, y), 0.01f, 0.01f, stubble, 0.35f, 0.5f);
            }
        }

        public static Vector3 Deformation(Vector3 point, int region, bool persistent)
        {
            var side = region - 1f;
            var center = new Vector3(side * 0.61f, region == 1 ? -0.22f : -0.08f, -0.72f);
            var offset = point - center;
            var influence = Mathf.Exp(-(offset.x * offset.x / 0.38f + offset.y * offset.y / 0.52f + offset.z * offset.z / 0.9f));
            influence *= Mathf.Clamp01((-point.z + 0.12f) / 0.55f);
            return persistent
                ? new Vector3(side * 0.16f, -0.035f, -0.21f) * influence
                : new Vector3(-side * 0.3f, region == 1 ? -0.16f : 0.1f, 0.36f) * influence;
        }

        private static Mesh CreateFaceMesh(Vector4 shape, Vector4 sculpt)
        {
            const int rings = 40;
            const int segments = 56;
            var vertices = new Vector3[(rings + 1) * (segments + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[rings * segments * 6];
            for (var ring = 0; ring <= rings; ring++)
            {
                var latitude = Mathf.PI * ring / rings;
                for (var segment = 0; segment <= segments; segment++)
                {
                    // Offset keeps the texture seam at the back of the head.
                    var longitude = 2f * Mathf.PI * segment / segments + Mathf.PI * 0.5f;
                    var index = ring * (segments + 1) + segment;
                    vertices[index] = FaceStyle.FacePoint(shape, sculpt, latitude, longitude);
                    uv[index] = new Vector2((float)segment / segments, (float)ring / rings);
                    if (ring == rings || segment == segments) continue;
                    var next = index + segments + 1;
                    var triangle = (ring * segments + segment) * 6;
                    triangles[triangle] = index;
                    triangles[triangle + 1] = index + 1;
                    triangles[triangle + 2] = next;
                    triangles[triangle + 3] = index + 1;
                    triangles[triangle + 4] = next + 1;
                    triangles[triangle + 5] = next;
                }
            }
            var mesh = new Mesh { name = "Custom cartoon face", vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var names = new[] { "LeftCompression", "CenterCompression", "RightCompression", "LeftPuff", "CenterPuff", "RightPuff" };
            for (var index = 0; index < 6; index++)
            {
                var deltas = new Vector3[vertices.Length];
                for (var vertex = 0; vertex < vertices.Length; vertex++)
                    deltas[vertex] = Deformation(vertices[vertex], index % 3, index >= 3);
                mesh.AddBlendShapeFrame(names[index], 100f, deltas, null, null);
            }
            for (var direction = -1; direction <= 1; direction += 2)
            {
                var deltas = new Vector3[vertices.Length];
                for (var vertex = 0; vertex < vertices.Length; vertex++)
                {
                    var point = vertices[vertex];
                    var weight = Mathf.SmoothStep(0f, 1f, (-0.3f - point.y) / 0.75f) * Mathf.Clamp01((-point.z + 0.3f) / 0.7f);
                    deltas[vertex] = new Vector3(direction * 0.24f * weight, 0f, 0f);
                }
                mesh.AddBlendShapeFrame(direction < 0 ? "JawLeft" : "JawRight", 100f, deltas, null, null);
            }
            return mesh;
        }

        public void Clear()
        {
            lumps.Clear();
            darts.Clear();
            flies.Clear();
            eggs.Clear();
            features.Clear();
            origins.Clear();
            if (Container != null) ArtKit.Destroy(Container.gameObject);
            Container = null;
            Face = null;
            kit?.Dispose();
            kit = null;
        }

        public void Dispose()
        {
            Clear();
            ArtKit.Destroy(Skin.Texture);
        }
    }
}

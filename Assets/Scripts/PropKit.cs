using UnityEngine;

namespace EmoRelief
{
    // Shared models for the six activities (held, thrown, or stuck in the face).
    public sealed class PropKit
    {
        public static readonly Color[] PaintColors =
        {
            new Color(0.95f, 0.15f, 0.6f),
            new Color(0.1f, 0.75f, 0.95f),
            new Color(0.5f, 0.9f, 0.15f),
            new Color(1f, 0.55f, 0.1f),
            new Color(0.55f, 0.25f, 0.95f)
        };

        public readonly Material Metal;
        public readonly Material DartBody;
        public readonly Material Flight;
        public readonly Material Shell;
        public readonly Material White;
        public readonly Material Ink;
        public readonly Material FootSkin;
        public readonly Material Nail;
        public readonly Material Dirt;
        public readonly Material Poo;
        public readonly Material Hair, Pimple, Denim, Stink, Bug, Wing, Corn;
        public readonly Material Wood;
        public readonly Material Grip;
        public readonly Material Band;
        public readonly Material Leather;
        public readonly Material[] Paint = new Material[PaintColors.Length];
        private readonly ArtKit kit = new ArtKit();
        private readonly System.Collections.Generic.Dictionary<string, Mesh> meshes = new System.Collections.Generic.Dictionary<string, Mesh>();

        public PropKit()
        {
            Metal = kit.Lit("Dart steel", new Color(0.75f, 0.78f, 0.82f), 0.85f);
            DartBody = kit.Lit("Dart barrel", new Color(0.18f, 0.12f, 0.32f), 0.6f);
            Flight = kit.Lit("Dart flight", new Color(0.95f, 0.2f, 0.62f), 0.3f);
            Shell = kit.Lit("Rotten egg shell", new Color(0.8f, 0.82f, 0.48f), 0.45f);
            White = kit.Lit("Prop white", new Color(1f, 0.98f, 0.95f), 0.6f);
            Ink = kit.Lit("Prop ink", new Color(0.07f, 0.03f, 0.06f), 0.4f);
            FootSkin = kit.Lit("Rotten foot", new Color(0.93f, 0.7f, 0.52f), 0.3f);
            FootSkin.SetTexture("_BaseMap", Mottled("Foot grime", new Color(1f, 1f, 1f), new Color(0.72f, 0.68f, 0.42f), new Color(0.6f, 0.45f, 0.3f), 11));
            Nail = kit.Lit("Toenail", new Color(0.86f, 0.74f, 0.28f), 0.7f);
            Dirt = kit.Lit("Dirt", new Color(0.32f, 0.26f, 0.12f), 0.2f);
            Hair = kit.Unlit("Leg hair", new Color(0.16f, 0.1f, 0.06f));
            Pimple = kit.Lit("Pimple", new Color(0.88f, 0.42f, 0.42f), 0.75f);
            Denim = kit.Lit("Jeans", new Color(0.18f, 0.3f, 0.62f), 0.2f);
            Stink = kit.Unlit("Stink line", new Color(0.62f, 0.85f, 0.3f));
            Bug = kit.Lit("Bug", new Color(0.06f, 0.06f, 0.08f), 0.9f);
            Wing = kit.Unlit("Bug wing", new Color(0.85f, 0.92f, 1f));
            Poo = kit.Lit("Poop", Color.white, 0.92f);
            Poo.SetTexture("_BaseMap", Mottled("Poop texture", new Color(0.45f, 0.26f, 0.09f), new Color(0.24f, 0.13f, 0.04f), new Color(0.5f, 0.38f, 0.12f), 5));
            Corn = kit.Lit("Corn", new Color(1f, 0.85f, 0.2f), 0.8f);
            Wood = kit.Lit("Bat wood", new Color(0.78f, 0.55f, 0.3f), 0.55f);
            Grip = kit.Lit("Bat grip", new Color(0.12f, 0.08f, 0.14f), 0.2f);
            Band = kit.Lit("Slingshot band", new Color(0.95f, 0.35f, 0.25f), 0.4f);
            Leather = kit.Lit("Slingshot pouch", new Color(0.4f, 0.22f, 0.14f), 0.25f);
            for (var index = 0; index < PaintColors.Length; index++)
                Paint[index] = kit.Lit("Paintball", PaintColors[index], 0.8f);
        }

        // Tip points along +Z.
        public Transform Dart(Transform parent)
        {
            var dart = ArtKit.Group("Dart", parent);
            ArtKit.Rod("Needle", dart, new Vector3(0f, 0f, 0.1f), 0.012f, 0.2f, Metal, new Vector3(90f, 0f, 0f));
            ArtKit.Rod("Barrel", dart, new Vector3(0f, 0f, -0.04f), 0.034f, 0.16f, DartBody, new Vector3(90f, 0f, 0f));
            ArtKit.Rod("Shaft", dart, new Vector3(0f, 0f, -0.2f), 0.014f, 0.18f, DartBody, new Vector3(90f, 0f, 0f));
            ArtKit.Box("Flight", dart, new Vector3(0f, 0f, -0.31f), new Vector3(0.17f, 0.006f, 0.13f), Flight);
            ArtKit.Box("Flight", dart, new Vector3(0f, 0f, -0.31f), new Vector3(0.006f, 0.17f, 0.13f), Flight);
            return dart;
        }

        public Transform Egg(Transform parent)
        {
            var egg = ArtKit.Ball("Rotten egg", parent, Vector3.zero, new Vector3(0.11f, 0.14f, 0.11f), Shell);
            ArtKit.Ball("Spot", egg, new Vector3(0.25f, 0.15f, -0.38f), Vector3.one * 0.14f, Dirt);
            ArtKit.Ball("Spot", egg, new Vector3(-0.3f, -0.2f, -0.32f), Vector3.one * 0.1f, Dirt);
            ArtKit.Ball("Crack", egg, new Vector3(0.05f, 0.35f, -0.4f), new Vector3(0.18f, 0.03f, 0.05f), Ink, new Vector3(0f, 0f, 25f));
            return egg;
        }

        // Sole faces +Z, toes point +Y; the bare leg trails back toward the player (-Z).
        public Transform Foot(Transform parent)
        {
            var random = new System.Random(7);
            float R(float a, float b) => a + (float)random.NextDouble() * (b - a);
            var foot = ArtKit.Group("Rotten foot", parent);
            MeshKit.Part("Foot", foot, Shape("Foot body", new Vector3(0.44f, 0.7f, 0.22f), 2.4f), FootSkin);
            MeshKit.Part("Heel", foot, Shape("Heel", new Vector3(0.38f, 0.32f, 0.3f), 2.2f), FootSkin, new Vector3(0f, -0.55f, -0.04f));
            MeshKit.Part("Ankle", foot, Shape("Ankle", new Vector3(0.3f, 0.6f, 0.3f), 2f), FootSkin, new Vector3(0f, -0.75f, -0.45f), new Vector3(-50f, 0f, 0f));
            var knee = new Vector3(0f, -1.25f, -2.7f);
            var ankle = new Vector3(0f, -0.82f, -0.55f);
            var shin = MeshKit.Part("Shin", foot, kit.Own(MeshKit.Capsule("Shin", 0.27f, 0.36f, (knee - ankle).magnitude)), FootSkin, ankle);
            shin.localRotation = Quaternion.LookRotation(knee - ankle, Vector3.up);
            var cuff = MeshKit.Part("Rolled jeans", foot, kit.Own(MeshKit.Capsule("Cuff", 0.42f, 0.46f, 0.9f)), Denim, Vector3.Lerp(ankle, knee, 0.82f));
            cuff.localRotation = shin.localRotation;
            for (var toe = 0; toe < 5; toe++)
            {
                var size = toe == 0 ? 0.15f : 0.11f - toe * 0.01f;
                var x = toe == 0 ? -0.3f : -0.1f + (toe - 1) * 0.155f;
                var y = 0.7f + size * 0.7f - (toe == 0 ? 0f : (toe - 1) * 0.05f);
                var digit = ArtKit.Group("Toe", foot, new Vector3(x, y, -0.02f), new Vector3(-12f, 0f, (x + 0.05f) * -25f));
                ArtKit.Ball("Toe", digit, Vector3.zero, new Vector3(size, size * 1.55f, size * 1.05f), FootSkin);
                ArtKit.Ball("Knuckle", digit, new Vector3(0f, -size * 0.9f, -size * 0.15f), new Vector3(size * 0.95f, size * 0.7f, size), FootSkin);
                ArtKit.Ball("Toenail", digit, new Vector3(0f, size * 0.55f, -size * 0.8f), new Vector3(size * 0.75f, size * 0.75f, size * 0.32f), Nail, new Vector3(R(-8f, 8f), 0f, R(-10f, 10f)));
                ArtKit.Ball("Nail grime", digit, new Vector3(0f, size * 1.05f, -size * 0.75f), new Vector3(size * 0.6f, size * 0.18f, size * 0.25f), Dirt);
                for (var strand = 0; strand < 3; strand++)
                    ArtKit.Box("Toe hair", digit, new Vector3(R(-0.5f, 0.5f) * size, -size * R(0.6f, 1.1f), -size * 1.05f), new Vector3(0.01f, 0.07f, 0.01f), Hair, new Vector3(R(-40f, 40f), 0f, R(-50f, 50f)));
            }
            for (var strand = 0; strand < 26; strand++)
                ArtKit.Box("Foot hair", foot, new Vector3(R(-0.38f, 0.38f), R(-0.4f, 0.55f), -0.235f), new Vector3(0.007f, R(0.06f, 0.11f), 0.007f), Hair, new Vector3(R(-50f, 50f), 0f, R(-60f, 60f)));
            var along = (knee - ankle).normalized;
            var side = Vector3.Cross(along, Vector3.up).normalized;
            var top = Vector3.Cross(side, along).normalized;
            for (var strand = 0; strand < 45; strand++)
            {
                var t = R(0.05f, 0.8f);
                var angle = R(-1.6f, 1.6f);
                var normal = top * Mathf.Cos(angle) + side * Mathf.Sin(angle);
                var position = Vector3.Lerp(ankle, knee, t) + normal * (Mathf.Lerp(0.27f, 0.36f, t) + 0.02f);
                var hair = ArtKit.Box("Leg hair", foot, position, new Vector3(0.007f, R(0.09f, 0.16f), 0.007f), Hair);
                hair.localRotation = Quaternion.LookRotation(along, normal) * Quaternion.Euler(R(30f, 70f), R(-30f, 30f), 0f);
            }
            for (var spot = 0; spot < 9; spot++)
                ArtKit.Ball("Pimple", foot, new Vector3(R(-0.36f, 0.36f), R(-0.5f, 0.5f), -0.225f), Vector3.one * R(0.025f, 0.045f), Pimple);
            for (var spot = 0; spot < 5; spot++)
                ArtKit.Ball("Wart", foot, new Vector3(R(-0.4f, 0.4f), R(-0.6f, 0.4f), R(-0.3f, -0.15f)), Vector3.one * R(0.03f, 0.05f), Dirt);
            ArtKit.Ball("Sole dirt", foot, new Vector3(0.1f, 0.1f, 0.22f), new Vector3(0.3f, 0.45f, 0.04f), Dirt);
            ArtKit.Ball("Heel dirt", foot, new Vector3(-0.05f, -0.55f, 0.3f), new Vector3(0.26f, 0.2f, 0.05f), Dirt);
            var flies = ArtKit.Group("Foot flies", foot, new Vector3(0f, 0.1f, -0.2f));
            for (var index = 0; index < 5; index++)
            {
                var angle = index * Mathf.PI * 2f / 5f;
                Fly(flies, new Vector3(Mathf.Cos(angle) * 0.65f, Mathf.Sin(angle) * 0.75f, R(-0.35f, 0f)), 1f);
            }
            var stink = ArtKit.Group("Foot stink", foot, new Vector3(0f, 0.95f, -0.15f));
            for (var line = 0; line < 3; line++)
                for (var dot = 0; dot < 6; dot++)
                    ArtKit.Ball("Stink", stink, new Vector3((line - 1) * 0.25f + Mathf.Sin(dot * 1.3f + line) * 0.06f, dot * 0.09f, 0f), new Vector3(0.025f, 0.06f, 0.025f), Stink);
            return foot;
        }

        public Transform Poop(Transform parent)
        {
            var random = new System.Random(3);
            float R(float a, float b) => a + (float)random.NextDouble() * (b - a);
            var poop = ArtKit.Group("Poop", parent);
            var tiers = new[] { 0.18f, 0.14f, 0.1f, 0.065f };
            for (var tier = 0; tier < tiers.Length; tier++)
            {
                var count = 7 - tier;
                for (var lump = 0; lump < count; lump++)
                {
                    var angle = (lump + tier * 0.5f) * Mathf.PI * 2f / count;
                    var radius = tiers[tier] * 0.62f;
                    ArtKit.Ball("Lump", poop, new Vector3(Mathf.Cos(angle) * radius, tier * 0.065f + R(-0.01f, 0.01f), Mathf.Sin(angle) * radius * 0.85f), new Vector3(tiers[tier] * 0.62f, 0.055f, tiers[tier] * 0.55f) * R(0.85f, 1.15f), Poo, new Vector3(R(-15f, 15f), angle * -Mathf.Rad2Deg, R(-15f, 15f)));
                }
            }
            ArtKit.Ball("Tip", poop, new Vector3(0.03f, 0.25f, 0f), new Vector3(0.03f, 0.055f, 0.03f), Poo, new Vector3(0f, 0f, -35f));
            for (var bit = 0; bit < 6; bit++)
                ArtKit.Ball("Corn", poop, new Vector3(R(-0.13f, 0.13f), R(0.01f, 0.2f), R(-0.15f, -0.08f)), new Vector3(0.018f, 0.014f, 0.012f), Corn, new Vector3(R(0f, 90f), R(0f, 90f), 0f));
            for (var drip = 0; drip < 4; drip++)
                ArtKit.Ball("Drip", poop, new Vector3(R(-0.15f, 0.15f), -0.04f - R(0f, 0.05f), R(-0.14f, 0.1f)), new Vector3(0.02f, R(0.04f, 0.07f), 0.02f), Poo);
            var flies = ArtKit.Group("Poop flies", poop, new Vector3(0f, 0.18f, 0f));
            for (var index = 0; index < 3; index++)
            {
                var angle = index * Mathf.PI * 2f / 3f;
                Fly(flies, new Vector3(Mathf.Cos(angle) * 0.25f, R(0f, 0.12f), Mathf.Sin(angle) * 0.25f), 0.55f);
            }
            return poop;
        }

        private void Fly(Transform parent, Vector3 position, float scale)
        {
            var fly = ArtKit.Ball("Fly", parent, position, new Vector3(0.04f, 0.03f, 0.055f) * scale, Bug);
            ArtKit.Box("Wing", fly, new Vector3(-0.9f, 0.7f, 0f), new Vector3(1.6f, 0.15f, 1f), Wing, new Vector3(0f, 0f, 25f));
            ArtKit.Box("Wing", fly, new Vector3(0.9f, 0.7f, 0f), new Vector3(1.6f, 0.15f, 1f), Wing, new Vector3(0f, 0f, -25f));
        }

        // Blotchy procedural texture: base colour with darker and lighter organic patches.
        private static Texture2D Mottled(string name, Color baseColor, Color dark, Color light, int seed)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Repeat };
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var u = x / (float)size;
                    var v = y / (float)size;
                    var a = Mathf.PerlinNoise(u * 6f + seed, v * 6f + seed * 0.7f);
                    var b = Mathf.PerlinNoise(u * 19f + seed * 1.3f, v * 19f);
                    var color = Color.Lerp(baseColor, dark, Mathf.SmoothStep(0f, 1f, (a - 0.45f) * 3f));
                    pixels[y * size + x] = Color.Lerp(color, light, Mathf.SmoothStep(0f, 1f, (b - 0.62f) * 4f));
                }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        // Boxing glove: wrist at origin, knuckles along +Z, back of hand +Y.
        public Transform Glove(Transform parent, float side, Material leather, Material trim)
        {
            var glove = ArtKit.Group("Boxing glove", parent);
            MeshKit.Part("Cuff", glove, Shape("Glove cuff", new Vector3(0.215f, 0.2f, 0.22f), 2.6f), leather, new Vector3(0f, 0f, -0.02f));
            MeshKit.Part("Cuff band", glove, Shape("Glove band", new Vector3(0.222f, 0.207f, 0.055f), 2.6f), trim, new Vector3(0f, 0f, -0.1f));
            MeshKit.Part("Mitt", glove, Shape("Glove mitt", new Vector3(0.3f, 0.26f, 0.33f), 2.3f), leather, new Vector3(0f, 0.02f, 0.35f));
            MeshKit.Part("Knuckle roll", glove, Shape("Glove knuckles", new Vector3(0.28f, 0.21f, 0.15f), 2.2f), leather, new Vector3(0f, 0.035f, 0.6f));
            MeshKit.Part("Thumb", glove, Shape("Glove thumb", new Vector3(0.09f, 0.1f, 0.21f), 2f), leather, new Vector3(-side * 0.27f, -0.05f, 0.33f), new Vector3(0f, -side * 18f, side * 12f));
            MeshKit.Part("Badge", glove, Shape("Glove badge", new Vector3(0.11f, 0.012f, 0.085f), 2.5f), trim, new Vector3(0f, 0.272f, 0.36f), new Vector3(-6f, 0f, 0f));
            return glove;
        }

        private Mesh Shape(string name, Vector3 radius, float exponent)
        {
            var key = name + radius;
            if (meshes.TryGetValue(key, out var mesh)) return mesh;
            mesh = kit.Own(MeshKit.Rounded(name, radius, exponent));
            meshes[key] = mesh;
            return mesh;
        }

        public Material GloveLeather(string name, Color color) => kit.Lit(name, color, 0.78f);

        public Transform Paintball(Transform parent, int color) => ArtKit.Ball("Paintball", parent, Vector3.zero, Vector3.one * 0.075f, Paint[color % Paint.Length]);

        // Grip at the origin, barrel along +Y.
        public Transform Bat(Transform parent)
        {
            var bat = ArtKit.Group("Baseball bat", parent);
            ArtKit.Ball("Knob", bat, new Vector3(0f, -0.06f, 0f), new Vector3(0.075f, 0.04f, 0.075f), Wood);
            ArtKit.Rod("Grip tape", bat, new Vector3(0f, 0.2f, 0f), 0.05f, 0.5f, Grip);
            ArtKit.Rod("Handle", bat, new Vector3(0f, 0.65f, 0f), 0.058f, 0.45f, Wood);
            ArtKit.Rod("Taper", bat, new Vector3(0f, 0.98f, 0f), 0.085f, 0.3f, Wood);
            ArtKit.Rod("Barrel", bat, new Vector3(0f, 1.45f, 0f), 0.12f, 0.7f, Wood);
            ArtKit.Ball("End cap", bat, new Vector3(0f, 1.8f, 0f), new Vector3(0.12f, 0.05f, 0.12f), Wood);
            foreach (var side in new[] { -1f, 1f })
            {
                ArtKit.Ball("Bat eye", bat, new Vector3(side * 0.05f, 1.52f, -0.105f), new Vector3(0.045f, 0.055f, 0.03f), White);
                ArtKit.Ball("Bat pupil", bat, new Vector3(side * 0.045f, 1.51f, -0.13f), new Vector3(0.02f, 0.028f, 0.012f), Ink);
                ArtKit.Box("Bat brow", bat, new Vector3(side * 0.055f, 1.59f, -0.11f), new Vector3(0.07f, 0.015f, 0.02f), Ink, new Vector3(0f, 0f, side * 25f));
            }
            ArtKit.Ball("Bat grin", bat, new Vector3(0f, 1.4f, -0.108f), new Vector3(0.07f, 0.03f, 0.02f), Ink);
            ArtKit.Box("Bat teeth", bat, new Vector3(0f, 1.41f, -0.122f), new Vector3(0.09f, 0.018f, 0.01f), White);
            return bat;
        }

        // Handle hangs below the origin; fork tips are returned for the bands.
        public Transform Slingshot(Transform parent, out Transform leftTip, out Transform rightTip)
        {
            var frame = ArtKit.Group("Slingshot", parent);
            ArtKit.Rod("Handle", frame, new Vector3(0f, -0.2f, 0f), 0.05f, 0.42f, Wood);
            ArtKit.Ball("Crotch", frame, new Vector3(0f, 0.02f, 0f), Vector3.one * 0.07f, Wood);
            ArtKit.Rod("Left fork", frame, new Vector3(-0.1f, 0.2f, 0f), 0.04f, 0.42f, Wood, new Vector3(0f, 0f, 28f));
            ArtKit.Rod("Right fork", frame, new Vector3(0.1f, 0.2f, 0f), 0.04f, 0.42f, Wood, new Vector3(0f, 0f, -28f));
            leftTip = ArtKit.Group("Left tip", frame, new Vector3(-0.2f, 0.39f, 0f));
            rightTip = ArtKit.Group("Right tip", frame, new Vector3(0.2f, 0.39f, 0f));
            ArtKit.Rod("Left wrap", leftTip, Vector3.zero, 0.048f, 0.07f, Band, new Vector3(0f, 0f, 28f));
            ArtKit.Rod("Right wrap", rightTip, Vector3.zero, 0.048f, 0.07f, Band, new Vector3(0f, 0f, -28f));
            return frame;
        }

        public void Dispose() => kit.Dispose();
    }
}

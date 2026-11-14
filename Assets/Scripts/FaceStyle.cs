using UnityEngine;

namespace EmoRelief
{
    public enum ToolKind { Punch, Slap, Egg, Slingshot, Bat, Dart, Feet, Poop }

    public enum DebrisKind { Blood, Tooth, Shell, Paint, Star, Tear, Stink, Poop }

    [System.Serializable]
    public struct FaceStyle
    {
        public int Sex;
        public int Shape;
        public int Skin;
        public int Eyes;
        public int Nose;
        public int Mouth;
        public int Hair;
        public int Ears;
        // Caricature controls: 0 = neutral.
        public float Chin;
        public float Jaw;
        public float Brow;
        public float Cranium;
        public float NoseBoost;
        public float EyeBoost;
        public float LipBoost;
        public float Bulk;

        public static readonly string[] CharacterNames = { "Big Bruno", "Drama Donna", "Lippy Lou", "Bug-Eye Bella", "Classic Guy", "Classic Gal" };

        public static FaceStyle Character(int index)
        {
            switch (index)
            {
                case 0: return new FaceStyle { Sex = 0, Shape = 2, Skin = 2, Eyes = 2, Nose = 3, Mouth = 1, Hair = 0, Ears = 1, Chin = 1.4f, Jaw = 1f, Brow = 1f, Cranium = -0.3f, NoseBoost = 0.6f, EyeBoost = -0.15f, LipBoost = 0.3f, Bulk = 1f };
                case 1: return new FaceStyle { Sex = 1, Shape = 3, Skin = 0, Eyes = 1, Nose = 1, Mouth = 3, Hair = 6, Ears = 2, Chin = 0.9f, Jaw = -0.25f, Brow = 0.3f, Cranium = 0.35f, NoseBoost = 0.5f, EyeBoost = 0.35f, LipBoost = 0.9f, Bulk = 0.15f };
                case 2: return new FaceStyle { Sex = 0, Shape = 2, Skin = 4, Eyes = 3, Nose = 2, Mouth = 4, Hair = 5, Ears = 2, Chin = 1.1f, Jaw = 1.4f, Brow = 0.6f, Cranium = -0.85f, NoseBoost = 0.1f, EyeBoost = -0.2f, LipBoost = 0.7f, Bulk = 0.8f };
                case 3: return new FaceStyle { Sex = 1, Shape = 4, Skin = 1, Eyes = 1, Nose = 0, Mouth = 2, Hair = 4, Ears = 0, Chin = 1.1f, Jaw = -0.9f, Brow = -0.2f, Cranium = 0.6f, NoseBoost = -0.45f, EyeBoost = 0.35f, LipBoost = -0.25f, Bulk = 0f };
                case 5: return new FaceStyle { Sex = 1, Skin = 1, Hair = 3, Mouth = 3 };
                default: return Default;
            }
        }

        public Vector4 SculptParameters() => new Vector4(Chin, Jaw, Brow, Cranium);

        public static readonly string[] SexNames = { "Male", "Female" };
        public static readonly string[] ShapeNames = { "Oval", "Round", "Square", "Long", "Heart" };
        public static readonly string[] SkinNames = { "Porcelain", "Peach", "Honey", "Olive", "Brown", "Deep" };
        public static readonly Color[] SkinColors =
        {
            new Color(0.98f, 0.80f, 0.70f),
            new Color(0.94f, 0.62f, 0.47f),
            new Color(0.86f, 0.60f, 0.38f),
            new Color(0.74f, 0.52f, 0.34f),
            new Color(0.55f, 0.35f, 0.22f),
            new Color(0.36f, 0.22f, 0.15f)
        };
        public static readonly string[] EyeNames = { "Round", "Big", "Narrow", "Sleepy" };
        public static readonly string[] NoseNames = { "Button", "Pointy", "Wide", "Big" };
        public static readonly string[] MouthNames = { "Smirk", "Grin", "Small", "Full lips", "Big lips", "Buck teeth", "Long tongue" };
        public static readonly string[] HairNames = { "Swept", "Spiky", "Bald", "Long", "Bob", "Mohawk", "Bun", "Afro" };
        public static readonly string[] EarNames = { "Normal", "Big", "Small", "Pointy" };
        public static readonly string[] ToolNames = { "Punch", "Slap", "Egg", "Slingshot", "Bat", "Dart", "Rotten Feet", "Poop Slap" };

        public static FaceStyle Default => new FaceStyle { Skin = 1 };

        public static FaceStyle Random(System.Random random)
        {
            var sex = random.Next(2);
            return new FaceStyle
            {
                Sex = sex,
                Shape = random.Next(ShapeNames.Length),
                Skin = random.Next(SkinNames.Length),
                Eyes = random.Next(EyeNames.Length),
                Nose = random.Next(NoseNames.Length),
                Mouth = sex == 1 && random.Next(2) == 0 ? 3 : random.Next(MouthNames.Length),
                Hair = sex == 1 ? new[] { 3, 4, 6, 7 }[random.Next(4)] : new[] { 0, 1, 2, 5, 7 }[random.Next(5)],
                Ears = random.Next(EarNames.Length)
            };
        }

        public Vector4 ShapeParameters()
        {
            // width, jaw width, half height, squareness
            Vector4 shape;
            switch (Shape)
            {
                case 1: shape = new Vector4(1.14f, 1.0f, 1.2f, 1f); break;
                case 2: shape = new Vector4(1.06f, 1.0f, 1.24f, 0.6f); break;
                case 3: shape = new Vector4(1.0f, 0.78f, 1.42f, 1f); break;
                case 4: shape = new Vector4(1.14f, 0.6f, 1.28f, 1f); break;
                default: shape = new Vector4(1.08f, 0.79f, 1.26f, 1f); break;
            }
            if (Sex == 1)
            {
                shape.x *= 0.97f;
                shape.y *= 0.9f;
            }
            return shape;
        }

        public static Vector3 FacePoint(Vector4 shape, Vector4 sculpt, float latitude, float longitude)
        {
            var height = Mathf.Cos(latitude) * shape.z;
            var jaw = Mathf.Lerp(shape.y, shape.x, Mathf.SmoothStep(0f, 1f, (height + shape.z) / (shape.z * 1.03f)));
            var ring = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(latitude)), shape.w);
            var c = Mathf.Cos(longitude);
            var s = Mathf.Sin(longitude);
            c = Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), shape.w);
            s = Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), shape.w);
            return Sculpt(new Vector3(ring * c * jaw, height, ring * s * 0.79f), sculpt);
        }

        // Caricature anatomy on top of the base egg: brow ridge, eye sockets, cheekbones, muzzle, chin, cranium.
        private static Vector3 Sculpt(Vector3 p, Vector4 sculpt)
        {
            var front = Mathf.Clamp01(-p.z / 0.55f);
            var x2 = p.x * p.x;
            var brow = Mathf.Exp(-Mathf.Pow((p.y - 0.64f) / 0.13f, 2f)) * Mathf.Exp(-Mathf.Pow(p.x / 0.62f, 4f));
            var sockets = 0f;
            for (var side = -1; side <= 1; side += 2)
                sockets += Mathf.Exp(-((p.x - side * 0.43f) * (p.x - side * 0.43f) + (p.y - 0.33f) * (p.y - 0.33f)) / 0.035f);
            var cheek = 0f;
            for (var side = -1; side <= 1; side += 2)
                cheek += Mathf.Exp(-((p.x - side * 0.6f) * (p.x - side * 0.6f) / 0.05f + (p.y + 0.02f) * (p.y + 0.02f) / 0.03f));
            var muzzle = Mathf.Exp(-(x2 / 0.18f + (p.y + 0.6f) * (p.y + 0.6f) / 0.09f));
            var chin = Mathf.Exp(-(x2 / (0.1f * (1f + sculpt.x)) + (p.y + 1.02f) * (p.y + 1.02f) / (0.045f * (1f + sculpt.x * 0.6f))));
            p.z -= front * (0.075f * (1f + sculpt.z * 1.5f) * brow - 0.065f * sockets + 0.05f * cheek + 0.07f * muzzle + (0.15f + 0.22f * sculpt.x) * chin);
            p.y -= front * chin * (0.05f + 0.16f * sculpt.x);
            p.x *= 1f + 0.05f * cheek + sculpt.y * 0.28f * Mathf.SmoothStep(0f, 1f, (-0.15f - p.y) / 0.8f);
            var upper = Mathf.SmoothStep(0f, 1f, (p.y - 0.3f) / 0.9f);
            p.x *= 1f + sculpt.w * 0.25f * upper;
            p.z *= 1f + sculpt.w * 0.2f * upper;
            p.y += sculpt.w * 0.15f * upper;
            var back = Mathf.Clamp01(p.z / 0.5f) * Mathf.Clamp01((p.y + 0.2f) / 0.8f);
            p.z += back * 0.08f;
            return p;
        }

        public static float HalfWidth(Vector4 shape, Vector4 sculpt, float height)
        {
            var latitude = Mathf.Acos(Mathf.Clamp(height / shape.z, -1f, 1f));
            return FacePoint(shape, sculpt, latitude, 0f).x;
        }
    }
}

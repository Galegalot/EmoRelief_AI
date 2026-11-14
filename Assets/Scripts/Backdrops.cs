using UnityEngine;

namespace EmoRelief
{
    // Simple stylised environments placed behind the character.
    public sealed class Backdrops
    {
        public static readonly string[] Names = { "Park", "Bar", "Bedroom", "Office", "Boxing gym", "Friends" };

        private ArtKit kit;
        private Transform root;
        private readonly System.Collections.Generic.List<Transform> friends = new System.Collections.Generic.List<Transform>();
        private readonly System.Collections.Generic.List<Vector3> friendBase = new System.Collections.Generic.List<Vector3>();
        private readonly System.Collections.Generic.List<Transform> friendArms = new System.Collections.Generic.List<Transform>();

        public void Build(int index, Transform parent, Camera view)
        {
            Clear();
            kit = new ArtKit { Outline = 1.3f };
            root = ArtKit.Group("Backdrop " + Names[index], parent);
            switch (index)
            {
                case 1: Bar(view); break;
                case 2: Bedroom(view); break;
                case 3: Office(view); break;
                case 4: Gym(view); break;
                case 5: Friends(view); break;
                default: Park(view); break;
            }
        }

        // Friends bounce and wave their arms while cheering.
        public void Tick(float clock)
        {
            for (var index = 0; index < friends.Count; index++)
                friends[index].localPosition = friendBase[index] + Vector3.up * Mathf.Abs(Mathf.Sin(clock * 5.5f + index * 1.3f)) * 0.35f;
            for (var index = 0; index < friendArms.Count; index++)
            {
                var side = index % 2 == 0 ? -1f : 1f;
                friendArms[index].localRotation = Quaternion.Euler(0f, 0f, -side * (25f + Mathf.Sin(clock * 11f + index * 0.9f) * 22f));
            }
        }

        private void Friends(Camera view)
        {
            Ambient(view, new Color(0.98f, 0.72f, 0.5f), new Color(0.78f, 0.7f, 0.66f));
            Room(L("Party wall", 0.98f, 0.8f, 0.55f), L("Party floor", 0.55f, 0.36f, 0.22f, 0.4f));
            var party = new[] { U("Flag", 1f, 0.25f, 0.35f), U("Flag", 0.25f, 0.75f, 1f), U("Flag", 1f, 0.85f, 0.2f), U("Flag", 0.45f, 0.9f, 0.35f), U("Flag", 0.75f, 0.4f, 1f) };
            for (var flag = 0; flag < 22; flag++)
            {
                var x = -7.5f + flag * 0.72f;
                ArtKit.Box("Bunting", root, new Vector3(x, 4.4f - Mathf.Sin(flag / 21f * Mathf.PI) * 0.6f, 6.9f), new Vector3(0.38f, 0.38f, 0.02f), party[flag % party.Length], new Vector3(0f, 0f, 45f));
            }
            foreach (var spot in new[] { new Vector3(-5.6f, 2.6f, 6.6f), new Vector3(-5f, 3.2f, 6.7f), new Vector3(5.4f, 2.8f, 6.6f), new Vector3(6f, 3.4f, 6.7f), new Vector3(0.4f, 4.2f, 6.8f) })
            {
                var color = party[Mathf.Abs((int)(spot.x * 3f)) % party.Length];
                ArtKit.Ball("Balloon", root, spot, new Vector3(0.45f, 0.55f, 0.45f), color);
                ArtKit.Rod("String", root, spot + new Vector3(0f, -1.3f, 0f), 0.01f, 2f, color);
            }
            ArtKit.Box("Couch", root, new Vector3(0f, -1.7f, 6.4f), new Vector3(7f, 1.1f, 1.2f), L("Couch", 0.3f, 0.45f, 0.8f, 0.3f));
            ArtKit.Box("Couch back", root, new Vector3(0f, -0.7f, 6.9f), new Vector3(7f, 1.4f, 0.4f), L("Couch", 0.3f, 0.45f, 0.8f, 0.3f));
            var skins = new[] { L("Friend skin", 0.95f, 0.75f, 0.6f), L("Friend skin", 0.6f, 0.4f, 0.26f), L("Friend skin", 0.86f, 0.62f, 0.42f), L("Friend skin", 0.4f, 0.26f, 0.18f) };
            var shirts = new[] { L("Shirt", 0.95f, 0.3f, 0.3f), L("Shirt", 0.3f, 0.8f, 0.5f), L("Shirt", 1f, 0.75f, 0.2f), L("Shirt", 0.6f, 0.35f, 0.95f) };
            var hairs = new[] { L("Friend hair", 0.15f, 0.08f, 0.05f), L("Friend hair", 0.95f, 0.8f, 0.35f), L("Friend hair", 0.08f, 0.05f, 0.05f), L("Friend hair", 0.7f, 0.25f, 0.1f) };
            var pants = L("Jeans", 0.2f, 0.28f, 0.5f);
            var ink = L("Friend ink", 0.06f, 0.04f, 0.06f);
            var mouth = L("Friend mouth", 0.45f, 0.08f, 0.12f);
            var white = L("Friend eye", 1f, 1f, 1f, 0.6f);
            var spots = new[] { new Vector3(-4.6f, -2.3f, 5.2f), new Vector3(-2.4f, -2.3f, 5.8f), new Vector3(2.5f, -2.3f, 5.7f), new Vector3(4.7f, -2.3f, 5.1f) };
            for (var index = 0; index < spots.Length; index++)
            {
                var friend = ArtKit.Group("Friend", root, spots[index], new Vector3(0f, spots[index].x * -3f, 0f));
                friends.Add(friend);
                friendBase.Add(spots[index]);
                var skin = skins[index];
                var shirt = shirts[index];
                ArtKit.Ball("Legs", friend, new Vector3(0f, 0.75f, 0f), new Vector3(0.48f, 0.8f, 0.36f), pants);
                ArtKit.Ball("Body", friend, new Vector3(0f, 1.85f, 0f), new Vector3(0.62f, 0.75f, 0.45f), shirt);
                ArtKit.Ball("Head", friend, new Vector3(0f, 3.05f, 0f), new Vector3(0.52f, 0.56f, 0.48f), skin);
                ArtKit.Ball("Hair", friend, new Vector3(0f, 3.4f, 0.06f), new Vector3(0.56f, 0.32f, 0.5f), hairs[index]);
                foreach (var side in new[] { -1f, 1f })
                {
                    ArtKit.Ball("Eye", friend, new Vector3(side * 0.18f, 3.15f, -0.42f), new Vector3(0.12f, 0.14f, 0.06f), white);
                    ArtKit.Ball("Pupil", friend, new Vector3(side * 0.18f, 3.13f, -0.47f), new Vector3(0.06f, 0.08f, 0.03f), ink);
                    ArtKit.Ball("Brow", friend, new Vector3(side * 0.18f, 3.36f, -0.43f), new Vector3(0.12f, 0.03f, 0.03f), ink, new Vector3(0f, 0f, side * -15f));
                    var arm = ArtKit.Group("Arm", friend, new Vector3(side * 0.55f, 2.3f, 0f));
                    ArtKit.Ball("Arm", arm, new Vector3(0f, 0.5f, 0f), new Vector3(0.15f, 0.55f, 0.15f), shirt);
                    ArtKit.Ball("Hand", arm, new Vector3(0f, 1.05f, 0f), Vector3.one * 0.17f, skin);
                    friendArms.Add(arm);
                }
                ArtKit.Ball("Cheering mouth", friend, new Vector3(0f, 2.82f, -0.44f), new Vector3(0.18f, 0.15f, 0.05f), mouth);
            }
            var sign = ArtKit.Group("Sign", friends[1], new Vector3(0f, 4.4f, -0.2f));
            ArtKit.Box("Sign board", sign, Vector3.zero, new Vector3(1.6f, 0.8f, 0.05f), L("Sign", 1f, 1f, 1f));
            ArtKit.Box("Sign stripe", sign, new Vector3(0f, 0f, -0.04f), new Vector3(1.3f, 0.2f, 0.02f), party[0]);
            ArtKit.Box("Sign stripe", sign, new Vector3(0f, 0.25f, -0.04f), new Vector3(0.9f, 0.12f, 0.02f), party[1]);
            var confetti = new[] { party[0], party[1], party[2], party[3] };
            for (var bit = 0; bit < 40; bit++)
                ArtKit.Box("Confetti", root, new Vector3(-6f + (bit * 37 % 120) * 0.1f, -2.2f, 1.5f + (bit * 13 % 40) * 0.1f), new Vector3(0.12f, 0.02f, 0.08f), confetti[bit % 4], new Vector3(0f, bit * 47f, 0f));
        }

        private Material L(string name, float r, float g, float b, float smooth = 0.25f) => kit.Lit(name, new Color(r, g, b), smooth);
        private Material U(string name, float r, float g, float b) => kit.Unlit(name, new Color(r, g, b));

        private void Ambient(Camera view, Color sky, Color ambient)
        {
            view.backgroundColor = sky;
            RenderSettings.ambientLight = ambient;
        }

        private void Room(Material wall, Material floor)
        {
            ArtKit.Box("Back wall", root, new Vector3(0f, 3f, 7.2f), new Vector3(70f, 16f, 0.2f), wall);
            ArtKit.Box("Floor", root, new Vector3(0f, -2.3f, 3f), new Vector3(70f, 0.1f, 30f), floor);
        }

        private void Park(Camera view)
        {
            Ambient(view, new Color(0.55f, 0.8f, 0.98f), new Color(0.72f, 0.76f, 0.8f));
            var grass = L("Grass", 0.42f, 0.72f, 0.35f);
            var hill = L("Hill", 0.33f, 0.62f, 0.32f);
            var trunk = L("Trunk", 0.45f, 0.29f, 0.17f);
            var leaves = L("Leaves", 0.24f, 0.6f, 0.3f);
            var leavesLight = L("Leaves light", 0.4f, 0.75f, 0.32f);
            var path = L("Path", 0.88f, 0.8f, 0.62f);
            var bench = L("Bench", 0.62f, 0.36f, 0.2f);
            var iron = L("Iron", 0.15f, 0.15f, 0.2f, 0.6f);
            var cloud = U("Cloud", 1f, 1f, 1f);
            ArtKit.Box("Grass", root, new Vector3(0f, -2.3f, 10f), new Vector3(60f, 0.1f, 40f), grass);
            ArtKit.Ball("Hill", root, new Vector3(-9f, -3f, 24f), new Vector3(13f, 5f, 6f), hill);
            ArtKit.Ball("Hill", root, new Vector3(10f, -3.5f, 26f), new Vector3(15f, 6.5f, 6f), hill);
            ArtKit.Box("Path", root, new Vector3(0.6f, -2.24f, 9f), new Vector3(2.6f, 0.02f, 24f), path, new Vector3(0f, 12f, 0f));
            ArtKit.Ball("Sun", root, new Vector3(5f, 7f, 28f), Vector3.one * 1.5f, U("Sun", 1f, 0.9f, 0.4f));
            foreach (var position in new[] { new Vector3(-4f, 5.5f, 22f), new Vector3(3f, 4.6f, 24f), new Vector3(-0.5f, 7.5f, 26f) })
                for (var puff = 0; puff < 3; puff++)
                    ArtKit.Ball("Cloud", root, position + new Vector3(puff * 0.9f - 0.9f, puff == 1 ? 0.3f : 0f, 0f), new Vector3(0.9f, 0.6f, 0.5f), cloud);
            foreach (var spot in new[] { new Vector3(-3.6f, 0f, 4.5f), new Vector3(4f, 0f, 6f), new Vector3(-6.5f, 0f, 10f), new Vector3(7f, 0f, 11f), new Vector3(0.8f, 0f, 15f) })
            {
                var scale = 1f + spot.z * 0.04f;
                ArtKit.Rod("Trunk", root, new Vector3(spot.x, -1.1f * scale, spot.z), 0.22f * scale, 2.4f * scale, trunk);
                ArtKit.Ball("Canopy", root, new Vector3(spot.x, 0.6f * scale, spot.z), new Vector3(1.3f, 1.1f, 1.2f) * scale, leaves);
                ArtKit.Ball("Canopy", root, new Vector3(spot.x - 0.6f * scale, 1.3f * scale, spot.z - 0.3f), new Vector3(0.9f, 0.8f, 0.9f) * scale, leavesLight);
                ArtKit.Ball("Canopy", root, new Vector3(spot.x + 0.7f * scale, 1.1f * scale, spot.z - 0.2f), new Vector3(0.85f, 0.75f, 0.8f) * scale, leaves);
            }
            ArtKit.Box("Bench seat", root, new Vector3(-2.6f, -1.75f, 3.2f), new Vector3(1.9f, 0.1f, 0.55f), bench);
            ArtKit.Box("Bench back", root, new Vector3(-2.6f, -1.3f, 3.45f), new Vector3(1.9f, 0.4f, 0.08f), bench, new Vector3(-10f, 0f, 0f));
            foreach (var x in new[] { -3.4f, -1.8f })
                ArtKit.Box("Bench leg", root, new Vector3(x, -2.02f, 3.2f), new Vector3(0.08f, 0.5f, 0.5f), iron);
            ArtKit.Rod("Lamp post", root, new Vector3(2.7f, -0.4f, 3.6f), 0.06f, 3.8f, iron);
            ArtKit.Ball("Lamp", root, new Vector3(2.7f, 1.6f, 3.6f), new Vector3(0.22f, 0.3f, 0.22f), U("Lamp glow", 1f, 0.95f, 0.7f));
            var petals = new[] { L("Petal", 1f, 0.45f, 0.7f), L("Petal", 1f, 0.85f, 0.3f), L("Petal", 0.7f, 0.5f, 1f) };
            for (var flower = 0; flower < 14; flower++)
                ArtKit.Ball("Flower", root, new Vector3(-4.5f + flower * 0.7f, -2.18f, 2.2f + (flower % 3) * 0.5f), Vector3.one * 0.09f, petals[flower % 3]);
        }

        private void Bar(Camera view)
        {
            Ambient(view, new Color(0.12f, 0.05f, 0.1f), new Color(0.42f, 0.3f, 0.4f));
            Room(L("Bar wall", 0.32f, 0.1f, 0.15f), L("Bar floor", 0.26f, 0.15f, 0.1f));
            var wood = L("Counter wood", 0.45f, 0.24f, 0.12f, 0.45f);
            var top = L("Counter top", 0.2f, 0.1f, 0.06f, 0.8f);
            var brass = L("Brass", 0.8f, 0.6f, 0.25f, 0.8f);
            var cushion = L("Stool cushion", 0.8f, 0.12f, 0.35f, 0.4f);
            ArtKit.Box("Counter", root, new Vector3(0f, -1.5f, 4.6f), new Vector3(9f, 1.6f, 1.2f), wood);
            ArtKit.Box("Counter top", root, new Vector3(0f, -0.66f, 4.5f), new Vector3(9.3f, 0.12f, 1.45f), top);
            ArtKit.Rod("Foot rail", root, new Vector3(0f, -2f, 3.95f), 0.05f, 9f, brass, new Vector3(0f, 0f, 90f));
            var glass = new[] { L("Bottle", 0.15f, 0.5f, 0.25f, 0.9f), L("Bottle", 0.7f, 0.4f, 0.1f, 0.9f), L("Bottle", 0.55f, 0.75f, 0.9f, 0.9f), L("Bottle", 0.6f, 0.1f, 0.15f, 0.9f) };
            for (var shelf = 0; shelf < 2; shelf++)
            {
                var y = 0.6f + shelf * 1.3f;
                ArtKit.Box("Shelf", root, new Vector3(0f, y, 6.8f), new Vector3(8f, 0.08f, 0.6f), wood);
                for (var bottle = 0; bottle < 11; bottle++)
                {
                    var x = -3.6f + bottle * 0.72f;
                    var height = 0.5f + (bottle * 7 + shelf * 3) % 4 * 0.08f;
                    ArtKit.Rod("Bottle", root, new Vector3(x, y + height * 0.5f + 0.04f, 6.75f), 0.13f, height, glass[(bottle + shelf) % glass.Length]);
                    ArtKit.Rod("Neck", root, new Vector3(x, y + height + 0.15f, 6.75f), 0.045f, 0.25f, glass[(bottle + shelf) % glass.Length]);
                }
            }
            var pink = U("Neon pink", 1f, 0.3f, 0.75f);
            var cyan = U("Neon cyan", 0.3f, 0.95f, 1f);
            for (var bulb = 0; bulb < 16; bulb++)
            {
                var angle = bulb * Mathf.PI * 2f / 16f;
                ArtKit.Ball("Neon", root, new Vector3(-2.8f + Mathf.Cos(angle) * 0.8f, 3.8f + Mathf.Sin(angle) * 0.8f, 7f), Vector3.one * 0.09f, pink);
            }
            ArtKit.Box("Neon bar", root, new Vector3(2.8f, 4.1f, 7f), new Vector3(2f, 0.1f, 0.05f), cyan);
            ArtKit.Box("Neon bar", root, new Vector3(2.8f, 3.7f, 7f), new Vector3(1.5f, 0.1f, 0.05f), pink);
            ArtKit.Box("Neon bar", root, new Vector3(2.8f, 3.3f, 7f), new Vector3(2f, 0.1f, 0.05f), cyan);
            var warm = U("Bulb", 1f, 0.8f, 0.45f);
            var shade = L("Shade", 0.15f, 0.12f, 0.1f, 0.6f);
            foreach (var x in new[] { -3.2f, 0f, 3.2f })
            {
                ArtKit.Rod("Cord", root, new Vector3(x, 4.6f, 3.6f), 0.02f, 3.4f, shade);
                ArtKit.Ball("Shade", root, new Vector3(x, 2.95f, 3.6f), new Vector3(0.42f, 0.24f, 0.42f), shade);
                ArtKit.Ball("Bulb", root, new Vector3(x, 2.75f, 3.6f), Vector3.one * 0.16f, warm);
            }
            foreach (var x in new[] { -3.3f, -1.6f, 1.6f, 3.3f })
            {
                ArtKit.Rod("Stool leg", root, new Vector3(x, -1.75f, 3.5f), 0.05f, 1.1f, brass);
                ArtKit.Rod("Stool seat", root, new Vector3(x, -1.15f, 3.5f), 0.36f, 0.14f, cushion);
            }
            var light = new GameObject("Bar glow", typeof(Light)).GetComponent<Light>();
            light.transform.SetParent(root, false);
            light.transform.localPosition = new Vector3(0f, 2.4f, 2f);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.6f, 0.75f);
            light.range = 9f;
            light.intensity = 2.2f;
        }

        private void Bedroom(Camera view)
        {
            Ambient(view, new Color(0.2f, 0.18f, 0.3f), new Color(0.62f, 0.58f, 0.7f));
            var wall = L("Bedroom wall", 0.72f, 0.62f, 0.86f);
            Room(wall, L("Bedroom floor", 0.7f, 0.52f, 0.36f, 0.4f));
            var stripe = L("Wallpaper stripe", 0.78f, 0.69f, 0.9f);
            for (var x = -30; x <= 30; x += 2)
                ArtKit.Box("Stripe", root, new Vector3(x, 3f, 7.08f), new Vector3(0.6f, 12f, 0.02f), stripe);
            ArtKit.Ball("Rug", root, new Vector3(0.4f, -2.24f, 3.2f), new Vector3(3.2f, 0.02f, 1.6f), L("Rug", 0.95f, 0.5f, 0.72f));
            var frame = L("Bed frame", 0.52f, 0.33f, 0.2f, 0.4f);
            ArtKit.Box("Bed frame", root, new Vector3(-3.4f, -1.75f, 5f), new Vector3(3.6f, 0.9f, 4f), frame);
            ArtKit.Box("Mattress", root, new Vector3(-3.4f, -1.12f, 5f), new Vector3(3.4f, 0.4f, 3.8f), L("Sheet", 0.97f, 0.96f, 1f));
            ArtKit.Box("Blanket", root, new Vector3(-3.4f, -0.94f, 4.5f), new Vector3(3.5f, 0.3f, 2.8f), L("Blanket", 0.48f, 0.26f, 0.7f));
            ArtKit.Ball("Pillow", root, new Vector3(-3.4f, -0.72f, 6.3f), new Vector3(1.0f, 0.25f, 0.45f), L("Pillow", 1f, 0.92f, 0.96f));
            ArtKit.Box("Headboard", root, new Vector3(-3.4f, -0.4f, 7f), new Vector3(3.8f, 2.6f, 0.2f), frame);
            var white = L("Window frame", 0.97f, 0.97f, 0.97f);
            ArtKit.Box("Window frame", root, new Vector3(3.2f, 1.8f, 7.05f), new Vector3(2.7f, 2.7f, 0.08f), white);
            ArtKit.Box("Night sky", root, new Vector3(3.2f, 1.8f, 6.99f), new Vector3(2.35f, 2.35f, 0.04f), U("Night", 0.12f, 0.15f, 0.38f));
            ArtKit.Ball("Moon", root, new Vector3(3.7f, 2.4f, 6.95f), new Vector3(0.3f, 0.3f, 0.02f), U("Moon", 1f, 0.97f, 0.8f));
            ArtKit.Box("Mullion", root, new Vector3(3.2f, 1.8f, 6.94f), new Vector3(0.08f, 2.35f, 0.04f), white);
            ArtKit.Box("Mullion", root, new Vector3(3.2f, 1.8f, 6.94f), new Vector3(2.35f, 0.08f, 0.04f), white);
            ArtKit.Box("Nightstand", root, new Vector3(1.2f, -1.75f, 6.4f), new Vector3(1.1f, 1.0f, 0.8f), frame);
            ArtKit.Rod("Lamp stem", root, new Vector3(1.2f, -1.0f, 6.4f), 0.05f, 0.6f, white);
            ArtKit.Ball("Lamp shade", root, new Vector3(1.2f, -0.6f, 6.4f), new Vector3(0.38f, 0.32f, 0.38f), U("Lamp shade", 1f, 0.85f, 0.6f));
            ArtKit.Box("Poster", root, new Vector3(-0.8f, 2.8f, 7.05f), new Vector3(1.4f, 1.9f, 0.04f), L("Poster", 0.95f, 0.3f, 0.6f));
            ArtKit.Ball("Poster art", root, new Vector3(-0.8f, 2.9f, 7.0f), new Vector3(0.45f, 0.45f, 0.02f), L("Poster art", 1f, 0.85f, 0.35f));
            ArtKit.Box("Poster", root, new Vector3(-3.6f, 2.6f, 7.05f), new Vector3(1.6f, 1.1f, 0.04f), L("Poster 2", 0.25f, 0.7f, 0.85f));
        }

        private void Office(Camera view)
        {
            Ambient(view, new Color(0.75f, 0.8f, 0.86f), new Color(0.7f, 0.72f, 0.76f));
            Room(L("Office wall", 0.82f, 0.85f, 0.88f), L("Carpet", 0.45f, 0.47f, 0.54f, 0.1f));
            ArtKit.Box("Sky", root, new Vector3(0f, 2.4f, 7.05f), new Vector3(9f, 4.4f, 0.04f), U("Sky", 0.62f, 0.82f, 0.97f));
            var city = new[] { U("Tower", 0.3f, 0.38f, 0.52f), U("Tower", 0.4f, 0.48f, 0.62f) };
            for (var tower = 0; tower < 12; tower++)
            {
                var height = 1.2f + (tower * 37 % 11) * 0.22f;
                ArtKit.Box("Tower", root, new Vector3(-4.1f + tower * 0.75f, 0.2f + height * 0.5f, 7f), new Vector3(0.62f, height, 0.03f), city[tower % 2]);
            }
            var frame = L("Window frame", 0.25f, 0.27f, 0.32f, 0.6f);
            foreach (var x in new[] { -4.5f, -1.5f, 1.5f, 4.5f })
                ArtKit.Box("Mullion", root, new Vector3(x, 2.4f, 6.95f), new Vector3(0.12f, 4.5f, 0.1f), frame);
            ArtKit.Box("Sill", root, new Vector3(0f, 0.15f, 6.95f), new Vector3(9.2f, 0.15f, 0.2f), frame);
            ArtKit.Box("Head", root, new Vector3(0f, 4.65f, 6.95f), new Vector3(9.2f, 0.15f, 0.2f), frame);
            var blind = L("Blind", 0.95f, 0.95f, 0.93f);
            for (var slat = 0; slat < 7; slat++)
                ArtKit.Box("Blind slat", root, new Vector3(0f, 4.45f - slat * 0.14f, 6.9f), new Vector3(9f, 0.08f, 0.04f), blind, new Vector3(30f, 0f, 0f));
            var desk = L("Desk", 0.62f, 0.45f, 0.3f, 0.5f);
            ArtKit.Box("Desk top", root, new Vector3(2.8f, -1.15f, 4f), new Vector3(3.4f, 0.12f, 1.6f), desk);
            foreach (var x in new[] { 1.3f, 4.3f })
                ArtKit.Box("Desk leg", root, new Vector3(x, -1.72f, 4f), new Vector3(0.1f, 1.05f, 1.4f), frame);
            ArtKit.Box("Monitor", root, new Vector3(2.8f, -0.45f, 4.4f), new Vector3(1.5f, 0.92f, 0.08f), L("Monitor", 0.08f, 0.08f, 0.1f, 0.8f));
            ArtKit.Box("Screen", root, new Vector3(2.8f, -0.45f, 4.35f), new Vector3(1.36f, 0.78f, 0.02f), U("Screen", 0.35f, 0.6f, 0.95f));
            ArtKit.Box("Stand", root, new Vector3(2.8f, -0.95f, 4.45f), new Vector3(0.12f, 0.3f, 0.08f), frame);
            ArtKit.Rod("Mug", root, new Vector3(1.8f, -0.95f, 3.7f), 0.12f, 0.28f, L("Mug", 0.9f, 0.25f, 0.55f));
            ArtKit.Rod("Pot", root, new Vector3(-3.4f, -1.85f, 4.2f), 0.38f, 0.8f, L("Pot", 0.78f, 0.42f, 0.28f));
            var leaf = L("Plant", 0.2f, 0.58f, 0.3f);
            for (var index = 0; index < 5; index++)
                ArtKit.Ball("Leaf", root, new Vector3(-3.4f + (index - 2) * 0.25f, -1.1f + (index % 2) * 0.35f, 4.2f), new Vector3(0.22f, 0.55f, 0.12f), leaf, new Vector3(0f, 0f, (index - 2) * 18f));
            ArtKit.Box("Cabinet", root, new Vector3(-2f, -1.4f, 6.3f), new Vector3(0.95f, 1.8f, 0.8f), L("Cabinet", 0.55f, 0.58f, 0.62f, 0.5f));
            ArtKit.Rod("Clock", root, new Vector3(-3.4f, 3.3f, 7.05f), 0.45f, 0.06f, L("Clock", 1f, 1f, 1f), new Vector3(90f, 0f, 0f));
            ArtKit.Box("Clock hand", root, new Vector3(-3.4f, 3.45f, 6.99f), new Vector3(0.04f, 0.3f, 0.02f), frame);
            ArtKit.Box("Clock hand", root, new Vector3(-3.3f, 3.3f, 6.99f), new Vector3(0.22f, 0.04f, 0.02f), frame);
        }

        private void Gym(Camera view)
        {
            Ambient(view, new Color(0.07f, 0.05f, 0.12f), new Color(0.42f, 0.38f, 0.52f));
            ArtKit.Box("Ring canvas", root, new Vector3(0f, -2.3f, 3f), new Vector3(30f, 0.1f, 14f), L("Canvas", 0.3f, 0.42f, 0.72f));
            ArtKit.Box("Arena floor", root, new Vector3(0f, -2.6f, 15f), new Vector3(80f, 0.1f, 24f), L("Arena", 0.1f, 0.08f, 0.14f));
            var post = L("Post", 0.85f, 0.85f, 0.9f, 0.7f);
            var pad = L("Corner pad", 0.9f, 0.15f, 0.3f);
            foreach (var x in new[] { -9f, 9f })
            {
                ArtKit.Rod("Post", root, new Vector3(x, -0.8f, 4.5f), 0.14f, 3f, post);
                ArtKit.Box("Pad", root, new Vector3(x, -0.4f, 4.4f), new Vector3(0.4f, 1.8f, 0.4f), pad);
            }
            var ropes = new[] { L("Rope", 0.9f, 0.15f, 0.2f, 0.5f), L("Rope", 0.95f, 0.95f, 0.95f, 0.5f), L("Rope", 0.2f, 0.35f, 0.9f, 0.5f) };
            for (var rope = 0; rope < 3; rope++)
                ArtKit.Rod("Rope", root, new Vector3(0f, -1.3f + rope * 0.65f, 4.5f), 0.06f, 18f, ropes[rope], new Vector3(0f, 0f, 90f));
            var crowd = new[] { L("Crowd", 0.3f, 0.08f, 0.1f), L("Crowd", 0.4f, 0.12f, 0.1f), L("Crowd", 0.2f, 0.06f, 0.08f) };
            for (var row = 0; row < 3; row++)
                for (var seat = 0; seat < 28; seat++)
                {
                    var x = -16.2f + seat * 1.2f + row * 0.4f;
                    var y = -1.6f + row * 0.9f;
                    var z = 10f + row * 1.6f;
                    ArtKit.Ball("Fan body", root, new Vector3(x, y, z), new Vector3(0.45f, 0.5f, 0.3f), crowd[(seat + row) % 3]);
                    ArtKit.Ball("Fan head", root, new Vector3(x, y + 0.7f, z), Vector3.one * 0.25f, crowd[(seat + row + 1) % 3]);
                }
            ArtKit.Box("Banner", root, new Vector3(0f, 4.4f, 14f), new Vector3(14f, 1.4f, 0.1f), U("Banner", 0.9f, 0.18f, 0.6f));
            ArtKit.Box("Banner stripe", root, new Vector3(0f, 4.4f, 13.9f), new Vector3(12.5f, 0.24f, 0.05f), U("Banner stripe", 1f, 0.85f, 0.3f));
            var bulb = U("Spotlight", 1f, 0.97f, 0.85f);
            for (var lamp = 0; lamp < 9; lamp++)
                ArtKit.Ball("Spotlight", root, new Vector3(-8f + lamp * 2f, 6.5f, 8f), new Vector3(0.35f, 0.2f, 0.35f), bulb);
            ArtKit.Rod("Chain", root, new Vector3(6.5f, 2.4f, 6.5f), 0.03f, 2f, post);
            ArtKit.Rod("Punching bag", root, new Vector3(6.5f, 0.4f, 6.5f), 0.5f, 2.2f, L("Bag", 0.75f, 0.1f, 0.15f, 0.5f));
        }

        public void Clear()
        {
            if (root != null) ArtKit.Destroy(root.gameObject);
            root = null;
            friends.Clear();
            friendBase.Clear();
            friendArms.Clear();
            kit?.Dispose();
            kit = null;
        }
    }
}

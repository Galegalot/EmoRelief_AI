using System;
using UnityEngine;

namespace EmoRelief
{
    // First-person arms (hoodie sleeves, five-finger hands, boxing gloves) that animate each tool.
    public sealed class PlayerRig
    {
        public static readonly Vector3 CameraPosition = new Vector3(0f, 0.15f, -6f);
        public const float FieldOfView = 40f;
        public static float Aspect = 16f / 9f;
        private const float UpperLength = 2.2f;
        private const float ForeLength = 2.1f;

        public ToolKind Tool { get; private set; }
        public FaceCharacter Character;
        public Action<ToolKind, HitPoint, float, Vector3> Impact;
        public Action<ToolKind, HitPoint, float, Vector3, Vector3> Release;
        public float Pull;
        public Vector3 Aim;

        private sealed class Arm
        {
            public float Side;
            public Hose Limb;
            public Transform Wrist;
            public HandModel Hand;
            public Transform Glove;
            public Transform Poop;
            public Vector3 Shoulder;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Lunge;
            public float Age = 10f;
            public HitPoint Hit;
            public float Power = 1f;
            public bool Fired = true;
        }

        private readonly Arm[] arms = new Arm[2];
        private readonly ArtKit kit = new ArtKit();
        private readonly Transform root;
        private readonly Transform egg;
        private readonly Transform dart;
        private readonly Vector3 eggScale;
        private readonly Transform bat;
        private readonly Transform sling;
        private readonly Transform slingLeft;
        private readonly Transform slingRight;
        private readonly Transform pouch;
        private readonly Transform bandLeft;
        private readonly Transform bandRight;
        private readonly Transform foot;
        private readonly Transform footFlies;
        private readonly Transform footStink;
        public float Showcase;
        public float SlapSpeed = 1f;
        private float footAge = 10f;
        private HitPoint footHit;
        private bool footFired = true;

        private static float Bump(float t, float center, float width) => Mathf.Exp(-(t - center) * (t - center) / (width * width));
        private float clock;
        private float snap = 10f;
        private float snapPull;
        private int nextArm = 1;

        public PlayerRig(Transform parent, PropKit props)
        {
            root = ArtKit.Group("Player arms", parent);
            var hoodie = kit.Lit("Sweatband", new Color(0.9f, 0.12f, 0.2f), 0.3f);
            var skin = kit.Lit("Player skin", new Color(0.9f, 0.66f, 0.52f), 0.55f);
            var nail = kit.Lit("Nail", new Color(0.98f, 0.82f, 0.78f), 0.8f);
            var gloveLeather = props.GloveLeather("Player glove", new Color(0.86f, 0.06f, 0.12f));
            var gloveTrim = kit.Lit("Glove trim", new Color(1f, 1f, 1f), 0.5f);
            for (var index = 0; index < 2; index++)
            {
                var side = index == 0 ? -1f : 1f;
                var arm = new Arm { Side = side, Shoulder = new Vector3(side * 1.25f, -1.6f, -5.75f) };
                // Popeye profile: shoulder, bulging bicep, pinched elbow, huge forearm, thin wrist.
                arm.Limb = new Hose("Muscle arm", root, skin, t => (Mathf.Lerp(0.38f, 0.17f, t) + 0.14f * Bump(t, 0.27f, 0.12f) + 0.2f * Bump(t, 0.72f, 0.13f) - 0.05f * Bump(t, 0.5f, 0.05f)) * 0.95f);
                arm.Wrist = ArtKit.Group(index == 0 ? "Left wrist" : "Right wrist", root);
                ArtKit.Rod("Sweatband", arm.Wrist, new Vector3(0f, 0f, -0.14f), 0.215f, 0.16f, hoodie, new Vector3(90f, 0f, 0f));
                arm.Hand = new HandModel(index == 0 ? "Left hand" : "Right hand", arm.Wrist, side, skin, nail, kit);
                arm.Glove = props.Glove(arm.Wrist, side, gloveLeather, gloveTrim);
                arm.Poop = props.Poop(arm.Wrist);
                arm.Poop.localPosition = new Vector3(0f, -0.12f, 0.36f);
                arm.Poop.localRotation = Quaternion.Euler(0f, 0f, 180f);
                arm.Poop.localScale = Vector3.one * 1.4f;
                arm.Poop.gameObject.SetActive(false);
                arms[index] = arm;
            }
            foot = props.Foot(root);
            foot.localScale = Vector3.one * 1.25f;
            footFlies = foot.Find("Foot flies");
            footStink = foot.Find("Foot stink");
            egg = props.Egg(arms[1].Wrist);
            egg.localPosition = new Vector3(0f, -0.25f, 0.42f);
            egg.localScale *= 1.3f;
            eggScale = egg.localScale;
            dart = props.Dart(arms[1].Wrist);
            dart.localPosition = new Vector3(-0.08f, -0.06f, 0.78f);
            bat = props.Bat(root);
            bat.localScale = Vector3.one * 1.15f;
            sling = props.Slingshot(root, out slingLeft, out slingRight);
            sling.localScale = Vector3.one * 1.2f;
            pouch = ArtKit.Ball("Pouch", root, Vector3.zero, new Vector3(0.12f, 0.1f, 0.06f), props.Leather);
            bandLeft = ArtKit.Shape(PrimitiveType.Capsule, "Band", root, Vector3.zero, Vector3.one, props.Band);
            bandRight = ArtKit.Shape(PrimitiveType.Capsule, "Band", root, Vector3.zero, Vector3.one, props.Band);
            SetTool(ToolKind.Punch);
        }

        public static Vector3 View(float x, float y, float depth)
        {
            var tan = Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad);
            return CameraPosition + new Vector3((x - 0.5f) * 2f * depth * tan * Aspect, (y - 0.5f) * 2f * depth * tan, depth);
        }

        public void SetVisible(bool visible) => root.gameObject.SetActive(visible);

        public void SetTool(ToolKind tool)
        {
            Tool = tool;
            Showcase = 0f;
            foreach (var arm in arms)
            {
                arm.Age = 10f;
                arm.Fired = true;
                arm.Lunge = Vector3.zero;
                arm.Glove.gameObject.SetActive(tool == ToolKind.Punch);
                arm.Hand.SetActive(tool != ToolKind.Punch);
                arm.Poop.gameObject.SetActive(tool == ToolKind.Poop);
            }
            footAge = 10f;
            footFired = true;
            foot.gameObject.SetActive(tool == ToolKind.Feet);
            Pull = 0f;
            snap = 10f;
            egg.gameObject.SetActive(tool == ToolKind.Egg);
            dart.gameObject.SetActive(tool == ToolKind.Dart);
            bat.gameObject.SetActive(tool == ToolKind.Bat);
            var slingshot = tool == ToolKind.Slingshot;
            sling.gameObject.SetActive(slingshot);
            pouch.gameObject.SetActive(slingshot);
            bandLeft.gameObject.SetActive(slingshot);
            bandRight.gameObject.SetActive(slingshot);
            var left = arms[0].Hand;
            var right = arms[1].Hand;
            switch (tool)
            {
                case ToolKind.Slap:
                case ToolKind.Poop: left.Pose(0.08f, 0.15f, 0.9f); right.Pose(0.08f, 0.15f, 0.9f); break;
                case ToolKind.Feet: left.Pose(0.9f, 0.8f, 0f); right.Pose(0.9f, 0.8f, 0f); break;
                case ToolKind.Egg: left.Pose(0.55f, 0.45f, 0.2f); right.Pose(0.3f, 0.35f, 0.25f); break;
                case ToolKind.Dart: left.Pose(0.55f, 0.45f, 0.2f); right.Pose(0.8f, 0.7f, 0f, 0.42f); break;
                case ToolKind.Slingshot: left.Pose(0.95f, 0.85f, 0f); right.Pose(0.75f, 0.8f, 0f, 0.5f); break;
                case ToolKind.Bat: left.Pose(0.88f, 0.85f, 0f); right.Pose(0.88f, 0.85f, 0f); break;
            }
            Update(0f);
        }

        public bool Ready
        {
            get
            {
                switch (Tool)
                {
                    case ToolKind.Punch: return arms[0].Age > 0.17f || arms[1].Age > 0.17f;
                    case ToolKind.Slap:
                    case ToolKind.Poop: return arms[0].Age > 0.34f && arms[1].Age > 0.34f;
                    case ToolKind.Feet: return footAge > 0.32f;
                    case ToolKind.Bat: return arms[1].Age > 0.55f;
                    case ToolKind.Slingshot: return snap > 0.18f;
                    default: return arms[1].Age > 0.4f;
                }
            }
        }

        public bool Begin(HitPoint hit, float power, bool force = false)
        {
            if ((!Ready && !force) || Tool == ToolKind.Slingshot) return false;
            if (Tool == ToolKind.Feet)
            {
                footAge = 0f;
                footHit = hit;
                footFired = false;
                return true;
            }
            Arm arm;
            if (Tool == ToolKind.Punch)
            {
                var preferred = hit.Local.x < -0.25f ? 0 : hit.Local.x > 0.25f ? 1 : nextArm;
                if (arms[preferred].Age < 0.17f) preferred = 1 - preferred;
                arm = arms[preferred];
                nextArm = 1 - preferred;
            }
            else if (Tool == ToolKind.Slap || Tool == ToolKind.Poop) arm = arms[hit.Local.x < 0f ? 0 : 1];
            else arm = arms[1];
            arm.Age = 0f;
            arm.Hit = hit;
            arm.Power = power;
            arm.Fired = false;
            return true;
        }

        public void FireSling(HitPoint hit, float power, Vector3 missTarget)
        {
            if (Tool != ToolKind.Slingshot) return;
            snap = 0f;
            snapPull = Mathf.Max(Pull, 0.2f);
            Pull = 0f;
            Release?.Invoke(ToolKind.Slingshot, hit, power, ForkCenter(), missTarget);
        }

        private Vector3 Target(HitPoint hit) => Character != null && Character.Root != null ? Character.Root.TransformPoint(hit.Local) : new Vector3(0f, 0.4f, -1f);

        private Vector3 ForkCenter() => sling.TransformPoint(new Vector3(0f, 0.39f, 0f));

        public void Update(float deltaTime)
        {
            clock += deltaTime;
            foreach (var arm in arms) arm.Age += deltaTime * (Tool == ToolKind.Poop ? SlapSpeed : 1f);
            snap += deltaTime;
            switch (Tool)
            {
                case ToolKind.Punch: PosePunch(); break;
                case ToolKind.Slap: PoseSlap(); break;
                case ToolKind.Poop:
                    PoseSlap();
                    if (Showcase > 0f) PoseShowcase();
                    foreach (var arm in arms) arm.Poop.GetChild(arm.Poop.childCount - 1).localRotation = Quaternion.Euler(0f, clock * 260f, 0f);
                    break;
                case ToolKind.Feet:
                    PoseFeet(deltaTime);
                    footFlies.localRotation = Quaternion.Euler(0f, 0f, clock * 140f);
                    footFlies.localScale = Vector3.one * (1f + Mathf.Sin(clock * 3f) * 0.12f);
                    footStink.localPosition = new Vector3(Mathf.Sin(clock * 2f) * 0.05f, 0.95f + Mathf.Repeat(clock * 0.3f, 0.2f), -0.15f);
                    break;
                case ToolKind.Egg: PoseThrow(egg, 0.14f, 0.22f, 0.32f, 0.55f, new Vector3(0.45f, 0.9f, -0.8f)); break;
                case ToolKind.Dart: PoseThrow(dart, 0.1f, 0.16f, 0.24f, 0.42f, new Vector3(0.12f, 0.3f, -0.6f)); break;
                case ToolKind.Slingshot: PoseSling(); break;
                case ToolKind.Bat: PoseBat(); break;
            }
            foreach (var arm in arms) Solve(arm);
        }

        private Vector3 Bob(Arm arm) => new Vector3(0f, Mathf.Sin(clock * 2.2f + arm.Side) * 0.025f, 0f);

        private Vector3 Low(Arm arm) => View(0.5f + arm.Side * 0.36f, -0.12f, 2.4f);

        private Quaternion Natural(Arm arm, Vector3 wrist)
        {
            var elbow = Elbow(arm.Shoulder + arm.Lunge, wrist, arm.Side);
            return Quaternion.LookRotation(wrist - elbow, Vector3.up);
        }

        private void Relax(Arm arm)
        {
            arm.Position = Low(arm) + Bob(arm);
            arm.Lunge = Vector3.zero;
            arm.Rotation = Natural(arm, arm.Position) * Quaternion.Euler(0f, 0f, arm.Side * 60f);
        }

        private void PosePunch()
        {
            foreach (var arm in arms)
            {
                var rest = View(0.5f + arm.Side * 0.2f, 0.07f, 3.1f) + Bob(arm);
                var t = arm.Age;
                var reach = 0f;
                if (t < 0.38f)
                {
                    var target = Target(arm.Hit);
                    var toward = (target - CameraPosition).normalized;
                    var strike = target - toward * 0.8f + Vector3.down * 0.05f;
                    var back = rest + new Vector3(0f, -0.08f, -0.25f);
                    if (t < 0.05f) arm.Position = Vector3.Lerp(rest, back, Smooth(t / 0.05f));
                    else if (t < 0.12f) { reach = Smooth((t - 0.05f) / 0.07f); arm.Position = Vector3.Lerp(back, strike, reach); }
                    else if (t < 0.17f) { reach = 1f; arm.Position = strike; }
                    else { reach = 1f - Smooth((t - 0.17f) / 0.21f); arm.Position = Vector3.Lerp(rest, strike, reach); }
                    if (!arm.Fired && t >= 0.12f)
                    {
                        arm.Fired = true;
                        Impact?.Invoke(ToolKind.Punch, arm.Hit, arm.Power, toward);
                    }
                }
                else arm.Position = rest;
                arm.Lunge = new Vector3(-arm.Side * 0.3f, 0.35f, 1.6f) * reach;
                arm.Rotation = Natural(arm, arm.Position) * Quaternion.Euler(-20f * (1f - reach), 0f, -arm.Side * 25f * (1f - reach));
            }
        }

        // Ultimate intro: right hand raised front and centre, palm up, presenting the poop.
        private void PoseShowcase()
        {
            var arm = arms[1];
            var show = View(0.55f, 0.3f, 2.9f) + new Vector3(0f, Mathf.Sin(clock * 6f) * 0.03f, 0f);
            arm.Position = Vector3.Lerp(arm.Position, show, Showcase);
            arm.Lunge = Vector3.Lerp(arm.Lunge, new Vector3(-0.3f, 0.4f, 1.2f), Showcase);
            var palmUp = Quaternion.LookRotation(new Vector3(-0.1f, 0.75f, 0.65f), new Vector3(0f, -0.65f, 0.75f)) * Quaternion.Euler(0f, 0f, Mathf.Sin(clock * 4f) * 6f);
            arm.Rotation = Quaternion.Slerp(arm.Rotation, palmUp, Showcase);
        }

        private void PoseSlap()
        {
            foreach (var arm in arms)
            {
                var side = arm.Side;
                var rest = View(0.5f + side * 0.3f, 0.0f, 3.2f) + Bob(arm);
                var t = arm.Age;
                arm.Lunge = Vector3.zero;
                if (t < 0.5f)
                {
                    var target = Target(arm.Hit);
                    var wind = target + new Vector3(side * 2.2f, 0.4f, -1.3f);
                    var contact = target + new Vector3(side * 0.12f, -0.05f, -0.3f);
                    var follow = target + new Vector3(-side * 1.8f, -0.6f, -1.3f);
                    float swing;
                    if (t < 0.1f) { swing = Smooth(t / 0.1f); arm.Position = Vector3.Lerp(rest, wind, swing); }
                    else if (t < 0.16f) { swing = 1f; arm.Position = Vector3.Lerp(wind, contact, Smooth((t - 0.1f) / 0.06f)); }
                    else if (t < 0.26f) { swing = 1f; arm.Position = Vector3.Lerp(contact, follow, Smooth((t - 0.16f) / 0.1f)); }
                    else { swing = 1f - Smooth((t - 0.26f) / 0.24f); arm.Position = Vector3.Lerp(rest, follow, swing); }
                    arm.Lunge = new Vector3(0f, 0.3f, 1.4f) * swing;
                    var slapping = Quaternion.LookRotation(new Vector3(-side * 0.3f, 0.75f, 0.6f), new Vector3(side, 0f, 0f));
                    arm.Rotation = Quaternion.Slerp(Natural(arm, arm.Position), slapping, swing);
                    if (!arm.Fired && t >= 0.16f)
                    {
                        arm.Fired = true;
                        Impact?.Invoke(Tool, arm.Hit, arm.Power, new Vector3(-side, 0f, 0.35f));
                    }
                    continue;
                }
                arm.Position = rest;
                arm.Rotation = Natural(arm, rest) * Quaternion.Euler(0f, 0f, side * 35f);
            }
        }

        private void PoseThrow(Transform item, float wind, float release, float follow, float end, Vector3 windOffset)
        {
            Relax(arms[0]);
            var arm = arms[1];
            var rest = View(0.68f, 0.06f, 3f) + Bob(arm);
            var t = arm.Age;
            var thrown = rest + new Vector3(-0.2f, 0.55f, 1.1f);
            var after = rest + new Vector3(-0.25f, -0.2f, 0.7f);
            var toward = (Target(arm.Hit) - CameraPosition).normalized;
            if (t < wind) arm.Position = Vector3.Lerp(rest, rest + windOffset, Smooth(t / wind));
            else if (t < release) arm.Position = Vector3.Lerp(rest + windOffset, thrown, Smooth((t - wind) / (release - wind)));
            else if (t < follow) arm.Position = Vector3.Lerp(thrown, after, Smooth((t - release) / (follow - release)));
            else if (t < end) arm.Position = Vector3.Lerp(after, rest, Smooth((t - follow) / (end - follow)));
            else arm.Position = rest;
            arm.Lunge = t < end ? new Vector3(0f, 0.2f, 0.8f) * Mathf.Sin(Mathf.Clamp01(t / end) * Mathf.PI) : Vector3.zero;
            var aim = Quaternion.LookRotation(toward, Vector3.up);
            var swing = t < end ? Mathf.Sin(Mathf.Clamp01(t / end) * Mathf.PI) : 0f;
            var palmUp = Natural(arm, arm.Position) * Quaternion.Euler(-15f, 0f, 165f);
            arm.Rotation = Quaternion.Slerp(palmUp, aim, swing);
            if (!arm.Fired && t >= release)
            {
                arm.Fired = true;
                Release?.Invoke(Tool, arm.Hit, arm.Power, item.position, Vector3.zero);
            }
            var hidden = t >= release && t < follow + 0.1f;
            item.gameObject.SetActive(!hidden);
            var grow = Mathf.Clamp01((t - follow - 0.1f) / 0.12f);
            item.localScale = (item == egg ? eggScale : Vector3.one) * (t < release ? 1f : grow);
            if (Tool == ToolKind.Dart) item.localRotation = Quaternion.Euler(-6f, 4f, 0f);
        }

        // Grip whose closed fingers wrap an axis running along the hand's local X.
        private static Quaternion Grip(Vector3 axis, Vector3 knuckles)
        {
            var forward = Vector3.ProjectOnPlane(knuckles, axis).normalized;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.ProjectOnPlane(Vector3.forward, axis).normalized;
            return Quaternion.LookRotation(forward, Vector3.Cross(forward, axis));
        }

        private static readonly Vector3 GripPoint = new Vector3(0f, -0.14f, 0.5f);

        private void PoseSling()
        {
            var left = arms[0];
            var right = arms[1];
            var frameBase = View(0.46f, 0.22f, 3f) + Bob(left);
            var aimDirection = (Aim - frameBase).normalized;
            if (aimDirection.sqrMagnitude < 0.5f) aimDirection = Vector3.forward;
            var frameRotation = Quaternion.LookRotation(aimDirection, Vector3.up) * Quaternion.Euler(-12f, 0f, 0f);
            sling.position = frameBase;
            sling.rotation = frameRotation;
            var fork = ForkCenter();
            left.Lunge = Vector3.zero;
            left.Rotation = Grip(frameRotation * Vector3.up, aimDirection);
            left.Position = sling.TransformPoint(new Vector3(0f, -0.25f, 0f)) - left.Rotation * GripPoint;

            Vector3 pouchPosition;
            if (snap < 0.25f)
            {
                var pulled = fork - aimDirection * (0.15f + snapPull * 1.2f) + Vector3.down * 0.1f * snapPull;
                var over = fork + aimDirection * 0.25f;
                pouchPosition = snap < 0.05f ? Vector3.Lerp(pulled, over, snap / 0.05f) : Vector3.Lerp(over, fork - aimDirection * 0.12f, Smooth((snap - 0.05f) / 0.2f));
            }
            else pouchPosition = fork - aimDirection * (0.12f + Pull * 1.2f) + Vector3.down * 0.1f * Pull;
            pouch.position = pouchPosition;
            pouch.rotation = frameRotation;
            ArtKit.Span(bandLeft, slingLeft.position, pouchPosition, 0.025f);
            ArtKit.Span(bandRight, slingRight.position, pouchPosition, 0.025f);
            right.Lunge = Vector3.zero;
            right.Rotation = Quaternion.LookRotation(aimDirection, Vector3.up) * Quaternion.Euler(0f, 0f, 25f);
            var pinch = snap < 0.25f ? Vector3.Lerp(pouchPosition, fork - aimDirection * 0.7f, 0.6f) : pouchPosition;
            right.Position = pinch - right.Rotation * new Vector3(-0.1f, -0.06f, 0.68f);
        }

        private void PoseBat()
        {
            var arm = arms[1];
            var t = arm.Age;
            var restGrip = View(0.64f, 0.14f, 3f) + Bob(arm);
            var restDirection = new Vector3(-0.2f, 1f, 0.5f).normalized;
            Vector3 grip;
            Vector3 direction;
            arm.Lunge = Vector3.zero;
            if (t < 0.7f)
            {
                var target = Target(arm.Hit);
                var windGrip = View(0.78f, 0.3f, 2.9f);
                var windDirection = new Vector3(0.45f, 0.85f, -0.25f).normalized;
                var contactDirection = new Vector3(-1f, 0.12f, 0.35f).normalized;
                var contactGrip = target - contactDirection * 1.6f;
                var followGrip = View(0.3f, 0.12f, 3.1f);
                var followDirection = new Vector3(-0.55f, -0.75f, -0.3f).normalized;
                if (t < 0.12f) { var k = Smooth(t / 0.12f); grip = Vector3.Lerp(restGrip, windGrip, k); direction = Vector3.Slerp(restDirection, windDirection, k); }
                else if (t < 0.2f) { var k = Mathf.Pow((t - 0.12f) / 0.08f, 1.6f); grip = Vector3.Lerp(windGrip, contactGrip, k); direction = Vector3.Slerp(windDirection, contactDirection, k); }
                else if (t < 0.32f) { var k = Smooth((t - 0.2f) / 0.12f); grip = Vector3.Lerp(contactGrip, followGrip, k); direction = Vector3.Slerp(contactDirection, followDirection, k); }
                else { var k = Smooth((t - 0.32f) / 0.38f); grip = Vector3.Lerp(followGrip, restGrip, k); direction = Vector3.Slerp(followDirection, restDirection, k); }
                var reach = t < 0.2f ? Smooth(t / 0.2f) : 1f - Smooth((t - 0.2f) / 0.4f);
                arm.Lunge = new Vector3(-0.3f, 0.3f, 1.5f) * reach;
                if (!arm.Fired && t >= 0.2f)
                {
                    arm.Fired = true;
                    Impact?.Invoke(ToolKind.Bat, arm.Hit, arm.Power, new Vector3(-1f, -0.1f, 0.3f));
                }
            }
            else
            {
                grip = restGrip;
                direction = restDirection;
            }
            bat.position = grip;
            bat.rotation = Quaternion.FromToRotation(Vector3.up, direction);
            var away = grip - CameraPosition;
            arm.Rotation = Grip(-direction, away);
            arm.Position = grip + direction * 0.12f - arm.Rotation * GripPoint;
            var left = arms[0];
            left.Lunge = arm.Lunge;
            left.Rotation = Grip(-direction, away);
            left.Position = grip + direction * 0.42f - left.Rotation * GripPoint;
        }

        private void PoseFeet(float deltaTime)
        {
            Relax(arms[0]);
            Relax(arms[1]);
            footAge += deltaTime;
            var t = footAge;
            var rest = View(0.5f, -0.35f, 2.4f);
            var target = Target(footHit);
            var toward = (target - CameraPosition).normalized;
            var strike = target - toward * 0.32f;
            var reach = t < 0.07f ? 0f : t < 0.15f ? Smooth((t - 0.07f) / 0.08f) : t < 0.2f ? 1f : 1f - Smooth((t - 0.2f) / 0.25f);
            foot.position = Vector3.Lerp(rest + Vector3.down * 0.3f * Mathf.Clamp01(t / 0.07f) * (1f - reach), strike, reach) + Vector3.up * Mathf.Sin(clock * 3f) * 0.02f;
            foot.rotation = Quaternion.Slerp(Quaternion.LookRotation(Vector3.up + Vector3.forward * 0.4f, Vector3.back), Quaternion.LookRotation(toward, Vector3.up), Mathf.Max(reach, 0.25f)) * Quaternion.Euler(0f, 0f, Mathf.Sin(clock * 5f) * 4f);
            if (footFired || t < 0.15f) return;
            footFired = true;
            Impact?.Invoke(ToolKind.Feet, footHit, 1f, toward);
        }

        private Vector3 Elbow(Vector3 shoulder, Vector3 wrist, float side)
        {
            var offset = wrist - shoulder;
            var length = Mathf.Max(offset.magnitude, 0.001f);
            var direction = offset / length;
            var total = UpperLength + ForeLength;
            if (length >= total * 0.999f) return shoulder + direction * (length * UpperLength / total);
            var along = (UpperLength * UpperLength - ForeLength * ForeLength + length * length) / (2f * length);
            var height = Mathf.Sqrt(Mathf.Max(0f, UpperLength * UpperLength - along * along));
            var bend = Vector3.ProjectOnPlane(new Vector3(side * 0.9f, -1f, -0.2f), direction).normalized;
            return shoulder + direction * along + bend * height;
        }

        private void Solve(Arm arm)
        {
            var shoulder = arm.Shoulder + arm.Lunge;
            var elbow = Elbow(shoulder, arm.Position, arm.Side);
            arm.Limb.Set(shoulder, elbow * 2f - (shoulder + arm.Position) * 0.5f, arm.Position, Vector3.up);
            arm.Wrist.position = arm.Position;
            arm.Wrist.rotation = arm.Rotation;
        }

        private static float Smooth(float t) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

        public void Dispose()
        {
            if (root != null) ArtKit.Destroy(root.gameObject);
            kit.Dispose();
        }
    }
}

using UnityEngine;

namespace EmoRelief
{
    // Five-finger hand: wrist at origin, fingers along +Z, back of hand +Y, thumb toward -X * side.
    public sealed class HandModel
    {
        public Transform Root { get; }
        private readonly Transform[][] joints = new Transform[5][];
        private readonly float side;

        private static readonly float[][] Lengths =
        {
            new[] { 0.17f, 0.14f, 0.12f },
            new[] { 0.21f, 0.13f, 0.11f },
            new[] { 0.23f, 0.145f, 0.12f },
            new[] { 0.215f, 0.135f, 0.11f },
            new[] { 0.17f, 0.105f, 0.095f }
        };
        private static readonly float[] Radius = { 0.07f, 0.058f, 0.06f, 0.057f, 0.05f };

        public HandModel(string name, Transform parent, float side, Material skin, Material nail, ArtKit kit)
        {
            this.side = side;
            Root = ArtKit.Group(name, parent);
            MeshKit.Part("Wrist", Root, kit.Own(MeshKit.Rounded("Wrist", new Vector3(0.18f, 0.13f, 0.16f), 2.2f)), skin, new Vector3(0f, 0f, 0.02f));
            MeshKit.Part("Palm", Root, kit.Own(MeshKit.Rounded("Palm", new Vector3(0.235f, 0.09f, 0.27f), 3f)), skin, new Vector3(0f, 0f, 0.29f));
            MeshKit.Part("Thumb pad", Root, kit.Own(MeshKit.Rounded("Thenar", new Vector3(0.1f, 0.085f, 0.16f), 2f)), skin, new Vector3(-side * 0.15f, -0.035f, 0.2f), new Vector3(0f, -side * 18f, 0f));
            var knuckles = new[] { -0.165f, -0.055f, 0.055f, 0.16f };
            var reach = new[] { 0.53f, 0.555f, 0.535f, 0.49f };
            for (var finger = 0; finger < 5; finger++)
            {
                Vector3 origin;
                Quaternion orientation;
                if (finger == 0)
                {
                    origin = new Vector3(-side * 0.19f, -0.04f, 0.17f);
                    orientation = Quaternion.LookRotation(new Vector3(-side * 0.65f, -0.2f, 0.72f), Vector3.up) * Quaternion.Euler(0f, 0f, -side * 55f);
                }
                else
                {
                    origin = new Vector3(knuckles[finger - 1] * side, 0.01f, reach[finger - 1]);
                    orientation = Quaternion.identity;
                }
                var chain = new Transform[3];
                var parentJoint = ArtKit.Group(finger == 0 ? "Thumb" : "Finger " + finger, Root, origin);
                parentJoint.localRotation = orientation;
                var holder = parentJoint;
                for (var bone = 0; bone < 3; bone++)
                {
                    var length = Lengths[finger][bone];
                    var start = Radius[finger] * (1f - bone * 0.08f);
                    var end = Radius[finger] * (1f - (bone + 1) * 0.08f);
                    var joint = ArtKit.Group("Joint", holder, bone == 0 ? Vector3.zero : new Vector3(0f, 0f, Lengths[finger][bone - 1]));
                    MeshKit.Part("Bone", joint, kit.Own(MeshKit.Capsule("Finger bone", start, end, length)), skin);
                    if (bone == 2)
                        MeshKit.Part("Nail", joint, kit.Own(MeshKit.Rounded("Nail", new Vector3(end * 0.75f, 0.012f, length * 0.38f), 2.5f)), nail, new Vector3(0f, end * 0.86f, length * 0.62f));
                    chain[bone] = joint;
                    holder = joint;
                }
                joints[finger] = chain;
            }
            Pose(0.1f, 0.1f, 0.5f);
        }

        // curl: 0 open .. 1 fist, thumb: 0 out .. 1 wrapped, spread: finger fan.
        public void Pose(float curl, float thumb, float spread, float indexCurl = -1f)
        {
            for (var finger = 1; finger < 5; finger++)
            {
                var amount = finger == 1 && indexCurl >= 0f ? indexCurl : finger == 2 && indexCurl >= 0f ? Mathf.Lerp(indexCurl, curl, 0.4f) : curl;
                var yaw = (finger - 2.5f) * spread * 7f * side;
                joints[finger][0].localRotation = Quaternion.Euler(amount * 82f, yaw, 0f);
                joints[finger][1].localRotation = Quaternion.Euler(amount * 100f, 0f, 0f);
                joints[finger][2].localRotation = Quaternion.Euler(amount * 65f, 0f, 0f);
            }
            joints[0][0].localRotation = Quaternion.Euler(thumb * 28f, -side * thumb * 26f, 0f);
            joints[0][1].localRotation = Quaternion.Euler(thumb * 38f, 0f, 0f);
            joints[0][2].localRotation = Quaternion.Euler(thumb * 45f, 0f, 0f);
        }

        public void SetActive(bool active) => Root.gameObject.SetActive(active);
    }
}

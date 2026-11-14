using System.Collections.Generic;
using UnityEngine;

namespace EmoRelief
{
    public sealed class ArtKit
    {
        private readonly List<Object> owned = new List<Object>();

        public float Outline = 2.5f;
        private static Shader toon;

        public Material Lit(string name, Color color, float smoothness = 0.35f)
        {
            if (toon == null) toon = Shader.Find("EmoRelief/Toon");
            var material = toon != null ? new Material(toon) { name = name } : new Material(Resources.Load<Material>("StageLit")) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_OutlineWidth", Outline);
            owned.Add(material);
            return material;
        }

        public Material Unlit(string name, Color color)
        {
            var material = new Material(Resources.Load<Material>("StageUnlit")) { name = name };
            material.SetColor("_BaseColor", color);
            owned.Add(material);
            return material;
        }

        public T Own<T>(T asset) where T : Object
        {
            owned.Add(asset);
            return asset;
        }

        public static Transform Shape(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 size, Material material, Vector3 euler = default)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            var collider = part.GetComponent<Collider>();
            collider.enabled = false;
            if (Application.isPlaying) Object.Destroy(collider);
            else Object.DestroyImmediate(collider);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = Quaternion.Euler(euler);
            part.transform.localScale = size;
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part.transform;
        }

        public static Transform Ball(string name, Transform parent, Vector3 position, Vector3 radius, Material material, Vector3 euler = default) =>
            Shape(PrimitiveType.Sphere, name, parent, position, radius * 2f, material, euler);

        public static Transform Box(string name, Transform parent, Vector3 position, Vector3 size, Material material, Vector3 euler = default) =>
            Shape(PrimitiveType.Cube, name, parent, position, size, material, euler);

        public static Transform Rod(string name, Transform parent, Vector3 position, float radius, float length, Material material, Vector3 euler = default) =>
            Shape(PrimitiveType.Cylinder, name, parent, position, new Vector3(radius * 2f, length * 0.5f, radius * 2f), material, euler);

        public static Transform Group(string name, Transform parent, Vector3 position = default, Vector3 euler = default)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            group.localPosition = position;
            group.localRotation = Quaternion.Euler(euler);
            return group;
        }

        // Stretches a capsule so its caps sit on points a and b (world space).
        public static void Span(Transform capsule, Vector3 a, Vector3 b, float radius)
        {
            var axis = b - a;
            var length = Mathf.Max(axis.magnitude, 0.001f);
            capsule.position = (a + b) * 0.5f;
            capsule.rotation = Quaternion.FromToRotation(Vector3.up, axis / length);
            capsule.localScale = new Vector3(radius * 2f, length * 0.5f + radius, radius * 2f);
        }

        public static void Destroy(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }

        public void Dispose()
        {
            foreach (var asset in owned) Destroy(asset);
            owned.Clear();
        }
    }
}

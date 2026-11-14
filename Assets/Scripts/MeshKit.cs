using System.Collections.Generic;
using UnityEngine;

namespace EmoRelief
{
    // Procedural meshes for smoother, more anatomical shapes than primitives.
    // Rubber-hose limb: a tube along a quadratic curve, rebuilt every frame in world space.
    public sealed class Hose
    {
        private const int Rings = 16;
        private const int Sides = 14;
        private readonly Mesh mesh;
        private readonly Transform body;
        private readonly Vector3[] vertices = new Vector3[(Rings + 1) * (Sides + 1)];
        private readonly System.Func<float, float> radius;

        public Hose(string name, Transform parent, Material material, System.Func<float, float> radius)
        {
            this.radius = radius;
            mesh = new Mesh { name = name };
            mesh.MarkDynamic();
            var uv = new Vector2[vertices.Length];
            var triangles = new int[Rings * Sides * 6];
            for (var ring = 0; ring <= Rings; ring++)
                for (var side = 0; side <= Sides; side++)
                    uv[ring * (Sides + 1) + side] = new Vector2((float)side / Sides, (float)ring / Rings);
            for (int ring = 0, t = 0; ring < Rings; ring++)
                for (var side = 0; side < Sides; side++, t += 6)
                {
                    var a = ring * (Sides + 1) + side;
                    var b = a + Sides + 1;
                    triangles[t] = a; triangles[t + 1] = a + 1; triangles[t + 2] = b;
                    triangles[t + 3] = a + 1; triangles[t + 4] = b + 1; triangles[t + 5] = b;
                }
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            body = MeshKit.Part(name, parent, mesh, material);
        }

        public Mesh Mesh => mesh;

        public void Set(Vector3 start, Vector3 control, Vector3 end, Vector3 up)
        {
            body.position = Vector3.zero;
            body.rotation = Quaternion.identity;
            var previous = start;
            var normal = up;
            for (var ring = 0; ring <= Rings; ring++)
            {
                var t = (float)ring / Rings;
                var point = (1f - t) * (1f - t) * start + 2f * (1f - t) * t * control + t * t * end;
                var tangent = 2f * (1f - t) * (control - start) + 2f * t * (end - control);
                if (tangent.sqrMagnitude < 1e-8f) tangent = end - start;
                tangent.Normalize();
                normal = Vector3.ProjectOnPlane(normal, tangent);
                if (normal.sqrMagnitude < 1e-6f) normal = Vector3.ProjectOnPlane(Vector3.right, tangent);
                normal.Normalize();
                var binormal = Vector3.Cross(tangent, normal);
                var r = radius(t);
                for (var side = 0; side <= Sides; side++)
                {
                    var angle = side * Mathf.PI * 2f / Sides;
                    vertices[ring * (Sides + 1) + side] = point + (normal * Mathf.Cos(angle) + binormal * Mathf.Sin(angle)) * r;
                }
                previous = point;
            }
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
    }

    public static class MeshKit
    {
        public struct Ring
        {
            public Vector3 Center;
            public float Width;
            public float Depth;
            public float Front;
            public float Square;

            public Ring(float y, float width, float depth, float z = 0f, float front = 0f, float square = 2.4f)
            {
                Center = new Vector3(0f, y, z);
                Width = width;
                Depth = depth;
                Front = front;
                Square = square;
            }
        }

        private static float Power(float value, float exponent) => Mathf.Sign(value) * Mathf.Pow(Mathf.Abs(value), exponent);

        // Rounded box / superellipsoid. exponent 2 = ellipsoid, larger = boxier.
        public static Mesh Rounded(string name, Vector3 radius, float exponent = 2f, int segments = 28, int rings = 18)
        {
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            var e = 2f / exponent;
            for (var ring = 0; ring <= rings; ring++)
            {
                var v = -Mathf.PI * 0.5f + Mathf.PI * ring / rings;
                for (var segment = 0; segment <= segments; segment++)
                {
                    var u = -Mathf.PI + 2f * Mathf.PI * segment / segments;
                    var cv = Power(Mathf.Cos(v), e);
                    vertices.Add(new Vector3(radius.x * cv * Power(Mathf.Cos(u), e), radius.y * Power(Mathf.Sin(v), e), radius.z * cv * Power(Mathf.Sin(u), e)));
                    uv.Add(new Vector2((float)segment / segments, (float)ring / rings));
                }
            }
            Grid(triangles, rings, segments, true);
            return Finish(name, vertices, uv, triangles, point => Vector3.zero);
        }

        // Body section lofted along +Y through elliptical rings; Front bulges toward -Z (the character faces -Z).
        public static Mesh Loft(string name, IList<Ring> rings, int segments = 36)
        {
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (var index = 0; index < rings.Count; index++)
            {
                var ring = rings[index];
                var e = 2f / ring.Square;
                for (var segment = 0; segment <= segments; segment++)
                {
                    var angle = 2f * Mathf.PI * segment / segments;
                    var c = Power(Mathf.Cos(angle), e);
                    var s = Power(Mathf.Sin(angle), e);
                    var front = Mathf.Max(0f, -Mathf.Sin(angle));
                    var bulge = ring.Front * front * front * (1f - Mathf.Pow(Mathf.Abs(Mathf.Cos(angle)), 6f));
                    vertices.Add(ring.Center + new Vector3(ring.Width * c, 0f, ring.Depth * s - bulge));
                    uv.Add(new Vector2((float)segment / segments, (float)index / (rings.Count - 1)));
                }
            }
            Grid(triangles, rings.Count - 1, segments, false);
            Cap(vertices, uv, triangles, 0, segments, rings[0].Center, false);
            Cap(vertices, uv, triangles, (rings.Count - 1) * (segments + 1), segments, rings[rings.Count - 1].Center, true);
            var bottom = rings[0].Center;
            var top = rings[rings.Count - 1].Center;
            return Finish(name, vertices, uv, triangles, point =>
            {
                var t = Mathf.Clamp((point.y - bottom.y) / Mathf.Max(0.001f, top.y - bottom.y), 0.02f, 0.98f);
                return Vector3.Lerp(bottom, top, t);
            });
        }

        // Tapered capsule from z = 0 to z = length.
        public static Mesh Capsule(string name, float startRadius, float endRadius, float length, int segments = 20)
        {
            var profile = new List<Vector2>();
            const int cap = 6;
            for (var step = 0; step <= cap; step++)
            {
                var angle = Mathf.PI * 0.5f * (1f - (float)step / cap);
                profile.Add(new Vector2(Mathf.Cos(angle) * startRadius, -Mathf.Sin(angle) * startRadius));
            }
            for (var step = 0; step <= cap; step++)
            {
                var angle = Mathf.PI * 0.5f * step / cap;
                profile.Add(new Vector2(Mathf.Cos(angle) * endRadius, length + Mathf.Sin(angle) * endRadius));
            }
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (var index = 0; index < profile.Count; index++)
            {
                for (var segment = 0; segment <= segments; segment++)
                {
                    var angle = 2f * Mathf.PI * segment / segments;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * profile[index].x, Mathf.Sin(angle) * profile[index].x, profile[index].y));
                    uv.Add(new Vector2((float)segment / segments, (float)index / (profile.Count - 1)));
                }
            }
            Grid(triangles, profile.Count - 1, segments, true);
            return Finish(name, vertices, uv, triangles, point => new Vector3(0f, 0f, Mathf.Clamp(point.z, length * 0.02f, length * 0.98f)));
        }

        private static void Grid(List<int> triangles, int rows, int segments, bool flip)
        {
            for (var row = 0; row < rows; row++)
            {
                for (var segment = 0; segment < segments; segment++)
                {
                    var a = row * (segments + 1) + segment;
                    var b = a + segments + 1;
                    if (flip)
                    {
                        triangles.Add(a); triangles.Add(b); triangles.Add(a + 1);
                        triangles.Add(a + 1); triangles.Add(b); triangles.Add(b + 1);
                    }
                    else
                    {
                        triangles.Add(a); triangles.Add(a + 1); triangles.Add(b);
                        triangles.Add(a + 1); triangles.Add(b + 1); triangles.Add(b);
                    }
                }
            }
        }

        private static void Cap(List<Vector3> vertices, List<Vector2> uv, List<int> triangles, int start, int segments, Vector3 center, bool top)
        {
            var hub = vertices.Count;
            vertices.Add(center);
            uv.Add(new Vector2(0.5f, top ? 1f : 0f));
            var first = vertices.Count;
            for (var segment = 0; segment <= segments; segment++)
            {
                vertices.Add(vertices[start + segment]);
                uv.Add(uv[start + segment]);
            }
            for (var segment = 0; segment < segments; segment++)
            {
                if (top) { triangles.Add(hub); triangles.Add(first + segment); triangles.Add(first + segment + 1); }
                else { triangles.Add(hub); triangles.Add(first + segment + 1); triangles.Add(first + segment); }
            }
        }

        private static Mesh Finish(string name, List<Vector3> vertices, List<Vector2> uv, List<int> triangles, System.Func<Vector3, Vector3> inside)
        {
            for (var index = 0; index < triangles.Count; index += 3)
            {
                var a = vertices[triangles[index]];
                var b = vertices[triangles[index + 1]];
                var c = vertices[triangles[index + 2]];
                var centroid = (a + b + c) / 3f;
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), centroid - inside(centroid)) >= 0f) continue;
                var swap = triangles[index + 1];
                triangles[index + 1] = triangles[index + 2];
                triangles[index + 2] = swap;
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Transform Part(string name, Transform parent, Mesh mesh, Material material, Vector3 position = default, Vector3 euler = default)
        {
            var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)).transform;
            part.SetParent(parent, false);
            part.localPosition = position;
            part.localRotation = Quaternion.Euler(euler);
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
            return part;
        }

        // Places a +Z-aligned segment so it starts at a and points at b (world space).
        public static void Aim(Transform segment, Vector3 a, Vector3 b, Vector3 up)
        {
            segment.position = a;
            var direction = b - a;
            if (direction.sqrMagnitude < 1e-6f) return;
            segment.rotation = Quaternion.LookRotation(direction, up);
        }
    }
}

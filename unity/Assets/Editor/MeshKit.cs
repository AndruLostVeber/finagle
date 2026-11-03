using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kuro.EditorTools
{
    // Процедурные меши офиса: скругленные коробки, тела вращения, трубки по пути, сетки бумаги.
    // Части можно собирать в один меш (Shape + Matrix4x4). Готовые меши лежат ассетами в Assets/Meshes;
    // папка пересоздается при каждой сборке офиса (Reset), одинаковые меши берутся из кэша по ключу.
    public static class MeshKit
    {
        const string Dir = "Assets/Meshes";
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        public static void Reset()
        {
            cache.Clear();
            if (AssetDatabase.IsValidFolder(Dir)) AssetDatabase.DeleteAsset(Dir);
            AssetDatabase.CreateFolder("Assets", "Meshes");
        }

        // Меш по ключу: из кэша или собранный build и сохраненный ассетом.
        public static Mesh Get(string key, System.Action<Shape> build)
        {
            if (cache.TryGetValue(key, out var mesh)) return mesh;
            var s = new Shape();
            build(s);
            mesh = s.ToMesh(key);
            AssetDatabase.CreateAsset(mesh, $"{Dir}/{key}.asset");
            cache[key] = mesh;
            return mesh;
        }

        public static string Key(string kind, params float[] values)
        {
            var parts = new string[values.Length];
            for (int i = 0; i < values.Length; i++) parts[i] = values[i].ToString("0.####", CultureInfo.InvariantCulture);
            return kind + "_" + string.Join("_", parts);
        }

        public static Mesh RoundedBox(Vector3 size, float radius, int seg = 4) =>
            Get(Key("RBox", size.x, size.y, size.z, radius, seg), s => s.RoundedBox(size, radius, seg));

        public static Mesh Lathe(string name, Vector2[] profile, int sides = 32) =>
            Get("Lathe_" + name, s => s.Lathe(profile, sides));

        public static Mesh Tube(string name, Vector3[] path, float radius, int sides = 12) =>
            Get("Tube_" + name, s => s.Tube(path, radius, sides));

        // Сетка в плоскости XZ с двумя сторонами (лицо вверх и вниз) — для бумаги и ткани,
        // которую гнет ветер (PaperFlutter, ClothSway). Обе стороны имеют одинаковые XZ у вершин.
        public static Mesh Sheet(float w, float d, int nx, int nz) =>
            Get(Key("Sheet", w, d, nx, nz), s => s.Sheet(w, d, nx, nz));

        // Стена-фасад w×h в плоскости XY лицом в −Z, UV в метрах: U по горизонтали, V по вертикали.
        public static Mesh Panel(float w, float h) =>
            Get(Key("Panel", w, h), s => s.Panel(w, h));

        // Дуга в плоскости XY: центр, радиус, углы в градусах (0 — вдоль +X).
        public static Vector3[] Arc(Vector3 center, float r, float a0, float a1, int steps)
        {
            var pts = new Vector3[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.Deg2Rad * Mathf.Lerp(a0, a1, i / (float)steps);
                pts[i] = center + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
            }
            return pts;
        }

        public class Shape
        {
            readonly List<Vector3> v = new List<Vector3>();
            readonly List<Vector3> n = new List<Vector3>();
            readonly List<Vector2> uv = new List<Vector2>();
            readonly List<int> tris = new List<int>();

            // Положение следующих частей внутри общего меша (повороты, сдвиги, равномерный масштаб).
            public Matrix4x4 M = Matrix4x4.identity;

            public Shape At(Vector3 pos, Vector3 euler = default, float scale = 1f)
            {
                M = Matrix4x4.TRS(pos, Quaternion.Euler(euler), Vector3.one * scale);
                return this;
            }

            int Vert(Vector3 p, Vector3 normal, Vector2 tex)
            {
                v.Add(M.MultiplyPoint3x4(p));
                n.Add(M.MultiplyVector(normal).normalized);
                uv.Add(tex);
                return v.Count - 1;
            }

            // Треугольник лицом туда же, куда нормали его вершин (порядок обхода подбирается сам).
            void Tri(int a, int b, int c)
            {
                var face = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
                if (Vector3.Dot(face, n[a] + n[b] + n[c]) < 0f) (b, c) = (c, b);
                tris.Add(a);
                tris.Add(b);
                tris.Add(c);
            }

            void Quad(int a, int b, int c, int d)
            {
                Tri(a, b, c);
                Tri(a, c, d);
            }

            // Коробка size со скругленными ребрами радиуса r (seg сегментов на четверть скругления).
            // UV — в метрах по граням, U вдоль длинной стороны: волокна дерева идут вдоль доски.
            public Shape RoundedBox(Vector3 size, float r, int seg = 4)
            {
                Vector3 half = size * 0.5f;
                r = Mathf.Clamp(r, 0f, Mathf.Min(half.x, Mathf.Min(half.y, half.z)));
                Vector3 inner = half - Vector3.one * r;
                for (int axis = 0; axis < 3; axis++)
                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        int ua = (axis + 1) % 3, va = (axis + 2) % 3;
                        if (size[va] > size[ua]) (ua, va) = (va, ua); // U — вдоль длинной стороны грани
                        var us = Coords(half[ua], r, seg);
                        var vs = Coords(half[va], r, seg);
                        int start = v.Count;
                        foreach (float b in vs)
                            foreach (float a in us)
                            {
                                var p = Vector3.zero;
                                p[axis] = sign * half[axis];
                                p[ua] = a;
                                p[va] = b;
                                var core = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                                var dir = p - core;
                                var normal = dir.sqrMagnitude > 1e-12f ? dir.normalized : Vector3.zero;
                                if (normal == Vector3.zero) normal[axis] = sign;
                                Vert(r > 0f ? core + normal * r : p, normal, new Vector2(a + half[ua], b + half[va]));
                            }
                        int w = us.Count;
                        for (int j = 0; j < vs.Count - 1; j++)
                            for (int i = 0; i < w - 1; i++)
                            {
                                int i0 = start + j * w + i;
                                Quad(i0, i0 + 1, i0 + w + 1, i0 + w);
                            }
                    }
                return this;
            }

            // Координаты вдоль оси грани: плоская середина и скругления по краям с равным шагом угла.
            static List<float> Coords(float half, float r, int seg)
            {
                var list = new List<float>();
                float flat = half - r;
                if (r <= 1e-6f)
                {
                    list.Add(-half);
                    list.Add(half);
                    return list;
                }
                for (int k = seg; k >= 0; k--) list.Add(-flat - r * Mathf.Tan(k / (float)seg * Mathf.PI / 4f));
                for (int k = 0; k <= seg; k++)
                {
                    float c = flat + r * Mathf.Tan(k / (float)seg * Mathf.PI / 4f);
                    if (k == 0 && flat < 1e-5f) continue; // середины нет — не дублировать точку
                    list.Add(c);
                }
                return list;
            }

            // Тело вращения вокруг Y. profile — точки (радиус, высота) снизу вверх снаружи;
            // повтор точки дает острый излом (нормали не сглаживаются через нее).
            public Shape Lathe(Vector2[] profile, int sides = 32)
            {
                int count = profile.Length;
                var normals2 = new Vector2[count];
                var along = new float[count];
                for (int i = 0; i < count; i++)
                {
                    Vector2 prev = i > 0 ? profile[i] - profile[i - 1] : Vector2.zero;
                    Vector2 next = i < count - 1 ? profile[i + 1] - profile[i] : Vector2.zero;
                    Vector2 sum = Vector2.zero;
                    if (prev.sqrMagnitude > 1e-12f) sum += new Vector2(prev.y, -prev.x).normalized;
                    if (next.sqrMagnitude > 1e-12f) sum += new Vector2(next.y, -next.x).normalized;
                    // У повторенной точки нормаль — только от ненулевого соседнего отрезка.
                    if (prev.sqrMagnitude <= 1e-12f && i > 0 && next.sqrMagnitude > 1e-12f) sum = new Vector2(next.y, -next.x).normalized;
                    if (next.sqrMagnitude <= 1e-12f && i < count - 1 && prev.sqrMagnitude > 1e-12f) sum = new Vector2(prev.y, -prev.x).normalized;
                    normals2[i] = sum.sqrMagnitude > 1e-12f ? sum.normalized : Vector2.up;
                    along[i] = i > 0 ? along[i - 1] + prev.magnitude : 0f;
                }

                int start = v.Count;
                for (int i = 0; i < count; i++)
                    for (int s = 0; s <= sides; s++)
                    {
                        float a = s / (float)sides * Mathf.PI * 2f;
                        float c = Mathf.Cos(a), sn = Mathf.Sin(a);
                        var p = new Vector3(profile[i].x * c, profile[i].y, profile[i].x * sn);
                        var nn = new Vector3(normals2[i].x * c, normals2[i].y, normals2[i].x * sn);
                        Vert(p, nn, new Vector2(s / (float)sides, along[i]));
                    }
                int row = sides + 1;
                for (int i = 0; i < count - 1; i++)
                {
                    if ((profile[i + 1] - profile[i]).sqrMagnitude <= 1e-12f) continue;
                    for (int s = 0; s < sides; s++)
                    {
                        int a0 = start + i * row + s;
                        Quad(a0, a0 + 1, a0 + row + 1, a0 + row);
                    }
                }
                return this;
            }

            // Трубка радиуса radius вдоль пути (рамки без закручивания), с торцами.
            public Shape Tube(Vector3[] path, float radius, int sides = 12, bool caps = true)
            {
                int count = path.Length;
                var tangents = new Vector3[count];
                for (int i = 0; i < count; i++)
                {
                    Vector3 a = path[Mathf.Max(i - 1, 0)], b = path[Mathf.Min(i + 1, count - 1)];
                    tangents[i] = (b - a).normalized;
                }
                Vector3 up = Mathf.Abs(Vector3.Dot(tangents[0], Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
                Vector3 normal = Vector3.Cross(tangents[0], up).normalized;
                int start = v.Count;
                float len = 0f;
                for (int i = 0; i < count; i++)
                {
                    if (i > 0)
                    {
                        len += Vector3.Distance(path[i], path[i - 1]);
                        var q = Quaternion.FromToRotation(tangents[i - 1], tangents[i]);
                        normal = (q * normal).normalized;
                    }
                    Vector3 bin = Vector3.Cross(tangents[i], normal);
                    for (int s = 0; s <= sides; s++)
                    {
                        float a = s / (float)sides * Mathf.PI * 2f;
                        var dir = normal * Mathf.Cos(a) + bin * Mathf.Sin(a);
                        Vert(path[i] + dir * radius, dir, new Vector2(s / (float)sides, len));
                    }
                }
                int row = sides + 1;
                for (int i = 0; i < count - 1; i++)
                    for (int s = 0; s < sides; s++)
                    {
                        int a0 = start + i * row + s;
                        Quad(a0, a0 + 1, a0 + row + 1, a0 + row);
                    }
                if (caps)
                {
                    Cap(path[0], -tangents[0], start, sides);
                    Cap(path[count - 1], tangents[count - 1], start + (count - 1) * row, sides);
                }
                return this;
            }

            void Cap(Vector3 center, Vector3 normal, int ring, int sides)
            {
                int c = Vert(center, normal, new Vector2(0.5f, 0.5f));
                var copy = new int[sides + 1];
                for (int s = 0; s <= sides; s++) copy[s] = Vert(M.inverse.MultiplyPoint3x4(v[ring + s]), normal, Vector2.zero);
                for (int s = 0; s < sides; s++) Tri(c, copy[s], copy[s + 1]);
            }

            public Shape Panel(float w, float h)
            {
                int a = Vert(new Vector3(-w / 2f, -h / 2f, 0f), Vector3.back, new Vector2(0f, 0f));
                int b = Vert(new Vector3(w / 2f, -h / 2f, 0f), Vector3.back, new Vector2(w, 0f));
                int c = Vert(new Vector3(w / 2f, h / 2f, 0f), Vector3.back, new Vector2(w, h));
                int d = Vert(new Vector3(-w / 2f, h / 2f, 0f), Vector3.back, new Vector2(0f, h));
                Quad(a, b, c, d);
                return this;
            }

            // Квадрат 1×1 в плоскости XZ лицом вверх, UV 0..1 (подложки-тени).
            public Shape Quad1()
            {
                int a = Vert(new Vector3(-0.5f, 0f, -0.5f), Vector3.up, new Vector2(0f, 0f));
                int b = Vert(new Vector3(0.5f, 0f, -0.5f), Vector3.up, new Vector2(1f, 0f));
                int c = Vert(new Vector3(0.5f, 0f, 0.5f), Vector3.up, new Vector2(1f, 1f));
                int d = Vert(new Vector3(-0.5f, 0f, 0.5f), Vector3.up, new Vector2(0f, 1f));
                Quad(a, b, c, d);
                return this;
            }

            // Двусторонний лист w×d в плоскости XZ, nx×nz клеток. UV 0..1.
            public Shape Sheet(float w, float d, int nx, int nz)
            {
                for (int side = 0; side < 2; side++)
                {
                    var normal = side == 0 ? Vector3.up : Vector3.down;
                    int start = v.Count;
                    for (int j = 0; j <= nz; j++)
                        for (int i = 0; i <= nx; i++)
                        {
                            float u = i / (float)nx, t = j / (float)nz;
                            Vert(new Vector3((u - 0.5f) * w, 0f, (t - 0.5f) * d), normal, new Vector2(side == 0 ? u : 1f - u, t));
                        }
                    int row = nx + 1;
                    for (int j = 0; j < nz; j++)
                        for (int i = 0; i < nx; i++)
                        {
                            int a = start + j * row + i;
                            Quad(a, a + 1, a + row + 1, a + row);
                        }
                }
                return this;
            }

            // Другой меш целиком (например, скругленная клавиша) — в текущее положение M.
            public Shape Add(Mesh mesh)
            {
                int start = v.Count;
                var mv = mesh.vertices;
                var mn = mesh.normals;
                var mu = mesh.uv;
                for (int i = 0; i < mv.Length; i++) Vert(mv[i], mn[i], mu.Length > i ? mu[i] : Vector2.zero);
                foreach (int t in mesh.triangles) tris.Add(start + t);
                return this;
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                if (v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(v);
                mesh.SetNormals(n);
                mesh.SetUVs(0, uv);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                return mesh;
            }
        }
    }
}

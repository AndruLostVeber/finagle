using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kuro.EditorTools
{
    // Процедурные текстуры офиса: дерево, паркет, штукатурка, плитка потолка, ткань, бетон,
    // шлифованный металл, документы, экраны, мягкое пятно. Шум бесшовный — текстуры тайлятся.
    // Карты нормалей строятся из карты высот. Файлы — PNG в Assets/Textures.
    public static class TextureKit
    {
        const string Dir = "Assets/Textures";

        public struct Set
        {
            public Texture2D albedo, normal;
        }

        // ---------- шум ----------

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 1442695041;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        static int Wrap(int i, int p) => ((i % p) + p) % p;

        // Бесшовный value-шум с периодом px×py клеток на всю текстуру (u, v в 0..1).
        static float Noise(float u, float v, int px, int py, int seed)
        {
            float x = u * px, y = v * py;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(Wrap(x0, px), Wrap(y0, py), seed), b = Hash(Wrap(x0 + 1, px), Wrap(y0, py), seed);
            float c = Hash(Wrap(x0, px), Wrap(y0 + 1, py), seed), d = Hash(Wrap(x0 + 1, px), Wrap(y0 + 1, py), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Fbm(float u, float v, int px, int py, int seed, int octaves = 4)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += Noise(u, v, px << o, py << o, seed + o * 31) * amp;
                norm += amp;
                amp *= 0.5f;
            }
            return sum / norm;
        }

        // ---------- сохранение ----------

        static Texture2D Save(string name, int w, int h, Color[] px, bool normal = false, bool alpha = false, bool mips = true)
        {
            EnsureFolder();
            string path = $"{Dir}/{name}.png";
            var tex = new Texture2D(w, h, alpha ? TextureFormat.RGBA32 : TextureFormat.RGB24, false);
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.alphaIsTransparency = alpha;
            imp.mipmapEnabled = mips;
            imp.anisoLevel = 8;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.maxTextureSize = Mathf.Max(w, h);
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets", "Textures");
        }

        // Нормали из высот (бесшовно), strength — крутизна рельефа.
        static Texture2D NormalFromHeight(string name, float[] hgt, int size, float strength)
        {
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float l = hgt[y * size + Wrap(x - 1, size)], r = hgt[y * size + Wrap(x + 1, size)];
                    float d = hgt[Wrap(y - 1, size) * size + x], u = hgt[Wrap(y + 1, size) * size + x];
                    var n = new Vector3((l - r) * strength, (d - u) * strength, 1f).normalized;
                    px[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f);
                }
            return Save(name, size, size, px, normal: true);
        }

        static Set Pair(string name, int size, Func<float, float, (Color c, float h)> f, float strength)
        {
            var px = new Color[size * size];
            var hgt = new float[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var (c, h) = f((x + 0.5f) / size, (y + 0.5f) / size);
                    px[y * size + x] = c;
                    hgt[y * size + x] = h;
                }
            return new Set { albedo = Save(name, size, size, px), normal = NormalFromHeight(name + "_N", hgt, size, strength) };
        }

        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.magenta;

        // ---------- материалы поверхностей ----------

        // Древесина с волокнами вдоль U. Светлый тон — основа, темный — волокна.
        public static Set Wood(string name, string light, string dark, int seed, float rings = 18f)
        {
            Color a = Hex(light), b = Hex(dark);
            return Pair(name, 512, (u, v) =>
            {
                float warp = Fbm(u, v, 2, 4, seed) * 1.4f;
                float fibre = Fbm(u, v, 4, 160, seed + 7, 3); // длинные волокна вдоль U
                float ring = 0.5f + 0.5f * Mathf.Sin((v * rings + warp) * Mathf.PI * 2f);
                float grain = Mathf.Pow(ring, 6f) * 0.3f + fibre * 0.32f;
                float blotch = Fbm(u, v, 2, 3, seed + 3) * 0.12f;
                var c = Color.Lerp(a, b, Mathf.Clamp01(grain + blotch - 0.08f));
                return (c, 1f - grain);
            }, 1.6f);
        }

        // Паркет: доски 1/8 текстуры в ширину, волокна, тон досок разный, швы — канавки.
        public static Set Parquet(string name, string light, string dark)
        {
            Color a = Hex(light), b = Hex(dark), seam = Hex("4A3322");
            const int rows = 8;
            return Pair(name, 1024, (u, v) =>
            {
                int row = Mathf.FloorToInt(v * rows);
                float offset = Hash(row, 0, 5);
                float along = Mathf.Repeat(u + offset, 1f);
                int plank = Mathf.FloorToInt(along * 2f);
                float inRow = v * rows - row, inPlank = along * 2f - plank;
                bool isSeam = inRow < 0.025f || inPlank < 0.006f;
                float tint = 0.85f + Hash(row, plank, 9) * 0.25f;
                float warp = Fbm(along, v, 4, 16, row * 3 + plank) * 2f;
                float ring = 0.5f + 0.5f * Mathf.Sin((inRow * 3f + warp) * Mathf.PI * 2f);
                float fine = Fbm(u, v, 128, 16, 4, 2);
                float grain = Mathf.Pow(ring, 3f) * 0.5f + fine * 0.3f;
                var c = Color.Lerp(a, b, grain) * tint;
                if (isSeam) c = seam;
                return (c, isSeam ? 0f : 1f - grain * 0.3f);
            }, 3f);
        }

        // Штукатурка: едва заметные пятна и мелкий рельеф.
        public static Set Plaster(string name, string hex, int seed)
        {
            Color a = Hex(hex);
            return Pair(name, 512, (u, v) =>
            {
                float big = Fbm(u, v, 4, 4, seed);
                float fine = Fbm(u, v, 96, 96, seed + 5, 2);
                return (a * (0.95f + big * 0.07f + fine * 0.03f), big * 0.3f + fine * 0.7f);
            }, 1.2f);
        }

        // Потолочная плитка 600×600: одна плитка на текстуру, фаска по краю, точечная перфорация.
        public static Set CeilingTile(string name)
        {
            Color a = Hex("F1EEE7"), groove = Hex("BDB6AA");
            return Pair(name, 512, (u, v) =>
            {
                float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                float speck = Hash(Mathf.FloorToInt(u * 160f), Mathf.FloorToInt(v * 160f), 3) > 0.93f ? 0.82f : 1f;
                float fine = Fbm(u, v, 64, 64, 8, 2);
                if (edge < 0.008f) return (groove, 0f);
                float bevel = Mathf.Clamp01((edge - 0.008f) / 0.02f);
                return (a * speck * (0.96f + fine * 0.05f) * Mathf.Lerp(0.9f, 1f, bevel), bevel * (speck < 1f ? 0.7f : 1f));
            }, 4f);
        }

        // Ткань: саржевое плетение и легкие переливы цвета.
        public static Set Fabric(string name, string hex, int seed)
        {
            Color a = Hex(hex);
            return Pair(name, 256, (u, v) =>
            {
                const float threads = 96f;
                float diag = Mathf.Repeat((u + v) * threads * 0.5f, 1f);
                float warp = 0.5f + 0.5f * Mathf.Sin(u * threads * Mathf.PI * 2f);
                float weft = 0.5f + 0.5f * Mathf.Sin(v * threads * Mathf.PI * 2f);
                float weave = Mathf.Lerp(warp, weft, diag > 0.5f ? 1f : 0f);
                float tone = Fbm(u, v, 6, 6, seed);
                return (a * (0.86f + weave * 0.12f + tone * 0.08f), weave);
            }, 1.5f);
        }

        public static Set Concrete(string name, string hex)
        {
            Color a = Hex(hex);
            return Pair(name, 512, (u, v) =>
            {
                float big = Fbm(u, v, 3, 3, 21);
                float fine = Fbm(u, v, 128, 128, 22, 2);
                float pit = Hash(Mathf.FloorToInt(u * 256f), Mathf.FloorToInt(v * 256f), 23) > 0.985f ? 0.7f : 1f;
                return (a * (0.85f + big * 0.2f + fine * 0.08f) * pit, big * 0.4f + fine * 0.6f - (1f - pit));
            }, 1.5f);
        }

        // Шлифованный металл: штрихи вдоль U.
        public static Set Brushed(string name, string hex)
        {
            Color a = Hex(hex);
            return Pair(name, 256, (u, v) =>
            {
                float streak = Fbm(u, v, 2, 256, 31, 3);
                return (a * (0.96f + streak * 0.06f), streak);
            }, 0.25f);
        }

        // Гофрокартон: волокна и чуть заметные полосы гофры.
        public static Set Carton(string name, string hex)
        {
            Color a = Hex(hex);
            return Pair(name, 256, (u, v) =>
            {
                float fib = Fbm(u, v, 64, 16, 41, 3);
                float flute = 0.5f + 0.5f * Mathf.Sin(v * 64f * Mathf.PI * 2f);
                return (a * (0.9f + fib * 0.14f), flute * 0.3f + fib * 0.7f);
            }, 0.8f);
        }

        // ---------- рисованные текстуры ----------

        // Документ: шапка, строки текста, таблица, подпись и печать. variant меняет раскладку.
        public static Texture2D Document(string name, int variant)
        {
            const int w = 512, h = 362; // A4 альбомом лежит на столе — текстура вдоль листа
            var px = new Color[w * h];
            var paper = Hex("FBF8F1");
            var ink = Hex("39424A");
            var rnd = new System.Random(100 + variant);
            for (int i = 0; i < px.Length; i++)
            {
                float n = Fbm((i % w) / (float)w, (i / w) / (float)h, 32, 24, 50 + variant, 2);
                px[i] = paper * (0.97f + n * 0.03f);
            }
            void Rect(int x0, int y0, int x1, int y1, Color c, float k = 1f)
            {
                for (int y = Mathf.Max(0, y0); y < Mathf.Min(h, y1); y++)
                    for (int x = Mathf.Max(0, x0); x < Mathf.Min(w, x1); x++)
                        px[y * w + x] = Color.Lerp(px[y * w + x], c, k);
            }
            // Строки идут сверху вниз (по V сверху — больше y).
            int top = h - 30;
            Rect(30, top - 18, 220, top, ink, 0.85f); // заголовок
            if (variant % 2 == 0) Rect(w - 150, top - 26, w - 30, top, Hex("1F6F68"), 0.8f); // логотип
            int yLine = top - 50;
            int lines = 7 + variant % 3;
            for (int l = 0; l < lines; l++)
            {
                int len = 200 + rnd.Next(240);
                if (l % 4 == 3) len = 120 + rnd.Next(80);
                Rect(30, yLine - 6, 30 + len, yLine, ink, 0.35f);
                yLine -= 16;
            }
            if (variant % 3 != 1)
            {
                // таблица сумм
                int ty = yLine - 10;
                for (int r = 0; r < 4; r++) Rect(30, ty - r * 18 - 1, w - 30, ty - r * 18, ink, 0.4f);
                for (int r = 0; r < 3; r++)
                {
                    Rect(40, ty - r * 18 - 13, 160 + rnd.Next(80), ty - r * 18 - 8, ink, 0.3f);
                    Rect(w - 120, ty - r * 18 - 13, w - 40, ty - r * 18 - 8, ink, 0.45f);
                }
            }
            // подпись и синяя печать внизу справа
            var blue = Hex("3A5BA8");
            for (int i = 0; i < 90; i++)
            {
                int x = 300 + i, y = 34 + (int)(Mathf.Sin(i * 0.25f) * 6f);
                Rect(x, y, x + 2, y + 2, ink, 0.7f);
            }
            if (variant != 2)
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), new Vector2(w - 90, 52));
                        if (d < 36f && (d > 31f || (d > 20f && d < 22f))) px[y * w + x] = Color.Lerp(px[y * w + x], blue, 0.55f);
                    }
            return Save(name, w, h, px);
        }

        // Рабочий стол «КУРО OS» на 3D-мониторе: фон, иконки приложений, панель задач.
        public static Texture2D Desktop(string name)
        {
            const int w = 512, h = 290;
            var px = new Color[w * h];
            Color top = Hex("1C4A46"), bottom = Hex("0F2624");
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float t = y / (float)h;
                    float glow = Mathf.Exp(-Vector2.SqrMagnitude(new Vector2(x / (float)w - 0.7f, t - 0.6f)) * 6f) * 0.35f;
                    px[y * w + x] = Color.Lerp(bottom, top, t) + new Color(0.1f, 0.25f, 0.22f) * glow;
                }
            string[] apps = { "1F6F68", "2F5E8C", "C4453B", "3A4045", "6E4E36" };
            for (int i = 0; i < apps.Length; i++)
            {
                int cx = 30 + (i % 2) * 46, cy = h - 34 - (i / 2) * 50;
                RoundRect(px, w, h, cx - 17, cy - 17, cx + 17, cy + 17, 7, Hex(apps[i]) * 1.4f);
                RoundRect(px, w, h, cx - 8, cy - 6, cx + 8, cy + 6, 2, Color.white * 0.9f);
                RoundRect(px, w, h, cx - 15, cy - 30, cx + 15, cy - 26, 2, Color.white * 0.55f);
            }
            RoundRect(px, w, h, 0, 0, w, 20, 0, Hex("081011"));
            RoundRect(px, w, h, 6, 4, 40, 16, 4, Hex("48C6B4"));
            RoundRect(px, w, h, w - 60, 6, w - 8, 14, 3, Color.white * 0.6f);
            return Save(name, w, h, px);
        }

        // Экран телефона: заставка с часами и карточкой уведомления (горит отдельным слоем Notice).
        public static Texture2D PhoneScreen(string name)
        {
            const int w = 128, h = 256;
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float t = y / (float)h;
                    px[y * w + x] = Color.Lerp(Hex("2A4F7A"), Hex("6FB4DA"), t) * (0.9f + 0.1f * Mathf.Sin(x * 0.05f + y * 0.03f));
                }
            RoundRect(px, w, h, 34, 190, 94, 214, 6, Color.white * 0.85f); // часы
            RoundRect(px, w, h, 46, 178, 82, 183, 2, Color.white * 0.5f);
            RoundRect(px, w, h, 52, 12, 76, 16, 2, Color.white * 0.6f); // полоска «домой»
            return Save(name, w, h, px);
        }

        // Стеклянный фасад башни: 8 модулей по 1,6 м × 8 этажей по 3,6 м на текстуру.
        // Отражение неба в стекле, импосты, межэтажные пояса; часть окон горит (карта свечения).
        public static (Texture2D albedo, Texture2D emission) Facade(string name, int seed)
        {
            const int s = 512, cell = 64;
            var px = new Color[s * s];
            var em = new Color[s * s];
            Color skyTop = Hex("9DB6CC"), skyLow = Hex("3E5369"), band = Hex("5E6B76"), mullion = Hex("C9D0D5"), warm = Hex("FFD49A");
            for (int cy = 0; cy < 8; cy++)
                for (int cx = 0; cx < 8; cx++)
                {
                    float r = Hash(cx, cy, seed);
                    bool lit = r > 0.8f, blind = r < 0.12f;
                    for (int y = 0; y < cell; y++)
                        for (int x = 0; x < cell; x++)
                        {
                            int i = (cy * cell + y) * s + cx * cell + x;
                            float u = (cx * cell + x) / (float)s, v = (cy * cell + y) / (float)s;
                            Color c, e = Color.black;
                            if (y < 11) c = band * (0.95f + Fbm(u, v, 32, 32, seed + 1, 2) * 0.08f);
                            else if (x < 3 || y < 13) c = mullion;
                            else
                            {
                                float t = (y - 13f) / (cell - 13f);
                                c = Color.Lerp(skyLow, skyTop, t * 0.8f + Fbm(u, v, 4, 8, seed + 2) * 0.35f);
                                if (blind) c = Color.Lerp(c, Hex("D8DCDC"), 0.55f);
                                if (lit)
                                {
                                    c = Color.Lerp(c, warm, 0.45f);
                                    e = warm * (0.55f + 0.3f * Hash(cx, cy, seed + 9));
                                }
                            }
                            px[i] = c;
                            em[i] = e;
                        }
                }
            return (Save(name, s, s, px), Save(name + "_E", s, s, em));
        }

        // Мягкое круглое пятно: тени-подложки, пар, снег.
        public static Texture2D Blob(string name, float power = 1.6f)
        {
            const int s = 128;
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(s / 2f, s / 2f)) / (s / 2f);
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), power);
                    px[y * s + x] = new Color(1f, 1f, 1f, a);
                }
            return Save(name, s, s, px, alpha: true);
        }

        // Полоса затенения в углу комнаты: 0 — у угла (темно), 1 — свет.
        public static Texture2D Gradient(string name)
        {
            const int w = 4, h = 64;
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float a = Mathf.Pow(1f - y / (h - 1f), 2.2f);
                for (int x = 0; x < w; x++) px[y * w + x] = new Color(1f, 1f, 1f, a);
            }
            var tex = Save(name, w, h, px, alpha: true, mips: false);
            var imp = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex));
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
            return tex;
        }

        // Небо за окном: голубое сверху, светлая дымка у горизонта.
        public static Texture2D Sky(string name)
        {
            const int w = 8, h = 128;
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float t = y / (h - 1f);
                var c = Color.Lerp(Hex("EEF2F5"), Hex("9DBCD8"), Mathf.Pow(t, 0.8f));
                for (int x = 0; x < w; x++) px[y * w + x] = c;
            }
            return Save(name, w, h, px, mips: false);
        }

        static void RoundRect(Color[] px, int w, int h, int x0, int y0, int x1, int y1, int r, Color c)
        {
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(h, y1); y++)
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(w, x1); x++)
                {
                    int dx = Mathf.Max(Mathf.Max(x0 + r - x, x - (x1 - 1 - r)), 0);
                    int dy = Mathf.Max(Mathf.Max(y0 + r - y, y - (y1 - 1 - r)), 0);
                    if (dx * dx + dy * dy <= r * r) px[y * w + x] = c;
                }
        }
    }
}

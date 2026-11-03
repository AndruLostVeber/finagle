using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kuro.EditorTools
{
    // WebGL-сборка игры для браузера. Меню Kuro → Собрать WebGL (в unity/Builds/WebGL).
    // Kuro → Собрать WebGL (next) — копия для проб: шаблон KuroNext.
    // Страница — шаблон KuroFull: 3D-офис на всю вкладку, поверх него интерфейс из
    // ../demo-next/index.html (титул, окна, отчетность). Обе сборки берут этот файл:
    // отдельной 2D-версии (папки demo) больше нет. Сжатие на время сборки выключено: билд открывается
    // простым сервером без особых заголовков. В конце пишет build-result.txt.
    public static class WebBuild
    {
        const string DefaultPath = "Builds/WebGL";
        const string Template = "PROJECT:KuroFull";

        [MenuItem("Kuro/Собрать WebGL")]
        static void BuildMenu() => Build(DefaultPath);

        [MenuItem("Kuro/Собрать WebGL (next)")]
        static void BuildNextMenu() => BuildNext("Builds/WebGL-next");

        public static void BuildNext(string outputPath) => Build(outputPath, "PROJECT:KuroNext", "demo-next");

        public static void Build(string outputPath) => Build(outputPath, Template, "demo-next");

        public static void Build(string outputPath, string template, string demoDir)
        {
            var oldTemplate = PlayerSettings.WebGL.template;
            PlayerSettings.WebGL.template = template;
            var oldCompression = PlayerSettings.WebGL.compressionFormat;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            string summary;
            try
            {
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/Scenes/Office.unity" },
                    locationPathName = outputPath,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None,
                });
                var s = report.summary;
                summary = $"{s.result} | ошибок {s.totalErrors} | {s.totalTime:mm\\:ss} | {s.totalSize / 1048576f:F1} МБ";
            }
            finally
            {
                PlayerSettings.WebGL.compressionFormat = oldCompression;
                PlayerSettings.WebGL.template = oldTemplate;
            }
            Directory.CreateDirectory(outputPath);
            // Метка сборки в адресах демо и моста: иначе браузер берет их старые копии из кэша.
            string stamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            string page = Path.Combine(outputPath, "index.html");
            if (File.Exists(page))
                File.WriteAllText(page, File.ReadAllText(page).Replace("src=\"demo/index.html\"", "src=\"demo/index.html?v=" + stamp + "\""));
            summary += " | демо: " + CopyDemo(outputPath, demoDir, stamp);
            File.WriteAllText(Path.Combine(outputPath, "build-result.txt"), summary);
            Debug.Log("WebGL: " + summary);
        }

        // Свежая копия <demoDir>/index.html (в билде — demo/index.html) с мостом: строка с window.kuroDemo открывает
        // состояние демо, дни и счетчик задач (они спрятаны в функции-обертке),
        // kuro-embed.js связывает с Unity.
        static string CopyDemo(string outputPath, string demoDir, string stamp)
        {
            string src = Path.GetFullPath(Path.Combine(Application.dataPath, "../../" + demoDir + "/index.html"));
            if (!File.Exists(src)) return "нет файла " + src;
            string html = File.ReadAllText(src);
            const string scriptEnd = "\n})();\n</script>";
            int i = html.LastIndexOf(scriptEnd, StringComparison.Ordinal);
            if (i < 0) return "не найден конец основного скрипта, мост не подключен";
            html = html.Insert(i + 1, "window.kuroDemo={get S(){return S;},DAYS:DAYS,openTasks:openTasks};\n");
            string tag = "<script src=\"kuro-embed.js?v=" + stamp + "\"></script>\n";
            int body = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
            html = body >= 0 ? html.Insert(body, tag) : html + tag;
            Directory.CreateDirectory(Path.Combine(outputPath, "demo"));
            File.WriteAllText(Path.Combine(outputPath, "demo", "index.html"), html);
            return "ок";
        }
    }
}

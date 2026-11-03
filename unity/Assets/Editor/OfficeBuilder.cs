using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Kuro.EditorTools
{
    // Собирает сцену Office: комната с окном и шторами, стол игрока, стол Марии, полка, архив,
    // календарь, часы, дверь и цех за стеклянной перегородкой. Модели — процедурные меши
    // (MeshKit: скругленные ребра, тела вращения, трубки), текстуры — TextureKit.
    // Живое: сквозняк шевелит бумаги, шторы и листья; телефон звонит, когда есть сообщения;
    // в лотке столько листов, сколько задач; часы идут; в цехе едет конвейер; за окном снег.
    // Меню Kuro → Собрать офис. Сцена пересоздается целиком — ручные правки в ней пропадут.
    // Оси: игрок смотрит в +Z, справа (+X) — цех, за спиной (−Z) — дверь.
    public static partial class OfficeBuilder
    {
        const string ScenePath = "Assets/Scenes/Office.unity";
        const string LightingPath = "Assets/Scenes/OfficeLighting.lighting";
        const string MaterialDir = "Assets/Materials";
        const string TextureDir = "Assets/Textures";
        const string UiDir = "Assets/UI";

        // Высота строки TextMesh при fontSize 96 и characterSize 1 (замерено во встроенном шрифте).
        const float LineHeight = 10.7f;

        // Глаза сидящего игрока.
        static readonly Vector3 Eye = new Vector3(0f, 1.2f, -0.55f);

        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        static Font font;
        static Material outline, worldText, aoBlob, aoEdge;
        static Texture2D blobTex;

        // Текстуры поверхностей (генерируются в начале сборки).
        static TextureKit.Set oak, walnut, darkWood, doorWood, parquet, plaster, ceilingTile, chairFabric,
            curtainFabric, tealFabric, concrete, brushed, carton;
        static Texture2D[] docs;
        static Texture2D desktop, phoneScreen, sky;

        [MenuItem("Kuro/Собрать офис")]
        public static void Build()
        {
            foreach (var dir in new[] { "Assets/Scenes", MaterialDir, TextureDir }) EnsureFolder(dir);
            materials.Clear();
            MeshKit.Reset();
            MakeTextures();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            outline = MaterialFor("Outline", Shader.Find("Kuro/Outline"));
            // Встроенный материал шрифта рисует надписи сквозь предметы — берем свой.
            worldText = MaterialFor("WorldText", Shader.Find("Kuro/WorldText"));
            worldText.mainTexture = font.material.mainTexture;
            aoBlob = MaterialFor("AO_Blob", Shader.Find("Kuro/AO"));
            aoBlob.mainTexture = blobTex;
            aoEdge = MaterialFor("AO_Edge", Shader.Find("Kuro/AO"));
            aoEdge.mainTexture = TextureKit.Gradient("EdgeShade");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupRendering();

            var office = Group("Office", null, Vector3.zero);
            BuildRoom(office);
            BuildWindow(office);
            var monitor = BuildPlayerDesk(office, out var chair);
            BuildMariaDesk(office);
            BuildShelf(office);
            BuildArchive(office);
            BuildCalendar(office);
            BuildClock(office);
            BuildDoor(office);
            BuildPartition(office);
            BuildWorkshop();
            BuildOutside();
            BuildLights();
            BuildArrival();
            BuildCamera(chair);
            BuildUi(monitor);
            MarkStatic();

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("Офис собран: " + ScenePath);
        }

        static void MakeTextures()
        {
            oak = TextureKit.Wood("Oak", "C49A6C", "8A5E38", 11);
            walnut = TextureKit.Wood("Walnut", "8C6440", "4F331E", 12, 14f);
            darkWood = TextureKit.Wood("DarkWood", "6E4E36", "3A2617", 13);
            doorWood = TextureKit.Wood("DoorWood", "9A6E44", "5E3E22", 14, 10f);
            parquet = TextureKit.Parquet("Parquet", "B0825A", "6E4A2C");
            plaster = TextureKit.Plaster("Plaster", "FFFFFF", 21);
            ceilingTile = TextureKit.CeilingTile("CeilingTile");
            chairFabric = TextureKit.Fabric("ChairFabric", "FFFFFF", 31);
            curtainFabric = TextureKit.Fabric("CurtainFabric", "FFFFFF", 32);
            tealFabric = TextureKit.Fabric("TealFabric", "FFFFFF", 33);
            concrete = TextureKit.Concrete("Concrete", "9A9C99");
            brushed = TextureKit.Brushed("Brushed", "FFFFFF");
            carton = TextureKit.Carton("Carton", "B08A5E");
            docs = new Texture2D[4];
            for (int i = 0; i < docs.Length; i++) docs[i] = TextureKit.Document("Doc_" + i, i);
            desktop = TextureKit.Desktop("Desktop");
            phoneScreen = TextureKit.PhoneScreen("PhoneScreen");
            sky = TextureKit.Sky("Sky");
            blobTex = TextureKit.Blob("Blob");
        }

        // ---------- рендер ----------

        static void SetupRendering()
        {
            PlayerSettings.colorSpace = ColorSpace.Linear;

            var lighting = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingPath);
            if (!lighting)
            {
                lighting = new LightingSettings { name = "OfficeLighting" };
                AssetDatabase.CreateAsset(lighting, LightingPath);
            }
            lighting.bakedGI = false;
            lighting.realtimeGI = false;
            Lightmapping.lightingSettings = lighting;

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex("A9A49C");
            RenderSettings.ambientEquatorColor = Hex("8E847A");
            RenderSettings.ambientGroundColor = Hex("6E6052");
            RenderSettings.fog = false;

            QualitySettings.pixelLightCount = 8;
            QualitySettings.shadowDistance = 16f;
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            QualitySettings.shadowCascades = 2;
            QualitySettings.antiAliasing = 4;
        }

        // ---------- комната ----------

        static void BuildRoom(Transform office)
        {
            var wall = Surface("Wall", plaster, "E9D8BE", 0.08f, 1f / 1.5f, bump: 0.6f);
            var panel = Surface("WallPanel", plaster, "D3BD9C", 0.35f, 1f, bump: 0.3f);
            var trim = Surface("Trim", darkWood, "FFFFFF", 0.45f, 1f);
            var ceiling = Surface("Ceiling", ceilingTile, "FFFFFF", 0.05f, 1f / 0.6f);
            var floor = Surface("Floor", parquet, "FFFFFF", 0.42f, 1f);
            var baseboard = Mat("Baseboard", "F4EFE6", 0.4f);

            var room = Group("Room", office, Vector3.zero);
            // Пол и потолок заходят под стены, стены — до верха потолка: иначе в углах просвечивает солнце.
            Slab("Floor", room, new Vector3(-0.5f, -0.05f, 0.5f), new Vector3(6.4f, 0.1f, 6.4f), floor);
            Slab("Ceiling", room, new Vector3(-0.5f, 2.95f, 0.5f), new Vector3(6.4f, 0.1f, 6.4f), ceiling);
            Slab("Wall_Left", room, new Vector3(-3.55f, 1.5f, 0.5f), new Vector3(0.1f, 3f, 6.2f), wall);
            Slab("Wall_Back", room, new Vector3(-0.5f, 1.5f, -2.55f), new Vector3(6.2f, 3f, 0.1f), wall);
            // Передняя стена с проемом окна: x −2.4…−0.8, y 1.0…2.2.
            Slab("Wall_Front_Low", room, new Vector3(-0.5f, 0.5f, 3.55f), new Vector3(6.2f, 1f, 0.1f), wall);
            Slab("Wall_Front_High", room, new Vector3(-0.5f, 2.6f, 3.55f), new Vector3(6.2f, 0.8f, 0.1f), wall);
            Slab("Wall_Front_L", room, new Vector3(-3f, 1.6f, 3.55f), new Vector3(1.2f, 1.2f, 0.1f), wall);
            Slab("Wall_Front_R", room, new Vector3(0.9f, 1.6f, 3.55f), new Vector3(3.4f, 1.2f, 0.1f), wall);
            // Откосы окна.
            Slab("Reveal_L", room, new Vector3(-2.42f, 1.6f, 3.55f), new Vector3(0.04f, 1.2f, 0.1f), wall);
            Slab("Reveal_R", room, new Vector3(-0.78f, 1.6f, 3.55f), new Vector3(0.04f, 1.2f, 0.1f), wall);
            Slab("Reveal_Top", room, new Vector3(-1.6f, 2.18f, 3.55f), new Vector3(1.6f, 0.04f, 0.1f), wall);

            // Панели из вагонки до 0,9 м, планка над ними, плинтус.
            Part("Panel_Left", room, Beadboard(6f), panel, new Vector3(-3.494f, 0.45f, 0.5f), new Vector3(0f, -90f, 0f));
            Part("Panel_Front", room, Beadboard(6f), panel, new Vector3(-0.5f, 0.45f, 3.494f), new Vector3(0f, 180f, 0f));
            Part("Panel_Back", room, Beadboard(6f), panel, new Vector3(-0.5f, 0.45f, -2.494f));
            Box("Rail_Left", room, new Vector3(-3.475f, 0.915f, 0.5f), new Vector3(0.05f, 0.035f, 6f), trim, bevel: 0.01f);
            Box("Rail_Front", room, new Vector3(-0.5f, 0.915f, 3.475f), new Vector3(6f, 0.035f, 0.05f), trim, bevel: 0.01f);
            Box("Rail_Back", room, new Vector3(-0.5f, 0.915f, -2.475f), new Vector3(6f, 0.035f, 0.05f), trim, bevel: 0.01f);
            Box("Plinth_Left", room, new Vector3(-3.47f, 0.04f, 0.5f), new Vector3(0.04f, 0.08f, 6f), baseboard, bevel: 0.008f);
            Box("Plinth_Front", room, new Vector3(-0.5f, 0.04f, 3.47f), new Vector3(6f, 0.08f, 0.04f), baseboard, bevel: 0.008f);
            Box("Plinth_Back", room, new Vector3(-0.5f, 0.04f, -2.47f), new Vector3(6f, 0.08f, 0.04f), baseboard, bevel: 0.008f);

            // Затенение по углам комнаты.
            Shade(room, new Vector3(-3.5f, 0f, -2.5f), new Vector3(-3.5f, 0f, 3.5f), Vector3.right, Vector3.up, 0.35f, 0.55f);
            Shade(room, new Vector3(-3.5f, 0f, 3.5f), new Vector3(2.5f, 0f, 3.5f), Vector3.back, Vector3.up, 0.35f, 0.55f);
            Shade(room, new Vector3(2.46f, 0f, -2.5f), new Vector3(2.46f, 0f, 3.5f), Vector3.left, Vector3.up, 0.3f, 0.45f);
            Shade(room, new Vector3(-3.5f, 2.9f, -2.5f), new Vector3(-3.5f, 2.9f, 3.5f), Vector3.right, Vector3.down, 0.5f, 0.45f);
            Shade(room, new Vector3(-3.5f, 2.9f, 3.5f), new Vector3(2.5f, 2.9f, 3.5f), Vector3.back, Vector3.down, 0.5f, 0.45f);
            Shade(room, new Vector3(-3.49f, 2.9f, -2.5f), new Vector3(-3.49f, 2.9f, 3.5f), Vector3.down, Vector3.right, 0.45f, 0.4f);
            Shade(room, new Vector3(-3.5f, 2.9f, 3.49f), new Vector3(2.5f, 2.9f, 3.49f), Vector3.down, Vector3.back, 0.45f, 0.4f);
            Shade(room, new Vector3(-3.49f, 0f, 3.49f), new Vector3(-3.49f, 2.9f, 3.49f), Vector3.right, Vector3.back, 0.3f, 0.4f);
            Shade(room, new Vector3(-3.49f, 0f, 3.49f), new Vector3(-3.49f, 2.9f, 3.49f), Vector3.back, Vector3.right, 0.3f, 0.4f);

            // Потолочные светильники 600×600 в сетке плитки.
            var frame = Mat("LampFrame", "F2F0EC", 0.5f);
            var diffuser = Emissive("LampWarm", "FFF3DC", "FFE2B0", 2.2f);
            foreach (var p in new[] { new Vector3(0f, 2.9f, 0.3f), new Vector3(-2f, 2.9f, 1.2f), new Vector3(0.4f, 2.9f, -1.6f) })
            {
                var lamp = Group("Lamp", room, p);
                Box("Frame", lamp, new Vector3(0f, -0.012f, 0f), new Vector3(0.6f, 0.024f, 0.6f), frame, bevel: 0.006f);
                Box("Diffuser", lamp, new Vector3(0f, -0.022f, 0f), new Vector3(0.56f, 0.01f, 0.56f), diffuser, bevel: 0.003f);
            }

            // Вывеска на задней стене.
            var sign = Group("Sign_Logo", room, new Vector3(-1.5f, 1.85f, -2.5f), 180f);
            Box("Board", sign, new Vector3(0f, 0f, -0.012f), new Vector3(1.1f, 0.42f, 0.024f), Mat("Teal", "1F6F68", 0.3f), bevel: 0.01f);
            Text("Title", sign, new Vector3(0f, 0.06f, -0.026f), "КУРО", 0.16f, "FFF6E6", style: FontStyle.Bold);
            Text("Subtitle", sign, new Vector3(0f, -0.1f, -0.026f), "ПРОДАКШН", 0.09f, "48C6B4");

            // Свидетельство о регистрации в рамке над столом Марии.
            var cert = Group("Certificate", room, new Vector3(-3.49f, 1.75f, -0.95f), -90f);
            Box("Frame", cert, new Vector3(0f, 0f, -0.012f), new Vector3(0.5f, 0.38f, 0.024f), Mat("FrameBlack", "23201D", 0.5f), bevel: 0.006f);
            var certPaper = Mat("CertPaper", "FFFFFF", 0.15f);
            certPaper.mainTexture = docs[3];
            Part("Paper", cert, MeshKit.Sheet(0.42f, 0.3f, 1, 1), certPaper, new Vector3(0f, 0f, -0.0245f), new Vector3(-90f, 0f, 0f), collide: false);
            NoShadows(Box("Glass", cert, new Vector3(0f, 0f, -0.027f), new Vector3(0.46f, 0.34f, 0.002f), Glass("Glass", new Color(0.8f, 0.9f, 0.95f, 0.12f)), bevel: 0f));

            // Выключатель у двери.
            Box("Switch", room, new Vector3(-3.49f, 1.05f, 1.9f), new Vector3(0.012f, 0.085f, 0.085f), Mat("PlasticLight", "E7E9E8", 0.4f), bevel: 0.004f);
            Box("Rocker", room, new Vector3(-3.482f, 1.05f, 1.9f), new Vector3(0.008f, 0.045f, 0.03f), Mat("PlasticLight", "E7E9E8", 0.4f), bevel: 0.003f);

            // Кулер в углу у двери.
            var cooler = Group("Cooler", room, new Vector3(-2.9f, 0f, -2.15f), 180f);
            Box("Body", cooler, new Vector3(0f, 0.5f, 0f), new Vector3(0.32f, 1f, 0.32f), Mat("PlasticLight", "E7E9E8", 0.4f), bevel: 0.03f);
            Box("Tap", cooler, new Vector3(-0.05f, 0.82f, -0.17f), new Vector3(0.04f, 0.05f, 0.04f), Mat("Blue", "2F5E8C", 0.5f), bevel: 0.008f);
            Box("Tap_Hot", cooler, new Vector3(0.05f, 0.82f, -0.17f), new Vector3(0.04f, 0.05f, 0.04f), Mat("Red", "C4453B", 0.5f), bevel: 0.008f);
            Lathe("Bottle", cooler, "CoolerBottle", new[]
            {
                V(0f, 0f), V(0.03f, 0f), V(0.03f, 0.04f), V(0.12f, 0.1f), V(0.135f, 0.14f), V(0.135f, 0.2f), V(0.128f, 0.22f),
                V(0.135f, 0.24f), V(0.135f, 0.32f), V(0.128f, 0.34f), V(0.135f, 0.36f), V(0.135f, 0.44f), V(0.11f, 0.48f), V(0f, 0.49f),
            }, Glass("Water", new Color(0.55f, 0.75f, 0.95f, 0.45f)), new Vector3(0f, 1.48f, 0f), new Vector3(180f, 0f, 0f), 40);
        }

        // Вагонка: доски 10 см с фаской, одна полоса длиной length вдоль локальной X, лицом в −Z.
        static Mesh Beadboard(float length)
        {
            return MeshKit.Get(MeshKit.Key("Beadboard", length), s =>
            {
                var board = MeshKit.RoundedBox(new Vector3(0.097f, 0.9f, 0.012f), 0.004f, 2);
                int count = Mathf.RoundToInt(length / 0.1f);
                for (int i = 0; i < count; i++)
                    s.At(new Vector3(-length / 2f + 0.05f + i * 0.1f, 0f, 0f)).Add(board);
            });
        }

        static void BuildWindow(Transform office)
        {
            var frame = Mat("WindowFrame", "F7F3EC", 0.55f);
            var glass = Glass("Glass", new Color(0.8f, 0.9f, 0.95f, 0.1f));
            var w = Group("Window", office, new Vector3(-1.6f, 1.6f, 3.55f));
            Box("Frame_Top", w, new Vector3(0f, 0.57f, 0f), new Vector3(1.6f, 0.06f, 0.12f), frame, bevel: 0.01f);
            Box("Frame_Bottom", w, new Vector3(0f, -0.57f, 0f), new Vector3(1.6f, 0.06f, 0.12f), frame, bevel: 0.01f);
            Box("Frame_L", w, new Vector3(-0.77f, 0f, 0f), new Vector3(0.06f, 1.2f, 0.12f), frame, bevel: 0.01f);
            Box("Frame_R", w, new Vector3(0.77f, 0f, 0f), new Vector3(0.06f, 1.2f, 0.12f), frame, bevel: 0.01f);
            Box("Mullion_V", w, new Vector3(0f, 0f, -0.01f), new Vector3(0.07f, 1.14f, 0.09f), frame, bevel: 0.01f);
            Box("Mullion_H", w, new Vector3(0f, 0.15f, -0.01f), new Vector3(1.5f, 0.07f, 0.09f), frame, bevel: 0.01f);

            // Нижние створки: рама створки и двойное стекло.
            foreach (float x in new[] { -0.385f, 0.385f })
                Sash(w, new Vector3(x, -0.2f, -0.01f), new Vector2(0.7f, 0.72f), frame, glass);
            // Верхняя правая — глухая, верхняя левая — форточка на проветривании: откуда сквозняк.
            Sash(w, new Vector3(0.385f, 0.375f, -0.01f), new Vector2(0.7f, 0.38f), frame, glass);
            var hinge = Group("Transom", w, new Vector3(-0.385f, 0.185f, -0.03f));
            hinge.localEulerAngles = new Vector3(-11f, 0f, 0f);
            Sash(hinge, new Vector3(0f, 0.19f, 0.02f), new Vector2(0.7f, 0.38f), frame, glass);

            // Ручки створок.
            var handleMat = Mat("Chrome", "C9CED0", 0.85f, 0.9f);
            Cyl("Handle_Base", w, new Vector3(0.04f, -0.2f, -0.07f), 0.012f, 0.012f, handleMat, new Vector3(90f, 0f, 0f));
            Tube("Handle", w, "WindowHandle", new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, -0.02f), new Vector3(0f, -0.09f, -0.025f) }, 0.006f, handleMat, new Vector3(0.04f, -0.2f, -0.07f));

            Box("Sill", w, new Vector3(0f, -0.62f, -0.13f), new Vector3(1.8f, 0.035f, 0.3f), Mat("Sill", "FAF8F4", 0.6f), bevel: 0.012f);

            // Кактус на подоконнике.
            var pot = Group("Cactus", w, new Vector3(0.55f, -0.6f, -0.14f));
            Lathe("Pot", pot, "SmallPot", new[] { V(0f, 0f), V(0.035f, 0f), V(0.045f, 0.07f), V(0.05f, 0.075f), V(0.05f, 0.085f), V(0.043f, 0.085f), V(0.043f, 0.07f), V(0f, 0.07f) },
                Mat("Terracotta", "B5654A", 0.25f), Vector3.zero, Vector3.zero, 24);
            Ball("Cactus", pot, new Vector3(0f, 0.12f, 0f), new Vector3(0.06f, 0.1f, 0.06f), Mat("CactusGreen", "4E7A3E", 0.35f));
            Ball("Cactus_Arm", pot, new Vector3(0.03f, 0.13f, 0f), new Vector3(0.03f, 0.05f, 0.03f), Mat("CactusGreen", "4E7A3E", 0.35f));

            // Шторы на карнизе, колышутся от сквозняка.
            var rodMat = Mat("Brass", "C9A452", 0.75f, 0.8f);
            Tube("Rod", office, "CurtainRod", new[] { new Vector3(-1.1f, 0f, 0f), new Vector3(1.1f, 0f, 0f) }, 0.012f, rodMat, new Vector3(-1.6f, 2.36f, 3.25f));
            foreach (float x in new[] { -1.12f, 1.12f })
                Ball("Finial", office, new Vector3(-1.6f + x, 2.36f, 3.25f), Vector3.one * 0.04f, rodMat);
            var curtain = Surface("Curtain", curtainFabric, "E6D7BC", 0.12f, 1f);
            curtain.mainTextureScale = new Vector2(6f, 10f);
            foreach (var (x, phase) in new[] { (-2.38f, 1f), (-0.82f, 6f) })
            {
                var c = Part("Curtain", office, MeshKit.Sheet(0.5f, 1.72f, 16, 22), curtain, new Vector3(x, 1.5f, 3.24f), new Vector3(-90f, 0f, 0f), collide: false);
                var sheet = c.AddComponent<WindSheet>();
                sheet.cloth = true;
                sheet.lift = 0.09f;
                sheet.folds = 0.022f;
                sheet.foldCount = 4f;
                sheet.phase = phase;
            }

            // Радиатор из секций.
            var radiator = Group("Radiator", office, new Vector3(-1.6f, 0.55f, 3.42f));
            Part("Sections", radiator, MeshKit.Get("Radiator", s =>
            {
                var section = MeshKit.RoundedBox(new Vector3(0.07f, 0.5f, 0.08f), 0.025f, 3);
                for (int i = 0; i < 10; i++) s.At(new Vector3(-0.405f + i * 0.09f, 0f, 0f)).Add(section);
                s.At(Vector3.zero).Tube(new[] { new Vector3(-0.45f, 0.21f, 0f), new Vector3(0.45f, 0.21f, 0f) }, 0.018f, 12);
                s.At(Vector3.zero).Tube(new[] { new Vector3(-0.45f, -0.21f, 0f), new Vector3(0.45f, -0.21f, 0f) }, 0.018f, 12);
            }), Mat("Radiator", "EDEBE6", 0.55f), Vector3.zero);
            Tube("Pipe", radiator, "RadiatorPipe", new[] { new Vector3(0.45f, -0.21f, 0f), new Vector3(0.52f, -0.21f, 0f), new Vector3(0.52f, -0.56f, 0f) }, 0.012f, Mat("Radiator", "EDEBE6", 0.55f), Vector3.zero);
        }

        // Створка окна: рама из профиля и двойное стекло.
        static void Sash(Transform parent, Vector3 pos, Vector2 size, Material frame, Material glass)
        {
            var s = Group("Sash", parent, pos);
            const float p = 0.05f, d = 0.06f;
            Box("Top", s, new Vector3(0f, size.y / 2f - p / 2f, 0f), new Vector3(size.x, p, d), frame, bevel: 0.008f);
            Box("Bottom", s, new Vector3(0f, -size.y / 2f + p / 2f, 0f), new Vector3(size.x, p, d), frame, bevel: 0.008f);
            Box("Left", s, new Vector3(-size.x / 2f + p / 2f, 0f, 0f), new Vector3(p, size.y - 2f * p, d), frame, bevel: 0.008f);
            Box("Right", s, new Vector3(size.x / 2f - p / 2f, 0f, 0f), new Vector3(p, size.y - 2f * p, d), frame, bevel: 0.008f);
            foreach (float z in new[] { -0.012f, 0.012f })
                NoShadows(Box("Glass", s, new Vector3(0f, 0f, z), new Vector3(size.x - 2f * p + 0.01f, size.y - 2f * p + 0.01f, 0.004f), glass, bevel: 0f));
        }

        // ---------- стол игрока ----------

        static Interactable BuildPlayerDesk(Transform office, out Transform chair)
        {
            var wood = Surface("DeskWood", oak, "FFFFFF", 0.35f, 1f);
            var woodDark = Surface("DeskWoodDark", darkWood, "FFFFFF", 0.4f, 1f);
            var plastic = Mat("PlasticDark", "3A4045", 0.45f);
            var keysMat = Mat("Keys", "50575D", 0.35f);
            var paper = Mat("Paper", "FBF8F1", 0.1f);
            var mustard = Mat("Mustard", "E2A93B", 0.55f);
            var ink = Mat("Ink", "1E1B18", 0.4f);

            var desk = Group("Desk_Player", office, new Vector3(0f, 0f, 0.25f));
            DeskModel(desk, new Vector2(1.6f, 0.8f), wood, woodDark);
            // Кожаный коврик под клавиатурой и мышью.
            Box("DeskPad", desk, new Vector3(0.08f, 0.7515f, -0.12f), new Vector3(0.9f, 0.003f, 0.34f), Mat("Leather", "2E2A27", 0.35f), bevel: 0.0015f);
            KeyboardModel(desk, new Vector3(0f, 0.753f, -0.12f), plastic, keysMat);
            Blob(desk, desk.TransformPoint(0f, 0.75f, -0.11f), new Vector2(0.5f, 0.2f), 0.35f);

            // Мышь и провод к монитору.
            var mouse = Group("Mouse", desk, new Vector3(0.32f, 0.753f, -0.13f));
            Ball("Body", mouse, new Vector3(0f, 0.008f, 0f), new Vector3(0.058f, 0.034f, 0.1f), plastic);
            Cyl("Wheel", mouse, new Vector3(0f, 0.024f, 0.022f), 0.006f, 0.005f, ink, new Vector3(0f, 0f, 90f));
            Box("Split", mouse, new Vector3(0f, 0.0248f, 0.03f), new Vector3(0.001f, 0.001f, 0.035f), ink, bevel: 0f);
            Tube("Cable", desk, "MouseCable", new[]
            {
                new Vector3(0.32f, 0.758f, -0.08f), new Vector3(0.32f, 0.756f, -0.04f), new Vector3(0.3f, 0.754f, 0.05f),
                new Vector3(0.24f, 0.754f, 0.16f), new Vector3(0.12f, 0.754f, 0.24f),
            }, 0.0022f, ink, Vector3.zero, 8);
            Blob(desk, mouse.position, new Vector2(0.08f, 0.12f), 0.4f);

            // Кружка с кофе: пар поднимается.
            var mug = Group("Mug", desk, new Vector3(0.62f, 0.75f, -0.15f), 140f);
            Lathe("Body", mug, "Mug", new[]
            {
                V(0f, 0f), V(0.034f, 0f), V(0.038f, 0.003f), V(0.039f, 0.006f), V(0.041f, 0.09f), V(0.042f, 0.097f), V(0.04f, 0.1f),
                V(0.037f, 0.098f), V(0.036f, 0.092f), V(0.035f, 0.012f), V(0f, 0.01f),
            }, mustard, Vector3.zero, Vector3.zero, 40);
            Tube("Handle", mug, "MugHandle", MeshKit.Arc(new Vector3(0.042f, 0.052f, 0f), 0.026f, -75f, 75f, 16), 0.0055f, mustard, Vector3.zero);
            Cyl("Coffee", mug, new Vector3(0f, 0.08f, 0f), 0.036f, 0.002f, Mat("Coffee", "2E1C10", 0.9f), bevel: 0f);
            Blob(desk, mug.position, new Vector2(0.11f, 0.11f), 0.45f);
            var steam = Particles("Steam", mug, new Vector3(0f, 0.085f, 0f), ParticleMat("Steam", new Color(1f, 1f, 1f, 0.8f)));
            SetupSteam(steam);

            // Стакан с ручками в дальнем левом углу.
            var cup = Group("PenCup", desk, new Vector3(-0.68f, 0.75f, 0.3f));
            Lathe("Cup", cup, "PenCup", new[] { V(0f, 0f), V(0.032f, 0f), V(0.034f, 0.1f), V(0.03f, 0.1f), V(0.029f, 0.006f), V(0f, 0.006f) },
                Mat("MetalDark", "2A2F33", 0.55f, 0.5f), Vector3.zero, Vector3.zero, 32);
            string[] penColors = { "2F5E8C", "C4453B", "1E1B18", "E2A93B" };
            for (int i = 0; i < penColors.Length; i++)
            {
                float a = i * 1.7f;
                Cyl("Pen", cup, new Vector3(Mathf.Cos(a) * 0.014f, 0.1f, Mathf.Sin(a) * 0.014f), 0.004f, 0.14f,
                    Mat("Pen_" + penColors[i], penColors[i], 0.55f), new Vector3(Mathf.Sin(a) * 9f, 0f, -Mathf.Cos(a) * 9f));
            }
            Blob(desk, cup.position, new Vector2(0.1f, 0.1f), 0.45f);

            // Лист на столе у монитора: сквозняк приподнимает край.
            var loose = Part("LooseDoc", desk, MeshKit.Sheet(0.205f, 0.29f, 8, 12), DocMat(1), new Vector3(-0.27f, 0.7517f, 0.17f), new Vector3(0f, 18f, 0f), collide: false);
            loose.AddComponent<WindSheet>().lift = 0.035f;

            // Монитор: экран смотрит на игрока (−Z), на нем рабочий стол «КУРО OS».
            var mon = Group("Monitor", office, new Vector3(0f, 0.75f, 0.47f));
            MonitorModel(mon, 1f, true);
            // Стикер на рамке монитора трепещет на сквозняке.
            var sticky = Part("Sticky", mon, MeshKit.Sheet(0.06f, 0.06f, 6, 6), Mat("Sticky", "F5D76E", 0.1f), new Vector3(0.27f, 0.468f, -0.0165f), new Vector3(-90f, 0f, 6f), collide: false);
            var stickyWind = sticky.AddComponent<WindSheet>();
            stickyWind.lift = 0.012f;
            var monitor = AddInteractable(mon, "Компьютер", "pc", FocusFrom(mon, mon.TransformPoint(0f, 0.32f, -0.02f), 0f, 0f, 0.36f));
            Tube("Cable", desk, "MonitorCable", new[]
            {
                new Vector3(0f, 0.92f, 0.27f), new Vector3(0f, 0.86f, 0.3f), new Vector3(0.05f, 0.77f, 0.32f), new Vector3(0.1f, 0.752f, 0.33f), new Vector3(0.16f, 0.752f, 0.36f),
            }, 0.003f, ink, Vector3.zero, 8);
            Blob(desk, mon.TransformPoint(0f, 0f, 0.03f), new Vector2(0.34f, 0.26f), 0.4f);

            // Лоток «Входящие»: листов столько, сколько задач (InboxStack), верхний шевелит сквозняк.
            var inbox = Group("Inbox", office, new Vector3(0.6f, 0.75f, 0.4f), -12f);
            Box("Base", inbox, new Vector3(0f, 0.004f, 0f), new Vector3(0.34f, 0.008f, 0.26f), plastic, bevel: 0.003f);
            Box("Wall_Back", inbox, new Vector3(0f, 0.035f, 0.125f), new Vector3(0.34f, 0.07f, 0.01f), plastic, bevel: 0.004f);
            Box("Wall_L", inbox, new Vector3(-0.165f, 0.03f, 0f), new Vector3(0.01f, 0.06f, 0.26f), plastic, bevel: 0.004f);
            Box("Wall_R", inbox, new Vector3(0.165f, 0.03f, 0f), new Vector3(0.01f, 0.06f, 0.26f), plastic, bevel: 0.004f);
            Box("Wall_Front", inbox, new Vector3(0f, 0.018f, -0.125f), new Vector3(0.34f, 0.036f, 0.01f), plastic, bevel: 0.004f);
            var stack = new Transform[6];
            for (int i = 0; i < stack.Length; i++)
            {
                var sheet = Part("Paper", inbox, MeshKit.Sheet(0.29f, 0.205f, 10, 8), DocMat(i % docs.Length),
                    new Vector3(0.003f * i, 0.0095f + 0.0024f * i, 0.004f * i - 0.008f), new Vector3(0f, i % 2 == 0 ? 3f : -4f, 0f), collide: false);
                sheet.AddComponent<WindSheet>().lift = 0.022f;
                stack[i] = sheet.transform;
            }
            inbox.gameObject.AddComponent<InboxStack>().papers = stack;
            Box("Sticker", inbox, new Vector3(0f, 0.02f, -0.1312f), new Vector3(0.13f, 0.024f, 0.002f), mustard, bevel: 0.001f);
            Text("Label", inbox, new Vector3(0f, 0.02f, -0.1335f), "ВХОДЯЩИЕ", 0.018f, "1E1B18", style: FontStyle.Bold);
            AddInteractable(inbox, "Входящие", "tray", FocusFrom(inbox, inbox.TransformPoint(0f, 0.02f, 0f), 15f, 55f, 0.5f));
            Blob(desk, inbox.position, new Vector2(0.42f, 0.34f), 0.4f, -12f);

            // Дневник: твердая обложка, резинка, закладка, ручка.
            var book = Group("Notebook", office, new Vector3(-0.4f, 0.75f, 0.15f), 8f);
            var cover = Surface("NotebookCover", tealFabric, "1F6F68", 0.3f, 1f);
            cover.mainTextureScale = new Vector2(3f, 3f);
            Box("Cover_Bottom", book, new Vector3(0f, 0.0015f, 0f), new Vector3(0.18f, 0.003f, 0.245f), cover, bevel: 0.0014f);
            Box("Pages", book, new Vector3(0.003f, 0.0105f, 0f), new Vector3(0.172f, 0.015f, 0.237f), Mat("PageEdge", "F3EDDF", 0.1f), bevel: 0.002f);
            Box("Cover", book, new Vector3(0f, 0.0195f, 0f), new Vector3(0.18f, 0.003f, 0.245f), cover, bevel: 0.0014f);
            Box("Spine", book, new Vector3(-0.088f, 0.0105f, 0f), new Vector3(0.008f, 0.021f, 0.245f), cover, bevel: 0.004f);
            Box("Band", book, new Vector3(0.065f, 0.0105f, 0f), new Vector3(0.007f, 0.0225f, 0.247f), ink, bevel: 0.002f);
            var ribbon = Part("Ribbon", book, MeshKit.Sheet(0.007f, 0.07f, 1, 8), Mat("Red", "C4453B", 0.5f), new Vector3(-0.02f, 0.004f, -0.155f), Vector3.zero, collide: false);
            ribbon.AddComponent<WindSheet>().lift = 0.01f;
            Box("Sticker", book, new Vector3(-0.01f, 0.0212f, 0.06f), new Vector3(0.11f, 0.0005f, 0.04f), paper, bevel: 0f);
            Text("Label", book, new Vector3(-0.01f, 0.0218f, 0.06f), "ДНЕВНИК", 0.02f, "1E1B18", new Vector3(90f, 0f, 0f), FontStyle.Bold);
            var pen = Group("Pen", book, new Vector3(0.118f, 0.006f, -0.01f));
            pen.localEulerAngles = new Vector3(90f, 0f, 0f);
            Lathe("Body", pen, "Pen", new[]
            {
                V(0f, -0.072f), V(0.0012f, -0.068f), V(0.0025f, -0.062f), V(0.0042f, -0.055f), V(0.0045f, -0.05f), V(0.0045f, 0.06f), V(0.004f, 0.068f), V(0f, 0.07f),
            }, Mat("Red", "C4453B", 0.5f), Vector3.zero, Vector3.zero, 16);
            Box("Clip", pen, new Vector3(0f, 0.045f, -0.005f), new Vector3(0.002f, 0.04f, 0.0015f), Mat("Chrome", "C9CED0", 0.85f, 0.9f), bevel: 0.0006f);
            AddInteractable(book, "Дневник", "book", FocusFrom(book, book.TransformPoint(0f, 0.02f, 0f), -10f, 62f, 0.42f));
            Blob(desk, book.position, new Vector2(0.26f, 0.32f), 0.4f, 8f);

            // Телефон: звонит и горит, когда есть непрочитанные сообщения (PhoneAlert).
            var phone = Group("Phone", office, new Vector3(-0.66f, 0.75f, 0.4f), -15f);
            var phoneBody = Group("Body", phone, Vector3.zero);
            Box("Case", phoneBody, new Vector3(0f, 0.00425f, 0f), new Vector3(0.076f, 0.0085f, 0.158f), Mat("Graphite", "26292C", 0.6f, 0.4f), bevel: 0.0042f);
            var screenMat = Emissive("PhoneScreen", "05080B", "FFFFFF", 1f);
            screenMat.mainTexture = phoneScreen;
            screenMat.SetTexture("_EmissionMap", phoneScreen);
            var screen = Part("Screen", phoneBody, MeshKit.Sheet(0.07f, 0.15f, 1, 1), screenMat, new Vector3(0f, 0.0087f, 0f));
            var notice = Part("Notice", phoneBody, MeshKit.RoundedBox(new Vector3(0.058f, 0.0005f, 0.022f), 0.00025f, 2), Emissive("PhoneNotice", "E2A93B", "E2A93B", 1f), new Vector3(0f, 0.0091f, 0.035f));
            Box("Camera", phoneBody, new Vector3(0f, 0.009f, 0.071f), new Vector3(0.005f, 0.0004f, 0.005f), ink, bevel: 0.0002f);
            Box("Button", phoneBody, new Vector3(0.0385f, 0.0045f, 0.03f), new Vector3(0.0015f, 0.003f, 0.02f), Mat("Graphite", "26292C", 0.6f, 0.4f), bevel: 0.0007f);
            var glowGo = Group("Glow", phone, new Vector3(0f, 0.06f, 0f));
            var glow = glowGo.gameObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.range = 0.6f;
            glow.intensity = 1.6f;
            glow.renderMode = LightRenderMode.ForcePixel;
            glow.color = Hex("9FD8FF");
            glow.shadows = LightShadows.None;
            var alert = phone.gameObject.AddComponent<PhoneAlert>();
            alert.body = phoneBody;
            alert.screen = screen.GetComponent<Renderer>();
            alert.notice = notice.GetComponent<Renderer>();
            alert.glow = glow;
            AddInteractable(phone, "Телефон", "phone", FocusFrom(phone, phone.TransformPoint(0f, 0.01f, 0f), -20f, 65f, 0.32f), padding: 0.03f);
            Blob(desk, phone.position, new Vector2(0.11f, 0.2f), 0.45f, -15f);

            // Кресло поворачивается вместе с игроком (SeatedLook.chair).
            chair = Group("Chair_Player", office, new Vector3(Eye.x, 0f, Eye.z));
            ChairModel(chair);
            StripColliders(chair);
            Blob(office, new Vector3(Eye.x, 0f, Eye.z), new Vector2(0.75f, 0.75f), 0.35f);
            return monitor;
        }

        // Стол: столешница с фаской, боковины, царга. Верх столешницы на высоте 0,75.
        static void DeskModel(Transform desk, Vector2 size, Material top, Material side)
        {
            Box("Top", desk, new Vector3(0f, 0.7325f, 0f), new Vector3(size.x, 0.035f, size.y), top, bevel: 0.008f);
            Box("Side_L", desk, new Vector3(-size.x / 2f + 0.03f, 0.357f, 0f), new Vector3(0.04f, 0.715f, size.y - 0.04f), side, bevel: 0.006f);
            Box("Side_R", desk, new Vector3(size.x / 2f - 0.03f, 0.357f, 0f), new Vector3(0.04f, 0.715f, size.y - 0.04f), side, bevel: 0.006f);
            Box("Modesty", desk, new Vector3(0f, 0.45f, size.y / 2f - 0.04f), new Vector3(size.x - 0.1f, 0.5f, 0.02f), side, bevel: 0.004f);
            foreach (float x in new[] { -1f, 1f })
                foreach (float z in new[] { -1f, 1f })
                    Cyl("Glide", desk, new Vector3(x * (size.x / 2f - 0.03f), 0.004f, z * (size.y / 2f - 0.06f)), 0.012f, 0.008f, Mat("PlasticDark", "3A4045", 0.45f));
            Blob(desk, desk.TransformPoint(0f, 0f, 0f), new Vector2(size.x + 0.25f, size.y + 0.25f), 0.35f);
            // Кабель-канал: отверстие в столешнице.
            Lathe("Grommet", desk, "Grommet", new[] { V(0f, 0f), V(0.025f, 0f), V(0.028f, 0.002f), V(0.03f, 0.002f) },
                Mat("PlasticDark", "3A4045", 0.45f), new Vector3(0.16f, 0.7505f, size.y / 2f - 0.05f), Vector3.zero, 24);
        }

        static void KeyboardModel(Transform parent, Vector3 pos, Material body, Material keys)
        {
            var kb = Group("Keyboard", parent, pos);
            kb.localEulerAngles = new Vector3(-2.5f, 0f, 0f);
            Box("Base", kb, new Vector3(0f, 0.008f, 0f), new Vector3(0.44f, 0.016f, 0.15f), body, bevel: 0.005f);
            Part("Keys", kb, MeshKit.Get("KeyboardKeys", s =>
            {
                var cap = MeshKit.RoundedBox(new Vector3(0.0165f, 0.007f, 0.0165f), 0.003f, 2);
                var wide = MeshKit.RoundedBox(new Vector3(0.026f, 0.007f, 0.0165f), 0.003f, 2);
                var space = MeshKit.RoundedBox(new Vector3(0.115f, 0.007f, 0.0165f), 0.003f, 2);
                const float step = 0.0192f;
                float[] stagger = { 0f, 0.004f, 0.008f, 0.012f };
                for (int row = 0; row < 4; row++)
                {
                    float z = 0.048f - row * step;
                    for (int i = 0; i < 19; i++)
                        s.At(new Vector3(-0.1824f + stagger[row] + i * step, 0f, z)).Add(i == 0 && row > 0 ? wide : cap);
                }
                float zb = 0.048f - 4 * step;
                for (int i = 0; i < 4; i++) s.At(new Vector3(-0.18f + i * step, 0f, zb)).Add(cap);
                s.At(new Vector3(-0.01f, 0f, zb)).Add(space);
                for (int i = 0; i < 5; i++) s.At(new Vector3(0.1f + i * step, 0f, zb)).Add(cap);
            }), keys, new Vector3(0f, 0.0185f, 0f));
        }

        // Монитор: корпус с тонкой рамкой, крышка сзади, стойка, подставка. Экран — дочерний Screen.
        static void MonitorModel(Transform mon, float k, bool desktopScreen)
        {
            var body = Mat("MonitorBody", "1C2023", 0.55f);
            var metal = Surface("MonitorStand", brushed, "3A3F44", 0.6f, 1f, 0.6f);
            Box("Base", mon, new Vector3(0f, 0.006f, 0.03f) * k, new Vector3(0.26f, 0.012f, 0.19f) * k, metal, bevel: 0.005f);
            var neck = Box("Neck", mon, new Vector3(0f, 0.15f, 0.065f) * k, new Vector3(0.06f, 0.27f, 0.022f) * k, metal, bevel: 0.008f);
            neck.transform.localEulerAngles = new Vector3(-6f, 0f, 0f);
            Box("Body", mon, new Vector3(0f, 0.315f, 0f) * k, new Vector3(0.62f, 0.38f, 0.026f) * k, body, bevel: 0.008f);
            Box("Back", mon, new Vector3(0f, 0.31f, 0.024f) * k, new Vector3(0.42f, 0.26f, 0.04f) * k, body, bevel: 0.02f);
            Material screenMat;
            if (desktopScreen)
            {
                screenMat = Emissive("Screen", "0B1615", "FFFFFF", 0.9f);
                screenMat.mainTexture = desktop;
                screenMat.SetTexture("_EmissionMap", desktop);
            }
            else
            {
                screenMat = Emissive("ScreenMaria", "0B1615", "FFFFFF", 0.75f);
                screenMat.mainTexture = docs[2];
                screenMat.SetTexture("_EmissionMap", docs[2]);
            }
            screenMat.SetFloat("_Glossiness", 0.85f);
            Part("Screen", mon, MeshKit.Sheet(0.594f * k, 0.336f * k, 1, 1), screenMat, new Vector3(0f, 0.325f, -0.0132f) * k, new Vector3(-90f, 0f, 0f));
            Ball("Led", mon, new Vector3(0.28f, 0.137f, -0.0135f) * k, Vector3.one * 0.004f * k, Emissive("Led", "48C6B4", "48C6B4", 2f));
        }

        // Офисное кресло: пятилучье с колесиками, газлифт, сиденье, спинка, подлокотники.
        static void ChairModel(Transform c)
        {
            var metal = Mat("MetalDark", "2A2F33", 0.55f, 0.5f);
            var fabric = Surface("ChairFabric", chairFabric, "3E4F86", 0.15f, 1f);
            fabric.mainTextureScale = new Vector2(4f, 4f);
            Part("Star", c, MeshKit.Get("ChairStar", s =>
            {
                for (int i = 0; i < 5; i++)
                {
                    float a = i * 72f * Mathf.Deg2Rad;
                    var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                    s.At(Vector3.zero).Tube(new[] { new Vector3(0f, 0.1f, 0f), dir * 0.15f + Vector3.up * 0.085f, dir * 0.3f + Vector3.up * 0.07f }, 0.018f, 10);
                }
            }), metal, Vector3.zero);
            for (int i = 0; i < 5; i++)
            {
                float a = i * 72f * Mathf.Deg2Rad;
                Ball("Caster", c, new Vector3(Mathf.Sin(a) * 0.3f, 0.03f, Mathf.Cos(a) * 0.3f), new Vector3(0.05f, 0.05f, 0.03f), Mat("PlasticDark", "3A4045", 0.45f));
            }
            Lathe("Lift", c, "GasLift", new[] { V(0f, 0.08f), V(0.035f, 0.08f), V(0.035f, 0.24f), V(0.022f, 0.26f), V(0.022f, 0.42f), V(0f, 0.43f) }, metal, Vector3.zero, Vector3.zero, 24);
            Box("Seat", c, new Vector3(0f, 0.47f, 0.02f), new Vector3(0.5f, 0.08f, 0.48f), fabric, bevel: 0.035f);
            var back = Box("Back", c, new Vector3(0f, 0.86f, -0.25f), new Vector3(0.46f, 0.56f, 0.07f), fabric, bevel: 0.03f);
            back.transform.localEulerAngles = new Vector3(-8f, 0f, 0f);
            Box("BackFrame", c, new Vector3(0f, 0.62f, -0.26f), new Vector3(0.06f, 0.2f, 0.03f), metal, bevel: 0.01f);
            foreach (float x in new[] { -0.27f, 0.27f })
            {
                Tube("Arm", c, "ChairArm", new[] { new Vector3(0f, 0.45f, -0.08f), new Vector3(0f, 0.6f, -0.08f), new Vector3(0f, 0.64f, -0.04f) }, 0.012f, metal, new Vector3(x, 0f, 0f), 10);
                Box("ArmPad", c, new Vector3(x, 0.655f, 0f), new Vector3(0.06f, 0.025f, 0.24f), Mat("PlasticDark", "3A4045", 0.45f), bevel: 0.012f);
            }
        }

        // ---------- Мария ----------

        // Стол Марии боком у левой стены ближе к задней: людей в 3D нет (разговоры — в окнах демо),
        // а место у двери нужно, чтобы до нее дотянуться взглядом.
        static void BuildMariaDesk(Transform office)
        {
            var wood = Surface("DeskWood", oak, "FFFFFF", 0.35f, 1f);
            var woodDark = Surface("DeskWoodDark", darkWood, "FFFFFF", 0.4f, 1f);
            var plastic = Mat("PlasticDark", "3A4045", 0.45f);

            var desk = Group("Desk_Maria", office, new Vector3(-2.45f, 0f, -1.2f), 90f);
            DeskModel(desk, new Vector2(1.3f, 0.7f), wood, woodDark);

            var mon = Group("Monitor", desk, new Vector3(-0.35f, 0.75f, 0.12f));
            MonitorModel(mon, 0.88f, false);
            Blob(desk, mon.TransformPoint(0f, 0f, 0.03f), new Vector2(0.3f, 0.24f), 0.4f);
            KeyboardModel(desk, new Vector3(-0.3f, 0.75f, -0.15f), plastic, Mat("Keys", "50575D", 0.35f));

            // Бумаги Марии: верхние листы шевелит сквозняк.
            for (int i = 0; i < 3; i++)
            {
                var sheet = Part("Paper", desk, MeshKit.Sheet(0.205f, 0.29f, 8, 12), DocMat((i + 1) % docs.Length),
                    new Vector3(0.2f + i * 0.015f, 0.7512f + i * 0.0012f, -0.08f + i * 0.01f), new Vector3(0f, 12f - i * 9f, 0f), collide: false);
                if (i > 0) sheet.AddComponent<WindSheet>().lift = 0.03f + i * 0.01f;
            }
            Blob(desk, desk.TransformPoint(0.22f, 0.75f, -0.07f), new Vector2(0.3f, 0.36f), 0.25f);

            // Торт к регистрации компании — разговор первого дня. Свеча мерцает.
            var cake = Group("Cake", desk, new Vector3(0.12f, 0.75f, 0.17f));
            Lathe("Plate", cake, "Plate", new[] { V(0f, 0f), V(0.1f, 0f), V(0.14f, 0.012f), V(0.145f, 0.016f), V(0.138f, 0.016f), V(0.1f, 0.006f), V(0f, 0.006f) },
                Mat("Porcelain", "FBFAF7", 0.75f), Vector3.zero, Vector3.zero, 40);
            Lathe("Cake", cake, "Cake", new[] { V(0f, 0.006f), V(0.11f, 0.006f), V(0.112f, 0.07f), V(0.108f, 0.078f), V(0.1f, 0.08f), V(0f, 0.08f) },
                Mat("Cake", "F3E2C0", 0.3f), Vector3.zero, Vector3.zero, 40);
            var cream = Mat("Berry", "C4453B", 0.6f);
            for (int i = 0; i < 10; i++)
            {
                float a = i * 36f * Mathf.Deg2Rad;
                Ball("Berry", cake, new Vector3(Mathf.Cos(a) * 0.085f, 0.088f, Mathf.Sin(a) * 0.085f), Vector3.one * 0.018f, cream);
            }
            Cyl("Candle", cake, new Vector3(0f, 0.11f, 0f), 0.005f, 0.06f, Mat("Candle", "F2F0EA", 0.4f));
            var flame = Ball("Flame", cake, new Vector3(0f, 0.147f, 0f), new Vector3(0.008f, 0.016f, 0.008f), Emissive("Flame", "FFB347", "FFB347", 4f));
            NoShadows(flame);
            var fb = flame.AddComponent<Blinker>();
            fb.mode = Blinker.Mode.Flicker;
            fb.emission = Hex("FFB347") * 4f;
            fb.period = 0.6f;
            Blob(desk, cake.position, new Vector2(0.32f, 0.32f), 0.4f);

            // Растение: листья покачиваются на сквозняке.
            var plant = Group("Plant", desk, new Vector3(0.5f, 0.75f, 0.2f));
            Lathe("Pot", plant, "PlantPot", new[] { V(0f, 0f), V(0.045f, 0f), V(0.06f, 0.11f), V(0.066f, 0.115f), V(0.066f, 0.13f), V(0.058f, 0.13f), V(0.056f, 0.115f), V(0f, 0.11f) },
                Mat("Terracotta", "B5654A", 0.25f), Vector3.zero, Vector3.zero, 32);
            var leaves = Group("Leaves", plant, new Vector3(0f, 0.115f, 0f));
            var leafMat = Mat("Leaves", "4E8A4A", 0.45f);
            var rnd = new System.Random(5);
            for (int i = 0; i < 14; i++)
            {
                float yaw = i * 137.5f;
                float tilt = 25f + (float)rnd.NextDouble() * 35f;
                var stem = Group("Stem", leaves, Vector3.zero, yaw);
                stem.localEulerAngles = new Vector3(tilt, yaw, 0f);
                float len = 0.12f + (float)rnd.NextDouble() * 0.08f;
                Ball("Leaf", stem, new Vector3(0f, len * 0.6f, 0f), new Vector3(0.045f, len, 0.008f), leafMat);
            }
            leaves.gameObject.AddComponent<PlantSway>().angle = 6f;
            Blob(desk, plant.position, new Vector2(0.18f, 0.18f), 0.45f);

            // Настольная лампа.
            var lamp = Group("Lamp", desk, new Vector3(0.52f, 0.75f, -0.22f), -30f);
            var lampMat = Mat("Mustard", "E2A93B", 0.55f);
            Lathe("Base", lamp, "LampBase", new[] { V(0f, 0f), V(0.07f, 0f), V(0.072f, 0.01f), V(0.06f, 0.02f), V(0.012f, 0.025f), V(0f, 0.025f) }, lampMat, Vector3.zero, Vector3.zero, 32);
            Tube("Arm", lamp, "LampArm", new[] { new Vector3(0f, 0.02f, 0f), new Vector3(0f, 0.22f, 0.05f), new Vector3(0f, 0.36f, -0.06f), new Vector3(0f, 0.34f, -0.14f) }, 0.006f, Mat("MetalDark", "2A2F33", 0.55f, 0.5f), Vector3.zero, 10);
            Lathe("Shade", lamp, "LampShade", new[] { V(0.075f, 0f), V(0.07f, 0.01f), V(0.045f, 0.05f), V(0.02f, 0.08f), V(0f, 0.085f) }, lampMat, new Vector3(0f, 0.27f, -0.15f), new Vector3(15f, 0f, 0f), 32);
            Ball("Bulb", lamp, new Vector3(0f, 0.285f, -0.15f), Vector3.one * 0.03f, Emissive("Bulb", "FFF3DC", "FFE2B0", 3f));

            var chair = Group("Chair", desk, new Vector3(0f, 0f, -0.68f));
            ChairModel(chair);
            Blob(desk, chair.position, new Vector2(0.7f, 0.7f), 0.3f);
        }

        // ---------- полка, архив, стена напротив, дверь ----------

        static void BuildShelf(Transform office)
        {
            var wood = Surface("ShelfWood", walnut, "FFFFFF", 0.4f, 1f);
            var woodDark = Surface("DeskWoodDark", darkWood, "FFFFFF", 0.4f, 1f);
            var paper = Mat("Paper", "FBF8F1", 0.1f);
            var box = Surface("Carton", carton, "FFFFFF", 0.15f, 2f);

            // У левой стены, лицом в комнату (+X).
            var shelf = Group("Shelf", office, new Vector3(-3.3f, 0f, 2.55f), -90f);
            Box("Side_L", shelf, new Vector3(-0.54f, 0.95f, 0f), new Vector3(0.03f, 1.9f, 0.34f), wood, bevel: 0.006f);
            Box("Side_R", shelf, new Vector3(0.54f, 0.95f, 0f), new Vector3(0.03f, 1.9f, 0.34f), wood, bevel: 0.006f);
            Box("Top", shelf, new Vector3(0f, 1.885f, 0f), new Vector3(1.11f, 0.03f, 0.34f), wood, bevel: 0.006f);
            Box("Plinth", shelf, new Vector3(0f, 0.04f, 0f), new Vector3(1.05f, 0.08f, 0.34f), wood, bevel: 0.004f);
            Box("Back", shelf, new Vector3(0f, 0.95f, 0.16f), new Vector3(1.05f, 1.9f, 0.02f), woodDark, bevel: 0.002f);
            foreach (float y in new[] { 0.5f, 0.95f, 1.4f })
            {
                Box("Board", shelf, new Vector3(0f, y, 0f), new Vector3(1.05f, 0.025f, 0.32f), wood, bevel: 0.005f);
                Shade(shelf, shelf.TransformPoint(-0.52f, y + 0.0126f, 0.15f), shelf.TransformPoint(0.52f, y + 0.0126f, 0.15f), shelf.TransformDirection(Vector3.back), Vector3.up, 0.12f, 0.5f);
            }

            string[] blue = { "2F5E8C", "1F6F68", "2F7A5C", "4A4A6A" };
            string[] warm = { "8C2F39", "D9A441", "8C2F39", "C4453B" };
            // Средняя полка — два предмета, как в демо: план счетов и учетная политика.
            var accounts = Group("Accounts", shelf, Vector3.zero);
            Binders(accounts, 0.9625f, -0.5f, blue, 7);
            Text("Label", accounts, new Vector3(-0.27f, 0.95f, -0.165f), "ПЛАН СЧЕТОВ", 0.022f, "FBF8F1", style: FontStyle.Bold);
            var policy = Group("Policy", shelf, Vector3.zero);
            Binders(policy, 0.9625f, 0.04f, warm, 6);
            Text("Label", policy, new Vector3(0.27f, 0.95f, -0.165f), "УЧЕТНАЯ ПОЛИТИКА", 0.022f, "FBF8F1", style: FontStyle.Bold);
            Binders(shelf, 0.5125f, -0.5f, new[] { "4A4A6A", "6F7F7A", "2F5E8C" }, 5);
            for (int i = 0; i < 5; i++)
                Box("Stack", shelf, new Vector3(0.25f + (i % 2) * 0.004f, 0.53f + i * 0.016f, -0.01f), new Vector3(0.3f, 0.014f, 0.22f), paper, new Vector3(0f, i * 2.5f - 4f, 0f), 0.003f);
            Binders(shelf, 1.4125f, -0.5f, new[] { "D9A441", "2F7A5C" }, 4);
            CartonBox(shelf, "FileBox", new Vector3(0.25f, 1.53f, 0f), new Vector3(0.36f, 0.23f, 0.28f), box);
            CartonBox(shelf, "Carton_L", new Vector3(-0.26f, 0.235f, 0f), new Vector3(0.46f, 0.31f, 0.3f), box);
            CartonBox(shelf, "Carton_R", new Vector3(0.26f, 0.235f, 0f), new Vector3(0.46f, 0.31f, 0.3f), box);
            Blob(office, new Vector3(-3.3f, 0f, 2.55f), new Vector2(0.5f, 1.3f), 0.35f);

            AddInteractable(accounts, "План счетов", "accounts", FocusFrom(accounts, shelf.TransformPoint(-0.27f, 1.12f, -0.17f), -90f, 2f, 1f));
            AddInteractable(policy, "Учетная политика", "terms", FocusFrom(policy, shelf.TransformPoint(0.27f, 1.12f, -0.17f), -90f, 2f, 1f));
        }

        static void Binders(Transform shelf, float y, float x0, string[] colors, int count)
        {
            var paper = Mat("Paper", "FBF8F1", 0.1f);
            var hole = Mat("Ink", "1E1B18", 0.4f);
            float x = x0;
            for (int i = 0; i < count; i++)
            {
                float w = i % 3 == 0 ? 0.075f : 0.06f;
                float h = 0.32f - (i % 2) * 0.02f;
                string c = colors[i % colors.Length];
                Box("Binder", shelf, new Vector3(x + w / 2f, y + h / 2f, -0.01f), new Vector3(w - 0.004f, h, 0.28f), Mat("Binder_" + c, c, 0.4f), bevel: 0.004f);
                Box("Spine", shelf, new Vector3(x + w / 2f, y + h * 0.62f, -0.1505f), new Vector3(w * 0.6f, 0.07f, 0.002f), paper, bevel: 0.0008f);
                Cyl("Hole", shelf, new Vector3(x + w / 2f, y + h * 0.2f, -0.1505f), 0.011f, 0.002f, hole, new Vector3(90f, 0f, 0f), 0f);
                x += w;
            }
        }

        static void CartonBox(Transform parent, string name, Vector3 pos, Vector3 size, Material box)
        {
            Box(name, parent, pos, size, box, bevel: 0.006f);
            Box(name + "_Tape", parent, pos + new Vector3(0f, size.y / 2f, 0f), new Vector3(0.05f, 0.002f, size.z + 0.002f), Mat("Tape", "C9A36A", 0.6f), bevel: 0.0008f);
            Box(name + "_Label", parent, pos + new Vector3(0f, 0.02f, -size.z / 2f - 0.001f), new Vector3(size.x * 0.4f, size.y * 0.3f, 0.001f), Mat("Paper", "FBF8F1", 0.1f), bevel: 0f);
        }

        static void BuildArchive(Transform office)
        {
            var metal = Mat("ArchiveMetal", "6F7F7A", 0.42f, 0.3f);
            var drawer = Mat("ArchiveDrawer", "7E8E89", 0.48f, 0.3f);
            var chrome = Mat("Chrome", "C9CED0", 0.85f, 0.9f);
            var paper = Mat("Paper", "FBF8F1", 0.1f);

            // Передний правый угол, ящики к игроку (−Z).
            var arch = Group("Archive", office, new Vector3(1.95f, 0f, 3.12f));
            Box("Body", arch, new Vector3(0f, 0.68f, 0f), new Vector3(0.6f, 1.36f, 0.62f), metal, bevel: 0.012f);
            for (int i = 0; i < 4; i++)
            {
                float y = 0.19f + i * 0.32f;
                Box("Drawer", arch, new Vector3(0f, y, -0.318f), new Vector3(0.54f, 0.29f, 0.022f), drawer, bevel: 0.006f);
                Tube("Handle", arch, "DrawerHandle", new[]
                {
                    new Vector3(-0.08f, 0f, 0f), new Vector3(-0.08f, 0f, -0.025f), new Vector3(-0.07f, 0f, -0.03f),
                    new Vector3(0.07f, 0f, -0.03f), new Vector3(0.08f, 0f, -0.025f), new Vector3(0.08f, 0f, 0f),
                }, 0.006f, chrome, new Vector3(0f, y + 0.07f, -0.329f), 10);
                Box("TagFrame", arch, new Vector3(0f, y - 0.01f, -0.3305f), new Vector3(0.11f, 0.055f, 0.003f), chrome, bevel: 0.001f);
                Box("Tag", arch, new Vector3(0f, y - 0.01f, -0.332f), new Vector3(0.1f, 0.045f, 0.001f), paper, bevel: 0f);
            }
            Lathe("Lock", arch, "Lock", new[] { V(0f, 0f), V(0.012f, 0f), V(0.012f, 0.008f), V(0.008f, 0.01f), V(0f, 0.01f) }, chrome, new Vector3(0.22f, 1.3f, -0.31f), new Vector3(-90f, 0f, 0f), 20);
            Text("Label", arch, new Vector3(0f, 1.14f, -0.331f), "АРХИВ", 0.03f, "1E1B18", style: FontStyle.Bold);
            CartonBox(arch, "Box", new Vector3(0.02f, 1.48f, 0f), new Vector3(0.42f, 0.24f, 0.32f), Surface("Carton", carton, "FFFFFF", 0.15f, 2f));
            Box("Folders", arch, new Vector3(-0.04f, 1.62f, 0.02f), new Vector3(0.32f, 0.04f, 0.24f), Mat("Binder_2F5E8C", "2F5E8C", 0.4f), bevel: 0.004f);
            AddInteractable(arch, "Архив документов", "docs", FocusFrom(arch, arch.TransformPoint(0f, 0.85f, -0.32f), 0f, 12f, 1.5f));
            Blob(office, new Vector3(1.95f, 0f, 3.1f), new Vector2(0.9f, 0.9f), 0.45f);
        }

        static void BuildCalendar(Transform office)
        {
            var cal = Group("Calendar", office, new Vector3(0.45f, 1.95f, 3.5f));
            var paper = Mat("Paper", "FBF8F1", 0.1f);
            // Пачка листов: по краю видна толщина.
            Box("Sheet", cal, new Vector3(0f, 0f, -0.006f), new Vector3(0.4f, 0.54f, 0.008f), paper, bevel: 0.0015f);
            Box("Header", cal, new Vector3(0f, 0.21f, -0.012f), new Vector3(0.41f, 0.13f, 0.006f), Mat("Red", "C4453B", 0.5f), bevel: 0.002f);
            var ring = Mat("Chrome", "C9CED0", 0.85f, 0.9f);
            for (int i = 0; i < 9; i++)
                Tube("Ring", cal, "CalendarRing", MeshKit.Arc(Vector3.zero, 0.008f, -30f, 210f, 10), 0.0012f, ring, new Vector3(-0.16f + i * 0.04f, 0.278f, -0.012f), 8)
                    .transform.localEulerAngles = new Vector3(0f, 90f, 0f);
            Text("Month", cal, new Vector3(0f, 0.21f, -0.0155f), "ЯНВАРЬ 2026", 0.05f, "FFFFFF", style: FontStyle.Bold);
            Text("Day", cal, new Vector3(0f, 0.01f, -0.0105f), "12", 0.24f, "1E1B18", style: FontStyle.Bold);
            Text("Weekday", cal, new Vector3(0f, -0.17f, -0.0105f), "понедельник", 0.045f, "6E665C");
            AddInteractable(cal, "Календарь", "cal", FocusFrom(cal, cal.position, 0f, 0f, 0.8f));
        }

        static void BuildClock(Transform office)
        {
            var ink = Mat("Ink", "1E1B18", 0.4f);
            var clock = Group("Clock", office, new Vector3(-0.3f, 2.3f, 3.5f));
            // Корпус — тело вращения вокруг оси Z (лицом к игроку, −Z).
            Lathe("Rim", clock, "ClockRim", new[]
            {
                V(0.163f, -0.032f), V(0.172f, -0.036f), V(0.182f, -0.034f), V(0.188f, -0.026f), V(0.19f, -0.012f), V(0.187f, 0f), V(0.17f, 0f),
            }, Mat("ClockRim", "5B4A3B", 0.55f), Vector3.zero, new Vector3(90f, 0f, 0f), 64);
            Cyl("Face", clock, new Vector3(0f, 0f, -0.012f), 0.166f, 0.024f, Mat("Paper", "FBF8F1", 0.1f), new Vector3(90f, 0f, 0f), 0f);
            for (int i = 0; i < 60; i++)
            {
                var tick = Group("Tick", clock, Vector3.zero);
                tick.localEulerAngles = new Vector3(0f, 0f, -i * 6f);
                bool hourMark = i % 5 == 0;
                Box("Mark", tick, new Vector3(0f, hourMark ? 0.14f : 0.147f, -0.0245f), new Vector3(hourMark ? (i % 15 == 0 ? 0.01f : 0.006f) : 0.0018f, hourMark ? 0.026f : 0.01f, 0.001f), ink, bevel: 0f);
            }
            var hour = Hand(clock, "Hand_Hour", 0.085f, 0.012f, -0.027f, ink);
            var minute = Hand(clock, "Hand_Minute", 0.125f, 0.008f, -0.029f, ink);
            var second = Hand(clock, "Hand_Second", 0.135f, 0.0025f, -0.031f, Mat("Red", "C4453B", 0.5f));
            Cyl("Pin", clock, new Vector3(0f, 0f, -0.032f), 0.009f, 0.004f, Mat("Brass", "C9A452", 0.75f, 0.8f), new Vector3(90f, 0f, 0f));
            NoShadows(Lathe("Glass", clock, "ClockGlass", new[] { V(0f, -0.045f), V(0.08f, -0.042f), V(0.14f, -0.037f), V(0.166f, -0.033f) },
                Glass("ClockGlass", new Color(0.9f, 0.95f, 1f, 0.08f)), Vector3.zero, new Vector3(90f, 0f, 0f), 48));
            var oc = clock.gameObject.AddComponent<OfficeClock>();
            oc.hour = hour;
            oc.minute = minute;
            oc.second = second;
            AddInteractable(clock, "Часы", "clock", FocusFrom(clock, clock.position, 0f, 0f, 0.7f));
        }

        // Стрелка на оси: поворачивает OfficeClock (угол по часовой от 12).
        static Transform Hand(Transform clock, string name, float length, float width, float z, Material mat)
        {
            var pivot = Group(name, clock, Vector3.zero);
            Box("Blade", pivot, new Vector3(0f, length / 2f - 0.015f, z), new Vector3(width, length + 0.03f, 0.0015f), mat, bevel: Mathf.Min(width * 0.4f, 0.0007f));
            return pivot;
        }

        static void BuildDoor(Transform office)
        {
            var frame = Surface("DoorFrame", darkWood, "C8B8A6", 0.4f, 1f);
            var slab = Surface("DoorWood", doorWood, "FFFFFF", 0.45f, 1f);
            var brass = Mat("Brass", "C9A452", 0.75f, 0.8f);

            // На левой стене, лицом в комнату (+X): назад игрок не поворачивается.
            var door = Group("Door", office, new Vector3(-3.5f, 0f, 1.2f), -90f);
            Box("Post_L", door, new Vector3(-0.5f, 1.06f, -0.035f), new Vector3(0.08f, 2.12f, 0.07f), frame, bevel: 0.01f);
            Box("Post_R", door, new Vector3(0.5f, 1.06f, -0.035f), new Vector3(0.08f, 2.12f, 0.07f), frame, bevel: 0.01f);
            Box("Lintel", door, new Vector3(0f, 2.16f, -0.035f), new Vector3(1.08f, 0.08f, 0.07f), frame, bevel: 0.01f);
            Box("Slab", door, new Vector3(0f, 1.03f, -0.025f), new Vector3(0.92f, 2.06f, 0.045f), slab, bevel: 0.006f);
            // Филенки: рамка-накладка и выпуклое поле.
            foreach (var (y, h) in new[] { (1.52f, 0.8f), (0.5f, 0.7f) })
            {
                Box("Panel", door, new Vector3(0f, y, -0.05f), new Vector3(0.7f, h, 0.008f), slab, bevel: 0.003f);
                Box("Field", door, new Vector3(0f, y, -0.056f), new Vector3(0.58f, h - 0.12f, 0.01f), slab, bevel: 0.012f);
            }
            for (int i = 0; i < 2; i++)
                Cyl("Hinge", door, new Vector3(-0.46f, 0.3f + i * 1.45f, -0.05f), 0.009f, 0.1f, brass);
            Lathe("Rosette", door, "Rosette", new[] { V(0f, 0f), V(0.028f, 0f), V(0.028f, 0.004f), V(0.02f, 0.01f), V(0.012f, 0.012f), V(0f, 0.012f) }, brass, new Vector3(0.36f, 1.02f, -0.048f), new Vector3(-90f, 0f, 0f), 32);
            Tube("Lever", door, "DoorLever", new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, -0.045f), new Vector3(-0.02f, 0f, -0.06f), new Vector3(-0.12f, -0.005f, -0.06f) }, 0.008f, brass, new Vector3(0.36f, 1.02f, -0.05f), 12);
            Box("KeyPlate", door, new Vector3(0.36f, 0.9f, -0.049f), new Vector3(0.04f, 0.07f, 0.004f), brass, bevel: 0.0015f);
            Box("KickPlate", door, new Vector3(0f, 0.11f, -0.049f), new Vector3(0.86f, 0.2f, 0.002f), Mat("Brushed", "B9C2C6", 0.65f, 0.85f), bevel: 0.0008f);
            Box("ExitSign", door, new Vector3(0f, 2.42f, -0.02f), new Vector3(0.34f, 0.13f, 0.03f), Emissive("ExitGreen", "2F7A5C", "2F7A5C", 1.6f), bevel: 0.008f);
            Text("ExitText", door, new Vector3(0f, 2.42f, -0.037f), "ВЫХОД", 0.07f, "FFFFFF", style: FontStyle.Bold);
            AddInteractable(door, "Завершить день", "door", FocusFrom(door, door.TransformPoint(0f, 1.15f, 0f), -90f, 4f, 1.7f));
        }

        // ---------- перегородка и цех ----------

        static void BuildPartition(Transform office)
        {
            var alu = Surface("Aluminium", brushed, "8E979C", 0.6f, 1f, 0.8f);
            var p = Group("Partition", office, new Vector3(2.5f, 0f, 0.5f));
            Box("Panel", p, new Vector3(0f, 0.45f, 0f), new Vector3(0.08f, 0.9f, 6f), Surface("WallPanel", plaster, "D3BD9C", 0.35f, 1f, bump: 0.3f), bevel: 0.004f);
            Box("Rail_Mid", p, new Vector3(0f, 0.92f, 0f), new Vector3(0.1f, 0.05f, 6f), alu, bevel: 0.008f);
            Box("Rail_Top", p, new Vector3(0f, 2.88f, 0f), new Vector3(0.1f, 0.05f, 6f), alu, bevel: 0.008f);
            foreach (float z in new[] { -3f, -1.5f, 0f, 1.5f, 3f })
                Box("Post", p, new Vector3(0f, 1.9f, z), new Vector3(0.06f, 1.96f, 0.06f), alu, bevel: 0.008f);
            NoShadows(Box("Glass", p, new Vector3(0f, 1.9f, 0f), new Vector3(0.012f, 1.94f, 6f), Glass("Glass", new Color(0.8f, 0.9f, 0.95f, 0.1f)), bevel: 0f));
            // Матовая полоса на стекле — чтобы было видно, что тут стекло.
            NoShadows(Box("Frost", p, new Vector3(-0.008f, 1.55f, 0f), new Vector3(0.002f, 0.06f, 6f), Glass("Frost", new Color(0.95f, 0.97f, 1f, 0.35f)), bevel: 0f));
        }

        static void BuildWorkshop()
        {
            var floorMat = Surface("Concrete", concrete, "FFFFFF", 0.25f, 0.5f);
            var wall = Surface("ShopWall", plaster, "B7BDBB", 0.1f, 0.5f, bump: 0.8f);
            var band = Mat("ShopBand", "5F7470", 0.25f);
            var roof = Mat("ShopCeiling", "3F4447", 0.1f);
            var steel = Surface("Steel", brushed, "B9C2C6", 0.65f, 1f, 0.85f);
            var metalDark = Mat("MetalDark", "2A2F33", 0.55f, 0.5f);
            var mustard = Mat("Mustard", "E2A93B", 0.55f);
            var teal = Mat("Teal", "1F6F68", 0.3f);

            var shop = Group("Workshop", null, Vector3.zero);
            Slab("Floor", shop, new Vector3(6.25f, -0.05f, 2.75f), new Vector3(7.7f, 0.1f, 10.7f), floorMat);
            Slab("Ceiling", shop, new Vector3(6.25f, 4.55f, 2.75f), new Vector3(7.7f, 0.1f, 10.7f), roof);
            Slab("Wall_Right", shop, new Vector3(10.05f, 2.3f, 2.75f), new Vector3(0.1f, 4.6f, 10.5f), wall);
            Slab("Wall_Front", shop, new Vector3(6.25f, 2.3f, 8.05f), new Vector3(7.6f, 4.6f, 0.1f), wall);
            Slab("Wall_Back", shop, new Vector3(6.25f, 2.3f, -2.55f), new Vector3(7.6f, 4.6f, 0.1f), wall);
            Slab("Wall_Left", shop, new Vector3(2.45f, 2.3f, 5.8f), new Vector3(0.1f, 4.6f, 4.5f), wall);
            Slab("Wall_OverOffice", shop, new Vector3(2.5f, 3.75f, 0.5f), new Vector3(0.1f, 1.7f, 6.2f), wall);
            Slab("Band_Right", shop, new Vector3(9.99f, 0.6f, 2.75f), new Vector3(0.02f, 1.2f, 10.5f), band);
            Slab("Band_Front", shop, new Vector3(6.25f, 0.6f, 7.99f), new Vector3(7.5f, 1.2f, 0.02f), band);
            Slab("Band_Back", shop, new Vector3(6.25f, 0.6f, -2.49f), new Vector3(7.5f, 1.2f, 0.02f), band);
            Slab("Band_Left", shop, new Vector3(2.51f, 0.6f, 5.8f), new Vector3(0.02f, 1.2f, 4.5f), band);
            Slab("SafetyLine", shop, new Vector3(3.3f, 0.001f, 2.75f), new Vector3(0.08f, 0.002f, 10.4f), mustard);
            Shade(shop, new Vector3(10f, 0f, -2.5f), new Vector3(10f, 0f, 8f), Vector3.left, Vector3.up, 0.6f, 0.5f);
            Shade(shop, new Vector3(2.5f, 0f, 8f), new Vector3(10f, 0f, 8f), Vector3.back, Vector3.up, 0.6f, 0.5f);

            // Люминесцентные светильники; один подмигивает.
            var tube = Emissive("LampCool", "F4F8FF", "E6F0FF", 2.2f);
            var housing = Mat("LampHousing", "D9DDDF", 0.5f, 0.3f);
            int n = 0;
            foreach (var p in new[] { new Vector2(4.5f, 1f), new Vector2(7.2f, 1f), new Vector2(4.5f, 5.2f), new Vector2(7.2f, 5.2f) })
            {
                Box("Housing", shop, new Vector3(p.x, 4.45f, p.y), new Vector3(0.26f, 0.06f, 2.5f), housing, bevel: 0.01f);
                var t = Box("Tube", shop, new Vector3(p.x, 4.41f, p.y), new Vector3(0.18f, 0.03f, 2.4f), tube, bevel: 0.012f);
                if (n++ == 1)
                {
                    var b = t.AddComponent<Blinker>();
                    b.mode = Blinker.Mode.Flicker;
                    b.emission = Hex("E6F0FF") * 2.2f;
                    b.period = 5f;
                }
            }

            var sign = Group("Sign", shop, new Vector3(6.2f, 3.6f, 8f));
            Box("Board", sign, new Vector3(0f, 0f, -0.015f), new Vector3(1.6f, 0.6f, 0.03f), teal, bevel: 0.01f);
            Text("Text", sign, new Vector3(0f, 0f, -0.032f), "ЦЕХ", 0.42f, "E2A93B", style: FontStyle.Bold);

            // Вытяжной вентилятор на правой стене.
            var fan = Group("Fan", shop, new Vector3(9.86f, 3.1f, 4f), 90f);
            Lathe("Shroud", fan, "FanShroud", new[] { V(0.36f, 0f), V(0.36f, 0.12f), V(0.33f, 0.12f), V(0.33f, 0.01f), V(0f, 0.01f) }, metalDark, Vector3.zero, new Vector3(90f, 0f, 0f), 48);
            var rotor = Group("Rotor", fan, new Vector3(0f, 0f, 0.06f));
            for (int i = 0; i < 5; i++)
            {
                var blade = Box("Blade", rotor, Vector3.zero, new Vector3(0.12f, 0.28f, 0.008f), steel, bevel: 0.003f);
                blade.transform.localPosition = Quaternion.Euler(0f, 0f, i * 72f) * new Vector3(0f, 0.17f, 0f);
                blade.transform.localEulerAngles = new Vector3(0f, 20f, i * 72f);
            }
            Cyl("Hub", rotor, Vector3.zero, 0.06f, 0.06f, metalDark, new Vector3(90f, 0f, 0f));
            var spin = rotor.gameObject.AddComponent<Spinner>();
            spin.axis = Vector3.forward;
            spin.degreesPerSecond = 220f;
            Part("Guard", fan, MeshKit.Get("FanGuard", s =>
            {
                for (int i = 1; i <= 4; i++) s.At(Vector3.zero).Tube(Circle(0.085f * i, 40), 0.004f, 6, false);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * 45f * Mathf.Deg2Rad;
                    s.At(Vector3.zero).Tube(new[] { Vector3.zero, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.34f }, 0.004f, 6);
                }
            }), metalDark, new Vector3(0f, 0f, -0.005f), Vector3.zero, collide: false);

            // Стол Петра: лабораторный, стоит лицом к офису. В одной колбе пузырьки.
            var bench = Group("Bench_Petr", shop, new Vector3(3.6f, 0f, 1.4f), -90f);
            Box("Cabinet", bench, new Vector3(0f, 0.44f, 0f), new Vector3(1.5f, 0.86f, 0.6f), Mat("ArchiveMetal", "6F7F7A", 0.42f, 0.3f), bevel: 0.01f);
            for (int i = 0; i < 3; i++)
            {
                Box("Door", bench, new Vector3(-0.5f + i * 0.5f, 0.44f, -0.302f), new Vector3(0.47f, 0.78f, 0.01f), Mat("ArchiveDrawerFlat", "7E8E89", 0.5f, 0.3f), bevel: 0.004f);
                Box("Pull", bench, new Vector3(-0.5f + i * 0.5f, 0.74f, -0.31f), new Vector3(0.12f, 0.015f, 0.012f), Mat("Chrome", "C9CED0", 0.85f, 0.9f), bevel: 0.004f);
            }
            Box("Top", bench, new Vector3(0f, 0.885f, 0f), new Vector3(1.6f, 0.05f, 0.7f), Mat("LabTop", "D9DDDB", 0.65f), bevel: 0.008f);
            Flask(bench, new Vector3(-0.5f, 0.91f, 0.1f), "48C6B4", true);
            Flask(bench, new Vector3(-0.3f, 0.91f, 0.18f), "E2A93B", false);
            Flask(bench, new Vector3(0.45f, 0.91f, 0.05f), "C4453B", false);
            for (int i = 0; i < 3; i++) Bottle(bench, new Vector3(0.05f + i * 0.09f, 0.91f, 0.15f));
            var rack = Group("TubeRack", bench, new Vector3(-0.05f, 0.91f, -0.12f));
            Box("Rack", rack, new Vector3(0f, 0.05f, 0f), new Vector3(0.24f, 0.012f, 0.06f), Mat("PlasticLight", "E7E9E8", 0.4f), bevel: 0.003f);
            for (int i = 0; i < 6; i++)
            {
                string liquid = i % 3 == 0 ? "48C6B4" : i % 3 == 1 ? "E2A93B" : "C4453B";
                Cyl("Tube", rack, new Vector3(-0.1f + i * 0.04f, 0.06f, 0f), 0.008f, 0.12f, Glass("FlaskGlass", new Color(0.9f, 0.95f, 1f, 0.3f)));
                Cyl("Liquid", rack, new Vector3(-0.1f + i * 0.04f, 0.03f, 0f), 0.0065f, 0.05f, Mat("Liquid_" + liquid, liquid, 0.9f));
            }

            var tanks = new[] { (pos: new Vector3(6.2f, 0f, 3.6f), r: 0.75f, h: 1.8f), (pos: new Vector3(8.5f, 0f, 5.2f), r: 0.9f, h: 2.4f) };
            foreach (var t in tanks) Tank(shop, t.pos, t.r, t.h, steel, teal, mustard);
            var pipe = steel;
            Tube("Pipe_X", shop, "PipeX", new[] { new Vector3(6.2f, 3.05f, 3.6f), new Vector3(6.2f, 3.35f, 3.6f), new Vector3(6.5f, 3.35f, 3.6f), new Vector3(8.2f, 3.35f, 3.6f), new Vector3(8.5f, 3.35f, 3.9f), new Vector3(8.5f, 3.35f, 4.6f) }, 0.06f, pipe, Vector3.zero, 16);
            foreach (var f in new[] { new Vector3(7f, 3.35f, 3.6f), new Vector3(7.8f, 3.35f, 3.6f) })
                Cyl("Flange", shop, f, 0.085f, 0.03f, pipe, new Vector3(0f, 0f, 90f));

            // Линия розлива вдоль Z: флаконы едут по ленте, ролики крутятся, на пульте мигают лампы.
            var line = Group("BottlingLine", shop, new Vector3(7.4f, 0f, 0f));
            Box("Frame", line, new Vector3(0f, 0.8f, 0f), new Vector3(0.55f, 0.08f, 3.6f), metalDark, bevel: 0.01f);
            Box("Belt", line, new Vector3(0f, 0.85f, 0f), new Vector3(0.45f, 0.02f, 3.6f), Mat("Rubber", "1E1E1E", 0.35f), bevel: 0.008f);
            foreach (float z in new[] { -1.6f, 0f, 1.6f })
                foreach (float x in new[] { -0.24f, 0.24f })
                    Box("Leg", line, new Vector3(x, 0.38f, z), new Vector3(0.05f, 0.76f, 0.05f), metalDark, bevel: 0.006f);
            foreach (float z in new[] { -1.8f, 1.8f })
            {
                var roller = Cyl("Roller", line, new Vector3(0f, 0.835f, z), 0.04f, 0.47f, steel, new Vector3(0f, 0f, 90f));
                var rs = roller.AddComponent<Spinner>();
                rs.axis = Vector3.up;
                rs.degreesPerSecond = -315f;
            }
            Box("Filler", line, new Vector3(0f, 1.45f, 0.4f), new Vector3(0.9f, 0.7f, 0.8f), mustard, bevel: 0.03f);
            Box("Filler_L", line, new Vector3(-0.4f, 0.55f, 0.4f), new Vector3(0.08f, 1.1f, 0.7f), mustard, bevel: 0.015f);
            Box("Filler_R", line, new Vector3(0.4f, 0.55f, 0.4f), new Vector3(0.08f, 1.1f, 0.7f), mustard, bevel: 0.015f);
            NoShadows(Box("Window", line, new Vector3(-0.451f, 1.45f, 0.5f), new Vector3(0.004f, 0.3f, 0.4f), Glass("FlaskGlass", new Color(0.9f, 0.95f, 1f, 0.3f)), bevel: 0.002f));
            Lathe("Nozzle", line, "Nozzle", new[] { V(0f, 0f), V(0.008f, 0f), V(0.012f, 0.03f), V(0.03f, 0.08f), V(0.03f, 0.1f), V(0f, 0.1f) }, steel, new Vector3(0f, 1f, 0.4f), Vector3.zero, 24);
            Box("Panel", line, new Vector3(-0.46f, 1.5f, 0.4f), new Vector3(0.02f, 0.3f, 0.4f), Emissive("ControlPanel", "1E4A46", "48C6B4", 0.6f), bevel: 0.005f);
            var lampGreen = Ball("Lamp_Green", line, new Vector3(-0.475f, 1.62f, 0.3f), Vector3.one * 0.03f, Emissive("LampGreen", "2F7A5C", "6BEA9B", 2f));
            var bg = lampGreen.AddComponent<Blinker>();
            bg.mode = Blinker.Mode.Pulse;
            bg.emission = Hex("6BEA9B") * 2.5f;
            bg.period = 2f;
            var lampAmber = Ball("Lamp_Amber", line, new Vector3(-0.475f, 1.62f, 0.5f), Vector3.one * 0.03f, Emissive("LampAmber", "B07A1A", "FFB347", 2f));
            var ba = lampAmber.AddComponent<Blinker>();
            ba.mode = Blinker.Mode.Blink;
            ba.emission = Hex("FFB347") * 3f;
            ba.period = 0.9f;
            var bottles = Group("Bottles", line, Vector3.zero);
            var moving = new List<Transform>();
            for (int i = 0; i < 10; i++)
            {
                var b = Group("Bottle", bottles, new Vector3(0f, 0.86f, -1.65f + i * 0.33f));
                Bottle(b, Vector3.zero);
                moving.Add(b);
            }
            var conveyor = bottles.gameObject.AddComponent<Conveyor>();
            conveyor.items = moving.ToArray();
            conveyor.length = 3.3f;

            var pallet = Group("Pallet", shop, new Vector3(4.9f, 0f, -1.4f), 8f);
            var palletWood = Surface("PalletWood", oak, "E0C8A8", 0.2f, 1f);
            var box = Surface("Carton", carton, "FFFFFF", 0.15f, 2f);
            foreach (float x in new[] { -0.55f, 0f, 0.55f })
                Box("Runner", pallet, new Vector3(x, 0.05f, 0f), new Vector3(0.1f, 0.1f, 1f), palletWood, bevel: 0.006f);
            for (int i = 0; i < 7; i++)
                Box("Deck", pallet, new Vector3(0f, 0.115f, -0.45f + i * 0.15f), new Vector3(1.2f, 0.03f, 0.1f), palletWood, bevel: 0.004f);
            for (int layer = 0; layer < 2; layer++)
                for (int c = 0; c < 3; c++)
                    for (int r = 0; r < 2; r++)
                        if (layer == 0 || c + r < 3)
                            CartonBox(pallet, "Carton", new Vector3(-0.4f + c * 0.4f, 0.275f + layer * 0.3f, -0.25f + r * 0.5f), new Vector3(0.38f, 0.29f, 0.48f), box);
            Blob(shop, new Vector3(4.9f, 0f, -1.4f), new Vector2(1.6f, 1.4f), 0.45f, 8f);

            StripColliders(shop);
        }

        static Vector3[] Circle(float r, int steps)
        {
            var pts = new Vector3[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float a = i / (float)steps * Mathf.PI * 2f;
                pts[i] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
            }
            return pts;
        }

        // Емкость: опоры, конусное дно, корпус, купол, бандаж, мотор мешалки (вал крутится).
        static void Tank(Transform parent, Vector3 pos, float r, float h, Material steel, Material band, Material motor)
        {
            const float legs = 0.5f;
            var t = Group("Tank", parent, pos);
            for (int i = 0; i < 4; i++)
            {
                float a = (45f + i * 90f) * Mathf.Deg2Rad;
                var foot = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r * 0.85f;
                Tube("Leg", t, MeshKit.Key("TankLeg", r, i), new[] { foot, foot * 0.98f + Vector3.up * (legs + r * 0.62f) }, 0.04f, steel, Vector3.zero, 12);
                Cyl("Foot", t, foot + Vector3.up * 0.01f, 0.07f, 0.02f, steel);
            }
            Lathe("Body", t, MeshKit.Key("TankBody", r, h), new[]
            {
                V(0.05f, legs - 0.05f), V(0.05f, legs), V(r * 0.6f, legs + r * 0.35f), V(r * 0.97f, legs + r * 0.5f), V(r, legs + r * 0.55f),
                V(r, legs + h), V(r * 0.97f, legs + h + r * 0.12f), V(r * 0.8f, legs + h + r * 0.3f), V(r * 0.4f, legs + h + r * 0.42f), V(0f, legs + h + r * 0.45f),
            }, steel, Vector3.zero, Vector3.zero, 56);
            Cyl("Band", t, new Vector3(0f, legs + h * 0.65f, 0f), r + 0.012f, 0.16f, band);
            Cyl("Band_Low", t, new Vector3(0f, legs + r * 0.55f + 0.06f, 0f), r + 0.01f, 0.04f, steel);
            float top = legs + h + r * 0.45f;
            Lathe("Motor", t, "TankMotor", new[] { V(0f, 0f), V(0.12f, 0f), V(0.14f, 0.02f), V(0.14f, 0.28f), V(0.12f, 0.3f), V(0f, 0.3f) }, motor, new Vector3(0f, top + 0.06f, 0f), Vector3.zero, 32);
            for (int i = 0; i < 6; i++)
                Box("Fin", t, new Vector3(0f, top + 0.2f, 0f) + Quaternion.Euler(0f, i * 60f, 0f) * new Vector3(0.145f, 0f, 0f), new Vector3(0.012f, 0.18f, 0.03f), motor, new Vector3(0f, i * 60f, 0f), 0.004f);
            var shaft = Group("Coupling", t, new Vector3(0f, top + 0.03f, 0f));
            Cyl("Shaft", shaft, Vector3.zero, 0.03f, 0.06f, steel);
            Box("Key", shaft, new Vector3(0.03f, 0f, 0f), new Vector3(0.02f, 0.05f, 0.012f), Mat("Red", "C4453B", 0.5f), bevel: 0.003f);
            var s = shaft.gameObject.AddComponent<Spinner>();
            s.degreesPerSecond = 120f;
            Lathe("Valve", t, "Valve", new[] { V(0f, 0f), V(0.04f, 0f), V(0.04f, 0.1f), V(0f, 0.1f) }, Mat("Red", "C4453B", 0.5f), new Vector3(-r * 0.4f, legs + 0.1f, 0f), new Vector3(0f, 0f, 90f), 16);
            Cyl("Wheel", t, new Vector3(-r * 0.4f - 0.1f, legs + 0.1f, 0f), 0.07f, 0.015f, Mat("Red", "C4453B", 0.5f), new Vector3(0f, 0f, 90f));
            Blob(t, t.position, new Vector2(r * 2.6f, r * 2.6f), 0.45f);
        }

        static void Flask(Transform parent, Vector3 pos, string liquid, bool bubbles)
        {
            var glass = Glass("FlaskGlass", new Color(0.9f, 0.95f, 1f, 0.3f));
            var f = Group("Flask", parent, pos);
            Lathe("Liquid", f, "FlaskLiquid", new[] { V(0f, 0.002f), V(0.052f, 0.002f), V(0.05f, 0.02f), V(0.038f, 0.05f), V(0f, 0.05f) }, Mat("Liquid_" + liquid, liquid, 0.9f), Vector3.zero, Vector3.zero, 32);
            NoShadows(Lathe("Glass", f, "FlaskGlass", new[] { V(0f, 0f), V(0.055f, 0f), V(0.058f, 0.006f), V(0.05f, 0.04f), V(0.02f, 0.12f), V(0.016f, 0.13f), V(0.016f, 0.19f), V(0.02f, 0.195f) },
                glass, Vector3.zero, Vector3.zero, 32));
            if (bubbles) SetupBubbles(Particles("Bubbles", f, new Vector3(0f, 0.01f, 0f), ParticleMat("Bubble", new Color(1f, 1f, 1f, 0.7f))));
        }

        // Флакон средства «Куро».
        static void Bottle(Transform parent, Vector3 pos)
        {
            Lathe("Bottle", parent, "KuroBottle", new[]
            {
                V(0f, 0f), V(0.03f, 0f), V(0.035f, 0.006f), V(0.035f, 0.15f), V(0.03f, 0.17f), V(0.016f, 0.19f), V(0.015f, 0.2f),
            }, Mat("Aqua", "48C6B4", 0.75f), pos, Vector3.zero, 24);
            Cyl("Label", parent, pos + new Vector3(0f, 0.085f, 0f), 0.036f, 0.07f, Mat("Paper", "FBF8F1", 0.1f), default, 0f, 24);
            Lathe("Cap", parent, "BottleCap", new[] { V(0f, 0f), V(0.017f, 0f), V(0.017f, 0.025f), V(0.012f, 0.032f), V(0.006f, 0.05f), V(0f, 0.05f) },
                Mat("PlasticLight", "E7E9E8", 0.4f), pos + new Vector3(0f, 0.2f, 0f), Vector3.zero, 20);
        }

        // ---------- за окном: январь, снег идет ----------

        static void BuildOutside()
        {
            var o = Group("Outside", null, Vector3.zero);
            var skyMat = Emissive("Sky", "D3E4F2", "FFFFFF", 1.25f);
            skyMat.mainTexture = sky;
            skyMat.SetTexture("_EmissionMap", sky);
            NoShadows(Box("Sky", o, new Vector3(-1.5f, 3f, 18f), new Vector3(44f, 24f, 0.1f), skyMat, bevel: 0f));
            NoShadows(Box("Snow", o, new Vector3(-1.5f, -2.6f, 10f), new Vector3(44f, 0.1f, 16f), Mat("Snow", "F2F5F8", 0.25f), bevel: 0f));
            var glass = Mat("HouseWindow", "5D6B7A", 0.85f);
            var snow = Mat("Snow", "F2F5F8", 0.25f);
            var frame = Mat("WindowFrame", "F7F3EC", 0.55f);
            // Невысокие дома, чтобы в окне было небо.
            var houses = new[]
            {
                (x: -8.5f, z: 14.5f, w: 4f, h: 6.5f, c: "8C98A6"),
                (x: -5f, z: 15.5f, w: 3.5f, h: 5f, c: "A7B1BC"),
                (x: -2.2f, z: 13.5f, w: 3f, h: 4.2f, c: "B5A089"),
                (x: 1.5f, z: 15f, w: 4f, h: 7f, c: "9AA4AF"),
            };
            foreach (var hs in houses)
            {
                NoShadows(Box("House", o, new Vector3(hs.x, -2.6f + hs.h / 2f, hs.z), new Vector3(hs.w, hs.h, 3f), Surface("House_" + hs.c, plaster, hs.c, 0.1f, 0.5f), bevel: 0.05f));
                NoShadows(Box("Roof", o, new Vector3(hs.x, -2.6f + hs.h + 0.1f, hs.z), new Vector3(hs.w + 0.25f, 0.2f, 3.25f), snow, bevel: 0.08f));
                for (int floor = 1; floor < hs.h / 1.6f; floor++)
                    for (int col = 0; col < 3; col++)
                    {
                        var wp = new Vector3(hs.x - hs.w / 3f + col * hs.w / 3f, -2.6f + floor * 1.6f - 0.5f, hs.z - 1.51f);
                        NoShadows(Box("Window", o, wp, new Vector3(0.6f, 0.7f, 0.02f), glass, bevel: 0f));
                        NoShadows(Box("Frame", o, wp + new Vector3(0f, 0f, -0.015f), new Vector3(0.66f, 0.04f, 0.03f), frame, bevel: 0.005f));
                        NoShadows(Box("Sill", o, wp + new Vector3(0f, -0.37f, -0.04f), new Vector3(0.7f, 0.05f, 0.1f), snow, bevel: 0.02f));
                    }
            }
            // Голые деревья во дворе.
            var bark = Mat("Bark", "4A3B30", 0.2f);
            var rnd = new System.Random(9);
            foreach (var p in new[] { new Vector3(-3.6f, -2.6f, 9f), new Vector3(0.2f, -2.6f, 10.5f), new Vector3(-6.8f, -2.6f, 11f) })
            {
                var tree = Group("Tree", o, p);
                Part("Branches", tree, MeshKit.Get(MeshKit.Key("Tree", p.x, p.z), s =>
                {
                    Branch(s, Vector3.zero, Vector3.up, 2.6f, 0.12f, 3, rnd);
                }), bark, Vector3.zero, Vector3.zero, collide: false);
            }
            // Снег идет перед домами.
            SetupSnow(Particles("Snowfall", o, new Vector3(-3.5f, 4.5f, 9f), ParticleMat("Snowflake", new Color(1f, 1f, 1f, 0.9f))));
            StripColliders(o);
        }

        // Ветка дерева рекурсивно: ствол и 2–3 ответвления с каждого конца.
        static void Branch(MeshKit.Shape s, Vector3 start, Vector3 dir, float length, float radius, int depth, System.Random rnd)
        {
            var mid = start + dir * length * 0.5f + new Vector3((float)rnd.NextDouble() - 0.5f, 0f, (float)rnd.NextDouble() - 0.5f) * length * 0.08f;
            var end = start + dir * length;
            s.At(Vector3.zero).Tube(new[] { start, mid, end }, radius, 8);
            if (depth == 0) return;
            int kids = 2 + rnd.Next(2);
            for (int i = 0; i < kids; i++)
            {
                var axis = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f) * Vector3.right;
                var d = Quaternion.AngleAxis(25f + (float)rnd.NextDouble() * 25f, axis) * dir;
                var from = Vector3.Lerp(start, end, 0.55f + 0.4f * (float)rnd.NextDouble());
                Branch(s, from, d.normalized, length * 0.6f, radius * 0.55f, depth - 1, rnd);
            }
        }

        // ---------- частицы ----------

        static ParticleSystem Particles(string name, Transform parent, Vector3 pos, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        static Material ParticleMat(string name, Color tint)
        {
            var m = MaterialFor("Particle_" + name, Shader.Find("Legacy Shaders/Particles/Alpha Blended"));
            m.mainTexture = blobTex;
            m.SetColor("_TintColor", tint * 0.5f);
            return m;
        }

        static void SetupSteam(ParticleSystem ps)
        {
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.015f, 0.03f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.04f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;
            main.gravityModifier = -0.004f;
            var em = ps.emission;
            em.rateOverTime = 7f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.025f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 3f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = Fade(0.2f, 0.55f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.025f;
            noise.frequency = 1.2f;
            noise.scrollSpeed = 0.4f;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.y = new ParticleSystem.MinMaxCurve(0.035f, 0.06f);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            ps.Play();
        }

        static void SetupSnow(ParticleSystem ps)
        {
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.prewarm = true;
            main.startLifetime = 13f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 1200;
            var em = ps.emission;
            em.rateOverTime = 90f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(10f, 0.1f, 6f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.04f, 0.06f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.6f, -0.45f);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0.04f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.35f;
            noise.scrollSpeed = 0.2f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = Fade(0.05f, 0.95f);
            ps.Play();
        }

        static void SetupBubbles(ParticleSystem ps)
        {
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.004f, 0.008f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 20;
            var em = ps.emission;
            em.rateOverTime = 14f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.035f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.y = new ParticleSystem.MinMaxCurve(0.06f, 0.09f);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = Fade(0.1f, 0.8f);
            ps.Play();
        }

        // Прозрачность частицы за жизнь: появляется, держится, тает.
        static ParticleSystem.MinMaxGradient Fade(float fadeIn, float peak)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peak, fadeIn), new GradientAlphaKey(peak * 0.6f, 0.7f), new GradientAlphaKey(0f, 1f) });
            return new ParticleSystem.MinMaxGradient(g);
        }

        // ---------- свет, камера, интерфейс ----------

        static void BuildLights()
        {
            var lights = Group("Lights", null, Vector3.zero);
            var warm = Hex("FFD7A6");
            var desk = AddLight("Light_Desk", lights, LightType.Spot, new Vector3(0f, 2.82f, 0.3f), new Vector3(90f, 0f, 0f), warm, 1.45f, 6f, LightShadows.Soft);
            desk.spotAngle = 130f;
            desk.shadowStrength = 0.75f;
            desk.shadowBias = 0.02f;
            desk.shadowNormalBias = 0.2f;
            var maria = AddLight("Light_Maria", lights, LightType.Point, new Vector3(-2f, 2.6f, 1.2f), Vector3.zero, warm, 1.1f, 5f, LightShadows.None);
            AddLight("Light_Back", lights, LightType.Point, new Vector3(0.4f, 2.55f, -1.6f), Vector3.zero, warm, 0.9f, 4.5f, LightShadows.None);
            var sun = AddLight("Sun", lights, LightType.Directional, Vector3.zero, new Vector3(24f, 200f, 0f), Hex("FFF1DD"), 1f, 0f, LightShadows.Soft);
            sun.shadowStrength = 0.85f;
            sun.shadowNormalBias = 0.3f;
            // Холодный свет из окна — заполняет переднюю часть комнаты.
            var window = AddLight("Light_Window", lights, LightType.Spot, new Vector3(-1.6f, 1.7f, 3.4f), new Vector3(15f, 180f, 0f), Hex("DCE8F5"), 1.2f, 6f, LightShadows.None);
            window.spotAngle = 120f;
            var cool = Hex("E6F0FF");
            AddLight("Light_Shop_1", lights, LightType.Point, new Vector3(4.5f, 4f, 1f), Vector3.zero, cool, 1.6f, 8f, LightShadows.None);
            AddLight("Light_Shop_2", lights, LightType.Point, new Vector3(7.2f, 4f, 1f), Vector3.zero, cool, 1.6f, 8f, LightShadows.None);
            AddLight("Light_Shop_3", lights, LightType.Point, new Vector3(5.8f, 4f, 5.2f), Vector3.zero, cool, 1.6f, 8f, LightShadows.None);
            maria.renderMode = LightRenderMode.ForcePixel;

            Probe("Reflections_Office", lights, new Vector3(-0.5f, 1.45f, 0.5f), new Vector3(6f, 2.9f, 6f));
            Probe("Reflections_Shop", lights, new Vector3(6.25f, 2.25f, 2.75f), new Vector3(7.5f, 4.5f, 10.5f));
        }

        static Light AddLight(string name, Transform parent, LightType type, Vector3 pos, Vector3 euler, Color color,
            float intensity, float range, LightShadows shadows)
        {
            var t = Group(name, parent, pos);
            t.localEulerAngles = euler;
            var l = t.gameObject.AddComponent<Light>();
            l.type = type;
            l.color = color;
            l.intensity = intensity;
            if (range > 0f) l.range = range;
            l.shadows = shadows;
            l.lightmapBakeType = LightmapBakeType.Realtime;
            return l;
        }

        static void Probe(string name, Transform parent, Vector3 pos, Vector3 size)
        {
            var p = Group(name, parent, pos).gameObject.AddComponent<ReflectionProbe>();
            p.mode = ReflectionProbeMode.Realtime;
            p.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            p.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            p.size = size;
            p.boxProjection = true;
            p.resolution = 128;
            p.hdr = true;
        }

        static void BuildCamera(Transform chair)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = Eye;
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 80f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Hex("0B1110");
            cam.allowMSAA = true;
            cam.allowHDR = true;
            go.AddComponent<AudioListener>();
            go.AddComponent<SeatedLook>().chair = chair;
            go.AddComponent<KuroPost>().shader = Shader.Find("Hidden/Kuro/Post");
        }

        static void BuildUi(Interactable monitor)
        {
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UiDir + "/Kuro.uss");
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(UiDir + "/KuroTheme.tss");
            string panelPath = UiDir + "/KuroPanel.asset";
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(panelPath);
            if (!panel)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, panelPath);
            }
            panel.themeStyleSheet = theme;
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            EditorUtility.SetDirty(panel);

            var hud = new GameObject("HUD");
            var hudDoc = hud.AddComponent<UIDocument>();
            hudDoc.panelSettings = panel;
            hudDoc.sortingOrder = 0;
            hud.AddComponent<OfficeHud>().styleSheet = sheet;

            // Имя важно: страница шлет сообщения объекту WebBridge.
            new GameObject("WebBridge").AddComponent<WebBridge>();

            var os = new GameObject("KuroOS");
            var osDoc = os.AddComponent<UIDocument>();
            osDoc.panelSettings = panel;
            osDoc.sortingOrder = 10;
            var kuro = os.AddComponent<KuroOS>();
            kuro.styleSheet = sheet;
            kuro.monitor = monitor;
            kuro.screenSurface = monitor.transform.Find("Screen").GetComponent<Renderer>();
        }

        // Неподвижное без Interactable и анимаций — в статические пакеты (меньше вызовов отрисовки).
        static void MarkStatic()
        {
            foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name != "Arrival") // включается только на время ролика
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var t = r.transform;
                    bool moving = r.GetComponent<TextMesh>();
                    for (var p = t; p && !moving; p = p.parent)
                        moving = p.GetComponent<Interactable>() || p.GetComponent<WindSheet>() || p.GetComponent<PlantSway>() ||
                                 p.GetComponent<Spinner>() || p.GetComponent<Conveyor>() || p.GetComponent<Blinker>() ||
                                 p.GetComponent<SeatedLook>() || p.name == "Chair_Player";
                    if (!moving) GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic);
                }
        }

        // ---------- предметы ----------

        // Один коллайдер на весь предмет (по габаритам частей) и Interactable с точкой приближения.
        // action — какое окно демо открыть в браузере (см. WebBridge).
        static Interactable AddInteractable(Transform root, string label, string action, Transform focus, float padding = 0.01f)
        {
            StripColliders(root);
            var bounds = LocalBounds(root);
            var box = root.gameObject.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = bounds.size + Vector3.one * (padding * 2f);
            var it = root.gameObject.AddComponent<Interactable>();
            it.label = label;
            it.focusPoint = focus;
            it.outlineMaterial = outline;
            it.webAction = action;
            return it;
        }

        // Точка, куда встает камера: смотрит на target под углами yaw/pitch с расстояния dist.
        static Transform FocusFrom(Transform root, Vector3 target, float yaw, float pitch, float dist)
        {
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            var t = new GameObject("FocusPoint").transform;
            t.SetParent(root, false);
            t.SetPositionAndRotation(target - rot * Vector3.forward * dist, rot);
            return t;
        }

        static Bounds LocalBounds(Transform root)
        {
            bool any = false;
            var b = new Bounds();
            foreach (var f in root.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = f.sharedMesh;
                if (!mesh || f.GetComponent<TextMesh>()) continue;
                var mb = mesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    var p = root.InverseTransformPoint(f.transform.TransformPoint(corner));
                    if (any) b.Encapsulate(p);
                    else b = new Bounds(p, Vector3.zero);
                    any = true;
                }
            }
            return b;
        }

        static void StripColliders(Transform root)
        {
            foreach (var c in root.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        }

        // ---------- детали ----------

        static Vector2 V(float r, float y) => new Vector2(r, y);

        static Transform Group(string name, Transform parent, Vector3 pos, float rotY = 0f)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localEulerAngles = new Vector3(0f, rotY, 0f);
            return t;
        }

        // Меш с материалом; по умолчанию с коробчатым коллайдером, чтобы взгляд не «просвечивал» сквозь мебель.
        static GameObject Part(string name, Transform parent, Mesh mesh, Material mat, Vector3 pos, Vector3 euler = default, float scale = 1f, bool collide = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collide) go.AddComponent<BoxCollider>();
            return go;
        }

        // Коробка со скругленными ребрами. bevel < 0 — фаска подбирается по размеру.
        static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, Vector3 euler = default, float bevel = -1f)
        {
            if (bevel < 0f) bevel = Mathf.Min(0.006f, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.3f);
            int seg = bevel >= 0.01f ? 4 : 3;
            return Part(name, parent, MeshKit.RoundedBox(size, bevel, seg), mat, pos, euler);
        }

        // Стена, пол, потолок: острые ребра, UV в метрах.
        static GameObject Slab(string name, Transform parent, Vector3 pos, Vector3 size, Material mat) => Box(name, parent, pos, size, mat, default, 0f);

        // Цилиндр вдоль локальной Y с фаской по краям.
        static GameObject Cyl(string name, Transform parent, Vector3 pos, float radius, float height, Material mat, Vector3 euler = default, float bevel = -1f, int sides = 32)
        {
            if (bevel < 0f) bevel = Mathf.Min(radius * 0.2f, height * 0.25f, 0.004f);
            float h = height / 2f;
            Vector2[] profile;
            if (bevel <= 1e-5f)
                profile = new[] { V(0f, -h), V(radius, -h), V(radius, -h), V(radius, h), V(radius, h), V(0f, h) };
            else
            {
                float c = bevel * 0.29f; // скругление четвертью окружности, приближенное двумя отрезками
                profile = new[]
                {
                    V(0f, -h), V(radius - bevel, -h), V(radius - c, -h + c), V(radius, -h + bevel),
                    V(radius, h - bevel), V(radius - c, h - c), V(radius - bevel, h), V(0f, h),
                };
            }
            var mesh = MeshKit.Lathe(MeshKit.Key("Cyl", radius, height, bevel, sides), profile, sides);
            return Part(name, parent, mesh, mat, pos, euler);
        }

        // Эллипсоид: сфера диаметра 1, растянутая до size.
        static GameObject Ball(string name, Transform parent, Vector3 pos, Vector3 size, Material mat)
        {
            var mesh = MeshKit.Get("Sphere", s =>
            {
                var prof = new Vector2[17];
                for (int i = 0; i <= 16; i++)
                {
                    float a = -Mathf.PI / 2f + i / 16f * Mathf.PI;
                    prof[i] = new Vector2(Mathf.Cos(a) * 0.5f, Mathf.Sin(a) * 0.5f);
                }
                s.Lathe(prof, 32);
            });
            var go = Part(name, parent, mesh, mat, pos);
            go.transform.localScale = size;
            return go;
        }

        static GameObject Lathe(string name, Transform parent, string key, Vector2[] profile, Material mat, Vector3 pos, Vector3 euler, int sides = 32) =>
            Part(name, parent, MeshKit.Lathe(key, profile, sides), mat, pos, euler);

        static GameObject Tube(string name, Transform parent, string key, Vector3[] path, float radius, Material mat, Vector3 pos, int sides = 12) =>
            Part(name, parent, MeshKit.Tube(key, path, radius, sides), mat, pos);

        static GameObject NoShadows(GameObject go)
        {
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        // Мягкое пятно тени под предметом на горизонтальной поверхности (worldPos — центр, на поверхности).
        static void Blob(Transform parent, Vector3 worldPos, Vector2 size, float strength, float rotY = 0f)
        {
            var go = Part("ContactShadow", parent, MeshKit.Get("Quad", s => s.Quad1()), BlobMat(strength), Vector3.zero, collide: false);
            go.transform.position = worldPos + Vector3.up * 0.0012f;
            go.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            go.transform.localScale = Vector3.one;
            var world = go.transform.lossyScale;
            go.transform.localScale = new Vector3(size.x / world.x, 1f / world.y, size.y / world.z);
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        static Material BlobMat(float strength)
        {
            string name = "AO_Blob_" + Mathf.RoundToInt(strength * 100f);
            var m = MaterialFor(name, Shader.Find("Kuro/AO"));
            m.mainTexture = blobTex;
            m.SetFloat("_Strength", strength);
            return m;
        }

        // Полоса затенения вдоль угла от a до b: темнее у угла, светлеет в сторону inward.
        // facing — нормаль поверхности, на которой лежит полоса. width 0 — по умолчанию 0,3 м.
        static void Shade(Transform parent, Vector3 a, Vector3 b, Vector3 inward, Vector3 facing, float width, float strength)
        {
            if (width <= 0f) width = 0.12f;
            if (strength <= 0f) strength = 0.35f;
            string name = "AO_Edge_" + Mathf.RoundToInt(strength * 100f);
            var m = MaterialFor(name, Shader.Find("Kuro/AO"));
            m.mainTexture = aoEdge.mainTexture;
            m.SetFloat("_Strength", strength);
            var go = Part("CornerShade", parent, MeshKit.Get("Quad", s => s.Quad1()), m, Vector3.zero, collide: false);
            var along = b - a;
            go.transform.position = (a + b) / 2f + inward.normalized * width / 2f + facing.normalized * 0.0015f;
            go.transform.rotation = Quaternion.LookRotation(inward.normalized, facing.normalized);
            go.transform.localScale = Vector3.one;
            var world = go.transform.lossyScale;
            // Локальная X — вдоль угла, Z — от угла.
            go.transform.localScale = new Vector3(along.magnitude / world.x, 1f / world.y, width / world.z);
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        // Надпись читается, если смотреть вдоль локального +Z.
        static TextMesh Text(string name, Transform parent, Vector3 pos, string text, float height, string hex,
            Vector3 euler = default, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = 96;
            tm.characterSize = height / LineHeight;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = style;
            tm.color = Hex(hex);
            tm.text = text;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = worldText;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return tm;
        }

        // ---------- материалы ----------

        static Material Mat(string name, string hex, float smoothness = 0.2f, float metallic = 0f)
        {
            var m = MaterialFor(name, Shader.Find("Standard"));
            m.color = Hex(hex);
            m.SetFloat("_Glossiness", smoothness);
            m.SetFloat("_Metallic", metallic);
            return m;
        }

        // Материал с текстурой и картой нормалей; tile — сколько повторов на метр (UV мешей — в метрах).
        static Material Surface(string name, TextureKit.Set set, string tintHex, float smoothness, float tile, float metallic = 0f, float bump = 1f)
        {
            var m = Mat(name, tintHex, smoothness, metallic);
            m.mainTexture = set.albedo;
            m.mainTextureScale = new Vector2(tile, tile);
            m.SetTexture("_BumpMap", set.normal);
            m.SetFloat("_BumpScale", bump);
            m.EnableKeyword("_NORMALMAP");
            return m;
        }

        static Material DocMat(int variant)
        {
            var m = Mat("Doc_" + variant, "E9E4DA", 0.12f);
            m.mainTexture = docs[variant];
            return m;
        }

        static Material Emissive(string name, string baseHex, string emitHex, float intensity)
        {
            var m = Mat(name, baseHex, 0.5f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Hex(emitHex) * intensity);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            return m;
        }

        static Material Glass(string name, Color color)
        {
            var m = MaterialFor(name, Shader.Find("Standard"));
            m.color = color;
            m.SetFloat("_Glossiness", 0.95f);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Mode", 3f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.One);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHABLEND_ON");
            m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        static Material MaterialFor(string name, Shader shader)
        {
            if (materials.TryGetValue(name, out var cached)) return cached;
            string path = $"{MaterialDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader)
            {
                m.shader = shader;
            }
            materials[name] = m;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.magenta;

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}

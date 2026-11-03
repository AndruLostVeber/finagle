using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kuro.EditorTools
{
    // Ролик первого дня: башня делового центра «Меридиан», площадь со снегом, флагами и фонарями,
    // улица с машинами, стеклянное лобби с ресепшен, турникетами и дверью офиса 104 «Куро Продакшн».
    // Стоит в стороне от офиса (x −1000), включается только на время ролика (ArrivalCinematic).
    // Локальные оси: башня вдоль −Z, вход в лобби смотрит в +Z, камера идет от улицы к входу.
    public static partial class OfficeBuilder
    {
        static readonly Vector3 ArrivalOrigin = new Vector3(-1000f, 0f, 0f);

        static void BuildArrival()
        {
            var root = Group("Arrival", null, ArrivalOrigin);
            var (facadeTex, facadeEm) = TextureKit.Facade("Facade", 41);
            var facade = FacadeMat("Facade", facadeTex, facadeEm, "FFFFFF");
            var facade2 = FacadeMat("Facade_Warm", facadeTex, facadeEm, "E8E2D2");
            var paving = Surface("Paving", concrete, "C9C6BF", 0.3f, 0.5f);
            var snow = Surface("SnowGround", plaster, "F4F7FA", 0.35f, 0.25f, bump: 0.6f);
            var asphalt = Surface("Asphalt", concrete, "4A4D50", 0.35f, 0.4f);
            var steel = Surface("Steel", brushed, "B9C2C6", 0.65f, 1f, 0.85f);
            var darkMetal = Mat("MetalDark", "2A2F33", 0.55f, 0.5f);
            var white = Mat("Paint_White", "F2F2EF", 0.5f);

            // ---------- земля, площадь, улица ----------
            Slab("Snow", root, new Vector3(0f, -0.15f, 0f), new Vector3(420f, 0.2f, 420f), snow);
            Slab("Plaza", root, new Vector3(0f, -0.05f, 36f), new Vector3(52f, 0.1f, 48f), paving);
            Slab("Base", root, new Vector3(0f, -0.05f, -9f), new Vector3(60f, 0.1f, 42f), paving);
            Slab("Street", root, new Vector3(0f, -0.04f, 67f), new Vector3(420f, 0.1f, 14f), asphalt);
            Box("Curb", root, new Vector3(0f, 0.06f, 60.1f), new Vector3(420f, 0.14f, 0.3f), Mat("Curb", "9A9C9E", 0.3f), bevel: 0.03f);
            for (int i = -12; i <= 12; i++)
                Slab("Lane", root, new Vector3(i * 6f, 0.015f, 67f), new Vector3(3f, 0.02f, 0.15f), white);

            // ---------- башня ----------
            Tower(root, "Tower", new Vector3(0f, 0f, -15f), 36f, 30f, 118f, facade, steel, 8.5f);
            Text("Crown", root, new Vector3(0f, 113f, 0.35f), "МЕРИДИАН", 4.2f, "FFFFFF", new Vector3(0f, 180f, 0f), FontStyle.Bold);
            Tube("Antenna", root, "Antenna", new[] { new Vector3(0f, 118f, -15f), new Vector3(0f, 138f, -15f) }, 0.25f, steel, Vector3.zero, 12);
            Ball("Beacon", root, new Vector3(0f, 138.4f, -15f), Vector3.one * 0.8f, Emissive("Beacon", "C4453B", "FF4A3A", 3f));

            // Соседние башни и дальние дома.
            Tower(root, "Tower_B", new Vector3(-60f, 0f, -38f), 30f, 30f, 82f, facade2, steel);
            Tower(root, "Tower_C", new Vector3(56f, 0f, -26f), 26f, 26f, 64f, facade2, steel);
            Tower(root, "Tower_D", new Vector3(-24f, 0f, -125f), 40f, 30f, 104f, facade, steel);
            Tower(root, "Tower_E", new Vector3(92f, 0f, -112f), 36f, 36f, 142f, facade, steel);
            Tower(root, "Tower_F", new Vector3(-118f, 0f, -90f), 34f, 30f, 58f, facade2, steel);

            BuildLobby(root, steel, darkMetal);
            BuildPlaza(root, steel, darkMetal);

            // Снег идет над площадью и у входа.
            var snowfall = Particles("Snowfall", root, new Vector3(0f, 14f, 30f), ParticleMat("Snowflake", new Color(1f, 1f, 1f, 0.9f)));
            SetupSnow(snowfall);
            var sm = snowfall.main;
            sm.startLifetime = 19f;
            sm.maxParticles = 5000;
            sm.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
            var se = snowfall.emission;
            se.rateOverTime = 260f;
            var ss = snowfall.shape;
            ss.scale = new Vector3(40f, 0.1f, 50f);
            var sv = snowfall.velocityOverLifetime;
            sv.x = new ParticleSystem.MinMaxCurve(-0.1f, 0.15f);
            sv.y = new ParticleSystem.MinMaxCurve(-0.85f, -0.65f);
            sv.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);

            Probe("Reflections_Plaza", root, new Vector3(0f, 6f, 30f), new Vector3(80f, 20f, 70f));
            Probe("Reflections_Lobby", root, new Vector3(0f, 4f, 6f), new Vector3(28f, 8f, 12f));

            // Небо зимнего утра и дымка — видны только в ролике (в офисе камера красит фон сама).
            var sky = MaterialFor("Sky_Winter", Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_SunSize", 0.035f);
            sky.SetFloat("_AtmosphereThickness", 1.05f);
            sky.SetColor("_SkyTint", Hex("A9B9CC"));
            sky.SetColor("_GroundColor", Hex("B9C1C8"));
            sky.SetFloat("_Exposure", 1.2f);
            RenderSettings.skybox = sky;
            var sun = GameObject.Find("Lights/Sun");
            if (sun) RenderSettings.sun = sun.GetComponent<Light>();
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Hex("C6D0D9");
            RenderSettings.fogStartDistance = 70f;
            RenderSettings.fogEndDistance = 560f;

            StripColliders(root);

            // Путь камеры: смотрит вверх на башню, идет к дверям, турникет, дверь офиса 104.
            var director = new GameObject("ArrivalDirector").AddComponent<ArrivalCinematic>();
            director.world = root.gameObject;
            director.origin = root;
            director.times = new[] { 0f, 3f, 7.5f, 10f, 12.5f, 14f, 16.5f, 18f };
            director.points = new[]
            {
                new Vector3(2f, 1.7f, 44f), new Vector3(1.5f, 1.7f, 38f), new Vector3(0.5f, 1.65f, 23f), new Vector3(0f, 1.65f, 14.5f),
                new Vector3(0f, 1.65f, 8.6f), new Vector3(0.3f, 1.65f, 6.3f), new Vector3(8f, 1.65f, 2.6f), new Vector3(10f, 1.65f, 0.2f),
            };
            director.looks = new[]
            {
                new Vector3(0f, 75f, -15f), new Vector3(0f, 32f, -8f), new Vector3(0f, 4f, 12f), new Vector3(0f, 1.8f, 6f),
                new Vector3(0f, 1.6f, 2f), new Vector3(9f, 1.6f, 1f), new Vector3(10f, 1.5f, 0f), new Vector3(10f, 1.5f, -3f),
            };
            root.gameObject.SetActive(false);
        }

        static Material FacadeMat(string name, Texture2D albedo, Texture2D emission, string tint)
        {
            var m = Mat(name, tint, 0.62f, 0.15f);
            m.mainTexture = albedo;
            m.mainTextureScale = new Vector2(1f / 12.8f, 1f / 28.8f);
            m.EnableKeyword("_EMISSION");
            m.SetTexture("_EmissionMap", emission);
            m.SetColor("_EmissionColor", Color.white * 1.1f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            return m;
        }

        // Башня: четыре стеклянных фасада, угловые пилоны, кровля с парапетом. pos — центр основания.
        // frontFrom — с какой высоты начинается стекло спереди (ниже — цоколь с лобби).
        static void Tower(Transform root, string name, Vector3 pos, float w, float d, float h, Material facade, Material frame, float frontFrom = 0f)
        {
            var t = Group(name, root, pos);
            Part("Front", t, MeshKit.Panel(w, h - frontFrom), facade, new Vector3(0f, (h + frontFrom) / 2f, d / 2f), new Vector3(0f, 180f, 0f), collide: false);
            Part("Back", t, MeshKit.Panel(w, h), facade, new Vector3(0f, h / 2f, -d / 2f), Vector3.zero, collide: false);
            Part("Left", t, MeshKit.Panel(d, h), facade, new Vector3(-w / 2f, h / 2f, 0f), new Vector3(0f, 90f, 0f), collide: false);
            Part("Right", t, MeshKit.Panel(d, h), facade, new Vector3(w / 2f, h / 2f, 0f), new Vector3(0f, -90f, 0f), collide: false);
            foreach (float x in new[] { -1f, 1f })
                foreach (float z in new[] { -1f, 1f })
                    Box("Pilaster", t, new Vector3(x * w / 2f, h / 2f, z * d / 2f), new Vector3(1.1f, h, 1.1f), frame, bevel: 0.2f);
            Box("Roof", t, new Vector3(0f, h + 0.4f, 0f), new Vector3(w + 0.8f, 0.8f, d + 0.8f), Mat("RoofDark", "3A3F44", 0.3f), bevel: 0.1f);
            Box("Plant", t, new Vector3(w * 0.15f, h + 2f, -d * 0.1f), new Vector3(w * 0.4f, 2.6f, d * 0.4f), Mat("RoofDark", "3A3F44", 0.3f), bevel: 0.2f);
        }

        // Лобби: стеклянный объем 28×12×8 перед башней, вход по центру, дверь офиса 104 справа в глубине.
        static void BuildLobby(Transform root, Material steel, Material darkMetal)
        {
            var lobby = Group("Lobby", root, Vector3.zero);
            var glass = Glass("LobbyGlass", new Color(0.6f, 0.7f, 0.78f, 0.12f));
            var marble = Surface("Marble", plaster, "D6CFC2", 0.72f, 0.5f, bump: 0.2f);
            var stone = Surface("StoneWall", plaster, "8C7F72", 0.4f, 0.5f, bump: 0.8f);
            var gold = Mat("Gold", "C9A452", 0.8f, 0.9f);
            var teal = Mat("Teal", "1F6F68", 0.3f);

            Slab("Floor", lobby, new Vector3(0f, 0.012f, 6f), new Vector3(28f, 0.02f, 12f), marble);
            Box("Roof", lobby, new Vector3(0f, 8.25f, 7f), new Vector3(29f, 0.5f, 18f), Mat("Canopy", "DADDE0", 0.5f, 0.3f), bevel: 0.05f);
            Box("RoofEdge", lobby, new Vector3(0f, 8.25f, 16.02f), new Vector3(29.1f, 0.55f, 0.1f), steel, bevel: 0.02f);
            var downlight = Emissive("Downlight", "FFF3DC", "FFE2B0", 1.5f);
            for (int i = 0; i < 6; i++)
                Cyl("Downlight", lobby, new Vector3(-7.5f + i * 3f, 7.99f, 14f), 0.2f, 0.02f, downlight);

            // Фасад лобби: стекло, стойки, ригель; проем входа x −1,7…1,7, высота 2,9.
            NoShadows(Box("Glass_L", lobby, new Vector3(-7.85f, 4f, 12f), new Vector3(12.3f, 8f, 0.03f), glass, bevel: 0f));
            NoShadows(Box("Glass_R", lobby, new Vector3(7.85f, 4f, 12f), new Vector3(12.3f, 8f, 0.03f), glass, bevel: 0f));
            NoShadows(Box("Glass_Top", lobby, new Vector3(0f, 5.45f, 12f), new Vector3(3.4f, 5.1f, 0.03f), glass, bevel: 0f));
            foreach (float x in new[] { -14f, -11.6f, -9.2f, -6.8f, -4.4f, -1.75f, 1.75f, 4.4f, 6.8f, 9.2f, 11.6f, 14f })
                Box("Mullion", lobby, new Vector3(x, 4f, 12.04f), new Vector3(0.14f, 8f, 0.22f), darkMetal, bevel: 0.02f);
            Box("Transom", lobby, new Vector3(0f, 2.95f, 12.04f), new Vector3(28f, 0.14f, 0.22f), darkMetal, bevel: 0.02f);
            foreach (float x in new[] { -14f, 14f })
            {
                NoShadows(Box("Glass_Side", lobby, new Vector3(x, 4f, 6f), new Vector3(0.03f, 8f, 12f), glass, bevel: 0f));
                foreach (float z in new[] { 3f, 6f, 9f })
                    Box("Mullion", lobby, new Vector3(x, 4f, z), new Vector3(0.22f, 8f, 0.14f), darkMetal, bevel: 0.02f);
            }

            // Автоматические двери входа.
            var entrance = Group("Entrance", lobby, new Vector3(0f, 1.3f, 12f));
            var leafL = Box("Leaf_L", entrance, new Vector3(-0.82f, 0.05f, 0.09f), new Vector3(1.62f, 2.7f, 0.04f), glass, bevel: 0.01f);
            var leafR = Box("Leaf_R", entrance, new Vector3(0.82f, 0.05f, 0.09f), new Vector3(1.62f, 2.7f, 0.04f), glass, bevel: 0.01f);
            Box("Leaf_L_Frame", leafL.transform, new Vector3(0f, -1.3f, 0f), new Vector3(1.62f, 0.1f, 0.06f), darkMetal, bevel: 0.01f);
            Box("Leaf_R_Frame", leafR.transform, new Vector3(0f, -1.3f, 0f), new Vector3(1.62f, 0.1f, 0.06f), darkMetal, bevel: 0.01f);
            var door = entrance.gameObject.AddComponent<AutoDoor>();
            door.leaves = new[] { leafL.transform, leafR.transform };
            door.openOffsets = new[] { new Vector3(-1.55f, 0f, 0f), new Vector3(1.55f, 0f, 0f) };
            door.radius = 6f;
            Box("Mat", lobby, new Vector3(0f, 0.015f, 13f), new Vector3(3.2f, 0.02f, 2f), Mat("Rubber", "1E1E1E", 0.35f), bevel: 0.005f);

            // Вывеска над входом.
            var sign = Group("Sign", lobby, new Vector3(0f, 8.25f, 16.1f), 180f);
            Text("Name", sign, new Vector3(0f, 0f, -0.02f), "ДЕЛОВОЙ ЦЕНТР «МЕРИДИАН»", 0.42f, "FFFFFF", style: FontStyle.Bold);

            // Каменная стена — основание башни внутри лобби: логотип, лифты, дверь офиса.
            // Цоколь во всю ширину башни, проем двери офиса x 8,9…11,1, высота 2,9.
            Box("StoneWall_L", lobby, new Vector3(-4.55f, 4.25f, 0.1f), new Vector3(26.9f, 8.5f, 0.2f), stone, bevel: 0f);
            Box("StoneWall_R", lobby, new Vector3(14.55f, 4.25f, 0.1f), new Vector3(6.9f, 8.5f, 0.2f), stone, bevel: 0f);
            Box("StoneWall_T", lobby, new Vector3(10f, 5.7f, 0.1f), new Vector3(2.2f, 5.6f, 0.2f), stone, bevel: 0f);
            var logo = Group("Logo", lobby, new Vector3(-3.5f, 5.6f, 0.22f), 180f);
            Text("Text", logo, Vector3.zero, "МЕРИДИАН", 0.9f, "D9B45A", style: FontStyle.Bold);
            Text("Sub", logo, new Vector3(0f, -0.75f, 0f), "деловой центр", 0.32f, "E9E1CF");
            foreach (float x in new[] { -10f, -7f })
            {
                Box("LiftFrame", lobby, new Vector3(x, 1.3f, 0.24f), new Vector3(1.6f, 2.6f, 0.06f), darkMetal, bevel: 0.02f);
                Box("LiftDoor_L", lobby, new Vector3(x - 0.33f, 1.2f, 0.28f), new Vector3(0.64f, 2.3f, 0.04f), steel, bevel: 0.005f);
                Box("LiftDoor_R", lobby, new Vector3(x + 0.33f, 1.2f, 0.28f), new Vector3(0.64f, 2.3f, 0.04f), steel, bevel: 0.005f);
                Box("LiftLamp", lobby, new Vector3(x, 2.75f, 0.28f), new Vector3(0.5f, 0.12f, 0.03f), Emissive("LiftLamp", "2A2F33", "FFB347", 2f), bevel: 0.01f);
            }

            // Дверь офиса 104 «Куро Продакшн»: стеклянные створки разъезжаются, за ними коридор.
            Box("OfficeFrame_L", lobby, new Vector3(8.95f, 1.45f, 0.25f), new Vector3(0.1f, 2.9f, 0.12f), darkMetal, bevel: 0.01f);
            Box("OfficeFrame_R", lobby, new Vector3(11.05f, 1.45f, 0.25f), new Vector3(0.1f, 2.9f, 0.12f), darkMetal, bevel: 0.01f);
            Box("OfficeFrame_T", lobby, new Vector3(10f, 2.95f, 0.25f), new Vector3(2.2f, 0.1f, 0.12f), darkMetal, bevel: 0.01f);
            var officeDoor = Group("OfficeDoor", lobby, new Vector3(10f, 1.2f, 0.25f));
            var oL = Box("Leaf_L", officeDoor, new Vector3(-0.5f, 0.25f, 0f), new Vector3(1f, 2.85f, 0.03f), glass, bevel: 0.006f);
            var oR = Box("Leaf_R", officeDoor, new Vector3(0.5f, 0.25f, 0f), new Vector3(1f, 2.85f, 0.03f), glass, bevel: 0.006f);
            var od = officeDoor.gameObject.AddComponent<AutoDoor>();
            od.leaves = new[] { oL.transform, oR.transform };
            od.openOffsets = new[] { new Vector3(-0.95f, 0f, 0f), new Vector3(0.95f, 0f, 0f) };
            od.radius = 3.4f;
            var plate = Group("OfficeSign", lobby, new Vector3(10f, 3.35f, 0.24f), 180f);
            Box("Board", plate, new Vector3(0f, 0f, 0.02f), new Vector3(2.2f, 0.5f, 0.04f), teal, bevel: 0.015f);
            Text("Text", plate, new Vector3(0f, 0.05f, -0.005f), "104 · КУРО ПРОДАКШН", 0.17f, "FFF6E6", style: FontStyle.Bold);
            // Коридор за дверью: темно, в конце светится вывеска.
            var corridorMat = Mat("Corridor", "2B2622", 0.2f);
            Slab("C_Floor", lobby, new Vector3(10f, 0f, -2.2f), new Vector3(2.2f, 0.04f, 4.6f), corridorMat);
            Slab("C_Ceil", lobby, new Vector3(10f, 3f, -2.2f), new Vector3(2.2f, 0.04f, 4.6f), corridorMat);
            Slab("C_WallL", lobby, new Vector3(8.9f, 1.5f, -2.2f), new Vector3(0.04f, 3f, 4.6f), corridorMat);
            Slab("C_WallR", lobby, new Vector3(11.1f, 1.5f, -2.2f), new Vector3(0.04f, 3f, 4.6f), corridorMat);
            Slab("C_End", lobby, new Vector3(10f, 1.5f, -4.5f), new Vector3(2.2f, 3f, 0.04f), corridorMat);
            Box("C_Glow", lobby, new Vector3(10f, 1.7f, -4.46f), new Vector3(1.2f, 0.3f, 0.02f), Emissive("KuroGlow", "1F6F68", "48C6B4", 2.5f), bevel: 0.01f);

            // Ресепшен.
            Box("Desk", lobby, new Vector3(-7f, 0.55f, 6.2f), new Vector3(4.2f, 1.1f, 0.9f), Mat("DeskWhite", "F4F2EE", 0.7f), bevel: 0.06f);
            Box("DeskTop", lobby, new Vector3(-7f, 1.12f, 6.25f), new Vector3(4.4f, 0.05f, 1.1f), Surface("DeskWood", oak, "FFFFFF", 0.35f, 1f), bevel: 0.015f);
            Box("DeskStrip", lobby, new Vector3(-7f, 0.85f, 6.66f), new Vector3(4.2f, 0.05f, 0.02f), Emissive("StripTeal", "1F6F68", "48C6B4", 2.5f), bevel: 0.005f);
            var mon = Group("Monitor", lobby, new Vector3(-6.2f, 1.145f, 6f), 180f);
            MonitorModel(mon, 0.8f, false);

            // Турникеты: створки средней дорожки поворачиваются, когда камера рядом.
            foreach (float x in new[] { -3f, -1f, 1f, 3f })
            {
                Box("Turnstile", lobby, new Vector3(x, 0.5f, 7f), new Vector3(0.22f, 1f, 1.1f), steel, bevel: 0.04f);
                Box("Reader", lobby, new Vector3(x, 1.01f, 7.35f), new Vector3(0.16f, 0.02f, 0.2f), Emissive("Reader", "1E4A46", "6BEA9B", 1.6f), bevel: 0.005f);
            }
            var gate = Group("Gate", lobby, new Vector3(0f, 1f, 7f));
            var pivotL = Group("Flap_L", gate, new Vector3(-0.89f, -0.4f, 0f));
            NoShadows(Box("Glass", pivotL, new Vector3(0.4f, 0f, 0f), new Vector3(0.8f, 0.7f, 0.02f), glass, bevel: 0.01f));
            var pivotR = Group("Flap_R", gate, new Vector3(0.89f, -0.4f, 0f));
            NoShadows(Box("Glass", pivotR, new Vector3(-0.4f, 0f, 0f), new Vector3(0.8f, 0.7f, 0.02f), glass, bevel: 0.01f));
            var gd = gate.gameObject.AddComponent<AutoDoor>();
            gd.leaves = new[] { pivotL, pivotR };
            gd.openAngles = new[] { new Vector3(0f, 90f, 0f), new Vector3(0f, -90f, 0f) };
            gd.radius = 2.8f;
            gd.speed = 2f;
            foreach (float x in new[] { -2.1f, 2.1f })
                NoShadows(Box("Flap", lobby, new Vector3(x, 0.6f, 7f), new Vector3(1.6f, 0.7f, 0.02f), glass, bevel: 0.01f));

            // Подвесные светильники, растения, диван.
            var shade = Mat("Gold", "C9A452", 0.8f, 0.9f);
            var bulb = Emissive("Bulb", "FFF3DC", "FFE2B0", 3f);
            foreach (float x in new[] { -6f, -2f, 2f, 6f })
            {
                Tube("Cord", lobby, "PendantCord", new[] { new Vector3(0f, 8f, 0f), new Vector3(0f, 6.2f, 0f) }, 0.01f, darkMetal, new Vector3(x, 0f, 7.5f), 6);
                Lathe("Shade", lobby, "PendantShade", new[] { V(0.35f, 0f), V(0.3f, 0.12f), V(0.12f, 0.3f), V(0.03f, 0.36f), V(0f, 0.36f) }, shade, new Vector3(x, 5.85f, 7.5f), Vector3.zero, 32);
                Ball("Bulb", lobby, new Vector3(x, 5.9f, 7.5f), Vector3.one * 0.16f, bulb);
            }
            foreach (var p in new[] { new Vector3(-12.5f, 0f, 10.5f), new Vector3(12.5f, 0f, 10.5f), new Vector3(-12.5f, 0f, 1.5f), new Vector3(6.5f, 0f, 1.2f) })
            {
                var pot = Group("Plant", lobby, p);
                Lathe("Pot", pot, "BigPot", new[] { V(0f, 0f), V(0.35f, 0f), V(0.42f, 0.7f), V(0.45f, 0.75f), V(0f, 0.72f) }, Mat("PotDark", "2E3134", 0.5f), Vector3.zero, Vector3.zero, 32);
                Ball("Leaves", pot, new Vector3(0f, 1.3f, 0f), new Vector3(1.1f, 1.3f, 1.1f), Mat("Leaves", "4E8A4A", 0.45f));
                Ball("Leaves_Top", pot, new Vector3(0.15f, 1.9f, 0.1f), new Vector3(0.7f, 0.8f, 0.7f), Mat("Leaves", "4E8A4A", 0.45f));
            }
            var sofaMat = Surface("ChairFabric", chairFabric, "3E4F86", 0.15f, 1f);
            sofaMat.mainTextureScale = new Vector2(4f, 4f);
            Box("Sofa_Seat", lobby, new Vector3(9f, 0.25f, 9.5f), new Vector3(3f, 0.5f, 1f), sofaMat, bevel: 0.12f);
            Box("Sofa_Back", lobby, new Vector3(9f, 0.7f, 9.95f), new Vector3(3f, 0.9f, 0.25f), sofaMat, bevel: 0.1f);

            var warm = Hex("FFD7A6");
            AddLight("Light_Lobby_1", lobby, LightType.Point, new Vector3(-6f, 5f, 6.5f), Vector3.zero, warm, 1f, 13f, LightShadows.None);
            AddLight("Light_Lobby_2", lobby, LightType.Point, new Vector3(3f, 5f, 6.5f), Vector3.zero, warm, 1f, 13f, LightShadows.None);
            AddLight("Light_Lobby_3", lobby, LightType.Point, new Vector3(9.5f, 3.5f, 2.5f), Vector3.zero, warm, 1.2f, 8f, LightShadows.None);
            AddLight("Light_Canopy", lobby, LightType.Point, new Vector3(0f, 6.5f, 14.5f), Vector3.zero, warm, 1.2f, 10f, LightShadows.None);
        }

        static void BuildPlaza(Transform root, Material steel, Material darkMetal)
        {
            var plaza = Group("PlazaProps", root, Vector3.zero);
            var snowMat = Mat("Snow", "F2F5F8", 0.3f);
            var bulb = Emissive("StreetBulb", "FFF3DC", "FFE2B0", 0.9f);

            // Фонари вдоль дорожки.
            foreach (float z in new[] { 20f, 30f, 40f, 50f })
                foreach (float x in new[] { -7f, 7f })
                {
                    Tube("Pole", plaza, "LampPole", new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 4f, 0f), new Vector3(0f, 4.3f, 0.1f), new Vector3(0f, 4.35f, 0.5f) }, 0.07f, darkMetal, new Vector3(x, 0f, z), 10);
                    Lathe("Head", plaza, "LampHead", new[] { V(0f, 0f), V(0.25f, 0f), V(0.22f, 0.1f), V(0.08f, 0.2f), V(0f, 0.22f) }, darkMetal, new Vector3(x, 4.2f, z + 0.5f), Vector3.zero, 24);
                    Ball("Bulb", plaza, new Vector3(x, 4.17f, z + 0.5f), new Vector3(0.36f, 0.08f, 0.36f), bulb);
                }

            // Скамейки под снегом.
            var benchWood = Surface("DeskWoodDark", darkWood, "FFFFFF", 0.4f, 1f);
            foreach (var p in new[] { new Vector3(-8f, 0f, 25f), new Vector3(8f, 0f, 25f), new Vector3(-8f, 0f, 45f), new Vector3(8f, 0f, 45f) })
            {
                var b = Group("Bench", plaza, p, p.x < 0 ? 90f : -90f);
                for (int i = 0; i < 3; i++) Box("Slat", b, new Vector3(0f, 0.45f, -0.18f + i * 0.18f), new Vector3(1.8f, 0.05f, 0.14f), benchWood, bevel: 0.015f);
                Box("Snow", b, new Vector3(0f, 0.5f, 0f), new Vector3(1.75f, 0.06f, 0.5f), snowMat, bevel: 0.025f);
                foreach (float x in new[] { -0.75f, 0.75f }) Box("Leg", b, new Vector3(x, 0.22f, 0f), new Vector3(0.06f, 0.45f, 0.5f), darkMetal, bevel: 0.01f);
            }

            // Флагштоки: флаги полощутся на ветру.
            string[] flagColors = { "1F6F68", "F2F2EF", "E2A93B" };
            for (int i = 0; i < 3; i++)
            {
                var basePos = new Vector3(-11.5f + i * 2f, 0f, 17f);
                Cyl("Pole", plaza, basePos + new Vector3(0f, 4.5f, 0f), 0.06f, 9f, steel);
                Ball("Top", plaza, basePos + new Vector3(0f, 9.05f, 0f), Vector3.one * 0.16f, Mat("Gold", "C9A452", 0.8f, 0.9f));
                var flag = Part("Flag", plaza, MeshKit.Sheet(1.2f, 1.8f, 8, 14), Mat("Flag_" + flagColors[i], flagColors[i], 0.2f), Vector3.zero, collide: false);
                flag.transform.localPosition = basePos + new Vector3(-0.95f, 8.2f, 0f);
                flag.transform.localRotation = Quaternion.LookRotation(Vector3.right, Vector3.forward);
                var ws = flag.AddComponent<WindSheet>();
                ws.cloth = true;
                ws.lift = 0.2f;
                ws.folds = 0.04f;
                ws.foldCount = 1.5f;
                ws.wave = 0.14f;
                ws.phase = i * 2.3f;
            }

            // Клумбы-кадки с голыми деревьями, сугробы по краям.
            var rnd = new System.Random(17);
            foreach (var p in new[] { new Vector3(-12f, 0f, 28f), new Vector3(12f, 0f, 28f), new Vector3(-12f, 0f, 44f), new Vector3(12f, 0f, 44f) })
            {
                Box("Planter", plaza, p + new Vector3(0f, 0.3f, 0f), new Vector3(2f, 0.6f, 2f), Mat("Curb", "9A9C9E", 0.3f), bevel: 0.05f);
                Box("PlanterSnow", plaza, p + new Vector3(0f, 0.62f, 0f), new Vector3(1.9f, 0.06f, 1.9f), snowMat, bevel: 0.03f);
                var tree = Group("Tree", plaza, p + new Vector3(0f, 0.6f, 0f));
                Part("Branches", tree, MeshKit.Get(MeshKit.Key("PlazaTree", p.x, p.z), s => Branch(s, Vector3.zero, Vector3.up, 3.2f, 0.12f, 3, rnd)), Mat("Bark", "4A3B30", 0.2f), Vector3.zero, collide: false);
            }
            foreach (float x in new[] { -25f, 25f })
                for (int i = 0; i < 6; i++)
                    Ball("Snowbank", plaza, new Vector3(x, 0f, 16f + i * 8f), new Vector3(2.5f, 1.2f, 7f), snowMat);
            for (int i = -8; i <= 8; i++)
                if (Mathf.Abs(i) > 2)
                    Ball("StreetSnow", plaza, new Vector3(i * 9f, 0f, 59.3f), new Vector3(7.5f, 0.8f, 1.6f), snowMat);

            // Стела с названием у улицы.
            var stela = Group("Stela", plaza, new Vector3(9f, 0f, 55f), 180f);
            Box("Body", stela, new Vector3(0f, 1.2f, 0f), new Vector3(4f, 2.4f, 0.6f), Mat("Teal", "1F6F68", 0.3f), bevel: 0.06f);
            Box("Snow", stela, new Vector3(0f, 2.42f, 0f), new Vector3(3.95f, 0.06f, 0.55f), snowMat, bevel: 0.02f);
            Text("Name", stela, new Vector3(0f, 1.45f, -0.31f), "МЕРИДИАН", 0.55f, "FFF6E6", style: FontStyle.Bold);
            Text("Sub", stela, new Vector3(0f, 0.85f, -0.31f), "ДЕЛОВОЙ ЦЕНТР", 0.22f, "E2A93B");

            // Машины на парковке у тротуара.
            string[] carColors = { "C4453B", "F2F2EF", "2F5E8C", "9AA4AF", "3A4045" };
            float[] carX = { -24f, -17f, -10f, 12f, 19f };
            for (int i = 0; i < carX.Length; i++) Car(plaza, new Vector3(carX[i], 0f, 63.3f), carColors[i], i);
        }

        // Машина: корпус, кабина, колеса, фары, снег на крыше. Длина вдоль X.
        static void Car(Transform parent, Vector3 pos, string color, int i)
        {
            var car = Group("Car", parent, pos, i % 2 == 0 ? 0f : 180f);
            var paint = Mat("CarPaint_" + color, color, 0.8f, 0.4f);
            Box("Body", car, new Vector3(0f, 0.62f, 0f), new Vector3(4.3f, 0.75f, 1.8f), paint, bevel: 0.25f);
            Box("Cabin", car, new Vector3(-0.25f, 1.25f, 0f), new Vector3(2.4f, 0.7f, 1.62f), Mat("CarGlass", "1C2630", 0.92f, 0.3f), bevel: 0.25f);
            Box("Roof", car, new Vector3(-0.25f, 1.57f, 0f), new Vector3(2.1f, 0.1f, 1.5f), paint, bevel: 0.04f);
            Box("SnowRoof", car, new Vector3(-0.25f, 1.65f, 0f), new Vector3(1.95f, 0.08f, 1.4f), Mat("Snow", "F2F5F8", 0.3f), bevel: 0.035f);
            foreach (float x in new[] { -1.35f, 1.35f })
                foreach (float z in new[] { -0.82f, 0.82f })
                    Cyl("Wheel", car, new Vector3(x, 0.34f, z), 0.34f, 0.24f, Mat("Rubber", "1E1E1E", 0.35f), new Vector3(90f, 0f, 0f));
            foreach (float z in new[] { -0.6f, 0.6f })
            {
                Box("Headlight", car, new Vector3(2.14f, 0.72f, z), new Vector3(0.04f, 0.14f, 0.32f), Emissive("Headlight", "F4F2EE", "FFFFFF", 0.6f), bevel: 0.01f);
                Box("Taillight", car, new Vector3(-2.14f, 0.72f, z), new Vector3(0.04f, 0.12f, 0.3f), Emissive("Taillight", "8C2F39", "C4453B", 0.8f), bevel: 0.01f);
            }
            Blob(car, car.position, new Vector2(4.8f, 2.3f), 0.5f, i % 2 == 0 ? 0f : 180f);
        }
    }
}

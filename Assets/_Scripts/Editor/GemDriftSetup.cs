using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates Assets/_Scripts/Resources/GemDriftConfig.asset: materials, and measures the Kenney truck
/// (which way it faces, its size, where the flat bed is). Runs automatically if the config is missing;
/// re-run any time from the menu Gem Drift > Rebuild Config.
/// </summary>
[InitializeOnLoad]
public static class GemDriftSetup
{
    const string CarModelPath = "Assets/_Art/Kenney/CarKit/delivery-flat.fbx";
    const string KitPath = "Assets/_Art/Kenney/RacingKit/";
    const string ForestScene = "Assets/Scenes/Forest.unity";
    const string MenuScene = "Assets/Scenes/Menu.unity";
    const string SpaceScene = "Assets/Scenes/Space.unity";
    const string ConfigPath = "Assets/_Scripts/Resources/GemDriftConfig.asset";
    const string GenFolder = "Assets/_Scripts/Generated";
    const float TargetCarLength = 4.2f;

    static GemDriftSetup()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return; // scenes can't be created in play mode; runs again after
            var existing = AssetDatabase.LoadAssetAtPath<GemDriftConfig>(ConfigPath);
            if (existing == null) { Rebuild(); return; }
            if (!MenuIsFirstScene() || !File.Exists(SpaceScene) || EditorBuildSettings.scenes.Length < 4) EnsureMenuScene();
            if (existing.carsVersion != CarsVersion || existing.postResources == null) { BuildCars(existing); AssetDatabase.SaveAssets(); }
        };
    }

    [MenuItem("Gem Drift/Play Circuit (Main) %#p")]
    public static void PlayRace() => Play("Assets/Scenes/Main.unity");

    [MenuItem("Gem Drift/Play Forest %#o")]
    public static void PlayForest() => Play("Assets/Scenes/Forest.unity");

    static void Play(string scenePath)
    {
        if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); return; }
        if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!File.Exists(scenePath)) Rebuild();
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
        EditorApplication.EnterPlaymode();
    }

    [MenuItem("Gem Drift/Play Space %#i")]
    public static void PlaySpace() => Play(SpaceScene);

    [MenuItem("Gem Drift/Play Menu %#m")]
    public static void PlayMenu() => Play(MenuScene);

    [MenuItem("Gem Drift/Build WebGL")]
    public static void BuildWebGL()
    {
        if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); return; }
        var opts = new BuildPlayerOptions
        {
            scenes = new[] { MenuScene, SpaceScene, ForestScene, "Assets/Scenes/Main.unity" }, // the menu is build index 0
            locationPathName = "Builds/WebGL",
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        };
        var report = UnityEditor.BuildPipeline.BuildPlayer(opts);
        Debug.Log($"GEMDRIFT_BUILD {report.summary.result} size={report.summary.totalSize / (1024 * 1024)}MB errors={report.summary.totalErrors} time={report.summary.totalTime}");
    }

    [MenuItem("Gem Drift/Rebuild Config")]
    public static void Rebuild()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Gem Drift: stop Play mode before Rebuild Config."); return; }
        madeTextures.Clear();
        EnsureForestScene(); // first: switching scenes unloads in-memory assets
        Directory.CreateDirectory("Assets/_Scripts/Resources");
        Directory.CreateDirectory(GenFolder);
        AssetDatabase.Refresh();

        var cfg = AssetDatabase.LoadAssetAtPath<GemDriftConfig>(ConfigPath);
        if (cfg == null)
        {
            cfg = ScriptableObject.CreateInstance<GemDriftConfig>();
            AssetDatabase.CreateAsset(cfg, ConfigPath);
        }

        // Bright, saturated palette; procedural textures + normal maps give surfaces some depth.
        cfg.road = TexMat("Road", Color.white, 0.2f, RoadTex, 1.2f);
        cfg.roadLine = Mat("RoadLine", new Color(0.05f, 0.05f, 0.08f), 0.1f);
        cfg.ground = TexMat("Ground", Color.white, 0.05f, GrassTex, 1f);
        cfg.wallA = TexMat("WallRed", new Color(1f, 0.22f, 0.2f), 0.4f, PanelTex, 1.5f, "Panel");
        cfg.wallB = TexMat("WallWhite", new Color(1f, 0.85f, 0.15f), 0.4f, PanelTex, 1.5f, "Panel");
        cfg.gemRed = Mat("GemRed", new Color(1f, 0.1f, 0.25f), 0.95f, 0.2f, new Color(0.9f, 0.05f, 0.15f));
        cfg.gemBlue = Mat("GemBlue", new Color(0.1f, 0.6f, 1f), 0.95f, 0.2f, new Color(0.05f, 0.35f, 0.95f));
        cfg.gemGold = Mat("GemGold", new Color(1f, 0.85f, 0.1f), 0.95f, 0.5f, new Color(0.9f, 0.62f, 0.02f));
        cfg.playerTints = new[] { new Color(1f, 0.35f, 0.3f), new Color(0.3f, 0.65f, 1f) };
        cfg.aiTints = new[] { new Color(1f, 0.85f, 0.2f), new Color(0.35f, 0.95f, 0.4f), new Color(0.75f, 0.45f, 1f), new Color(1f, 0.6f, 0.15f) };
        cfg.checker = CheckerMat();
        cfg.skid = SpriteMat("Skid", new Color(0.05f, 0.05f, 0.05f, 0.55f));
        cfg.spark = SpriteMat("Spark", Color.white);
        cfg.flame = FlameMat();
        cfg.nitroTankShell = Mat("NitroTankShell", new Color(0.62f, 0.66f, 0.72f), 0.85f, 0.7f);
        cfg.nitroGlow = Mat("NitroGlow", new Color(0.85f, 0.4f, 1f), 0.9f, 0f, new Color(0.9f, 0.35f, 1.6f));

        // Forest course
        cfg.dirtRoad = TexMat("DirtRoad", Color.white, 0.05f, DirtTex, 1.3f);
        cfg.forestGround = TexMat("ForestGround", Color.white, 0.03f, ForestFloorTex, 1f);
        cfg.woodA = TexMat("WoodA", new Color(0.78f, 0.5f, 0.28f), 0.15f, WoodTex, 1.4f, "Wood");
        cfg.woodB = TexMat("WoodB", new Color(0.95f, 0.72f, 0.42f), 0.15f, WoodTex, 1.4f, "Wood");
        cfg.treeModels = new[] { ReadableModel(KitPath + "treeLarge.fbx"), ReadableModel(KitPath + "treeSmall.fbx") };
        cfg.finishArch = AssetDatabase.LoadAssetAtPath<GameObject>(KitPath + "overheadRoundColored.fbx");

        // Scenery props (readable so they can be static-batched at runtime)
        string[] racing = { "bannerTowerRed", "bannerTowerGreen", "flagCheckers", "flagCheckersSmall", "flagRed", "flagGreen",
                            "grandStand", "grandStandCovered", "pitsGarage", "pitsOffice", "tent", "tentLong", "radarEquipment",
                            "lightPostLarge", "lightRedDouble", "billboard", "billboardLow", "grass" };
        var names = new System.Collections.Generic.List<string>();
        var models = new System.Collections.Generic.List<GameObject>();
        foreach (var r in racing) { names.Add(r); models.Add(ReadableModel(KitPath + r + ".fbx")); }
        names.Add("cone"); models.Add(ReadableModel("Assets/_Art/Kenney/CarKit/cone.fbx"));
        cfg.decorNames = names.ToArray();
        cfg.decorModels = models.ToArray();
        cfg.skyCircuit = Sky("SkyCircuit", new Color(0.42f, 0.62f, 1f), 1.05f, 1.25f);
        cfg.skyForest = Sky("SkyForest", new Color(0.45f, 0.7f, 0.85f), 1.2f, 1.1f);
        cfg.cpeBanner = BannerMat();

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(CarModelPath);
        if (model == null) { Debug.LogError("Gem Drift: car model not found at " + CarModelPath); return; }
        cfg.carModel = model;
        BuildCars(cfg);

        EditorUtility.SetDirty(cfg);
        AssetDatabase.SaveAssets();
        Debug.Log($"GEMDRIFT_SETUP_OK yaw={cfg.modelYaw} scale={cfg.modelScale:F3} size={cfg.carSize} bed={cfg.bedCenter} bedSize={cfg.bedSize}");
    }

    // ---------- Garage ----------

    const int CarsVersion = 6;
    const string CarKit = "Assets/_Art/Kenney/CarKit/";

    /// <summary>The garage roster: model, length, gem storage and handling of each car. Original names (no real brands).</summary>
    static CarSpec[] Roster() => new[]
    {
        new CarSpec { name = "RAPTOR V12", classKey = "car_class_super", capacity = 5, pod = true,
                      speed = 1.14f, accel = 1.1f, handling = 0.94f, grip = 0.9f, weight = 0.9f, gloss = 0.92f, metal = 0.55f },
        new CarSpec { name = "STUTTGART GT", classKey = "car_class_gt", capacity = 7, pod = true,
                      speed = 1.08f, accel = 1.05f, handling = 1.08f, grip = 1.05f, weight = 1f, gloss = 0.9f, metal = 0.5f },
        new CarSpec { name = "FORMULA X", classKey = "car_class_formula", capacity = 4, pod = true,
                      speed = 1.1f, accel = 1.2f, handling = 1.16f, grip = 1.15f, weight = 0.75f, gloss = 0.85f, metal = 0.3f },
        new CarSpec { name = "HOT HATCH R", classKey = "car_class_hatch", capacity = 8, pod = true,
                      speed = 1.02f, accel = 1.08f, handling = 1.12f, grip = 1f, weight = 0.95f, gloss = 0.85f, metal = 0.4f },
        new CarSpec { name = "GEM HAULER", classKey = "car_class_truck", capacity = 12, pod = false,
                      speed = 1f, accel = 1f, handling = 1f, grip = 1f, weight = 1.4f, gloss = 0.6f, metal = 0.25f },
    };

    static readonly (string file, float length)[] RosterModels =
    {
        ("race-future.fbx", 4.4f), ("sedan-sports.fbx", 4.3f), ("race.fbx", 4.5f), ("hatchback-sports.fbx", 4.0f), ("delivery-flat.fbx", TargetCarLength),
    };

    /// <summary>Measures every garage car, makes the gem pod materials and links the post FX resources.</summary>
    public static void BuildCars(GemDriftConfig cfg)
    {
        Directory.CreateDirectory(GenFolder);
        cfg.podShell = Mat("PodShell", new Color(0.05f, 0.055f, 0.07f), 0.55f, 0.35f);
        cfg.podGlow = Mat("PodGlow", new Color(0.3f, 0.85f, 1f), 0.9f, 0f, new Color(0.12f, 0.5f, 0.85f));
        cfg.postResources = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.PostProcessing.PostProcessResources>(
            "Packages/com.unity.postprocessing/PostProcessing/PostProcessResources.asset");
        if (cfg.postResources == null) Debug.LogWarning("Gem Drift: PostProcessResources not found (is com.unity.postprocessing installed?)");

        BuildSpace(cfg);
        var roster = Roster();
        var list = new List<CarSpec>();
        for (int i = 0; i < roster.Length; i++)
        {
            var spec = roster[i];
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(CarKit + RosterModels[i].file);
            if (model == null) { Debug.LogWarning("Gem Drift: missing car model " + RosterModels[i].file); continue; }
            spec.model = model;
            MeasureCar(model, spec, RosterModels[i].length);
            list.Add(spec);
            if (!spec.pod) // the truck also fills the old single-car fields
            {
                cfg.carModel = model; cfg.modelYaw = spec.yaw; cfg.modelScale = spec.scale; cfg.modelOffset = spec.offset;
                cfg.carSize = spec.size; cfg.bedCenter = spec.bedCenter; cfg.bedSize = spec.bedSize;
            }
            Debug.Log($"GEMDRIFT_CAR {spec.name} yaw={spec.yaw} scale={spec.scale:F3} size={spec.size} bed={spec.bedCenter} bedSize={spec.bedSize}");
        }
        cfg.cars = list.ToArray();
        cfg.carsVersion = CarsVersion;
        EditorUtility.SetDirty(cfg);
    }

    // Static batching of trees at runtime needs Read/Write enabled on the mesh.
    static GameObject ReadableModel(string path)
    {
        if (AssetImporter.GetAtPath(path) is ModelImporter mi && !mi.isReadable)
        {
            mi.isReadable = true;
            mi.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    // Forest scene: light + green fog saved in the scene (so WebGL builds keep the fog shaders)
    static void EnsureForestScene()
    {
        if (!File.Exists(ForestScene))
        {
            var active = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path;
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects, UnityEditor.SceneManagement.NewSceneMode.Single);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.72f, 0.88f, 0.8f);
            RenderSettings.fogStartDistance = 35f;
            RenderSettings.fogEndDistance = 190f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.7f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.5f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.24f, 0.18f);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, ForestScene);
            if (!string.IsNullOrEmpty(active)) UnityEditor.SceneManagement.EditorSceneManager.OpenScene(active);
        }
        EnsureMenuScene();
    }

    static bool MenuIsFirstScene() =>
        File.Exists(MenuScene) && EditorBuildSettings.scenes.Length > 0 && EditorBuildSettings.scenes[0].path == MenuScene;

    // Menu scene: just a camera + light, like Main. GameBootstrap builds the live course background and the menu at runtime.
    // Always the first scene in the build; the race scenes follow (Forest opens first).
    static void EnsureMenuScene()
    {
        if (!File.Exists(MenuScene))
        {
            // additive, so whatever scene the user has open is left alone
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects, UnityEditor.SceneManagement.NewSceneMode.Additive);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, MenuScene);
            UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
        }
        if (!File.Exists(SpaceScene))
        {
            // space: camera + light only; GameBootstrap builds the Nebula Ring and sets the starfield and lighting
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects, UnityEditor.SceneManagement.NewSceneMode.Additive);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, SpaceScene);
            UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
        }
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MenuScene, true),
            new EditorBuildSettingsScene(SpaceScene, true),
            new EditorBuildSettingsScene(ForestScene, true),
            new EditorBuildSettingsScene("Assets/Scenes/Main.unity", true),
        };
    }

    // ---------- Space course materials ----------

    static void BuildSpace(GemDriftConfig cfg)
    {
        cfg.spaceRoad = Glowing(TexMat("SpaceRoad", Color.white, 0.62f, SpaceRoadTex, 0.6f), Color.white * 0.9f);
        cfg.spaceWallA = Glowing(TexMat("SpaceWallA", new Color(0.22f, 0.25f, 0.34f), 0.8f, PanelTex, 1.2f, "Panel"), new Color(0f, 0.28f, 0.45f));
        cfg.spaceWallB = Glowing(TexMat("SpaceWallB", new Color(0.3f, 0.22f, 0.34f), 0.8f, PanelTex, 1.2f, "Panel"), new Color(0.42f, 0.04f, 0.36f));
        cfg.boostPad = Glowing(TexMat("BoostPad", Color.white, 0.9f, BoostTex, 0.3f), Color.white * 1.4f);
        cfg.jumpPad = Glowing(TexMat("JumpPad", Color.white, 0.75f, JumpTex, 0.6f), Color.white * 1.1f);
        cfg.ringCyan = Mat("RingCyan", new Color(0.2f, 0.85f, 1f), 0.9f, 0f, new Color(0.25f, 1.6f, 2.6f));
        cfg.ringMagenta = Mat("RingMagenta", new Color(1f, 0.3f, 0.9f), 0.9f, 0f, new Color(2.2f, 0.35f, 1.9f));
        cfg.pylon = Mat("Pylon", new Color(0.13f, 0.14f, 0.19f), 0.7f, 0.6f);
        cfg.asteroid = TexMat("Asteroid", new Color(0.62f, 0.58f, 0.56f), 0.12f, RockTex, 1.6f);
        cfg.asteroid.enableInstancing = true;
        cfg.moon = TexMat("Moon", new Color(0.8f, 0.82f, 0.9f), 0.05f, RockTex, 1.2f, "Asteroid");
        cfg.planet = Mat("Planet", Color.white, 0.2f);
        cfg.planet.mainTexture = SaveTex(PlanetTexture(), "Planet_Albedo");
        cfg.planetRing = RingMat();
        cfg.skySpace = SpaceSky();
        foreach (var m in new[] { cfg.planet, cfg.asteroid, cfg.moon }) EditorUtility.SetDirty(m);
    }

    static Material Glowing(Material m, Color emission)
    {
        m.EnableKeyword("_EMISSION");
        m.SetTexture("_EmissionMap", m.mainTexture);
        m.SetColor("_EmissionColor", emission);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(m);
        return m;
    }

    // dark metal deck: cyan edge lines, faint purple seams across, magenta centre dashes (they glow through the emission map)
    static (Color, float) SpaceRoadTex(float u, float v, float n, float g)
    {
        float edge = Mathf.Min(u, 1f - u);
        if (edge < 0.025f) return (new Color(0.25f, 0.9f, 1f), 0.6f);
        if (edge < 0.045f) return (new Color(0.02f, 0.03f, 0.05f), 0.2f);
        float seam = Mathf.Abs(Mathf.Repeat(v * 4f, 1f) - 0.5f);
        float shade = 0.07f + 0.035f * n + (g - 0.5f) * 0.02f;
        var c = new Color(shade, shade * 1.05f, shade * 1.35f);
        float h = 0.5f + n * 0.1f;
        if (seam > 0.485f) { c = new Color(0.22f, 0.12f, 0.45f); h = 0.3f; }
        if (Mathf.Abs(u - 0.5f) < 0.008f && Mathf.Repeat(v * 2f, 1f) < 0.5f) c = new Color(0.95f, 0.25f, 0.85f);
        float lane = Mathf.Min(Mathf.Abs(u - 0.25f), Mathf.Abs(u - 0.75f));
        if (lane < 0.003f && Mathf.Repeat(v * 4f, 1f) < 0.3f) c = new Color(0.3f, 0.35f, 0.5f);
        return (c, h);
    }

    // three chevrons pointing along the road (+v), cyan to white
    static (Color, float) BoostTex(float u, float v, float n, float g)
    {
        float x = Mathf.Abs(u - 0.5f) * 2f;
        float cv = Mathf.Repeat(v * 3f - x * 0.9f, 1f);
        bool chevron = cv > 0.45f && cv < 0.8f && x < 0.85f;
        float border = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
        if (border < 0.03f) return (new Color(0.2f, 0.7f, 1f), 0.8f);
        if (chevron) return (Color.Lerp(new Color(0.1f, 0.65f, 1f), new Color(0.85f, 1f, 1f), cv - 0.45f), 0.9f);
        return (new Color(0.02f, 0.06f, 0.12f), 0.3f);
    }

    // yellow / black warning stripes with orange chevrons
    static (Color, float) JumpTex(float u, float v, float n, float g)
    {
        float edge = Mathf.Min(u, 1f - u);
        if (edge < 0.08f) return (Mathf.Repeat((u + v) * 8f, 1f) < 0.5f ? new Color(1f, 0.8f, 0.05f) : new Color(0.03f, 0.03f, 0.03f), 0.7f);
        float x = Mathf.Abs(u - 0.5f) * 2f;
        float cv = Mathf.Repeat(v * 2.5f - x * 0.8f, 1f);
        if (cv > 0.5f && cv < 0.8f) return (new Color(1f, 0.45f, 0.08f), 0.9f);
        return (new Color(0.08f, 0.07f, 0.06f), 0.4f);
    }

    static (Color, float) RockTex(float u, float v, float n, float g)
    {
        float crater = ValueNoise(u, v, 12, 33);
        float shade = 0.45f + 0.4f * n - (crater > 0.72f ? (crater - 0.72f) * 1.5f : 0f);
        return (new Color(shade, shade * 0.97f, shade * 0.94f), n * 0.8f - (crater > 0.72f ? 0.4f : 0f) + g * 0.2f);
    }

    // gas giant: latitude bands, warped by noise (equirectangular, for the sphere's UVs)
    static Texture2D PlanetTexture()
    {
        const int W = 512, H = 256;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Repeat, anisoLevel = 2 };
        var cols = new Color[W * H];
        Color[] bands = { new Color(0.95f, 0.78f, 0.55f), new Color(0.75f, 0.45f, 0.3f), new Color(0.98f, 0.9f, 0.75f), new Color(0.55f, 0.32f, 0.35f), new Color(0.85f, 0.6f, 0.4f) };
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = (float)x / W, v = (float)y / H;
                float warp = Fbm(u, v, 6, 51) - 0.5f;
                float b = (v + warp * 0.08f) * 14f;
                int i0 = Mathf.FloorToInt(b);
                var c = Color.Lerp(bands[((i0 % 5) + 5) % 5], bands[(((i0 + 1) % 5) + 5) % 5], Mathf.SmoothStep(0f, 1f, b - i0));
                cols[y * W + x] = c * (0.9f + 0.15f * Fbm(u, v, 16, 52));
            }
        tex.SetPixels(cols);
        tex.Apply(true);
        return tex;
    }

    // planet ring: transparent banded annulus (u = radius)
    static Material RingMat()
    {
        const int W = 256;
        var tex = new Texture2D(W, 4, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
        var cols = new Color[W * 4];
        for (int x = 0; x < W; x++)
        {
            float u = (float)x / W;
            float a = Mathf.Clamp01(0.25f + 0.6f * ValueNoise(u, 0.5f, 40, 61)) * Mathf.SmoothStep(0f, 0.08f, u) * Mathf.SmoothStep(1f, 0.9f, u);
            if (u > 0.55f && u < 0.6f) a *= 0.15f; // a gap in the ring
            var c = Color.Lerp(new Color(0.9f, 0.8f, 0.7f), new Color(0.65f, 0.55f, 0.6f), ValueNoise(u, 0.2f, 20, 62));
            for (int y = 0; y < 4; y++) cols[y * W + x] = new Color(c.r, c.g, c.b, a);
        }
        tex.SetPixels(cols);
        tex.Apply(true);
        var m = Mat("PlanetRing", Color.white, 0.1f);
        m.mainTexture = SaveTex(tex, "PlanetRing_Albedo");
        m.SetFloat("_Mode", 2f); // Fade
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_ALPHATEST_ON");
        m.EnableKeyword("_ALPHABLEND_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = 3000;
        EditorUtility.SetDirty(m);
        return m;
    }

    // starfield + nebula, equirectangular, seamless (noise is sampled on the view direction, not on the image)
    static Material SpaceSky()
    {
        const int W = 2048, H = 1024;
        string pngPath = GenFolder + "/SpaceSky.png";
        var cols = new Color[W * H];
        for (int y = 0; y < H; y++)
        {
            float lat = ((y + 0.5f) / H - 0.5f) * Mathf.PI;
            for (int x = 0; x < W; x++)
            {
                float lon = ((x + 0.5f) / W) * Mathf.PI * 2f;
                var d = new Vector3(Mathf.Cos(lat) * Mathf.Sin(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Cos(lon));
                float n1 = Fbm3(d * 2.2f, 71), n2 = Fbm3(d * 3.5f + Vector3.one * 7f, 72), n3 = Fbm3(d * 6f, 73);
                float cloud = Mathf.SmoothStep(0.45f, 0.8f, n1);
                var c = new Color(0.006f, 0.008f, 0.022f);
                c += new Color(0.32f, 0.08f, 0.45f) * cloud * 0.55f;                                   // purple nebula
                c += new Color(0.05f, 0.25f, 0.5f) * Mathf.SmoothStep(0.5f, 0.85f, n2) * 0.5f;         // blue nebula
                c += new Color(0.9f, 0.35f, 0.4f) * Mathf.SmoothStep(0.7f, 0.95f, n1 * n3 * 1.6f) * 0.35f; // hot pink cores
                cols[y * W + x] = c;
            }
        }
        // stars: many faint pixels, a few bright ones with a soft halo
        var rng = new System.Random(4242);
        for (int i = 0; i < 14000; i++)
        {
            float z = (float)rng.NextDouble() * 2f - 1f, lon = (float)rng.NextDouble() * Mathf.PI * 2f;
            int x = Mathf.Clamp((int)(lon / (Mathf.PI * 2f) * W), 0, W - 1);
            int y = Mathf.Clamp((int)((Mathf.Asin(z) / Mathf.PI + 0.5f) * H), 0, H - 1);
            float b = Mathf.Pow((float)rng.NextDouble(), 4f) * 0.7f + 0.08f; // faint: the bright stars are particles (SpaceTrack)
            var tint = Color.Lerp(new Color(0.75f, 0.85f, 1f), new Color(1f, 0.9f, 0.75f), (float)rng.NextDouble());
            int r = b > 2.2f ? 1 : 0;
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    float fall = r == 0 ? 1f : Mathf.Exp(-(dx * dx + dy * dy) / (0.35f * r * r + 0.3f));
                    int px = (x + dx + W) % W, py = Mathf.Clamp(y + dy, 0, H - 1);
                    cols[py * W + px] += tint * b * fall;
                }
        }
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        for (int i = 0; i < cols.Length; i++) cols[i] = new Color(Mathf.Clamp01(cols[i].r), Mathf.Clamp01(cols[i].g), Mathf.Clamp01(cols[i].b));
        tex.SetPixels(cols);
        tex.Apply(false);
        File.WriteAllBytes(pngPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(pngPath);
        if (AssetImporter.GetAtPath(pngPath) is TextureImporter ti)
        {
            ti.mipmapEnabled = false;
            ti.wrapModeU = TextureWrapMode.Repeat;
            ti.wrapModeV = TextureWrapMode.Clamp;
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
        }
        string path = GenFolder + "/SkySpace.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Skybox/Panoramic"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath));
        m.SetFloat("_Mapping", 1f);   // latitude-longitude layout
        m.SetFloat("_ImageType", 0f); // 360 degrees
        m.SetFloat("_Exposure", 1.1f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static float Noise3(Vector3 p, int seed)
    {
        int ix = Mathf.FloorToInt(p.x), iy = Mathf.FloorToInt(p.y), iz = Mathf.FloorToInt(p.z);
        float fx = p.x - ix, fy = p.y - iy, fz = p.z - iz;
        fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy); fz = fz * fz * (3f - 2f * fz);
        float H(int a, int b, int c) => Hash(a + c * 7919, b - c * 104729, seed) / (float)int.MaxValue;
        float x00 = Mathf.Lerp(H(ix, iy, iz), H(ix + 1, iy, iz), fx), x10 = Mathf.Lerp(H(ix, iy + 1, iz), H(ix + 1, iy + 1, iz), fx);
        float x01 = Mathf.Lerp(H(ix, iy, iz + 1), H(ix + 1, iy, iz + 1), fx), x11 = Mathf.Lerp(H(ix, iy + 1, iz + 1), H(ix + 1, iy + 1, iz + 1), fx);
        return Mathf.Lerp(Mathf.Lerp(x00, x10, fy), Mathf.Lerp(x01, x11, fy), fz);
    }

    static float Fbm3(Vector3 p, int seed)
    {
        float sum = 0f, amp = 0.5f, norm = 0f;
        for (int o = 0; o < 5; o++, p *= 2.03f, amp *= 0.5f) { sum += Noise3(p, seed + o) * amp; norm += amp; }
        return sum / norm;
    }

    /// <summary>Finds which way the model faces, scales it to targetLength, puts its wheels on y = 0 and finds the gem floor:
    /// the truck's flat bed, or for a pod car the rear deck (biggest flat upward area in the back third).</summary>
    static void MeasureCar(GameObject model, CarSpec cfg, float targetLength)
    {
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
        inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var tris = new List<int>();
        foreach (var mf in inst.GetComponentsInChildren<MeshFilter>())
        {
            var m = mf.sharedMesh;
            if (m == null) continue;
            int b0 = verts.Count;
            var mv = m.vertices;
            var mn = m.normals;
            for (int i = 0; i < mv.Length; i++)
            {
                verts.Add(mf.transform.TransformPoint(mv[i]));
                normals.Add(mn.Length == mv.Length ? mf.transform.TransformDirection(mn[i]).normalized : Vector3.up);
            }
            foreach (var t in m.triangles) tris.Add(b0 + t);
        }
        Object.DestroyImmediate(inst);

        var raw = BoundsOf(verts);
        bool longZ = raw.size.z >= raw.size.x;
        float rawLen = longZ ? raw.size.z : raw.size.x;
        float axisCenter = longZ ? raw.center.z : raw.center.x;

        // The cab is taller than the flat bed: the taller end is the front.
        float hiPos = float.MinValue, hiNeg = float.MinValue;
        foreach (var v in verts)
        {
            float rel = ((longZ ? v.z : v.x) - axisCenter) / rawLen;
            if (rel > 0.2f) hiPos = Mathf.Max(hiPos, v.y);
            if (rel < -0.2f) hiNeg = Mathf.Max(hiNeg, v.y);
        }
        // the Kenney Car Kit faces +Z; only the truck can tell front from back by its taller cab
        bool frontPositive = cfg.pod || hiPos >= hiNeg;
        cfg.yaw = longZ ? (frontPositive ? 0f : 180f) : (frontPositive ? -90f : 90f);
        cfg.scale = targetLength / rawLen;

        var rot = Quaternion.Euler(0, cfg.yaw, 0);
        for (int i = 0; i < verts.Count; i++)
        {
            verts[i] = rot * (verts[i] * cfg.scale);
            normals[i] = rot * normals[i];
        }
        var b = BoundsOf(verts);
        cfg.offset = new Vector3(-b.center.x, -b.min.y, -b.center.z);
        for (int i = 0; i < verts.Count; i++) verts[i] += cfg.offset;
        cfg.size = b.size;

        // Bed floor = the biggest upward-facing flat area in the rear part of the truck.
        float L = b.size.z, W = b.size.x, H = b.size.y;
        float zMin = -L * 0.5f + L * 0.05f, zMax = -L * 0.5f + L * (cfg.pod ? 0.36f : 0.5f);
        var bins = new Dictionary<int, float>();
        float binSize = H * 0.02f;
        for (int t = 0; t < tris.Count; t += 3)
        {
            Vector3 a = verts[tris[t]], c1 = verts[tris[t + 1]], c2 = verts[tris[t + 2]];
            var centroid = (a + c1 + c2) / 3f;
            if (centroid.z < zMin || centroid.z > zMax || Mathf.Abs(centroid.x) > W * 0.4f || centroid.y < H * 0.15f) continue;
            var n = (normals[tris[t]] + normals[tris[t + 1]] + normals[tris[t + 2]]).normalized;
            if (n.y < 0.9f) continue;
            float area = Vector3.Cross(c1 - a, c2 - a).magnitude * 0.5f;
            int bin = Mathf.FloorToInt(centroid.y / binSize);
            bins[bin] = (bins.TryGetValue(bin, out var cur) ? cur : 0f) + area;
        }
        float bedY = H * 0.45f;
        float bestArea = 0f;
        foreach (var kv in bins)
            if (kv.Value > bestArea) { bestArea = kv.Value; bedY = (kv.Key + 0.5f) * binSize; }

        if (cfg.pod)
        {
            // pod sits on top of the rear body (85th percentile height, so a thin spoiler tip doesn't lift it)
            var ys = new List<float>();
            foreach (var v in verts)
                if (v.z >= zMin && v.z <= zMax && Mathf.Abs(v.x) < W * 0.35f) ys.Add(v.y);
            ys.Sort();
            if (ys.Count > 0) bedY = ys[Mathf.Clamp((int)(ys.Count * 0.85f), 0, ys.Count - 1)] + 0.01f;
        }
        cfg.bedCenter = new Vector3(0f, bedY, (zMin + zMax) * 0.5f);
        cfg.bedSize = cfg.pod ? new Vector2(W * 0.5f, (zMax - zMin) * 0.75f) : new Vector2(W * 0.7f, (zMax - zMin) * 0.8f);
    }

    static Bounds BoundsOf(List<Vector3> pts)
    {
        var b = new Bounds(pts[0], Vector3.zero);
        foreach (var p in pts) b.Encapsulate(p);
        return b;
    }

    static Material Mat(string name, Color color, float smoothness, float metallic = 0f, Color? emission = null)
    {
        string path = GenFolder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = color;
        m.SetFloat("_Glossiness", smoothness);
        m.SetFloat("_Metallic", metallic);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material SpriteMat(string name, Color color)
    {
        string path = GenFolder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = color;
        EditorUtility.SetDirty(m);
        return m;
    }

    // soft round dot, additive: overlapping particles add up into a bright flame core
    static Material FlameMat()
    {
        string texPath = GenFolder + "/FlameDot.asset";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (tex == null)
        {
            const int S = 64;
            tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(S / 2f, S / 2f)) / (S / 2f);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            AssetDatabase.CreateAsset(tex, texPath);
        }
        string path = GenFolder + "/Flame.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var shader = Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Sprites/Default");
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        m.mainTexture = tex;
        if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---------- Procedural textures (tileable value noise; albedo + normal map from a height field) ----------

    const int TexSize = 256;

    delegate (Color albedo, float height) TexFn(float u, float v, float n, float grain);

    // A white tint keeps the texture's own colours; walls and wood use greyscale textures tinted by the material.
    static readonly Dictionary<string, (Texture2D, Texture2D)> madeTextures = new Dictionary<string, (Texture2D, Texture2D)>();

    static Material TexMat(string name, Color tint, float smoothness, TexFn fn, float bump, string texName = null)
    {
        var m = Mat(name, tint, smoothness);
        texName = texName ?? name;
        if (!madeTextures.TryGetValue(texName, out var pair)) madeTextures[texName] = pair = MakeTextures(texName, fn);
        var (albedo, normal) = pair;
        m.mainTexture = albedo;
        m.SetTexture("_BumpMap", normal);
        m.SetFloat("_BumpScale", bump);
        m.EnableKeyword("_NORMALMAP");
        EditorUtility.SetDirty(m);
        return m;
    }

    static (Texture2D, Texture2D) MakeTextures(string name, TexFn fn)
    {
        int S = TexSize;
        var cols = new Color[S * S];
        var h = new float[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float u = (x + 0.5f) / S, v = (y + 0.5f) / S;
                var (c, hh) = fn(u, v, Fbm(u, v, 8, 17), Hash(x, y, 91) / (float)int.MaxValue);
                cols[y * S + x] = c;
                h[y * S + x] = hh;
            }
        var albedo = new Texture2D(S, S, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
        albedo.SetPixels(cols);
        albedo.Apply(true);

        // normal = slope of the height field, packed as (x, y, z, x) so it unpacks right with or without DXT5nm
        var normal = new Texture2D(S, S, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
        var ns = new Color[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = h[y * S + (x + 1) % S] - h[y * S + (x - 1 + S) % S];
                float dy = h[((y + 1) % S) * S + x] - h[((y - 1 + S) % S) * S + x];
                var n = new Vector3(-dx * 4f, -dy * 4f, 1f).normalized * 0.5f + Vector3.one * 0.5f;
                ns[y * S + x] = new Color(n.x, n.y, n.z, n.x);
            }
        normal.SetPixels(ns);
        normal.Apply(true);
        return (SaveTex(albedo, name + "_Albedo"), SaveTex(normal, name + "_Normal"));
    }

    static Texture2D SaveTex(Texture2D tex, string name)
    {
        string path = GenFolder + "/" + name + ".asset";
        AssetDatabase.DeleteAsset(path); // regenerate on every rebuild so tweaks show up
        AssetDatabase.CreateAsset(tex, path);
        return tex;
    }

    static int Hash(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 144269504;
            h = (h ^ (h >> 13)) * 1274126177;
            return (h ^ (h >> 16)) & int.MaxValue;
        }
    }

    // value noise on a grid that wraps, so the texture tiles seamlessly
    static float ValueNoise(float u, float v, int cells, int seed)
    {
        float x = u * cells, y = v * cells;
        int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
        float fx = x - ix, fy = y - iy;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        float H(int a, int b) => Hash(((a % cells) + cells) % cells, ((b % cells) + cells) % cells, seed) / (float)int.MaxValue;
        return Mathf.Lerp(Mathf.Lerp(H(ix, iy), H(ix + 1, iy), fx), Mathf.Lerp(H(ix, iy + 1), H(ix + 1, iy + 1), fx), fy);
    }

    static float Fbm(float u, float v, int cells, int seed)
    {
        float sum = 0f, amp = 0.5f, norm = 0f;
        for (int o = 0; o < 4; o++, cells *= 2, amp *= 0.5f) { sum += ValueNoise(u, v, cells, seed + o) * amp; norm += amp; }
        return sum / norm;
    }

    // u runs across the road, v along it (one tile = one road width); red/white kerbs are baked into both edges
    static (Color, float) RoadTex(float u, float v, float n, float g)
    {
        float edge = Mathf.Min(u, 1f - u);
        if (edge < 0.055f)
        {
            bool red = Mathf.FloorToInt(v * 8f) % 2 == 0;
            float ridge = Mathf.Sin(Mathf.Clamp01(edge / 0.055f) * Mathf.PI);
            var c = red ? new Color(0.95f, 0.15f, 0.12f) : new Color(0.98f, 0.98f, 0.98f);
            return (c * (0.9f + 0.1f * n), 0.4f + ridge * 0.6f);
        }
        float shade = 0.84f + n * 0.12f + (g - 0.5f) * 0.06f;
        return (new Color(shade, shade, shade * 1.02f), n * 0.25f + g * 0.08f);
    }

    static (Color, float) GrassTex(float u, float v, float n, float g)
    {
        var c = Color.Lerp(new Color(0.3f, 0.66f, 0.2f), new Color(0.58f, 0.9f, 0.32f), n);
        if (g > 0.985f) c = new Color(1f, 0.92f, 0.35f); // tiny flowers
        return (c * (0.9f + g * 0.15f), n * 0.6f + g * 0.4f);
    }

    static (Color, float) DirtTex(float u, float v, float n, float g)
    {
        var c = Color.Lerp(new Color(0.66f, 0.47f, 0.28f), new Color(0.88f, 0.72f, 0.48f), n);
        float rut = Mathf.Exp(-Mathf.Pow((Mathf.Abs(u - 0.5f) - 0.2f) / 0.05f, 2f)); // two tyre ruts
        c *= 1f - rut * 0.18f;
        float h = n * 0.6f - rut * 0.4f;
        if (g > 0.97f) { c = Color.Lerp(c, new Color(0.9f, 0.88f, 0.82f), 0.7f); h += 0.6f; } // pebbles
        float edge = Mathf.Min(u, 1f - u);
        if (edge < 0.04f) c = Color.Lerp(new Color(0.35f, 0.6f, 0.22f), c, edge / 0.04f); // grass creeping in
        return (c, h);
    }

    static (Color, float) ForestFloorTex(float u, float v, float n, float g)
    {
        var c = Color.Lerp(new Color(0.22f, 0.5f, 0.2f), new Color(0.42f, 0.72f, 0.28f), n);
        float leaves = ValueNoise(u, v, 32, 5);
        if (leaves > 0.7f) c = Color.Lerp(c, new Color(0.85f, 0.55f, 0.2f), (leaves - 0.7f) * 2.5f);
        return (c * (0.9f + g * 0.15f), n * 0.7f + g * 0.3f);
    }

    // bevelled greyscale panel, tinted by the material colour (one panel per wall piece)
    static (Color, float) PanelTex(float u, float v, float n, float g)
    {
        float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
        float bevel = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / 0.08f));
        float stripe = Mathf.FloorToInt(v * 2f + 0.5f) % 2 == 0 ? 1f : 0.92f;
        float shade = (0.72f + 0.28f * bevel) * stripe * (0.94f + 0.06f * n);
        return (new Color(shade, shade, shade), bevel + n * 0.05f);
    }

    // three horizontal planks with stretched grain
    static (Color, float) WoodTex(float u, float v, float n, float g)
    {
        float pv = v * 3f;
        float inPlank = pv - Mathf.Floor(pv);
        float gap = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(inPlank, 1f - inPlank) / 0.08f));
        float grain = ValueNoise(u * 0.25f, v, 24, Mathf.FloorToInt(pv) + 3);
        float shade = (0.7f + 0.3f * grain) * (0.55f + 0.45f * gap);
        return (new Color(shade, shade, shade), gap * 0.8f + grain * 0.2f);
    }

    // Sign print: drop an official file at Assets/_Art/Banners/CPE_Logo_Official.png (4:1 works best) to use it
    // instead of the generated CPE_Banner.png.
    static Material BannerMat()
    {
        string official = "Assets/_Art/Banners/CPE_Logo_Official.png", generated = "Assets/_Art/Banners/CPE_Banner.png";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(official) ?? AssetDatabase.LoadAssetAtPath<Texture2D>(generated);
        if (tex == null) return null;
        string path = GenFolder + "/CpeBanner.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Unlit/Texture"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.mainTexture = tex;
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Sky(string name, Color tint, float thickness, float exposure)
    {
        string path = GenFolder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Skybox/Procedural"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_SkyTint", tint);
        m.SetFloat("_AtmosphereThickness", thickness);
        m.SetFloat("_Exposure", exposure);
        m.SetFloat("_SunSize", 0.05f);
        m.SetColor("_GroundColor", new Color(0.42f, 0.45f, 0.4f));
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material CheckerMat()
    {
        string texPath = GenFolder + "/Checker.asset";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (tex == null)
        {
            tex = new Texture2D(14, 3, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            for (int x = 0; x < 14; x++)
                for (int y = 0; y < 3; y++)
                    tex.SetPixel(x, y, (x + y) % 2 == 0 ? Color.white : Color.black);
            tex.Apply();
            AssetDatabase.CreateAsset(tex, texPath);
        }
        var m = Mat("Checker", Color.white, 0.2f);
        m.mainTexture = tex;
        return m;
    }
}

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
    const string ConfigPath = "Assets/_Scripts/Resources/GemDriftConfig.asset";
    const string GenFolder = "Assets/_Scripts/Generated";
    const float TargetCarLength = 4.2f;

    static GemDriftSetup()
    {
        EditorApplication.delayCall += () =>
        {
            if (AssetDatabase.LoadAssetAtPath<GemDriftConfig>(ConfigPath) == null) Rebuild();
            else if (!MenuIsFirstScene()) EnsureMenuScene();
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

    [MenuItem("Gem Drift/Play Menu %#m")]
    public static void PlayMenu() => Play(MenuScene);

    [MenuItem("Gem Drift/Build WebGL")]
    public static void BuildWebGL()
    {
        if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); return; }
        var opts = new BuildPlayerOptions
        {
            scenes = new[] { MenuScene, ForestScene, "Assets/Scenes/Main.unity" }, // the menu is build index 0
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
        MeasureCar(model, cfg);

        EditorUtility.SetDirty(cfg);
        AssetDatabase.SaveAssets();
        Debug.Log($"GEMDRIFT_SETUP_OK yaw={cfg.modelYaw} scale={cfg.modelScale:F3} size={cfg.carSize} bed={cfg.bedCenter} bedSize={cfg.bedSize}");
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
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MenuScene, true),
            new EditorBuildSettingsScene(ForestScene, true),
            new EditorBuildSettingsScene("Assets/Scenes/Main.unity", true),
        };
    }

    static void MeasureCar(GameObject model, GemDriftConfig cfg)
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
        bool frontPositive = hiPos >= hiNeg;
        cfg.modelYaw = longZ ? (frontPositive ? 0f : 180f) : (frontPositive ? -90f : 90f);
        cfg.modelScale = TargetCarLength / rawLen;

        var rot = Quaternion.Euler(0, cfg.modelYaw, 0);
        for (int i = 0; i < verts.Count; i++)
        {
            verts[i] = rot * (verts[i] * cfg.modelScale);
            normals[i] = rot * normals[i];
        }
        var b = BoundsOf(verts);
        cfg.modelOffset = new Vector3(-b.center.x, -b.min.y, -b.center.z);
        for (int i = 0; i < verts.Count; i++) verts[i] += cfg.modelOffset;
        cfg.carSize = b.size;

        // Bed floor = the biggest upward-facing flat area in the rear part of the truck.
        float L = b.size.z, W = b.size.x, H = b.size.y;
        float zMin = -L * 0.5f + L * 0.05f, zMax = -L * 0.5f + L * 0.5f;
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

        cfg.bedCenter = new Vector3(0f, bedY, (zMin + zMax) * 0.5f);
        cfg.bedSize = new Vector2(W * 0.7f, (zMax - zMin) * 0.8f);
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

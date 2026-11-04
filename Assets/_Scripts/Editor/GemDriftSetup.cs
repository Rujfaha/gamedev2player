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
    const string ConfigPath = "Assets/_Scripts/Resources/GemDriftConfig.asset";
    const string GenFolder = "Assets/_Scripts/Generated";
    const float TargetCarLength = 4.2f;

    static GemDriftSetup()
    {
        EditorApplication.delayCall += () =>
        {
            if (AssetDatabase.LoadAssetAtPath<GemDriftConfig>(ConfigPath) == null) Rebuild();
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

    [MenuItem("Gem Drift/Build WebGL")]
    public static void BuildWebGL()
    {
        if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); return; }
        var opts = new BuildPlayerOptions
        {
            scenes = new[] { ForestScene, "Assets/Scenes/Main.unity" }, // Forest opens first
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

        cfg.road = Mat("Road", new Color(0.93f, 0.93f, 0.9f), 0.15f);
        cfg.roadLine = Mat("RoadLine", new Color(0.05f, 0.05f, 0.06f), 0.1f);
        cfg.ground = Mat("Ground", new Color(0.36f, 0.55f, 0.36f), 0.05f);
        cfg.wallA = Mat("WallRed", new Color(0.85f, 0.18f, 0.16f), 0.3f);
        cfg.wallB = Mat("WallWhite", new Color(0.95f, 0.95f, 0.95f), 0.3f);
        cfg.gemRed = Mat("GemRed", new Color(1f, 0.15f, 0.2f), 0.9f, 0.2f, new Color(0.6f, 0.05f, 0.08f));
        cfg.gemBlue = Mat("GemBlue", new Color(0.15f, 0.45f, 1f), 0.9f, 0.2f, new Color(0.05f, 0.2f, 0.6f));
        cfg.gemGold = Mat("GemGold", new Color(1f, 0.78f, 0.15f), 0.9f, 0.5f, new Color(0.6f, 0.42f, 0.02f));
        cfg.checker = CheckerMat();
        cfg.skid = SpriteMat("Skid", new Color(0.05f, 0.05f, 0.05f, 0.55f));
        cfg.spark = SpriteMat("Spark", Color.white);

        // Forest course
        cfg.dirtRoad = Mat("DirtRoad", new Color(0.72f, 0.6f, 0.43f), 0.05f);
        cfg.forestGround = Mat("ForestGround", new Color(0.22f, 0.38f, 0.2f), 0.02f);
        cfg.woodA = Mat("WoodA", new Color(0.45f, 0.29f, 0.16f), 0.1f);
        cfg.woodB = Mat("WoodB", new Color(0.62f, 0.45f, 0.27f), 0.1f);
        cfg.treeModels = new[] { ReadableModel(KitPath + "treeLarge.fbx"), ReadableModel(KitPath + "treeSmall.fbx") };
        cfg.finishArch = AssetDatabase.LoadAssetAtPath<GameObject>(KitPath + "overheadRoundColored.fbx");

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
            RenderSettings.fogColor = new Color(0.62f, 0.74f, 0.66f);
            RenderSettings.fogStartDistance = 35f;
            RenderSettings.fogEndDistance = 190f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.7f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.5f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.24f, 0.18f);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, ForestScene);
            if (!string.IsNullOrEmpty(active)) UnityEditor.SceneManagement.EditorSceneManager.OpenScene(active);
        }
        EditorBuildSettings.scenes = new[]
        {
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

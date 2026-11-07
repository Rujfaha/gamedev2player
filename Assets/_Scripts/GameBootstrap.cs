using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds the whole race when a game scene loads ("Main" = arena circuit, "Forest" = forest switchback):
/// track (hand-built from the scene if present, otherwise the generated course), the player trucks plus AI trucks,
/// gems, cameras, race rules and HUD. How many players / AI and their colours come from MatchSettings (main menu);
/// opened directly in the editor it falls back to GemDriftConfig: two players and aiCount AI trucks.
/// In the "Menu" scene it builds a live background instead: a course with two AI trucks and an orbiting camera, plus the main menu.
/// Nothing has to be placed in the scene by hand for the game to be playable.
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    public const string MenuScene = "Menu";

    // scene name → built-in course used when the scene has no hand-built track
    static readonly string[] GameScenes = { "Space", "Main", "Forest" };

    /// <summary>Course 0 = Forest Switchback, 1 = CPE Arena Circuit, 2 = Nebula Ring (space) (keys 1 / 2 / 3).</summary>
    public static string SceneForCourse(int course) => course == 0 ? "Forest" : course == 2 ? "Space" : "Main";

    public static string NextScene(string current)
    {
        int i = System.Array.IndexOf(GameScenes, current);
        return GameScenes[(i + 1) % GameScenes.Length];
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bool ours = scene.name == MenuScene || System.Array.IndexOf(GameScenes, scene.name) >= 0;
        if (ours && FindAnyObjectByType<GameBootstrap>() == null)
            new GameObject("GemDrift").AddComponent<GameBootstrap>();
    }

    void Awake()
    {
        Time.timeScale = 1f; // a paused race may have been left through the pause menu
        Time.fixedDeltaTime = 1f / 60f;
        var cfg = GemDriftConfig.Load();
        if (cfg == null)
        {
            Debug.LogError("Gem Drift: GemDriftConfig is missing. Run the menu Gem Drift > Rebuild Config.");
            return;
        }

        var sceneCamera = Camera.main;
        if (sceneCamera != null) sceneCamera.gameObject.SetActive(false);
        var sun = FindAnyObjectByType<Light>();
        if (sun != null && sun.type == LightType.Directional)
        {
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            sun.shadows = LightShadows.Soft;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.intensity = 1.25f;
        }
        // bright, sunny look: coloured ambient light instead of the grey default
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.72f, 0.82f, 0.98f);
        RenderSettings.ambientEquatorColor = new Color(0.58f, 0.64f, 0.58f);
        RenderSettings.ambientGroundColor = new Color(0.36f, 0.34f, 0.28f);

        string sceneName = SceneManager.GetActiveScene().name;
        bool menu = sceneName == MenuScene;
        var id = menu ? TrackId.Space // the menu background is the space course
                      : sceneName == "Forest" ? TrackId.Forest : sceneName == "Space" ? TrackId.Space : TrackId.Circuit;
        if (id == TrackId.Space)
        {
            // deep space: no fog, cool starlight from one side, purple-blue ambient bounce from the nebula
            RenderSettings.fog = false;
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(38f, -60f, 0f);
                sun.color = new Color(0.86f, 0.9f, 1f);
                sun.intensity = 1.35f;
            }
            RenderSettings.ambientSkyColor = new Color(0.34f, 0.36f, 0.62f);
            RenderSettings.ambientEquatorColor = new Color(0.26f, 0.2f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.1f, 0.08f, 0.2f);
        }
        else if (id == TrackId.Forest)
        {
            RenderSettings.fogColor = new Color(0.72f, 0.88f, 0.8f);
            if (menu) // the Forest scene saves its fog, the Menu scene does not
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogStartDistance = 35f;
                RenderSettings.fogEndDistance = 190f;
            }
        }
        else
        {
            // light haze on the arena so far-away scenery fades instead of popping
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.78f, 0.87f, 0.97f);
            RenderSettings.fogStartDistance = 140f;
            RenderSettings.fogEndDistance = 520f;
        }
        var sky = id == TrackId.Space ? cfg.skySpace : id == TrackId.Forest ? cfg.skyForest : cfg.skyCircuit;
        if (sky != null) RenderSettings.skybox = sky;
        var layout = TrackBuilder.FromScene() ?? TrackBuilder.Build(cfg, transform, id);
        GraphicsFx.SetupScene(cfg, sun, menu);

        foreach (var (pos, color) in layout.gems)
            GemPickup.CreateOnTrack(cfg, color, new Vector3(pos.x, 0f, pos.z));

        var race = gameObject.AddComponent<RaceManager>();
        if (menu)
        {
            // two AI trucks lap the course behind the menu; the camera orbits the first one
            var pal = MatchSettings.Palette(cfg);
            var demo = new CarController[2];
            for (int k = 0; k < demo.Length; k++)
                demo[k] = CarController.Create(cfg, k, layout.starts[k].position, layout.starts[k].rotation, true, pal[2 + k], Random.Range(0, cfg.CarCount));
            race.InitDemo(demo, layout);
            GraphicsFx.Apply(cfg, MenuCamera.Create(demo[0].transform));
            gameObject.AddComponent<MainMenu>();
            return;
        }

        int humans = MatchSettings.Humans;
        int aiCount = MatchSettings.AiCount(cfg);
        var cars = new CarController[humans + aiCount];
        for (int i = 0; i < humans; i++)
            cars[i] = CarController.Create(cfg, i, layout.starts[i].position, layout.starts[i].rotation, false, MatchSettings.Tint(cfg, i), MatchSettings.Car(cfg, i));
        for (int k = 0; k < aiCount; k++)
        {
            var start = AIStart(layout, k, humans + k);
            cars[humans + k] = CarController.Create(cfg, humans + k, start.position, start.rotation, true, MatchSettings.Tint(cfg, humans + k), MatchSettings.Car(cfg, humans + k));
        }

        // one full-screen camera for a single player; split screen for two (P1 left half, P2 right half)
        float width = 1f / humans;
        for (int i = 0; i < humans; i++)
            GraphicsFx.Apply(cfg, FollowCamera.Create(cars[i], new Rect(i * width, 0f, width, 1f)).GetComponent<Camera>());

        race.Init(cars, layout);
        gameObject.AddComponent<RaceHUD>().Init(race, layout);
        gameObject.AddComponent<PauseMenu>();
    }

    // Hand-built tracks (no course grid): 3 lanes x 2 rows. Humans take the outer lanes of row 0 (the Start_P1 / Start_P2 poses),
    // AI trucks fill row 1 and then the middle lane. More than 6 go in extra rows.
    static readonly (int row, int lane)[] Grid = { (0, 0), (0, 2), (1, 0), (1, 2), (0, 1), (1, 1) };

    // Generated courses: the front row has 5 lanes (AI, P1, AI, P2, AI). Trucks that don't fit line up in a second row.
    static readonly int[] SecondRowLanes = { 0, -1, 1, -2, 2 };

    /// <summary>An optional Start_AI1, Start_AI2.. marker in the scene, else the course's grid slot,
    /// else a slot behind the front row (6 m per row).</summary>
    static Pose AIStart(TrackLayout layout, int k, int slot)
    {
        var marker = GameObject.Find("Start_AI" + (k + 1));
        if (marker != null) return new Pose(marker.transform.position, marker.transform.rotation);
        if (k < layout.aiStarts.Count) return layout.aiStarts[k];
        var p1 = layout.starts[0];
        Vector3 back = p1.rotation * Vector3.back * 6f;
        if (layout.aiStarts.Count > 0)
        {
            // P1 and P2 sit one lane either side of the middle, so half the gap between them is one lane step
            int n = k - layout.aiStarts.Count;
            var middle = (p1.position + layout.starts[1].position) * 0.5f;
            var laneStep = (layout.starts[1].position - p1.position) * 0.5f;
            return new Pose(middle + laneStep * SecondRowLanes[n % SecondRowLanes.Length] + back * (1 + n / SecondRowLanes.Length), p1.rotation);
        }
        var (row, lane) = Grid[slot % Grid.Length];
        row += 2 * (slot / Grid.Length);
        return new Pose(Vector3.Lerp(p1.position, layout.starts[1].position, lane * 0.5f) + back * row, p1.rotation);
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds the whole race when a game scene loads ("Main" = arena circuit, "Forest" = forest switchback):
/// track (hand-built from the scene if present, otherwise the generated course), two trucks, gems, split-screen cameras, race rules and HUD.
/// Nothing has to be placed in the scene by hand for the game to be playable.
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    // scene name → built-in course used when the scene has no hand-built track
    static readonly string[] GameScenes = { "Main", "Forest" };

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
        if (System.Array.IndexOf(GameScenes, scene.name) >= 0 && FindAnyObjectByType<GameBootstrap>() == null)
            new GameObject("GemDrift").AddComponent<GameBootstrap>();
    }

    void Awake()
    {
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
        }

        var id = SceneManager.GetActiveScene().name == "Forest" ? TrackId.Forest : TrackId.Circuit;
        var layout = TrackBuilder.FromScene() ?? TrackBuilder.Build(cfg, transform, id);

        var cars = new CarController[2];
        for (int i = 0; i < 2; i++)
            cars[i] = CarController.Create(cfg, i, layout.starts[i].position, layout.starts[i].rotation);

        foreach (var (pos, color) in layout.gems)
            GemPickup.CreateOnTrack(cfg, color, new Vector3(pos.x, 0f, pos.z));

        // split screen: P1 left half, P2 right half
        FollowCamera.Create(cars[0], new Rect(0f, 0f, 0.5f, 1f));
        FollowCamera.Create(cars[1], new Rect(0.5f, 0f, 0.5f, 1f));

        var race = gameObject.AddComponent<RaceManager>();
        race.Init(cars, layout);
        gameObject.AddComponent<RaceHUD>().Init(race);
    }
}

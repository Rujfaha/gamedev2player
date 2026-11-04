using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds the whole race when the "Main" scene loads: track (hand-built from the scene if present,
/// otherwise a generated test circuit), two player trucks plus AI trucks, gems, split-screen cameras, race rules and HUD.
/// Nothing has to be placed in the scene by hand for the game to be playable.
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    const string GameScene = "Main";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == GameScene && FindAnyObjectByType<GameBootstrap>() == null)
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

        var layout = TrackBuilder.FromScene() ?? TrackBuilder.Build(cfg, transform);

        int aiCount = Mathf.Max(0, cfg.aiCount);
        var cars = new CarController[2 + aiCount];
        for (int i = 0; i < 2; i++)
            cars[i] = CarController.Create(cfg, i, layout.starts[i].position, layout.starts[i].rotation);
        for (int k = 0; k < aiCount; k++)
        {
            var start = AIStart(layout, k);
            cars[2 + k] = CarController.Create(cfg, 2 + k, start.position, start.rotation, true);
        }

        foreach (var (pos, color) in layout.gems)
            GemPickup.CreateOnTrack(cfg, color, new Vector3(pos.x, 0f, pos.z));

        // split screen: P1 left half, P2 right half
        FollowCamera.Create(cars[0], new Rect(0f, 0f, 0.5f, 1f));
        FollowCamera.Create(cars[1], new Rect(0.5f, 0f, 0.5f, 1f));

        var race = gameObject.AddComponent<RaceManager>();
        race.Init(cars, layout.checkpoints);
        gameObject.AddComponent<RaceHUD>().Init(race);
    }

    /// <summary>An optional Start_AI1, Start_AI2.. marker in the scene, else a grid slot behind Start_P1/P2 (6 m per row).</summary>
    static Pose AIStart(TrackLayout layout, int k)
    {
        var marker = GameObject.Find("Start_AI" + (k + 1));
        if (marker != null) return new Pose(marker.transform.position, marker.transform.rotation);
        var s = layout.starts[k % 2];
        return new Pose(s.position - s.rotation * Vector3.forward * (6f * (1 + k / 2)), s.rotation);
    }
}

using UnityEngine;

/// <summary>Asset references the runtime needs (models, materials). Built by menu Gem Drift > Rebuild Config.</summary>
[CreateAssetMenu(menuName = "Gem Drift/Config")]
public class GemDriftConfig : ScriptableObject
{
    [Header("Car model (Kenney Car Kit)")]
    public GameObject carModel;
    public float modelYaw;          // rotates the model so its nose points +Z
    public float modelScale = 1f;   // scales the model to ~4.2 m long
    public Vector3 modelOffset;     // puts wheels on y = 0, centred on x/z
    public Vector3 carSize;         // size after yaw/scale (x = width, y = height, z = length)
    public Vector3 bedCenter;       // truck-bed floor centre in car space
    public Vector2 bedSize;         // bed floor width (x) and length (y)
    [Header("Garage: every car a racer can pick (built by Rebuild Config / EnsureCars)")]
    public CarSpec[] cars;
    public int carsVersion;         // bumped by the editor setup when the roster changes
    public UnityEngine.Rendering.PostProcessing.PostProcessResources postResources; // needed to add post FX at runtime

    public Color[] playerTints = { new Color(1f, 0.55f, 0.5f), new Color(0.55f, 0.75f, 1f) };

    [Header("AI racers")]
    public int aiCount = 3;         // computer-driven trucks racing alongside the two players
    public Color[] aiTints =
    {
        new Color(1f, 0.85f, 0.4f), new Color(0.6f, 0.95f, 0.6f), new Color(0.8f, 0.6f, 1f), new Color(0.75f, 0.75f, 0.75f),
    };

    [Header("Crash attacks")]
    public float crashMinSpeed = 8f;    // closing speed (m/s) a ram needs before the rammed truck loses gems
    public int crashDropMin = 1;        // gems the rammed truck drops: random from min to max (inclusive)
    public int crashDropMax = 3;
    public float crashCooldown = 1.5f;  // seconds before the same truck can lose gems to a crash again

    [Header("Materials")]
    public Material road, roadLine, ground, wallA, wallB, checker;
    public Material gemRed, gemBlue, gemGold;
    public Material skid, spark;
    public Material flame;                     // additive soft dot for exhaust fire
    public Material nitroTankShell, nitroGlow; // little nitro tank on the bed
    public Material podShell, podGlow;         // gem pod on the sports cars

    [Header("Forest course")]
    public Material dirtRoad, forestGround, woodA, woodB;
    public GameObject[] treeModels;   // [0] large, [1] small (Kenney Racing Kit)
    public GameObject finishArch;     // Kenney overhead arch over the finish line

    [Header("Scenery (TrackDecor)")]
    public string[] decorNames;       // looked up by name, e.g. "grandStand", "cone"
    public GameObject[] decorModels;
    public Material skyCircuit, skyForest;
    public Material cpeBanner;        // CPE · KMUTT sign print (Assets/_Art/Banners)

    public int CarCount => cars != null && cars.Length > 0 ? cars.Length : 1;

    /// <summary>Car i of the garage. Without a garage (old config) the single truck fields above are used.</summary>
    public CarSpec Car(int i)
    {
        if (cars != null && cars.Length > 0) return cars[((i % cars.Length) + cars.Length) % cars.Length];
        return new CarSpec
        {
            name = "GEM HAULER", model = carModel, yaw = modelYaw, scale = modelScale, offset = modelOffset, size = carSize,
            bedCenter = bedCenter, bedSize = bedSize, capacity = 12, pod = false,
        };
    }

    public Material GemMaterial(GemColor c) => c == GemColor.Gold ? gemGold : c == GemColor.Blue ? gemBlue : gemRed;

    public static GemDriftConfig Load() => Resources.Load<GemDriftConfig>("GemDriftConfig");
}

/// <summary>Gem colour; the int value is the score it is worth.</summary>
public enum GemColor { Red = 1, Blue = 2, Gold = 3 }

/// <summary>Layer numbers set up in ProjectSettings/TagManager.</summary>
public static class Layers
{
    public const int Road = 6, Wall = 7, Car = 8, Gem = 9;
}

/// <summary>One car of the garage: model placement (measured in the editor), where its gems ride, and its handling.</summary>
[System.Serializable]
public class CarSpec
{
    public string name;               // shown in the garage, e.g. "RAPTOR V12"
    public string classKey;           // Loc key of its class line, e.g. "car_class_super"
    public GameObject model;
    public float yaw, scale = 1f;
    public Vector3 offset, size;
    public Vector3 bedCenter;         // floor of the gem storage in car space
    public Vector2 bedSize;           // storage floor width (x) and length (y)
    public bool pod;                  // true: no truck bed, so a gem pod (tray + rails) is built on the rear deck
    public int capacity = 12;         // gems it can carry

    [Header("Handling (1 = the original truck)")]
    public float speed = 1f;          // top speed
    public float accel = 1f;
    public float handling = 1f;       // turn rate
    public float grip = 1f;           // sideways grip (lower = slides more)
    public float weight = 1f;         // ramming mass: heavier knocks lighter cars around

    [Header("Paint")]
    public float gloss = 0.8f, metal = 0.35f;
}

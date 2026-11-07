using UnityEngine;

/// <summary>
/// What the main menu picked for the next race. GameBootstrap reads it when a race scene loads.
/// FromMenu stays false when a race scene is opened directly (editor shortcuts), and then
/// GemDriftConfig (2 players, aiCount AI trucks, default tints) is used instead.
/// </summary>
public static class MatchSettings
{
    public const int MaxRacers = 6;       // the start grid: 5 in the front row + 1 behind on generated courses, 3 x 2 on hand-built ones

    public static bool FromMenu;
    public static int playerCount = 2;    // human players: 1 or 2
    public static int totalRacers = 4;    // players + AI trucks
    public static int[] colors = { 0, 1 }; // palette index of P1's and P2's truck
    public static int[] cars = { 0, 1 };   // garage car of P1 and P2 (GemDriftConfig.cars)
    public static int course;             // 0 = Forest, 1 = Circuit, 2 = Space (see GameBootstrap.SceneForCourse)

    // Statics survive scene loads; make sure a fresh Play session starts from the config defaults.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => FromMenu = false;

    public static readonly string[] ColorKeys = { "color_red", "color_blue", "color_yellow", "color_green", "color_purple", "color_orange" };

    /// <summary>The six truck colours a player can pick: the two player tints, then the AI tints.</summary>
    public static Color[] Palette(GemDriftConfig cfg)
    {
        var pal = new Color[MaxRacers];
        for (int i = 0; i < pal.Length; i++)
            pal[i] = i < 2 ? cfg.playerTints[i] : cfg.aiTints[(i - 2) % cfg.aiTints.Length];
        return pal;
    }

    public static int Humans => FromMenu ? playerCount : 2;
    public static int AiCount(GemDriftConfig cfg) => FromMenu ? Mathf.Max(0, totalRacers - playerCount) : Mathf.Max(0, cfg.aiCount);

    /// <summary>Garage car of racer number i: humans keep their pick, AI racers get a spread of the other cars.</summary>
    public static int Car(GemDriftConfig cfg, int racer)
    {
        int n = cfg.CarCount;
        if (racer < Humans) return FromMenu ? cars[racer] % n : racer % n;
        return (racer * 3 + 1) % n;
    }

    /// <summary>Body colour of racer number i (humans first, then AI). AI trucks get the colours no human picked.</summary>
    public static Color Tint(GemDriftConfig cfg, int racer)
    {
        if (!FromMenu) return racer < 2 ? cfg.playerTints[racer] : cfg.aiTints[(racer - 2) % cfg.aiTints.Length];
        var pal = Palette(cfg);
        if (racer < playerCount) return pal[colors[racer]];
        int skip = racer - playerCount;
        for (int i = 0; i < pal.Length; i++)
        {
            if (i == colors[0] || (playerCount > 1 && i == colors[1])) continue;
            if (skip-- == 0) return pal[i];
        }
        return pal[0];
    }
}

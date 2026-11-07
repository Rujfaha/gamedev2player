using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Dev helper: lets a script outside Unity drive the editor for quick checks. Write one command per line to
/// Temp/gd_remote.txt: "refresh", "play Assets/Scenes/Menu.unity", "stop", "shot Temp/shot.png", "menu Gem Drift/Build WebGL".
/// Results are appended to Temp/gd_remote.log. Editor only; never in a build.
/// </summary>
[InitializeOnLoad]
static class DevRemote
{
    const string CmdFile = "Temp/gd_remote.txt", LogFile = "Temp/gd_remote.log";
    static double next;

    static DevRemote() => EditorApplication.update += Poll;

    static void Log(string s) => File.AppendAllText(LogFile, System.DateTime.Now.ToString("HH:mm:ss ") + s + "\n");

    static void Poll()
    {
        if (EditorApplication.timeSinceStartup < next) return;
        next = EditorApplication.timeSinceStartup + 0.4;
        if (!File.Exists(CmdFile)) return;
        var lines = File.ReadAllLines(CmdFile);
        File.Delete(CmdFile);
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            int sp = line.IndexOf(' ');
            string cmd = sp < 0 ? line : line.Substring(0, sp), arg = sp < 0 ? "" : line.Substring(sp + 1);
            try
            {
                switch (cmd)
                {
                    case "refresh": AssetDatabase.Refresh(); break;
                    case "bg": PlayerSettings.runInBackground = true; AssetDatabase.SaveAssets(); break;
                    case "play":
                        if (!EditorApplication.isPlaying)
                        {
                            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(arg);
                            EditorApplication.EnterPlaymode();
                        }
                        break;
                    case "stop": EditorApplication.ExitPlaymode(); break;
                    case "shot": ScreenCapture.CaptureScreenshot(arg); break;
                    case "menu": EditorApplication.ExecuteMenuItem(arg); break;
                    case "view": // "view Colors 2": jump the main menu to a screen (optionally setting the player count)
                    {
                        var parts = arg.Split(' ');
                        if (parts.Length > 1) MatchSettings.playerCount = int.Parse(parts[1]);
                        var menu = Object.FindAnyObjectByType<MainMenu>();
                        var viewType = typeof(MainMenu).GetNestedType("View", System.Reflection.BindingFlags.NonPublic);
                        var show = typeof(MainMenu).GetMethod("Show", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        show.Invoke(menu, new object[] { System.Enum.Parse(viewType, parts[0]), 0 });
                        break;
                    }
                    case "call": // "call Colors.ChangeCar 0 1" style hooks are not needed; "car P DIR" changes a player's car
                    {
                        var parts = arg.Split(' ');
                        var menu = Object.FindAnyObjectByType<MainMenu>();
                        typeof(MainMenu).GetMethod(parts[0], System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                            .Invoke(menu, new object[] { int.Parse(parts[1]), int.Parse(parts[2]) });
                        break;
                    }
                    case "clearrecords":
                        foreach (var name in new[] { "NEBULA RING", "FOREST SWITCHBACK", "CPE ARENA CIRCUIT" })
                        { PlayerPrefs.DeleteKey("gd_best_" + name); PlayerPrefs.DeleteKey("gd_best_" + name + "_who"); }
                        PlayerPrefs.Save();
                        break;
                    case "endrace": // test results: random gems, the first few racers finish, the rest DNF
                    {
                        var rm = RaceManager.Instance;
                        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                        var finish = typeof(RaceManager).GetMethod("Finish", flags);
                        var end = typeof(RaceManager).GetMethod("EndRace", flags);
                        var colors = new[] { GemColor.Red, GemColor.Blue, GemColor.Gold };
                        for (int i = 0; i < rm.Racers.Length; i++)
                        {
                            int g = Random.Range(1, rm.Racers[i].car.Bed.Capacity + 1);
                            for (int q = 0; q < g; q++) rm.Racers[i].car.Bed.TryAdd(colors[Random.Range(0, 3)]);
                        }
                        for (int i = 0; i < rm.Racers.Length - 1; i++) finish.Invoke(rm, new object[] { rm.Racers[i] });
                        if (rm.State != RaceManager.Phase.Results) end.Invoke(rm, null);
                        break;
                    }
                }
                Log("OK " + line);
            }
            catch (System.Exception e) { Log("ERR " + line + " " + e.Message); }
        }
    }
}

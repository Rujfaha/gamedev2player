using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>In-race pause: Esc (or gamepad Start) stops the game and shows Resume / Restart / Main Menu.</summary>
public class PauseMenu : MonoBehaviour
{
    GameObject panel;
    Button first;

    bool Paused => panel != null && panel.activeSelf;

    void Update()
    {
        var kb = Keyboard.current;
        var gp = Gamepad.current;
        bool toggle = (kb != null && kb.escapeKey.wasPressedThisFrame) || (gp != null && gp.startButton.wasPressedThisFrame);
        if (Paused && gp != null && gp.buttonEast.wasPressedThisFrame) toggle = true;
        if (!toggle) return;

        var race = RaceManager.Instance;
        if (Paused) SetPaused(false);
        else if (race != null && race.State != RaceManager.Phase.Results) SetPaused(true); // Esc on the results screen goes to the menu
    }

    void SetPaused(bool pause)
    {
        if (pause && panel == null) Build();
        panel.SetActive(pause);
        Time.timeScale = pause ? 0f : 1f;
        AudioListener.pause = pause;
        if (pause) EventSystem.current.SetSelectedGameObject(first.gameObject);
    }

    void Build()
    {
        MenuUI.EnsureEventSystem();
        var canvas = MenuUI.CreateCanvas(transform, "PauseMenu", 100);
        panel = canvas.gameObject;
        MenuUI.Fill(MenuUI.Stretch(canvas, "Dim", Vector2.zero, Vector2.one), new Color(MenuUI.Ink.r, MenuUI.Ink.g, MenuUI.Ink.b, 0.7f));

        var card = MenuUI.Card(canvas, 640f, TextAnchor.UpperCenter, 60, 48);
        MenuUI.Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, card.sizeDelta);
        MenuUI.Label(card, Loc.T("pause_title"), 72, TextAnchor.MiddleCenter, MenuUI.Accent).Italic();
        MenuUI.Space(card, 14);
        first = MenuUI.MakeButton(card, Loc.T("resume"), 520, 80, () => SetPaused(false), 38, true, true);
        MenuUI.MakeButton(card, Loc.T("restart"), 520, 80, () => Leave(SceneManager.GetActiveScene().name), 38);
        MenuUI.MakeButton(card, Loc.T("mainmenu"), 520, 80, () => Leave(GameBootstrap.MenuScene), 38);
    }

    static void Leave(string scene)
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(scene);
    }

    void OnDestroy()
    {
        if (Paused) { Time.timeScale = 1f; AudioListener.pause = false; }
    }
}

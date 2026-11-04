using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Hover / keyboard-focus animation for a menu button: eases the colours of its graphics, a small scale-up and an optional
/// sideways slide. Runs on unscaled time, so it also works while a race is paused.
/// </summary>
public class MenuFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    struct Tint { public Graphic graphic; public Color idle, on; }

    readonly List<Tint> tints = new List<Tint>();
    public float scale = 1.04f;
    public UnityAction onFocus;       // keyboard / gamepad focus arrived (tick sound)
    RectTransform slideTarget;
    float slidePixels;
    Vector2 slideStart;
    Selectable selectable;
    bool hover, focus;
    float t;

    void Awake() => selectable = GetComponent<Selectable>();
    void OnEnable() { hover = focus = false; t = 0f; }

    public int Add(Graphic graphic, Color idle, Color on)
    {
        tints.Add(new Tint { graphic = graphic, idle = idle, on = on });
        graphic.color = idle;
        return tints.Count - 1;
    }

    /// <summary>Change the colours of graphic number i (segmented options and toggles do this when the choice changes).</summary>
    public void Set(int i, Color idle, Color on) => tints[i] = new Tint { graphic = tints[i].graphic, idle = idle, on = on };

    public void Slide(RectTransform target, float pixels)
    {
        slideTarget = target;
        slidePixels = pixels;
        slideStart = target.anchoredPosition;
    }

    public void OnPointerEnter(PointerEventData e) => hover = true;
    public void OnPointerExit(PointerEventData e) => hover = false;
    public void OnSelect(BaseEventData e) { focus = true; onFocus?.Invoke(); }
    public void OnDeselect(BaseEventData e) => focus = false;

    void Update()
    {
        bool usable = selectable == null || selectable.interactable;
        t = Mathf.MoveTowards(t, usable && (hover || focus) ? 1f : 0f, Time.unscaledDeltaTime * 7f);
        float e = t * t * (3f - 2f * t); // smoothstep
        transform.localScale = Vector3.one * Mathf.Lerp(1f, scale, e);
        if (slideTarget != null) slideTarget.anchoredPosition = slideStart + new Vector2(slidePixels * e, 0f);
        float alpha = usable ? 1f : 0.3f;
        foreach (var x in tints)
        {
            if (x.graphic == null) continue;
            var c = Color.Lerp(x.idle, x.on, e);
            c.a *= alpha;
            x.graphic.color = c;
        }
    }
}

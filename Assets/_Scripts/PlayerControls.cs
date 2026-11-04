using UnityEngine.InputSystem;

/// <summary>Keyboard bindings for one player. P1 = WASD side, P2 = arrow-key side.</summary>
public class PlayerControls
{
    readonly InputAction move, drift, reset;

    public PlayerControls(int playerIndex)
    {
        move = new InputAction("Move", InputActionType.Value);
        // mode=2 → digital, not normalised, so W+D gives full throttle and full steer
        if (playerIndex == 0)
        {
            move.AddCompositeBinding("2DVector(mode=2)")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            drift = new InputAction("Drift", InputActionType.Button, "<Keyboard>/leftShift");
            drift.AddBinding("<Keyboard>/space");
            reset = new InputAction("Reset", InputActionType.Button, "<Keyboard>/r");
        }
        else
        {
            move.AddCompositeBinding("2DVector(mode=2)")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            drift = new InputAction("Drift", InputActionType.Button, "<Keyboard>/rightShift");
            drift.AddBinding("<Keyboard>/slash");
            reset = new InputAction("Reset", InputActionType.Button, "<Keyboard>/enter");
            reset.AddBinding("<Keyboard>/numpadEnter");
        }
        move.Enable(); drift.Enable(); reset.Enable();
    }

    public UnityEngine.Vector2 Move => move.ReadValue<UnityEngine.Vector2>();
    public bool DriftHeld => drift.IsPressed();
    public bool DriftPressed => drift.WasPressedThisFrame();
    public bool ResetPressed => reset.WasPressedThisFrame();

    public void Dispose()
    {
        move.Dispose(); drift.Dispose(); reset.Dispose();
    }
}

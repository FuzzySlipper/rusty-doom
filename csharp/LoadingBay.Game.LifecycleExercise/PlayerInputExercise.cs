using System.Numerics;
using LoadingBay.Game;
using Rusty.Engine;

internal static class PlayerInputExercise
{
    internal static void Run()
    {
        var input = new LoadingBayPlayerInput(LoadingBayTuning.E1M1);
        LoadingBayPlayerInputFrame Read(params ProductInputEvent[] events) => input.Consume(events, 1f / 60f, default);

        // Physical release must stop a previously mapped held direction, then allow another press.
        var mappedW = Event(InputEventKind.MappedDigital, InputEdge.Held, x: 1f) with
        {
            Intent = "player.move.forward"u8.ToArray(),
        };
        Require(Read(Event(InputEventKind.Key, InputEdge.Pressed, KeyboardControl.KeyW), mappedW).Controls.Movement.Y == 1f,
            "W did not start movement");
        Require(Read().Controls.Movement.Y == 1f, "held movement was lost between input batches");
        Require(Read(Event(InputEventKind.Key, InputEdge.Released, KeyboardControl.KeyW)).Controls.Movement == Vector2.Zero,
            "physical W release left mapped movement latched");
        Require(Read(Event(InputEventKind.Key, InputEdge.Pressed, KeyboardControl.KeyW)).Controls.Movement.Y == 1f,
            "W could not restart after release");
        Require(Read(Event(InputEventKind.Key, InputEdge.Pressed, KeyboardControl.KeyS)).Controls.Movement == Vector2.Zero,
            "opposing keys did not cancel");
        Require(Read(Event(InputEventKind.Key, InputEdge.Released, KeyboardControl.KeyW)).Controls.Movement.Y == -1f,
            "releasing one opposing key lost the other direction");

        var cleared = Read(Event(InputEventKind.PointerDelta, x: 20f, y: -20f), Event(InputEventKind.Clear));
        Require(cleared.Cleared && cleared.Controls.Movement == Vector2.Zero && cleared.Look.After == default,
            "focus clear retained movement or pending look");
        Require(Read(Event(InputEventKind.Key, InputEdge.Pressed, KeyboardControl.KeyA)).Controls.Movement.X == -1f,
            "movement could not resume after focus clear");
        input.Clear();
        Require(Read().Controls.Movement == Vector2.Zero, "restore clear retained physical controls");

        var rightDown = Read(Event(InputEventKind.PointerDelta, x: 20f, y: 20f));
        Require(rightDown.Look.After.YawRadians > 0f && rightDown.Look.After.PitchRadians < 0f,
            "mouse right/down did not turn right/look down");
        var leftUp = Read(Event(InputEventKind.PointerDelta, x: -20f, y: -20f));
        Require(leftUp.Look.After.YawRadians < 0f && leftUp.Look.After.PitchRadians > 0f,
            "mouse left/up did not turn left/look up");
        var largeLook = Read(Event(InputEventKind.PointerDelta, x: 10_000f, y: -10_000f));
        Require(float.IsFinite(largeLook.Look.After.YawRadians) && largeLook.Look.After.PitchRadians <= LoadingBayTuning.E1M1.PointerLook.MaximumPitchRadians,
            "large pointer input did not clamp to valid look state");

        Require(Read(Event(InputEventKind.ControllerAxis, axis: ControllerAxis.Axis0, x: .1f)).Controls.Movement == Vector2.Zero,
            "idle stick drift moved the player");
        var forward = Read(Event(InputEventKind.ControllerAxis, axis: ControllerAxis.Axis0, x: 0f),
            Event(InputEventKind.ControllerAxis, axis: ControllerAxis.Axis1, x: -.575f));
        Require(MathF.Abs(forward.Controls.Movement.Y - .5f) < .0001f, "partial forward stick lost proportional movement");
        Require(Read(Event(InputEventKind.ControllerAxis, axis: ControllerAxis.Axis1, x: 0f)).Controls.Movement == Vector2.Zero,
            "neutral stick retained movement");
        var stick = Event(InputEventKind.ControllerAxis, axis: ControllerAxis.Axis2, x: 1f);
        float oneStep = input.Consume([stick], 1f / 60f, default).Look.After.YawRadians;
        float twoSteps = input.Consume([], 2f / 60f, default).Look.After.YawRadians;
        Require(MathF.Abs(twoSteps - 2f * oneStep) < .0001f, "stick look did not scale with admitted simulation time");
        Require(Read(Event(InputEventKind.Clear)).Look.After == default, "focus clear retained stick look");

        Require(Read(Event(InputEventKind.ControllerButton, InputEdge.Pressed, button: ControllerButton.Button0)).Controls.JumpPressed,
            "controller A did not request jump");
        Require(!Read().Controls.JumpPressed, "jump press repeated without another edge");
        Require(!Read(Event(InputEventKind.ControllerButton, InputEdge.Released, button: ControllerButton.Button0)).Controls.JumpHeld,
            "controller A release retained jump");
        Require(Read(Event(InputEventKind.ControllerButton, InputEdge.Pressed, button: ControllerButton.Button2)).Controls.UsePressed,
            "controller X did not request use");
        var fire = Event(InputEventKind.ControllerButton, InputEdge.Pressed, button: ControllerButton.Button7);
        Require(Read(fire).FireRequested && !Read().FireRequested, "controller fire was lost or repeated without an edge");
        Read(Event(InputEventKind.ControllerButton, InputEdge.Released, button: ControllerButton.Button7));
        Require(Read(fire).FireRequested, "controller fire did not rearm after release");
        Require(!Read(Event(InputEventKind.PointerButton, InputEdge.Pressed) with { PointerButton = PointerButton.Primary },
            Event(InputEventKind.Clear)).FireRequested, "focus clear retained pending fire");

        var gamepadAim = new LoadingBayPlayerInput(LoadingBayTuning.E1M1);
        bool aimActive = false;
        Vector2 aimDelta = default;
        LoadingBayPlayerInputFrame assistedLook = gamepadAim.Consume([Event(InputEventKind.ControllerAxis, axis: ControllerAxis.Axis2, x: 1f)], 1f / 60f, default,
            (delta, _, active) => { aimDelta = delta; aimActive = active; return Vector2.Zero; });
        Require(aimActive && aimDelta.X > 0f && assistedLook.Look.After.YawRadians == 0f,
            "gamepad aim seam did not receive the shaped controller delta before look integration");
        _ = gamepadAim.Consume([Event(InputEventKind.ControllerAxis, axis: ControllerAxis.Axis2, x: 0f)], 1f / 60f, default,
            (_, _, active) => { aimActive = active; return Vector2.Zero; });
        Require(aimActive, "neutral gamepad look unexpectedly cleared the selected gamepad aim mode");
        _ = gamepadAim.Consume([Event(InputEventKind.Key, InputEdge.Pressed, KeyboardControl.KeyJ)], 1f / 60f, default,
            (_, _, active) => { aimActive = active; return Vector2.Zero; });
        Require(!aimActive, "keyboard look did not clear the selected gamepad aim mode");
    }

    private static ProductInputEvent Event(InputEventKind kind, InputEdge edge = InputEdge.None,
        KeyboardControl key = KeyboardControl.None, ControllerButton button = ControllerButton.None,
        ControllerAxis axis = ControllerAxis.None, float x = 0f, float y = 0f) => default(ProductInputEvent) with
    {
        Kind = kind, Edge = edge, Keyboard = key, ControllerButton = button, ControllerAxis = axis, X = x, Y = y,
    };

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

using System.Numerics;
using Rusty.Engine;
using Rusty.Engine.Input;

namespace LoadingBay.Game;

/// <summary>Selects E1M1 controls; Engine owns physical state, stick shaping, and look math.</summary>
internal sealed class LoadingBayPlayerInput(LoadingBayTuning tuning)
{
    private readonly FpsInput _fps = new(tuning.PlayerInput);
    private readonly LookConfig _controllerLook = new(
        1f, 1f,
        tuning.PointerLook.MinimumPitchRadians,
        tuning.PointerLook.MaximumPitchRadians,
        tuning.PointerLook.MaximumDeltaRadians,
        InvertHorizontal: false,
        InvertVertical: false,
        WrapYaw: tuning.PointerLook.WrapYaw);
    private readonly LookConfig _keyboardLook = new(
        1f, 1f,
        tuning.PointerLook.MinimumPitchRadians,
        tuning.PointerLook.MaximumPitchRadians,
        MathF.PI,
        InvertHorizontal: false,
        InvertVertical: false,
        WrapYaw: true);
    private bool _gamepadAimActive;

    internal LoadingBayPlayerInputFrame Consume(
        ReadOnlySpan<ProductInputEvent> events,
        float simulationSeconds,
        LookState look,
        Func<Vector2, float, bool, Vector2>? adjustGamepadLook = null)
    {
        bool cleared = false;
        bool nonGamepadInput = false;
        foreach (ProductInputEvent input in events)
        {
            cleared |= input.Kind == InputEventKind.Clear;
            nonGamepadInput |= input.Kind is InputEventKind.Key or InputEventKind.PointerButton or InputEventKind.PointerDelta;
        }
        FpsInputFrame controls = _fps.Consume(events, simulationSeconds);
        float keyboardLookRate = LoadingBayTuning.KeyboardLookDegreesPerSecond;
        if (_fps.Physical.Held(LoadingBayTuning.PrecisionLookKeyboardControl))
            keyboardLookRate *= LoadingBayTuning.KeyboardPrecisionLookMultiplier;
        float radians = keyboardLookRate * (MathF.PI / 180f) * simulationSeconds;
        Vector2 keyboardLook = new(
            Axis(_fps.Physical.Held(LoadingBayTuning.LookRightKeyboardControl), _fps.Physical.Held(LoadingBayTuning.LookLeftKeyboardControl)) * radians,
            Axis(_fps.Physical.Held(LoadingBayTuning.LookUpKeyboardControl), _fps.Physical.Held(LoadingBayTuning.LookDownKeyboardControl)) * radians);
        bool controllerActivity = controls.ControllerLookRadians.LengthSquared() > 0f
            || _fps.Physical.Pressed(LoadingBayTuning.FireControllerButton)
            || _fps.Physical.Pressed(_fps.Config.Bindings.UseButton);
        if (cleared || nonGamepadInput)
            _gamepadAimActive = false;
        else if (controllerActivity)
            _gamepadAimActive = true;

        LookReceipt pointerLook = Look.IntegrateClamped(new LookRequest(look, controls.PointerDelta, _fps.Config.PointerLookConfig));
        Vector2 controllerLook = controls.ControllerLookRadians;
        if (_fps.Config.InvertControllerHorizontal) controllerLook.X = -controllerLook.X;
        if (_fps.Config.InvertControllerVertical) controllerLook.Y = -controllerLook.Y;
        if (adjustGamepadLook is not null)
            controllerLook = adjustGamepadLook(controllerLook, simulationSeconds, _gamepadAimActive);
        LookReceipt controllerIntegrated = Look.IntegrateClamped(new LookRequest(pointerLook.After, controllerLook, _controllerLook));
        LookReceipt integratedLook = Look.IntegrateClamped(new LookRequest(controllerIntegrated.After, keyboardLook, _keyboardLook)) with { Before = look };
        return new(controls, integratedLook, cleared,
            _fps.Physical.Pressed(PointerButton.Primary)
            || _fps.Physical.Pressed(LoadingBayTuning.FireKeyboardControl)
            || _fps.Physical.Pressed(LoadingBayTuning.FireControllerButton),
            _gamepadAimActive);
    }

    internal void Clear()
    {
        _fps.Physical.Clear();
        _gamepadAimActive = false;
    }

    private static float Axis(bool positive, bool negative) => positive == negative ? 0f : positive ? 1f : -1f;
}

internal readonly record struct LoadingBayPlayerInputFrame(
    FpsInputFrame Controls, LookReceipt Look, bool Cleared, bool FireRequested, bool GamepadAimActive);

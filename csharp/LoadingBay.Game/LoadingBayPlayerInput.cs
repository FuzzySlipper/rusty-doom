using System.Numerics;
using Rusty.Engine;
using Rusty.Engine.Input;

namespace LoadingBay.Game;

/// <summary>Selects E1M1 controls; Engine owns physical state, stick shaping, and look math.</summary>
internal sealed class LoadingBayPlayerInput(LoadingBayTuning tuning)
{
    private readonly FpsInput _fps = new(tuning.PlayerInput);
    private readonly LookConfig _keyboardLook = new(
        1f, 1f,
        tuning.PointerLook.MinimumPitchRadians,
        tuning.PointerLook.MaximumPitchRadians,
        MathF.PI,
        InvertHorizontal: false,
        InvertVertical: false,
        WrapYaw: true);

    internal LoadingBayPlayerInputFrame Consume(ReadOnlySpan<ProductInputEvent> events, float simulationSeconds, LookState look)
    {
        bool cleared = false;
        foreach (ProductInputEvent input in events)
            cleared |= input.Kind == InputEventKind.Clear;
        FpsInputFrame controls = _fps.Consume(events, simulationSeconds);
        LookReceipt pointerAndControllerLook = _fps.IntegrateLook(look, controls);
        float keyboardLookRate = LoadingBayTuning.KeyboardLookDegreesPerSecond;
        if (_fps.Physical.Held(LoadingBayTuning.PrecisionLookKeyboardControl))
            keyboardLookRate *= LoadingBayTuning.KeyboardPrecisionLookMultiplier;
        float radians = keyboardLookRate * (MathF.PI / 180f) * simulationSeconds;
        Vector2 keyboardLook = new(
            Axis(_fps.Physical.Held(LoadingBayTuning.LookRightKeyboardControl), _fps.Physical.Held(LoadingBayTuning.LookLeftKeyboardControl)) * radians,
            Axis(_fps.Physical.Held(LoadingBayTuning.LookUpKeyboardControl), _fps.Physical.Held(LoadingBayTuning.LookDownKeyboardControl)) * radians);
        LookReceipt integratedLook = Look.IntegrateClamped(new LookRequest(pointerAndControllerLook.After, keyboardLook, _keyboardLook)) with { Before = look };
        return new(controls, integratedLook, cleared,
            _fps.Physical.Pressed(PointerButton.Primary)
            || _fps.Physical.Pressed(LoadingBayTuning.FireKeyboardControl)
            || _fps.Physical.Pressed(LoadingBayTuning.FireControllerButton));
    }

    internal void Clear() => _fps.Physical.Clear();

    private static float Axis(bool positive, bool negative) => positive == negative ? 0f : positive ? 1f : -1f;
}

internal readonly record struct LoadingBayPlayerInputFrame(
    FpsInputFrame Controls, LookReceipt Look, bool Cleared, bool FireRequested);

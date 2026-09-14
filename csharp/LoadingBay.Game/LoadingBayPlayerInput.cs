using Rusty.Engine;
using Rusty.Engine.Input;

namespace LoadingBay.Game;

/// <summary>Selects E1M1 controls; Engine owns physical state, stick shaping, and look math.</summary>
internal sealed class LoadingBayPlayerInput(LoadingBayTuning tuning)
{
    private readonly FpsInput _fps = new(tuning.PlayerInput);

    internal LoadingBayPlayerInputFrame Consume(ReadOnlySpan<ProductInputEvent> events, float simulationSeconds, LookState look)
    {
        bool cleared = false;
        foreach (ProductInputEvent input in events)
            cleared |= input.Kind == InputEventKind.Clear;
        FpsInputFrame controls = _fps.Consume(events, simulationSeconds);
        return new(controls, _fps.IntegrateLook(look, controls), cleared,
            _fps.Physical.Pressed(PointerButton.Primary)
            || _fps.Physical.Pressed(LoadingBayTuning.FireControllerButton));
    }

    internal void Clear() => _fps.Physical.Clear();
}

internal readonly record struct LoadingBayPlayerInputFrame(
    FpsInputFrame Controls, LookReceipt Look, bool Cleared, bool FireRequested);

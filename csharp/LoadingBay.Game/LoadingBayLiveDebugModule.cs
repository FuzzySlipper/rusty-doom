using Rusty.Engine.Debugging;

namespace LoadingBay.Game;

/// <summary>Bounded product debug operations for the current live session.</summary>
public sealed class LoadingBayLiveDebugModule(
    Func<string> readout,
    Func<string, int, DebugCommandResult> setTrack,
    Func<bool, string> diagnostics) : IDebugCommandModule
{
    [DebugCommand("loading-bay.diagnostics", Description = "Enable or disable continuous HUD diagnostics (4 Hz).")]
    public string Diagnostics(bool enabled) => diagnostics(enabled);
    [DebugCommand("loading-bay.readout", Description = "Shows the current bounded Loading Bay session readout.")]
    public string Readout() => readout();
    [DebugCommand("loading-bay.set-track", Description = "Sets health or armor within authored bounds.")]
    public DebugCommandResult SetTrack(string track, int value) => setTrack(track, value);
}

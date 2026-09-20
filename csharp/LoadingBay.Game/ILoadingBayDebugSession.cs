using Rusty.Engine.Entities;
using Rusty.Engine.Debugging;

namespace LoadingBay.Game;

/// <summary>Optional lifecycle seam for exposing the session's current Engine entity projection.</summary>
internal interface ILoadingBayDebugSession
{
    EntityStore DebugEntityWorld { get; }

    /// <summary>Receives a replacement whenever persistence installs a fresh Engine projection.</summary>
    void SetDebugEntityWorldChanged(Action<EntityStore>? callback);
}

/// <summary>Optional live spatial inspection seam. Legacy sessions intentionally do not emulate it.</summary>
internal interface ILoadingBaySpatialObservationSession
{
    DebugCommandResult ReadSpatialMap(string format, int radius, double cellSize);

    DebugCommandResult ReadSpatialMapAt(string format, double centerX, double centerZ, double supportY, int radius, double cellSize);
}

using Rusty.Engine.Entities;
using Rusty.Engine.Debugging;

namespace LoadingBay.Game;

/// <summary>Optional lifecycle seam for exposing the session's current Engine entity projection.</summary>
internal interface ILoadingBayDebugSession
{
    EntityStore DebugEntityWorld { get; }

}

/// <summary>Exposes the Engine-owned interaction command surface over the session's ordinary use handler.</summary>
internal interface ILoadingBayInteractionDebugSession
{
    IDebugCommandModule InteractionDebugModule { get; }
}

/// <summary>Optional experiment integration; the E1M1 product owns no experiment gameplay.</summary>
internal interface ILoadingBayExperimentSession
{
    string DiagnosticReadout { get; }
    void Restart();
    void RegisterDebugCommands(IDebugCommandModuleRegistrar registrar);
}

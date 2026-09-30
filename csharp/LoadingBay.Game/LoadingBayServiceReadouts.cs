using Rusty.Engine;

namespace LoadingBay.Game;

internal readonly record struct LoadingBayPerceptionReadout(string LandmarkId, bool Visible, ulong Revision, uint VisibilityCasts, uint OcclusionRejects);
internal readonly record struct LoadingBayAnimationReadout(
    string CueId,
    bool RetainedAppearance,
    bool CompletionObserved,
    uint CompletionTransitionCount,
    uint AdmittedMeshes,
    uint RetainedInstances,
    uint PendingPlaybackCommands,
    uint RetainedRealizationFacts,
    ulong EvictedRealizationFacts);

internal readonly record struct LoadingBayEngineServiceReadout(
    LoadingBayPerceptionReadout Perception,
    PresentationFactsResult Presentation,
    LoadingBayAnimationReadout Animation,
    AudioBusReadout Audio,
    LoadingBayVoxelSceneReadout VoxelScene,
    LoadingBaySkyReadout Sky)
{
    internal static LoadingBayEngineServiceReadout Empty => new(
        default,
        default,
        default,
        default,
        LoadingBayVoxelSceneReadout.Empty,
        LoadingBaySkyReadout.Empty);
}

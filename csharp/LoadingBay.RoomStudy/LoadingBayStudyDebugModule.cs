using Rusty.Engine.Debugging;

namespace LoadingBay.Game;

/// <summary>Optional study authoring and playtest queries, outside the supported product.</summary>
internal sealed class LoadingBayStudyDebugModule(LoadingBayRoomStudy study) : IDebugCommandModule
{
    [DebugCommand("loading-bay.geometry-audit", Description = "Reads the opt-in initial construction-study geometry audit.")]
    public string GeometryAudit() => AuditPart(false);
    [DebugCommand("loading-bay.continuity-audit", Description = "Reads cached initial-pose mesh integrity, joins and enclosure findings.")]
    public string ContinuityAudit() => AuditPart(true);
    [DebugCommand("spatial.map", Description = "Reads the live omniscient X/Z map centered on the study player.")]
    public DebugCommandResult SpatialMap(string format, int radius, double cellSize) => study.ReadSpatialMap(format, radius, cellSize);
    [DebugCommand("spatial.map-at", Description = "Reads the omniscient study map at an explicit center and support height.")]
    public DebugCommandResult SpatialMapAt(string format, double centerX, double centerZ, double supportY, int radius, double cellSize) => study.ReadSpatialMapAt(format, centerX, centerZ, supportY, radius, cellSize);
    [DebugCommand("combat.observe", Description = "Reads study player, hostile, door and aim-assist facts.")]
    public DebugCommandResult CombatObserve() => study.ReadCombatObservation();
    [DebugCommand("navigation.targets", Description = "Reads authored study destinations and per-run progress.")]
    public DebugCommandResult NavigationTargets() => study.ReadNavigationTargets();
    [DebugCommand("navigation.route", Description = "Reads an Engine route toward a study target without moving the player.")]
    public DebugCommandResult NavigationRoute(string targetId) => study.ReadNavigationRoute(targetId);

    private string AuditPart(bool continuity)
    {
        try
        {
            string report = study.GeometryAudit;
            if (!report.StartsWith('{')) return report;
            using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(report);
            if (continuity) return document.RootElement.GetProperty("continuity").GetRawText();
            using MemoryStream stream = new();
            using (System.Text.Json.Utf8JsonWriter json = new(stream))
            {
                json.WriteStartObject();
                foreach (var property in document.RootElement.EnumerateObject())
                    if (property.Name != "continuity") property.WriteTo(json);
                json.WriteString("continuityCommand", "loading-bay.continuity-audit");
                json.WriteEndObject();
            }
            return System.Text.Encoding.UTF8.GetString(stream.ToArray());
        }
        catch (Exception error) { return $"Geometry audit failed; no complete report: {error}"; }
    }

}

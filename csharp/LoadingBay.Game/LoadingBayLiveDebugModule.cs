using Rusty.Engine.Debugging;

namespace LoadingBay.Game;

/// <summary>Explicit, bounded product debug operations for the current live session.</summary>
public sealed class LoadingBayLiveDebugModule : IDebugCommandModule
{
    private readonly Func<string> _readout;
    private readonly Func<bool, string>? _diagnostics;
    private readonly Func<string>? _geometryAudit;
    private readonly Func<string, int, DebugCommandResult> _setTrack;

    public LoadingBayLiveDebugModule(Func<string> readout, Func<string, int, DebugCommandResult> setTrack, Func<string>? geometryAudit = null, Func<bool, string>? diagnostics = null)
    {
        _geometryAudit = geometryAudit;
        _diagnostics = diagnostics;
        _readout = readout ?? throw new ArgumentNullException(nameof(readout));
        _setTrack = setTrack ?? throw new ArgumentNullException(nameof(setTrack));
    }

    [DebugCommand("loading-bay.diagnostics", Description = "Enable or disable continuous HUD diagnostics (4 Hz); default is disabled.")]
    public string Diagnostics(bool enabled) => _diagnostics?.Invoke(enabled) ?? "Diagnostics unavailable";

    [DebugCommand("loading-bay.readout", Description = "Shows the current bounded Loading Bay session readout.")]
    public string Readout() => _readout();

    [DebugCommand("loading-bay.geometry-audit", Description = "Reads the opt-in initial construction-study geometry audit; does not run per frame.")]
    public string GeometryAudit() => AuditPart(false);

    [DebugCommand("loading-bay.continuity-audit", Description = "Reads opt-in mesh integrity, declared joins and enclosure findings from the same initial capture.")]
    public string ContinuityAudit() => AuditPart(true);

    private string AuditPart(bool continuity)
    {
        try
        {
            string report = _geometryAudit?.Invoke() ?? "No construction-study audit in this session.";
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

    [DebugCommand("loading-bay.set-track", Description = "Sets the current health or armor track within its authored bounds.")]
    public DebugCommandResult SetTrack(string track, int value) => _setTrack(track, value);
}

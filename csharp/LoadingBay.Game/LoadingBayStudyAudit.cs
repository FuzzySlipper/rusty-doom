using System.Numerics;
using System.Text;
using System.Text.Json;
using Rusty.Engine;
using Rusty.Engine.Implicit;

namespace LoadingBay.Game;

/// <summary>Product labels and report projection for the opt-in Engine authoring audit.</summary>
internal sealed class LoadingBayStudyAudit : IDisposable
{
    private readonly List<RecipeJoin> _joins = [];
    internal void Register(RecipeJoin join) => _joins.Add(join.Placed(Matrix4x4.CreateScale(1, 1, -1)));
    private readonly IImplicitSurfacesService _service;
    private readonly ImplicitAudit _audit;
    private readonly Dictionary<ulong, string> _labels = [];
    private readonly Dictionary<string, int> _occurrences = [];
    internal LoadingBayStudyAudit(IImplicitSurfacesService service)
    {
        _service = service;
        _audit = service.CreateAudit();
    }
    internal void Capture(RecipeSurface surface, MeshResource mesh)
    {
        int ordinal = _occurrences.GetValueOrDefault(surface.Name) + 1;
        _occurrences[surface.Name] = ordinal;
        string label = $"{surface.Name} [{ordinal}]";
        // Stable across unrelated insertions; duplicated recipe names are numbered in author order.
        ulong id = 14695981039346656037UL;
        foreach (byte value in Encoding.UTF8.GetBytes(label)) id = unchecked((id ^ value) * 1099511628211UL);
        _labels.Add(id, label);
        _service.CaptureAuditPiece(new(_audit, id, surface.Field, surface.Root, mesh,
            surface.Placement, _service.ReadGeneration(surface.Field).SampleSpacing));
    }
    internal string Read()
    {
        var report = _service.ReadAudit(new(_audit, .1f));
        using MemoryStream stream = new();
        using (Utf8JsonWriter json = new(stream))
        {
            json.WriteStartObject();
            json.WriteString("scope", "initial authored placements; doors closed; sampled geometry diagnostics");
            json.WriteNumber("pieces", _labels.Count);
            json.WriteNumber("toleranceCells", .1f);
            json.WriteNumber("candidatePairs", report.CandidatePairs);
            json.WriteNumber("trianglePairs", report.TrianglePairs);
            json.WriteStartArray("diagnostics");
            foreach (var finding in report.Diagnostics.Span)
            {
                json.WriteStartObject();
                json.WriteString("pieceA", _labels[finding.PieceA]);
                json.WriteString("pieceB", _labels[finding.PieceB]);
                json.WriteString("classification", finding.Classification.ToString());
                json.WriteNumber("approximateArea", finding.ApproximateArea);
                Vector("minimum", finding.Minimum); Vector("maximum", finding.Maximum);
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WritePropertyName("continuity");
            WriteContinuity(json);
            json.WriteEndObject();
            void Vector(string name, Vector3 point)
            {
                json.WriteStartArray(name); json.WriteNumberValue(point.X); json.WriteNumberValue(point.Y);
                json.WriteNumberValue(point.Z); json.WriteEndArray();
            }
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }
    private void WriteContinuity(Utf8JsonWriter json)
    {
        ulong Id(string name) => _labels.Single(pair => pair.Value == name + " [1]").Key;
        void Vector(string name, Vector3 point)
        {
            json.WriteStartObject(name);
            json.WriteNumber("X", point.X); json.WriteNumber("Y", point.Y); json.WriteNumber("Z", point.Z);
            json.WriteEndObject();
        }
        void Report(string name, ImplicitAnalysisReportLeaseReceipt report)
        {
            json.WriteStartObject(name);
            json.WriteNumber("Complete", report.Complete); json.WriteNumber("Sampled", report.Sampled);
            json.WriteNumber("Resolution", report.Resolution);
            json.WriteStartArray("Diagnostics");
            foreach (var finding in report.Diagnostics.Span)
            {
                json.WriteStartObject();
                json.WriteNumber("PieceA", finding.PieceA); json.WriteNumber("PieceB", finding.PieceB);
                json.WriteString("Classification", finding.Classification.ToString());
                Vector("Minimum", finding.Minimum); Vector("Maximum", finding.Maximum);
                json.WriteNumber("ApproximateWidth", finding.ApproximateWidth);
                json.WriteNumber("ApproximateLength", finding.ApproximateLength);
                json.WriteNumber("ApproximateArea", finding.ApproximateArea);
                json.WriteNumber("Resolution", finding.Resolution);
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteStartArray("Path");
            foreach (Vector3 point in report.Path.Span)
            {
                json.WriteStartObject();
                json.WriteNumber("X", point.X); json.WriteNumber("Y", point.Y); json.WriteNumber("Z", point.Z);
                json.WriteEndObject();
            }
            json.WriteEndArray(); json.WriteEndObject();
        }
        void Join(RecipeJoin join)
        {
            json.WriteStartObject();
            json.WriteString("Name", join.Name); json.WriteString("PieceA", join.SurfaceA); json.WriteString("PieceB", join.SurfaceB);
            Vector("Center", join.Center); Vector("HalfU", join.HalfU); Vector("HalfV", join.HalfV);
            Report("Report", _service.ReadExpectedJoin(join.Request(_audit, Id, .4f, .05f, .025f, 20_000)));
            json.WriteEndObject();
        }
        ImplicitAnalysisReportLeaseReceipt Enclosure(Vector3 min, Vector3 max, Vector3 seed,
            ImplicitEnclosureOpening[] openings, uint budget)
            => _service.ReadEnclosure(new(_audit, LoadingBayStudyCoordinates.Minimum(min, max),
                LoadingBayStudyCoordinates.Maximum(min, max), LoadingBayStudyCoordinates.World(seed),
                openings.Select(o => new ImplicitEnclosureOpening(
                    LoadingBayStudyCoordinates.Minimum(o.Minimum, o.Maximum),
                    LoadingBayStudyCoordinates.Maximum(o.Minimum, o.Maximum))).ToArray(), .125f, budget));
        json.WriteStartObject();
        json.WriteStartObject("Labels");
        foreach (var label in _labels)
            json.WriteString(label.Key.ToString(System.Globalization.CultureInfo.InvariantCulture), label.Value);
        json.WriteEndObject();
        Report("Integrity", _service.ReadMeshIntegrity(new(_audit, ReadOnlyMemory<ImplicitAuditOpenRegion>.Empty)));
        json.WriteStartArray("Joins");
        foreach (RecipeJoin join in _joins) Join(join);
        json.WriteEndArray();
        // Explicit regional enclosure with intentional connections to the rest of the level.
        Report("Enclosure", Enclosure(new(-3, -1, 19), new(13, 8, 36), new(7, 1, 32),
            new ImplicitEnclosureOpening[] {
                new(new(-2.1f, -.1f, 19), new(2.1f, 4.6f, 20.8f)),
                new(new(11.8f, -.1f, 30), new(13, 4.6f, 34)),
            }, 1_500_000));
        Report("SpawnEnclosure", Enclosure(new(-26, -2, -6), new(4, 8, 21), new(-7, 1, -3),
            new ImplicitEnclosureOpening[] {
                new(new(-2.1f, -.1f, 20), new(2.1f, 4.6f, 20.8f)),
                new(new(-24.6f, -.35f, 6.75f), new(-24.3f, 3.75f, 11.25f)),
                new(new(1.9f, .2f, 5), new(3.1f, 6.1f, 8)),
                new(new(1.9f, .2f, 10), new(3.1f, 6.1f, 13)),
            }, 5_000_000));
        Report("EastGalleryEnclosure", Enclosure(new(65, -3, -13), new(79, 7, 21), new(72, -.5f, -10),
            new ImplicitEnclosureOpening[] {
                new(new(65.9f, -1.6f, -12), new(66.5f, 2.3f, -8)),
                new(new(65.9f, 2.7f, 15), new(66.5f, 5.6f, 19)),
            }, 3_000_000));
        json.WriteEndObject();
    }
    public void Dispose() => _audit.Dispose();
}

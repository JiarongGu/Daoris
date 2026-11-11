using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A run's node record whose binding is gone: the record of unit <c>run:&lt;id&gt;</c> is removed, uncopied, when <c>workflows/runs/&lt;id&gt;.json</c>
/// is not there, since a history clear took the binding and left the record. A record of any other unit, one that does not read and one
/// named for another file are left, and the step run again changes nothing.
/// </summary>
public sealed class UnboundRecordsStep : IHomeMigrationStep
{
    public string Name => "unbound-records";

    public void Run(HomeMigrationRun run)
    {
        var folder = NodeRecordFiles.FolderOf(run.Home);
        if (!Directory.Exists(folder)) return;
        foreach (var file in Directory.EnumerateFiles(folder, "*.json").ToList())
        {
            if (UnitOf(file) is not { } unit || !unit.StartsWith(WorkplaceRunFiles.RunUnitPrefix, StringComparison.Ordinal)) continue;
            var id = unit[WorkplaceRunFiles.RunUnitPrefix.Length..];
            if (!WorkplaceRunFiles.IsRunId(id) || WorkplaceRunFiles.Bound(run.Home, id)) continue;
            if (!string.Equals(Path.GetFileNameWithoutExtension(file), NodeRecordFiles.KeyOf(unit), StringComparison.Ordinal)) continue;
            try
            {
                File.Delete(file);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            run.Count(Name, "removed");
        }
    }

    private static string? UnitOf(string file)
    {
        try
        {
            using var document = JsonDocument.Parse(AtomicFile.ReadText(file));
            return document.RootElement is { ValueKind: JsonValueKind.Object } root && root.TryGetProperty("unit", out var unit)
                && unit.ValueKind == JsonValueKind.String ? unit.GetString() : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }
}

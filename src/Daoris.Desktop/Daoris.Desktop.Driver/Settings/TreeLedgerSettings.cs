using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The copies' floor and budget on this machine, two fields of <c>driver.json</c> read by this unit alone: <c>treeFloorGb</c>, the free
/// space under which the ledger's reading writes its floor notice, 30 GB where absent; <c>treeBudgetGb</c>, the most a repository's
/// copies may hold before it writes its budget notice, none where absent. Whole GB from 1 to 4096; a value outside, or a file that does
/// not read, reads as absent. The choices' writer edits the file as it stands, so a field set here by hand stays across its saves. A
/// repository's own file narrows the budget once the keeper reads one.
/// </summary>
public sealed record TreeLedgerSettings(int? FloorGb, int? BudgetGb)
{
    public const string FloorFile = "treeFloorGb";

    public const string BudgetFile = "treeBudgetGb";

    public const int DefaultFloorGb = 30;

    private const long Gb = 1L << 30;

    public long FloorBytes => (FloorGb ?? DefaultFloorGb) * Gb;

    public long? BudgetBytes => BudgetGb * Gb;

    /// <summary>The two fields of the <c>driver.json</c> at <paramref name="path"/>; both absent where it is missing or does not read.</summary>
    public static TreeLedgerSettings Of(string path)
    {
        try
        {
            if (!File.Exists(path)) return new(null, null);
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? new(Field(document.RootElement, FloorFile), Field(document.RootElement, BudgetFile))
                : new(null, null);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return new(null, null);
        }
    }

    private static int? Field(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var gb) && gb is >= 1 and <= 4096
            ? (int)gb
            : null;
}

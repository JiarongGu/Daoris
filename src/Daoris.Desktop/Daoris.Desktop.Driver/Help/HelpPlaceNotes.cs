using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// One call an Ask Daoris connector noted as naming a place: when, what it did, the workspace (or <c>all</c>) and the repository it
/// named, what it named of them, and how many a search's answer found where its tool said so.
/// </summary>
public sealed record HelpPlaceNote(DateTimeOffset At, string Act, string? Workspace, string? Repository, string? What, int? Found = null);

/// <summary>
/// The places an Ask Daoris conversation's connector noted (the service's <c>CallPlaceFile</c>), read for its receipt.
/// </summary>
/// <remarks>
/// <b>The file is the contract</b> (<c>.claude/knowledge/twins.md</c>): <c>&lt;home&gt;/sessions/&lt;session&gt;/places.jsonl</c>, one JSON
/// object a line. 🔴 <see cref="FileName"/> and the folder are the connector's, copied here. A line that does not read is skipped.
/// </remarks>
public static class HelpPlaceNotes
{
    /// <summary>The file's name in the session's folder. 🔴 The connector's <c>CallPlaceFile.FileName</c> is the same name.</summary>
    public const string FileName = "places.jsonl";

    private static readonly Regex SessionShape = new("^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$", RegexOptions.CultureInvariant);

    public static string PathOf(string home, string sessionId) => DaorisHome.Entry(home, HomeEntry.Sessions, sessionId, FileName);

    /// <summary>Each noted call, first to last; none where the session is not shaped as an id or noted nothing.</summary>
    public static IReadOnlyList<HelpPlaceNote> Read(string home, string sessionId)
    {
        if (!SessionShape.IsMatch(sessionId)) return [];
        string[] lines;
        try
        {
            using var stream = new FileStream(PathOf(home, sessionId), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            lines = reader.ReadToEnd().Split('\n');
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return [];
        }

        var notes = new List<HelpPlaceNote>();
        foreach (var line in lines)
        {
            if (Note(line.Trim()) is { } note) notes.Add(note);
        }

        return notes;
    }

    private static HelpPlaceNote? Note(string line)
    {
        if (line.Length == 0) return null;
        try
        {
            using var read = JsonDocument.Parse(line);
            var element = read.RootElement;
            if (element.ValueKind != JsonValueKind.Object || Moments.Read(element, "at") is not { } at) return null;
            if (Json.NonEmpty(element, "act") is not { } act) return null;
            var workspace = Json.NonEmpty(element, "workspace")?.Trim();
            var repository = Json.NonEmpty(element, "repository")?.Trim();
            var found = element.TryGetProperty("found", out var count) && count.ValueKind == JsonValueKind.Number
                && count.TryGetInt32(out var number) && number >= 0 ? number : (int?)null;
            return workspace is null && repository is null ? null : new(at, act, workspace, repository, Json.NonEmpty(element, "what"), found);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

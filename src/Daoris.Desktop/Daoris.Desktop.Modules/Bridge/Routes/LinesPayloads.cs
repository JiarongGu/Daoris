using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daoris.Desktop;

// The payloads of LinesRoutes' choices, each named for its route.

/// <summary>A repository or a workspace, one of them; no language clears it, and a code the table does not hold is the driver's refusal.</summary>
public sealed record SetLanguage(string? Language, string? Repository, string? Workspace);

/// <summary>
/// A repository or a workspace, one of them. A <c>Branch</c> sent at all sets the line, blank or null clearing it; a <c>Policy</c> sent
/// sets how the line is integrated, null withdrawing it, and with no branch beside it leaves the line as it is.
/// </summary>
public sealed record SetLine(string? Repository, string? Workspace, JsonElement? Branch, JsonElement? Policy);

// A rule edit in its shared table's own shape: the place it is for, then what it does, the nested object the table's alone to judge.
// The handler hands it to the table as the terminal's verb hands its own (`RuleEdit.Shape`).

public sealed record SetReview(string? Repository, string? Workspace, JsonElement? Put, bool? Required, string? Drop, bool None = false, bool Clear = false);

public sealed record SetOpinion(string? Repository, string? Workspace, JsonElement? Set, bool None = false, bool Clear = false);

public sealed record SetHelpers(string? Repository, string? Workspace, JsonElement? Set, bool Clear = false);

public sealed record SetHandOver(string? Repository, string? Workspace, JsonElement? Set, bool Clear = false);

public sealed record SetGrowth(string? Repository, string? Workspace, string? Value);

public sealed record SetCommits(string? Repository, string? Workspace, JsonElement? Set, bool Clear = false);

/// <summary>No <c>Access</c>, or a null one, clears the level's setup.</summary>
public sealed record SetAccess(string? Repository, string? Workspace, JsonElement? Access, bool Machine = false);

/// <summary>The classes <c>Access</c> sets, or <c>OneValue</c> for read and write; neither clears the level.</summary>
public sealed record SetGitAccess(string? Repository, string? Workspace, JsonElement? Access, string? OneValue, bool Machine = false);

/// <summary>A rule edit's payload as its shared table reads it, the terminal's verbs' and the page's alike.</summary>
internal static class RuleEdit
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    /// <summary>The record in the table's shape: a field not sent is left out, and a JSON null sent is kept.</summary>
    public static JsonElement Shape<T>(T press) => JsonSerializer.SerializeToElement(press, Wire);
}

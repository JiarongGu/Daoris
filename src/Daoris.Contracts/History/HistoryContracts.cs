using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daoris.Contracts.History;

// Clearing finished history from this machine, listed first and then pressed: the desk judges the records' half for both doors, and the
// driver, which judges the trees, the landings and the processes, calls these and then removes what the home kept. A local host's
// alone, as a person clears their own machine; nothing here travels, and a quest a remote numbered is forgotten here while the team
// keeps its copy.

/// <summary>
/// What each unit holds and would take, or why it stays: a workspace's finished history, one quest's work, one quest's failed sessions,
/// or one ask's work. Deletes nothing. Name one scope; <c>Failed</c> only beside a quest.
/// </summary>
public sealed record PlanHistory(string? Workspace = null, string? Quest = null, string? Ask = null, bool? Failed = null)
    : IRoute<HistoryPlanResponse>
{
    public static string Method => "GET";
    public static string Path => "/api/history";
}

/// <summary>The second press: exactly the units named, each judged again where it is cleared, and cleared or kept with its word.</summary>
public sealed record ClearHistory(IReadOnlyList<HistoryUnitRequest?>? Units) : IRoute<HistoryClearResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/history/clear";
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(PlanHistory))]
[JsonSerializable(typeof(ClearHistory))]
[JsonSerializable(typeof(HistoryPlanResponse))]
[JsonSerializable(typeof(HistoryClearResponse))]
internal sealed partial class HistoryJson : JsonSerializerContext;

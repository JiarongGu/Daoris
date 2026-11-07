using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The routes a workflow proposal chooses its workflow for: <c>default</c>, <c>landed</c>
/// or <c>kind:&lt;id&gt;</c>, how they are judged, the edit that chooses, and the words that say them.
/// </summary>
internal static class HelpWorkflowRoutes
{
    public static class Routes
    {
        public const string Default = "default";
        public const string Landed = "landed";
        public const string KindPrefix = "kind:";
    }

    /// <summary>Why a route is none a proposal names, or null: <c>default</c>, <c>landed</c>, or <c>kind:&lt;id&gt;</c>.</summary>
    public static string? RouteProblem(string route) =>
        route is Routes.Default or Routes.Landed || (KindOf(route) is { } kind && WorkflowSelection.IsKindId(kind))
            ? null
            : $"`{route}` is not a route: `default`, `landed` for the work that lands, or `kind:<id>` for one of the scope's kinds, such as `kind:docs`.";

    public static string? KindOf(string? route) =>
        route is not null && route.StartsWith(Routes.KindPrefix, StringComparison.Ordinal) ? route[Routes.KindPrefix.Length..] : null;

    /// <summary>
    /// Why a route with no version cannot choose its workflow, or null: the workflow is saved here and its newest
    /// version reads, and the proposal names no base and no name, which only a save takes.
    /// </summary>
    public static string? UseProblem(string id, HelpProposal proposal, WorkflowFileFound? found)
    {
        if (proposal.Base is not null || !string.IsNullOrWhiteSpace(proposal.Name)) return "a `route` with no version saves nothing, so it names no `base` and no `name`.";
        if (found is null) return $"there is no workflow `{id}` on this machine: a `route` with no version chooses a workflow saved here.";
        if (found.Read.Problem is { } unread) return $"`{id}`'s file cannot be read here, so nothing chooses it: {unread}";
        return found.Read.Versions[^1] is { Steps: null, Graph: null } newest
            ? $"`{id}` v{newest.Version} cannot be read here, so nothing chooses it: {newest.Problem}"
            : null;
    }

    /// <summary>Whether a proposed graph holds a landing node, which makes a new workflow a development pipeline.</summary>
    public static bool HoldsLanding(JsonElement graph) =>
        graph.TryGetProperty("nodes", out var nodes) && nodes.ValueKind == JsonValueKind.Array
        && nodes.EnumerateArray().Any(node => node.ValueKind == JsonValueKind.Object && node.TryGetProperty("kind", out var kind)
            && kind.ValueKind == JsonValueKind.String && kind.GetString() is NodeKinds.Land);

    /// <summary>Whether a choice names the workflow for the route already.</summary>
    public static bool ChoosesRoute(WorkflowChoice? choice, string id, string route) => choice is not null && route switch
    {
        Routes.Default => choice.Default == id,
        Routes.Landed => choice.Landed?.Workflow == id,
        _ => choice.KindOf(KindOf(route)!)?.Workflow == id,
    };

    /// <summary>
    /// The edit choosing the workflow for the route, as the terminal's <c>use</c> makes it: a repository's kind checked against its
    /// workspace's kinds, and a landed route with no gates accepted, which only the person's <i>Accept the gates</i> does.
    /// </summary>
    public static WorkflowUse UseOf(string id, string route, string? repository, string? workspace, string? inWorkspace)
    {
        var kind = KindOf(route);
        return new WorkflowUse(id, repository, repository is null ? workspace : null, kind)
        {
            InKnown = repository is not null && kind is not null,
            In = inWorkspace,
            Landed = route == Routes.Landed,
        };
    }

    /// <summary>Who the choice is for, as the card's first line says it.</summary>
    public static string ForRoute(string? route, string? repository, string? workspace)
    {
        var whose = repository is not null ? $"`{repository}`" : $"the workspace `{workspace}`";
        return route switch
        {
            Routes.Landed => $"for the work that lands in {whose}",
            not null when KindOf(route) is { } kind => repository is not null ? $"for `{repository}`'s kind `{kind}`" : $"for the workspace `{workspace}`'s kind `{kind}`",
            _ => $"for {whose}",
        };
    }

    /// <summary>The route as said after <i>chooses it</i>: nothing for the default.</summary>
    public static string RouteWords(string route) => route switch
    {
        Routes.Landed => " for the work that lands",
        _ when KindOf(route) is { } kind => $" for its kind `{kind}`",
        _ => "",
    };
}

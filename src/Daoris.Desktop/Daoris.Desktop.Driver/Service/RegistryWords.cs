namespace Daoris.Driver;

/// <summary>
/// What <c>connect</c>, <c>retire</c>, <c>wire</c> and <c>import</c> say (ONEWAY1u, D194 point 6): one words function per verb over the
/// operation's result, the lines the TypeScript verbs said (<c>fixtures/driver-registry-text.json</c>), and the dry run's, which sends
/// nothing.
/// </summary>
public static class RegistryWords
{
    public static IReadOnlyList<string> ConnectDry(ConnectPlan plan) =>
        [plan.Body.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }), $"daoris: would register with {plan.Service}/api/registry"];

    public static IReadOnlyList<string> Connected(ConnectDone done)
    {
        var plan = done.Plan;
        List<string> lines =
        [
            $"daoris: registered {plan.Repository} with {plan.Service}",
            $"  owns {plan.Owns} area(s); accepts {plan.Accepts} kind(s)",
        ];
        if (plan.Uses.Count > 0) lines.Add($"  uses {string.Join(", ", plan.Uses)}");
        if (plan.Lanes.Count > 0)
        {
            var named = string.Join(", ", plan.Lanes.Select(lane => $"{lane.Id}{(lane.Steward ? " (the steward's)" : "")}"));
            lines.Add($"  lanes {named}: a quest addresses one as {plan.Repository}:<lane>, or several as {plan.Repository}:<lane>+<lane>");
        }

        if (!string.IsNullOrEmpty(done.Workspace)) lines.Add($"  workspace: {done.Workspace} — this machine's wiring; nothing was written here");
        lines.Add("  siblings can now address quests here, and see what is worth asking.");
        return lines;
    }

    public static string RetireDry(string repository) => $"daoris: would retire {repository} — the registration only; no file is ever touched";

    public static string Retired(string message) => $"daoris: {message}";

    public static string WireDry(string repository, string workspace) => $"daoris: would move `{repository}` to workspace `{workspace}`";

    public static string ImportDry(string? folder, string? workspace) =>
        $"daoris: would import {folder ?? "the service's own knowledge root"}" + (string.IsNullOrEmpty(workspace) ? "" : $" into workspace `{workspace}`");

    public static string Imported(string message) => $"daoris: {message}";
}

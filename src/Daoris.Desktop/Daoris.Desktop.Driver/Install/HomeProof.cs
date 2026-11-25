using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What a build says of a home it starts on: the agent accounts it lists, how many sessions' records and runs' bindings it meets, and
/// each thing it cannot read or finds amiss (<see cref="Problems"/>, each a sentence): a session's record, a run's binding or a
/// workflow's file that does not read, a run that binds a workflow the home does not hold, a path no entry names.
/// </summary>
public sealed record HomeProofReport(
    IReadOnlyDictionary<string, IReadOnlyList<string>> Accounts, int Sessions, int Runs, IReadOnlyList<string> Problems);

/// <summary>
/// <c>daoris home proof</c>'s reading, the one a build asks of itself and a running build asks of the staged one
/// (<see cref="HomeProbe"/>): the answers a build must still give of the home it is about to meet, which an update that read a place
/// the home no longer held once did not (FIX-LOG 2026-11-20, three accounts hidden). It reads and changes nothing under the home.
/// </summary>
public static class HomeProof
{
    /// <summary>The build's reading of <paramref name="home"/>.</summary>
    public static HomeProofReport Read(string home)
    {
        var problems = new List<string>();
        var accounts = new SortedDictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var agent in HarnessAccountFolders.AgentFolders(home))
        {
            if (HarnessAccountFolders.Profiles(home, agent) is { Count: > 0 } found) accounts[agent] = found;
        }

        var sessions = Sessions(home, problems);
        var runs = Runs(home, problems);
        foreach (var stray in HomeStrays.Read(home).Paths)
        {
            problems.Add($"`{stray.Path}` names no entry of the home's table, so no reader finds what is there");
        }

        return new(accounts, sessions, runs, problems);
    }

    /// <summary>The report as one line, for a verb to print and <see cref="Parse"/> to read back.</summary>
    public static string Say(HomeProofReport report) => JsonSerializer.Serialize(report, Options);

    /// <summary>The report a verb printed, found in its output; null where there is none.</summary>
    public static HomeProofReport? Parse(string output)
    {
        foreach (var line in output.Split('\n').Select(each => each.Trim()).Where(each => each.StartsWith('{')).Reverse())
        {
            try
            {
                return JsonSerializer.Deserialize<HomeProofReport>(line, Options);
            }
            catch (JsonException)
            {
                // Not the report: a line of something else that begins so.
            }
        }

        return null;
    }

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private static int Sessions(string home, List<string> problems)
    {
        var folder = DaorisHome.Entry(home, HomeEntry.Sessions);
        if (!Directory.Exists(folder)) return 0;
        var events = new SessionEvents(folder);
        const string suffix = ".events.jsonl";
        var count = 0;
        foreach (var path in Directory.GetFiles(folder, "*" + suffix).Order(StringComparer.Ordinal))
        {
            var id = Path.GetFileName(path)[..^suffix.Length];
            if (!SessionEventText.IsId(id)) continue;
            count++;
            try
            {
                events.Page(id, null, 1);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                problems.Add($"session `{id}`'s record does not read: {error.Message}");
            }
        }

        return count;
    }

    private static int Runs(string home, List<string> problems)
    {
        var folder = WorkflowRunBindings.FolderOf(home);
        var workflows = WorkflowPorts.Store(home).List();
        foreach (var workflow in workflows.Where(each => each.Read.Problem is not null))
        {
            problems.Add($"workflow `{workflow.Id}` does not read: {workflow.Read.Problem}");
        }

        if (!Directory.Exists(folder)) return 0;
        var runs = WorkflowPorts.Runs(home);
        var held = workflows.Select(each => each.Id).ToHashSet(StringComparer.Ordinal);
        var count = 0;
        foreach (var path in Directory.GetFiles(folder, "*.json").Order(StringComparer.Ordinal))
        {
            var run = Path.GetFileNameWithoutExtension(path);
            if (!WorkflowRunBindings.IsRunId(run)) continue;
            count++;
            if (runs.Read(run) is not { } binding) problems.Add($"run `{run}`'s binding does not read");
            else if (!held.Contains(binding.Workflow)) problems.Add($"run `{run}` binds the workflow `{binding.Workflow}`, which the home does not hold");
        }

        return count;
    }
}

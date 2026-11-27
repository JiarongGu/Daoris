using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>The build's reading of a home (<see cref="HomeProof"/>): accounts, sessions' records, runs' bindings and workflows, with the strays said apart.</summary>
internal sealed class HomeProofReading : IHomeProofReading
{
    [ModuleInitializer]
    internal static void Install() => HomeProof.Reading = new HomeProofReading();

    public HomeProofReport Read(string home)
    {
        var problems = new List<string>();
        var accounts = Accounts(home);
        var sessions = Sessions(home, problems);
        var runs = Runs(home, problems);
        var strays = HomeStrays.Read(home).Paths.Select(stray => stray.Path).ToList();
        return new(accounts, sessions, runs, problems, strays);
    }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> Accounts(string home)
    {
        var accounts = new SortedDictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var agent in HarnessAccountFolders.AgentFolders(home))
        {
            if (HarnessAccountFolders.Profiles(home, agent) is { Count: > 0 } found) accounts[agent] = found;
        }

        return accounts;
    }

    public IReadOnlyList<string> Reads { get; } =
    [
        HomeEntry.AgentHomes.New[..HomeEntry.AgentHomes.New.LastIndexOf('/')],
        HomeEntry.Sessions.New + "/*" + SessionRecord,
        HomeEntry.DesktopDb.New,
        HomeEntry.Workflows.New + "/*.json",
        HomeEntry.Workflows.New + "/" + WorkplaceRunFiles.Runs + "/*.json",
    ];

    private const string SessionRecord = ".events.jsonl";

    private static int Sessions(string home, List<string> problems)
    {
        var folder = DaorisHome.Entry(home, HomeEntry.Sessions);
        if (!Directory.Exists(folder)) return 0;
        var events = new SessionEvents(folder);
        const string suffix = SessionRecord;
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
            // A run on Current names no saved workflow, and one the bind held (its workflow not saved, unread) says so itself: both are records the product writes.
            else if (!binding.IsCurrent && binding.Problem is null && !held.Contains(binding.Workflow)) problems.Add($"run `{run}` binds the workflow `{binding.Workflow}`, which the home does not hold");
        }

        return count;
    }
}

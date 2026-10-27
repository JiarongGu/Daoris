namespace Daoris.Driver;

/// <summary>
/// Where a run's files lie under a home and how a run's id reads, which the workplace's history, its clearing and a session's files
/// name without the workflows (FOUNDATION1 mod-x; the design §14.2: the lowest subdomain that reads it owns it). A quest's run is
/// its binding under the home's workflows (<see cref="Runs"/>), a chat's run is keyed <see cref="ChatPrefix"/> and the first record of its
/// line, with the opinion gate's file and the look's beside it. The workflows' <c>WorkflowRunBindings</c> and <c>ChatRuns</c> answer
/// through these, so every other reader keeps its spelling and no path, id or sentence changes.
/// </summary>
public static class WorkplaceRunFiles
{
    /// <summary>The folder under the home's workflows that holds the runs' bindings.</summary>
    public const string Runs = "runs";

    /// <summary>The folder under the home's workflows that holds a chat run's look files.</summary>
    public const string Looks = "looks";

    /// <summary>What a chat run's id begins with, apart from a quest's 12 hex digits by its spelling.</summary>
    public const string ChatPrefix = "chat-";

    /// <summary>The folder of a home's runs.</summary>
    public static string RunsFolder(string home) => Path.Combine(DaorisHome.Entry(home, HomeEntry.Workflows), Runs);

    /// <summary>A run's id as a file may be named by it: a quest's id, letters, digits, dashes and underscores, at most 64.</summary>
    public static bool IsRunId(string? run) => run is not null && SafeNames.IsPlainId(run);

    /// <summary>A run's file, its id passed by <see cref="IsRunId"/>, so it names no path.</summary>
    public static string PathOf(string home, string run) =>
        IsRunId(run) ? Path.Combine(RunsFolder(home), run + ".json") : throw new DriverException($"`{run}` is not a run's id.");

    /// <summary>Whether a run is bound here: its id is a run's, and its file is there.</summary>
    public static bool Bound(string home, string run) => IsRunId(run) && File.Exists(PathOf(home, run));

    /// <summary>Whether a run's id is a chat run's.</summary>
    public static bool IsChatRun(string? run) => run is not null && run.StartsWith(ChatPrefix, StringComparison.Ordinal) && run.Length > ChatPrefix.Length;

    /// <summary>The record a chat run's id names, its line's first in its tree; null for a run that is not a chat's.</summary>
    public static string? RecordOf(string run) => IsChatRun(run) ? run[ChatPrefix.Length..] : null;

    /// <summary>The folder of the opinion gates' files, by chain or by chat run.</summary>
    public static string GatesFolder(string home) => DaorisHome.Entry(home, HomeEntry.OpinionGates);

    /// <summary>The folder of a chat run's look files (§5.3), apart from a quest's, which lives on its quest.</summary>
    public static string LooksFolder(string home) => Path.Combine(DaorisHome.Entry(home, HomeEntry.Workflows), Looks);
}

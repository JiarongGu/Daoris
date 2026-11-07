using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>What the press is handed (<see cref="SpaceFree"/>): the home, and the reads and seams a test stands in for.</summary>
public sealed record SpaceFreeWorld(string Home)
{
    /// <summary>The sizes counted, whose set-aside walk says each folder's bytes; a run's end counts the kinds it removed from again.</summary>
    public SpaceSizes Sizes { get; init; } = SpaceSizes.Shared;

    /// <summary>How the downloads are judged right before each goes (KEEP1b's act).</summary>
    public Func<DownloadJudge>? Judge { get; init; }

    /// <summary>The shell's browser host; null with no shell to say whether the browser runs, which keeps its caches.</summary>
    public IBrowserCaches? Caches { get; init; }

    /// <summary>The machine log the run writes <c>space.freed</c> and <c>space.kept</c> to.</summary>
    public MachineLog? Log { get; init; }

    /// <summary>What time it is.</summary>
    public Func<DateTimeOffset> Now { get; init; } = () => DateTimeOffset.UtcNow;

    /// <summary>The rules kept by; the home's by default.</summary>
    public Func<string, KeepPolicy> Policy { get; init; } = KeepPolicy.Of;

    /// <summary>A rename aside; a test's refuses, as a program holding the folder makes Windows refuse.</summary>
    public Action<string, string>? Move { get; init; }

    /// <summary>
    /// The registry's repositories, whose checkouts the finished trees' record pass reads git in, read-only; null where the press
    /// has no service, and then no record is posted and no folder resumed before it lists.
    /// </summary>
    public Func<CancellationToken, Task<IReadOnlyList<RepoView>>>? Registry { get; init; }

    /// <summary>The service's door for a finished tree's record: null where it keeps it, else why not.</summary>
    internal Func<FinishedPost, CancellationToken, Task<RecordRefusal?>>? PostRecord { get; init; }

    /// <summary>This world with the service's registry and record door, for the window's press.</summary>
    public SpaceFreeWorld WithService(ServiceClient service) => this with { Registry = service.RegistryAsync, PostRecord = service.PostWorkRecordAsync };

    /// <summary>The terminal's world alone: this home's downloads judged from its files, its running sessions by the process markers.</summary>
    public static SpaceFreeWorld OfThisTerminal(string home, MachineLog? log) => new(home)
    {
        Log = log,
        Judge = () => DownloadJudge.Of(home, HarnessSettings.ResolvePath(home), DriverConfig.ResolvePath(home),
            token => LiveSessions.ReadAsync(null, DaorisHome.Entry(home, HomeEntry.SessionPids), token)),
    };

    internal DownloadJudge JudgeOrFiles() => Judge?.Invoke() ?? DownloadJudge.Of(
        Home, HarnessSettings.ResolvePath(Home), DriverConfig.ResolvePath(Home),
        token => LiveSessions.ReadAsync(null, DaorisHome.Entry(Home, HomeEntry.SessionPids), token));
}

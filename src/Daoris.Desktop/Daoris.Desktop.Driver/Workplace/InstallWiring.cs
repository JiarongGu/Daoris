using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>
/// The workplace's answer to the port the install declares for a child's git (FOUNDATION1 mod-v, the design §14.3): the prompt variables and
/// the doors <see cref="GitWiring"/> hands. Installed when the driver's assembly loads, before any start can pass the tools' seam, so every
/// host, the loop and every test hand a child exactly what they always did and no construction site hands the install anything.
/// </summary>
internal sealed class WorkingTreeGit : IChildGit
{
    [ModuleInitializer]
    internal static void Install() => ChildGit.Current = new WorkingTreeGit();

    public void Hand(ProcessStartInfo info, string? home) => GitWiring.Hand(info, home);
}

/// <summary>
/// The workplace's answer to the port the install declares for the delete of a downloaded version (FOUNDATION1 mod-v, the design §14.3): the
/// download clearing, judged by what the files that say what is used say, the sessions that run and the locks of those files. Installed when
/// the driver's assembly loads, so the tools' delete, in a host or a test, clears exactly as it always did.
/// </summary>
internal sealed class DownloadClearings : IToolDownloads
{
    [ModuleInitializer]
    internal static void Install() => ToolDownloads.Current = new DownloadClearings();

    public async Task<ToolVersionCleared> ClearAsync(
        string home, string tool, string version, string harnesses, string config, Func<CancellationToken, Task<LiveSessions>> sessions,
        MachineLog? log, Action<string, string>? move, CancellationToken ct)
    {
        var judge = DownloadJudge.Of(home, harnesses, config, sessions) with { KeepsRecent = false };
        var cleared = (await DownloadClearing.ClearAsync(home, [new DownloadAsked("tool", tool, version)], judge, log, move, ct, door: "tools")
            .ConfigureAwait(false)).Single();
        return new(cleared.Outcome, cleared.Version?.Keeps, cleared.Version?.By ?? []);
    }

    public Task<LiveSessions> RunningAsync(string home, CancellationToken ct) =>
        LiveSessions.ReadAsync(null, DaorisHome.Entry(home, HomeEntry.SessionPids), ct);
}

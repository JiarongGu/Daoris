using System.Diagnostics;
using System.Globalization;

namespace Daoris.Driver;

/// <summary>A channel install staged and verified, not yet the version (PUSH1f2e, the alone-path design §1.4).</summary>
/// <param name="Where">The managed folder the pin points at, which the staging becomes.</param>
/// <param name="Staging">The staging folder, <c>&lt;where&gt;.part-&lt;pid&gt;</c>, its <c>package</c> folder the install.</param>
/// <param name="Executable">The executable, relative to the package folder, <c>/</c>-separated.</param>
public sealed record ChannelStaged(string Where, string Staging, string Executable);

/// <summary>
/// The makers' own release channels this build installs a pin from (AGT2b; PUSH1f2e): Claude Code's release bucket
/// (<see cref="ClaudeReleaseDownloads"/>) and Codex's releases (<see cref="CodexReleaseDownloads"/>), the CLI's <c>channels.ts</c>
/// until it left. A pin is staged where no reader takes it and no other run meets it, <c>&lt;where&gt;.part-&lt;pid&gt;</c>, holding
/// nothing while it downloads, then published by one folder move after a look again at what is there (the alone-path design §1.4): a
/// terminal's pin publishes under the home's lease, which it waited for; the window's loop holds its home already.
/// </summary>
public static class ReleaseChannels
{
    /// <summary>Where a channel's install is staged, beside the folder it becomes, then its process's id.</summary>
    public const string Staging = ".part";

    /// <summary>The channels this build installs from.</summary>
    public static IReadOnlyList<string> Known { get; } = [ClaudeReleases.Channel, CodexReleases.Channel];

    /// <summary>Whether this build installs from <paramref name="channel"/>.</summary>
    public static bool Knows(string channel) => Known.Contains(channel, StringComparer.Ordinal);

    /// <summary>Refuse a version the channel cannot install verifiably, before anything is fetched.</summary>
    /// <exception cref="DriverException">It is not one exact release the channel installs; or the channel is not one this build knows.</exception>
    public static void RefuseVersion(string channel, string version)
    {
        switch (channel)
        {
            case ClaudeReleases.Channel:
                ClaudeReleases.RefuseVersion(version);
                break;
            case CodexReleases.Channel:
                CodexReleases.RefuseVersion(version);
                break;
            default:
                throw Unknown(channel);
        }
    }

    /// <summary>The exact version a channel names as its newest release (USE1a), what an update pins.</summary>
    public static Task<string> LatestAsync(string channel, CancellationToken ct, HttpMessageHandler? transport = null) => channel switch
    {
        ClaudeReleases.Channel => ClaudeReleaseDownloads.LatestAsync(ct, transport),
        CodexReleases.Channel => CodexReleaseDownloads.LatestAsync(ct, transport),
        _ => throw Unknown(channel),
    };

    /// <summary>
    /// A pin's first half: fetched and verified into its own staging, <c>&lt;where&gt;.part-&lt;pid&gt;</c>. Its start sweeps the
    /// stagings beside it whose process no longer runs, and an older build's bare <c>.part</c> of this version. A refusal or a stop
    /// removes the staging.
    /// </summary>
    /// <param name="platform">The machine's build in the channel's own names: this one, unless a test names another.</param>
    /// <param name="trust">Who signs Claude Code's manifests: the vendor's pinned key, unless a test holds its own.</param>
    /// <param name="process">The process the staging is named by: this one, unless a test names two runs in one process.</param>
    /// <param name="running">Whether a process of that id runs here: the sweep's look, or a test's stand-in.</param>
    public static async Task<ChannelStaged> StageAsync(
        string channel, string where, string version, Action<string> write, CancellationToken ct,
        HttpMessageHandler? transport = null, string? platform = null, (string Key, string Fingerprint)? trust = null,
        int? process = null, Func<int, bool>? running = null)
    {
        RefuseVersion(channel, version);
        Sweep(where, running ?? Running);
        var staging = StagingOf(where, process ?? Environment.ProcessId);
        var kept = false;
        try
        {
            if (Directory.Exists(where + Staging)) Directory.Delete(where + Staging, recursive: true);
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
            Directory.CreateDirectory(Path.Combine(staging, "package"));
            var executable = channel == ClaudeReleases.Channel
                ? await ClaudeReleaseDownloads.StageAsync(staging, version, write, ct, transport, platform, trust).ConfigureAwait(false)
                : await CodexReleaseDownloads.StageAsync(staging, version, write, ct, transport, platform).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            kept = true;
            return new ChannelStaged(where, staging, executable);
        }
        finally
        {
            if (!kept) Discard(staging);
        }
    }

    /// <summary>
    /// A pin's second half: the staging made the version by one folder move, after a look again at what is there now. An install another
    /// run published meanwhile is kept and this run's staging removed; a folder holding nothing that runs, judged now, is replaced.
    /// </summary>
    /// <returns>The executable, and whether this run's staging became the version.</returns>
    public static (string Exe, bool Published) Publish(ChannelStaged staged, Action<string> write)
    {
        var exe = Path.Combine([staged.Where, .. staged.Executable.Split('/')]);
        try
        {
            if (File.Exists(exe))
            {
                write($"another run installed {Path.GetFileName(staged.Where)} meanwhile: it is kept, and this run's copy is removed.");
                return (exe, false);
            }

            if (Directory.Exists(staged.Where))
            {
                // Daoris's own directory, holding nothing that runs (asked just now): a download that stopped, or an npm install that did
                // not finish.
                write($"replacing {staged.Where}, which held nothing that runs");
                Directory.Delete(staged.Where, recursive: true);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(staged.Where)!);
            // The scanner opens the executables just unpacked, and the folder cannot move until it lets go (FIX-LOG 2026-10-07).
            AtomicFile.MoveFolder(Path.Combine(staged.Staging, "package"), staged.Where);
            return (exe, true);
        }
        finally
        {
            Discard(staged.Staging);
        }
    }

    /// <summary>A staging this run made and will not publish, removed (the person's stop while it waited to publish).</summary>
    public static void Discard(ChannelStaged staged) => Discard(staged.Staging);

    /// <summary>Where a run stages a version: <c>&lt;where&gt;.part-&lt;pid&gt;</c>, its own process's.</summary>
    public static string StagingOf(string where, int process) => $"{where}{Staging}-{process.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>
    /// The stagings beside <paramref name="where"/> whose process no longer runs, removed: a crash between a staging and its publication
    /// leaves nothing for good. A process id the system gave another process since keeps its staging only until that process ends.
    /// </summary>
    public static void Sweep(string where, Func<int, bool> running)
    {
        var parent = Path.GetDirectoryName(where);
        if (parent is null || !Directory.Exists(parent)) return;
        foreach (var folder in Directory.EnumerateDirectories(parent))
        {
            var name = Path.GetFileName(folder);
            var at = name.LastIndexOf(Staging + "-", StringComparison.Ordinal);
            if (at <= 0 || !int.TryParse(name.AsSpan(at + Staging.Length + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var process)
                || running(process))
            {
                continue;
            }

            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Held this moment: the next run's sweep takes it.
            }
        }
    }

    private static void Discard(string staging)
    {
        try
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Held this moment: the next run's sweep takes it, its process gone.
        }
    }

    private static bool Running(int process)
    {
        try
        {
            using var found = Process.GetProcessById(process);
            return !found.HasExited;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }

    private static DriverException Unknown(string channel) =>
        new($"this build installs from no `{channel}` channel, so nothing was fetched or pinned.");
}

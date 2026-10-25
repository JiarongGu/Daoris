namespace Daoris.Driver;

/// <summary>What one managed tool's downloaded version came to when the delete asked for it to go (<see cref="IToolDownloads.ClearAsync"/>).</summary>
/// <param name="Outcome"><c>removed</c>, <c>kept</c>, <c>gone</c> or <c>unfinished</c>, as the clearing says it of the one row asked.</param>
/// <param name="Keeps">Where the version was kept: why, as the clearing judged it right before it would have gone (<c>used</c>, <c>running</c>, <c>named</c>, <c>unread</c>); null where nothing kept it.</param>
/// <param name="By">What kept it: a session's id, the file that names it, or the sessions that could not be read.</param>
public sealed record ToolVersionCleared(string Outcome, string? Keeps, IReadOnlyList<string> By);

/// <summary>
/// What the tools' delete of a downloaded version asks of the workplace's download clearing, and what the verb asks of it for the sessions
/// that run (FOUNDATION1 mod-v, the design §14.3: the lower declares a port, the higher implements it): the install judges whether the
/// version may go by its own rules and asks the clearing to take it, then says what the clearing answered in its own words.
/// </summary>
public interface IToolDownloads
{
    /// <summary>
    /// Clear one managed tool's downloaded version under the clearing's judgement (KEEP1b), never keeping one for being recent: the use files
    /// read at <paramref name="harnesses"/> and <paramref name="config"/> as they stand, the running sessions as <paramref name="sessions"/>
    /// reads them right before the judgement, the move asked of <paramref name="move"/> where a test holds its own.
    /// </summary>
    Task<ToolVersionCleared> ClearAsync(
        string home, string tool, string version, string harnesses, string config, Func<CancellationToken, Task<LiveSessions>> sessions,
        MachineLog? log, Action<string, string>? move, CancellationToken ct);

    /// <summary>The sessions running on <paramref name="home"/> as its process markers say, with no Daoris to ask (the terminal verb's, alone).</summary>
    Task<LiveSessions> RunningAsync(string home, CancellationToken ct);
}

/// <summary>
/// The download clearing in force in this process: the workplace's, installed when the driver's assembly loads (<c>Trees/InstallWiring.cs</c>),
/// so every host and test that deletes a downloaded version clears it as it always has. A test that wants another sets it and puts it back.
/// </summary>
public static class ToolDownloads
{
    private static IToolDownloads? _current;

    /// <summary>The clearing in force.</summary>
    /// <exception cref="InvalidOperationException">None was installed: the assembly's own initializer has not run.</exception>
    public static IToolDownloads Current
    {
        get => _current ?? throw new InvalidOperationException("no download clearing is installed: the workplace installs it when the driver loads");
        set => _current = value;
    }
}

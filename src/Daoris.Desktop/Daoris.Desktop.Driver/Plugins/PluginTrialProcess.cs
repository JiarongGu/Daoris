using System.ComponentModel;
using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// A plugin's program as the kit's trial starts it (PLUG8, D101; STRUCT3a): from the driver's own
/// <see cref="HookProcess.StartInfo"/>, so it starts exactly as the driver starts it, and owned here only so the trial
/// sees what the driver does not need to: its exit code, its stdout whole, whether it left when told.
/// </summary>
internal sealed class PluginTrialProcess : IDisposable
{
    private readonly Process _process;

    private PluginTrialProcess(Process process) => _process = process;

    /// <summary>The plugin's program started in the home it is told.</summary>
    /// <param name="noRepositoryAt">
    /// Where git is told a repository is not, for the sample frames, which name none: git walks UP, so a folder with no
    /// repository of its own answers for the one above it. Null with the person's own frame, whose root is theirs.
    /// </param>
    /// <exception cref="DriverException">Its first word's tool cannot run as the person chose: the driver would refuse it too.</exception>
    /// <exception cref="Win32Exception">The program did not start.</exception>
    /// <remarks>
    /// Through the start gate (PROCSTART2): the plugin page's trial is a press, so a person's start (PARALLEL1d), and the
    /// trial's token ends its wait for a place (PARALLEL1e). Through the launcher where one is laid out beside the home (TERMTRUST1a).
    /// </remarks>
    /// <param name="world">
    /// The kit's stand-in world (FABRIC-PERFORM1e, <see cref="PluginCycles.World"/>): a file answering in a working plugin's platform's place,
    /// so a trial's attempt reaches nothing. Null where nothing it is sent can reach a platform's work.
    /// </param>
    public static async Task<PluginTrialProcess> StartAsync(PluginEntry entry, string home, string? noRepositoryAt, CancellationToken ct, string? world = null)
    {
        var info = HookProcess.StartInfo(entry, home);
        if (noRepositoryAt is not null)
        {
            info.Environment["GIT_DIR"] = Path.Combine(noRepositoryAt, ".git");
            info.Environment["GIT_TERMINAL_PROMPT"] = "0";
        }

        if (world is not null) info.Environment[PluginCycles.World] = world;

        // Through the launcher (TERMTRUST1a, D201 point 4), as the driver starts the plugin: born in this process's agents' job.
        return new(await AgentStarts.StartAsync(info, ContainedKind.Plugin, StartClass.Person, home, log: null, ct).ConfigureAwait(false)
            ?? throw new Win32Exception("the process did not start"));
    }

    /// <summary>Its stdout: the wire.</summary>
    public TextReader Output => _process.StandardOutput;

    /// <summary>Its stdin: the wire's other way.</summary>
    public TextWriter Input => _process.StandardInput;

    /// <summary>Its stderr: what it says, shown under its name.</summary>
    public TextReader Error => _process.StandardError;

    public bool HasExited => _process.HasExited;

    public int ExitCode => _process.ExitCode;

    /// <summary>Whether it exited within <paramref name="within"/>.</summary>
    public async Task<bool> ExitedAsync(TimeSpan within)
    {
        using var wait = new CancellationTokenSource(within);
        try
        {
            await _process.WaitForExitAsync(wait.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return _process.HasExited;
        }
    }

    /// <summary>Ended with all it started, as the driver ends a plugin that stays.</summary>
    public void Kill()
    {
        try
        {
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit(2000);
        }
        catch (Exception error) when (error is InvalidOperationException or Win32Exception)
        {
            // Already gone, which is the state being asked for.
        }
    }

    public void Dispose() => _process.Dispose();
}

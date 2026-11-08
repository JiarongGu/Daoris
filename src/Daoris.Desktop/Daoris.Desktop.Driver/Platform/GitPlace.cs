using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// The place a running git holds in <see cref="GitScheduler"/>: the flow it runs in carries it, so the start gate and
/// <see cref="HeldChild"/> start a git's image only inside one, and the place is freed when the processes started in it exit, never
/// when their runner returns.
/// </summary>
internal sealed class GitPlace
{
    private static readonly AsyncLocal<GitPlace?> s_current = new();

    private readonly object _gate = new();
    private readonly List<(Func<bool> Exited, Func<Task> Exit)> _exits = [];

    internal GitPlace(StartClass whose, GitWait wait, int depth)
    {
        Whose = whose;
        Wait = wait;
        Depth = depth;
    }

    /// <summary>The place this flow's git runs in; null outside the scheduler, where no git may start.</summary>
    internal static GitPlace? Current => s_current.Value;

    /// <summary>Whose the git is, for its start at the gate: the highest class among its askers at its placing.</summary>
    internal StartClass Whose { get; }

    /// <summary>What its run waits on: a place of an outside-waiting git lends one to the gits its hooks ask.</summary>
    internal GitWait Wait { get; }

    /// <summary>0 for a run place; one more than its parent's for a place a parent lent.</summary>
    internal int Depth { get; }

    /// <summary>How many places it has lent that are held now (under the scheduler's lock).</summary>
    internal int Lent { get; set; }

    /// <summary>This flow's git runs in <paramref name="place"/> from here: set by the scheduler's own run of it, so it never reaches the asker.</summary>
    internal static void Enter(GitPlace place) => s_current.Value = place;

    /// <summary>A process started in this place: the place is held until it exits.</summary>
    internal void Bind(Process process)
    {
        int pid;
        try
        {
            pid = process.Id;
        }
        catch (InvalidOperationException)
        {
            return;
        }

        Bind(pid);
    }

    /// <summary>A process started in this place, by its id (a held git's, which the runtime did not start): opened apart, so the runner's dispose of its own handle leaves the place's.</summary>
    internal void Bind(int pid)
    {
        Process own;
        try
        {
            own = Process.GetProcessById(pid);
        }
        catch (ArgumentException)
        {
            return; // exited already
        }
        catch (InvalidOperationException)
        {
            return;
        }

        Bind(() => HasExited(own), () => own.WaitForExitAsync());
    }

    /// <summary>The test seam's binding: an exit read and awaited, with no process.</summary>
    internal void Bind(Func<bool> exited, Func<Task> exit)
    {
        lock (_gate) _exits.Add((exited, exit));
    }

    /// <summary>Whether every process started in the place has exited.</summary>
    internal bool Exited
    {
        get
        {
            lock (_gate) return _exits.All(each => each.Exited());
        }
    }

    /// <summary>The exit of every process started in the place.</summary>
    internal Task ExitedAsync()
    {
        lock (_gate) return Task.WhenAll(_exits.Select(each => each.Exit()));
    }

    private static bool HasExited(Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return true;
        }
    }

    /// <summary>
    /// Whether <paramref name="file"/> is a git's image: a file named <c>git</c> (Git for Windows' launcher and the git behind it
    /// are both), or a file the scheduler has started as git (Tools' git under another name).
    /// </summary>
    internal static bool IsGit(string? file)
    {
        if (string.IsNullOrEmpty(file)) return false;
        if (string.Equals(Path.GetFileNameWithoutExtension(file), "git", StringComparison.OrdinalIgnoreCase)) return true;
        lock (s_known) return s_known.Contains(file);
    }

    // The files the scheduler has started git from, by their whole path as handed.
    private static readonly HashSet<string> s_known = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A file a git start of the scheduler's names: a start of it outside the scheduler is refused from now on.</summary>
    internal static void Known(string file)
    {
        lock (s_known) s_known.Add(file);
    }

    /// <summary>
    /// The gate's refusal of a git's image started with no place of the scheduler's, for every purpose: said in the machine log as
    /// <c>git.start.outside</c> with its purpose and its caller, and thrown as a start the runtime refused, so the git fails as one that
    /// could not start. Inside a place, the process is bound to it.
    /// </summary>
    /// <exception cref="GitStartRefusedException">A git's image with no place.</exception>
    internal static void Admit(ProcessStartInfo info, ProcessPurpose purpose)
    {
        if (Current is not null || !IsGit(info.FileName)) return;
        var caller = new StackTrace(1, false).GetFrames()
            .Select(frame => frame.GetMethod()?.DeclaringType)
            .Select(type => type?.DeclaringType is { } outer && type.Name.Contains('<') ? outer : type)
            .FirstOrDefault(type => type is not null && type != typeof(GitPlace) && type != typeof(ProcessStarts) && type != typeof(HeldChild))
            ?.Name ?? "unknown";
        if (ProgramHome.Named is { } home)
        {
            using var log = new MachineLog(home, "driver");
            log.Warn("git.start.outside", ("purpose", purpose.ToString()), ("caller", caller));
        }

        throw new GitStartRefusedException(purpose, caller);
    }
}

/// <summary>A git's image started outside the scheduler: refused at the gate, as a start the runtime refused.</summary>
internal sealed class GitStartRefusedException(ProcessPurpose purpose, string caller)
    : InvalidOperationException($"git starts only through Daoris's git scheduler; a start of purpose {purpose} from {caller} had no place, so it was refused")
{
    public ProcessPurpose Purpose { get; } = purpose;

    public string Caller { get; } = caller;
}

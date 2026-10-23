namespace Daoris.Driver;

/// <summary>
/// Where a git that outlived its kill is said (GITAPP1a-iii, the git-app design P1.6, Codex R4-5): <c>git.unended</c> in the machine
/// log, with the node, the runner, the pid and the reason, never a path. A kill that failed once read as one that worked
/// (<see cref="ChildProcess.KillTree"/> swallows its refusal); each runner now awaits the exit after its kill and tells the seam
/// (<see cref="GitLaunch"/>) when it saw none.
/// </summary>
/// <remarks>Flow-scoped, as the seam's count is: the route opens it around its read, so a read's starts are said to its log alone.</remarks>
public static class GitUnended
{
    private static readonly AsyncLocal<MachineLog?> s_log = new();

    /// <summary>Say each git this flow's runners could not end to <paramref name="log"/>, until disposed.</summary>
    public static IDisposable SaidTo(MachineLog? log)
    {
        var before = s_log.Value;
        s_log.Value = log;
        return new Restore(() => s_log.Value = before);
    }

    internal static void Say(GitLaunched launched, int pid, string reason) =>
        s_log.Value?.Warn(
            "git.unended", ("node", launched.Node), ("runner", launched.Runner.ToString()), ("kind", launched.Kind.ToString()), ("pid", pid),
            ("reason", reason));

    private sealed class Restore(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }
}

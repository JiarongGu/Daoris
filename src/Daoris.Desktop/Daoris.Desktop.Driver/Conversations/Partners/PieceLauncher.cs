using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// The launcher every agent, plugin process and piece starts through (COAGENT1b, generalized by TERMTRUST1a; D199's note on how a partner
/// starts in its strict job, D201 point 4): a small program of Daoris's own, <c>daoris-piece-start.exe</c> in the install's <c>app/bin/</c>
/// beside the git doors (<c>Daoris.Desktop.PieceStart</c>). It joins the jobs it is named, in order (this Daoris process's agents' job,
/// <see cref="AgentJobs"/>, then a piece's, <see cref="PieceJobs.NameOf"/>, which nests beneath it), each made empty and held by the driver,
/// then a job of its own that ends what it holds when the launcher ends, and only then starts the harness as its child with the stdio it was
/// handed: no instruction of the harness runs outside them, and the doors read the launcher as they read any session's process. Where a job
/// cannot be joined it refuses, <see cref="Refused"/>, and starts nothing: a refused start is rejected, never made outside the jobs it named.
/// </summary>
/// <remarks>
/// <para>Its words: <c>--job &lt;name&gt; [--job &lt;name&gt;] -- &lt;harness&gt; [its arguments…]</c>, as <see cref="Arguments(IEnumerable{string}, string, IEnumerable{string})"/>
/// writes them. A name is the agents' job (<see cref="AgentsPrefix"/> and a process id, first and once) or a piece's (once); the harness is
/// named by its full path, and its environment and working folder are the launcher's own, which the driver set as it sets a harness's.</para>
/// <para><b>Its own job</b> (TERMTRUST1a): the driver's tracking would hold the launcher in a job of its own after the start (ORPHAN1's
/// <see cref="ProcessJob.Hold(Process)"/>), which races the launcher's joins: a launcher already held in a fresh job cannot then join the
/// agents' job, which other agents hold and which is not beneath it. So the tracking holds none over a start made through here
/// (<see cref="Carries"/>), and the launcher makes the job that ends the harness's tree with it, empty and beneath the jobs it joined.</para>
/// <para>The base library alone: the launcher compiles this in with <see cref="ProcessJob"/>, as the git doors compile theirs.</para>
/// </remarks>
public static class PieceLauncher
{
    /// <summary>The program's file in the install's <c>app/bin/</c>: a twin of <c>tools/desktop-publish.mjs</c>'s <c>PIECE_START</c>.</summary>
    public const string Program = "daoris-piece-start.exe";

    /// <summary>The exit of a start the launcher refused, the harness never started: its one stderr line says why.</summary>
    public const int Refused = 125;

    /// <summary>What every piece's job is named from.</summary>
    public const string JobPrefix = "Local\\daoris-piece-";

    /// <summary>
    /// What each Daoris process's agents' job is named from, a process id after it (TERMTRUST1a, D201 point 4): no home id, since the judge
    /// asks each job by its handle and a home not minted yet has none (Fable's F7).
    /// </summary>
    public const string AgentsPrefix = "Local\\daoris-agents-";

    private const string JobOption = "--job";

    /// <summary>The launcher's words for a start of <paramref name="harness"/> in <paramref name="job"/>.</summary>
    public static IReadOnlyList<string> Arguments(string job, string harness, IEnumerable<string> arguments) =>
        Arguments([job], harness, arguments);

    /// <summary>The launcher's words for a start of <paramref name="harness"/> in <paramref name="jobs"/>, joined in their order.</summary>
    public static IReadOnlyList<string> Arguments(IEnumerable<string> jobs, string harness, IEnumerable<string> arguments) =>
        [.. jobs.SelectMany(job => new[] { JobOption, job }), "--", harness, .. arguments];

    /// <summary>Whether <paramref name="name"/> is a Daoris process's agents' job: the prefix and a process id, nothing else.</summary>
    public static bool IsAgentsJob(string name) =>
        name.StartsWith(AgentsPrefix, StringComparison.Ordinal)
        && name.Length > AgentsPrefix.Length
        && name.AsSpan(AgentsPrefix.Length).IndexOfAnyExceptInRange('0', '9') < 0;

    /// <summary>Whether <paramref name="name"/> is a piece's job.</summary>
    public static bool IsPieceJob(string name) => name.StartsWith(JobPrefix, StringComparison.Ordinal) && name.Length > JobPrefix.Length;

    /// <summary>
    /// Whether <paramref name="info"/> is already a start through the launcher (TERMTRUST1a): a second pass leaves it as it is (a run tried
    /// again on the same start, SESSCTX1c), and the doors' tracking holds no job over it, which would race the launcher's joins.
    /// </summary>
    public static bool Carries(ProcessStartInfo info) =>
        string.Equals(Path.GetFileName(info.FileName), Program, StringComparison.OrdinalIgnoreCase)
        && info.ArgumentList.Count > 0 && info.ArgumentList[0] == JobOption;

    /// <summary>Its words read: the jobs in order, the harness and the harness's own arguments; or why they cannot be taken.</summary>
    public static ((IReadOnlyList<string> Jobs, string Harness, IReadOnlyList<string> Arguments)? Call, string? Problem) Read(IReadOnlyList<string> arguments)
    {
        const string Shape = "daoris-piece-start: takes --job <a job> [--job <a job>] -- <the harness> [its arguments], as the driver passes them.";
        var jobs = new List<string>();
        var at = 0;
        while (at + 1 < arguments.Count && arguments[at] == JobOption)
        {
            jobs.Add(arguments[at + 1]);
            at += 2;
        }

        if (jobs.Count == 0 || at + 1 >= arguments.Count || arguments[at] != "--") return (null, Shape);

        for (var index = 0; index < jobs.Count; index++)
        {
            var job = jobs[index];
            // The agents' job first, so a piece's nests beneath it: one already held in a piece's job cannot then join a job other agents hold.
            var taken = IsAgentsJob(job) ? index == 0 : IsPieceJob(job) && jobs.Take(index).All(before => !IsPieceJob(before));
            if (!taken)
            {
                return (null, $"daoris-piece-start: joins this Daoris's agents' job first and a piece's job beneath it, once each, and {job} is not one of them there.");
            }
        }

        var harness = arguments[at + 1];
        if (string.IsNullOrWhiteSpace(harness)) return (null, "daoris-piece-start: was given no harness to start.");
        return ((jobs, harness, arguments.Skip(at + 2).ToList()), null);
    }

    /// <summary>
    /// The launcher's run: its words read, each job joined in order through <paramref name="join"/>, then the harness started through
    /// <paramref name="start"/> and its exit answered. A refusal starts nothing.
    /// </summary>
    public static int Run(IReadOnlyList<string> arguments, TextWriter error, Func<string, bool> join, Func<ProcessStartInfo, TextWriter, int> start)
    {
        var (call, problem) = Read(arguments);
        if (call is not { } taken) return Refuse(error, problem!);
        foreach (var job in taken.Jobs)
        {
            if (!join(job))
            {
                var whose = IsAgentsJob(job) ? "this Daoris's agents' job" : "the piece's job";
                return Refuse(error, $"daoris-piece-start: could not join {whose} {job}, so the harness was not started.");
            }
        }

        // Not the tools' environment (TOOLS5): the launcher's own, which the driver built for the harness and handed the launcher whole.
        var info = new ProcessStartInfo(taken.Harness)
        {
            UseShellExecute = false,
            // inherits its console: the launcher is the agent's process to the doors, and the harness speaks its protocol over the stdio
            // the driver handed the launcher. A spawn with no stream redirected and no window is given a console of its own, which cuts
            // that stdio; the launcher's own console was made with no window, so the harness's has none either.
            CreateNoWindow = false,
        };
        foreach (var word in taken.Arguments) info.ArgumentList.Add(word);
        return start(info, error);
    }

    /// <summary>
    /// The harness started and waited for: its exit is the launcher's. First the launcher's own job (TERMTRUST1a), so whatever the harness
    /// starts ends as the launcher ends, its exit or its kill; best effort, since the jobs joined already contain the harness.
    /// </summary>
    public static int Start(ProcessStartInfo info, TextWriter error)
    {
        // A bare name is searched for in the launcher's working folder first, which is the session's tree: the driver hands the full path.
        if (!Path.IsPathFullyQualified(info.FileName))
        {
            return Refuse(error, $"daoris-piece-start: starts a harness named by its full path, and {info.FileName} is not one.");
        }

        // Held, never disposed: its handle closes as this process ends, which ends what the job still holds (kill-on-close).
        s_own ??= ProcessJob.Create();
        s_own?.JoinThis();
        try
        {
            using var process = Process.Start(info)!;
            process.WaitForExit();
            return process.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception failure)
        {
            return Refuse(error, $"daoris-piece-start: could not start {Path.GetFileName(info.FileName)} ({failure.Message}).");
        }
    }

    // The launcher's own job, for its life (TERMTRUST1a): a static, so nothing collects it before the process ends.
    private static ProcessJob? s_own;

    private static int Refuse(TextWriter error, string line)
    {
        error.WriteLine(line);
        error.Flush();
        return Refused;
    }
}

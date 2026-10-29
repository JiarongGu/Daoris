using System.Text;

namespace Daoris.Driver;

/// <summary>The runner's inputs (the integration design §5.9, D192's own two and the five integration adds): set on the runner's process alone, which strips them from everything it starts.</summary>
internal static class RunnerInputs
{
    internal const string Commit = "DAORIS_VERIFY_COMMIT";
    internal const string Result = "DAORIS_VERIFY_RESULT";
    internal const string Mode = "DAORIS_VERIFY_MODE";
    internal const string Base = "DAORIS_VERIFY_BASE";
    internal const string Declaration = "DAORIS_VERIFY_DECLARATION";
    internal const string Gates = "DAORIS_VERIFY_GATES";
    internal const string Failure = "DAORIS_VERIFY_FAILURE";
    internal const string Tree = "DAORIS_VERIFY_TREE";

    /// <summary>The modes a call asks: <c>run</c>, <c>plan</c>, <c>probe</c> or <c>commits</c>.</summary>
    internal static readonly IReadOnlyList<string> Modes = ["run", "plan", "probe", "commits"];

    /// <summary>The exit that says the runner's own run holds its lock (D192): the step waits it out.</summary>
    internal const int BusyExit = 75;

    /// <summary>The exit of a refusal, which writes no answer.</summary>
    internal const int RefusedExit = 2;
}

/// <summary>One call of the runner: the mode, the commit the tree stands at, the candidate's base, the accepted blob and its file, and what only some modes take.</summary>
/// <param name="Commit">The full object id the tree stands at (the candidate's tip, or a prefix's).</param>
/// <param name="Declared">The gates the accepted declaration declares, which the answer is read against.</param>
internal sealed record RunnerCallAsk(string Mode, string Commit, string Base, string DeclarationFile, string DeclarationId, string Tree, IReadOnlyList<string> Declared)
{
    /// <summary><c>run</c> and <c>plan</c> only: the gates named; null for the runner's own plan.</summary>
    public IReadOnlyList<string>? Gates { get; init; }

    /// <summary><c>probe</c> only: the file naming the failure the probe repeats.</summary>
    public string? FailureFile { get; init; }
}

/// <summary>What a call came to: the answer read against what it asked, the exit, and the log its output went to.</summary>
internal sealed record RunnerCalled(RunnerRead Read, int? Exit, string Log);

/// <summary>
/// The caller of a repository's declared runner (INTEGRATE1f part f2; D192's <c>verification</c> runner, the integration design §5.9): its
/// command started in the candidate's job with its inputs set on that process only, through <see cref="GateEnvironment.Compose"/>, so
/// nothing else of the driver's environment reaches it, and the answer read from the file it was handed, never from its output.
/// </summary>
/// <remarks>
/// <para><b>Exit 75 or <c>busy: true</c></b> is busy (the runner's own run holds its lock); <b>exit 2 with no answer</b> is a refusal, said in
/// its log's words; a missing, misshapen or mismatched answer is unknown, never a pass (<see cref="RunnerAnswers"/>). A stale answer file is
/// removed before the call, so one left by an earlier run is never read for this one. A drop ends the candidate's job and with it the call.</para>
/// <para>The caller takes no place in the machine's admission itself: the judging takes it around the call, and hands the call the hold it runs
/// inside (<see cref="GateEnvironment.HoldVariable"/>), so a heavy run the runner starts asks no second place.</para>
/// </remarks>
internal sealed class RunnerCaller(CandidateJob job, VerdictRunner runner, IReadOnlyList<KeyValuePair<string, string>> start, IReadOnlyList<string> pass, string scratch)
{
    private int _calls;

    /// <summary>
    /// The runner's environment (§5.5): the gate environment composed from <paramref name="start"/> (every Daoris name, git's locating
    /// variables and the tool's switches removed, the build servers of the gate's own and the declaration's <c>env.pass</c> names added, the
    /// hold marker where the call runs inside a hold), then its own inputs. Nothing else of this process's environment is in it.
    /// </summary>
    internal static IReadOnlyList<KeyValuePair<string, string>> EnvironmentOf(
        IEnumerable<KeyValuePair<string, string>> start, IEnumerable<string>? pass, string? hold, RunnerCallAsk ask, string result)
    {
        var composed = GateEnvironment.Compose(start, pass, hold).ToList();
        void Put(string name, string value)
        {
            var at = composed.FindIndex(each => string.Equals(each.Key, name, StringComparison.OrdinalIgnoreCase));
            if (at >= 0) composed[at] = KeyValuePair.Create(name, value);
            else composed.Add(KeyValuePair.Create(name, value));
        }

        Put(RunnerInputs.Commit, ask.Commit.ToLowerInvariant());
        Put(RunnerInputs.Result, result);
        Put(RunnerInputs.Mode, ask.Mode);
        Put(RunnerInputs.Base, ask.Base.ToLowerInvariant());
        Put(RunnerInputs.Declaration, ask.DeclarationFile);
        Put(RunnerInputs.Tree, ask.Tree);
        if (ask.Mode is "run" or "plan" && ask.Gates is { } gates) Put(RunnerInputs.Gates, string.Join(',', gates));
        if (ask.Mode == "probe" && ask.FailureFile is { } failure) Put(RunnerInputs.Failure, failure);
        return composed;
    }

    /// <summary>Call the runner once and read what it answered. Never throws for a runner that fails, refuses or writes nothing: those are answers.</summary>
    /// <param name="hold">The id of the admission hold the call runs inside; null where it runs in none.</param>
    public async Task<RunnerCalled> CallAsync(RunnerCallAsk ask, string? hold, CancellationToken drop)
    {
        var number = Interlocked.Increment(ref _calls);
        var stem = Path.Combine(scratch, $"runner-{number:D3}-{ask.Mode}");
        var answer = stem + ".answer.json";
        var log = stem + ".log";
        Directory.CreateDirectory(scratch);
        File.Delete(answer);
        var asked = new RunnerAsk(ask.Commit, ask.Declared) { Mode = ask.Mode, Base = ask.Base, Declaration = ask.DeclarationId, Gates = ask.Gates };
        var environment = EnvironmentOf(start, pass, hold, ask, answer).ToDictionary(each => each.Key, each => each.Value, StringComparer.OrdinalIgnoreCase);
        var directory = runner.Cwd is { Length: > 0 } relative ? Path.GetFullPath(Path.Combine(ask.Tree, relative)) : ask.Tree;
        if (drop.IsCancellationRequested) throw new OperationCanceledException(drop);

        int exit;
        HeavyStep step;
        try
        {
            step = HeavyStep.Start(job.Job, runner.Run, directory, new Dictionary<string, string>(), new StepOptions(environment, log));
        }
        catch (DriverException error)
        {
            return new RunnerCalled(new RunnerRead(RunnerKind.Unknown) { Why = $"the runner would not start: {error.Message}" }, null, log);
        }

        using (step)
        using (drop.Register(() =>
        {
            // A drop ends the candidate's job, and the call with it.
            job.Job?.Terminate();
            step.Stop();
        }))
        {
            exit = await step.ExitAsync(CancellationToken.None).ConfigureAwait(false);
        }

        if (drop.IsCancellationRequested) throw new OperationCanceledException(drop);
        return new RunnerCalled(Interpret(exit, ReadAnswer(answer), asked, log), exit, log);
    }

    /// <summary>The answer file's text; null where it is not there (or not readable), which the answers read as unknown.</summary>
    private static string? ReadAnswer(string answer)
    {
        try
        {
            return File.Exists(answer) ? File.ReadAllText(answer, new UTF8Encoding(false)) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// The answer read against what the call asked, with the exit: 75 is busy; 2 with no answer is a refusal (said in the log's last words);
    /// anything else is what <see cref="RunnerAnswers.Read(string?, RunnerAsk)"/> makes of the file.
    /// </summary>
    internal static RunnerRead Interpret(int exit, string? text, RunnerAsk asked, string log)
    {
        if (exit == RunnerInputs.BusyExit) return new RunnerRead(RunnerKind.Busy);
        if (text is null && exit == RunnerInputs.RefusedExit)
        {
            return new RunnerRead(RunnerKind.Unknown) { Why = $"the runner refused (exit 2) and wrote no answer: {Tail(log)}" };
        }

        return RunnerAnswers.Read(text, asked);
    }

    /// <summary>The last of a log's words, for a refusal: the runner says there why it refused.</summary>
    private static string Tail(string log)
    {
        try
        {
            var lines = File.ReadAllLines(log).Select(line => line.Trim()).Where(line => line.Length > 0).TakeLast(3);
            var text = string.Join(" / ", lines);
            return text.Length == 0 ? "it said nothing" : text.Length > 600 ? text[^600..] : text;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return "its log could not be read";
        }
    }
}

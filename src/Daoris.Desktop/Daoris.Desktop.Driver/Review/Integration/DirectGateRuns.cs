using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The gates' runs of a repository that declares no runner (INTEGRATE1f part f2; the integration design §5.9 last paragraph): each declared
/// gate's own <c>run</c> command, whole, as a step in the integration tree, in the candidate's job, composed with the gate environment, its
/// exit its verdict; a probe being the gate run again. A failure names no test (the output is not read for tests), so it is signed by its
/// step, and the merge tool's rule for a gate that names no test judges it. The caller is its own runner here, so the composition digest is
/// built by <see cref="GateComposition"/>, the twin of the tool's.
/// </summary>
/// <remarks>The commands and the gates are the accepted blob's, never the tree's. A heavy gate takes a place in the admission, and a light one none.</remarks>
internal sealed class DirectGateRuns(IntegrationVerdictAsk ask, CandidateJob job, Places places, IntegrationWorld world) : IGateRuns
{
    private int _runs;

    public async Task<GatesRun> RunAsync(string commit, string? hold, IReadOnlyList<string>? only)
    {
        var declared = DeclaredGates.Read(ask.Declaration);
        var named = only ?? ask.Gates;
        IReadOnlyList<DeclaredGate> chosen;
        if (named is null)
        {
            chosen = declared;
        }
        else
        {
            var unknown = named.FirstOrDefault(name => declared.All(gate => gate.Name != name));
            if (unknown is not null) return new GatesRun(RunnerStandingKind.Unknown, $"the gate '{unknown}' is not declared by the accepted declaration");
            chosen = [.. named.Select(name => declared.First(gate => gate.Name == name))];
        }

        if (chosen.Count == 0) return new GatesRun(RunnerStandingKind.Unknown, "the accepted declaration declares no gate to run: nothing is shown");
        var runs = new List<VerdictRun>();
        var failures = new List<GateFailure>();
        var compositions = new Dictionary<string, RunnerComposition>();
        foreach (var gate in chosen)
        {
            var (code, log) = gate.Heavy
                ? await places.PlacedAsync($"integration gate `{gate.Name}` on {commit[..7]}", hold, inside => RunOneAsync(gate, inside)).ConfigureAwait(false)
                : await RunOneAsync(gate, hold).ConfigureAwait(false);
            var composition = Composition(gate);
            compositions[gate.Name] = new RunnerComposition(null, null, composition.Digest);
            runs.Add(new VerdictRun("run", gate.Name, code == 0 ? RunnerVerdicts.Pass : RunnerVerdicts.Fail, composition.Digest) { Exit = code, Log = log });
            if (code != 0)
            {
                var output = ReadLog(log);
                failures.Add(new GateFailure(gate.Name, new RunnerFailure("", gate.Name, FailureReader.GateSignature(gate.Name, "run", output, ask.Tree))));
            }
        }

        return new GatesRun(failures.Count > 0 ? RunnerStandingKind.Failed : RunnerStandingKind.Reached, failures.Count > 0 ? $"the gate '{failures[0].Gate}' failed" : null)
        {
            Failures = failures, Runs = runs, Compositions = compositions,
        };
    }

    public async Task<ProbeAnswer> ProbeAsync(GateFailure failure, RunnerComposition? composition, string commit, string? hold)
    {
        var again = await RunAsync(commit, hold, [failure.Gate]).ConfigureAwait(false);
        return again.Kind switch
        {
            RunnerStandingKind.Reached => new ProbeAnswer("pass") { Run = again.Runs.FirstOrDefault() },
            RunnerStandingKind.Failed => new ProbeAnswer("fail", again.Failures[0].Failure.Signature) { Run = again.Runs.FirstOrDefault() },
            _ => new ProbeAnswer("unknown", Why: again.Why),
        };
    }

    /// <summary>One gate's command through the platform's shell in the gate's folder, composed as a gate is; its exit and the log its output went to.</summary>
    private async Task<(int Code, string Log)> RunOneAsync(DeclaredGate gate, string? inside)
    {
        var log = Path.Combine(ask.Scratch, $"gate-{Interlocked.Increment(ref _runs):D3}-{Slug(gate.Name)}.log");
        Directory.CreateDirectory(ask.Scratch);
        var environment = GateEnvironment.Compose(world.Start, ask.Pass, inside).ToDictionary(each => each.Key, each => each.Value, StringComparer.OrdinalIgnoreCase);
        var directory = gate.Cwd is { Length: > 0 } relative ? Path.GetFullPath(Path.Combine(ask.Tree, relative)) : ask.Tree;
        if (ask.Drop.IsCancellationRequested) throw new OperationCanceledException(ask.Drop);
        HeavyStep step;
        try
        {
            step = HeavyStep.Start(job.Job, gate.Run, directory, new Dictionary<string, string>(), new StepOptions(environment, log));
        }
        catch (DriverException error)
        {
            await File.WriteAllTextAsync(log, $"error: the gate would not start: {error.Message}\n", new UTF8Encoding(false)).ConfigureAwait(false);
            return (-1, log);
        }

        int code;
        using (step)
        using (ask.Drop.Register(() =>
        {
            job.Job?.Terminate();
            step.Stop();
        }))
        {
            code = await step.ExitAsync(CancellationToken.None).ConfigureAwait(false);
        }

        if (ask.Drop.IsCancellationRequested) throw new OperationCanceledException(ask.Drop);
        return (code, log);
    }

    /// <summary>A gate's composition when the caller is its own runner: the gate as the declaration writes it, no units, no narrowing, the environment's rule.</summary>
    private static GateComposition Composition(DeclaredGate gate) =>
        GateComposition.Of(gate.Name, gate.Run, gate.Cwd, null, [], null, GateEnvironment.Rule());

    private static string Slug(string name) => new([.. name.Select(letter => char.IsAsciiLetterOrDigit(letter) ? letter : '-')]);

    private static string ReadLog(string log)
    {
        try
        {
            return File.Exists(log) ? File.ReadAllText(log) : "";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return "";
        }
    }
}

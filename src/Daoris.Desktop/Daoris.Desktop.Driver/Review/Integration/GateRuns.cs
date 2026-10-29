namespace Daoris.Driver;

/// <summary>
/// What the integration step's runs are made with (INTEGRATE1f part f2): the environment they are composed from, the machine admission's
/// seams and the words said while they wait. The install's own by default; a test hands its own.
/// </summary>
internal sealed record IntegrationWorld
{
    /// <summary>The environment every gate and runner is composed from: the install's own, never a shell a person or the steward holds.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Start { get; init; } = Own();

    public AdmissionWaiting Waiting { get; init; } = AdmissionWaiting.Default;

    /// <summary>How many places the machine admits, where not this machine's (<see cref="MachineAdmission.Capacity"/>).</summary>
    public int? Capacity { get; init; }

    public Func<string, string?> Variable { get; init; } = Environment.GetEnvironmentVariable;

    /// <summary>Where a wait for the admission is said.</summary>
    public TextWriter Said { get; init; } = TextWriter.Null;

    /// <summary>How many of a gate's failures its confirm run repeats; the rest are not shown, so they read inconclusive and split (never land).</summary>
    public int MaxRepeats { get; init; } = 5;

    private static IReadOnlyList<KeyValuePair<string, string>> Own() =>
        [.. System.Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
            .Where(entry => entry.Key is string && entry.Value is string)
            .Select(entry => KeyValuePair.Create((string)entry.Key, (string)entry.Value!))];
}

/// <summary>What a run of the gates came to: how the run stands (reached, failed, unknown, busy), and the failures, the flakes read as passes, every run and the compositions the runner kept.</summary>
internal sealed record GatesRun(RunnerStandingKind Kind, string? Why = null)
{
    public IReadOnlyList<GateFailure> Failures { get; init; } = [];

    /// <summary>The gates that passed only on a second run alone (a FLAKE1 re-run): read as a pass, and said.</summary>
    public IReadOnlyList<string> Flaked { get; init; } = [];

    public IReadOnlyList<VerdictRun> Runs { get; init; } = [];

    /// <summary>The gates the runner's plan kept, on the standing it states.</summary>
    public IReadOnlyList<RunnerPlanEntry> Kept { get; init; } = [];

    /// <summary>Each gate's composition as the run kept it, by the declared name; a gate that kept none is not here.</summary>
    public IReadOnlyDictionary<string, RunnerComposition> Compositions { get; init; } = new Dictionary<string, RunnerComposition>();
}

/// <summary>A probe's answer: <c>pass</c>, <c>fail</c> or <c>cannot</c> (with the signature it ended with), or <c>unknown</c> or <c>busy</c> where no answer can be read.</summary>
internal sealed record ProbeAnswer(string Result, FailureSignature? Signature = null, string? Why = null)
{
    public VerdictRun? Run { get; init; }
}

/// <summary>
/// The gates' runs at a commit, whichever way they are made (the runner's contract, or each gate whole): the run at a commit the tree
/// stands at, and the repetition of one failure there. Every process runs in the candidate's job, and a place in the machine's admission
/// is held only while its call runs and is let go after the job is ended.
/// </summary>
internal interface IGateRuns
{
    /// <summary>The gates run at <paramref name="commit"/>. <paramref name="only"/> narrows them (the confirm run of a gate that kept no composition); null for the asked gates.</summary>
    /// <param name="hold">The hold the run is inside (the confirm run's whole admission); null where the run takes a place of its own.</param>
    Task<GatesRun> RunAsync(string commit, string? hold, IReadOnlyList<string>? only);

    /// <summary>One failure repeated at <paramref name="commit"/> as its gate composed it (<paramref name="composition"/>), or the gate whole where it kept none.</summary>
    Task<ProbeAnswer> ProbeAsync(GateFailure failure, RunnerComposition? composition, string commit, string? hold);
}

/// <summary>
/// A place in the machine's admission around one call (the integration design §4.5, §5.6): taken in the order the queue asks, and the
/// candidate's job ended <b>before</b> it is let go. A call inside a hold (the confirm run's whole admission) takes none.
/// </summary>
internal sealed class Places(IntegrationVerdictAsk ask, CandidateJob job, IntegrationWorld world)
{
    /// <summary>Run <paramref name="body"/> holding a place, handing it the hold's id; the job's processes are ended and waited for before the place goes.</summary>
    public async Task<T> PlacedAsync<T>(string what, string? hold, Func<string?, Task<T>> body)
    {
        if (hold is not null) return await body(hold).ConfigureAwait(false);
        AdmissionHold? place;
        try
        {
            place = await MachineAdmission.TakeAsync(
                ask.Home, new AdmissionRequest(what) { Kind = "queue", Repository = ask.Candidate.Repository, Tree = ask.Tree }, world.Said, ask.Drop,
                world.Waiting, world.Capacity, world.Variable).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A record that cannot be kept costs the run its place and nothing else, as a heavy gate waiting for it does.
            world.Said.WriteLine($"integration: the machine's admission could not be kept ({error.Message}); {what} goes on without a place in it.");
            place = null;
        }

        using (place)
        {
            try
            {
                return await body(place?.Id).ConfigureAwait(false);
            }
            finally
            {
                await job.EndLeftAsync().ConfigureAwait(false);
            }
        }
    }
}

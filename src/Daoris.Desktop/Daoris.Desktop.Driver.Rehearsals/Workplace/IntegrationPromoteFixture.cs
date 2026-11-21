using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>What the promotion's tests over real git share: a store, a repository with a candidate formed and replayed on its line, and the verdict port as a test sets it.</summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public abstract class IntegrationPromoteFixture : LandedFixture
{
    private protected IntegrationStore Store(string run, Func<StoreRun, bool>? alive = null) => new(Home, () => StoreSettled.Yes, alive, run, StoreProcess.Shell);

    /// <summary>The second process's store: run-b, which reads run-a as dead.</summary>
    private protected IntegrationStore Taker() => Store("run-b", run => run.Run != "run-a");

    private protected string TreesHome => DaorisHome.Entry(Home, HomeEntry.Trees);

    private protected const string Gates = """{ "gates": [ { "name": "unit", "run": "node check.mjs" } ] }""" + "\n";

    private protected const string Lines20 = "line 1\nline 2\nline 3\nline 4\nline 5\nline 6\nline 7\nline 8\nline 9\nline 10\nline 11\nline 12\nline 13\nline 14\nline 15\nline 16\nline 17\nline 18\nline 19\nline 20\n";

    private protected static string Lines(params (int Line, string Text)[] changes)
    {
        var lines = Enumerable.Range(1, 20).Select(each => $"line {each}").ToArray();
        foreach (var (line, text) in changes) lines[line - 1] = text;
        return string.Join("\n", lines) + "\n";
    }

    private protected static async Task<string> CommitOfAsync(string cwd, string reference) => (await GitAsync(cwd, "rev-parse", reference)).Trim();

    /// <summary>A branch from <c>main</c> with one commit for each file given, the checkout left on <c>main</c>; the branch's tip.</summary>
    private protected static async Task<string> BranchAsync(string root, string name, params (string File, string Content)[] commits)
    {
        await GitAsync(root, "checkout", "--quiet", "-b", name, "main");
        foreach (var (file, content) in commits) await CommitAsync(root, file, content, $"work on {name}: {file}");
        var tip = await CommitOfAsync(root, name);
        await GitAsync(root, "checkout", "--quiet", "main");
        return tip;
    }

    private protected static void Queue(IntegrationStore store, string id, string branch, string tip, int order, LandingIntegrate integrate, string? session = null, string? frozen = null) =>
        Assert.Null(store.Add(new IntegrationRequest(
            id, "engine", "aurora", branch, tip, IntegrationRoutes.Recorded, "main", IntegrationRequester.Card, integrate.Resolved(),
            new DateTimeOffset(2026, 10, 30, 9, 0, order, TimeSpan.Zero))
        {
            Session = session,
            Epoch = frozen is null ? null : IntegrationEpoch.Of(integrate.Resolved(), frozen),
        }));

    /// <summary>What a candidate formed and replayed on the line stands on: the requests are ra, rb, rc... in the order given.</summary>
    private protected sealed record Candidate(
        string Root, string Base, IntegrationStore Store, IntegrationClaim Claim, IntegrationPromoteAsk Ask, IntegrationReplayRun Run, string Blob)
    {
        public IntegrationCandidate Now => Store.Candidate(Claim.Candidate)!;

        public IntegrationMember Member(string request) => Store.Members(Claim.Candidate).Single(each => each.Request == request);

        public string Tip => Now.Tip!;
    }

    /// <summary>
    /// <c>engine</c> on <c>main</c>, with <paramref name="declaration"/> committed on it first where one is given (and accepted by the owner, so
    /// the gates may run), a branch for each member, and a candidate formed and replayed from their requests. The checkout is left detached
    /// where <paramref name="detached"/>, so no working tree has the line out, and on <c>main</c> otherwise.
    /// </summary>
    private protected async Task<Candidate> FormedAsync(
        (string Branch, (string File, string Content)[] Commits)[] members, string? declaration = Gates, LandingIntegrate? integrate = null,
        bool detached = true, bool accepted = true, Func<string, Task>? beforeFormed = null, bool frozen = false)
    {
        var root = await RepositoryAsync("engine");
        var blob = "";
        if (declaration is not null)
        {
            await CommitAsync(root, "daoris.gates.json", declaration, "the declaration");
            blob = (await GitAsync(root, "rev-parse", "HEAD:daoris.gates.json")).Trim();
        }

        if (beforeFormed is not null) await beforeFormed(root);
        var @base = await CommitOfAsync(root, "main");
        var store = Store("run-a");
        var policy = integrate ?? new LandingIntegrate(Batch: 8, Gates: LandingIntegrate.Declared);
        for (var at = 0; at < members.Length; at++)
        {
            var tip = await BranchAsync(root, members[at].Branch, members[at].Commits);
            Queue(store, $"r{(char)('a' + at)}", members[at].Branch, tip, at + 1, policy, frozen: frozen && declaration is not null ? blob : null);
        }

        var config = declaration is not null && accepted ? DriverConfig.Empty.WithIntegrationGates("engine", blob) : DriverConfig.Empty;
        var replays = new IntegrationReplays(store, TreesHome);
        var run = await replays.RunAsync(new IntegrationReplayAsk("engine", "aurora", root, "main") { Config = config });
        Assert.Equal(IntegrationReplayRuns.Replayed, run.Kind);
        if (detached) await GitAsync(root, "checkout", "--quiet", "--detach");
        return new Candidate(root, @base, store, run.Claim!, new IntegrationPromoteAsk("engine", "aurora", root, "main", config, Home), run, blob);
    }

    private protected static (string Branch, (string File, string Content)[] Commits) Member(string branch, params (string File, string Content)[] commits) => (branch, commits);

    private protected IntegrationPromotes Promotes(Candidate candidate) => new(candidate.Store, TreesHome);

    /// <summary>The verdict port as this class sets it: every ask kept, each answered by <paramref name="answer"/>.</summary>
    private protected sealed class Verdict(Func<IntegrationVerdictAsk, Task<IntegrationVerdict>> answer) : IIntegrationVerdicts
    {
        public List<IntegrationVerdictAsk> Asks { get; } = [];

        public async Task<IntegrationVerdict> JudgeAsync(IntegrationVerdictAsk ask)
        {
            Asks.Add(ask);
            return await answer(ask).ConfigureAwait(false);
        }
    }

    private protected static Verdict Judging(Func<IntegrationVerdictAsk, IntegrationVerdict> answer) => Judging(ask => Task.FromResult(answer(ask)));

    private protected static Verdict Judging(Func<IntegrationVerdictAsk, Task<IntegrationVerdict>> answer)
    {
        var verdict = new Verdict(answer);
        IntegrationVerdicts.Within = verdict;
        return verdict;
    }

    private protected static Verdict Green() => Judging(_ => new IntegrationVerdict(VerdictActions.Green));

    private protected static IReadOnlyList<(string Step, string? Outcome)> Written(Candidate candidate) =>
        [.. candidate.Store.Steps(candidate.Claim.Candidate).Select(each => (each.Step, each.Outcome))];

    private protected static IntegrationStep Last(Candidate candidate, string step) => candidate.Store.Steps(candidate.Claim.Candidate).Last(each => each.Step == step);
}

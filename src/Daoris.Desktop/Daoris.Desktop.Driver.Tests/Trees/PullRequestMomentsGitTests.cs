using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-END6 over real git (the counted half is <see cref="PullRequestMomentsTests"/>): Daoris's push asks once and fetches nothing
/// after its press's fetch; the platform squash-merges with the line moving on, so git alone cannot tell; the person's fetch moves
/// origin's line, the ref watch tells the moments, the plugin answers completed, and the repository's ends are judged again at once,
/// so the landing's end is held on the pull request's word with nobody pressing anything. The plugin is faked on its channel, the
/// ref watch on a fake source, and the asks' clock stands still while the test moves it.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class PullRequestMomentsGitTests : LandedFixture
{
    private const string Plugin = "acme.asks";

    private readonly Dictionary<string, Func<PullRequestState>> _answers = new(StringComparer.Ordinal);

    private int _asked;

    private DateTimeOffset _now = DateTimeOffset.UtcNow;

    public PullRequestMomentsGitTests()
    {
        var folder = DaorisHome.Entry(Home, HomeEntry.Plugins, Plugin);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"), JsonSerializer.Serialize(new
        {
            id = Plugin,
            hooks = new { command = new[] { "node", "plugin.mjs" }, points = new[] { HookPoints.Land, HookPoints.State } },
        }));
        DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin))
            .Save(DaorisHome.Entry(Home, HomeEntry.DriverConfig));
    }

    private SessionTrees Trees() => new(Home, new LandingPlugins(
        Home, start: (_, _, _) => Task.FromResult<IHookChannel>(new Platform(this)), clock: () => _now));

    [Fact]
    public async Task A_merged_pull_request_s_work_ends_with_no_press_once_origin_s_line_is_heard_moving()
    {
        using var lease = HomeLease.TryAcquire(Home, LeaseKind.Headless, out _);
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        IReadOnlyList<RepoView> registry = [new RepoView("engine", true, root, "aurora")];
        var trees = Trees();
        await using var moments = PullRequestMoments.ForDriver(Home, _ => Task.FromResult(registry), trees: trees, clock: () => _now);
        moments.Open();

        // Daoris's push (the person's Accept, whose press fetched the line first): asked once, the press's fetch counted.
        var landed = await LandAsync(trees, root, "a.txt", "the work\n", new LandingSubject("s1", "q1", "First"));
        Assert.True(landed.Plugin is { Pushed: true }, landed.Message);
        Assert.NotNull(RemoteFetches.When(root, "main"));
        await Step(moments);
        Assert.Equal(1, _asked);
        Assert.Equal(PullRequestStates.Open, trees.Recorded.Landing("s1")!.PullRequestState!.State);

        // The platform squash-merges and the line moves on over the same file, so git's own proofs cannot see the work landed.
        var source = (await GitAsync(root, "rev-parse", landed.Branch!)).Trim();
        var merge = await SquashOnPlatformAsync(origin, landed.Branch!, deleteBranch: false, then: ("a.txt", "changed on the line since\n"));
        _answers[landed.Branch!] = () => new PullRequestState(PullRequestStates.Completed)
        {
            PullRequest = "https://example.test/org/project/_git/engine/pullrequest/7",
            MergeCommit = merge,
            SourceCommit = source,
            Target = "main",
            How = MergeHow.Squash,
        };
        _now += TimeSpan.FromMinutes(2);

        // The ref watch over the repository, on a fake source: its judgement is told only what this test tells it.
        await using var refs = RefWatch.ForDriver(Home, _ => Task.FromResult(registry), source: new FakeChangeSource());
        refs.Open();
        await refs.RebuildAsync(CancellationToken.None);

        // The person's fetch moves origin's line. Judged by git alone, the work is not held.
        await GitAsync(root, "fetch", "--quiet", "origin");
        RefMoves.Mine(Home, "engine");
        await Step(refs);
        Assert.Null(trees.Recorded.Landing("s1")!.Held);

        // The ref watch told the moments: origin's line moved, so the plugin is asked, and nothing is fetched.
        await Step(moments);
        Assert.Equal(2, _asked);
        Assert.Equal(PullRequestStates.Completed, trees.Recorded.Landing("s1")!.PullRequestState!.State);

        // Its completed answer judged the repository again at once: the end is held on the pull request's word, with no press.
        await Step(refs);
        var held = trees.Recorded.Landing("s1")!.Held;
        Assert.NotNull(held);
        Assert.Equal((EndBy.PullRequest, source, "origin/main"), (held!.By, held.Tip, held.On));

        // The judgement's echo told the moments again: origin's line stands where it was, so nothing more is asked.
        await Step(moments);
        Assert.Equal(2, _asked);
    }

    private static async Task Step(PullRequestMoments moments)
    {
        moments.Changes.Pump();
        await moments.Changes.Settled();
    }

    private static async Task Step(RefWatch refs)
    {
        refs.Changes.Pump();
        await refs.Changes.Settled();
    }

    /// <summary>The fake platform: a landing's push is a real one into the bare origin; a state is what the test says for the branch.</summary>
    private sealed class Platform(PullRequestMomentsGitTests test) : IHookChannel
    {
        public IReadOnlyList<string> Points => [HookPoints.Land, HookPoints.State];
        public bool Alive => true;
        public Task<HookDecision> ConsiderAsync(object payload, CancellationToken ct) => Task.FromResult(HookDecision.Allow);
        public Task EndedAsync(object payload, CancellationToken ct) => Task.CompletedTask;

        public async Task<PluginLanding> LandAsync(object payload, CancellationToken ct)
        {
            var frame = JsonSerializer.SerializeToElement(payload);
            var branch = frame.GetProperty("branch").GetString()!;
            await GitAsync(frame.GetProperty("root").GetString()!, "push", "--quiet", "origin", branch);
            return new PluginLanding(Plugin, Pushed: true, "https://example.test/org/project/_git/engine/pullrequest/7", "pushed");
        }

        public Task<PullRequestState> StateAsync(object payload, CancellationToken ct)
        {
            test._asked++;
            var branch = JsonSerializer.SerializeToElement(payload).GetProperty("branch").GetString()!;
            return Task.FromResult(test._answers.TryGetValue(branch, out var answer) ? answer() : new PullRequestState(PullRequestStates.Open));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// PLUGHOOK1a (D148 point 2, design §2.1 and §2.4): an occasion's asks. One process per plugin, started for the frames and
/// stopped after them; frames one at a time, the entry asked longest ago first; each waits its bound, the handshake inside the
/// first; no frame starts once the occasion's bound has passed; and a failure is a code, never a state. Plugins faked on their
/// channel, so this is the suite's fast half.
/// </summary>
public sealed class PullRequestAskTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 14, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-pr-ask-" + Guid.NewGuid().ToString("N")[..8]);

    public PullRequestAskTests()
    {
        Install("acme.asks", [HookPoints.Land, HookPoints.State]);
        Install("acme.other", [HookPoints.Land, HookPoints.State]);
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static LandedBranch Entry(string branch, string session, DateTimeOffset? asked = null) =>
        new("engine", "aurora", branch, "main", new string('b', 40), session, "q1", "Fix", Now.AddDays(-1))
        {
            PullRequestState = asked is { } at ? new PullRequestState(PullRequestStates.Open) { AskedAt = at } : null,
        };

    private static StateAsk Ask(LandedBranch entry, string plugin = "acme.asks") =>
        new(entry, plugin, new StateFrame(entry.Repository, entry.Workspace, "D:/fam/engine", entry.Branch, "main", null, null));

    [Fact]
    public async Task One_process_per_plugin_asks_its_entries_one_at_a_time_the_one_asked_longest_ago_first()
    {
        var started = new List<string>();
        var told = new List<string>();
        var plugins = new LandingPlugins(_home, start: (plugin, _, _) =>
        {
            started.Add(plugin.Manifest.Id);
            return Task.FromResult<IHookChannel>(new Answering(told, branch => new PullRequestState(PullRequestStates.Open) { Plugin = plugin.Manifest.Id }));
        }, clock: () => Now);

        var asked = await plugins.AskStatesAsync([
            Ask(Entry("feature/recent", "s1", asked: Now.AddMinutes(-5))),
            Ask(Entry("feature/never", "s2")),
            Ask(Entry("feature/old", "s3", asked: Now.AddHours(-5)), "acme.other"),
        ]);

        Assert.Equal(["feature/never", "feature/old", "feature/recent"], told);
        Assert.Equal(["acme.asks", "acme.other"], started);
        Assert.All(asked, answer => Assert.Equal(PullRequestStates.Open, answer.Answer!.State));
        Assert.Equal(["feature/never", "feature/old", "feature/recent"], asked.Select(answer => answer.Ask.Entry.Branch));
    }

    /// <summary>
    /// Each way a plugin fails is its own code, and a plugin that would not start is not started again for its next entry. A
    /// channel that does not listen at the point it declares is not asked.
    /// </summary>
    [Fact]
    public async Task Each_failure_is_a_code_and_never_a_state()
    {
        Install("acme.deaf", [HookPoints.State]);
        Install("acme.broken", [HookPoints.State]);
        var starts = 0;
        var plugins = new LandingPlugins(_home, start: (plugin, _, _) => plugin.Manifest.Id switch
        {
            "acme.broken" => Task.FromException<IHookChannel>(PluginFailures.Mark(new DriverException("could not start"), PluginEvents.Unstartable))
                .ContinueWith(task => { starts++; return task; }).Unwrap(),
            "acme.deaf" => Task.FromResult<IHookChannel>(new Answering([], _ => new PullRequestState(PullRequestStates.Open), points: [HookPoints.Land])),
            _ => Task.FromResult<IHookChannel>(new Answering([], _ => throw PluginFailures.Mark(new DriverException("az: not signed in"), PluginEvents.Errored))),
        }, clock: () => Now);

        var asked = await plugins.AskStatesAsync([
            Ask(Entry("feature/a", "s1"), "acme.broken"),
            Ask(Entry("feature/b", "s2"), "acme.broken"),
            Ask(Entry("feature/c", "s3"), "acme.deaf"),
            Ask(Entry("feature/d", "s4")),
        ]);

        Assert.Equal(1, starts);
        Assert.Equal(
            [PluginEvents.Unstartable, PluginEvents.Unstartable, PluginEvents.Unreadable, PluginEvents.Errored],
            asked.Select(answer => answer.Failure));
        Assert.All(asked, answer => Assert.Null(answer.Answer));
        Assert.Contains("az: not signed in", asked[3].Sentence);
        Assert.Contains("does not listen", asked[2].Sentence);
    }

    /// <summary>A frame waits its bound and no longer; a late answer is <c>late</c>, and the next entry is still asked.</summary>
    [Fact]
    public async Task A_frame_that_outwaits_its_bound_is_late()
    {
        var calls = 0;
        var plugins = new LandingPlugins(_home, start: (_, _, _) => Task.FromResult<IHookChannel>(new Answering([], _ =>
            Interlocked.Increment(ref calls) == 1 ? null : new PullRequestState(PullRequestStates.Abandoned))),
            clock: () => Now, statePatience: TimeSpan.FromMilliseconds(200));

        var asked = await plugins.AskStatesAsync([Ask(Entry("feature/a", "s1")), Ask(Entry("feature/b", "s2"))]);

        Assert.Equal(PluginEvents.Late, asked[0].Failure);
        Assert.Equal(PullRequestStates.Abandoned, asked[1].Answer!.State);
    }

    /// <summary>No frame starts once the occasion's bound has passed: an entry it did not reach is not asked this time, and not a failure.</summary>
    [Fact]
    public async Task No_frame_starts_after_the_occasions_bound()
    {
        var clock = Now;
        var plugins = new LandingPlugins(_home, start: (_, _, _) => Task.FromResult<IHookChannel>(new Answering([], _ =>
        {
            clock = clock.AddSeconds(45);
            return new PullRequestState(PullRequestStates.Open);
        })), clock: () => clock);

        var asked = await plugins.AskStatesAsync([Ask(Entry("feature/a", "s1")), Ask(Entry("feature/b", "s2")), Ask(Entry("feature/c", "s3"))]);

        Assert.Equal([null, null, PullRequestCodes.NotAsked], asked.Select(answer => answer.Failure));
        Assert.Contains("not asked this time", asked[2].Sentence);
    }

    /// <summary>The machine log says each start, answer and stop by <c>state</c>, with the answer's code and never the address or the words.</summary>
    [Fact]
    public async Task The_machine_log_says_the_answers_code_and_never_its_address_or_words()
    {
        using (var log = new MachineLog(_home, "driver"))
        {
            var plugins = new LandingPlugins(_home, start: (_, _, _) => Task.FromResult<IHookChannel>(new Answering([], _ =>
                new PullRequestState(PullRequestStates.Completed)
                {
                    PullRequest = "https://example.test/org/project/_git/engine/pullrequest/7",
                    MergeCommit = new string('a', 40),
                    SourceCommit = new string('b', 40),
                    Message = "completed by squash, the plugin says",
                })), log: log, clock: () => Now);
            await plugins.AskStatesAsync([Ask(Entry("feature/a", "s1"))]);
        }

        var lines = MachineLogReader.Read(Path.Combine(_home, MachineLog.Folder), new LogFilter()).Lines;
        Assert.Equal([PluginEvents.Started, PluginEvents.Called, PluginEvents.Stopped], lines.Select(line => line.Event));
        Assert.All(lines.Where(line => line.Event != PluginEvents.Called), line => Assert.Equal(PluginEvents.ByState, line.Data.GetProperty("by").GetString()));
        var called = lines.Single(line => line.Event == PluginEvents.Called);
        Assert.Equal(HookPoints.State, called.Data.GetProperty("point").GetString());
        Assert.Equal(PullRequestStates.Completed, called.Data.GetProperty("answer").GetString());
        var raw = File.ReadAllText(Directory.GetFiles(Path.Combine(_home, MachineLog.Folder)).Single());
        Assert.DoesNotContain("example.test", raw);
        Assert.DoesNotContain("the plugin says", raw);
    }

    private void Install(string id, IReadOnlyList<string> points)
    {
        var folder = Path.Combine(_home, "plugins", id);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"), JsonSerializer.Serialize(new
        {
            id,
            hooks = new { command = new[] { "node", "plugin.mjs" }, points },
        }));
    }

    /// <summary>A plugin answering each frame from <paramref name="answer"/>: null waits until the frame's bound ends it.</summary>
    private sealed class Answering(List<string> told, Func<string, PullRequestState?> answer, IReadOnlyList<string>? points = null) : IHookChannel
    {
        public IReadOnlyList<string> Points => points ?? [HookPoints.Land, HookPoints.State];
        public bool Alive => true;
        public Task<HookDecision> ConsiderAsync(object payload, CancellationToken ct) => Task.FromResult(HookDecision.Allow);
        public Task EndedAsync(object payload, CancellationToken ct) => Task.CompletedTask;
        public Task<PluginLanding> LandAsync(object payload, CancellationToken ct) => Task.FromResult(new PluginLanding("x", false, null, "x"));

        public async Task<PullRequestState> StateAsync(object payload, CancellationToken ct)
        {
            var branch = JsonSerializer.SerializeToElement(payload).GetProperty("branch").GetString()!;
            told.Add(branch);
            if (answer(branch) is { } said) return said;
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("unreachable");
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

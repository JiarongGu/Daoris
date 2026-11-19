using System.Text.Json;
using Daoris.Driver;
using Daoris.Ports.Browser;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// D325's ACTORFLOW1c proof: an agent holding the release, whose start is refused for its sign-in, asks the credentials keeper through the
/// one request door; the person's sign-in answers it as the agent's next turn through the say door, the agent's retry's receipt settles the
/// step with no word from anyone else, and what the act came to is proposed into the repository's delivery knowledge entry, carried by the
/// next run's hand-off of a step naming it. Each seen failing first.
/// </summary>
public sealed class RoleRequestLoopTests : NodeRunFixture
{
    private const string ActorRelease = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"release","kind":"task","version":1,"reach":"pressed","outcomes":["done"],"role":"release-manager",
                   "options":{"knowledge":"release-path","evidence":["release","started"],
                              "receipts":{"release":"release-created","started":"stage-started"}}},
                  {"id":"config-needed","kind":"task","version":1,"reach":"pressed","outcomes":["needed","not-needed"],"role":"release-manager"},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>r","kind":"after","from":"claimed","to":"release"},
                  {"id":"r>n","kind":"after","from":"release","to":"config-needed"},
                  {"id":"n>h","kind":"when","from":"config-needed","to":"handed","outcome":"not-needed"}]}
        """;

    private const string Work = "s-work";

    private const string Site = "https://dev.example/org/_signin?return=project";

    private readonly List<(string Session, string Text, string Key)> _told = [];

    private IReadOnlyList<SessionRecord> _sessions = [];

    private PlaceCastFiles Casts => new(_home, () => true);

    /// <summary>The browser's readings, one per look, the last one again once they run out.</summary>
    private sealed class Pages(params IReadOnlyList<BrowserPage>[] readings) : IInAppBrowser
    {
        private int _next;

        public Task<string?> EnsureAsync(CancellationToken ct = default) => Task.FromResult<string?>(null);

        public void Show()
        {
        }

        public void Open(string address)
        {
        }

        public Task<IReadOnlyList<BrowserPage>?> PagesAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<BrowserPage>?>(readings[Math.Min(_next++, readings.Length - 1)]);
    }

    private void Read()
    {
        _branchTips["daoris/s1"] = Tip1;
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")) with { Executors = [Work] }, RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId])];
    }

    private NodeRuntime Telling()
    {
        var runtime = Runtime();
        runtime.Say = (session, text, key, _) =>
        {
            _told.Add((session, text, key));
            return Task.FromResult(new SayAnswer(true, "kept"));
        };
        runtime.SessionsRead = _ => Task.FromResult<IReadOnlyList<SessionRecord>?>(_sessions);
        return runtime;
    }

    /// <summary>A pass at the look's deadline, which judges every governed run, as the acts' journal is read then.</summary>
    private async Task PassAsync(NodeRuntime runtime, bool whole = true)
    {
        Read();
        await runtime.PassAsync(RunLook(whole: whole) with { Steps = () => [Step, Build, Deployed] }, CancellationToken.None);
    }

    private void Acted(string id, string tool, string fact, string state, string message)
    {
        var receipt = JsonSerializer.SerializeToElement(new Dictionary<string, string?>
        {
            ["fact"] = fact, ["state"] = state, ["message"] = message, ["by"] = state == "reached" ? "Daoris" : null,
        });
        PluginActRounds.Append(_home, Work, id, "daoris-azure-devops_" + tool, receipt, _now);
    }

    /// <summary>The agent's request to the credentials keeper, as the one door writes it: a question waiting on the person's sign-in at its site.</summary>
    private void AskedTheCredentialsKeeper()
    {
        var file = new System.Text.Json.Nodes.JsonObject
        {
            ["id"] = "q-keys", ["session"] = Work, ["question"] = "The release identity's sign-in expired: sign in so I can start stage 403.",
            ["choices"] = new System.Text.Json.Nodes.JsonArray(), ["recommend"] = null, ["why"] = null, ["by"] = null, ["owner"] = "sign-in",
            ["site"] = Site, ["role"] = StepRoles.CredentialsKeeper, ["at"] = Moments.Write(_now),
        };
        Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Sessions));
        File.WriteAllText(DaorisHome.Entry(_home, HomeEntry.Sessions, Work + QuestionFiles.Suffix), file.ToJsonString());
    }

    [Fact]
    public async Task A_refused_start_asks_the_credentials_keeper_whose_sign_in_brings_the_agent_back_and_its_receipt_settles_the_step()
    {
        Casts.Cast("machine", StepRoles.ReleaseManager, CastHolders.Agent, "the release path", _now);
        _sessions = [new SessionRecord(Work, RunHome.Repository, SessionStates.AwaitingStep) { NoteParts = new StepWait(RunId).Note().Parts }];
        BoundTo(ActorRelease);
        var runtime = Telling();
        await PassAsync(runtime, whole: true);
        await PassAsync(runtime);

        // The hand-off names the one door: the role that can, and the act.
        var handed = Assert.Single(_told, each => each.Key.Contains(":release:", StringComparison.Ordinal));
        Assert.Contains("`question_ask`", handed.Text, StringComparison.Ordinal);
        Assert.Contains("`role`", handed.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("go_ahead_ask", handed.Text, StringComparison.Ordinal);

        // The release is made; its start is refused for the sign-in, journaled on the step, which stays the agent's.
        Acted("a1", "create-release", "release-created", "reached", "Release-91 of definition 43 created by Daoris.");
        Acted("a2", "start-stage", "stage-started", "failed", "Stage 403 of Release-91 was not started: the release identity's sign-in expired.");
        await PassAsync(runtime);
        Assert.Equal(["reached", "failed"], StepActFiles.Journal(_home, RunUnit).Select(row => row.State));
        Assert.Equal(NodeStates.Active, RunStatus("release").State);

        // It asks the credentials keeper and ends its turn awaiting the step: the request stands for the person, not deleted with the turn.
        var since = _now;
        AskedTheCredentialsKeeper();
        Assert.Equal(QuestionSettle.Kept, new QuestionFiles(_home).Settle(Work, SessionStates.AwaitingStep, since));
        Assert.Equal(StepRoles.CredentialsKeeper, new QuestionFiles(_home).Read(Work)!.Role);

        // The person signs in through Daoris's browser: the answer is the agent's next turn through the say door, Daoris's word alone.
        var browser = new Pages(
            [new BrowserPage("t1", "https://login.example.net/oauth2/authorize?client=devops")],
            [new BrowserPage("t1", "https://dev.example/org/project/_releases")]);
        var before = _told.Count;
        Assert.Empty(await SignInAnswers.SayAsync(_home, browser, (session, text, key, ct) => runtime.Say!(session, text, key, ct), CancellationToken.None));
        var answered = Assert.Single(await SignInAnswers.SayAsync(_home, browser, (session, text, key, ct) => runtime.Say!(session, text, key, ct), CancellationToken.None));
        var word = Assert.Single(_told.Skip(before));
        Assert.Equal((Work, $"sign-in:{Work}:q-keys"), (word.Session, word.Key));
        Assert.Contains("dev.example", answered, StringComparison.Ordinal);

        // Its next turn retries the start; the receipt settles the step with no word from anyone else, and the next role's step is handed on.
        _now = _now.AddMinutes(5);
        Acted("a3", "start-stage", "stage-started", "reached", "Stage 403 of Release-91 started by Daoris.");
        before = _told.Count;
        await PassAsync(runtime);
        await PassAsync(runtime);
        await PassAsync(runtime);
        var release = RunStatus("release");
        Assert.Equal(NodeStates.Reached, release.State);
        Assert.Contains("by its receipts", release.Words, StringComparison.Ordinal);
        Assert.All(_told.Skip(before), each => Assert.Contains(":config-needed:", each.Key, StringComparison.Ordinal));

        // The long loop: what the start came to is proposed into `release-path`, and the next run's hand-off of a step naming it carries it.
        var proposed = Assert.Single(StepLessons.Open(_home, RunHome.Repository, "release-path", "q8"));
        Assert.Equal((RunId, "release"), (proposed.Run, proposed.Node));
        Assert.Contains("sign-in expired", proposed.Words, StringComparison.Ordinal);
        Assert.Contains("Stage 403 of Release-91 started by Daoris.", proposed.Words, StringComparison.Ordinal);
        Assert.Empty(StepLessons.Open(_home, RunHome.Repository, "release-path", RunId));
        var node = new GraphRuns(Files).Find(RunId)!.Version.Graph!.Nodes.Single(each => each.Id == "release");
        var held = new HeldStep("q8", "release", RunHome.Repository, "default", PersonWaits.Task, StepRoles.ReleaseManager, [], null, null) { Holder = StepHolders.Agent };
        var next = StepHandOffs.Words("q8", node, held, 1, StepLessons.Words("release-path", [proposed]));
        Assert.Contains("`release-path`", next, StringComparison.Ordinal);
        Assert.Contains("sign-in expired", next, StringComparison.Ordinal);
    }

    [Fact]
    public void A_step_whose_acts_all_reached_first_proposes_nothing()
    {
        var node = new NodeDeclaration("release", "task", 1, NodeReach.Pressed)
        {
            Options = new Dictionary<string, string> { ["receipts"] = """{"started":"stage-started"}""" },
        };
        StepActRow Row(string state, string message) => new("a", Work, "start-stage", "stage-started", state, message, null, null, null, _now) { Node = "release", Attempt = 1 };

        Assert.Null(StepLessons.Of(node, [Row("reached", "started")]));
        Assert.Null(StepLessons.Of(node, [Row("failed", "refused")]));
        Assert.Contains("refused", StepLessons.Of(node, [Row("failed", "refused"), Row("waiting", "waits on approver"), Row("reached", "started")]), StringComparison.Ordinal);
    }
}

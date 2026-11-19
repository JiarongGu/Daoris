using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A quest across repositories: an app part and a configuration part of one ask's plan, each a run in its own repository held by
/// its own role. The configuration part starts once the app's run journals a reached <c>stage-ended</c> (the plan's edge, no new graph); the
/// tester's joint check over every part's receipts fails on the configuration part, whose holder is asked through the request door; its
/// fix's receipt passes the check when the tester takes it again, what it came to is proposed into the part's delivery knowledge, and then
/// the approval is given. Seen failing first.
/// </summary>
public sealed class QuestAcrossRepositoriesTests : NodeRunFixture
{
    private const string App = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"release","kind":"task","version":1,"reach":"pressed","outcomes":["done"],"role":"release-manager",
                   "options":{"knowledge":"release-path","receipts":{"release":"release-created","ended":"stage-ended"}}},
                  {"id":"together","kind":"task","version":1,"reach":"pressed","outcomes":["done"],"role":"tester",
                   "options":{"joint":{"app":"stage-ended","config":"config-written"},"receipts":{"tested":"joint-tested"}}},
                  {"id":"release-approve","kind":"task","version":1,"reach":"pressed","outcomes":["done"],"role":"approver",
                   "options":{"receipts":{"approved":"approved"}}},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>r","kind":"after","from":"claimed","to":"release"},
                  {"id":"r>t","kind":"after","from":"release","to":"together"},
                  {"id":"t>a","kind":"after","from":"together","to":"release-approve"},
                  {"id":"a>h","kind":"after","from":"release-approve","to":"handed"}]}
        """;

    private const string Config = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"configure","kind":"task","version":1,"reach":"pressed","outcomes":["done"],"role":"config-changer",
                   "options":{"knowledge":"config-path","receipts":{"written":"config-written"}}},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>w","kind":"after","from":"claimed","to":"configure"},
                  {"id":"w>h","kind":"after","from":"configure","to":"handed"}]}
        """;

    private const string Settings = "settings";

    private const string ConfigRun = "q9";

    private const string AppWork = "s-app";

    private const string ConfigWork = "s-config";

    private static readonly string ConfigUnit = RunUnits.IdOf(ConfigRun);

    private readonly List<(string Session, string Text, string Key)> _told = [];

    private PlaceCastFiles Casts => new(_home, () => true);

    /// <summary>The ask's plan as the look reads it: the configuration part starts after the app's <c>stage-ended</c>.</summary>
    private static PartsReport Plan => new("a1", 1, ["app", "config"], new HashSet<string>())
    {
        Repositories = new Dictionary<string, string> { ["app"] = RunHome.Repository, ["config"] = Settings },
        Quests = new Dictionary<string, string> { ["app"] = RunId, ["config"] = ConfigRun },
        Waits = new Dictionary<string, IReadOnlyList<PartWait>> { ["config"] = [new PartWait("app", "stage-ended")] },
    };

    private NodeRuntime Telling()
    {
        var runtime = Runtime();
        runtime.Say = (session, text, key, _) =>
        {
            _told.Add((session, text, key));
            return Task.FromResult(new SayAnswer(true, "kept"));
        };
        IReadOnlyList<SessionRecord> sessions =
        [
            new SessionRecord(AppWork, RunHome.Repository, SessionStates.AwaitingStep) { NoteParts = new StepWait(RunId).Note().Parts },
            new SessionRecord(ConfigWork, Settings, SessionStates.AwaitingStep) { NoteParts = new StepWait(ConfigRun).Note().Parts },
        ];
        runtime.SessionsRead = _ => Task.FromResult<IReadOnlyList<SessionRecord>?>(sessions);
        return runtime;
    }

    private async Task PassAsync(NodeRuntime runtime, int times = 1)
    {
        _branchTips["daoris/s1"] = Tip1;
        _units =
        [
            RunHome.QuestUnit(RunHome.Quest(RunId, "Done")) with { Executors = [AppWork] },
            RunHome.QuestUnit(RunHome.Quest(ConfigRun, "Done", to: Settings)) with { Executors = [ConfigWork] },
            RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId]),
        ];
        for (var each = 0; each < times; each++)
        {
            var look = RunLook(whole: true) with
            {
                Units = _ => Task.FromResult(new NodeUnitsRead(_units, new Dictionary<string, string> { [RunHome.Repository] = "checkout", [Settings] = "checkout-b" })
                {
                    Parts = new Dictionary<string, PartsReport> { ["a1"] = Plan },
                }),
                Steps = () => [Step, Build, Deployed],
            };
            await runtime.PassAsync(look, CancellationToken.None);
        }
    }

    private void Acted(string session, string id, string tool, string fact, string state, string message, string? part = null)
    {
        _now = _now.AddMinutes(1);
        var receipt = JsonSerializer.SerializeToElement(new Dictionary<string, string?>
        {
            ["fact"] = fact, ["state"] = state, ["message"] = message, ["by"] = state == "reached" ? "Daoris" : null, ["part"] = part,
        });
        PluginActRounds.Append(_home, session, id, tool, receipt, _now);
    }

    private IEnumerable<(string Session, string Text, string Key)> ToldAfter(int count) => _told.Skip(count);

    [Fact]
    public async Task The_configuration_part_starts_on_the_apps_receipt_and_the_joint_check_sends_its_failure_back_then_the_approval()
    {
        foreach (var role in new[] { StepRoles.ReleaseManager, StepRoles.ConfigChanger, StepRoles.Tester, StepRoles.Approver })
        {
            Casts.Cast("machine", role, CastHolders.Agent, "the quest across repositories", _now);
        }

        BoundTo(App, ask: "a1");
        Assert.True(new WorkflowRunFiles(_home).Bind(new WorkflowRunBinding(ConfigRun, Settings, "default", "config", WorkflowLevels.Repository, _now)
        {
            Version = 1, Digest = RunHome.SaveGraph(_home, Config, "config"), Ask = "a1",
        }));
        var runtime = Telling();
        await PassAsync(runtime, 2);

        // The app part's holder is handed its release; the configuration part's step waits on the app's `stage-ended`, handed to nobody.
        Assert.Single(_told, each => each.Session == AppWork && each.Key.Contains(":release:", StringComparison.Ordinal));
        Assert.DoesNotContain(_told, each => each.Session == ConfigWork);
        Assert.Equal(NodeStates.Active, RunStatus("configure", ConfigUnit).State);

        // The app's stage ends: its receipts settle its release, and that receipt starts the configuration part.
        Acted(AppWork, "a1", "daoris-azure-devops_create-release", "release-created", "reached", "Release-91 created by Daoris.");
        Acted(AppWork, "a2", "daoris-azure-devops_release-status", "stage-ended", "reached", "Stage test of Release-91 ended.");
        var before = _told.Count;
        await PassAsync(runtime, 3);
        Assert.Equal(NodeStates.Reached, RunStatus("release").State);
        var started = Assert.Single(ToldAfter(before), each => each.Session == ConfigWork);
        Assert.Contains(":configure:", started.Key, StringComparison.Ordinal);
        Assert.Contains("`config-path`", started.Text, StringComparison.Ordinal);

        // The joint check waits on every part's receipt: not handed while the configuration is unwritten.
        Assert.Equal(NodeStates.Active, RunStatus("together").State);
        Assert.DoesNotContain(_told, each => each.Key.Contains(":together:", StringComparison.Ordinal));
        Acted(ConfigWork, "c1", "daoris-settings_write-config", "config-written", "reached", "Setting store written for the test stage.");
        before = _told.Count;
        await PassAsync(runtime, 2);
        Assert.Equal(NodeStates.Reached, RunStatus("configure", ConfigUnit).State);
        var check = Assert.Single(ToldAfter(before), each => each.Key.Contains(":together:", StringComparison.Ordinal));
        Assert.Equal(AppWork, check.Session);
        Assert.Contains("`config`", check.Text, StringComparison.Ordinal);

        // The tests run together fail on the configuration part: its holder is asked, the check stays the tester's.
        Acted(AppWork, "t1", "daoris-tests_run-together", "joint-tested", "failed", "The app could not read its connection setting.", part: "config");
        before = _told.Count;
        await PassAsync(runtime, 2);
        Assert.Equal(NodeStates.Active, RunStatus("together").State);
        var asked = Assert.Single(ToldAfter(before));
        Assert.Equal(ConfigWork, asked.Session);
        Assert.Contains("connection setting", asked.Text, StringComparison.Ordinal);
        Assert.Contains("`together`", asked.Text, StringComparison.Ordinal);

        // Its fix's receipt is journaled on the part's step, proposed into its delivery knowledge, and the check is handed again.
        Acted(ConfigWork, "c2", "daoris-settings_write-config", "config-written", "reached", "Connection setting added for the test stage.");
        before = _told.Count;
        await PassAsync(runtime, 2);
        Assert.Equal(["c1", "returned:t1", "c2"], StepActFiles.Journal(_home, ConfigUnit).Select(row => row.Id));
        var lesson = Assert.Single(StepLessons.Open(_home, Settings, "config-path", "q11"));
        Assert.Contains("connection setting", lesson.Words, StringComparison.Ordinal);
        var again = Assert.Single(ToldAfter(before));
        Assert.Contains(":together:", again.Key, StringComparison.Ordinal);
        Assert.Equal(NodeStates.Active, RunStatus("together").State);

        // Taken again, the check passes on the fix's receipt; then the approval is the approver's act.
        Acted(AppWork, "t2", "daoris-tests_run-together", "joint-tested", "reached", "The app and its configuration passed together.");
        await PassAsync(runtime, 3);
        var together = RunStatus("together");
        Assert.Equal(NodeStates.Reached, together.State);
        Assert.Contains("by its receipts", together.Words, StringComparison.Ordinal);
        Assert.Contains(_told, each => each.Key.Contains(":release-approve:", StringComparison.Ordinal));
        Assert.Equal(NodeStates.Active, RunStatus("release-approve").State);
        Acted(AppWork, "p1", "daoris-azure-devops_approve-release", "approved", "reached", "Release-91 approved for production.");
        await PassAsync(runtime, 2);
        Assert.Equal(NodeStates.Reached, RunStatus("release-approve").State);
    }

    /// <summary>The plan's edge naming a receipt is the part's wait and the look's report carries it with the parts' quests; it holds no landing.</summary>
    [Fact]
    public void A_plans_receipt_edge_is_the_parts_wait_and_never_a_landings()
    {
        var answer = new Daoris.Contracts.Asks.AskPlanResponse("a1",
        [
            new Daoris.Contracts.Asks.AskPlanRevisionResponse(null, 1, 1, "two repositories",
                [new("app", RunHome.Repository, RunId, "the app runs", [], []), new("config", Settings, ConfigRun, "its configuration is read", [], [])],
                [new("config", "app", "after", "configured once the app's stage ended") { Receipt = "stage-ended" }], [], [], [], [], []),
        ], [], []);

        var part = TrainPlan.Of(answer)!.Parts.Single(each => each.Id == "config");

        Assert.Empty(part.After);
        Assert.Equal([new PartWait("app", "stage-ended")], part.Waits);
    }

    /// <summary>A check passes only on a test taken after every part's newest receipt: a part acting again sends it back to its tester.</summary>
    [Fact]
    public void A_joint_check_passes_only_on_a_test_newer_than_every_parts_receipt()
    {
        var node = new NodeDeclaration("together", "task", 1, NodeReach.Pressed)
        {
            Options = new Dictionary<string, string>
            {
                ["joint"] = """{"app":"stage-ended","config":"config-written"}""", ["receipts"] = """{"tested":"joint-tested"}""",
            },
        };
        StepActRow Row(string id, string fact, string state, int minute) =>
            new(id, AppWork, "tool", fact, state, id, null, null, null, _now.AddMinutes(minute));
        var parts = new Dictionary<string, StepActRow?> { ["app"] = Row("a", "stage-ended", "reached", 1), ["config"] = Row("c", "config-written", "reached", 2) };

        Assert.Equal(new Dictionary<string, string> { ["app"] = "stage-ended", ["config"] = "config-written" }, JointChecks.Over(node));
        Assert.True(JointChecks.Passed(node, [Row("t", "joint-tested", "reached", 3)], parts));
        Assert.False(JointChecks.Passed(node, [Row("t", "joint-tested", "reached", 1)], parts));
        Assert.False(JointChecks.Passed(node, [Row("t", "joint-tested", "failed", 3)], parts));
        Assert.False(JointChecks.Passed(node, [Row("t", "joint-tested", "reached", 3)], new Dictionary<string, StepActRow?> { ["app"] = parts["app"], ["config"] = null }));
        Assert.Equal(["config"], JointChecks.Missing(node, new Dictionary<string, StepActRow?> { ["app"] = parts["app"], ["config"] = null }));
    }
}

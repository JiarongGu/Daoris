using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A release whose release manager is an agent: the step names no target, only the repository's delivery knowledge, the acts and the receipts
/// that settle it; the agent takes its acts as its plugin's tools, each round's receipt journaled on the run's step, and the step is settled by
/// its receipts, never by its words; the approval beside it stays the approver's, observed; and the run goes on to the next role's step.
/// Each seen failing first.
/// </summary>
public sealed class AgentReleaseActsTests : NodeRunFixture
{
    private static readonly NodeDeclaration Approve = Step with { Id = Plugin + "/approve", Kind = Plugin + "/approve" };

    private const string ActorRelease = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"release","kind":"task","version":1,"reach":"pressed","outcomes":["done"],"role":"release-manager",
                   "options":{"knowledge":"release-path","evidence":["release","started","ended"],
                              "receipts":{"release":"release-created","started":"stage-started","ended":"stage-ended"}}},
                  {"id":"approve","kind":"acme.deploy/approve","version":1,"reach":"performed","role":"approver"},
                  {"id":"config-needed","kind":"task","version":1,"reach":"pressed","outcomes":["needed","not-needed"],"role":"release-manager"},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>r","kind":"after","from":"claimed","to":"release"},
                  {"id":"c>a","kind":"after","from":"claimed","to":"approve"},
                  {"id":"r>n","kind":"after","from":"release","to":"config-needed"},
                  {"id":"a>n","kind":"after","from":"approve","to":"config-needed"},
                  {"id":"n>h","kind":"when","from":"config-needed","to":"handed","outcome":"not-needed"}]}
        """;

    /// <summary>The repository's delivery knowledge for its release, as a repository keeps any knowledge entry.</summary>
    private const string Procedure = """
        ---
        name: release-path
        applies_when: releasing the reporting app after its pull request merges
        enforces: the release is made from the merged build on its declared definition and stage, and its approval stays the approver's
        ---
        # Releasing the reporting app

        - release definition: 43
        - stage: 403
        - pipeline: 7

        Create the release with `create-release`, start the stage with `start-stage`, and read the stage's end with `release-status`.
        """;

    private const string Work = "s-work";

    private const string Origin = "https://dev.example/org/project/_git/app";

    private readonly List<(string Session, string Text, string Key)> _told = [];

    private IReadOnlyList<SessionRecord> _sessions = [];

    private PlaceCastFiles Casts => new(_home, () => true);

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
        await runtime.PassAsync(RunLook(whole: whole) with { Steps = () => [Step, Build, Deployed, Approve] }, CancellationToken.None);
    }

    /// <summary>A round of a plugin's tool whose answer carries a receipt, as the session's tools server keeps it.</summary>
    private void Acted(string id, string tool, string fact, string state, string message, string? url = null, string? waitsOn = null)
    {
        var receipt = JsonSerializer.SerializeToElement(new Dictionary<string, string?>
        {
            ["fact"] = fact, ["state"] = state, ["message"] = message, ["url"] = url, ["waitsOn"] = waitsOn, ["by"] = state == "reached" ? "Daoris" : null,
        });
        StepActFiles.Append(_home, Work, id, "daoris-azure-devops_" + tool, receipt, _now);
    }

    [Fact]
    public async Task The_agent_releases_by_its_acts_each_receipt_journaled_the_approval_stays_the_approvers_and_the_run_goes_on()
    {
        Casts.Cast("machine", StepRoles.ReleaseManager, CastHolders.Agent, "the release path", _now);
        _sessions = [new SessionRecord(Work, RunHome.Repository, SessionStates.AwaitingStep) { NoteParts = new StepWait(RunId).Note().Parts }];
        var repository = Path.Combine(_home, "checkout");
        Directory.CreateDirectory(Path.Combine(repository, ".claude", "knowledge"));
        File.WriteAllText(Path.Combine(repository, ".claude", "knowledge", "release-path.md"), Procedure);
        BoundTo(ActorRelease);
        var runtime = Telling();
        await PassAsync(runtime, whole: true);
        await PassAsync(runtime);
        Assert.Equal((NodeStates.Active, NodeStates.Active), (RunStatus("release").State, RunStatus("approve").State));

        // The step names no target: the session is pointed at the repository's delivery knowledge, its acts and the receipts that settle it.
        var declared = new GraphRuns(Files).Find(RunId)!.Version.Graph!.Nodes.Single(node => node.Id == "release");
        Assert.DoesNotContain(declared.Options.Keys, name => name is "releaseDefinition" or "stage" or "pipeline");
        var handed = Assert.Single(_told, each => each.Key.Contains(":release:", StringComparison.Ordinal));
        Assert.Equal(Work, handed.Session);
        Assert.Contains("`release-path`", handed.Text, StringComparison.Ordinal);
        Assert.Contains("`stage-ended`", handed.Text, StringComparison.Ordinal);

        // The agent reads its procedure from the entry the hand-off names, in the repository, and takes its acts with what it says.
        var entry = StepReceipts.Knowledge(declared)!;
        var procedure = File.ReadAllLines(Path.Combine(repository, ".claude", "knowledge", entry + ".md"));
        string Said(string name) => procedure.Single(line => line.StartsWith($"- {name}: ", StringComparison.Ordinal))[(name.Length + 4)..];
        var link = $"{Origin}/_releaseProgress?releaseId=91&definition={Said("release definition")}";
        Acted("a1", "create-release", "release-created", "reached", "Release-91 of definition 43 carries 11111111, created by Daoris.", link);
        Acted("a2", "start-stage", "stage-started", "reached", $"Stage {Said("stage")} of Release-91 started by Daoris.", link);
        await PassAsync(runtime);

        var journaled = StepActFiles.Journal(_home, RunUnit);
        Assert.Equal(["release-created", "stage-started"], journaled.Select(row => row.Fact));
        Assert.All(journaled, row => Assert.Equal(("release", Work), (row.Node, row.Session)));
        Assert.Equal(NodeStates.Active, RunStatus("release").State);

        // The receipts settle the step, never its words: the agent's settle naming a receipt's evidence is refused, and nothing kept.
        var worded = StepSettles.Settle(Files, new StepSettleAsk(RunId, "release", null, "it ended",
            new Dictionary<string, string> { ["release"] = "91", ["started"] = "yes", ["ended"] = "yes" }), new SettleBy(CastHolders.AgentNamed("claude"), 1), () => _now);
        Assert.Equal(StepReceipts.ByReceipt, worded.Refused);
        Assert.Empty(Files.Read(RunUnit)!.Pending);

        // The approval stays the approver's: no agent's settle reaches it; its plugin observes it, and the agent's read says it waits on it.
        Assert.Equal(RunNodeWords.NotWaiting,
            StepSettles.Settle(Files, new StepSettleAsk(RunId, "approve", "approved", null, new Dictionary<string, string>()), new SettleBy(CastHolders.AgentNamed("claude"), 1), () => _now).Refused);
        Acted("a3", "release-status", "stage-ended", "waiting", "Stage 403 of Release-91 waits on its approver.", link, waitsOn: StepRoles.Approver);
        await PassAsync(runtime);
        Assert.Equal(NodeStates.Active, RunStatus("release").State);
        var invocation = _port.Sent.Single(call => call.Step == Approve.Kind).Invocation;
        _port.Heard.Enqueue(new(Plugin, 1, new WorkStepFrame("approve", new WorkOutcome(WorkStates.Reached, Evidence: new WorkEvidence(null, link, null, "approved") { By = "outside: A Person" }),
            _now, "e-approve", Invocation: invocation)));
        await PassAsync(runtime);
        Assert.Equal(NodeStates.Reached, RunStatus("approve").State);

        // The stage's end, observed by the agent's read, settles the step as its holder with its receipts, and the next role's step is handed on.
        Acted("a4", "release-status", "stage-ended", "reached", "Stage 403 of Release-91 succeeded.", link);
        await PassAsync(runtime);
        await PassAsync(runtime);
        await PassAsync(runtime);
        var release = RunStatus("release");
        Assert.Equal(NodeStates.Reached, release.State);
        Assert.Contains("by its receipts", release.Words, StringComparison.Ordinal);
        Assert.Contains("ended: Stage 403 of Release-91 succeeded.", release.Words, StringComparison.Ordinal);
        Assert.Equal(4, StepActFiles.Journal(_home, RunUnit).Count);
        Assert.Equal(NodeStates.Active, RunStatus("config-needed").State);
        Assert.Contains(_told, each => each.Key.Contains(":config-needed:", StringComparison.Ordinal) && each.Session == Work);
    }

    [Fact]
    public async Task An_act_is_journaled_once_and_one_no_held_step_maps_is_journaled_nowhere()
    {
        Casts.Cast("machine", StepRoles.ReleaseManager, CastHolders.Agent, "the release path", _now);
        _sessions = [new SessionRecord(Work, RunHome.Repository, SessionStates.AwaitingStep) { NoteParts = new StepWait(RunId).Note().Parts }];
        BoundTo(ActorRelease);
        var runtime = Telling();
        await PassAsync(runtime, whole: true);
        await PassAsync(runtime);

        Acted("a1", "create-release", "release-created", "reached", "Release-91 created by Daoris.");
        Acted("a2", "discover-releases", "candidates", "reached", "three definitions found.");
        Assert.Equal(["release-created", "candidates"], StepActFiles.Rounds(_home, Work).Select(row => row.Fact));
        Read();
        var pass = await runtime.PassAsync(RunLook() with { Steps = () => [Step, Build, Deployed, Approve] }, CancellationToken.None);
        await PassAsync(runtime);

        Assert.Equal(["release-created"], StepActFiles.Journal(_home, RunUnit).Select(each => each.Fact));
        Assert.Contains(pass.Said, line => line.StartsWith("act release-created (reached) of step release of #q7", StringComparison.Ordinal));
    }
}

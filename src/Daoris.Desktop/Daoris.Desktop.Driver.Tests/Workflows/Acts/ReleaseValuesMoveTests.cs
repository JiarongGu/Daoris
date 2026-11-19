using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A repository's release values move into its delivery knowledge (D325 point 5): a new version of its workflow holding the actor form reads
/// none of the values its fixed release steps took, so they are proposed into the entry the release step names, handed to its holder, kept
/// where they are until the entry in the repository's checkout holds each, then cleared with that reason; the approval node's values stay its
/// reader (point 7). Each seen failing first.
/// </summary>
public sealed class ReleaseValuesMoveTests : NodeRunFixture
{
    private const string Workflow = "docs";

    /// <summary>The fixed form: one plugin step per fact of the release, each reading the repository's values.</summary>
    private const string FixedRelease = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"release-target","kind":"acme.deploy/release-target","version":1,"reach":"performed","role":"release-manager"},
                  {"id":"release-create","kind":"acme.deploy/release-create","version":1,"reach":"performed","role":"release-manager"},
                  {"id":"release-approve","kind":"acme.deploy/release-approve","version":1,"reach":"performed","role":"approver"},
                  {"id":"release-watch","kind":"acme.deploy/release-watch","version":1,"reach":"performed"},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>t","kind":"after","from":"claimed","to":"release-target"},
                  {"id":"t>c","kind":"after","from":"release-target","to":"release-create"},
                  {"id":"c>a","kind":"after","from":"release-create","to":"release-approve"},
                  {"id":"a>w","kind":"after","from":"release-approve","to":"release-watch"},
                  {"id":"w>h","kind":"after","from":"release-watch","to":"handed"}]}
        """;

    /// <summary>The actor form: the release manager's one step naming its procedure and receipts, the approver's approval beside it.</summary>
    private const string ActorRelease = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"release","kind":"task","version":1,"reach":"pressed","outcomes":["done"],"role":"release-manager",
                   "options":{"knowledge":"release-path","evidence":["release","started","ended"],
                              "receipts":{"release":"release-created","started":"stage-started","ended":"stage-ended"}}},
                  {"id":"release-approve","kind":"acme.deploy/release-approve","version":1,"reach":"performed","role":"approver",
                   "options":{"receipts":{"approval":"approved"}}},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>r","kind":"after","from":"claimed","to":"release"},
                  {"id":"c>a","kind":"after","from":"claimed","to":"release-approve"},
                  {"id":"r>h","kind":"after","from":"release","to":"handed"},
                  {"id":"a>h","kind":"after","from":"release-approve","to":"handed"}]}
        """;

    /// <summary>A release definition, its stage, the build pipeline and the action, as the window's form set them on every release step.</summary>
    private const string Target = """{"action":"create-and-start","pipeline":7,"releaseDefinition":43,"stage":403}""";

    private static readonly string Values = """{"workflowValues":{"engine":{"docs":{"release-target":""" + Target + ""","release-create":""" + Target
        + ""","release-approve":""" + Target + ""","release-watch":""" + Target + "}}}}";

    private static readonly string[] Moved = ["release-create", "release-target", "release-watch"];

    private const string Work = "s-work";

    private readonly List<(string Session, string Text, string Key)> _told = [];

    private IReadOnlyList<SessionRecord> _sessions = [];

    private string Checkout => Path.Combine(_home, "checkout");

    private string ConfigPath => DaorisHome.Entry(_home, HomeEntry.DriverConfig);

    private static IReadOnlyDictionary<string, string> TargetOptions() =>
        JsonDocument.Parse(Target).RootElement.EnumerateObject().ToDictionary(each => each.Name, each => each.Value.GetRawText(), StringComparer.Ordinal);

    /// <summary>The workflow kept with both versions, the values in the driver's file, and run <c>q7</c> bound to the actor form.</summary>
    private IReadOnlyList<NamedVersion> Saved()
    {
        Directory.CreateDirectory(WorkflowStore.FolderOf(_home));
        File.WriteAllText(WorkflowStore.PathOf(_home, Workflow),
            $$"""{"id":"docs","name":"Docs","versions":[{"version":1,"at":"2026-10-21T09:00:00Z","door":"terminal","schema":2,"graph":{{FixedRelease}}},{"version":2,"at":"2026-11-19T09:00:00Z","door":"terminal","schema":2,"graph":{{ActorRelease}}}]}""");
        var versions = new WorkflowFiles(_home).Load(Workflow)!.Read.Versions;
        Assert.Equal(2, versions.Count);
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        File.WriteAllText(ConfigPath, Values);
        Assert.True(new WorkflowRunFiles(_home).Bind(new WorkflowRunBinding(RunId, RunHome.Repository, "default", Workflow, WorkflowLevels.Repository, _now)
        {
            Version = 2, Digest = versions[1].Digest,
        }));
        return versions;
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

    /// <summary>A pass at the look's deadline over the repository's checkout, under the driver's file as it stands.</summary>
    private async Task PassAsync(NodeRuntime runtime)
    {
        _branchTips["daoris/s1"] = Tip1;
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")) with { Executors = [Work] }, RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId])];
        await runtime.PassAsync(RunLook(File.ReadAllText(ConfigPath)) with
        {
            Units = _ => Task.FromResult(new NodeUnitsRead(_units, new Dictionary<string, string> { [RunHome.Repository] = Checkout })),
        }, CancellationToken.None);
    }

    /// <summary>The entry as the holder writes it in its repository's own change, from the values it was handed.</summary>
    private void Landed(string stage = "403") => Write(Path.Combine(Checkout, ".claude", "knowledge", "release-path.md"), $"""
        ---
        name: release-path
        applies_when: releasing the app after its pull request merges
        enforces: the release is made from the merged build on its declared definition and stage
        ---
        # Releasing the app

        - release definition: 43
        - stage: {stage}
        - pipeline: 7
        - action: create-and-start

        Create the release with `create-release`, start the stage with `start-stage`, and read the stage's end with `release-status`.
        """);

    private static void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    [Fact]
    public async Task Applying_the_actor_form_proposes_the_entry_with_each_value_and_loses_none()
    {
        var versions = Saved();
        var config = DriverConfig.Parse(Values);

        // `workflow apply` says where the values go, and no longer offers to carry them onto another plugin node.
        var notes = WorkflowApplyNotes.Unread(config, Workflow, 2, versions[0].Graph, versions[1].Graph!);
        var line = Assert.Single(notes);
        Assert.Contains("`release-path`", line, StringComparison.Ordinal);
        Assert.All(Moved, node => Assert.Contains($"`{node}`", line, StringComparison.Ordinal));
        Assert.DoesNotContain("`release-approve`", line, StringComparison.Ordinal);

        // The next look proposes the entry to the repository, each value the steps held in it, and the driver's file keeps every one.
        var runtime = Telling();
        await PassAsync(runtime);
        var proposed = Assert.Single(StepLessons.Read(_home));
        Assert.Equal((RunHome.Repository, "release-path"), (proposed.Repository, proposed.Entry));
        Assert.Equal((Workflow, 2), (proposed.Moved!.Workflow, proposed.Moved.Version));
        Assert.Equal(Moved, proposed.Moved.Values.Keys.Order(StringComparer.Ordinal));
        Assert.All(Moved, node => Assert.Equal(TargetOptions(), proposed.Moved.Values[node]));
        Assert.All(["`releaseDefinition` 43", "`stage` 403", "`pipeline` 7", "`action` \"create-and-start\""], value => Assert.Contains(value, proposed.Words, StringComparison.Ordinal));
        Assert.Null(proposed.Landed);
        var kept = DriverConfig.Load(ConfigPath);
        Assert.All([.. Moved, "release-approve"], node => Assert.Equal(TargetOptions(), WorkflowValuesKey.Of(kept.WorkflowValues, RunHome.Repository, Workflow, node)));

        // Proposed once: the next look finds it kept.
        await PassAsync(runtime);
        Assert.Single(StepLessons.Read(_home));
    }

    [Fact]
    public async Task The_holder_is_handed_the_values_their_entry_landing_clears_them_and_its_acts_read_their_target_there()
    {
        new PlaceCastFiles(_home, () => true).Cast("machine", StepRoles.ReleaseManager, CastHolders.Agent, "the release path", _now);
        _sessions = [new SessionRecord(Work, RunHome.Repository, SessionStates.AwaitingStep) { NoteParts = new StepWait(RunId).Note().Parts }];
        Saved();
        var runtime = Telling();
        await PassAsync(runtime);
        await PassAsync(runtime);

        // The hand-off names the entry and carries the values proposed into it, each one, while the entry does not hold them.
        var handed = Assert.Single(_told, each => each.Key.Contains(":release:", StringComparison.Ordinal));
        Assert.Contains("`release-path`", handed.Text, StringComparison.Ordinal);
        Assert.All(["`releaseDefinition` 43", "`stage` 403", "`pipeline` 7"], value => Assert.Contains(value, handed.Text, StringComparison.Ordinal));

        // An entry holding another stage has not taken them: nothing is cleared.
        Landed(stage: "404");
        await PassAsync(runtime);
        Assert.Equal(TargetOptions(), WorkflowValuesKey.Of(DriverConfig.Load(ConfigPath).WorkflowValues, RunHome.Repository, Workflow, "release-create"));

        // The holder's change lands the entry with each value: the values the new version reads no more are cleared with that reason, and
        // the approval node's stay, its reader until it takes its stage from the holder's receipt.
        Landed();
        await PassAsync(runtime);
        var kept = DriverConfig.Load(ConfigPath);
        Assert.All(Moved, node => Assert.Null(WorkflowValuesKey.Of(kept.WorkflowValues, RunHome.Repository, Workflow, node)));
        Assert.All(Moved, node => Assert.Equal(ValuesMoves.Reason("release-path"), kept.WorkflowValuesWhy[WorkflowValuesKey.WhyOf(RunHome.Repository, Workflow, node)]));
        Assert.Equal(TargetOptions(), WorkflowValuesKey.Of(kept.WorkflowValues, RunHome.Repository, Workflow, "release-approve"));
        Assert.NotNull(Assert.Single(StepLessons.Read(_home)).Landed);
        Assert.Empty(StepLessons.Open(_home, RunHome.Repository, "release-path", "q9"));

        // The act reads its target from the entry the step names, the one place that holds it now, and its receipt settles the step's fact.
        var declared = new GraphRuns(Files).Find(RunId)!.Version.Graph!.Nodes.Single(node => node.Id == "release");
        var procedure = File.ReadAllLines(Path.Combine(Checkout, ".claude", "knowledge", StepReceipts.Knowledge(declared)! + ".md"));
        string Said(string name) => procedure.Single(each => each.StartsWith($"- {name}: ", StringComparison.Ordinal))[(name.Length + 4)..];
        var receipt = JsonSerializer.SerializeToElement(new Dictionary<string, string?>
        {
            ["fact"] = "release-created", ["state"] = "reached", ["by"] = "Daoris",
            ["message"] = $"Release-91 of definition {Said("release definition")} from pipeline {Said("pipeline")} carries 11111111, created by Daoris.",
        });
        PluginActRounds.Append(_home, Work, "a1", "daoris-azure-devops_create-release", receipt, _now);
        await PassAsync(runtime);
        var journaled = Assert.Single(StepActFiles.Journal(_home, RunUnit));
        Assert.Equal(("release", "release-created"), (journaled.Node, journaled.Fact));
        Assert.Contains("definition 43 from pipeline 7", journaled.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Values_an_unended_run_still_reads_or_set_anew_since_stay()
    {
        var versions = Saved();
        var config = DriverConfig.Parse(Values);
        var move = Assert.Single(ValuesMoves.Of(config, Workflow, versions, _now)) with { Landed = _now };
        var onFixed = new GraphRun(new WorkflowRunBinding("q6", RunHome.Repository, "default", Workflow, WorkflowLevels.Repository, _now) { Version = 1 },
            new DecodedVersion(1, null, versions[0].Graph, versions[0].Digest, null), null);

        // A run bound to the fixed steps still reads them: kept, and said.
        var said = new List<string>();
        var held = ValuesMoves.Cleared(config, move, [onFixed], said);
        Assert.All(Moved, node => Assert.Equal(TargetOptions(), WorkflowValuesKey.Of(held.WorkflowValues, RunHome.Repository, Workflow, node)));
        Assert.Contains(said, line => line.Contains("#q6", StringComparison.Ordinal));

        // Once it ends they are cleared, but a node's values set anew since the move are another target, and stay.
        var reset = WorkflowValuesKey.Apply(config, RunHome.Repository, Workflow, "release-watch", new Dictionary<string, string> { ["stage"] = "404" }, "another stage");
        var cleared = ValuesMoves.Cleared(reset, move, [onFixed with { Ended = new RunEnd("q6", _now) }], said);
        Assert.Null(WorkflowValuesKey.Of(cleared.WorkflowValues, RunHome.Repository, Workflow, "release-create"));
        Assert.Null(WorkflowValuesKey.Of(cleared.WorkflowValues, RunHome.Repository, Workflow, "release-target"));
        Assert.Equal("404", WorkflowValuesKey.Of(cleared.WorkflowValues, RunHome.Repository, Workflow, "release-watch")!["stage"]);
        Assert.Equal("another stage", cleared.WorkflowValuesWhy[WorkflowValuesKey.WhyOf(RunHome.Repository, Workflow, "release-watch")]);
    }
}

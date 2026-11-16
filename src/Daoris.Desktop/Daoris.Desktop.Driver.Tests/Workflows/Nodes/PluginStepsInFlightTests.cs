using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A plugin step in flight is an attempt its plugin performs that is recorded, sent or unknown, on a run the runtime governs now, at a node
/// its graph declares, while its plugin's process is up, and whose step waits on no role's act: what an update's drain waits on beside the
/// sessions and the turns. Settled and held attempts, Daoris's own kinds, an orphaned record (a run no binding governs, a node no graph
/// declares), a plugin down and a step whose progress names the role it waits on are none, so nothing that never moves holds the install.
/// </summary>
public sealed class PluginStepsInFlightTests : NodeRunFixture
{
    /// <summary>The claim, the plugin's performed deploy, then the landing.</summary>
    private const string ClaimedDeployHanded = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"deploy","kind":"acme.deploy/deploy","version":1,"reach":"performed"},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>d","kind":"after","from":"claimed","to":"deploy"},{"id":"d>h","kind":"after","from":"deploy","to":"handed"}]}
        """;

    private NodeAttempt Attempt(string node, string step, string state, string invocation = "i-1") =>
        new(node, 1, step.Contains('/') ? step[..step.IndexOf('/')] : step,
            new WorkPerformCall("unit-b1", step, 1, invocation, "k-" + invocation, Tip1, [], _now.AddMinutes(15)), state)
        { RecordedAt = _now, Generation = 1, Deliveries = 1 };

    [Theory]
    [InlineData(AttemptStates.Recorded, true)]
    [InlineData(AttemptStates.Sent, true)]
    [InlineData(AttemptStates.Unknown, true)]
    [InlineData(AttemptStates.Settled, false)]
    [InlineData(AttemptStates.Held, false)]
    public void A_plugin_s_attempt_is_in_flight_until_it_settles_or_is_held_for_the_person(string state, bool inFlight) =>
        Assert.Equal(inFlight, PluginStepsInFlight.InFlight(Attempt("release", "acme.deploy/release", state)));

    [Fact]
    public void Daoris_s_own_kinds_are_no_plugin_step()
    {
        Assert.False(PluginStepsInFlight.InFlight(Attempt("gates", NodeKinds.Check, AttemptStates.Sent)));
        Assert.False(PluginStepsInFlight.InFlight(Attempt("close", NodeKinds.QuestClose, AttemptStates.Unknown)));
        Assert.False(PluginStepsInFlight.InFlight(Attempt("ask", NodeKinds.Request, AttemptStates.Recorded)));
    }

    /// <summary>
    /// Run q7 bound to the deploy graph, its deploy sent to the plugin with <paramref name="progress"/> said of it; and run q9's record, which
    /// no binding names any more (its repository retired), holding a deploy sent that nothing will ever ask after.
    /// </summary>
    private NodeRuntime Governing(StepProgress? progress = null)
    {
        var digest = RunHome.SaveGraph(_home, ClaimedDeployHanded);
        RunHome.Bind(_home, RunId, digest, _now);
        var bound = new NodeAcceptance(RunUnit, Tip1, digest, "deploy", 1, 1) { Subject = "unit-b1" };
        Files.Write(new NodeRecord(RunUnit)
        {
            Statuses = new Dictionary<string, NodeStatus>
            {
                ["deploy"] = new(RunUnit, "deploy", NodeStates.Active) { Attempt = 1, Tries = 1, Binding = bound, Scope = new NodeScope(digest, RunId) },
            },
            Attempts = [Attempt("deploy", Plugin + "/deploy", AttemptStates.Sent) with { Progress = progress }],
        });
        var orphan = RunUnits.IdOf("q9");
        Files.Write(new NodeRecord(orphan)
        {
            Statuses = new Dictionary<string, NodeStatus> { ["deploy"] = new(orphan, "deploy", NodeStates.Active) { Attempt = 1, Tries = 1 } },
            Attempts = [Attempt("deploy", Plugin + "/deploy", AttemptStates.Sent, "i-orphan")],
        });
        _branchTips["daoris/s1"] = Tip1;
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId])];
        _port.Answer = _ => new(ReconcileStates.Running);
        return Runtime();
    }

    [Fact]
    public async Task A_governed_run_s_plugin_step_counts_while_its_plugin_is_up_and_an_orphaned_record_never_does()
    {
        var runtime = Governing();

        Assert.Equal(0, runtime.PluginStepsInFlight());

        await RunPassAsync(runtime);

        Assert.Equal(AttemptStates.Sent, Assert.Single(Files.Read(RunUnit)!.Attempts).State);
        Assert.Equal(1, runtime.PluginStepsInFlight());

        // The plugin removed, or crashing at its start: nothing asks after the step until it is up, so it holds nothing.
        _port.Generation = null;
        Assert.Equal(0, runtime.PluginStepsInFlight());
    }

    /// <summary>
    /// A release's stage waiting on its approver, or a deploy on its deployer, waits on a role's act outside the plugin, whoever holds that
    /// role: the count reads the role the progress names and never its holder, so the person's approval or an agent's is alike. A step
    /// whose progress says it goes on is in flight.
    /// </summary>
    [Theory]
    [InlineData("approver", 0)]
    [InlineData("deployer", 0)]
    [InlineData(null, 1)]
    public async Task A_step_whose_progress_names_the_role_it_waits_on_holds_nothing_and_one_going_on_does(string? role, int inFlight)
    {
        var runtime = Governing(new StepProgress(role, "Release-12: Production.", "https://example.test/release/12", "Production", _now));

        await RunPassAsync(runtime);

        var kept = Assert.Single(Files.Read(RunUnit)!.Attempts);
        Assert.Equal((AttemptStates.Sent, role), (kept.State, kept.Progress?.Actor));
        Assert.Equal(inFlight, runtime.PluginStepsInFlight());
    }

    /// <summary>
    /// A watch, a step whose plugin declares it acts on nothing outside (<c>perform.acts: false</c>), only observes: cut off by a swap it loses
    /// nothing, and its plugin watches again after the restart, so however long it runs it holds no update.
    /// </summary>
    [Fact]
    public async Task A_watch_that_acts_on_nothing_never_holds_the_install()
    {
        var watch = Step with { Id = Plugin + "/deploy", Kind = Plugin + "/deploy", Perform = Step.Perform! with { Acts = false } };
        var runtime = Governing();

        await runtime.PassAsync(RunLook() with { Steps = () => [watch, Build, Deployed] }, CancellationToken.None);

        Assert.Equal(AttemptStates.Sent, Assert.Single(Files.Read(RunUnit)!.Attempts).State);
        Assert.Equal(0, runtime.PluginStepsInFlight());
    }

    /// <summary>
    /// An attempt whose outcome is not known holds an update for <see cref="PluginStepsInFlight.UnknownBound"/> from when it was first counted
    /// so, then the install goes on: the restart asks its plugin after it, as it would after any stop.
    /// </summary>
    [Fact]
    public async Task An_attempt_not_known_holds_the_install_for_a_bounded_time()
    {
        var runtime = Governing();
        await RunPassAsync(runtime);
        var record = Files.Read(RunUnit)!;
        Files.Write(record with { Attempts = [record.Attempts[0] with { State = AttemptStates.Unknown }] });

        Assert.Equal(1, runtime.PluginStepsInFlight());
        _now += PluginStepsInFlight.UnknownBound - TimeSpan.FromMinutes(1);
        Assert.Equal(1, runtime.PluginStepsInFlight());
        _now += TimeSpan.FromMinutes(2);
        Assert.Equal(0, runtime.PluginStepsInFlight());

        // Sent again and answered going on, it holds again, and a later unknown has its own bound.
        Files.Write(record);
        Assert.Equal(1, runtime.PluginStepsInFlight());
    }
}

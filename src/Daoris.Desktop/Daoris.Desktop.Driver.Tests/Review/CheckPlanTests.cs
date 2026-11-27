using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A check asked with no gate and no command runs the repository's required hand-back commands, in its declaration's order and each in its
/// folder, or its heavy gates where it requires none; and where it has nothing to run, the ask is answered when the broker takes it, before
/// the queue or the machine's admission, with what the agent may run instead.
/// </summary>
public sealed class CheckPlanTests : IDisposable
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-26T10:00:00Z");

    private readonly string _home = Path.Combine(AppContext.BaseDirectory, "check-plan-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string _tree;

    public CheckPlanTests()
    {
        _tree = Path.Combine(_home, "tree");
        Directory.CreateDirectory(Path.Combine(_tree, "web"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private void Declare(string gates) => File.WriteAllText(Path.Combine(_tree, DeclarationNames.Gates), "{ \"gates\": [" + gates + "] }");

    private static CheckAsk Ask(string id, int minute = 0, string? gate = null) => new(id, "s1", gate, null, At.AddMinutes(minute));

    private sealed record Ran(string? Gate, string? Line, IReadOnlyList<string>? Command, string Where);

    private CheckBroker Broker(List<Ran> ran, int exit = 0, int? failAt = null) => new(_home, (_, _) => Task.FromResult<string?>(_tree), async (ask, world, output, _) =>
    {
        ran.Add(new Ran(ask.Gate, ask.Line, ask.Command, world.Directory));
        await output.WriteLineAsync($"ran {ask.Line ?? ask.Gate}");
        return failAt is { } at && ran.Count == at ? exit : failAt is null ? exit : 0;
    });

    private async Task<CheckState> RunOneAsync(CheckBroker broker, string id = "a1")
    {
        await broker.RunAsync(CancellationToken.None);
        return new CheckFiles(_home).ReadState(id)!;
    }

    [Fact]
    public async Task A_check_with_no_command_runs_the_required_handback_commands_in_order_each_in_its_folder()
    {
        Declare("""
            { "name": "build", "run": "npm run build", "heavy": true },
            { "name": "cli", "run": "npm run verify:checks", "handback": true },
            { "name": "web", "run": "npm test", "cwd": "web", "handback": "npm test -- --run" },
            { "name": "lint", "run": "npm run lint" }
            """);
        new CheckFiles(_home).WriteAsk(Ask("a1"));
        var ran = new List<Ran>();

        var state = await RunOneAsync(Broker(ran));

        Assert.Equal(CheckStates.Reached, state.State);
        Assert.Equal(["npm run verify:checks", "npm test -- --run"], ran.Select(each => each.Line));
        Assert.Equal([_tree, Path.Combine(_tree, "web")], ran.Select(each => each.Where));
        Assert.All(ran, each => Assert.Null(each.Gate));
        Assert.Contains("ran npm test -- --run", state.Tail);
    }

    [Fact]
    public async Task A_check_running_its_handback_commands_runs_them_as_commands_and_holds_no_place_in_the_order_another_check_waits_in()
    {
        // The hand-back commands wait first come for the machine's places, as a command of the asker's own does: a turn they never ask at
        // would hold every check behind it and never say the check runs.
        Declare("""{ "name": "cli", "run": "npm run verify:checks", "handback": true }""");
        var files = new CheckFiles(_home);
        files.WriteAsk(Ask("a1"));
        files.WriteAsk(Ask("a2", minute: 1, gate: "tests"));
        var gateRan = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        AdmissionTurn? commandTurn = null;
        string? whileCommandRuns = null;
        var gateMayTry = false;
        var broker = new CheckBroker(_home, (_, _) => Task.FromResult<string?>(_tree), async (ask, world, _, _) =>
        {
            if (ask.Line is not null)
            {
                commandTurn = world.Turn;
                world.Admitted!();
                whileCommandRuns = files.ReadState("a1")?.State;
                await gateRan.Task;
                return 0;
            }

            gateMayTry = world.Turn!.MayTry(out _);
            world.Turn.Admitted();
            gateRan.TrySetResult();
            return 0;
        });

        await broker.RunAsync(CancellationToken.None);

        Assert.Null(commandTurn);
        Assert.Equal(CheckStates.Running, whileCommandRuns);
        Assert.True(gateMayTry);
        Assert.Equal((CheckStates.Reached, CheckStates.Reached), (files.ReadState("a1")!.State, files.ReadState("a2")!.State));
    }

    [Fact]
    public async Task The_first_required_command_that_fails_ends_the_check_and_the_rest_do_not_run()
    {
        Declare("""
            { "name": "cli", "run": "npm run verify:checks", "handback": true },
            { "name": "reached", "run": "node a.mjs", "handback": "node b.mjs" }
            """);
        new CheckFiles(_home).WriteAsk(Ask("a1"));
        var ran = new List<Ran>();

        var state = await RunOneAsync(Broker(ran, exit: 1, failAt: 1));

        Assert.Equal((CheckStates.Failed, 1), (state.State, state.Exit));
        Assert.Equal(["npm run verify:checks"], ran.Select(each => each.Line));
        Assert.Contains("failed at `npm run verify:checks` (exit 1).", CheckWaits.Words(Ask("a1"), state));
    }

    [Fact]
    public async Task A_repository_that_requires_no_handback_runs_its_heavy_gates_as_before()
    {
        Declare("""{ "name": "build", "run": "npm run build", "heavy": true }""");
        new CheckFiles(_home).WriteAsk(Ask("a1"));
        var ran = new List<Ran>();

        var state = await RunOneAsync(Broker(ran));

        Assert.Equal(CheckStates.Reached, state.State);
        var only = Assert.Single(ran);
        Assert.Null(only.Gate);
        Assert.Null(only.Line);
        Assert.Null(only.Command);
    }

    [Fact]
    public async Task A_check_asked_by_name_or_by_command_is_run_as_asked_even_where_the_repository_requires_handback_commands()
    {
        Declare("""{ "name": "cli", "run": "npm run verify:checks", "handback": true }, { "name": "tests", "run": "npm test", "heavy": true }""");
        var files = new CheckFiles(_home);
        files.WriteAsk(Ask("a1", gate: "tests"));
        files.WriteAsk(Ask("a2", minute: 1) with { Command = ["dotnet", "test"] });
        var ran = new List<Ran>();

        await Broker(ran).RunAsync(CancellationToken.None);

        Assert.Equal("tests", ran.Single(each => each.Gate is not null).Gate);
        Assert.Equal(["dotnet", "test"], ran.Single(each => each.Command is not null).Command);
        Assert.All(ran, each => Assert.Null(each.Line));
    }

    [Fact]
    public async Task A_check_with_nothing_to_run_is_refused_when_the_broker_reaches_it_and_nothing_runs()
    {
        Declare("""{ "name": "lint", "run": "npm run lint" }""");
        new CheckFiles(_home).WriteAsk(Ask("a1"));
        var ran = new List<Ran>();

        var state = await RunOneAsync(Broker(ran));

        Assert.Equal(CheckStates.Refused, state.State);
        Assert.Empty(ran);
        Assert.Contains("handback", state.Error);
        Assert.Contains("`command`", state.Error);
    }

    [Fact]
    public async Task An_empty_check_is_answered_at_once_while_another_check_still_runs()
    {
        // The repository declares no file at all: the second ask has nothing to run, and it is answered while the first is still running, with no slot.
        var files = new CheckFiles(_home);
        files.WriteAsk(Ask("a0", gate: "tests"));
        files.WriteAsk(Ask("a1", minute: 1));
        var answered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var broker = new CheckBroker(_home, (_, _) => Task.FromResult<string?>(_tree), async (_, _, _, _) =>
        {
            await answered.Task;
            return 0;
        })
        {
            Settled = state =>
            {
                if (state.Id == "a1") answered.TrySetResult();
            },
        };

        await broker.RunAsync(CancellationToken.None);

        Assert.Equal(CheckStates.Refused, files.ReadState("a1")!.State);
        Assert.Equal(CheckStates.Reached, files.ReadState("a0")!.State);
    }

    [Fact]
    public async Task A_refusal_is_said_to_the_session_with_the_reason_and_is_finished_not_retried()
    {
        var files = new CheckFiles(_home);
        files.WriteAsk(Ask("a1"));
        var answered = await RunOneAsync(Broker([]));
        var words = new List<string>();

        var handed = await CheckHanding.HandAsync(files, (_, text, _, _) =>
        {
            words.Add(text);
            return Task.FromResult<string?>("handed");
        }, CancellationToken.None);

        Assert.True(CheckStates.Finished(answered.State));
        Assert.Equal(["a1"], handed);
        Assert.Empty(CheckBroker.Pending(files.Asks(), files.ReadState, At.AddDays(1)));
        var said = Assert.Single(words);
        Assert.StartsWith("Daoris did not run the checks you asked for", said);
        Assert.Contains(answered.Error!, said);
    }

    [Fact]
    public async Task A_check_with_no_command_that_reached_has_verified_the_required_gates_it_ran_at_its_tip()
    {
        Declare("""
            { "name": "cli", "run": "npm run verify:checks", "handback": true },
            { "name": "web", "run": "npm test", "cwd": "web", "handback": true }
            """);
        var files = new CheckFiles(_home);
        files.WriteAsk(Ask("a1"));
        var state = await RunOneAsync(Broker([]));
        files.WriteState(state with { Tip = "abc123" });

        Assert.Equal(["npm run verify:checks", "npm test"], state.Ran);
        Assert.All(HandbackGates.OfFolder(_tree), gate => Assert.True(HandbackGates.Verified(files, gate, "abc123")));
        Assert.All(HandbackGates.OfFolder(_tree), gate => Assert.False(HandbackGates.Verified(files, gate, "def456")));
    }

    [Fact]
    public async Task A_check_with_no_command_verifies_no_gate_it_did_not_run_such_as_an_admission_gate_that_is_not_required_by_hand_back()
    {
        Declare("""
            { "name": "cli", "run": "npm run verify:checks", "handback": true },
            { "name": "universal", "run": "node tools/universal.mjs" }
            """);
        var files = new CheckFiles(_home);
        files.WriteAsk(Ask("a1"));
        var state = await RunOneAsync(Broker([]));
        files.WriteState(state with { Tip = "abc123" });
        var universal = DeclaredGates.Read(File.ReadAllText(Path.Combine(_tree, DeclarationNames.Gates))).Single(gate => gate.Name == "universal");

        Assert.False(HandbackGates.Verified(files, universal, "abc123"));
    }

    [Fact]
    public void A_no_command_check_that_reached_under_an_older_build_or_ran_heavy_gates_verifies_no_hand_back_gate()
    {
        Declare("""{ "name": "cli", "run": "npm run verify:checks", "handback": true }""");
        var files = new CheckFiles(_home);
        files.WriteAsk(Ask("a1"));
        files.WriteState(new CheckState("a1", CheckStates.Reached, At) { Tip = "abc123" });

        Assert.All(HandbackGates.OfFolder(_tree), gate => Assert.False(HandbackGates.Verified(files, gate, "abc123")));
    }

    [Fact]
    public void A_required_command_that_holds_a_placeholder_nothing_fills_is_refused_naming_the_gate()
    {
        Declare("""{ "name": "reached", "run": "node tools/reached-checks.mjs --base {base}", "handback": true }""");

        var plan = CheckPlans.Of(_tree, () => null);

        Assert.Contains("`reached`", plan.Empty);
        Assert.Contains("{base}", plan.Empty);
        Assert.Empty(plan.Handback);
    }

    [Fact]
    public async Task A_declaration_that_cannot_be_read_ends_that_ask_errored_and_the_next_ask_still_runs()
    {
        Declare("""{ "name": "cli", "run": "npm run verify:checks", "handback": true }""");
        var other = Path.Combine(_home, "other");
        Directory.CreateDirectory(other);
        File.WriteAllText(Path.Combine(other, DeclarationNames.Gates), "{ \"gates\": [{ \"name\": \"cli\", \"run\": \"npm run b\", \"handback\": true }] }");
        var files = new CheckFiles(_home);
        files.WriteAsk(Ask("a1") with { Session = "locked" });
        files.WriteAsk(Ask("a2", minute: 1));
        using var held = new FileStream(Path.Combine(_tree, DeclarationNames.Gates), FileMode.Open, FileAccess.Read, FileShare.None);
        var broker = new CheckBroker(_home, (session, _) => Task.FromResult<string?>(session == "locked" ? _tree : other), (_, _, _, _) => Task.FromResult(0));

        await broker.RunAsync(CancellationToken.None);

        Assert.Equal(CheckStates.Errored, files.ReadState("a1")!.State);
        Assert.Equal(CheckStates.Reached, files.ReadState("a2")!.State);
    }

    [Fact]
    public void A_check_whose_declaration_does_not_read_is_refused_with_why_and_a_mapping_makes_heavy_gates_to_run()
    {
        File.WriteAllText(Path.Combine(_tree, DeclarationNames.Gates), "{ not json");
        Assert.Contains("not valid JSON", CheckPlans.Of(_tree, () => null).Empty);

        File.Delete(Path.Combine(_tree, DeclarationNames.Gates));
        Assert.NotNull(CheckPlans.Of(_tree, () => null).Empty);
        var mapped = CheckPlans.Of(_tree, () => [new HeavyGate("all", "npm test", null, true)]);
        Assert.Null(mapped.Empty);
        Assert.True(mapped.Heavy);
    }

    [Fact]
    public async Task A_required_command_whose_folder_is_no_folder_of_the_tree_is_refused_naming_the_gate_and_nothing_runs()
    {
        Declare("""
            { "name": "cli", "run": "npm run verify:checks", "handback": true },
            { "name": "web", "run": "npm test", "cwd": "gone", "handback": true }
            """);
        new CheckFiles(_home).WriteAsk(Ask("a1"));
        var ran = new List<Ran>();

        var state = await RunOneAsync(Broker(ran));

        Assert.Equal(CheckStates.Refused, state.State);
        Assert.Contains("`web`", state.Error);
        Assert.Contains("`gone`", state.Error);
        Assert.Empty(ran);
    }

    [Theory]
    [InlineData(false, CheckStates.Queued)]
    [InlineData(true, CheckStates.Stopped)]
    public async Task A_check_cut_while_its_handback_command_waits_for_admission_is_queued_again_and_one_cut_after_it_is_stopped(bool admitted, string after)
    {
        Declare("""{ "name": "cli", "run": "npm run verify:checks", "handback": true }""");
        var files = new CheckFiles(_home);
        files.WriteAsk(Ask("a1"));
        using var end = new CancellationTokenSource();
        var settled = new List<CheckState>();
        var broker = new CheckBroker(_home, (_, _) => Task.FromResult<string?>(_tree), async (_, world, _, ct) =>
        {
            if (admitted) world.Admitted!();
            var cut = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await using (ct.Register(() => cut.TrySetResult()))
            {
                await end.CancelAsync();
                await cut.Task;
            }

            return 1;
        })
        {
            Settled = settled.Add,
        };

        await broker.RunAsync(end.Token);

        Assert.Equal(after, files.ReadState("a1")!.State);
        Assert.Equal(admitted, settled.Count == 1);
    }
}

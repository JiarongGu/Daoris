using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// HEAVYRUN1a (D180's HEAVYRUN1 note): <c>daoris-driver run-heavy [--gate &lt;name&gt;]</c>, run in a checkout, runs the gates its
/// <c>daoris.gates.json</c> marks <c>"heavy": true</c>, in declared order, or the one named. Its words, its choice of gates and its
/// refusals start no process, so they are the fast half's; the runs themselves are <see cref="HeavyRunProcessTests"/>'.
/// </summary>
public sealed class HeavyRunCommandTests : IDisposable
{
    private readonly HeavyFixture _fixture = new("heavy-run-command");

    public void Dispose() => _fixture.Dispose();

    public static TheoryData<string, string?, bool> AskRows() => new()
    {
        { "", null, false },
        { "--gate web", "web", false },
        // HEAVYSLOT1: the terminal's twin of a repository's *Heavy run* row, which runs nothing.
        { "--look", null, true },
    };

    [Theory]
    [MemberData(nameof(AskRows))]
    public void The_words_name_every_heavy_gate_or_one_or_a_look(string words, string? gate, bool look)
    {
        var ask = HeavyRun.Read(Words(words), out var problem);

        Assert.Null(problem);
        Assert.Equal(gate, ask?.Gate);
        Assert.Equal(look, ask?.Look);
    }

    [Theory]
    [InlineData("--gate", "`--gate` takes a gate's name.")]
    [InlineData("--gate --gate", "`--gate` takes a gate's name.")]
    [InlineData("web", "`web` is not a word `run-heavy` takes: it takes `--gate <name>`, `--look`, or nothing for every heavy gate.")]
    [InlineData("--gate web --gate e2e", "`run-heavy` runs one named gate, or every heavy one.")]
    [InlineData("--look --gate web", "`--look` says who holds the slot and runs nothing, so it takes no gate.")]
    public void Words_it_does_not_take_are_said(string words, string said)
    {
        Assert.Null(HeavyRun.Read(Words(words), out var problem));
        Assert.Equal(said, problem);
    }

    /// <summary>The heavy gates in the order the repository declares them; one named alone; a gate not marked heavy is no heavy gate.</summary>
    [Fact]
    public void The_heavy_gates_are_taken_in_declared_order_and_a_named_one_alone()
    {
        _fixture.Gates(_fixture.Checkout, ("lint", "npm run lint", null), ("build", "dotnet build", true), ("unit", "npm test", false), ("e2e", "npm run e2e", true));
        var declared = HeavyGates.Read(_fixture.Checkout);

        var every = HeavyGates.Choose(declared, null);
        Assert.Equal((0, (string?)null), (every.Exit, every.Refusal));
        Assert.Equal(["build", "e2e"], every.Gates.Select(gate => gate.Name));
        Assert.Equal("dotnet build", every.Gates[0].Run);

        var one = HeavyGates.Choose(declared, "e2e");
        Assert.Equal(["e2e"], one.Gates.Select(gate => gate.Name));
    }

    /// <summary>A gate named that is not marked heavy is refused, saying so: the slot is for the heavy ones.</summary>
    [Fact]
    public void A_named_gate_not_marked_heavy_is_refused_saying_so()
    {
        _fixture.Gates(_fixture.Checkout, ("lint", "npm run lint", null), ("build", "dotnet build", true));

        var refused = HeavyGates.Choose(HeavyGates.Read(_fixture.Checkout), "lint");

        Assert.Empty(refused.Gates);
        Assert.Equal(1, refused.Exit);
        Assert.Equal(
            "`lint` is not marked heavy in daoris.gates.json, so `run-heavy` does not run it: run it as you would, or mark it `\"heavy\": true`. Its heavy gates: build.",
            refused.Refusal);
    }

    /// <summary>A gate nobody declared, or a file that declares no heavy gate, is a tool error: there is nothing to run.</summary>
    [Fact]
    public void An_unknown_gate_or_no_heavy_gate_is_a_tool_error()
    {
        _fixture.Gates(_fixture.Checkout, ("lint", "npm run lint", null), ("unit", "npm test", false));
        var declared = HeavyGates.Read(_fixture.Checkout);

        var unknown = HeavyGates.Choose(declared, "nope");
        Assert.Equal(2, unknown.Exit);
        Assert.Equal("daoris.gates.json declares no gate `nope`; its gates: lint, unit.", unknown.Refusal);

        var none = HeavyGates.Choose(declared, null);
        Assert.Equal(2, none.Exit);
        Assert.Equal("daoris.gates.json marks no gate `\"heavy\": true`, so there is nothing for `run-heavy` to run.", none.Refusal);
    }

    /// <summary>The file is the contract, and a reading that cannot tell is a tool error, never a guess.</summary>
    [Fact]
    public void A_missing_file_or_one_that_does_not_read_is_a_tool_error_naming_why()
    {
        Assert.Contains("no daoris.gates.json", Assert.Throws<DriverException>(() => HeavyGates.Read(_fixture.Checkout)).Message);

        File.WriteAllText(Path.Combine(_fixture.Checkout, "daoris.gates.json"), "{ \"gates\": [ ");
        Assert.Contains("daoris.gates.json is not valid JSON", Assert.Throws<DriverException>(() => HeavyGates.Read(_fixture.Checkout)).Message);

        File.WriteAllText(Path.Combine(_fixture.Checkout, "daoris.gates.json"), """{ "gates": [ { "name": "build" } ] }""");
        Assert.Contains("needs both 'name' and 'run'", Assert.Throws<DriverException>(() => HeavyGates.Read(_fixture.Checkout)).Message);

        File.WriteAllText(Path.Combine(_fixture.Checkout, "daoris.gates.json"), """{ "gates": [ { "name": "build", "run": "x", "heavy": "yes" } ] }""");
        Assert.Contains("`build`'s \"heavy\" is true or false", Assert.Throws<DriverException>(() => HeavyGates.Read(_fixture.Checkout)).Message);
    }

    /// <summary>
    /// HEAVYRUN1b2c: whether the repository's own file wins, read-only. It marks some (their names), marks none, has no file, or does not
    /// read (the driver's words): the last is "cannot tell", never "marks none".
    /// </summary>
    [Fact]
    public void Marks_says_whether_the_file_wins_and_cannot_tell_is_never_marks_none()
    {
        var file = Path.Combine(_fixture.Checkout, "daoris.gates.json");

        var noFile = HeavyGates.Marks(_fixture.Checkout);
        Assert.False(noFile.Marks);
        Assert.Empty(noFile.Names);
        Assert.Null(noFile.Refusal);

        File.WriteAllText(file, """{ "gates": [ { "name": "build", "run": "x" }, { "name": "web", "run": "y", "heavy": true }, { "name": "e2e", "run": "z", "heavy": true } ] }""");
        var some = HeavyGates.Marks(_fixture.Checkout);
        Assert.True(some.Marks);
        Assert.Equal(["web", "e2e"], some.Names);

        File.WriteAllText(file, """{ "gates": [ { "name": "build", "run": "x", "heavy": false } ] }""");
        Assert.False(HeavyGates.Marks(_fixture.Checkout).Marks);

        File.WriteAllText(file, "{ \"gates\": [ ");
        var unreadable = HeavyGates.Marks(_fixture.Checkout);
        Assert.Null(unreadable.Marks);
        Assert.Contains("daoris.gates.json is not valid JSON", unreadable.Refusal);
    }

    /// <summary>A gate's folder is the repository's, where it names one: its <c>cwd</c>, as the devkit reads it.</summary>
    [Fact]
    public void A_gate_s_folder_is_its_cwd_under_the_checkout()
    {
        File.WriteAllText(
            Path.Combine(_fixture.Checkout, "daoris.gates.json"),
            """{ "gates": [ { "name": "web", "run": "npm test", "cwd": "src/web", "heavy": true }, { "name": "build", "run": "x", "heavy": true } ] }""");

        var gates = HeavyGates.Read(_fixture.Checkout);

        Assert.Equal("src/web", gates[0].WorkingDirectory);
        Assert.Null(gates[1].WorkingDirectory);
    }

    /// <summary>A run that finds nothing to run says why and exits 2, holding nothing.</summary>
    [Fact]
    public async Task A_run_with_nothing_to_run_says_why_and_holds_nothing()
    {
        // A tree reads its own copy of the file, as it reads its own copy of every other.
        _fixture.Gates(_fixture.Tree, ("lint", "npm run lint", null));
        var said = new SaidWriter();

        var exit = await HeavyRun.RunAsync(new HeavyRunAsk(null), World(_fixture.Tree), said, CancellationToken.None).WaitAsync(Poll.Patience);

        Assert.Equal(2, exit);
        Assert.Contains("run-heavy: daoris.gates.json marks no gate", said.Text);
        using var free = HeavySlot.TryTake(_fixture.Home, HeavySlot.KeyOf(_fixture.Checkout)!);
        Assert.NotNull(free);
    }

    /// <summary>
    /// 🔴 Never waits on itself. A heavy gate whose command runs <c>run-heavy</c> again is inside the run that holds the slot: its
    /// environment says so. Asked again for a gate already running in that run, it would run inside itself without end, so it is
    /// refused, saying so, at once, while the slot stays held by the run it is inside.
    /// </summary>
    [Fact]
    public async Task A_run_inside_a_run_already_running_its_gate_is_refused_at_once_and_never_waits_on_the_slot()
    {
        _fixture.Gates(_fixture.Tree, ("build", "dotnet build", true), ("e2e", "npm run e2e", true));
        var key = HeavySlot.KeyOf(_fixture.Tree)!;
        using var outer = HeavySlot.TryTake(_fixture.Home, key)!;
        var said = new SaidWriter();
        var marker = HeavyRun.Marker(key, ["build"]);

        var exit = await HeavyRun.RunAsync(new HeavyRunAsk(null), World(_fixture.Tree, (HeavyRun.Variable, marker)), said, CancellationToken.None)
            .WaitAsync(Poll.Patience);

        Assert.Equal(1, exit);
        Assert.Equal(
            "run-heavy: `build` is already running in the heavy run this one is inside, so it is not run again inside itself.",
            Assert.Single(said.Lines));
    }

    /// <summary>
    /// FABRIC-PIPELINE1b (the pipeline design §3.2's second layer): run from a tree a session holds, whose run's checks are light, it is
    /// refused at once, exit 1, saying the heavy gates are the verification's, and takes no slot. The session is read from git's own lock
    /// on the tree, whose reason names it, never from a variable the session could leave out.
    /// </summary>
    [Fact]
    public async Task From_a_tree_a_light_session_holds_it_is_refused_at_once_and_takes_no_slot()
    {
        _fixture.Gates(_fixture.Tree, ("lint", "npm run lint", null), ("build", "dotnet build", true));
        Locked("daoris session s-1");
        SessionChecks.Hold(_fixture.Home, "s-1", SessionChecks.Light);
        var said = new SaidWriter();

        var exit = await HeavyRun.RunAsync(new HeavyRunAsk(null), World(_fixture.Tree), said, CancellationToken.None).WaitAsync(Poll.Patience);

        Assert.Equal(1, exit);
        Assert.Equal(
            "run-heavy: this tree is session `s-1`'s, and its development pipeline is light: the repository's heavy gates run in its "
            + "verification after the work lands, never in a session's tree. Run the checks your change reaches, filtered, instead.",
            Assert.Single(said.Lines));
        using var free = HeavySlot.TryTake(_fixture.Home, HeavySlot.KeyOf(_fixture.Checkout)!);
        Assert.NotNull(free);
    }

    /// <summary>
    /// A person's run, from a checkout no session holds, runs as today; so does one from a tree a session holds whose checks are not
    /// light, or kept nothing, and one held by a lock that names no session (a person's, an opening's).
    /// </summary>
    [Theory]
    [InlineData(null, null)]
    [InlineData("daoris session s-1", SessionChecks.HeavyAtEnd)]
    [InlineData("daoris session s-1", null)]
    [InlineData("on a portable disk", SessionChecks.Light)]
    public async Task From_a_tree_no_light_session_holds_it_runs_as_today(string? reason, string? checks)
    {
        _fixture.Gates(_fixture.Tree, ("lint", "npm run lint", null), ("build", "dotnet build", true));
        if (reason is not null) Locked(reason);
        SessionChecks.Hold(_fixture.Home, "s-1", checks);
        var said = new SaidWriter();

        // A gate not marked heavy is refused by the gates' own rule: the run got past the session's checks to its choice of gates.
        var exit = await HeavyRun.RunAsync(new HeavyRunAsk("lint"), World(_fixture.Tree), said, CancellationToken.None).WaitAsync(Poll.Patience);

        Assert.Equal(1, exit);
        Assert.StartsWith("run-heavy: `lint` is not marked heavy", Assert.Single(said.Lines));
    }

    /// <summary>The checkout itself is never a session's tree: git locks only a linked tree, so a run there is the person's.</summary>
    [Fact]
    public async Task From_the_checkout_itself_it_runs_as_today()
    {
        _fixture.Gates(_fixture.Checkout, ("lint", "npm run lint", null));
        Locked("daoris session s-1");
        SessionChecks.Hold(_fixture.Home, "s-1", SessionChecks.Light);
        var said = new SaidWriter();

        var exit = await HeavyRun.RunAsync(new HeavyRunAsk(null), World(_fixture.Checkout), said, CancellationToken.None).WaitAsync(Poll.Patience);

        Assert.Equal(2, exit);
        Assert.Contains("run-heavy: daoris.gates.json marks no gate", said.Text);
    }

    /// <summary>The tree's lock as <c>git worktree lock --reason</c> writes it: the reason and a line's end, in its folder under the checkout's.</summary>
    private void Locked(string reason) =>
        File.WriteAllText(Path.Combine(_fixture.Checkout, ".git", "worktrees", "engine-tree", "locked"), reason + "\n");

    /// <summary>The marker a run hands its gates names its slot and the gates running, outermost first, and reads back as written.</summary>
    [Fact]
    public void The_marker_names_the_slot_and_the_gates_running_and_reads_back()
    {
        var marker = HeavyRun.ReadMarker(HeavyRun.Marker("0123abcd0123abcd", ["build", "web"]));

        Assert.Equal("0123abcd0123abcd", marker?.Slot);
        Assert.Equal(["build", "web"], marker?.Gates);
        Assert.Null(HeavyRun.ReadMarker(null));
        Assert.Null(HeavyRun.ReadMarker("not a marker"));
    }

    /// <summary>
    /// HEAVYRUN1b2a: where the repository's own file marks no heavy gate, or it has none, this machine's mapping for it is what a run
    /// takes, every gate of it heavy by being there; where its own file marks one, its own win and the mapping is never read.
    /// </summary>
    [Fact]
    public void A_repository_that_marks_none_takes_this_machine_s_mapping_and_its_own_marks_win()
    {
        IReadOnlyList<HeavyGate> mapped = [new("build", "dotnet build", null, true), new("web", "npm test", "web", true)];

        var none = HeavyGates.Resolve(_fixture.Checkout, "engine", () => mapped);
        Assert.True(none.Mapped);
        Assert.Equal(["build", "web"], HeavyGates.Choose(none, null).Gates.Select(gate => gate.Name));
        Assert.Equal(["web"], HeavyGates.Choose(none, "web").Gates.Select(gate => gate.Name));
        var unknown = HeavyGates.Choose(none, "lint");
        Assert.Equal((2, "this machine's heavy gates for `engine` name no gate `lint`; they are: build, web."), (unknown.Exit, unknown.Refusal));

        _fixture.Gates(_fixture.Checkout, ("lint", "npm run lint", null), ("unit", "npm test", false));
        Assert.True(HeavyGates.Resolve(_fixture.Checkout, "engine", () => mapped).Mapped);

        _fixture.Gates(_fixture.Checkout, ("lint", "npm run lint", null), ("e2e", "npm run e2e", true));
        var own = HeavyGates.Resolve(_fixture.Checkout, "engine", () => throw new InvalidOperationException("the mapping is read only where the file marks none"));
        Assert.False(own.Mapped);
        Assert.Equal(["e2e"], HeavyGates.Choose(own, null).Gates.Select(gate => gate.Name));
    }

    /// <summary>
    /// With neither its own marks nor a mapping there is nothing to run, and the refusal says where each is set; a file that does not
    /// read is refused whatever this machine sets, since "cannot tell" is never "marks none".
    /// </summary>
    [Fact]
    public void With_neither_its_own_marks_nor_a_mapping_the_refusal_says_where_each_is_set()
    {
        var missing = Assert.Throws<DriverException>(() => HeavyGates.Resolve(_fixture.Checkout, "engine", () => null));
        Assert.Equal(
            $"no daoris.gates.json at {_fixture.Checkout}, and this machine sets no heavy gates for `engine`: `run-heavy` runs the gates a "
            + "repository marks heavy there, or those `daoris-driver heavy engine <name>=<command>…` sets here.",
            missing.Message);

        _fixture.Gates(_fixture.Checkout, ("lint", "npm run lint", null));
        var none = HeavyGates.Choose(HeavyGates.Resolve(_fixture.Checkout, "engine", () => null), null);
        Assert.Equal(
            (2, "daoris.gates.json marks no gate `\"heavy\": true`, and this machine sets no heavy gates for `engine`, so there is nothing for `run-heavy` to run."),
            (none.Exit, none.Refusal));

        File.WriteAllText(Path.Combine(_fixture.Checkout, "daoris.gates.json"), "{ not json");
        Assert.Throws<DriverException>(() => HeavyGates.Resolve(_fixture.Checkout, "engine", () => [new HeavyGate("build", "x", null, true)]));
    }

    /// <summary>
    /// A run reads the mapping from its home's driver file, under the repository its session names (the variable every driven session
    /// is handed), else under the folder its repository's git folder is in, whichever tree it runs in.
    /// </summary>
    [Fact]
    public async Task A_run_reads_the_mapping_under_its_session_s_repository_else_its_folder_s()
    {
        File.WriteAllText(
            DaorisHome.Entry(_fixture.Home, HomeEntry.DriverConfig),
            """{"heavyGates":{"engine":[{"name":"build","run":"dotnet build"}],"shop":[{"name":"web","run":"npm test"}]}}""");

        var byFolder = new SaidWriter();
        Assert.Equal(2, await HeavyRun.RunAsync(new HeavyRunAsk("nope"), World(_fixture.Tree), byFolder, CancellationToken.None).WaitAsync(Poll.Patience));
        Assert.Equal("run-heavy: this machine's heavy gates for `engine` name no gate `nope`; they are: build.", Assert.Single(byFolder.Lines));

        var bySession = new SaidWriter();
        var exit = await HeavyRun.RunAsync(new HeavyRunAsk("nope"), World(_fixture.Tree, (HeavyRun.RepositoryVariable, "shop")), bySession, CancellationToken.None)
            .WaitAsync(Poll.Patience);
        Assert.Equal(2, exit);
        Assert.Equal("run-heavy: this machine's heavy gates for `shop` name no gate `nope`; they are: web.", Assert.Single(bySession.Lines));
    }

    /// <summary>A driver file that does not read, where the run needs its mapping, is a tool error naming why: nothing is guessed.</summary>
    [Fact]
    public async Task A_run_whose_driver_file_does_not_read_is_refused_where_it_needs_the_mapping()
    {
        File.WriteAllText(DaorisHome.Entry(_fixture.Home, HomeEntry.DriverConfig), "{ not json");
        var said = new SaidWriter();

        Assert.Equal(2, await HeavyRun.RunAsync(new HeavyRunAsk(null), World(_fixture.Tree), said, CancellationToken.None).WaitAsync(Poll.Patience));
        Assert.Contains("is not readable JSON", said.Text);
    }

    /// <summary>The host answers the verb, the usage census (DriverCommandTests) checks its line, and it holds no home lease for the minutes it runs.</summary>
    [Fact]
    public void The_host_routes_the_verb_and_its_usage_names_it()
    {
        Assert.Equal("usage: daoris-driver run-heavy [--gate <name> | --look]", HeavyRun.Usage);

        var program = File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Host", "Program.cs"));
        Assert.Contains("if (args is [\"run-heavy\", .. var heavyArgs])", program);
        Assert.Contains("Daoris.Driver.Host.HeavyConsole.RunAsync(heavyArgs)", program);
        Assert.Contains("//   run-heavy [--gate <name> | --look]", program);
        Assert.Contains("if (ask.Look) return HeavyRun.Look(", File.ReadAllText(
            Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Host", "HeavyConsole.cs")));
    }

    /// <summary>
    /// HEAVYSLOT1 (the UI/UX design §3.5): <c>--look</c> says the slot as the page reads it, its holder at its gate and who waits, a run
    /// from a terminal named as one, and a holder whose process is gone as no run at all; it runs nothing.
    /// </summary>
    [Fact]
    public void A_look_says_the_holder_at_its_gate_and_who_waits_and_no_run_where_the_holder_is_gone()
    {
        var key = HeavySlot.KeyOf(_fixture.Checkout)!;
        var since = DateTimeOffset.Parse("2026-10-20T09:00:00Z");
        using var slot = HeavySlot.TryTake(_fixture.Home, key)!;
        slot.Keep(new HeavyHolder(4242, 7, _fixture.Checkout, ["build", "web", "e2e"], since, "s-12", null)
        {
            Now = new HeavyGateNow("web", 1, since + TimeSpan.FromMinutes(4)),
        });
        Directory.CreateDirectory(HeavySlot.WaitersOf(_fixture.Home, key));
        File.WriteAllText(
            Path.Combine(HeavySlot.WaitersOf(_fixture.Home, key), "5151-9.json"),
            HeavySlot.SerializeWaiter(new HeavyWaiter(5151, 9, _fixture.Tree, since + TimeSpan.FromMinutes(3), null)));
        var world = World(_fixture.Tree) with
        {
            Waiting = new HeavyWaiting(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), () => since + TimeSpan.FromMinutes(6)),
        };

        var said = new StringWriter();
        Assert.Equal(0, HeavyRun.Look(world, said, (_, _) => true));
        Assert.Equal(
            "run-heavy: `engine`'s heavy run: session s-12 (process 4242), 2 of 3, `web` for 2m 0s; 6m 0s in all.\n"
            + "run-heavy: waits: a run from a terminal (process 5151), for 3m 0s.\n",
            said.ToString().ReplaceLineEndings("\n"));

        var gone = new StringWriter();
        Assert.Equal(0, HeavyRun.Look(world, gone, (pid, _) => pid != 4242));
        Assert.StartsWith("run-heavy: no heavy run of `engine` now.", gone.ToString());
    }

    private HeavyRunWorld World(string directory, params (string Name, string Value)[] variables) =>
        new(_fixture.Home, directory, name => variables.FirstOrDefault(variable => variable.Name == name).Value);

    private static string[] Words(string words) => words.Length == 0 ? [] : words.Split(' ');
}

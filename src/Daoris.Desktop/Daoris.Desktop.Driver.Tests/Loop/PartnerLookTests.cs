using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// COAGENT1b (D199 points 2 to 4; the co-agent design §2.3, §7.2): the look's partner pieces, in process. A person's ask on file is
/// judged, reserved, prepared and started beside the look; the state names the copy before it is made and the record and the job
/// before the turn; the end is written with the keeper's moment only once the pass is back, which is after its job was waited empty;
/// the keeper's result moves the piece to its answer, held or refused; and a restart reconciles a piece from what it observes. The pass
/// and the keeper are stand-ins here (the real ones are the <c>Process</c> and git halves'), so nothing starts a process (MOD8).
/// </summary>
public sealed class PartnerLookTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-partner-look-" + Guid.NewGuid().ToString("N")[..8]);

    private const string Partner = "pt000001";

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-22T10:00:00Z");

    public PartnerLookTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private PartnerFiles Files => new(_home);

    private void Ask(string id, string asker = "s1", IReadOnlyList<string>? paths = null) =>
        Files.WriteAsk(new PartnerAsk(id, asker, "Make the window seven days.", paths ?? ["src"], "codex-acp", OpinionStandIn.Tip, ["src/window.ts"], PartnerAsk.ByPerson, At));

    /// <summary>The stand-in pass: it names its record and job at once, as the real one does once the host opened it, then ends.</summary>
    private sealed class Pass(PartnerFiles files)
    {
        public List<PartnerPassAsk> Asked { get; } = [];
        public List<PartnerState?> SeenAtTurn { get; } = [];
        public List<PartnerState?> SeenAtStart { get; } = [];
        public string? Refused { get; init; }
        public string? LastWords { get; init; }
        public string End { get; init; } = PartnerEnds.Answered;

        public Task<PartnerPassRun> RunAsync(PartnerPassAsk ask, ReviewerChoice choice, CancellationToken ct)
        {
            Asked.Add(ask);
            SeenAtStart.Add(files.ReadState(ask.Ask.Id));
            if (Refused is { } code) return Task.FromResult(new PartnerPassRun(null, "held") { Code = code });
            ask.Opened?.Invoke(Partner, PieceJobs.NameOf(ask.Ask.Id));
            SeenAtTurn.Add(files.ReadState(ask.Ask.Id));
            return Task.FromResult(new PartnerPassRun(Partner, $"completed  partner {Partner}") { End = End, LastWords = LastWords });
        }
    }

    /// <summary>The stand-in keeper: what it is told to say, and the state it was handed.</summary>
    private sealed class Keeper
    {
        public List<PartnerState> Handed { get; } = [];
        public PieceKept Kept { get; init; } = new(
            PartnerStates.Imported, "c0ffee0000000000000000000000000000000000", null,
            new PieceImport.Judged([(new PieceEntry("src/report.ts", PieceEntryKind.File, 3, null), "100644"), (new PieceEntry("README.md", PieceEntryKind.File, 3, null), "100644")], ["old.md"], [new PieceLeftOut("dist/out.js", PieceImport.WhyIgnored)], false),
            null);

        public Task<PieceKept> KeepAsync(PartnerAsk ask, PartnerState state, CancellationToken ct)
        {
            Handed.Add(state);
            return Task.FromResult(Kept);
        }
    }

    private Daoris.Driver.Driver Driver(OpinionStandIn host, Pass? pass, Keeper? keeper = null, int cap = 3, ReviewerChoice? choice = null) =>
        new(host.Client(), DriverConfig.Empty with { Adapter = "claude-code-acp", Cap = cap }, AdapterSet.Built(), _home)
        {
            PartnerPasses = pass is null ? (_, _, _) => throw new InvalidOperationException("no partner starts here") : pass.RunAsync,
            PartnerKeeps = (keeper ?? new Keeper()).KeepAsync,
            PartnerTrees = (_, _) => Task.FromResult<string?>("D:/checkouts/reports"),
            PartnerChoices = (_, _, _) => Task.FromResult(choice ?? new ReviewerChoice("codex-acp", "another-maker", null, "codex-acp writes it.")),
        };

    private static OpinionStandIn Host() => new OpinionStandIn().Session("s1", "working", tree: "trees/default/reports/s-1");

    private async Task Until(Func<bool> done)
    {
        var until = DateTime.UtcNow.AddSeconds(15);
        while (!done())
        {
            if (DateTime.UtcNow > until)
            {
                var folder = DaorisHome.Entry(_home, HomeEntry.Partners);
                var held = string.Join(" | ", Directory.GetFiles(folder).Select(file => $"{Path.GetFileName(file)}: {File.ReadAllText(file).ReplaceLineEndings(" ")}"));
                throw new TimeoutException($"the piece did not settle. {held}");
            }

            await Task.Delay(20);
        }
    }

    private async Task<(int Begun, List<string> Events)> Look(Daoris.Driver.Driver driver, int taken = 0)
    {
        var events = new List<string>();
        var begun = await driver.PiecesAsync(new Snapshot([], [], []) { Pieces = Files.Reservations() }, taken, events, CancellationToken.None);
        return (begun, events);
    }

    /// <summary>
    /// 🔴 A person's ask goes approved, preparing naming its copy, started naming its record and job before the turn, ending with the
    /// keeper's moment and the end, imported at the keeper's commit, answered with the facts Daoris read; one slot taken at the look.
    /// </summary>
    [Fact]
    public async Task A_persons_piece_goes_from_its_ask_to_its_answer_each_state_after_its_side_effect()
    {
        Ask("p1", paths: ["src"]);
        var pass = new Pass(Files) { LastWords = "Changed the window." };
        var keeper = new Keeper();

        var (begun, _) = await Look(Driver(Host(), pass, keeper));
        await Until(() => Files.ReadState("p1") is { State: PartnerStates.Answered });

        Assert.Equal(1, begun);
        var preparing = Assert.Single(pass.SeenAtStart)!;
        Assert.Equal((PartnerStates.Preparing, "codex-acp"), (preparing.State, preparing.Agent));
        Assert.Equal(PartnerPass.CopyFor(_home, null, "reports", "p1"), preparing.Copy);
        Assert.Equal(preparing.Copy, Assert.Single(pass.Asked).Copy);
        var started = Assert.Single(pass.SeenAtTurn)!;
        Assert.Equal((PartnerStates.Started, Partner, PieceJobs.NameOf("p1")), (started.State, started.Partner, started.Job));
        var ending = Assert.Single(keeper.Handed);
        Assert.Equal((PartnerStates.Ending, PartnerEnds.Answered), (ending.State, ending.End));
        Assert.NotNull(ending.Kept);

        var answered = Files.ReadState("p1")!;
        Assert.Equal(("c0ffee0000000000000000000000000000000000", true), (answered.Commit, answered.CopyRemoved));
        var facts = Files.ReadFacts("p1")!;
        Assert.Equal(["README.md", "old.md", "src/report.ts"], facts.Files);
        Assert.Equal(["README.md", "old.md"], facts.Beyond);
        Assert.Equal("dist/out.js", Assert.Single(facts.LeftOut).Path);
        var answer = Files.ReadAnswer("p1")!;
        Assert.Equal(("Changed the window.", true), (answer.Summary, answer.LastWords));
    }

    /// <summary>
    /// 🔴 The hand (the design §2.3 step 9, §7.6): a running lead takes nothing; once it ended, the answered piece is handed once, its
    /// receipt claimed for the lead and taken naming it, the state `handed`, in fixed words naming the agent, the facts and the line that
    /// takes it in; a later look hands nothing again.
    /// </summary>
    [Fact]
    public async Task An_answered_piece_is_handed_once_to_its_ended_lead_by_its_receipt()
    {
        Ask("p1", paths: ["src"]);
        var host = Host().Session(Partner, "completed", tree: "trees/default/reports/p-p1", adapter: "codex-acp", kind: "chat");
        host.Set(Partner, "serves", "s1");
        host.Set(Partner, "piece", "p1");
        var driver = Driver(host, new Pass(Files) { LastWords = "Changed the window." });

        await Look(driver);
        await Until(() => Files.ReadState("p1") is { State: PartnerStates.Answered });
        Assert.Empty(host.PartnerHands);
        Assert.Null(Files.ReadReceipt("p1"));

        host.Move("s1", "completed");
        var (_, events) = await Look(driver);
        await Until(() => Files.ReadState("p1") is { State: PartnerStates.Handed });

        var (partner, to, text) = Assert.Single(host.PartnerHands);
        Assert.Equal((Partner, "s1"), (partner, to));
        Assert.Contains("`codex-acp`", text);
        Assert.Contains("Changed the window.", text);
        Assert.Contains("claim to check against the commit", text);
        Assert.Contains("2 beyond its piece's paths: `README.md`, `old.md`", text);
        Assert.Contains($"git merge --no-ff {PieceRefs.Of("p1")}", text);
        var receipt = Files.ReadReceipt("p1")!;
        Assert.Equal(("s1", PartnerDelivery.KeyOf("p1"), PartnerDelivery.Taken), (receipt.To, receipt.Key, receipt.Outcome));
        Assert.Contains(events, line => line.Contains("is handed to session s1"));

        await Look(driver);
        Assert.Single(host.PartnerHands);
    }

    /// <summary>
    /// 🔴 A hand a restart cut (its receipt attempted, the state still `answered`) may have reached the lead: it is uncertain, waits for
    /// the person, and nothing hands it again by itself.
    /// </summary>
    [Fact]
    public async Task A_hand_a_restart_cut_is_uncertain_and_never_handed_again_by_itself()
    {
        Ask("p1");
        Files.WriteState(new PartnerState("p1", PartnerStates.Answered, null, At) { Partner = Partner, Commit = "c0ffee", Agent = "codex-acp", CopyRemoved = true });
        Files.WriteReceipt(new PartnerReceipt("p1", "s1", PartnerDelivery.KeyOf("p1"), PartnerDelivery.Attempted, At));
        var host = new OpinionStandIn().Session("s1", "completed", tree: "trees/default/reports/s-1").Session(Partner, "completed", kind: "chat");
        host.Set(Partner, "serves", "s1");
        host.Set(Partner, "piece", "p1");
        var driver = Driver(host, pass: null);

        await Look(driver);
        await Look(driver);

        Assert.Empty(host.PartnerHands);
        Assert.Equal(PartnerDelivery.Uncertain, Files.ReadReceipt("p1")!.Outcome);
        Assert.Equal(PartnerStates.Answered, Files.ReadState("p1")!.State);
    }

    /// <summary>A lead the host does not know, and one that serves another itself, are refused by their codes and nothing starts.</summary>
    [Fact]
    public async Task A_gone_or_unfit_lead_is_refused_and_nothing_starts()
    {
        Ask("p1", asker: "nobody");
        Ask("p2", asker: "s2");
        var host = Host().Session("s2", "working", tree: "trees/default/reports/s-2");
        host.Set("s2", "serves", "s1");

        await Look(Driver(host, pass: null));

        Assert.Equal((PartnerStates.Refused, PartnerWhy.AskerGone), (Files.ReadState("p1")!.State, Files.ReadState("p1")!.Code));
        Assert.Equal((PartnerStates.Refused, PartnerWhy.AskerUnfit), (Files.ReadState("p2")!.State, Files.ReadState("p2")!.Code));
    }

    /// <summary>🔴 D199 point 4: a choice the sheet refuses (`not-measured`) refuses the piece, and nothing is reserved or started.</summary>
    [Fact]
    public async Task An_agent_not_measured_is_refused_at_the_persons_own_ask()
    {
        Ask("p1");
        var driver = Driver(Host(), pass: null, choice: new ReviewerChoice(null, null, PartnerWhy.NotMeasured, "codex-acp is not admitted."));

        var (begun, _) = await Look(driver);

        Assert.Equal(0, begun);
        Assert.Equal((PartnerStates.Refused, PartnerWhy.NotMeasured), (Files.ReadState("p1")!.State, Files.ReadState("p1")!.Code));
    }

    /// <summary>A full cap leaves the piece waiting with its ask kept, and a later look with a slot reserves it.</summary>
    [Fact]
    public async Task A_full_cap_waits_and_reserves_nothing()
    {
        Ask("p1");

        var (begun, _) = await Look(Driver(Host(), pass: null, cap: 1), taken: 1);

        Assert.Equal(0, begun);
        Assert.Equal((PartnerStates.Waiting, "cap"), (Files.ReadState("p1")!.State, Files.ReadState("p1")!.Code));
    }

    /// <summary>A pass that could not join its job starts nothing: the piece is refused `no-job`, which releases its reservation.</summary>
    [Fact]
    public async Task A_pass_that_cannot_join_its_job_is_refused_and_releases_its_slot()
    {
        Ask("p1");
        var pass = new Pass(Files) { Refused = PartnerWhy.NoJob };

        await Look(Driver(Host(), pass));
        await Until(() => Files.ReadState("p1") is { State: PartnerStates.Refused });

        Assert.Equal(PartnerWhy.NoJob, Files.ReadState("p1")!.Code);
        Assert.False(PartnerStates.Reserves(Files.ReadState("p1")!.State));
    }

    /// <summary>A refused import holds the piece with its copy kept for the person; no work refuses it `no-work`.</summary>
    [Fact]
    public async Task A_refused_import_is_held_and_no_work_is_refused()
    {
        Ask("p1");
        Ask("p2");
        var judged = new PieceImport.Judged([], [], [new PieceLeftOut("link", PieceImport.WhyLink)], false);

        await Look(Driver(Host(), new Pass(Files), new Keeper { Kept = new(PartnerStates.Held, null, PartnerWhy.ImportRefused, judged, null) }));
        await Until(() => Files.ReadState("p1") is { State: PartnerStates.Held } && Files.ReadState("p2") is { State: PartnerStates.Held });

        Assert.Equal(PartnerWhy.ImportRefused, Files.ReadState("p1")!.Code);
        Assert.False(Files.ReadState("p1")!.CopyRemoved);
        Assert.True(PartnerStates.HoldsCopy(Files.ReadState("p1")!));

        Ask("p3");
        await Look(Driver(Host(), new Pass(Files), new Keeper { Kept = new(null, null, PartnerWhy.NoWork, new([], [], [], false), null) }));
        await Until(() => Files.ReadState("p3") is { State: PartnerStates.Refused });
        Assert.Equal(PartnerWhy.NoWork, Files.ReadState("p3")!.Code);
    }

    /// <summary>
    /// 🔴 A restart (the design §7.2): a `started` piece whose named job no name answers is uncertain, its copy untouched and no agent run;
    /// an `ending` piece with its keeper's moment is kept at the next look with that same moment.
    /// </summary>
    [Fact]
    public async Task A_restart_reads_an_unknown_job_as_uncertain_and_keeps_an_ending_piece_with_its_moment()
    {
        Ask("p1");
        Ask("p2");
        var copy = PartnerPass.CopyFor(_home, null, "reports", "p1");
        Files.WriteState(new PartnerState("p1", PartnerStates.Started, null, At) { Copy = copy, Partner = Partner, Job = PieceJobs.NameOf("p1-gone-" + Guid.NewGuid().ToString("N")[..6]) });
        var moment = At.AddMinutes(5);
        Files.WriteState(new PartnerState("p2", PartnerStates.Ending, null, At) { Copy = PartnerPass.CopyFor(_home, null, "reports", "p2"), Kept = moment, End = PartnerEnds.Stopped });
        var keeper = new Keeper();

        await Look(Driver(Host(), pass: null, keeper));
        await Until(() => Files.ReadState("p2") is { State: PartnerStates.Answered });

        Assert.Equal((PartnerStates.Uncertain, PartnerWhy.JobUnknown), (Files.ReadState("p1")!.State, Files.ReadState("p1")!.Code));
        Assert.Equal(moment, Assert.Single(keeper.Handed).Kept);
    }
}

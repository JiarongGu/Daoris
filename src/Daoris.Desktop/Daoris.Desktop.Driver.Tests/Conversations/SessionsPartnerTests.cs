using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// COAGENT1b (D199 point 2; the co-agent design §6.4): the person's terminal door to a piece, <c>daoris-driver sessions partner</c>. Its
/// ask is written by the one judge and writer the page's press shares (<see cref="PartnerPersonAsk"/>), as the person's, at the exact
/// commit the lead's tree stands at with its uncommitted paths, for the driver's look to take; its list reads the box; its stop is the
/// partner's record's stop. The tree is read by a stand-in, so nothing starts a process (MOD8).
/// </summary>
public sealed class SessionsPartnerTests : IDisposable
{
    private const string Head = "3333333333333333333333333333333333333333";

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-sessions-partner-" + Guid.NewGuid().ToString("N")[..8]);

    public SessionsPartnerTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private PartnerFiles Files => new(_home);

    private static SessionsAsk Read(params string[] words)
    {
        var ask = SessionsCommand.Read(words, out var problem);
        Assert.Null(problem);
        return ask!;
    }

    [Fact]
    public void The_verb_reads_a_session_a_piece_its_agent_and_its_paths_or_a_list_or_a_stop()
    {
        var asked = Read("partner", "s1a2b3c4", "Make the window seven days.", "--agent", "codex-acp", "--paths", "src/window.ts", "docs");
        Assert.Equal(("partner", "s1a2b3c4", "Make the window seven days.", "codex-acp"), (asked.Verb, asked.Ids[0], asked.Text, asked.Agent));
        Assert.Equal(["src/window.ts", "docs"], asked.Paths);

        var before = Read("partner", "s1", "Write it.", "--paths", "src", "--agent", "codex-acp");
        Assert.Equal(["src"], before.Paths);
        Assert.Equal("codex-acp", before.Agent);

        Assert.Equal(("partner-list", "s1"), (Read("partner", "s1", "--list").Verb, Read("partner", "s1", "--list").Ids[0]));
        Assert.Equal(("partner-stop", "p1"), (Read("partner", "#p1", "--stop").Verb, Read("partner", "#p1", "--stop").Ids[0]));
    }

    [Theory]
    [InlineData("partner")]
    [InlineData("partner", "s1")]
    [InlineData("partner", "s1", "Write it.")]
    [InlineData("partner", "s1", "Write it.", "--agent")]
    [InlineData("partner", "s1", "Write it.", "--agent", "codex-acp", "--paths")]
    [InlineData("partner", "s1", "Write it.", "--agent", "codex-acp", "--agent", "stub")]
    [InlineData("partner", "s1", "Write it.", "--agent", "codex-acp", "--discard")]
    [InlineData("partner", "p1", "--discard")]
    [InlineData("partner", "p1", "--hand-over")]
    public void Anything_else_is_a_usage_problem(params string[] words)
    {
        Assert.Null(SessionsCommand.Read(words, out var problem));
        Assert.Contains("`partner` takes one session's id, the piece in quotes and `--agent <name>`", problem);
    }

    [Fact]
    public void The_usage_spells_the_verb_in_both_places_a_person_reads_it()
    {
        foreach (var usage in new[] { SessionsCommand.Usage, DriverCommand.Usage })
        {
            Assert.Contains("sessions partner <id> \"<piece>\" --agent <name> [--paths <path>…]", usage);
            Assert.Contains("sessions partner <id> --list  ·  sessions partner <piece> --stop", usage);
        }
    }

    private (SessionsWorld World, OpinionStandIn Host, List<string> Read) World(Action<OpinionStandIn>? set = null, string? head = Head)
    {
        var host = new OpinionStandIn().Session("s1", "working", tree: "trees/default/reports/s-1");
        set?.Invoke(host);
        var read = new List<string>();
        var world = new SessionsWorld(host.Client(), _home, DriverConfig.Empty, SessionWire.Pipe, null)
        {
            PieceBase = (tree, _) =>
            {
                read.Add(tree);
                return Task.FromResult(new PieceBase(head, ["src/draft.ts"]));
            },
        };
        return (world, host, read);
    }

    /// <summary>
    /// 🔴 The ask is written once as the person's: the piece, its paths, the agent named, the commit the lead's tree stands at as its exact
    /// base and the lead's uncommitted paths; the driver takes it at its next look.
    /// </summary>
    [Fact]
    public async Task The_ask_is_written_as_the_persons_at_the_leads_commit_for_the_driver_to_take()
    {
        var (world, _, read) = World();
        var output = new StringWriter();

        var exit = await SessionsCommand.RunAsync(
            Read("partner", "s1", "  Make the window seven days.  ", "--agent", "codex-acp", "--paths", "src/window.ts"), world, output);

        Assert.Equal(0, exit);
        Assert.Equal(["trees/default/reports/s-1"], read);
        var ask = Assert.Single(Files.Asks());
        Assert.Equal(("s1", "Make the window seven days.", "codex-acp", Head, PartnerAsk.ByPerson),
            (ask.Asker, ask.Piece, ask.Agent, ask.Base, ask.AskedBy));
        Assert.Equal(["src/window.ts"], ask.Paths);
        Assert.Equal(["src/draft.ts"], ask.Uncommitted);
        Assert.Null(Files.ReadState(ask.Id));
        Assert.Contains($"#{ask.Id}", output.ToString());
        Assert.Contains("next look", output.ToString());
    }

    [Theory]
    [InlineData("gone", PartnerAskWhy.NotHeld, "No session `s1`")]
    [InlineData("partner", PartnerAskWhy.Unfit, "cannot lead a piece")]
    [InlineData("reviewer", PartnerAskWhy.Unfit, "cannot lead a piece")]
    [InlineData("intake", PartnerAskWhy.Unfit, "cannot lead a piece")]
    [InlineData("no-tree", PartnerAskWhy.Unfit, "cannot lead a piece")]
    [InlineData("no-commit", PartnerAskWhy.NoCommit, "has no commit")]
    public async Task A_session_that_cannot_lead_a_piece_is_refused_and_nothing_is_written(string row, string why, string says)
    {
        var (world, _, _) = World(host =>
        {
            switch (row)
            {
                case "partner": host.Set("s1", "serves", "s0"); break;
                case "reviewer": host.Set("s1", "opinion", "o1"); break;
                case "intake": host.Set("s1", "ask", "a9"); break;
                case "no-tree": host.Set("s1", "tree", null); break;
            }
        }, head: row == "no-commit" ? null : Head);
        if (row == "gone") world = world with { Service = new OpinionStandIn().Client() };
        var output = new StringWriter();

        var exit = await SessionsCommand.RunAsync(Read("partner", "s1", "Write it.", "--agent", "codex-acp"), world, output);

        Assert.Equal(1, exit);
        Assert.Contains(says, output.ToString());
        Assert.Contains("Nothing was asked.", output.ToString());
        Assert.Empty(Files.Asks());
        var asked = await PartnerPersonAsk.AskAsync(_home, world.Service, "s1", "Write it.", [], "codex-acp", world.PieceBase, DateTimeOffset.UtcNow);
        Assert.Equal((null, why), (asked.Asked, asked.Why));
    }

    [Theory]
    [InlineData("", "codex-acp", PartnerAskWhy.Blank)]
    [InlineData("Write it.", null, PartnerAskWhy.Agent)]
    [InlineData("Write it.", "codex acp", PartnerAskWhy.Agent)]
    public async Task Words_no_piece_can_be_asked_by_are_refused_before_the_record_is_read(string piece, string? agent, string why)
    {
        var (world, _, read) = World();

        var asked = await PartnerPersonAsk.AskAsync(_home, world.Service, "s1", piece, [], agent, world.PieceBase, DateTimeOffset.UtcNow);

        Assert.Equal((null, why), (asked.Asked, asked.Why));
        Assert.Empty(read);
        Assert.Empty(Files.Asks());
    }

    [Fact]
    public async Task A_piece_past_its_bound_or_a_path_outside_the_repository_is_refused()
    {
        var (world, _, _) = World();

        var tooLong = await PartnerPersonAsk.AskAsync(
            _home, world.Service, "s1", new string('x', PartnerAsk.PieceLimit + 1), [], "codex-acp", world.PieceBase, DateTimeOffset.UtcNow);
        var outside = await PartnerPersonAsk.AskAsync(
            _home, world.Service, "s1", "Write it.", ["src", "../elsewhere"], "codex-acp", world.PieceBase, DateTimeOffset.UtcNow);

        Assert.Equal(PartnerAskWhy.TooLong, tooLong.Why);
        Assert.Equal(PartnerAskWhy.Path, outside.Why);
        Assert.Contains("`../elsewhere`", outside.Message);
        Assert.Empty(Files.Asks());
    }

    [Fact]
    public void Status_porcelain_is_read_as_its_paths_a_renames_source_skipped()
    {
        var paths = PartnerPersonAsk.Porcelain(" M src/a.ts\0?? notes/new.md\0R  src/b.ts\0src/old.ts\0");
        Assert.Equal(["notes/new.md", "src/a.ts", "src/b.ts"], paths);
    }

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-22T10:00:00Z");

    private void Piece(string id, PartnerState? state, string asker = "s1") =>
        Files.WriteAsk(new PartnerAsk(id, asker, "Make the window seven days.", ["src"], "codex-acp", Head, [], PartnerAsk.ByPerson, At));

    [Fact]
    public async Task The_list_shows_a_sessions_pieces_where_each_stands_and_its_frozen_ref()
    {
        Piece("p1", null);
        Files.WriteState(new PartnerState("p1", PartnerStates.Handed, null, At)
        {
            Agent = "codex-acp", Partner = "pt1", Commit = "c0ffee0000000000000000000000000000000000",
        });
        Piece("p2", null);
        Files.WriteState(new PartnerState("p2", PartnerStates.Refused, PartnerWhy.NotMeasured, At) { Agent = "codex-acp" });
        Piece("p3", null, asker: "s9");
        var (world, _, _) = World();
        var output = new StringWriter();

        var exit = await SessionsCommand.RunAsync(Read("partner", "s1", "--list"), world, output);

        Assert.Equal(0, exit);
        var said = output.ToString();
        Assert.Contains("#p1  handed  codex-acp  partner pt1  c0ffee0 at refs/daoris/pieces/p1", said);
        Assert.Contains("#p2  refused (not-measured)  codex-acp", said);
        Assert.DoesNotContain("#p3", said);
        Assert.Contains("Make the window seven days.", said);
    }

    [Fact]
    public async Task A_session_with_no_piece_lists_none()
    {
        var (world, _, _) = World();
        var output = new StringWriter();

        Assert.Equal(0, await SessionsCommand.RunAsync(Read("partner", "s1", "--list"), world, output));
        Assert.Contains("has asked no piece", output.ToString());
    }

    [Theory]
    [InlineData(null, "there is no piece `#p1`")]
    [InlineData(PartnerStates.Asked, "is asked, and only a piece whose partner works can be stopped")]
    [InlineData(PartnerStates.Handed, "is handed, and only a piece whose partner works can be stopped")]
    public async Task A_piece_whose_partner_does_not_work_is_not_stopped(string? state, string says)
    {
        if (state is not null) Piece("p1", null);
        if (state is not null and not PartnerStates.Asked) Files.WriteState(new PartnerState("p1", state, null, At) { Partner = "pt1" });
        var (world, _, _) = World();
        var output = new StringWriter();

        var exit = await SessionsCommand.RunAsync(Read("partner", "p1", "--stop"), world, output);

        Assert.Equal(1, exit);
        Assert.Contains(says, output.ToString());
        Assert.Contains("Nothing was stopped.", output.ToString());
    }

    [Fact]
    public void A_working_pieces_stop_is_its_partners_record()
    {
        Piece("p1", null);
        Files.WriteState(new PartnerState("p1", PartnerStates.Started, null, At) { Partner = "pt1", Job = PieceJobs.NameOf("p1") });
        var (world, _, _) = World();

        Assert.Equal("pt1", SessionsPartnerCommand.Working(world, "p1", new StringWriter()));
    }
}

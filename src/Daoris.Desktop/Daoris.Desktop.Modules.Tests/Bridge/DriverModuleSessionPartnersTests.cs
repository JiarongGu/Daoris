using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// *Ask another agent to work on a piece…* over the bridge (COAGENT1b, D199 point 2; the co-agent design §2.3 step 1, §6.1): the person's
/// own ask of a piece for a session, the file `daoris-driver sessions partner` writes, by the one judge and writer both share
/// (<see cref="PartnerPersonAsk"/>); and the session's pieces as its Pieces section lists them, each with where the driver's look left
/// it. The lead's tree is read by a stand-in, so nothing starts a process (MOD8).
/// </summary>
public sealed class DriverModuleSessionPartnersTests : DriverModuleBridge
{
    private const string Head = "3333333333333333333333333333333333333333";

    private static string Record(string id, string extra = "") =>
        $$"""{"id":"{{id}}","repository":"engine","adapter":"claude-code","state":"working","kind":"driven","tree":"trees/work/engine/s-{{id}}","created":"2026-10-22T09:00:00Z","updated":"2026-10-22T09:05:00Z"{{extra}}}""";

    /// <summary>The loop up over a ledger on this machine holding a driven session, a partner, a reviewer and an intake.</summary>
    private async Task<DriverLoop> UpAsync(LoopbackHost ledger)
    {
        var records = $"[{Record("d1")},{Record("p1", ""","serves":"d1","piece":"x1" """)},{Record("r1", ""","opinion":"o1" """)},{Record("i1", ""","ask":"0fda18" """)}]";
        ledger.Serve("/api/sessions?includeClosed=true", System.Text.Encoding.UTF8.GetBytes(records));
        ledger.Serve("/api/sessions", System.Text.Encoding.UTF8.GetBytes(records));
        ledger.Serve("/api/quests?includeClosed=true", System.Text.Encoding.UTF8.GetBytes("[]"));
        ledger.Serve("/api/quests", System.Text.Encoding.UTF8.GetBytes("[]"));
        ledger.Serve("/api/registry", System.Text.Encoding.UTF8.GetBytes("[]"));
        var loop = Loop();
        loop.PieceBases = (_, _) => Task.FromResult(new PieceBase(Head, ["src/draft.ts"]));
        await loop.ComeUpAsync(new ServiceClient(ledger.Address, null));
        return loop;
    }

    [Fact]
    public async Task The_persons_ask_is_the_file_the_terminal_writes_at_the_leads_commit_for_the_drivers_next_look()
    {
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "SESSION_PARTNER_ASK", new { id = "d1", piece = "Write it.", agent = "codex-acp" }));

        using var ledger = new LoopbackHost();
        var loop = await UpAsync(ledger);

        var asked = await AnswerAsync(new DriverModule(Bus, loop), "SESSION_PARTNER_ASK",
            new { id = "d1", piece = "  Make the window seven days.  ", agent = "codex-acp", paths = new[] { "src/window.ts" } });
        var id = asked.GetProperty("asked").GetString();
        Assert.Equal(JsonValueKind.Null, asked.GetProperty("why").ValueKind);

        var ask = Assert.Single(new PartnerFiles(loop.Home).Asks());
        Assert.Equal((id, "d1", "Make the window seven days.", "codex-acp", Head, PartnerAsk.ByPerson),
            (ask.Id, ask.Asker, ask.Piece, ask.Agent, ask.Base, ask.AskedBy));
        Assert.Equal(["src/window.ts"], ask.Paths);
        Assert.Equal(["src/draft.ts"], ask.Uncommitted);
        // No path of the machine's on the wire.
        Assert.DoesNotContain(JsonSerializer.Serialize(Home).Trim('"'), asked.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Each refusal is a code and the verb's sentence, answered as data, and nothing is written.</summary>
    [Theory]
    [InlineData("d1", "   ", "codex-acp", PartnerAskWhy.Blank)]
    [InlineData("d1", "Write it.", null, PartnerAskWhy.Agent)]
    [InlineData("d1", "Write it.", "codex acp", PartnerAskWhy.Agent)]
    [InlineData("gone1", "Write it.", "codex-acp", PartnerAskWhy.NotHeld)]
    [InlineData("p1", "Write it.", "codex-acp", PartnerAskWhy.Unfit)]
    [InlineData("r1", "Write it.", "codex-acp", PartnerAskWhy.Unfit)]
    [InlineData("i1", "Write it.", "codex-acp", PartnerAskWhy.Unfit)]
    public async Task What_cannot_lead_a_piece_is_refused_by_code_and_writes_nothing(string id, string piece, string? agent, string why)
    {
        using var ledger = new LoopbackHost();
        var loop = await UpAsync(ledger);

        var refused = await AnswerAsync(new DriverModule(Bus, loop), "SESSION_PARTNER_ASK", new { id, piece, agent });

        Assert.Equal(JsonValueKind.Null, refused.GetProperty("asked").ValueKind);
        Assert.Equal(why, refused.GetProperty("why").GetString());
        Assert.EndsWith("Nothing was asked.", refused.GetProperty("message").GetString());
        Assert.Empty(new PartnerFiles(loop.Home).Asks());
    }

    /// <summary>`sessions partner` sent over the pipe answers the verb's own lines, the alone path's, judged by the same code.</summary>
    [Fact]
    public async Task The_route_answers_the_verbs_own_lines_when_asked_for_them()
    {
        using var ledger = new LoopbackHost();
        var loop = await UpAsync(ledger);
        var module = new DriverModule(Bus, loop);

        var asked = await AnswerAsync(module, "SESSION_PARTNER_ASK", new { id = "d1", piece = "Write it.", agent = "codex-acp", said = true });
        var refused = await AnswerAsync(module, "SESSION_PARTNER_ASK", new { id = "r1", piece = "Write it.", agent = "codex-acp", said = true });

        var said = asked.GetProperty("said");
        Assert.Equal(0, said.GetProperty("code").GetInt32());
        Assert.StartsWith("sessions: Asked piece `#", Assert.Single(said.GetProperty("out").EnumerateArray()).GetString());
        Assert.Equal(1, refused.GetProperty("said").GetProperty("code").GetInt32());
        Assert.Single(new PartnerFiles(loop.Home).Asks());
    }

    [Fact]
    public async Task A_path_outside_the_repository_is_refused()
    {
        using var ledger = new LoopbackHost();
        var loop = await UpAsync(ledger);

        var refused = await AnswerAsync(new DriverModule(Bus, loop), "SESSION_PARTNER_ASK",
            new { id = "d1", piece = "Write it.", agent = "codex-acp", paths = new[] { "../elsewhere" } });

        Assert.Equal(PartnerAskWhy.Path, refused.GetProperty("why").GetString());
        Assert.Empty(new PartnerFiles(loop.Home).Asks());
    }

    /// <summary>
    /// The session's pieces, oldest first, each as the driver's look left it: none judged yet, refused with its code, and handed with its
    /// partner, its frozen commit and ref and the files Daoris read with those beyond its paths. Another session's pieces are not its own.
    /// </summary>
    [Fact]
    public async Task A_sessions_pieces_are_listed_with_where_the_look_left_each()
    {
        var files = new PartnerFiles(Home);
        var at = DateTimeOffset.Parse("2026-10-22T10:00:00Z");
        PartnerAsk Ask(string id, string asker, int minute) =>
            new(id, asker, $"Piece {id}.", ["src"], "codex-acp", Head, [], PartnerAsk.ByPerson, at.AddMinutes(minute));
        files.WriteAsk(Ask("a1", "d1", 0));
        files.WriteAsk(Ask("a2", "d1", 1));
        files.WriteAsk(Ask("a3", "d1", 2));
        files.WriteAsk(Ask("a4", "other", 3));
        files.WriteState(new PartnerState("a2", PartnerStates.Refused, PartnerWhy.NotMeasured, at) { Agent = "codex-acp" });
        files.WriteState(new PartnerState("a3", PartnerStates.Handed, null, at)
        {
            Agent = "codex-acp", Partner = "pt9", Commit = "c0ffee0000000000000000000000000000000000", End = PartnerEnds.Answered,
        });
        files.WriteFacts(new PartnerFacts("a3", "c0ffee0000000000000000000000000000000000", ["README.md", "src/window.ts"], ["README.md"], [], at));

        var pieces = (await AnswerAsync(Module(), "SESSION_PARTNERS", new { id = "d1" })).GetProperty("pieces").EnumerateArray().ToList();

        Assert.Equal(["a1", "a2", "a3"], pieces.Select(row => row.GetProperty("id").GetString()));
        Assert.Equal([PartnerStates.Asked, PartnerStates.Refused, PartnerStates.Handed], pieces.Select(row => row.GetProperty("state").GetString()));
        Assert.Equal(PartnerWhy.NotMeasured, pieces[1].GetProperty("code").GetString());
        var handed = pieces[2];
        Assert.Equal(("pt9", "codex-acp", PartnerEnds.Answered), (handed.GetProperty("partner").GetString(), handed.GetProperty("startedOn").GetString(), handed.GetProperty("end").GetString()));
        Assert.Equal(("c0ffee0000000000000000000000000000000000", PieceRefs.Of("a3")), (handed.GetProperty("commit").GetString(), handed.GetProperty("ref").GetString()));
        Assert.Equal(["README.md", "src/window.ts"], handed.GetProperty("files").EnumerateArray().Select(file => file.GetString()));
        Assert.Equal(["README.md"], handed.GetProperty("beyond").EnumerateArray().Select(file => file.GetString()));
        Assert.Equal("Piece a3.", handed.GetProperty("piece").GetString());
    }
}

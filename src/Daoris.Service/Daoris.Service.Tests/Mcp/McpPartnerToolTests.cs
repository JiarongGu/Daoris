using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;

namespace Daoris.Service.Tests;

/// <summary>
/// COAGENT1b (D199 point 8; the co-agent design §2.3 step 8): the connector's `partner_answer`, for the partner the driver started for a
/// piece, over the ledger's record: the record's `piece` is what the box finds the piece by, and a working session or a helper is refused.
/// </summary>
public sealed class McpPartnerToolTests : McpToolsFixture
{
    private string Home => Path.Combine(_root, "rules-home");

    private async Task<(PartnerTools Tools, SessionStore Sessions)> ConnectorAsync(Func<SessionStore, Task<string?>> session)
    {
        var sessions = await SessionStore.OpenAsync(_connection);
        var asks = await AskStore.OpenAsync(_connection);
        var ledger = new SessionLedger(_quests, sessions, _service, asks);
        var named = await session(sessions);
        return (new PartnerTools(intake: new IntakeScope(null, named), ledger: ledger, partners: new PartnerBox(Home)), sessions);
    }

    private void State(string piece, string partner)
    {
        Directory.CreateDirectory(DaorisHome.Entry(Home, HomeEntry.Partners));
        File.WriteAllText(PartnerBox.PathOf(Home, piece, "state"), $"{{ \"id\": \"{piece}\", \"state\": \"started\", \"partner\": \"{partner}\" }}");
    }

    /// <summary>🔴 The partner whose record carries the started piece keeps its answer once; the file is the driver's to read.</summary>
    [Fact]
    public async Task The_started_pieces_partner_keeps_its_answer_once()
    {
        string partner = null!;
        var (tools, _) = await ConnectorAsync(async sessions =>
        {
            var lead = await sessions.CreateAsync("q1", "Owner", "claude-code", DateTimeOffset.UtcNow, tree: Path.Combine(_root, "trees", "lead"));
            partner = (await sessions.CreateAsync(null, "Owner", "codex-acp", DateTimeOffset.UtcNow, kind: SessionKind.Chat, serves: lead.Id, piece: "p1a2b3c4")).Id;
            return partner;
        });
        State("p1a2b3c4", partner);

        var said = await tools.AnswerPartnerAsync("Seven days now.", "No test for the edge.");

        Assert.Contains("kept for piece `#p1a2b3c4`", said);
        Assert.True(File.Exists(PartnerBox.PathOf(Home, "p1a2b3c4", "answer")));
        Assert.Contains("already answered", await tools.AnswerPartnerAsync("Again."));
    }

    /// <summary>A working session and a helper (a record with `serves` and no `piece`) are refused, and nothing is written.</summary>
    [Fact]
    public async Task A_working_session_and_a_helper_are_refused()
    {
        var (working, _) = await ConnectorAsync(async sessions =>
            (await sessions.CreateAsync("q1", "Owner", "claude-code", DateTimeOffset.UtcNow, tree: Path.Combine(_root, "trees", "w"))).Id);
        Assert.Contains("is no partner", await working.AnswerPartnerAsync("Done."));

        var (helper, _) = await ConnectorAsync(async sessions =>
        {
            var lead = await sessions.CreateAsync("q2", "Owner", "claude-code", DateTimeOffset.UtcNow, tree: Path.Combine(_root, "trees", "x"));
            return (await sessions.CreateAsync(null, "Owner", "codex-acp", DateTimeOffset.UtcNow, kind: SessionKind.Chat, serves: lead.Id)).Id;
        });
        Assert.Contains("is no partner", await helper.AnswerPartnerAsync("Done."));
        Assert.False(Directory.Exists(DaorisHome.Entry(Home, HomeEntry.Partners)));
    }
}

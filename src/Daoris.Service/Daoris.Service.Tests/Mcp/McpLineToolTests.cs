using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;

namespace Daoris.Service.Tests;

/// <summary>
/// SESSCTX1f (D189's SESSCTX1f note; the session-context design §3.5): `line_read` pages the events of the line the connector's
/// session continues, from this machine's event files, so what its brief's bound left out is one call away. It reads the parts of its
/// own session's line alone, bounded, and refuses every other record.
/// </summary>
public sealed class McpLineToolTests : McpToolsFixture
{
    private string Home => Path.Combine(_root, "rules-home");

    /// <summary>A part's event file under the home, as the driver keeps it: one event a line.</summary>
    private void Keep(string session, IEnumerable<string> events)
    {
        var path = LineEvents.PathOf(Home, session);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Join("\n", events) + "\n");
    }

    private static string Event(long seq, string kind, string fields) =>
        $$"""{"seq":{{seq}},"at":"2026-10-20T10:{{seq % 60:00}}:00+00:00","kind":"{{kind}}",{{fields}}}""";

    private static string Person(long seq, string words) => Event(seq, "user", $$"""
        "text":"{{words}}","origin":"person"
        """);

    private static string Agent(long seq, string words) => Event(seq, "message", $$"""
        "text":"{{words}}"
        """);

    /// <summary>The connector a session is handed, with a ledger to read the records and a reader under the rules home.</summary>
    private async Task<(LineTools Tools, SessionStore Sessions)> ConnectorAsync(
        Func<SessionStore, Task<string?>> session, bool home = true)
    {
        var sessions = await SessionStore.OpenAsync(_connection);
        var asks = await AskStore.OpenAsync(_connection);
        var ledger = new SessionLedger(_quests, sessions, _service, asks);
        var named = await session(sessions);
        return (new LineTools(intake: new IntakeScope(null, named), ledger: ledger, lines: new LineEvents(home ? Home : null)), sessions);
    }

    private static Task<Session> PartAsync(SessionStore sessions, Session? from = null, string? why = null) =>
        sessions.CreateAsync("q1", "Owner", "claude-code", DateTimeOffset.UtcNow, from: from?.Id, fromWhy: why, fromBy: from is null ? null : "daoris");

    /// <summary>
    /// 🔴 A session that continues another reads, by default, the part it continues: its entries oldest first, the line named oldest
    /// part first with how each began, and where the part before it is. The part before reads to the start of the line.
    /// </summary>
    [Fact]
    public async Task A_continued_part_s_events_are_read_oldest_first_with_the_line_named()
    {
        Session first = null!, moved = null!;
        var (tools, _) = await ConnectorAsync(async sessions =>
        {
            first = await PartAsync(sessions);
            moved = await PartAsync(sessions, first, "limit");
            return (await PartAsync(sessions, moved, "press")).Id;
        });
        // A word from a record kept before `origin` was written reads as the person's, as the driver's brief reads it.
        Keep(first.Id, [Person(1, "Make the report faster."), Agent(2, "Found the slow query."), Event(3, "user", "\"text\":\"Old words.\"")]);
        Keep(moved.Id, [Person(1, "Go on with the index."), Agent(2, "Added the index.")]);

        var said = await tools.ReadAsync();

        Assert.Contains($"`{first.Id}`", said);
        Assert.Contains($"`{moved.Id}`, which continued `{first.Id}` (limit)", said);
        Assert.Contains("this session", said);
        Assert.Contains("Go on with the index.", said);
        Assert.True(said.IndexOf("Go on with the index.", StringComparison.Ordinal) < said.IndexOf("Added the index.", StringComparison.Ordinal));
        Assert.Contains("the person", said);
        Assert.Contains("the agent", said);
        Assert.DoesNotContain("Make the report faster.", said);
        Assert.Contains($"part `{first.Id}`", said);

        var earlier = await tools.ReadAsync(part: first.Id);

        Assert.Contains("Make the report faster.", earlier);
        Assert.Contains("Found the slow query.", earlier);
        Assert.Contains("· the person\nOld words.", earlier);
        Assert.Contains("start of the line", earlier);
        Assert.DoesNotContain("Go on with the index.", earlier);
    }

    /// <summary>A page holds the newest entries asked for, and says the number to read before for the ones it left; that reads back.</summary>
    [Fact]
    public async Task A_page_reads_back_by_the_number_it_names()
    {
        Session first = null!;
        var (tools, _) = await ConnectorAsync(async sessions =>
        {
            first = await PartAsync(sessions);
            return (await PartAsync(sessions, first, "press")).Id;
        });
        Keep(first.Id, Enumerable.Range(1, 10).Select(n => n % 2 == 1 ? Person(n, $"Word {n:00}.") : Agent(n, $"Answer {n:00}.")));

        var newest = await tools.ReadAsync(limit: 3);

        Assert.Contains("Answer 10.", newest);
        Assert.Contains("Word 09.", newest);
        Assert.Contains("Answer 08.", newest);
        Assert.DoesNotContain("Word 07.", newest);
        Assert.Contains($"part `{first.Id}` and before 8", newest);

        var before = await tools.ReadAsync(part: first.Id, before: 8, limit: 3);

        Assert.Contains("Word 07.", before);
        Assert.Contains("Word 05.", before);
        Assert.DoesNotContain("Answer 08.", before);
        Assert.DoesNotContain("Answer 04.", before);
        Assert.Contains("before 5", before);

        var none = await tools.ReadAsync(part: first.Id, before: 1);

        Assert.Contains("no entry before #1", none);
    }

    /// <summary>
    /// 🔴 The bound: an answer weighs at most the brief's own bound, a Han character as three, and says where to read on; one entry
    /// heavier than a whole page alone is cut, and the cut is said with its length.
    /// </summary>
    [Fact]
    public async Task An_answer_is_bounded_and_says_what_it_cut()
    {
        Session english = null!, chinese = null!, longest = null!;
        var (tools, _) = await ConnectorAsync(async sessions =>
        {
            english = await PartAsync(sessions);
            chinese = await PartAsync(sessions, english, "press");
            longest = await PartAsync(sessions, chinese, "limit");
            return (await PartAsync(sessions, longest, "agent")).Id;
        });
        Keep(english.Id, Enumerable.Range(1, 30).Select(n => Person(n * 2 - 1, $"Word {n:00}.") + "\n" + Agent(n * 2, $"Answer {n:00}: " + new string('x', 3000))));
        Keep(chinese.Id, Enumerable.Range(1, 20).Select(n => Person(n * 2 - 1, $"问{n:00}") + "\n" + Agent(n * 2, $"答{n:00}" + new string('深', 2000))));
        Keep(longest.Id, [Person(1, "Write it all."), Agent(2, "Start " + new string('y', 50_000) + " end.")]);

        var page = await tools.ReadAsync(part: english.Id, limit: 200);

        Assert.True(LineEvents.Weight(page) <= LineEvents.Bound, $"{LineEvents.Weight(page)} over the bound");
        Assert.Contains("Answer 30:", page);
        Assert.DoesNotContain("Answer 01:", page);
        Assert.Matches(@"before \d+", page);

        var han = await tools.ReadAsync(part: chinese.Id, limit: 200);

        Assert.True(LineEvents.Weight(han) <= LineEvents.Bound, $"{LineEvents.Weight(han)} over the bound");
        Assert.Contains("答20", han);
        Assert.DoesNotContain("答10", han);

        var cut = await tools.ReadAsync(part: longest.Id, limit: 1);

        Assert.True(LineEvents.Weight(cut) <= LineEvents.Bound, $"{LineEvents.Weight(cut)} over the bound");
        Assert.Contains("Start yyy", cut);
        Assert.DoesNotContain(" end.", cut);
        Assert.Contains("cut", cut);
        Assert.Contains("50,011 characters", cut);
    }

    /// <summary>
    /// 🔴 A record off the session's line is refused and nothing of it is read: a part beside the line, one that continues this session,
    /// and one with no link to it at all.
    /// </summary>
    [Fact]
    public async Task A_record_off_the_line_is_refused_and_nothing_of_it_is_read()
    {
        Session first = null!, beside = null!, after = null!, apart = null!;
        string mine = null!;
        var (tools, _) = await ConnectorAsync(async sessions =>
        {
            first = await PartAsync(sessions);
            var me = await PartAsync(sessions, first, "press");
            beside = await PartAsync(sessions, first, "agent");
            after = await PartAsync(sessions, me, "limit");
            apart = await PartAsync(sessions);
            return mine = me.Id;
        });
        foreach (var other in new[] { beside, after, apart }) Keep(other.Id, [Person(1, "Secret words.")]);

        foreach (var other in new[] { beside, after, apart })
        {
            var said = await tools.ReadAsync(part: other.Id);

            Assert.Contains($"`{other.Id}` is not on this session's line", said);
            Assert.Contains($"`{first.Id}`, `{mine}`", said);
            Assert.Contains("Nothing was read.", said);
            Assert.DoesNotContain("Secret words.", said);
        }
    }

    /// <summary>A part named as anything but an id of this machine's names no file here and is refused before the line is asked.</summary>
    [Theory]
    [InlineData("m1:a1b2c3d4")]
    [InlineData("origin/a1b2c3d4")]
    [InlineData("..")]
    [InlineData("a1/../x")]
    [InlineData("CON")]
    public async Task A_part_that_is_no_local_id_is_refused(string part)
    {
        var (tools, _) = await ConnectorAsync(async sessions => (await PartAsync(sessions)).Id);

        var said = await tools.ReadAsync(part: part);

        Assert.Contains("names no record on this machine", said);
        Assert.Contains("Nothing was read.", said);
    }

    /// <summary>A connector that names no session, a session not held here, and no home each read nothing, and say why.</summary>
    [Fact]
    public async Task With_no_session_no_record_or_no_home_nothing_is_read()
    {
        var (unnamed, _) = await ConnectorAsync(_ => Task.FromResult<string?>(null));
        var (unheld, _) = await ConnectorAsync(_ => Task.FromResult<string?>("zz999999"));
        var (homeless, _) = await ConnectorAsync(async sessions => (await PartAsync(sessions)).Id, home: false);

        Assert.Contains("speaks for no session", await unnamed.ReadAsync());
        Assert.Contains("No session `zz999999` of this machine's is held", await unheld.ReadAsync());
        Assert.Contains("DAORIS_HOME", await homeless.ReadAsync());
        Assert.All(
            [await unnamed.ReadAsync(), await unheld.ReadAsync(), await homeless.ReadAsync()],
            said => Assert.Contains("Nothing was read.", said));
    }

    /// <summary>A session that continues none reads its own part; a part whose record keeps no events here says so.</summary>
    [Fact]
    public async Task A_session_that_continues_none_reads_its_own_part_and_a_part_with_no_file_says_so()
    {
        Session alone = null!;
        var (own, _) = await ConnectorAsync(async sessions => (alone = await PartAsync(sessions)).Id);
        Keep(alone.Id, [Person(1, "Start here.")]);

        var said = await own.ReadAsync();

        Assert.Contains("Start here.", said);
        Assert.Contains("start of the line", said);

        Session first = null!;
        var (empty, _) = await ConnectorAsync(async sessions =>
        {
            first = await PartAsync(sessions);
            return (await PartAsync(sessions, first, "press")).Id;
        });

        Assert.Contains($"Part `{first.Id}` keeps no events on this machine", await empty.ReadAsync());
    }
}

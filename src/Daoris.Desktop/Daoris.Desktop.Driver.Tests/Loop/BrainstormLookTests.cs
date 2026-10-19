using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// BRAINSTORM1a (D176 points 1-4 and 8): a brainstorm's round trip at the look, in process. The request a verb wrote is frozen into
/// one packet the person shows and sends; each generator's turn answers in a room holding that packet byte for byte; Daoris
/// validates every answer with no model, gives a malformed one its one repair, hands the synthesis the proposals anonymous and
/// re-attaches who gave which; and the document is kept and the rooms go. The turns are stand-ins here (the real ones, on the
/// native stub and the ACP stub, are the <c>Process</c> half's <c>BrainstormProcessTests</c>), so nothing starts a process (MOD8).
/// </summary>
public sealed class BrainstormLookTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-brainstorm-look-" + Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-19T10:00:00Z");

    public BrainstormLookTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private BrainstormFiles Files => new(_home);

    /// <summary>Every request the driver made of the service, by method and path: the proof no quest operation was made.</summary>
    private sealed class Service : HttpMessageHandler
    {
        public List<string> Asked { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            lock (Asked) Asked.Add($"{request.Method} {request.RequestUri!.AbsolutePath}");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{\"error\":\"none here\"}") });
        }
    }

    /// <summary>What each turn saw in its room, and what it answers through the file its result tool writes.</summary>
    private sealed class Turns
    {
        public List<(BrainstormTurn Turn, string Packet, string Instruction, string? Proposals)> Seen { get; } = [];

        /// <summary>The answer each turn writes, by turn id; a turn with none writes nothing.</summary>
        public Dictionary<string, string> Answers { get; } = [];

        public Task<BrainstormTurnRun> RunAsync(BrainstormTurn turn, CancellationToken ct)
        {
            var packet = File.ReadAllBytes(Path.Combine(turn.Room, BrainstormInstruction.PacketFile));
            var proposals = Path.Combine(turn.Room, BrainstormInstruction.ProposalsFile);
            lock (Seen)
            {
                // The packet's digest as it reads, and its file's own bytes' hash: the same file, byte for byte, in every room.
                Seen.Add((turn, $"{BrainstormPackets.Read(packet)?.Digest} {Convert.ToHexStringLower(SHA256.HashData(packet))}",
                    File.ReadAllText(Path.Combine(turn.Room, BrainstormInstruction.InstructionFile)),
                    File.Exists(proposals) ? File.ReadAllText(proposals) : null));
            }

            if (Answers.TryGetValue(turn.Turn, out var answer))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(turn.Answer)!);
                File.WriteAllText(turn.Answer, answer);
            }

            return Task.FromResult(new BrainstormTurnRun(true, "work", null, $"completed  brainstorm turn {turn.Turn}"));
        }
    }

    private static string Give(params (string Idea, string Reversible, string Risk)[] proposals) =>
        new JsonObject
        {
            ["brainstorm"] = 1,
            ["proposals"] = new JsonArray([.. proposals.Select(each => (JsonNode)new JsonObject
            {
                ["idea"] = each.Idea, ["rationale"] = "Because the report reads the window.", ["effort"] = "small", ["risk"] = each.Risk,
                ["reversible"] = each.Reversible, ["undo"] = each.Reversible == "yes" ? null : "Restore the rows from the backup.",
                ["confidence"] = "medium", ["assumptions"] = new JsonArray("the window is exclusive"), ["sources"] = new JsonArray(),
            })]),
            ["needs"] = new JsonArray("the report's tests"),
            ["none"] = null,
        }.ToJsonString();

    private static string Synthesize(params (string Title, int Rank, string[] Members)[] groups) =>
        new JsonObject
        {
            ["brainstorm"] = 1,
            ["groups"] = new JsonArray([.. groups.Select(each => (JsonNode)new JsonObject
            {
                ["title"] = each.Title, ["idea"] = each.Title + ", merged.", ["members"] = new JsonArray([.. each.Members.Select(id => (JsonNode)id)]),
                ["rank"] = each.Rank, ["why"] = "It answers the question as asked.", ["flags"] = new JsonArray(),
            })]),
            ["leftOut"] = new JsonArray(),
        }.ToJsonString();

    private (Daoris.Driver.Driver Driver, Service Service, Turns Turns) Start(int cap = 3)
    {
        var service = new Service();
        var turns = new Turns();
        var driver = new Daoris.Driver.Driver(
            new ServiceClient("http://127.0.0.1:9", null, new HttpClient(service)), DriverConfig.Empty with { Adapter = "stub", Cap = cap }, AdapterSet.Built(), _home)
        {
            BrainstormTurns = turns.RunAsync,
        };
        return (driver, service, turns);
    }

    private static async Task<List<string>> LookAsync(Daoris.Driver.Driver driver)
    {
        var events = new List<string>();
        await driver.BrainstormsAsync(new Snapshot([], [], []), 0, events, CancellationToken.None);
        await driver.Running.SettledAsync().WaitAsync(TimeSpan.FromSeconds(30));
        return events;
    }

    /// <summary>
    /// 🔴 PARALLEL1b (D181 point 3): under the watch a request's packet is frozen as the <c>freeze</c> topic's judge beside the look, so
    /// a checkout or a knowledge search slow to answer holds no start; looked at again while it freezes, it is frozen once, and its
    /// line joins a later look's report.
    /// </summary>
    [Fact]
    public async Task Under_the_watch_a_packet_is_frozen_beside_the_look_and_once()
    {
        var id = Files.WriteRequest("How should the daily report stop dropping its last day?", "engine", ["stub", "acp-stub"], At);
        var reading = new TaskCompletionSource<(string? Root, string? Revision)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var read = 0;
        var driver = new Daoris.Driver.Driver(
            new ServiceClient("http://127.0.0.1:9", null, new HttpClient(new Service())), DriverConfig.Empty with { Adapter = "stub", Cap = 3 },
            AdapterSet.Built(), _home)
        {
            BrainstormTurns = new Turns().RunAsync,
            BrainstormCheckouts = (_, ct) =>
            {
                Interlocked.Increment(ref read);
                return reading.Task.WaitAsync(ct);
            },
            Wanted = token => Task.Delay(Timeout.Infinite, token),
        };
        var events = new List<string>();

        await driver.BrainstormsAsync(new Snapshot([], [], []), 0, events, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(20));
        await driver.BrainstormsAsync(new Snapshot([], [], []), 0, events, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Null(Files.ReadState(id));
        Assert.DoesNotContain(events, line => line.Contains("frozen", StringComparison.Ordinal));

        reading.TrySetResult((null, null));
        await driver.Running.SettledAsync().WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(BrainstormStates.Frozen, Files.ReadState(id)?.State);
        Assert.Single(driver.Running.Drain(), run => run.Line.Contains($"brainstorm `{id}` frozen", StringComparison.Ordinal));
        Assert.Equal(1, read);
    }

    /// <summary>
    /// 🔴 PARALLEL1b2 (D181 point 3, PARALLEL1g's note): a freeze that keeps failing says so once until it is judged again, and a look
    /// that meets it again asks it again but says nothing, so its failing line wakes no look of its own and the loop does not spin.
    /// </summary>
    [Fact]
    public async Task A_freeze_that_keeps_failing_says_so_once()
    {
        var id = Files.WriteRequest("How should the daily report stop dropping its last day?", "engine", ["stub", "acp-stub"], At);
        var read = 0;
        var driver = new Daoris.Driver.Driver(
            new ServiceClient("http://127.0.0.1:9", null, new HttpClient(new Service())), DriverConfig.Empty with { Adapter = "stub", Cap = 3 },
            AdapterSet.Built(), _home)
        {
            BrainstormTurns = new Turns().RunAsync,
            BrainstormCheckouts = (_, _) =>
            {
                Interlocked.Increment(ref read);
                throw new IOException("the checkout will not read");
            },
            Wanted = token => Task.Delay(Timeout.Infinite, token),
        };

        for (var look = 0; look < 3; look++)
        {
            await driver.BrainstormsAsync(new Snapshot([], [], []), 0, [], CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(20));
            await driver.Running.SettledAsync().WaitAsync(TimeSpan.FromSeconds(30));
        }

        Assert.Equal(3, read);
        Assert.Null(Files.ReadState(id));
        Assert.Single(driver.Running.Drain(), run => run.Line.Contains($"brainstorm `{id}` could not be looked at", StringComparison.Ordinal));
    }

    /// <summary>
    /// 🔴 A request is frozen at the look into one packet, whose digest the state names, and waits for the person's send: no turn
    /// runs on a packet nobody saw (D176 point 2).
    /// </summary>
    [Fact]
    public async Task A_request_is_frozen_at_a_look_and_waits_for_the_person_s_send()
    {
        var id = Files.WriteRequest("How should the daily report stop dropping its last day?", null, ["stub", "acp-stub"], At);
        var (driver, service, turns) = Start();

        var said = await LookAsync(driver);

        var state = Files.ReadState(id);
        Assert.Equal(BrainstormStates.Frozen, state?.State);
        var packet = BrainstormPackets.Read(Files.PacketBytes(id)!);
        Assert.NotNull(packet);
        Assert.Equal(packet.Digest, state!.Packet);
        Assert.Equal("How should the daily report stop dropping its last day?", packet.Question);
        Assert.Null(packet.Repository);
        Assert.Empty(packet.Sources);
        Assert.Contains(said, line => line.Contains($"brainstorm show {id}", StringComparison.Ordinal) && line.Contains($"brainstorm send {id}", StringComparison.Ordinal));
        Assert.Empty(turns.Seen);

        // Looked at again unsent, it is still the same packet, and still nothing runs.
        await LookAsync(driver);
        Assert.Equal(state.Packet, Files.ReadState(id)!.Packet);
        Assert.Empty(turns.Seen);
        Assert.Empty(service.Asked);
    }

    /// <summary>
    /// 🔴 Sent, two stub doors each answer once in a room holding the one packet; the synthesis reads the proposals anonymous and
    /// ranks them; Daoris re-attaches who gave which, flags from the fields, keeps the document and prints it, says stubs are no
    /// maker diversity, removes the rooms, and asks the service nothing: no quest operation.
    /// </summary>
    [Fact]
    public async Task Sent_two_stub_doors_answer_and_a_synthesis_ranks_them_with_their_provenance_re_attached()
    {
        var id = Files.WriteRequest("How should the daily report stop dropping its last day?", null, ["stub", "acp-stub"], At);
        var (driver, service, turns) = Start();
        await LookAsync(driver);
        var digest = Files.ReadState(id)!.Packet!;
        Files.WriteSent(id, digest, At);
        turns.Answers["g1"] = Give(("Make the window inclusive of its last day", "yes", "low"), ("Rewrite the report's history", "no", "high"));
        turns.Answers["g2"] = Give(("Include the last day in the window", "yes", "low"));
        turns.Answers["s1"] = Synthesize(("An inclusive window", 1, ["P1", "P2"]), ("Rewrite history", 2, ["P3"]));

        await LookAsync(driver);

        // One packet digest in every room, the synthesis's too: what the person saw is what every pass read.
        Assert.Equal(["g1", "g2", "s1"], turns.Seen.Select(seen => seen.Turn.Turn));
        var frozen = $"{digest} {Convert.ToHexStringLower(SHA256.HashData(Files.PacketBytes(id)!))}";
        Assert.All(turns.Seen, seen => Assert.Equal(frozen, seen.Packet));
        Assert.Equal(["stub", "acp-stub", "stub"], turns.Seen.Select(seen => seen.Turn.Agent));
        Assert.All(turns.Seen, seen => Assert.Contains(digest, seen.Instruction, StringComparison.Ordinal));

        // The synthesis read the proposals by anonymous id, the makers interleaved, and nothing of who gave which.
        var handed = JsonNode.Parse(turns.Seen[2].Proposals!)!["proposals"]!.AsArray();
        Assert.Equal(["P1", "P2", "P3"], handed.Select(each => each!["id"]!.GetValue<string>()));
        Assert.Equal("Make the window inclusive of its last day", handed[0]!["idea"]!.GetValue<string>());
        Assert.Equal("Include the last day in the window", handed[1]!["idea"]!.GetValue<string>());
        Assert.DoesNotContain("stub", turns.Seen[2].Proposals!, StringComparison.Ordinal);
        Assert.DoesNotContain("g1", turns.Seen[2].Proposals!, StringComparison.Ordinal);

        // The document, ranked, with each member's provenance re-attached by Daoris, and its flags from the fields.
        Assert.Equal(BrainstormStates.Ended, Files.ReadState(id)!.State);
        var document = JsonNode.Parse(File.ReadAllText(Files.PathOf(id, "document.json")))!;
        Assert.True(document["ranked"]!.GetValue<bool>());
        Assert.True(document["stubs"]!.GetValue<bool>());
        Assert.Equal(["stub"], document["makers"]!.AsArray().Select(maker => maker!.GetValue<string>()));
        var rows = document["rows"]!.AsArray();
        Assert.Equal([("P1", "g1", "stub"), ("P2", "g2", "acp-stub")],
            rows[0]!["members"]!.AsArray().Select(member => (member!["id"]!.GetValue<string>(), member["pass"]!.GetValue<string>(), member["adapter"]!.GetValue<string>())));
        Assert.Equal(1, rows[0]!["makers"]!.GetValue<int>());
        Assert.Empty(rows[0]!["flags"]!.AsArray());
        Assert.Equal(["hard-to-reverse", "risky"], rows[1]!["flags"]!.AsArray().Select(flag => flag!["flag"]!.GetValue<string>()));
        var printed = Files.DocumentText(id)!;
        Assert.Contains("## 1. An inclusive window", printed, StringComparison.Ordinal);
        Assert.Contains("stubs are no maker diversity", printed, StringComparison.Ordinal);
        Assert.Contains("the rank is its claim", printed, StringComparison.Ordinal);

        // Each pass kept its record; the rooms went; the service was asked nothing at all.
        Assert.Equal("answered", JsonNode.Parse(File.ReadAllText(Files.PathOf(id, "passes", "g2.json")))!["state"]!.GetValue<string>());
        Assert.False(Directory.Exists(Files.Rooms(id)));
        Assert.Empty(service.Asked);
    }

    /// <summary>
    /// 🔴 A malformed answer is a failed pass, never an empty success: its one repair is handed the validator's codes in fixed
    /// words, and a second failure is the pass's, kept with its codes. The document says one maker answered.
    /// </summary>
    [Fact]
    public async Task A_malformed_answer_gets_one_repair_with_its_codes_and_then_fails_its_pass()
    {
        var id = Files.WriteRequest("How should the daily report stop dropping its last day?", null, ["stub", "acp-stub"], At);
        var (driver, _, turns) = Start();
        await LookAsync(driver);
        Files.WriteSent(id, Files.ReadState(id)!.Packet!, At);
        turns.Answers["g1"] = Give(("Make the window inclusive of its last day", "yes", "low"));
        turns.Answers["g2"] = Give(("Rewrite the report", "maybe", "low"));
        turns.Answers["g2r"] = Give(("Rewrite the report", "no", "low")).Replace("Restore the rows from the backup.", "");
        turns.Answers["s1"] = Synthesize(("An inclusive window", 1, ["P1"]));

        await LookAsync(driver);

        Assert.Equal(["g1", "g2", "g2r", "s1"], turns.Seen.Select(seen => seen.Turn.Turn));
        Assert.Contains("field:reversible", turns.Seen[2].Instruction, StringComparison.Ordinal);
        var pass = JsonNode.Parse(File.ReadAllText(Files.PathOf(id, "passes", "g2.json")))!;
        Assert.Equal(("failed", 2), (pass["state"]!.GetValue<string>(), pass["turns"]!.GetValue<int>()));
        Assert.Equal(["field:undo"], pass["codes"]!.AsArray().Select(code => code!.GetValue<string>()));
        var document = JsonNode.Parse(File.ReadAllText(Files.PathOf(id, "document.json")))!;
        Assert.Single(document["rows"]!.AsArray());
    }

    /// <summary>
    /// 🔴 A synthesis that leaves a proposal out without a word is refused, twice, and the document is the validated proposals
    /// unranked, saying why, with exact duplicates from two passes folded into one row naming both.
    /// </summary>
    [Fact]
    public async Task A_synthesis_that_drops_a_proposal_is_refused_and_the_document_is_unranked()
    {
        var id = Files.WriteRequest("How should the daily report stop dropping its last day?", null, ["stub", "acp-stub"], At);
        var (driver, _, turns) = Start();
        await LookAsync(driver);
        Files.WriteSent(id, Files.ReadState(id)!.Packet!, At);
        turns.Answers["g1"] = Give(("Make the window inclusive", "yes", "low"));
        turns.Answers["g2"] = Give(("make the  window inclusive", "yes", "low"), ("Cache the report", "partly", "medium"));
        turns.Answers["s1"] = Synthesize(("An inclusive window", 1, ["P1", "P2"]));
        turns.Answers["s1r"] = Synthesize(("An inclusive window", 1, ["P1", "P2"]), ("Cache it", 1, ["P3"]));

        await LookAsync(driver);

        Assert.Contains("member-missing", turns.Seen.Single(seen => seen.Turn.Turn == "s1r").Instruction, StringComparison.Ordinal);
        Assert.Equal((BrainstormStates.Ended, BrainstormStates.Unranked), (Files.ReadState(id)!.State, Files.ReadState(id)!.Code));
        var document = JsonNode.Parse(File.ReadAllText(Files.PathOf(id, "document.json")))!;
        Assert.False(document["ranked"]!.GetValue<bool>());
        var rows = document["rows"]!.AsArray();
        Assert.Equal(2, rows.Count);
        Assert.Equal(2, rows[0]!["members"]!.AsArray().Count);
        Assert.Contains("Unranked: the synthesis did not finish", Files.DocumentText(id)!, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Files.Rooms(id)));
    }

    /// <summary>
    /// 🔴 A question about a repository is frozen from Daoris's own knowledge, asked in more than one wording, its sources named by
    /// path, lines, revision and why, cut to their bound with what was cut said; and the search is the only thing asked of the
    /// service.
    /// </summary>
    [Fact]
    public async Task A_repository_s_question_is_frozen_from_the_knowledge_search_with_each_source_s_provenance()
    {
        var id = Files.WriteRequest("Why does the daily report drop its last day?", "reports", [], At);
        var asked = new List<(string Words, string Repository)>();
        var long_ = new string('x', 13_000);
        var looked = new Daoris.Driver.Driver(
            new ServiceClient("http://127.0.0.1:9", null, new HttpClient(new Service())), DriverConfig.Empty with { Adapter = "stub" }, AdapterSet.Built(), _home)
        {
            BrainstormSearches = (words, repository, _) =>
            {
                asked.Add((words, repository));
                return Task.FromResult(new BrainstormSearch(
                    [
                        new BrainstormFound("decision", "docs/decisions/D7.md", 3, 9, "The window is exclusive of its end.", words),
                        new BrainstormFound("knowledge", "docs/report.md", null, null, long_, words),
                    ], BrainstormPackets.Words));
            },
            BrainstormCheckouts = (_, _) => Task.FromResult<(string?, string?)>(("D:/checkouts/reports", "0123456789abcdef0123456789abcdef01234567")),
        };

        await LookAsync(looked);

        Assert.Equal(2, asked.Count);
        Assert.All(asked, each => Assert.Equal("reports", each.Repository));
        var packet = BrainstormPackets.Read(Files.PacketBytes(id)!)!;
        Assert.Equal(("reports", "words"), (packet.Repository, packet.Tier));
        Assert.Equal(2, packet.Sources.Count);
        var first = packet.Sources[0];
        Assert.Equal(("S1", "docs/decisions/D7.md", 3, 9), (first.Id, first.Path, first.First, first.Last));
        Assert.Equal("0123456789abcdef0123456789abcdef01234567", first.Revision);
        Assert.Equal((12_000, 13_000), (packet.Sources[1].Kept, packet.Sources[1].Of));
        Assert.DoesNotContain("checkouts", Encoding.UTF8.GetString(Files.PacketBytes(id)!), StringComparison.Ordinal);
    }
}

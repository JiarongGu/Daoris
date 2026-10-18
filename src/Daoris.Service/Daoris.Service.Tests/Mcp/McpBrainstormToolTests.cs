using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Nodes;
using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;
using ModelContextProtocol.Server;

namespace Daoris.Service.Tests;

/// <summary>
/// BRAINSTORM1a2 (D176 points 1-4): a brainstorm pass's two result tools on the connector. `brainstorm_give` (a generator's turn) and
/// `brainstorm_synthesize` (the synthesis's) write the turn's answer file the driver's spawn names, and nothing else; each answers only
/// a turn of its own kind; and each judges the answer by the same table as the driver's validator, saying a refusal's codes back to
/// the agent as the tool's answer, so it may give it again within its turn. The driver judges the file again when the turn ends,
/// and a turn that ends refused gets the driver's one repair, as before.
/// </summary>
public sealed class McpBrainstormToolTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-brainstorm-tool-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private string Answer(string turn) => Path.Combine(_root, "brainstorms", "b1", "answers", $"{turn}.json");

    /// <summary>A turn's room as the driver opens it: the packet with its sources, and for a synthesis the anonymous proposals.</summary>
    private string Room(string turn, bool packet = true)
    {
        var room = Directory.CreateDirectory(Path.Combine(_root, "brainstorm-rooms", "b1", turn)).FullName;
        if (packet)
        {
            File.WriteAllText(Path.Combine(room, "packet.json"), """
                { "packet": 1, "digest": "sha256:00", "question": "Why?", "sources": [{ "id": "S1", "kind": "decision" }, { "id": "S2", "kind": "document" }] }
                """);
        }

        if (turn.StartsWith('s'))
        {
            File.WriteAllText(Path.Combine(room, "proposals.json"), """
                { "brainstorm": 1, "proposals": [{ "id": "P1", "idea": "A" }, { "id": "P2", "idea": "B" }] }
                """);
        }

        return room;
    }

    /// <summary>The connector a brainstorm's turn is handed: its scope names the brainstorm, the turn, the answer file and the room.</summary>
    private BrainstormTools Turn(string turn, bool packet = true) =>
        new(new BrainstormBox(new BrainstormScope("b1", turn, Answer(turn), Room(turn, packet))));

    private static BrainstormProposalInput Proposal(string idea = "Make the window inclusive", string effort = "small") => new(
        idea, "The report reads an exclusive window.", effort, "low", "yes", null, "high", ["the window is exclusive"], ["S1"]);

    /// <summary>
    /// 🔴 Outside a brainstorm's turn both tools are refused and write nothing: a connector that names no turn (every other session's),
    /// and one that names a turn but no answer file.
    /// </summary>
    [Fact]
    public void Both_tools_are_refused_outside_a_brainstorm_turn_and_write_nothing()
    {
        var none = new BrainstormTools(new BrainstormBox(new BrainstormScope(null, null, null, null)));
        var half = new BrainstormTools(new BrainstormBox(new BrainstormScope("b1", "g1", null, Room("g1"))));

        Assert.Contains("serves no brainstorm turn", none.Give([Proposal()]));
        Assert.Contains("serves no brainstorm turn", none.Synthesize([new BrainstormGroupInput("T", "I", ["P1"], 1, "W", null)]));
        Assert.Contains("serves no brainstorm turn", half.Give([Proposal()]));
        Assert.Contains("serves no brainstorm turn", new BrainstormTools().Give([Proposal()]));
        Assert.False(Directory.Exists(Path.Combine(_root, "brainstorms")));
    }

    /// <summary>🔴 Each tool answers its own kind of turn only, naming the other, and writes nothing in the other's.</summary>
    [Fact]
    public void Each_tool_answers_only_its_own_kind_of_turn()
    {
        Assert.Contains("answer through `brainstorm_synthesize`", Turn("s1").Give([Proposal()]));
        Assert.Contains("answer through `brainstorm_give`", Turn("g1").Synthesize([new BrainstormGroupInput("T", "I", ["P1"], 1, "W", null)]));
        Assert.False(File.Exists(Answer("s1")));
        Assert.False(File.Exists(Answer("g1")));
    }

    /// <summary>
    /// 🔴 A refused answer is said back with its codes, in the validator's words, and stays the turn's answer file until a valid one
    /// replaces it (so a turn that ends refused hands its repair those codes); a valid one is kept, once, in the shape the driver reads.
    /// </summary>
    [Fact]
    public void A_refused_answer_is_said_back_with_its_codes_and_a_valid_one_is_kept_once()
    {
        var tools = Turn("g1");

        var refused = tools.Give([Proposal(effort: "tiny"), Proposal() with { Sources = ["S9"] }]);

        Assert.Contains("refused", refused);
        Assert.Contains("field:effort, unknown-source", refused);
        Assert.Contains("again", refused);
        Assert.Contains("\"tiny\"", File.ReadAllText(Answer("g1")));

        var kept = tools.Give([Proposal(), Proposal("Rewrite the stored reports") with { Reversible = "no", Undo = "Restore them.", Risk = "high" }], ["the tests"]);

        Assert.Contains("kept: 2 proposals", kept);
        var answer = JsonNode.Parse(File.ReadAllText(Answer("g1")))!;
        Assert.Equal(1, answer["brainstorm"]!.GetValue<int>());
        Assert.Equal(["the tests"], answer["needs"]!.AsArray().Select(need => need!.GetValue<string>()));
        Assert.Null(answer["none"]);
        var first = answer["proposals"]!.AsArray()[0]!.AsObject();
        Assert.Equal(
            ["idea", "rationale", "effort", "risk", "reversible", "undo", "confidence", "assumptions", "sources"],
            first.Select(field => field.Key));
        Assert.Equal(("Make the window inclusive", "S1"), (first["idea"]!.GetValue<string>(), first["sources"]![0]!.GetValue<string>()));

        Assert.Contains("already kept", tools.Give([Proposal("Another")]));
        Assert.DoesNotContain("Another", File.ReadAllText(Answer("g1")));
    }

    /// <summary>An answer with no proposal is valid only with why, and is kept as such.</summary>
    [Fact]
    public void No_proposal_is_an_answer_only_with_why()
    {
        var tools = Turn("g2");

        Assert.Contains("empty", tools.Give([]));
        Assert.Contains("kept: no proposal", tools.Give(null, ["the tests"], "The packet holds no code."));
        Assert.Equal("The packet holds no code.", JsonNode.Parse(File.ReadAllText(Answer("g2")))!["none"]!.GetValue<string>());
    }

    /// <summary>
    /// 🔴 The synthesis is judged against the ids its room was handed: one it was not handed is refused by code, and every id placed
    /// once, ranked without gap, is kept with its groups and what it left out.
    /// </summary>
    [Fact]
    public void The_synthesis_is_judged_against_the_ids_its_room_was_handed()
    {
        var tools = Turn("s1");

        Assert.Contains("member-unknown", tools.Synthesize([new BrainstormGroupInput("All", "Merged.", ["P1", "P2", "P7"], 1, "It answers.", null)]));

        var kept = tools.Synthesize(
            [new BrainstormGroupInput("First", "Merged.", ["P1"], 1, "It answers.", [new BrainstormFlagInput("risky", "It touches a release.")])],
            [new BrainstormLeftOutInput("P2", "It answers another question.")]);

        Assert.Contains("kept: 1 group, 1 left out", kept);
        var answer = JsonNode.Parse(File.ReadAllText(Answer("s1")))!;
        Assert.Equal(["title", "idea", "members", "rank", "why", "flags"], answer["groups"]![0]!.AsObject().Select(field => field.Key));
        Assert.Equal(("P2", "It answers another question."), (answer["leftOut"]![0]!["id"]!.GetValue<string>(), answer["leftOut"]![0]!["why"]!.GetValue<string>()));
    }

    /// <summary>A room that holds no packet cannot judge an answer: refused, and nothing written.</summary>
    [Fact]
    public void A_room_with_no_packet_keeps_nothing()
    {
        Assert.Contains("packet.json", Turn("g3", packet: false).Give([Proposal()]));
        Assert.False(File.Exists(Answer("g3")));
    }

    // ——— The twin of the driver's validator: ONE table, the driver suite's fixtures/brainstorm-answers.json, row for row.

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "daoris.json"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("no workspace above the tests");
    }

    private static JsonNode Table() => JsonNode.Parse(File.ReadAllText(Path.Combine(
        Root(), "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "brainstorm-answers.json")))!;

    public static TheoryData<string, string> TableRows()
    {
        var rows = new TheoryData<string, string>();
        foreach (var kind in new[] { "give", "synthesize" })
        {
            foreach (var row in Table()[kind]!.AsArray()) rows.Add(kind, row!["name"]!.GetValue<string>());
        }

        return rows;
    }

    [Theory]
    [MemberData(nameof(TableRows))]
    public void Each_row_of_the_shared_table_fails_on_its_codes_in_order_or_is_valid(string kind, string name)
    {
        var table = Table();
        var row = table[kind]!.AsArray().Single(each => each!["name"]!.GetValue<string>() == name)!;
        var text = row["answer"] is { } answer ? answer.ToJsonString() : row["text"]?.GetValue<string>();
        if (row["pad"] is { } pad) text = (text ?? "") + new string(' ', pad.GetValue<int>());
        IReadOnlyList<string> Ids(string list) => table[list]!.AsArray().Select(id => id!.GetValue<string>()).ToList();

        var codes = kind == "give"
            ? BrainstormChecks.Give(text, Ids("sources").ToHashSet(StringComparer.Ordinal))
            : BrainstormChecks.Synthesize(text, Ids("proposals"));

        Assert.Equal(row["codes"]!.AsArray().Select(code => code!.GetValue<string>()), codes);
    }

    // ——— What an agent reads when it chooses a tool.

    private static string Said(string tool) => string.Join(" ", typeof(BrainstormTools).GetMethods()
        .Where(method => method.GetCustomAttribute<McpServerToolAttribute>()?.Name == tool)
        .SelectMany(method => new[] { method.GetCustomAttribute<DescriptionAttribute>()?.Description }
            .Concat(method.GetParameters().Select(parameter => parameter.GetCustomAttribute<DescriptionAttribute>()?.Description)))
        .OfType<string>());

    [Fact]
    public void Each_tool_says_it_is_a_brainstorm_turn_s_alone_once_and_a_claim()
    {
        foreach (var tool in new[] { "brainstorm_give", "brainstorm_synthesize" })
        {
            var text = Said(tool);
            Assert.Contains("Only for a turn Daoris started", text);
            Assert.Contains("once", text);
            Assert.Contains("claim", text);
            Assert.Contains("changes nothing", text);
        }

        Assert.Contains("unknown", Said("brainstorm_give"));
        Assert.Contains("P1", Said("brainstorm_synthesize"));
    }
}

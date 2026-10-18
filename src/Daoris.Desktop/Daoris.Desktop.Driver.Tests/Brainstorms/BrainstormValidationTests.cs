using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// BRAINSTORM1a (D176 points 2-4): the no-model tier of a brainstorm, on values. The packet is frozen within its bounds, names no
/// machine path, and is named by its content's digest; a generator's answer is held to its schema, its scales and its bounds, and a
/// failure is codes, never part of an answer; the synthesis is handed anonymous ids with the makers interleaved, is held to placing
/// every id once and ranking without gap or tie, and Daoris re-attaches who gave which and keeps its own flags on each group.
/// </summary>
public sealed class BrainstormValidationTests
{
    private static readonly IReadOnlySet<string> Sources = new HashSet<string> { "S1", "S2" };

    private static JsonObject Proposal(string idea = "Make the window inclusive", string reversible = "yes", string risk = "low") => new()
    {
        ["idea"] = idea, ["rationale"] = "The report reads an exclusive window.", ["effort"] = "small", ["risk"] = risk,
        ["reversible"] = reversible, ["undo"] = reversible == "yes" ? null : "Restore it from the backup.", ["confidence"] = "high",
        ["assumptions"] = new JsonArray("the window is exclusive"), ["sources"] = new JsonArray("S1"),
    };

    private static string Answer(params JsonObject[] proposals) =>
        new JsonObject { ["brainstorm"] = 1, ["proposals"] = new JsonArray([.. proposals]), ["needs"] = new JsonArray(), ["none"] = null }.ToJsonString();

    [Fact]
    public void The_packet_is_frozen_within_its_bounds_and_named_by_its_content_s_digest()
    {
        var found = new List<BrainstormFound>
        {
            new("decision", "docs/decisions/D7.md", 3, 9, new string('a', 50), "report"),
            new("decision", "docs/decisions/D7.md", 3, 9, new string('a', 50), "daily report"),
            // A machine path, spelled in pieces as SessionSyncTests spells one (a literal one fails the universal gate).
            new("knowledge", string.Join('/', "C:", "Users", "someone", "notes.md"), null, null, "private", "report"),
            new("knowledge", "../elsewhere/notes.md", null, null, "outside", "report"),
            new("document", "docs/report.md", null, null, new string('b', 80), "report"),
            new("document", "docs/more.md", null, null, new string('c', 80), "report"),
        };

        var packet = BrainstormPackets.Freeze("Why?", "reports", "words", "abc123", found, new BrainstormBounds(Sources: 2, Each: 60, All: 100));

        // The same path and range found twice is one source; a machine path and a climb out are left out unnamed; the bounds cut.
        Assert.Equal(["S1", "S2"], packet.Sources.Select(source => source.Id));
        Assert.Equal((50, 50), (packet.Sources[0].Kept, packet.Sources[0].Of));
        Assert.Equal((50, 80), (packet.Sources[1].Kept, packet.Sources[1].Of));
        Assert.Equal(
            [("(not a repository path)", "machine-path"), ("(not a repository path)", "machine-path"), ("docs/more.md", "bound")],
            packet.LeftOut.Select(left => (left.Path, left.Why)));
        var bytes = BrainstormPackets.Bytes(packet);
        var text = Encoding.UTF8.GetString(bytes);
        Assert.DoesNotContain("Users", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", text, StringComparison.Ordinal);
        Assert.StartsWith("{\n  \"packet\": 1,\n  \"digest\": \"sha256:", text, StringComparison.Ordinal);

        // Read back it is the same packet; a changed byte of content is not the packet its digest names.
        Assert.Equal(packet.Digest, BrainstormPackets.Read(bytes)!.Digest);
        Assert.Null(BrainstormPackets.Read(Encoding.UTF8.GetBytes(text.Replace("Why?", "How?"))));
        Assert.NotEqual(packet.Digest, BrainstormPackets.Freeze("How?", "reports", "words", "abc123", found, BrainstormBounds.First).Digest);
    }

    [Fact]
    public void A_question_with_no_repository_is_the_question_alone_and_is_searched_in_more_than_one_wording()
    {
        var packet = BrainstormPackets.Freeze("Why?", null, "words", null, [new("decision", "docs/D1.md", 1, 2, "x", "x")], BrainstormBounds.First);
        Assert.Null(packet.Tier);

        Assert.Equal(["Why does the daily report drop its last day?", "daily report"], BrainstormWordings.Of("Why does the daily report drop its last day?"));
        Assert.Equal(["Why?"], BrainstormWordings.Of("Why?"));
    }

    [Fact]
    public void A_valid_answer_is_kept_whole_with_Daoris_s_flags_read_from_its_fields()
    {
        var checkedAnswer = BrainstormAnswers.Check(Answer(Proposal(), Proposal("Rewrite history", "no", "unknown")), Sources);

        Assert.True(checkedAnswer.Valid);
        Assert.Empty(checkedAnswer.Answer!.Proposals[0].Flags);
        Assert.Equal(["hard-to-reverse", "risky"], checkedAnswer.Answer.Proposals[1].Flags);
        Assert.Equal(["hard-to-reverse"], (Proposal("Partly", "partly") is var p ? BrainstormAnswers.Check(Answer(p), Sources).Answer!.Proposals[0].Flags : []));
    }

    public static TheoryData<string, string> Malformed => new()
    {
        { "missing", "" },
        { "shape", "not json" },
        { "shape", """{ "brainstorm": 2, "proposals": [] }""" },
        { "empty", """{ "brainstorm": 1, "proposals": [], "none": null }""" },
        { "proposals-beside-none", Answer(Proposal()).Replace("\"none\":null", "\"none\":\"nothing fits\"") },
        { "field:effort", Answer(Proposal()).Replace("\"small\"", "\"tiny\"") },
        { "field:undo", Answer(Proposal("Rewrite", "partly")).Replace("Restore it from the backup.", "") },
        { "field:idea-length", Answer(Proposal(new string('i', 201))) },
        { "field:assumptions-count", Answer(Proposal()).Replace("[\"the window is exclusive\"]", "[\"a\",\"b\",\"c\",\"d\",\"e\",\"f\"]") },
        { "unknown-source", Answer(Proposal()).Replace("[\"S1\"]", "[\"S9\"]") },
        { "idea-twice", Answer(Proposal("Same idea"), Proposal("same  IDEA")) },
        { "too-many", Answer([.. Enumerable.Range(1, 9).Select(n => Proposal($"Idea {n}"))]) },
        { "too-long", new string(' ', 20_001) },
    };

    [Theory]
    [MemberData(nameof(Malformed))]
    public void A_malformed_answer_is_no_answer_and_says_its_codes(string code, string text)
    {
        var checkedAnswer = BrainstormAnswers.Check(code == "missing" ? null : text, Sources);

        Assert.False(checkedAnswer.Valid);
        Assert.Null(checkedAnswer.Answer);
        Assert.Contains(code, checkedAnswer.Codes);
    }

    [Fact]
    public void An_answer_with_none_and_no_proposal_is_valid()
    {
        var text = new JsonObject { ["brainstorm"] = 1, ["proposals"] = new JsonArray(), ["needs"] = new JsonArray("the tests"), ["none"] = "The packet holds no code." }.ToJsonString();

        var checkedAnswer = BrainstormAnswers.Check(text, Sources);

        Assert.True(checkedAnswer.Valid);
        Assert.Equal(("The packet holds no code.", "the tests"), (checkedAnswer.Answer!.None, checkedAnswer.Answer.Needs.Single()));
    }

    private static IReadOnlyList<BrainstormAnonymous> Handed()
    {
        var one = BrainstormAnswers.Check(Answer(Proposal("A1"), Proposal("A2", "no", "high")), Sources).Answer!;
        var two = BrainstormAnswers.Check(Answer(Proposal("B1")), Sources).Answer!;
        return BrainstormSyntheses.Number([(new BrainstormBy("g1", "anthropic", "claude-code", "work"), one), (new BrainstormBy("g2", "openai", "codex-acp", null), two)]);
    }

    private static string Synthesis(string groups, string leftOut = "[]") => $$"""{ "brainstorm": 1, "groups": {{groups}}, "leftOut": {{leftOut}} }""";

    private static string Group(int rank, params string[] members) =>
        $$"""{ "title": "Group {{rank}}", "idea": "Merged.", "members": [{{string.Join(",", members.Select(m => $"\"{m}\""))}}], "rank": {{rank}}, "why": "It answers.", "flags": [] }""";

    [Fact]
    public void The_synthesis_is_handed_anonymous_ids_with_the_makers_interleaved()
    {
        var handed = Handed();

        Assert.Equal([("P1", "A1"), ("P2", "B1"), ("P3", "A2")], handed.Select(each => (each.Id, each.Proposal.Idea)));
        var given = Encoding.UTF8.GetString(BrainstormSyntheses.Anonymous(handed));
        Assert.DoesNotContain("anthropic", given, StringComparison.Ordinal);
        Assert.DoesNotContain("claude-code", given, StringComparison.Ordinal);
        Assert.DoesNotContain("g1", given, StringComparison.Ordinal);
        Assert.DoesNotContain("work", given, StringComparison.Ordinal);
    }

    public static TheoryData<string, string> Unplaced => new()
    {
        { "member-missing", Synthesis($"[{Group(1, "P1", "P2")}]") },
        { "member-twice", Synthesis($"[{Group(1, "P1", "P2")}, {Group(2, "P2", "P3")}]") },
        { "member-unknown", Synthesis($"[{Group(1, "P1", "P2", "P3", "P4")}]") },
        { "ranks", Synthesis($"[{Group(1, "P1")}, {Group(1, "P2", "P3")}]") },
        { "ranks", Synthesis($"[{Group(1, "P1")}, {Group(3, "P2", "P3")}]") },
        { "field:why", Synthesis($"[{Group(1, "P1", "P2")}]", """[{ "id": "P3" }]""") },
        { "flag-unknown", Synthesis($"[{Group(1, "P1", "P2", "P3").Replace("\"flags\": []", "\"flags\": [{ \"flag\": \"fine\", \"why\": \"x\" }]")}]") },
    };

    [Theory]
    [MemberData(nameof(Unplaced))]
    public void A_synthesis_that_does_not_place_every_id_once_or_ranks_with_a_gap_or_a_tie_is_refused(string code, string text)
    {
        var checkedSynthesis = BrainstormSyntheses.Check(text, Handed());

        Assert.False(checkedSynthesis.Valid);
        Assert.Contains(code, checkedSynthesis.Codes);
    }

    [Fact]
    public void Daoris_re_attaches_who_gave_which_and_keeps_its_flags_on_each_group()
    {
        var handed = Handed();
        var synthesis = BrainstormSyntheses.Check(
            Synthesis($"[{Group(1, "P1", "P2")}]", """[{ "id": "P3", "why": "It rewrites history." }]"""), handed).Synthesis!;
        var packet = BrainstormPackets.Freeze("Why?", null, null, null, [], BrainstormBounds.First);
        var passes = new[]
        {
            new BrainstormPassRecord(handed[0].By, BrainstormPassRecord.Generate, BrainstormPassRecord.Answered, [], 1),
            new BrainstormPassRecord(handed[1].By, BrainstormPassRecord.Generate, BrainstormPassRecord.Answered, [], 1),
        };

        var document = BrainstormDocuments.Ranked("b1", packet, passes, handed, synthesis, []);

        var row = Assert.Single(document.Rows);
        Assert.Equal([("P1", "anthropic", "claude-code", "g1"), ("P2", "openai", "codex-acp", "g2")],
            row.Members.Select(member => (member.Id, member.By.Maker, member.By.Adapter, member.By.Pass)));
        Assert.Equal(2, row.Makers);
        var (left, why) = Assert.Single(document.LeftOut);
        Assert.Equal(("P3", "anthropic", "It rewrites history."), (left.Id, left.By.Maker, why));
        Assert.False(document.Stubs);
        Assert.Equal(["anthropic", "openai"], document.Makers);
        var printed = BrainstormDocuments.Markdown(document);
        Assert.Contains("Named by 2 makers", printed, StringComparison.Ordinal);
        Assert.DoesNotContain("no maker diversity", printed, StringComparison.Ordinal);

        // A group holding a flagged member carries Daoris's flag, whatever the synthesis said.
        var flagged = BrainstormSyntheses.Check(Synthesis($"[{Group(1, "P1", "P3")}, {Group(2, "P2")}]"), handed).Synthesis!;
        var rows = BrainstormDocuments.Ranked("b1", packet, passes, handed, flagged, []).Rows;
        Assert.Equal([("hard-to-reverse", "daoris"), ("risky", "daoris")], rows[0].Flags.Select(flag => (flag.Flag, flag.By)));
        Assert.Empty(rows[1].Flags);
    }
}

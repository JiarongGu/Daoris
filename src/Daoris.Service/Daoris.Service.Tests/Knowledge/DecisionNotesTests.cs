using System.Text.Json;
using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// A decision's dated notes, each found where the decisions digest finds it and labelled as the digest labels it
/// (ORIENT1g; D134 §5 as amended). D125 is one file and twenty-two notes, and a question about one note was
/// answered with the whole file, or not at all: the note was buried under every other.
/// </summary>
/// <remarks>
/// The service's half of a TWIN with the digest, <c>tools/orient-index.mjs</c>: the two read a decision with code
/// of their own, and both are held to one table, <c>tools/orient-index-fixtures/decision-notes.json</c>, row for
/// row (ORIENT1h). <c>tools/orient-index.test.mjs</c> holds the digest to it. Before ORIENT1h this side was held
/// to rows the digest wrote once, inline here, so a note's form changed on the digest's side alone passed every gate.
/// </remarks>
public sealed class DecisionNotesTests
{
    private static readonly JsonElement Table = ReadTable();

    /// <summary>
    /// The table's first decision, D7: every form a note takes, a fence, a glued label and a label twice. The
    /// scanner's tests write it as a decision's file.
    /// </summary>
    internal static string Decision => Text(Decided("D7"));

    /// <summary>The digest's rows for <see cref="Decision"/>: <c>D7:&lt;first&gt;-&lt;last&gt; &lt;label&gt;</c>.</summary>
    internal static IReadOnlyList<string> DigestRows => Rows(Decided("D7"), "notes");

    /// <summary>
    /// Each decision of the table split as the digest splits it: each note on the digest's lines with its label,
    /// and the decision's own entry on the lines of the digest's title row, the blank lines at its end left out.
    /// </summary>
    [Theory]
    [MemberData(nameof(Decisions))]
    public void Each_note_is_found_on_the_lines_the_digest_gives_it_with_the_digest_s_label(string id)
    {
        var decision = Decided(id);

        var (entry, notes) = DecisionNotes.Split(Text(decision));

        Assert.Equal(Rows(decision, "notes"), notes.Select(note => $"{id}:{note.First}-{note.Last} {note.Label}"));
        Assert.Equal(decision.GetProperty("entry").GetString(), $"{id}:1-{LastWritten(entry)}");
    }

    /// <summary>The digest's title row says the entry's own lines before its notes: D7:1-5.</summary>
    [Fact]
    public void The_entry_is_the_decision_before_its_first_note()
    {
        var (entry, notes) = DecisionNotes.Split(Decision);

        Assert.Equal(string.Join('\n', Decision.Split('\n')[..5]), entry);
        Assert.StartsWith("**Built 2026-10-02 (TOOL4a)", notes[0].Body);
        Assert.EndsWith("```", notes[0].Body);
        // A note's text is its lines, its label's first: the label stays searchable in full.
        Assert.Contains("hand-kept or\ngenerated\".**", notes[1].Body);
        Assert.Contains("**Proven without the rehearsal**", notes[3].Body);
    }

    [Fact]
    public void A_decision_with_no_note_is_its_entry_whole()
    {
        const string text = "## D1 — one (2026-08-04)\n\n**Decision.** Why.\n\n**Why.** Because.";

        var (entry, notes) = DecisionNotes.Split(text);

        Assert.Equal(text, entry);
        Assert.Empty(notes);
    }

    /// <summary>
    /// The label is the emphasised span that opens the line, when it is dated or in a form the record writes
    /// undated, cut to a row at a word with an ellipsis (the digest's <c>noteLabel</c>).
    /// </summary>
    [Theory]
    [MemberData(nameof(Labels))]
    public void A_label_is_read_as_the_digest_reads_it(string why, string line, string? label)
    {
        var read = DecisionNotes.Label(line);

        Assert.True(read == label, $"{why}: expected {label ?? "none"}, read {read ?? "none"}");
    }

    /// <summary>A table read as empty would hold nothing, so each side checks it holds both kinds of row.</summary>
    [Fact]
    public void The_table_holds_a_decision_with_notes_and_one_without_and_a_label_and_a_line_that_is_none()
    {
        var decisions = Table.GetProperty("decisions").EnumerateArray().ToList();
        Assert.Contains(decisions, decision => decision.GetProperty("notes").GetArrayLength() > 0);
        Assert.Contains(decisions, decision => decision.GetProperty("notes").GetArrayLength() == 0);
        Assert.Contains(Labels(), row => row[2] is not null);
        Assert.Contains(Labels(), row => row[2] is null);
    }

    public static TheoryData<string> Decisions()
    {
        var data = new TheoryData<string>();
        foreach (var decision in Table.GetProperty("decisions").EnumerateArray()) data.Add(decision.GetProperty("id").GetString()!);
        return data;
    }

    public static TheoryData<string, string, string?> Labels()
    {
        var data = new TheoryData<string, string, string?>();
        foreach (var row in Table.GetProperty("labels").EnumerateArray())
        {
            data.Add(row[0].GetString()!, row[1].GetString()!, row[2].ValueKind == JsonValueKind.Null ? null : row[2].GetString());
        }

        return data;
    }

    private static JsonElement Decided(string id) =>
        Table.GetProperty("decisions").EnumerateArray().Single(decision => decision.GetProperty("id").GetString() == id);

    /// <summary>A decision's file, its lines joined as the digest's test writes them.</summary>
    private static string Text(JsonElement decision) =>
        string.Join('\n', decision.GetProperty("lines").EnumerateArray().Select(line => line.GetString()!));

    private static IReadOnlyList<string> Rows(JsonElement decision, string name) =>
        [.. decision.GetProperty(name).EnumerateArray().Select(row => row.GetString()!)];

    /// <summary>The last line of a text that is not blank, from 1: where the digest ends a part.</summary>
    private static int LastWritten(string text)
    {
        var lines = text.Split('\n');
        var last = lines.Length;
        while (last > 1 && lines[last - 1].Trim().Length == 0) last--;
        return last;
    }

    private static JsonElement ReadTable()
    {
        using var table = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "tools", "orient-index-fixtures", "decision-notes.json")));
        return table.RootElement.Clone();
    }

    /// <summary>The workspace root, found by walking up from the test binaries to <c>daoris.json</c>.</summary>
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "daoris.json"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("no workspace root above the test binaries");
    }
}

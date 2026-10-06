using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// A decision's dated notes, each found where the decisions digest finds it and labelled as the digest labels it
/// (ORIENT1g; D134 §5 as amended). D125 is one file and twenty-two notes, and a question about one note was
/// answered with the whole file, or not at all: the note was buried under every other.
/// </summary>
/// <remarks>
/// The expected rows are what <c>tools/orient-index.mjs</c> wrote for <see cref="Decision"/>: the digest and the
/// service read one file with code of their own, so this table is what each is held to (twins).
/// </remarks>
public sealed class DecisionNotesTests
{
    internal const string Decision = """
        ## D7 — The tier is the directory (2026-08-04)

        **Decision (TIER1, 2026-08-04).** The tier is where the file is, and an opening dated is never a note.

        **Why.** Because the harness decides by path.

        **Built 2026-10-02 (TOOL4a): the limit table and its reader** (point 1). The table reads the agent's words.
        - A list item under the note.

        ```markdown
        **Built 2026-10-09 (FENCED): inside a fence, never a note**
        ```

        **Amended 2026-10-04 (ORIENT1b): a generated digest of the record, against point 2's "no index, hand-kept or
        generated".** The design's reason was that a generated index needs a generator.

        **DRIFT1a, built 2026-10-02: a note named by its task first.** Its text.

        *As built (PLUGUI1d, 2026-10-01)*: an italic label.
        **Fixed 2026-10-03: glued to the line above, so read as that note's text.**

        **Proven without the rehearsal** — an emphasised sentence, not a note.

        ### 2026-10-05 — a dated heading

        Under the heading.

        **Measured 2026-10-06: a rare word counts with a date.** Its text.

        **Measured later: and not without one.** Still the note above.

        **Built 2026-10-07 (TOOL6g): a signed-out account is said, and asked about once per sign-out** (points 4, 6 and 7). One status question per account at a time (`ProbeLock`).

        **Built 2026-10-07 (TOOL6g): a signed-out account is said, and asked about once per sign-out** (a second time).
        """;

    /// <summary>The digest's rows for <see cref="Decision"/>: <c>D7:&lt;first&gt;-&lt;last&gt; &lt;label&gt;</c>.</summary>
    internal static readonly string[] DigestRows =
    [
        "D7:7-12 Built 2026-10-02 (TOOL4a): the limit table and its reader",
        "D7:14-15 Amended 2026-10-04 (ORIENT1b): a generated digest of the record, against point…",
        "D7:17-17 DRIFT1a, built 2026-10-02: a note named by its task first.",
        "D7:19-22 As built (PLUGUI1d, 2026-10-01)",
        "D7:24-26 2026-10-05 — a dated heading",
        "D7:28-30 Measured 2026-10-06: a rare word counts with a date.",
        "D7:32-32 Built 2026-10-07 (TOOL6g): a signed-out account is said, and asked about once…",
        "D7:34-34 Built 2026-10-07 (TOOL6g): a signed-out account is said, and asked about once…",
    ];

    [Fact]
    public void Each_note_is_found_on_the_lines_the_digest_gives_it_with_the_digest_s_label()
    {
        var (_, notes) = DecisionNotes.Split(Decision);

        Assert.Equal(DigestRows, notes.Select(note => $"D7:{note.First}-{note.Last} {note.Label}"));
    }

    /// <summary>The digest's title row says the entry's own lines before its notes: D7:1-5.</summary>
    [Fact]
    public void The_entry_is_the_decision_before_its_first_note()
    {
        var (entry, notes) = DecisionNotes.Split(Decision);

        Assert.Equal(string.Join('\n', Decision.Replace("\r\n", "\n").Split('\n')[..5]), entry);
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
    [InlineData("**Built 2026-10-03 (DOC8b): the check** (point 4).", "Built 2026-10-03 (DOC8b): the check")]
    [InlineData("**Built.** Undated, and a form the record writes.", "Built.")]
    [InlineData("**As built (ACP1)**: undated too.", "As built (ACP1)")]
    [InlineData("*Amended (UX6e)*: italic.", "Amended (UX6e)")]
    [InlineData("**Noted 2026-10-01: a rarer word, dated**", "Noted 2026-10-01: a rarer word, dated")]
    [InlineData("**ORIENT1c, built 2026-10-04: the server is a deployment.**", "ORIENT1c, built 2026-10-04: the server is a deployment.")]
    [InlineData("**Reviewed 2026-10-01 with no closing mark", "Reviewed 2026-10-01 with no closing mark")]
    [InlineData("**Noted, with no date.** A rarer word needs one.", null)]
    [InlineData("**Proven without the rehearsal** — a sentence.", null)]
    [InlineData("**Decision.** The opening's form.", null)]
    [InlineData("* a list item 2026-10-01", null)]
    [InlineData("Plain text dated 2026-10-01.", null)]
    [InlineData("**", null)]
    public void A_label_is_read_as_the_digest_reads_it(string line, string? label)
    {
        Assert.Equal(label, DecisionNotes.Label(line));
    }
}

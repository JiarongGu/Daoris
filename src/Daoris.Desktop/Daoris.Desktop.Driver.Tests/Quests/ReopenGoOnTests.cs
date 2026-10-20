using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-CLOSE1c5 (§E5): which commit a step the person went on with is brought up to date with. Its source's current submission's,
/// where the step grew from that one now, and none otherwise. Runs no process: the fast half (MOD8).
/// </summary>
public sealed class ReopenGoOnTests
{
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";

    private static readonly QuestView Step =
        new("q8", "game", "engine", "Next", "", "Taken") { Parent = "q7", GrewFrom = new QuestSubmissionRef("desk", 43) };

    private static QuestView Source(QuestSubmissionView? submission) =>
        new("q7", "game", "engine", "Ship it", "", "Taken") { Closure = "gph-0123", Submission = submission };

    [Fact]
    public void The_sources_current_submission_names_the_commit_where_the_step_grew_from_it()
    {
        Assert.Equal(Commit, Planner.SourceCommit(Step, Source(new QuestSubmissionView("desk", 43, null, "ready", Commit))));
    }

    [Fact]
    public void No_commit_where_the_source_is_unread_names_another_submission_none_or_no_commit()
    {
        Assert.Null(Planner.SourceCommit(Step, null));
        Assert.Null(Planner.SourceCommit(Step, Source(null)));
        Assert.Null(Planner.SourceCommit(Step, Source(new QuestSubmissionView("desk", 44, null, "ready", Commit))));
        Assert.Null(Planner.SourceCommit(Step, Source(new QuestSubmissionView("laptop", 43, null, "ready", Commit))));
        Assert.Null(Planner.SourceCommit(Step, Source(new QuestSubmissionView("desk", 43, null, "ready"))));
        Assert.Null(Planner.SourceCommit(Step with { GrewFrom = null }, Source(new QuestSubmissionView("desk", 43, null, "ready", Commit))));
        Assert.Null(Planner.SourceCommit(Step, Source(new QuestSubmissionView("desk", 43, null, "ready", Commit)) with { Id = "q6" }));
    }

    // FABRIC-CLOSE1c13: which going-on is brought up to date with the source first. The session the reopen stopped, as CLOSE1c5 has
    // it, and a session that ended normally going on with the words Daoris told it of the go-on to the submission the step grows
    // from now; every other going-on syncs with the line alone, as before. FABRIC-CLOSE1c17 (COWORK1b): the gate reads the word's
    // mark, Daoris's with the go-on's key, and never its sentence.

    private static readonly QuestSubmissionRef Grew = new("desk", 43);

    private static PriorSession Record(params SaidWordView[] words) =>
        new("s1", "X:/daoris/trees/s1", "awaiting-person", "It stopped with its quest still taken, to ask you:", Repository: "engine")
        {
            Said = [.. words],
        };

    private static SaidWordView Persons(string text, int at = 0) => new($"w{at}", text, DateTimeOffset.UnixEpoch, [], Reopens: false);

    /// <summary>Daoris's word of a go-on, under the key of <paramref name="step"/> and <paramref name="grew"/>, in any words.</summary>
    private static SaidWordView Told(string step = "q8", QuestSubmissionRef? grew = null, string? text = null) =>
        new("wd", text ?? ReopenTelling.GoneOnWords(step, "q7", grew ?? Grew), DateTimeOffset.UnixEpoch, [], Reopens: false)
        {
            From = DaorisWords.From, Key = DaorisKeys.GoneOn(step, grew ?? Grew),
        };

    [Fact]
    public void A_session_going_on_with_the_go_ons_words_is_brought_up_to_date_with_the_source()
    {
        Assert.True(ReopenTelling.BringsToSource(Step, Record(Told())));
        Assert.True(ReopenTelling.BringsToSource(Step, Record(Persons("Also add the changelog line."), Told())));
        // The mark is read, never the sentence: Daoris's go-on worded otherwise brings it up to date all the same.
        Assert.True(ReopenTelling.BringsToSource(Step, Record(Told(text: "Daoris: went on with #q8 on `desk` 43."))));
    }

    /// <summary>
    /// FABRIC-CLOSE1c17's three cases: the person's words equal to the go-on's sentence are theirs and bring nothing up to date;
    /// Daoris's do (above); and a word an older host kept, which carries no mark, reads as the person's.
    /// </summary>
    [Fact]
    public void The_persons_words_equal_to_the_go_ons_sentence_and_an_older_hosts_word_are_synced_with_the_line_alone()
    {
        var sentence = ReopenTelling.GoneOnWords("q8", "q7", Grew);
        var older = ServiceClient.ReadWord(System.Text.Json.JsonDocument.Parse(
            $$"""{ "id": "w1", "text": {{System.Text.Json.JsonSerializer.Serialize(sentence)}}, "at": "2026-10-20T10:00:00Z", "files": [], "reopens": true }""").RootElement)!;

        Assert.False(ReopenTelling.BringsToSource(Step, Record(Persons(sentence))));
        Assert.True(older.Persons);
        Assert.False(ReopenTelling.BringsToSource(Step, Record(older)));
    }

    [Fact]
    public void Any_other_going_on_is_synced_with_the_line_alone()
    {
        Assert.False(ReopenTelling.BringsToSource(Step, Record()));
        Assert.False(ReopenTelling.BringsToSource(Step, Record(Persons("Also add the changelog line."))));
        // Told of a go-on to another submission, which a later go-on moved past, or for another quest.
        Assert.False(ReopenTelling.BringsToSource(Step, Record(Told(grew: new QuestSubmissionRef("desk", 41)))));
        Assert.False(ReopenTelling.BringsToSource(Step, Record(Told(step: "q9"))));
        // Daoris's word of a reopen, not of a go-on.
        Assert.False(ReopenTelling.BringsToSource(Step, Record(Told() with { Key = DaorisKeys.Reopen("q8", "desk", 43) })));
        // A step grown from no submission, or one with a result again.
        Assert.False(ReopenTelling.BringsToSource(Step with { GrewFrom = null }, Record(Told())));
        Assert.False(ReopenTelling.BringsToSource(Step with { Submission = new QuestSubmissionView("desk", 47, null, "ready") }, Record(Told())));
    }
}

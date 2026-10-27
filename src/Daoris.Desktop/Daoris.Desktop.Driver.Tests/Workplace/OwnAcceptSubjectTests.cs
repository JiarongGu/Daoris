using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// ACCEPTALL1f (D167's 2026-10-15 amendment point 2): every door that lands one session names it as the review's Accept does since
/// ACCEPTALL1b, by one call (<see cref="SessionTrees.OwnSubjectAsync"/>): the review's <c>LANDING</c> and <c>LAND_SESSION_TREE</c>, the
/// terminal's <c>trees land</c> and the look's landing at done. A part of an entry with others is named for its entry's first part and
/// joins the entry's branch; a session alone, a merge rule and a name the person entered land as before. The tree here is one no git
/// holds, so the entry's branch is the rule's name for the first part, read with no git; <c>EntryLandingTests</c> and
/// <c>AutoLandingTests</c> hold the doors over real git.
/// </summary>
public sealed class OwnAcceptSubjectTests : IDisposable
{
    private const string Branch = "feature/q1-fix-the-gap";

    private readonly string _scratch;
    private readonly string _home;
    private readonly Dictionary<string, QuestView> _quests = new(StringComparer.OrdinalIgnoreCase)
    {
        ["q1"] = new("q1", "game", "engine", "Fix the gap", "Fix it.", "Done"),
        ["q2"] = new("q2", "game", "engine", "Check the gap", "Check it.", "Done"),
        ["q3"] = new("q3", "game", "engine", "Paint the wall", "Paint it.", "Done"),
    };

    public OwnAcceptSubjectTests()
    {
        _scratch = Path.Combine(RepoRoot(), "_fixtures", "own-accept-subject", Guid.NewGuid().ToString("N")[..8]);
        _home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(_home);
    }

    public void Dispose()
    {
        GitSeed.Discard(_scratch);
    }

    /// <summary>The row's choice: a later part of an entry lands under its entry's first part's name, recording its own session.</summary>
    [Fact]
    public async Task A_part_of_an_entry_with_others_joins_its_entrys_branch_named_for_its_first_part()
    {
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", AutoAccept: true));

        var later = await SubjectAsync("s2", "q2", Entry());
        var first = await SubjectAsync("s1", "q1", Entry());

        Assert.Equal(("s2", "q1", Branch, true), (later.Session, later.Quest, later.EntryBranch, later.Joins));
        Assert.Equal(("s1", "q1", Branch, true), (first.Session, first.Quest, first.EntryBranch, first.Joins));
    }

    /// <summary>A session alone in its entry, or with no record here, is named for its own quest and joins nothing, as before.</summary>
    [Fact]
    public async Task A_session_alone_or_with_no_record_lands_as_before()
    {
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"));

        var alone = await SubjectAsync("s3", "q3", [.. Entry(), Record("s3", "ask:a9/engine", "q3", 0)]);
        var unrecorded = await SubjectAsync("s4", "q3", Entry());

        Assert.Equal(("s3", "q3", null, false), (alone.Session, alone.Quest, alone.EntryBranch, alone.Joins));
        Assert.Equal(("s4", "q3", null, false), (unrecorded.Session, unrecorded.Quest, unrecorded.EntryBranch, unrecorded.Joins));
    }

    /// <summary>A merge rule makes no branch to join; a name the person entered lands as entered and joins nothing.</summary>
    [Fact]
    public async Task A_merge_rule_or_an_entered_name_joins_nothing()
    {
        Rule(LandingRule.Merge);
        var merged = await SubjectAsync("s2", "q2", Entry());
        Assert.Equal(("s2", null, false), (merged.Session, merged.EntryBranch, merged.Joins));

        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"));
        var entered = await SubjectAsync("s2", "q2", Entry()) with { Entered = "feature/mine" };
        Assert.Equal((Branch, false), (entered.EntryBranch, entered.Joins));
    }

    /// <summary>
    /// FABRIC-UNITLINK1 and FABRIC-CLOSE1h13 (D173's CLOSELAND1 note): whatever the landing is named for, the subject keeps the quest the
    /// session itself served and its current submission as the door read it, which the landing's record keeps: a later part of an entry
    /// its own, not its first part's; a session alone its own; a conversation none.
    /// </summary>
    [Fact]
    public async Task The_subject_keeps_the_quest_its_session_served_and_its_submission_as_read()
    {
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", AutoAccept: true));
        _quests["q2"] = _quests["q2"] with { Status = "Taken", Closure = "sha256:closure", Submission = new QuestSubmissionView("desk", 43, null, "ready") };

        var later = await SubjectAsync("s2", "q2", Entry());
        var first = await SubjectAsync("s1", "q1", Entry());
        var alone = await SubjectAsync("s3", "q3", []);
        var chat = await new SessionTrees(_home).OwnSubjectAsync(
            DaorisHome.Entry(_home, HomeEntry.Trees, "aurora", "engine", "a1b2c3d4"), "s5", null, [], _ => Task.FromResult<QuestView?>(null),
            _ => "tidy the docs", process: null);

        Assert.Equal(("q1", "q2", new QuestSubmissionRef("desk", 43)), (later.Quest, later.Served, later.Submission));
        Assert.Equal(("q1", "q1", (QuestSubmissionRef?)null), (first.Quest, first.Served, first.Submission));
        Assert.Equal(("q3", "q3", (QuestSubmissionRef?)null), (alone.Quest, alone.Served, alone.Submission));
        Assert.Equal(((string?)null, (QuestSubmissionRef?)null), (chat.Served, chat.Submission));
    }

    private static List<SessionRecord> Entry() => [Record("s2", "ask:a3/engine", "q2", 2), Record("s1", "ask:a3/engine", "q1", 1)];

    private static SessionRecord Record(string id, string entry, string quest, int minute) =>
        new(id, "engine", "completed") { Entry = entry, Quest = quest, Created = new DateTimeOffset(2026, 10, 16, 9, minute, 0, TimeSpan.Zero) };

    private Task<LandingSubject> SubjectAsync(string session, string quest, IReadOnlyList<SessionRecord> records) =>
        new SessionTrees(_home).OwnSubjectAsync(
            DaorisHome.Entry(_home, HomeEntry.Trees,"aurora", "engine", "a1b2c3d4"), session, quest, records,
            id => Task.FromResult(_quests.GetValueOrDefault(id)), _ => null, process: null);

    private void Rule(LandingRule rule) => DriverConfig.Empty.WithLanding("engine", rule).Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));

    /// <summary>The checkout this build runs from: a linked worktree's <c>.git</c> is a file, so it stops there too.</summary>
    private static string RepoRoot() => GitSeed.RepoRoot();
}

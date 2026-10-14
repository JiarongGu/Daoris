using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// XAGENT1c (D155 points 5, 7, 8, 10 and 11; the second agent design §5.4, §6.1–§6.2, §6.4): a second opinion on the record.
/// The local host keeps each pass beside the session records, with its reviewer's record a chat that names it; the reviewer
/// says its opinion once, within the rule's minutes; its findings reach the working session as another agent's words, never
/// the person's; the working session answers each; and the bounds hold: one pass the rule starts, one recheck. A shared host
/// keeps none.
/// </summary>
public sealed class OpinionStoreTests : IAsyncLifetime
{
    private const string Base = "1111111111111111111111111111111111111111";
    private const string Tip = "2222222222222222222222222222222222222222";
    private const string Later = "3333333333333333333333333333333333333333";
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-09T10:00:00Z");

    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-opinion-" + Guid.NewGuid().ToString("N")[..8]);
    private SqliteConnection _connection = null!;
    private SessionStore _sessions = null!;
    private OpinionStore _opinions = null!;
    private OpinionDesk _desk = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _sessions = await SessionStore.OpenAsync(_connection);
        _opinions = await OpinionStore.OpenAsync(_connection);
        _desk = new OpinionDesk(_opinions, _sessions);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    private string Tree(string name) => Path.Combine(_root, name);

    /// <summary>A driven session of this machine's on <c>Owner</c>, in a tree of its own, ended as it would be when its work is read.</summary>
    private async Task<Session> WorkingAsync(string tree = "work", SessionState state = SessionState.Completed)
    {
        var session = await _sessions.CreateAsync("q1", "Owner", "claude-code", Now, tree: Tree(tree));
        return (await _sessions.SetStateAsync(session.Id, state, "landed.", null, null, Now))!;
    }

    private OpinionAsk Ask(
        string working, string occasion = Opinions.Landing, string pass = Opinions.First, string? rechecks = null,
        string baseCommit = Base, string tip = Tip, string clone = "clone-1", int minutes = 20) =>
        new(occasion, pass, working, new OpinionCandidate("Owner", baseCommit, tip, [tip]),
            new OpinionReviewer("codex-acp", Opinions.AnotherMaker) { Product = "Codex", Maker = "OpenAI", Account = "work" },
            Opinions.CopyAlone, minutes, Tree(clone))
        {
            Rechecks = rechecks, Families = ["claude-code"], HarnessVersion = "1.12.0",
        };

    private static OpinionFinding Finding(string weight = Opinions.Must, string where = "src/Report.cs:42", string claim = "The workspace name is not quoted.") =>
        new(weight, where, claim, "A name with a space breaks the command.", "Ran it with `my space`: it split in two.", "likely")
        {
            Proposal = "Quote it.",
        };

    /// <summary>A pass asked, its reviewer working, as the driver leaves it once the process is up.</summary>
    private async Task<(Opinion Opinion, Session Reviewer)> AskedAsync(OpinionAsk ask)
    {
        var asked = await _desk.AskAsync(ask, Now);
        Assert.True(asked.Refusal == OpinionRefusal.None, asked.Message);
        await _sessions.SetStateAsync(asked.Session!.Id, SessionState.Working, null, null, null, Now);
        return (asked.Opinion!, asked.Session!);
    }

    /// <summary>A first pass given with <paramref name="findings"/> and handed to its working session, which ended.</summary>
    private async Task<Opinion> HandedAsync(Session working, params OpinionFinding[] findings)
    {
        var (opinion, reviewer) = await AskedAsync(Ask(working.Id, clone: "clone-handed-" + working.Id));
        Assert.Equal(OpinionRefusal.None, (await _desk.GiveAsync(reviewer.Id, findings, "src/, the two commits", null, [], Now.AddMinutes(5))).Refusal);
        var handed = await _desk.HandAsync(opinion.Id, Now.AddMinutes(6));
        Assert.True(handed.Refusal == OpinionRefusal.None, handed.Message);
        return handed.Opinion!;
    }

    /// <summary>
    /// A pass is kept with everything it asked, and its reviewer's record opens with it: a chat that serves no quest, names
    /// the opinion, runs in its own clone at the candidate's tip and as the reviewer's account. It reads as `reading`.
    /// </summary>
    [Fact]
    public async Task A_pass_is_kept_with_its_reviewers_record_a_chat_that_serves_no_quest()
    {
        var working = await WorkingAsync();

        var asked = await _desk.AskAsync(Ask(working.Id), Now);

        Assert.True(asked.Refusal == OpinionRefusal.None, asked.Message);
        var opinion = (await _opinions.FindAsync(asked.Opinion!.Id))!;
        Assert.Equal((Opinions.Landing, Opinions.First, working.Id, asked.Session!.Id), (opinion.Occasion, opinion.Pass, opinion.Working, opinion.Session));
        Assert.Equal(("Owner", Base, Tip), (opinion.Candidate.Repository, opinion.Candidate.Base, opinion.Candidate.Tip));
        Assert.Equal([Tip], opinion.Candidate.Commits);
        Assert.Equal(("codex-acp", Opinions.AnotherMaker, "Codex", "OpenAI", "work"),
            (opinion.Reviewer.Adapter, opinion.Reviewer.Label, opinion.Reviewer.Product, opinion.Reviewer.Maker, opinion.Reviewer.Account));
        Assert.Equal(["claude-code"], opinion.Families);
        Assert.Equal((Opinions.CopyAlone, 20, Opinions.Agent, Now), (opinion.Posture, opinion.Minutes, opinion.Tier, opinion.Asked));
        Assert.Null(opinion.Given);

        var reviewer = (await _sessions.FindAsync(asked.Session.Id))!;
        Assert.Equal((SessionKind.Chat, null, "Owner", "codex-acp", opinion.Id), (reviewer.Kind, reviewer.Quest, reviewer.Repository, reviewer.Adapter, reviewer.Opinion));
        Assert.Equal((Tree("clone-1"), Tip, "work", "1.12.0"), (reviewer.Tree, reviewer.BaseCommit, reviewer.Profile, reviewer.HarnessVersion));
        Assert.Equal(working.Workspace, reviewer.Workspace);
        Assert.Contains("Codex", asked.Message);

        var standing = (await _desk.ReadAsync(opinion.Id, Now.AddMinutes(1)))!;
        Assert.Equal((Opinions.Reading, null), (standing.State, standing.Why));
    }

    /// <summary>
    /// What a pass asks is judged before anything is kept: each part's shape, a working session this machine holds in the
    /// candidate's repository that is no reviewer's and no intake's, and a tree of the reviewer's own, never the working
    /// session's. A refusal keeps no opinion and opens no record.
    /// </summary>
    [Fact]
    public async Task What_a_pass_asks_is_judged_before_anything_is_kept()
    {
        var working = await WorkingAsync();
        var elsewhere = await _sessions.CreateAsync("q2", "Other", "claude-code", Now, tree: Tree("other"));
        await _sessions.MirrorAsync(new Session("alice-laptop/cd34ef56", "q1", "Owner", "claude-code", SessionState.Completed, null, null, null, Now, Now));
        var (_, reviewer) = await AskedAsync(Ask(working.Id, Opinions.Asked, clone: "clone-0"));
        var before = (await _sessions.ListAsync(includeClosed: true)).Count;

        foreach (var (unfit, refusal, says) in new (OpinionAsk Ask, OpinionRefusal Refusal, string Says)[]
        {
            (Ask(working.Id, occasion: "whenever"), OpinionRefusal.BadShape, "`occasion`"),
            (Ask(working.Id, pass: "third"), OpinionRefusal.BadShape, "`pass`"),
            (Ask(working.Id, pass: Opinions.Recheck), OpinionRefusal.BadShape, "rechecks"),
            (Ask(working.Id) with { Rechecks = "abcd1234" }, OpinionRefusal.BadShape, "rechecks nothing"),
            (Ask(working.Id, minutes: 4), OpinionRefusal.BadShape, "from 5 to 120"),
            (Ask(working.Id, minutes: 121), OpinionRefusal.BadShape, "from 5 to 120"),
            (Ask(working.Id, tip: "abc1234"), OpinionRefusal.BadShape, "full ids"),
            (Ask(working.Id, tip: Base), OpinionRefusal.BadShape, "no work between"),
            (Ask(working.Id) with { Posture = "trusting" }, OpinionRefusal.BadShape, "`posture`"),
            (Ask(working.Id) with { Reviewer = new OpinionReviewer("codex-acp", "independent") }, OpinionRefusal.BadShape, "`label`"),
            (Ask(working.Id) with { Tree = " " }, OpinionRefusal.BadShape, "own tree"),
            (Ask("nobody12"), OpinionRefusal.NotFound, "no session `nobody12`"),
            (Ask("alice-laptop/cd34ef56"), OpinionRefusal.NotFound, "of this machine's"),
            (Ask(elsewhere.Id), OpinionRefusal.BadShape, "`Other`"),
            (Ask(reviewer.Id), OpinionRefusal.BadShape, "reads another session's work"),
            (Ask(working.Id, clone: "work"), OpinionRefusal.BadShape, "never the working session's"),
            (Ask(working.Id, clone: "clone-0"), OpinionRefusal.Busy, "already has an active session"),
        })
        {
            var refused = await _desk.AskAsync(unfit, Now);

            Assert.True(refused.Refusal == refusal, $"{refused.Refusal}: {refused.Message}");
            Assert.Contains(says, refused.Message);
            Assert.EndsWith("Nothing was kept.", refused.Message);
        }

        Assert.Single(await _opinions.ListAsync());
        Assert.Equal(before, (await _sessions.ListAsync(includeClosed: true)).Count);
    }

    /// <summary>
    /// The bound (design §8.3): the rule starts one first pass per working session, `landing` or `steps`, and a second is
    /// refused naming the first. The person's own asks, and a failure's help, which only their press starts, are not capped.
    /// </summary>
    [Fact]
    public async Task The_rule_starts_one_pass_per_working_session_and_the_persons_asks_are_not_capped()
    {
        var working = await WorkingAsync();
        var (first, _) = await AskedAsync(Ask(working.Id));

        var again = await _desk.AskAsync(Ask(working.Id, Opinions.Steps, clone: "clone-2"), Now);

        Assert.Equal(OpinionRefusal.Bound, again.Refusal);
        Assert.Contains($"`{first.Id}`", again.Message);
        Assert.Equal(OpinionRefusal.None, (await _desk.AskAsync(Ask(working.Id, Opinions.Asked, clone: "clone-3"), Now)).Refusal);
        Assert.Equal(OpinionRefusal.None, (await _desk.AskAsync(Ask(working.Id, Opinions.Failure, clone: "clone-4"), Now)).Refusal);
        var another = await WorkingAsync("work-2");
        Assert.Equal(OpinionRefusal.None, (await _desk.AskAsync(Ask(another.Id, clone: "clone-5"), Now)).Refusal);
    }

    /// <summary>
    /// One recheck (design §6.5, §8.3): it reads the commits since a first pass whose findings were handed, from that pass's
    /// tip, once. Before the findings went, from another base, of a recheck, or a second time, it is refused.
    /// </summary>
    [Fact]
    public async Task A_recheck_reads_the_commits_since_a_handed_pass_once()
    {
        var working = await WorkingAsync();
        var (unhanded, unhandedReviewer) = await AskedAsync(Ask(working.Id, Opinions.Asked, clone: "clone-0"));
        await _desk.GiveAsync(unhandedReviewer.Id, [Finding()], "src/", null, [], Now.AddMinutes(1));
        Assert.Equal(OpinionRefusal.Bound, (await _desk.AskAsync(Ask(working.Id, pass: Opinions.Recheck, rechecks: unhanded.Id, baseCommit: Tip, tip: Later, clone: "r0"), Now)).Refusal);

        var first = await HandedAsync(working, Finding());
        var fromElsewhere = await _desk.AskAsync(Ask(working.Id, pass: Opinions.Recheck, rechecks: first.Id, baseCommit: Base, tip: Later, clone: "r1"), Now);
        Assert.Equal(OpinionRefusal.Bound, fromElsewhere.Refusal);
        Assert.Contains("since", fromElsewhere.Message);

        var (recheck, _) = await AskedAsync(Ask(working.Id, pass: Opinions.Recheck, rechecks: first.Id, baseCommit: Tip, tip: Later, clone: "r2"));
        Assert.Equal((Opinions.Recheck, first.Id), (recheck.Pass, recheck.Rechecks));

        var twice = await _desk.AskAsync(Ask(working.Id, pass: Opinions.Recheck, rechecks: first.Id, baseCommit: Tip, tip: Later, clone: "r3"), Now);
        Assert.Equal(OpinionRefusal.Bound, twice.Refusal);
        Assert.Contains($"`{recheck.Id}`", twice.Message);
        Assert.Equal(OpinionRefusal.Bound, (await _desk.AskAsync(Ask(working.Id, pass: Opinions.Recheck, rechecks: recheck.Id, baseCommit: Later, tip: Base, clone: "r4"), Now)).Refusal);
        Assert.Equal(OpinionRefusal.NotFound, (await _desk.AskAsync(Ask(working.Id, pass: Opinions.Recheck, rechecks: "nothing1", baseCommit: Tip, tip: Later, clone: "r5"), Now)).Refusal);
    }

    /// <summary>
    /// The reviewer says its opinion once (design §6.1): numbered findings, what it read and its limits. Said again, after its
    /// minutes, from a record that ended, or from a session that is no reviewer, it is refused; and a part out of shape keeps
    /// nothing. A pass with no findings still says what it read.
    /// </summary>
    [Fact]
    public async Task The_reviewer_says_its_opinion_once_within_its_minutes()
    {
        var working = await WorkingAsync();
        var (opinion, reviewer) = await AskedAsync(Ask(working.Id));

        foreach (var (findings, read, says) in new (OpinionFinding[] Findings, string? Read, string Says)[]
        {
            ([Finding()], " ", "`read`"),
            ([Finding(weight: "blocker")], "src/", "`weight`"),
            ([Finding(where: "/etc/passwd")], "src/", "not a place in the repository"),
            ([Finding(where: "src/a.cs:9-3")], "src/", "ends before it starts"),
            ([Finding(claim: new string('x', 301))], "src/", "`claim` is at most 300"),
            ([Finding() with { Sure = "certain" }], "src/", "`sure`"),
            ([Finding() with { Reproduce = "" }], "src/", "`reproduce`"),
            (Enumerable.Repeat(Finding(), 21).ToArray(), "src/", "at most 20"),
        })
        {
            var refused = await _desk.GiveAsync(reviewer.Id, findings, read, null, [], Now.AddMinutes(1));
            Assert.Equal(OpinionRefusal.BadShape, refused.Refusal);
            Assert.Contains(says, refused.Message);
        }

        Assert.Equal(OpinionRefusal.BadShape, (await _desk.GiveAsync(reviewer.Id, [Finding()], "src/", null, [new OpinionRecheck(1, Opinions.Withdrawn)], Now)).Refusal);
        Assert.Equal(OpinionRefusal.NotAReviewer, (await _desk.GiveAsync(working.Id, [Finding()], "src/", null, [], Now)).Refusal);
        Assert.Equal(OpinionRefusal.NotFound, (await _desk.GiveAsync(null, [Finding()], "src/", null, [], Now)).Refusal);
        Assert.Null((await _opinions.FindAsync(opinion.Id))!.Given);

        var given = await _desk.GiveAsync(
            reviewer.Id, [Finding(), Finding(Opinions.Weights[1], "general", "The table is kept twice."), Finding("note", "a1b2c3d", "A word.")],
            "src/Report.cs and both commits", "I did not run the gate.", [], Now.AddMinutes(5));

        Assert.True(given.Refusal == OpinionRefusal.None, given.Message);
        var kept = (await _opinions.FindAsync(opinion.Id))!.Given!;
        Assert.Equal([1, 2, 3], kept.Findings.Select(finding => finding.Number));
        Assert.Equal(("src/Report.cs and both commits", "I did not run the gate.", Now.AddMinutes(5)), (kept.Read, kept.Limits, kept.At));
        Assert.Equal("Quote it.", kept.Findings[0].Proposal);
        Assert.Contains("3 findings", given.Message);
        Assert.Equal(Opinions.Given, (await _desk.ReadAsync(opinion.Id, Now.AddMinutes(6)))!.State);

        var twice = await _desk.GiveAsync(reviewer.Id, [], "nothing more", null, [], Now.AddMinutes(6));
        Assert.Equal(OpinionRefusal.AlreadyGiven, twice.Refusal);
        Assert.Equal(3, (await _opinions.FindAsync(opinion.Id))!.Given!.Findings.Count);

        var late = await AskedAsync(Ask(working.Id, Opinions.Asked, clone: "clone-late", minutes: 5));
        var outOfTime = await _desk.GiveAsync(late.Reviewer.Id, [], "src/", null, [], Now.AddMinutes(5).AddSeconds(1));
        Assert.Equal(OpinionRefusal.OutOfTime, outOfTime.Refusal);
        Assert.Contains("5 minutes", outOfTime.Message);

        var ended = await AskedAsync(Ask(working.Id, Opinions.Asked, clone: "clone-ended"));
        await _sessions.SetStateAsync(ended.Reviewer.Id, SessionState.Completed, null, null, null, Now);
        Assert.Equal(OpinionRefusal.Ended, (await _desk.GiveAsync(ended.Reviewer.Id, [], "src/", null, [], Now)).Refusal);

        var nothing = await AskedAsync(Ask(working.Id, Opinions.Asked, clone: "clone-nothing"));
        var raised = await _desk.GiveAsync(nothing.Reviewer.Id, [], "src/ and both commits", null, [], Now.AddMinutes(2));
        Assert.Equal(OpinionRefusal.None, raised.Refusal);
        Assert.Contains("raised nothing in what it read", raised.Message);
        Assert.DoesNotContain("no issues", raised.Message);
    }

    /// <summary>
    /// A pass that ends without an opinion is `failed`, never empty (design §6.1): `ended` where its session ended first,
    /// `out-of-time` where its minutes ran out, still running or stopped after.
    /// </summary>
    [Fact]
    public async Task A_pass_that_ends_without_an_opinion_reads_as_failed()
    {
        var working = await WorkingAsync();
        var (early, earlyReviewer) = await AskedAsync(Ask(working.Id, Opinions.Asked, clone: "clone-early"));
        await _sessions.SetStateAsync(earlyReviewer.Id, SessionState.Failed, "exit 1", null, null, Now.AddMinutes(3));
        var (slow, _) = await AskedAsync(Ask(working.Id, Opinions.Asked, clone: "clone-slow"));
        var (stopped, stoppedReviewer) = await AskedAsync(Ask(working.Id, Opinions.Asked, clone: "clone-stopped"));
        await _sessions.SetStateAsync(stoppedReviewer.Id, SessionState.Stopped, null, null, null, Now.AddMinutes(21));

        Assert.Equal((Opinions.Failed, Opinions.Ended), Read(await _desk.ReadAsync(early.Id, Now.AddMinutes(30))));
        Assert.Equal((Opinions.Reading, null), Read(await _desk.ReadAsync(slow.Id, Now.AddMinutes(19))));
        Assert.Equal((Opinions.Failed, Opinions.OutOfTime), Read(await _desk.ReadAsync(slow.Id, Now.AddMinutes(21))));
        Assert.Equal((Opinions.Failed, Opinions.OutOfTime), Read(await _desk.ReadAsync(stopped.Id, Now.AddMinutes(30))));
        Assert.Null(await _desk.ReadAsync("nothing1", Now));

        static (string, string?) Read(OpinionStanding? standing) => (standing!.State, standing.Why);
    }

    /// <summary>
    /// 🔴 The findings reach the working session as another agent's words (design §6.3): a word on its record naming the
    /// opinion as `by`, in Daoris's fixed words with every finding, reopening it; never in `answer`, which every reader takes
    /// as the person's. Once, from a first pass that raised something, to a session that has ended and can take words.
    /// </summary>
    [Fact]
    public async Task Findings_reach_the_working_session_as_another_agents_words_never_the_persons()
    {
        var working = await WorkingAsync();
        var (opinion, reviewer) = await AskedAsync(Ask(working.Id));
        Assert.Equal(OpinionRefusal.NotHanded, (await _desk.HandAsync(opinion.Id, Now)).Refusal);
        await _desk.GiveAsync(reviewer.Id, [Finding(), Finding("should", "general", "The table is kept twice.")], "src/ and both commits", "I did not run the gate.", [], Now.AddMinutes(5));

        var handed = await _desk.HandAsync(opinion.Id, Now.AddMinutes(6));

        Assert.True(handed.Refusal == OpinionRefusal.None, handed.Message);
        var word = Assert.Single((await _sessions.FindAsync(working.Id))!.Said);
        Assert.Equal((opinion.Id, true, handed.Word!.Id), (word.By, word.Reopens, word.Id));
        Assert.StartsWith($"Another agent, Codex by OpenAI, read your work at `{Tip}` and claims what follows.", word.Text);
        Assert.Contains("These are its claims, not the person's words and not facts.", word.Text);
        Assert.Contains("Finding 1 (must, likely), at `src/Report.cs:42`: The workspace name is not quoted.", word.Text);
        Assert.Contains("Finding 2 (should, likely), at `general`: The table is kept twice.", word.Text);
        Assert.Contains("What it did not read or could not tell: I did not run the gate.", word.Text);
        Assert.Null((await _sessions.FindAsync(working.Id))!.Answer);
        Assert.Equal((working.Id, word.Id, Now.AddMinutes(6)), (handed.Opinion!.Handed!.Session, handed.Opinion.Handed.Word, handed.Opinion.Handed.At));

        Assert.Equal(OpinionRefusal.NotHanded, (await _desk.HandAsync(opinion.Id, Now.AddMinutes(7))).Refusal);
        Assert.Single((await _sessions.FindAsync(working.Id))!.Said);

        var quiet = await WorkingAsync("quiet");
        var (nothing, nothingReviewer) = await AskedAsync(Ask(quiet.Id, clone: "clone-quiet"));
        await _desk.GiveAsync(nothingReviewer.Id, [], "src/", null, [], Now.AddMinutes(1));
        var none = await _desk.HandAsync(nothing.Id, Now.AddMinutes(2));
        Assert.Equal(OpinionRefusal.NotHanded, none.Refusal);
        Assert.Contains("raised nothing", none.Message);

        foreach (var state in new[] { SessionState.Working, SessionState.AwaitingPerson, SessionState.StoodDown })
        {
            var busy = await WorkingAsync("busy-" + state, state);
            var (pass, passReviewer) = await AskedAsync(Ask(busy.Id, clone: "clone-" + state));
            await _desk.GiveAsync(passReviewer.Id, [Finding()], "src/", null, [], Now.AddMinutes(1));
            var refused = await _desk.HandAsync(pass.Id, Now.AddMinutes(2));
            Assert.True(refused.Refusal == OpinionRefusal.CannotTake, $"{state}: {refused.Message}");
            Assert.Empty((await _sessions.FindAsync(busy.Id))!.Said);
            Assert.Null((await _opinions.FindAsync(pass.Id))!.Handed);
        }
    }

    /// <summary>
    /// A recheck's findings go to the person, never back to the working session by themselves (design §6.5): it is never
    /// handed. What it says of each first-pass finding is kept, and a number the first pass did not give is refused.
    /// </summary>
    [Fact]
    public async Task A_recheck_says_whether_each_finding_stands_and_is_never_handed()
    {
        var working = await WorkingAsync();
        var first = await HandedAsync(working, Finding(), Finding("should", "general", "The table is kept twice."));
        var (recheck, reviewer) = await AskedAsync(Ask(working.Id, pass: Opinions.Recheck, rechecks: first.Id, baseCommit: Tip, tip: Later, clone: "recheck"));

        Assert.Equal(OpinionRefusal.BadShape, (await _desk.GiveAsync(reviewer.Id, [], "the commit since", null, [new OpinionRecheck(3, Opinions.Withdrawn)], Now)).Refusal);
        Assert.Equal(OpinionRefusal.BadShape, (await _desk.GiveAsync(reviewer.Id, [], "the commit since", null, [new OpinionRecheck(1, "gone")], Now)).Refusal);
        Assert.Equal(OpinionRefusal.BadShape, (await _desk.GiveAsync(reviewer.Id, [], "the commit since", null, [new OpinionRecheck(1, Opinions.Withdrawn), new OpinionRecheck(1, Opinions.Stands)], Now)).Refusal);

        var given = await _desk.GiveAsync(reviewer.Id, [Finding("note", "general", "A new word.")], "the commit since", null, [new OpinionRecheck(1, Opinions.Withdrawn)], Now.AddMinutes(2));

        Assert.True(given.Refusal == OpinionRefusal.None, given.Message);
        Assert.Equal([new OpinionRecheck(1, Opinions.Withdrawn)], (await _opinions.FindAsync(recheck.Id))!.Given!.Rechecked);
        var refused = await _desk.HandAsync(recheck.Id, Now.AddMinutes(3));
        Assert.Equal(OpinionRefusal.NotHanded, refused.Refusal);
        Assert.Contains("go to the person", refused.Message);
    }

    /// <summary>
    /// The working session answers each finding (design §6.4): `fixed` with a commit, `rejected` with evidence, `unresolved`
    /// with why; a later answer to a finding stands over the earlier. A session handed nothing, a finding the pass did not
    /// give, and a part out of shape are refused, and once the recheck read the answers, none is taken.
    /// </summary>
    [Fact]
    public async Task The_working_session_answers_each_finding_until_the_recheck_reads_them()
    {
        var working = await WorkingAsync();
        var opinion = await HandedAsync(working, Finding(), Finding("should", "general", "The table is kept twice."), Finding("note", "general", "A word."));
        var other = await WorkingAsync("other");

        Assert.Equal(OpinionRefusal.NotHanded, (await _desk.AnswerAsync(other.Id, null, [new OpinionAnswer(1, Opinions.Unresolved, Now) { Why = "x" }], Now)).Refusal);
        Assert.Equal(OpinionRefusal.NotFound, (await _desk.AnswerAsync(null, null, [new OpinionAnswer(1, Opinions.Unresolved, Now) { Why = "x" }], Now)).Refusal);
        foreach (var (unfit, says) in new (OpinionAnswer Answer, string Says)[]
        {
            (new OpinionAnswer(4, Opinions.Fixed, Now) { Commit = "a1b2c3d" }, "finding 4"),
            (new OpinionAnswer(1, Opinions.Fixed, Now), "commit"),
            (new OpinionAnswer(1, Opinions.Fixed, Now) { Commit = "not-a-commit" }, "commit"),
            (new OpinionAnswer(1, Opinions.Rejected, Now) { Evidence = new string('x', 601) }, "at most 600"),
            (new OpinionAnswer(1, Opinions.Unresolved, Now), "why"),
            (new OpinionAnswer(1, Opinions.Fixed, Now) { Commit = "a1b2c3d", Why = "both" }, "only its own part"),
            (new OpinionAnswer(1, "agreed", Now), "`fixed`, `rejected` or `unresolved`"),
        })
        {
            var refused = await _desk.AnswerAsync(working.Id, null, [unfit], Now);
            Assert.True(refused.Refusal == OpinionRefusal.BadShape, $"{refused.Refusal}: {refused.Message}");
            Assert.Contains(says, refused.Message);
        }

        Assert.Equal(OpinionRefusal.BadShape, (await _desk.AnswerAsync(working.Id, null,
            [new OpinionAnswer(1, Opinions.Unresolved, Now) { Why = "x" }, new OpinionAnswer(1, Opinions.Unresolved, Now) { Why = "y" }], Now)).Refusal);
        Assert.Empty((await _opinions.FindAsync(opinion.Id))!.Answers);

        var answered = await _desk.AnswerAsync(working.Id, null,
            [new OpinionAnswer(1, Opinions.Unresolved, Now) { Why = "Cannot reproduce yet." }, new OpinionAnswer(2, Opinions.Rejected, Now) { Evidence = "The twin test holds the table." }],
            Now.AddMinutes(10));
        Assert.True(answered.Refusal == OpinionRefusal.None, answered.Message);
        Assert.Contains("3", answered.Message);

        var fixedLater = await _desk.AnswerAsync(working.Id, opinion.Id, [new OpinionAnswer(1, Opinions.Fixed, Now) { Commit = "A1B2C3D" }], Now.AddMinutes(12));
        Assert.Equal(OpinionRefusal.None, fixedLater.Refusal);
        var kept = (await _opinions.FindAsync(opinion.Id))!;
        Assert.Equal((Opinions.Fixed, "a1b2c3d", Now.AddMinutes(12)), (Opinions.AnswerTo(kept, 1)!.Said, Opinions.AnswerTo(kept, 1)!.Commit, Opinions.AnswerTo(kept, 1)!.At));
        Assert.Equal(Opinions.Rejected, Opinions.AnswerTo(kept, 2)!.Said);
        Assert.Null(Opinions.AnswerTo(kept, 3));

        await AskedAsync(Ask(working.Id, pass: Opinions.Recheck, rechecks: opinion.Id, baseCommit: Tip, tip: Later, clone: "recheck"));
        var closed = await _desk.AnswerAsync(working.Id, opinion.Id, [new OpinionAnswer(3, Opinions.Unresolved, Now) { Why = "x" }], Now.AddMinutes(20));
        Assert.Equal(OpinionRefusal.Closed, closed.Refusal);
        Assert.Null(Opinions.AnswerTo((await _opinions.FindAsync(opinion.Id))!, 3));
    }

    /// <summary>
    /// D155 point 7: the say door refuses words to a reviewer's record with its own word, `opinion`: a pass takes one turn
    /// and no words, and the person asks again instead.
    /// </summary>
    [Fact]
    public async Task The_say_door_refuses_words_to_a_reviewers_record()
    {
        var working = await WorkingAsync();
        var (opinion, reviewer) = await AskedAsync(Ask(working.Id));
        await _sessions.SetStateAsync(reviewer.Id, SessionState.Completed, null, null, null, Now);
        var ledger = new SessionLedger(await QuestStore.OpenAsync(_connection), _sessions);

        var refused = await ledger.SayAsync(reviewer.Id, "Look at the tests too.", null, Now);

        Assert.Equal(SessionSayRefusal.Opinion, refused.Refusal);
        Assert.Equal(opinion.Id, refused.Opinion);
        Assert.Contains("one turn", refused.Message);
        Assert.Empty((await _sessions.FindAsync(reviewer.Id))!.Said);
    }

    /// <summary>
    /// 🔴 A shared host keeps no opinion (design §6.2, D47 §4): every door refuses with one sentence, and nothing is kept or
    /// opened.
    /// </summary>
    [Fact]
    public async Task A_shared_host_keeps_no_opinion()
    {
        var shared = new OpinionDesk(_opinions, _sessions, local: false);
        var working = await WorkingAsync();
        var before = (await _sessions.ListAsync(includeClosed: true)).Count;

        foreach (var refused in new[]
        {
            await shared.AskAsync(Ask(working.Id), Now),
            await shared.GiveAsync(working.Id, [Finding()], "src/", null, [], Now),
            await shared.HandAsync("abcd1234", Now),
            await shared.AnswerAsync(working.Id, null, [new OpinionAnswer(1, Opinions.Unresolved, Now) { Why = "x" }], Now),
        })
        {
            Assert.Equal(OpinionRefusal.Shared, refused.Refusal);
            Assert.Equal(Opinions.SharedSentence, refused.Message);
        }

        Assert.Empty(await _opinions.ListAsync());
        Assert.Equal(before, (await _sessions.ListAsync(includeClosed: true)).Count);
    }

    /// <summary>
    /// An opinion reads back whole, and a part this build cannot read is passed over, never a failed read: a field a later
    /// build added, a finding missing its claim, an answer of a kind it does not know.
    /// </summary>
    [Fact]
    public async Task An_opinion_reads_back_whole_and_a_part_this_build_cannot_read_is_passed_over()
    {
        var working = await WorkingAsync();
        var opinion = await HandedAsync(working, Finding(), Finding("should", "general", "The table is kept twice."));
        await _desk.AnswerAsync(working.Id, null, [new OpinionAnswer(2, Opinions.Rejected, Now) { Evidence = "The twin test." }], Now.AddMinutes(8));
        await using (var plant = _connection.CreateCommand())
        {
            plant.CommandText = """
                UPDATE opinions SET
                  given = json_set(json_insert(given, '$.findings[#]', json('{"number":3,"weight":"note"}')), '$.tone', 'calm'),
                  answers = json_insert(answers, '$[#]', json('{"finding":1,"said":"agreed","at":"2026-10-09T10:09:00Z"}')),
                  asking = json_set(asking, '$.colour', 'blue')
                WHERE id = $id
                """;
            plant.Parameters.AddWithValue("$id", opinion.Id);
            await plant.ExecuteNonQueryAsync();
        }

        var read = (await _opinions.FindAsync(opinion.Id))!;

        Assert.Equal([1, 2], read.Given!.Findings.Select(finding => finding.Number));
        Assert.Equal((2, Opinions.Rejected), (Assert.Single(read.Answers).Finding, read.Answers[0].Said));
        Assert.Equal(("codex-acp", Tip), (read.Reviewer.Adapter, read.Candidate.Tip));
        Assert.Equal(working.Id, read.Handed!.Session);
        Assert.Equal([opinion.Id], (await _desk.ListAsync(working.Id, null, null, Now)).Where(each => each.Opinion.Pass == Opinions.First).Select(each => each.Opinion.Id));
    }
}

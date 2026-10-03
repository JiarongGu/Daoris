using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LAND2b (D145 point 2, design §2, §5, §8): the due list's pure rules and its file. A driven session in a tree of its own
/// whose record concludes on a done quest, under a rule that accepts automatically, is due; a refusal is tried again only
/// when its tree's tip or status moves; each try keeps its code, and the facts it was made at. Files and tables only, so the
/// suite's fast half; the landing itself over real git is <see cref="AutoLandingTests"/>.
/// </summary>
public sealed class AutoLandingRulesTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-auto-landing-" + Guid.NewGuid().ToString("N")[..8]);

    public AutoLandingRulesTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static readonly DateTimeOffset At = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    private static readonly LandingRule Automatic = new(LandingForm.Branch, "feature/{quest}-{slug}", Tidy: true, AutoAccept: true);

    private static AutoLanding Due(string session = "s1", string quest = "q1") =>
        new(session, quest, "engine", "aurora", $"trees/aurora/engine/{session}", At);

    // ——— when a concluded record is due

    /// <summary>
    /// Only a rule that accepts automatically makes anything due, and then only a done in a tree of its own; a done in the
    /// repository's own checkout, and an end that is not a done, are each said, and a session still waiting is not an end.
    /// </summary>
    [Theory]
    [InlineData(true, "Done", true, "completed", AutoConcluded.Due)]
    [InlineData(true, "Done", false, "completed", AutoConcluded.NoTree)]
    [InlineData(true, "Done", true, "failed", AutoConcluded.Due)]
    [InlineData(true, "Open", true, "declined", AutoConcluded.NotDone)]
    [InlineData(true, "Taken", true, "failed", AutoConcluded.NotDone)]
    [InlineData(true, "Taken", true, "stood-down", AutoConcluded.NotDone)]
    [InlineData(true, "Taken", true, "stopped", AutoConcluded.NotDone)]
    [InlineData(true, "Taken", true, "completed", null)]
    [InlineData(true, "Taken", true, "awaiting-person", null)]
    [InlineData(false, "Done", true, "completed", null)]
    public void A_concluded_record_is_due_only_on_a_done_in_its_own_tree_under_the_switch(
        bool autoAccept, string status, bool ownTree, string state, string? expected)
    {
        var rule = Automatic with { AutoAccept = autoAccept };

        Assert.Equal(expected, AutoLandingRules.Concluded(rule, status, ownTree, state));
    }

    /// <summary>
    /// The conclusion acts on its verdict: due, the session joins the due list keyed on the tree's layout; a done in the
    /// repository's own checkout, and an end that is not a done, are said in its conversation by code; under a rule with the
    /// switch off nothing is written anywhere.
    /// </summary>
    [Fact]
    public void A_conclusion_under_the_switch_is_due_or_said_and_without_it_writes_nothing()
    {
        var events = new SessionEvents(Path.Combine(_home, "sessions"));
        var quest = new QuestView("q1", "game", "engine", "Fix the gap", "Fix it.", "Done") { Workspace = "aurora" };
        var tree = Path.Combine(_home, "trees", "aurora", "engine", "s-1a2b3c4d");
        var config = DriverConfig.Empty.WithWorkspaceLanding("aurora", Automatic);

        Assert.Equal(AutoConcluded.Due, AutoLander.Concluded(_home, config, events, "s1", quest, "Done", "completed", tree, "aurora", At));
        var due = Assert.Single(new AutoLandings(_home).Open());
        Assert.Equal(("s1", "q1", "engine", "aurora", tree, At), (due.Session, due.Quest, due.Repository, due.Workspace, due.Tree, due.DueAt));

        var checkout = Path.Combine(_home, "checkouts", "engine");
        Assert.Equal(AutoConcluded.NoTree, AutoLander.Concluded(_home, config, events, "s2", quest, "Done", "completed", checkout, "aurora"));
        Assert.Equal(NoteCodes.LandingNoTree.Code, events.After("s2", 0).Events.Single().Parts![0].Code);
        Assert.Equal(AutoConcluded.NotDone,
            AutoLander.Concluded(_home, config, events, "s3", quest with { Status = "Open" }, "Open", "declined", tree, "aurora"));
        Assert.Equal(NoteCodes.LandingNotDone.Code, events.After("s3", 0).Events.Single().Parts![0].Code);
        Assert.Single(new AutoLandings(_home).All());

        Assert.Null(AutoLander.Concluded(_home, DriverConfig.Empty, events, "s4", quest, "Done", "completed", tree, "aurora"));
        Assert.False(File.Exists(Path.Combine(_home, "sessions", "s4.events.jsonl")));
        Assert.Single(new AutoLandings(_home).All());
    }

    /// <summary>
    /// 🔴 A landing at done reads its rule again where it lands, not only where the look chose it: one changed meanwhile to a
    /// merge, or with the switch off, lands nothing, before git is asked anything, and is kept as <c>off</c> (D51 rule 6).
    /// </summary>
    [Theory]
    [InlineData("merge")]
    [InlineData("branch")]
    public async Task A_landing_at_done_under_a_rule_changed_meanwhile_lands_nothing(string form)
    {
        var rule = form == "merge" ? LandingRule.Merge : new LandingRule(LandingForm.Branch, "feature/{quest}");
        DriverConfig.Empty.WithWorkspaceLanding("aurora", rule).Save(Path.Combine(_home, "driver.json"));
        var tree = Path.Combine(_home, "trees", "aurora", "engine", "s-1a2b3c4d");

        var landed = await new SessionTrees(_home).LandAsync(tree, new LandingSubject("s1", "q1", "Fix"), acceptedBy: AcceptedBy.Auto);

        Assert.False(landed.Landed);
        Assert.Equal(AutoLandingCode.Off, AutoLandingRules.CodeOf(landed));
        Assert.Contains("no longer accepts automatically", landed.Message);
    }

    /// <summary>A merge never accepts automatically, whatever a hand-written file says (D145 point 1).</summary>
    [Fact]
    public void A_merge_rule_makes_nothing_due() =>
        Assert.Null(AutoLandingRules.Concluded(new LandingRule(LandingForm.Merge, AutoAccept: true), "Done", ownTree: true, "completed"));

    // ——— when a due session is tried again

    /// <summary>
    /// A refusal is tried again only when the tree's tip or status changed since it (design §2); a hold is read again at every
    /// look, since its release is the quest's and not the tree's; a first try always runs.
    /// </summary>
    [Theory]
    [InlineData(null, "tip1", "clean", "tip1", "clean", true)]
    [InlineData(AutoLandingCode.Held, "tip1", "clean", "tip1", "clean", true)]
    [InlineData(AutoLandingCode.Uncommitted, "tip1", "sha256:aaaa", "tip1", "sha256:aaaa", false)]
    [InlineData(AutoLandingCode.Uncommitted, "tip1", "sha256:aaaa", "tip1", "sha256:bbbb", true)]
    [InlineData(AutoLandingCode.Uncommitted, "tip1", "sha256:aaaa", "tip2", "clean", true)]
    [InlineData(AutoLandingCode.Exists, "tip1", "clean", "tip1", "clean", false)]
    [InlineData(AutoLandingCode.Exists, "tip1", "clean", "tip2", "clean", true)]
    [InlineData(AutoLandingCode.Refused, "tip1", "clean", "tip1", "clean", false)]
    [InlineData(AutoLandingCode.Refused, "tip1", "clean", "tip1", "sha256:cccc", true)]
    public void A_refusal_is_tried_again_only_when_the_trees_tip_or_status_moves(
        string? last, string? triedTip, string triedStatus, string? tip, string status, bool expected)
    {
        var entry = Due();
        if (last is not null) entry = entry with { Tries = [new AutoTry(At, last) { Tip = triedTip, Status = triedStatus }] };

        Assert.Equal(expected, AutoLandingRules.ShouldTry(entry, tip, status));
    }

    /// <summary>The tree's status as a fact a try is kept with: clean, or a fingerprint of what git listed, never the paths.</summary>
    [Fact]
    public void A_trees_status_is_kept_as_clean_or_a_fingerprint_and_never_its_paths()
    {
        Assert.Equal("clean", AutoLandingRules.Fingerprint(""));
        Assert.Equal("clean", AutoLandingRules.Fingerprint("  \n"));
        var dirty = AutoLandingRules.Fingerprint(" M src/secret-plan.ts\n?? notes.txt\n");
        Assert.StartsWith("sha256:", dirty);
        Assert.DoesNotContain("secret", dirty);
        Assert.Equal(dirty, AutoLandingRules.Fingerprint(" M src/secret-plan.ts\r\n?? notes.txt"));
        Assert.NotEqual(dirty, AutoLandingRules.Fingerprint(" M src/secret-plan.ts\n"));
    }

    /// <summary>
    /// What a landing came to, by the code its try keeps (design §8): landed, a plugin that could not land here, a plugin that
    /// failed or did not push, and each refusal by the landing's own code.
    /// </summary>
    [Fact]
    public void A_landings_outcome_is_kept_by_its_code()
    {
        Assert.Equal(AutoLandingCode.Landed, AutoLandingRules.CodeOf(new TreeLanding(true, "put the work on `feature/x`.", "feature/x")));
        Assert.Equal(AutoLandingCode.Landed, AutoLandingRules.CodeOf(new TreeLanding(true, "", "feature/x",
            new PluginLanding("acme.lands", Pushed: true, "https://example.test/pull/7", "pushed"))));
        Assert.Equal(AutoLandingCode.PluginFailed, AutoLandingRules.CodeOf(new TreeLanding(true, "", "feature/x",
            new PluginLanding("acme.lands", Pushed: false, null, "gh is not signed in"))));
        Assert.Equal(AutoLandingCode.PluginFailed, AutoLandingRules.CodeOf(new TreeLanding(true, "", "feature/x",
            new PluginLanding("acme.lands", Pushed: false, null, "did not answer", Failed: true))));
        Assert.Equal(AutoLandingCode.PluginUnready,
            AutoLandingRules.CodeOf(new TreeLanding(true, "", "feature/x") { Unready = "plugin `acme.lands` is switched off" }));
        Assert.Equal(AutoLandingCode.Exists, AutoLandingRules.CodeOf(new TreeLanding(false, "already a branch") { Refusal = AutoLandingCode.Exists }));
        Assert.Equal(AutoLandingCode.Uncommitted, AutoLandingRules.CodeOf(new TreeLanding(false, "") { Refusal = AutoLandingCode.Uncommitted }));
        Assert.Equal(AutoLandingCode.Nothing, AutoLandingRules.CodeOf(new TreeLanding(false, "") { Refusal = AutoLandingCode.Nothing }));
        Assert.Equal(AutoLandingCode.Refused, AutoLandingRules.CodeOf(new TreeLanding(false, "git would not make it")));
    }

    /// <summary>Which codes end an entry, and which leave it waiting for a change, a release or the person's press.</summary>
    [Theory]
    [InlineData(AutoLandingCode.Landed, true)]
    [InlineData(AutoLandingCode.Nothing, true)]
    [InlineData(AutoLandingCode.PluginUnready, true)]
    [InlineData(AutoLandingCode.PluginFailed, true)]
    [InlineData(AutoLandingCode.Already, true)]
    [InlineData(AutoLandingCode.Superseded, true)]
    [InlineData(AutoLandingCode.Gone, true)]
    [InlineData(AutoLandingCode.Undone, true)]
    [InlineData(AutoLandingCode.Off, true)]
    [InlineData(AutoLandingCode.Held, false)]
    [InlineData(AutoLandingCode.Uncommitted, false)]
    [InlineData(AutoLandingCode.Exists, false)]
    [InlineData(AutoLandingCode.Refused, false)]
    public void A_code_ends_its_entry_or_leaves_it_waiting(string code, bool closes) =>
        Assert.Equal(closes, AutoLandingCode.Closes(code));

    // ——— the due list's file

    /// <summary>
    /// <c>&lt;home&gt;/sessions/auto-landings.json</c>, written whole beside and renamed, LF, BOM-less: when each became due, each try's
    /// tip, status and code, and whether it closed. Read back whole.
    /// </summary>
    [Fact]
    public void The_due_list_keeps_when_each_became_due_and_each_try_and_reads_back_whole()
    {
        var due = new AutoLandings(_home);
        due.Due(Due("s1"));
        due.Due(Due("s2", "q2"));

        due.Tried("s1", new AutoTry(At.AddMinutes(1), AutoLandingCode.Uncommitted) { Tip = "abc1234", Status = "sha256:aaaa", Uncommitted = 2 }, close: false);
        due.Tried("s2", new AutoTry(At.AddMinutes(2), AutoLandingCode.Landed) { Tip = "def5678", Status = "clean", Branch = "feature/q2-fix", Commits = 3 }, close: true);

        var path = Path.Combine(_home, "sessions", AutoLandings.FileName);
        Assert.Equal(path, due.FilePath);
        var text = File.ReadAllText(path);
        Assert.DoesNotContain("\r", text);
        Assert.False(File.ReadAllBytes(path).AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }));
        Assert.Contains("\"dueAt\"", text);
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(_home, "sessions"), "*.writing"));

        var again = new AutoLandings(_home);
        Assert.Equal(["s1"], again.Open().Select(entry => entry.Session));
        var first = again.Of("s1")!;
        Assert.Equal(("engine", "aurora", "q1", At), (first.Repository, first.Workspace, first.Quest, first.DueAt));
        var tried = Assert.Single(first.Tries);
        Assert.Equal((AutoLandingCode.Uncommitted, "abc1234", "sha256:aaaa", 2), (tried.Code, tried.Tip, tried.Status, tried.Uncommitted));
        var second = again.Of("s2")!;
        Assert.NotNull(second.Closed);
        Assert.Equal(("feature/q2-fix", 3), (second.Tries[0].Branch, second.Tries[0].Commits));
    }

    /// <summary>
    /// A session concluded again (words sent to it after its landing, D137) is due again, its earlier tries kept: what it
    /// came to before is a fact of its own.
    /// </summary>
    [Fact]
    public void A_session_due_again_reopens_with_its_earlier_tries_kept()
    {
        var due = new AutoLandings(_home);
        due.Due(Due("s1"));
        due.Tried("s1", new AutoTry(At, AutoLandingCode.Landed) { Branch = "feature/q1-fix" }, close: true);

        due.Due(Due("s1") with { DueAt = At.AddHours(1) });

        var entry = Assert.Single(due.Open());
        Assert.Equal(At.AddHours(1), entry.DueAt);
        Assert.Equal([AutoLandingCode.Landed], entry.Tries.Select(each => each.Code));
    }

    /// <summary>A file that does not read is no due list, never a failure; and a closed entry beyond the bound is dropped, an open one never.</summary>
    [Fact]
    public void A_file_that_does_not_read_is_none_and_closed_entries_are_bounded()
    {
        Directory.CreateDirectory(Path.Combine(_home, "sessions"));
        File.WriteAllText(Path.Combine(_home, "sessions", AutoLandings.FileName), "{ not json");
        Assert.Empty(new AutoLandings(_home).All());

        var due = new AutoLandings(_home);
        due.Due(Due("open"));
        for (var i = 0; i < AutoLandings.ClosedKept + 5; i++)
        {
            due.Due(Due($"s{i}"));
            due.Tried($"s{i}", new AutoTry(At.AddMinutes(i), AutoLandingCode.Nothing), close: true);
        }

        var all = new AutoLandings(_home).All();
        Assert.Equal(AutoLandings.ClosedKept, all.Count(entry => entry.Closed is not null));
        Assert.Contains(all, entry => entry.Session == "open" && entry.Closed is null);
        Assert.DoesNotContain(all, entry => entry.Session == "s0");
        Assert.Contains(all, entry => entry.Session == $"s{AutoLandings.ClosedKept + 4}");
    }

    // ——— what the conversation keeps of each, by code

    /// <summary>
    /// Each try the conversation is told of is a note whose lead-in is a code (LANG1a), worded in both catalogues, with the
    /// landing's own sentence beneath it as a part of its own; an acceptance reads as one, for the trace.
    /// </summary>
    [Fact]
    public void Each_try_is_a_coded_note_and_an_acceptance_reads_as_one()
    {
        var landed = new TreeLanding(true, "put the work on `feature/q1-fix` — 2 commit(s) from `main`.", "feature/q1-fix");

        var note = AutoLandingNotes.Of(AutoLandingCode.Landed, landed);

        Assert.Equal(SessionEventKind.Note, note.Kind);
        Assert.StartsWith(LandingRules.AutoAccepted, note.Text);
        Assert.EndsWith(landed.Message, note.Text);
        Assert.Equal(NoteCodes.LandingAccepted.Code, note.Parts![0].Code);
        Assert.Equal("feature/q1-fix", note.Parts[0].Value("branch"));
        Assert.Equal((landed.Message, NoteBy.Program), (note.Parts[1].Words, note.Parts[1].By));
        Assert.True(LandingRules.IsAcceptance(note.Text!));

        var unready = AutoLandingNotes.Of(AutoLandingCode.PluginUnready,
            new TreeLanding(true, "put the work on `feature/q1-fix`.", "feature/q1-fix") { Unready = "plugin `acme.lands` is switched off on this machine." },
            plugin: "acme.lands");
        Assert.Equal(("feature/q1-fix", "acme.lands"), (unready.Parts![0].Value("branch") as string, unready.Parts[0].Value("plugin") as string));
        Assert.Contains("`trees hand`", unready.Text);
        Assert.True(LandingRules.IsAcceptance(unready.Text!));

        var dirty = AutoLandingNotes.Of(AutoLandingCode.Uncommitted, landed: null, uncommitted: 3);
        Assert.Equal(3, dirty.Parts![0].Value("paths"));
        Assert.Single(dirty.Parts);
        Assert.False(LandingRules.IsAcceptance(dirty.Text!));

        Assert.Equal(NoteCodes.LandingNothing.Code, AutoLandingNotes.Of(AutoLandingCode.Nothing, landed: null).Parts![0].Code);
        Assert.Equal(NoteCodes.LandingHeld.Code, AutoLandingNotes.Of(AutoLandingCode.Held, landed: null).Parts![0].Code);
        Assert.Equal(NoteCodes.LandingNoTree.Code, AutoLandingNotes.Concluded(AutoConcluded.NoTree).Parts![0].Code);
        Assert.Equal(NoteCodes.LandingNotDone.Code, AutoLandingNotes.Concluded(AutoConcluded.NotDone).Parts![0].Code);
    }

    /// <summary>Every code a try can keep that the conversation is told of has a note code; the rest stay the due list's and the log's.</summary>
    [Fact]
    public void The_codes_a_note_says_each_have_a_note_code()
    {
        foreach (var code in new[]
                 {
                     AutoLandingCode.Landed, AutoLandingCode.PluginUnready, AutoLandingCode.PluginFailed, AutoLandingCode.Nothing,
                     AutoLandingCode.Held, AutoLandingCode.Uncommitted, AutoLandingCode.Exists, AutoLandingCode.Refused,
                 })
        {
            Assert.True(AutoLandingNotes.Says(code), code);
            var part = AutoLandingNotes.Of(code, new TreeLanding(code is AutoLandingCode.Landed or AutoLandingCode.PluginUnready or AutoLandingCode.PluginFailed, "said.", "feature/x"), plugin: "acme.lands", uncommitted: 1).Parts![0];
            Assert.Contains(NoteCodes.All, each => each.Code == part.Code);
        }

        foreach (var code in new[] { AutoLandingCode.Already, AutoLandingCode.Superseded, AutoLandingCode.Gone, AutoLandingCode.Undone, AutoLandingCode.Off })
        {
            Assert.False(AutoLandingNotes.Says(code), code);
        }
    }

    /// <summary>The machine log's line (D94): codes and counts, the plugin's id, never a sentence, a branch's words or a path.</summary>
    [Fact]
    public void The_logs_line_is_codes_and_counts_and_never_words()
    {
        var line = LandingLine.Auto("s1", "engine", "aurora", AutoLandingCode.PluginFailed, commits: 2, uncommitted: null, plugin: "acme.lands", pushed: false);

        Assert.Equal("landing.auto", line.Event);
        Assert.Equal(["session", "repository", "workspace", "code", "commits", "uncommitted", "plugin", "pushed"], line.Data.Select(pair => pair.Key));
        Assert.Equal("acme.lands", line.Data.Single(pair => pair.Key == "plugin").Value);
        Assert.Null(LandingLine.Auto("s1", "engine", "aurora", AutoLandingCode.Landed, null, null, plugin: "not a plugin's name", pushed: null)
            .Data.Single(pair => pair.Key == "plugin").Value);
    }
}

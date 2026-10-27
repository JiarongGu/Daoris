using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 mod-x, the Workplace-names-Participants cut: the behaviour the cut keeps, written against the surface that exists on both
/// sides of it (the harness suffix and its set-aside spelling in deletion's inventory, the binding forget, held and refusal path, the
/// kept conversation's answers as the tool-home origins read them, what holds a tree for a helper's wait, the effective landing and the
/// merges note's words at each access source, and the piece keeper's outcomes and the ref reading), so the same cases pass on the base
/// and after it. The seams the cut adds are <c>WorkplaceParticipantsPortsTests</c>'.
/// </summary>
public sealed class WorkplaceParticipantsBoundaryTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "workplace-participants", Guid.NewGuid().ToString("N")[..8]);
    private string Sessions => DaorisHome.Entry(_home, HomeEntry.Sessions);
    private string Db => DesktopStore.LocationIn(_home);

    public WorkplaceParticipantsBoundaryTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Remove(_home);
    }

    private HarnessConversations Newer() => new(_home, () => StoreSettled.Yes);

    // ——— The harness suffix and its set-aside spelling

    [Fact]
    public void The_harness_suffix_and_its_set_aside_spelling_keep_their_literal_bytes_in_the_inventory()
    {
        Assert.Equal(".harness.json", HarnessConversations.Suffix);
        Assert.Equal(".imported", HarnessConversations.SetAsideSuffix);
        Assert.Equal(".imported", BindingStore.SetAsideSuffix);
        Assert.Contains(".harness.json", SessionHomeFiles.SessionSuffixes);
        Assert.Contains(".harness.json.imported", SessionHomeFiles.SessionSuffixes);
        Assert.Equal(Path.Combine(Sessions, "s1.harness.json"), new HarnessConversations(_home).PathOf("s1"));
        Assert.Equal(Path.Combine(Sessions, "s1.harness.json.imported"), new BindingStore(_home, null, _ => null).SetAsideOf("s1"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(".harness.json")]
    [InlineData(".harness.json.imported")]
    public void The_harness_files_go_in_inventory_order_under_the_harness_name_and_a_refusal_goes_on(string? refused)
    {
        Directory.CreateDirectory(Sessions);
        var conversation = Path.Combine(Sessions, "s1.events.jsonl");
        var kept = Path.Combine(Sessions, "s1.harness.json");
        var setAside = Path.Combine(Sessions, "s1.harness.json.imported");
        var other = Path.Combine(Sessions, "s2.harness.json");
        File.WriteAllText(conversation, "abc");
        File.WriteAllText(kept, "12345");
        File.WriteAllText(setAside, "1234567");
        File.WriteAllText(other, "another session");
        var calls = new List<string>();
        var files = new SessionHomeFiles(_home, (path, folder) =>
        {
            Assert.False(folder);
            calls.Add(path);
            if (refused is not null && path.EndsWith(refused, StringComparison.Ordinal) && !path.EndsWith(refused + ".imported", StringComparison.Ordinal))
                throw new IOException("held");
            SessionHomeFiles.FromDisk(path, folder);
        });

        Assert.Equal((3L, 12L), (files.Size("s1").Conversations, files.Size("s1").Other));
        var result = files.Remove(["s1"], null);

        Assert.Equal([conversation, kept, setAside], calls);
        Assert.Equal(["conversation", "harness"], result.Went["s1"]);
        Assert.Equal(refused is null ? [] : ["harness"], result.Stayed["s1"]);
        Assert.Equal(refused is null ? [] : [refused == ".harness.json" ? kept : setAside], result.Failed);
        Assert.Equal("another session", File.ReadAllText(other));
        Assert.False(File.Exists(Db));
    }

    // ——— The binding's forget, the held read and the refusal

    [Fact]
    public void A_record_deleted_forgets_its_binding_after_its_files_and_names_it_as_the_harness()
    {
        var conversations = Newer();
        conversations.Keep("s1", "claude-code-acp", "c-1");
        conversations.Keep("s2", "codex-acp", "c-2");
        conversations.ImportFiles();
        conversations.Keep("s3", "claude-code-acp", "c-3");

        var removed = new SessionHomeFiles(_home).Remove(["s1", "s3", "s9"], events: null);

        Assert.Empty(removed.Failed);
        Assert.Equal(["harness"], removed.Went["s1"]);
        Assert.Equal(["harness"], removed.Went["s3"]);
        Assert.Empty(removed.Went["s9"]);
        Assert.Null(conversations.Read("s1"));
        Assert.Null(conversations.Read("s3"));
        Assert.Equal("c-2", conversations.Read("s2")!.Conversation);
        Assert.True(File.Exists(conversations.PathOf("s2") + ".imported"));
    }

    [Fact]
    public void A_store_that_will_not_take_the_delete_is_a_failure_at_its_location_and_holds_the_bindings_it_still_holds()
    {
        var conversations = Newer();
        conversations.Keep("s1", "claude-code-acp", "c-1");
        conversations.Keep("s2", "claude-code-acp", "c-2");
        conversations.ImportFiles();
        // A newer Daoris migrated the store and stamped it past this build's: this build reads what it knows and writes nothing.
        using (DesktopStore.Open(Db, new DesktopStoreOptions
        {
            Writer = 2,
            Steps = [.. DesktopStore.Steps, new StoreMigration(DesktopStore.Steps.Count + 1, 2, write => write.Execute("CREATE TABLE raised (x TEXT)"))],
            Settled = () => StoreSettled.Yes,
        }))
        {
        }

        Assert.Equal(Db, conversations.StoreLocation);
        Assert.Equal(["s1"], conversations.BindingsOf(["s1", "s7"]));
        Assert.ThrowsAny<DesktopStoreException>(() => conversations.ForgetBindings(["s1"]));
        var removed = new SessionHomeFiles(_home).Remove(["s1", "s7"], events: null);

        Assert.Equal([Db], removed.Failed);
        Assert.Equal(["harness"], removed.Stayed["s1"]);
        Assert.Empty(removed.Stayed["s7"]);
        Assert.Equal(["harness"], removed.Went["s1"]); // its set-aside file went; the binding stayed
        Assert.Equal(["c-1", "c-2"], new[] { "s1", "s2" }.Select(session => conversations.Read(session)!.Conversation));
    }

    // ——— What a kept conversation answers as the tool-home origins read it

    private static string Records(string session = "s1") => JsonSerializer.Serialize(new object[] { new { id = session, state = "completed", tree = "trees/" + session } });

    [Fact]
    public void The_origin_of_an_ended_record_without_a_profile_keeps_what_the_conversation_answers()
    {
        // Nothing kept.
        Assert.Null(Assert.Single(ToolHomeLeftovers.Origins(_home, Records())!).Keeps);

        // A readable file: resumable; one that does not read as a conversation: unread; one that will not open: unread.
        Directory.CreateDirectory(Sessions);
        var file = Path.Combine(Sessions, "s1.harness.json");
        GoOnFiles.WriteConversation(file, "claude-code-acp", "c-1");
        Assert.Equal("resumable", Assert.Single(ToolHomeLeftovers.Origins(_home, Records())!).Keeps);
        File.WriteAllText(file, "{broken");
        Assert.Equal("record-unread", Assert.Single(ToolHomeLeftovers.Origins(_home, Records())!).Keeps);
        GoOnFiles.WriteConversation(file, "claude-code-acp", "c-1");
        using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Assert.Equal("record-unread", Assert.Single(ToolHomeLeftovers.Origins(_home, Records())!).Keeps);
        File.Delete(file);
        Assert.Null(Assert.Single(ToolHomeLeftovers.Origins(_home, Records())!).Keeps);
    }

    [Fact]
    public void A_binding_in_the_store_is_a_conversation_and_a_later_file_is_read_as_it_stands()
    {
        var conversations = Newer();
        conversations.Keep("s1", "claude-code-acp", "c-1");
        conversations.ImportFiles();
        Assert.False(File.Exists(conversations.PathOf("s1")));

        Assert.Equal((true, true), conversations.Kept("s1"));
        Assert.Equal("resumable", Assert.Single(ToolHomeLeftovers.Origins(_home, Records())!).Keeps);
        Assert.Equal((false, false), conversations.Kept("s2"));
        Assert.Null(Assert.Single(ToolHomeLeftovers.Origins(_home, Records("s2"))!).Keeps);

        File.WriteAllText(conversations.PathOf("s2"), "{broken");
        Assert.Equal((true, false), conversations.Kept("s2"));
        Assert.Equal("record-unread", Assert.Single(ToolHomeLeftovers.Origins(_home, Records("s2"))!).Keeps);
        Assert.Equal((false, false), conversations.Kept("not an id/s3"));
    }

    // ——— What holds a tree for a helper's wait

    private static PriorSession Waiting(string session = "s1") =>
        new(session, "trees/s-1", "completed") { Quest = "q1", Tree = "trees/s-1", NoteParts = [NoteCodes.EndedAwaitsHelper.Part("a line.", [])], Said = [] };

    [Fact]
    public void A_record_holds_its_tree_for_its_helper_only_as_a_completed_record_of_this_machine_carrying_the_code()
    {
        var waiting = Waiting();

        Assert.True(HelperWaits.HoldsTree(waiting));
        Assert.True(HelperWaits.HoldsTree(waiting with { State = "Completed" }));
        Assert.True(HelperWaits.HoldsTree(waiting with { Said = [new SaidWordView("w1", "the answer", At, [], true) { Helper = "h1" }] }));
        Assert.True(HelperWaits.HoldsTree(waiting with { NoteParts = [NoteCodes.EndedAwaits.Part("a line.", []), NoteCodes.EndedAwaitsHelper.Part("a line.", [])] }));
        Assert.False(HelperWaits.HoldsTree(waiting with { State = "working" }));
        Assert.False(HelperWaits.HoldsTree(waiting with { Session = "laptop/s1" }));
        Assert.False(HelperWaits.HoldsTree(waiting with { NoteParts = [NoteCodes.EndedAwaits.Part("a line.", [])] }));
        Assert.False(HelperWaits.HoldsTree(waiting with { NoteParts = [] }));
        Assert.False(HelperWaits.HoldsTree(waiting with { NoteParts = null }));
        Assert.True(HelperWaits.Ended([NoteCodes.EndedAwaitsHelper.Part("a line.", [])]));
        Assert.False(HelperWaits.Ended(null));
        Assert.False(HelperWaits.Ended([NoteCodes.EndedAwaits.Part("a line.", [])]));
    }

    [Fact]
    public async Task The_tidy_keeps_the_trees_of_records_waiting_on_helpers_whose_quest_is_taken_and_a_terminal_record_still_holds_its_lock()
    {
        using var ledger = new Ledger();
        using var service = new ServiceClient("http://ledger.test", null, new HttpClient(ledger, disposeHandler: false));

        var held = (await BranchTidying.WaitingOnHelpersAsync(service, default)).ToList();

        Assert.Equal(["trees/waiting-taken"], held);
        // A record that ended waiting on its helper keeps its lock; a plain terminal one lets go (the tree is not there, so nothing is unlocked).
        Assert.False(await SessionLocks.LetGoEndedAsync(Waiting() with { Tree = _home }));
        Assert.False(await SessionLocks.LetGoEndedAsync(Waiting() with { Tree = null, NoteParts = [] }));
    }

    private sealed class Ledger : HttpMessageHandler
    {
        private static string Session(string id, string state, string? quest, string tree, bool helper) =>
            JsonSerializer.Serialize(new
            {
                id, state, repository = "engine", quest, tree, kind = "driven",
                noteParts = helper ? new[] { new { code = NoteCodes.EndedAwaitsHelper.Code, values = new { }, text = "a line." } } : [],
                created = "2026-10-27T00:00:00Z", updated = "2026-10-27T00:00:00Z",
            });

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.PathAndQuery;
            var body = path.StartsWith("/api/sessions", StringComparison.Ordinal)
                ? "[" + string.Join(",",
                    Session("waiting-taken", "completed", "q1", "trees/waiting-taken", helper: true),
                    Session("waiting-closed", "completed", "q2", "trees/waiting-closed", helper: true),
                    Session("waiting-noquest", "completed", null!, "trees/waiting-noquest", helper: true),
                    Session("plain", "completed", "q1", "trees/plain", helper: false),
                    Session("working", "working", "q1", "trees/working", helper: true),
                    Session("laptop/theirs", "completed", "q1", "trees/theirs", helper: true)) + "]"
                : "[" + string.Join(",",
                    "{\"id\":\"q1\",\"status\":\"Taken\",\"repository\":\"engine\",\"title\":\"t\",\"description\":\"d\"}",
                    "{\"id\":\"q2\",\"status\":\"Done\",\"repository\":\"engine\",\"title\":\"t\",\"description\":\"d\"}") + "]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    // ——— The effective landing and the merges note at each access source

    private static AccessSetting Pause(string pause, string value) =>
        new() { Pauses = new Dictionary<string, string>(StringComparer.Ordinal) { [pause] = value } };

    private static DriverConfig Setup(DriverConfig config, string? repository = null, string? workspace = null, string? machine = null) =>
        config with
        {
            Access = repository is null ? config.Access
                : new Dictionary<string, AccessSetting>(StringComparer.OrdinalIgnoreCase) { ["engine"] = Pause("landing", repository) },
            WorkspaceAccess = workspace is null ? config.WorkspaceAccess
                : new Dictionary<string, AccessSetting>(StringComparer.OrdinalIgnoreCase) { ["aurora"] = Pause("landing", workspace) },
            MachineAccess = machine is null ? config.MachineAccess : Pause("landing", machine),
        };

    [Theory]
    [InlineData(LandingForm.Branch, false, null, null, null, false, null)]
    [InlineData(LandingForm.Branch, false, "rule", null, null, true, "repository")]
    [InlineData(LandingForm.Branch, false, null, "rule", null, true, "workspace")]
    [InlineData(LandingForm.Branch, false, null, null, "rule", true, "machine")]
    [InlineData(LandingForm.Branch, false, "person", "rule", "rule", false, null)]
    [InlineData(LandingForm.Branch, false, "rule", "person", "person", true, "repository")]
    [InlineData(LandingForm.Branch, true, null, null, null, true, null)]
    [InlineData(LandingForm.Branch, true, "person", null, null, true, null)]
    [InlineData(LandingForm.Merge, false, "rule", null, null, false, "repository")]
    [InlineData(LandingForm.Merge, false, null, "rule", null, false, "workspace")]
    [InlineData(LandingForm.Merge, false, null, null, "rule", false, "machine")]
    [InlineData(LandingForm.Merge, false, "person", null, null, false, null)]
    public void The_effective_landing_reads_the_resolved_landing_pause_at_each_source(
        string form, bool own, string? repository, string? workspace, string? machine, bool accepts, string? access)
    {
        var rule = form == LandingForm.Merge ? LandingRule.Merge : new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Tidy: true, AutoAccept: own);
        var config = Setup(DriverConfig.Empty.WithWorkspaceLanding("aurora", rule), repository, workspace, machine);

        var effective = LandingRules.Effective(config, "ENGINE", "aurora");

        Assert.Equal((form, accepts, access, LandingSource.Workspace), (effective.Rule.Form, effective.Rule.AutoAccept, effective.Access, effective.Source));
        Assert.Equal(rule with { AutoAccept = accepts }, effective.Rule);
        Assert.Equal(rule, LandingRules.Choose(config, "engine", "aurora").Rule);
    }

    [Fact]
    public void The_effective_landing_reads_full_access_and_a_workspace_in_any_case_as_the_resolution_does()
    {
        var rule = new LandingRule(LandingForm.Branch, "feature/{quest}");
        var full = DriverConfig.Empty.WithWorkspaceLanding("aurora", rule) with { MachineAccess = AccessSetting.FullAccess };

        Assert.Equal(("machine", true), (LandingRules.Effective(full, "engine", "aurora").Access, LandingRules.Effective(full, "engine", "aurora").Rule.AutoAccept));
        var shared = DriverConfig.Empty.WithWorkspaceLanding("Aurora", rule) with
        {
            WorkspaceAccess = new Dictionary<string, AccessSetting>(StringComparer.OrdinalIgnoreCase) { ["AURORA"] = AccessSetting.FullAccess },
        };
        Assert.Equal(("workspace", true), (LandingRules.Effective(shared, "engine", " aurora ").Access, LandingRules.Effective(shared, "engine", " aurora ").Rule.AutoAccept));
    }

    [Theory]
    [InlineData(null, "its", "default")]
    [InlineData("repository", "its repository's", "repository")]
    [InlineData("workspace", "its workspace's", "workspace")]
    [InlineData("machine", "this machine's", "machine")]
    [InlineData("anything else", "the default's", "anything else")]
    public void The_merges_note_names_whose_setup_lands_and_carries_the_source_field(string? access, string whose, string field)
    {
        var said = AutoLandingNotes.Concluded(AutoConcluded.Merges, access);

        Assert.Equal(
            $"its quest was done, and {whose} access setup lands work by its rule, "
            + "but the rule merges into the line, and a merge never lands by itself: its work waits for your Accept.",
            said.Text);
        Assert.Equal(NoteCodes.LandingMerges.Code, said.Parts![0].Code);
        Assert.Equal(field, said.Parts[0].Value("access"));
        Assert.Equal("the default", AccessRules.Whose("default"));
        Assert.Equal("default", AccessRules.DefaultSource);
    }

    // ——— The piece keeper's outcomes and the ref reading

    [Theory]
    [InlineData(null, null, null)]
    [InlineData("0123abc", "0123abc", "imported")]
    [InlineData("0123ABC", "0123abc", "imported")]
    [InlineData("0123abc", "89fed45", "uncertain")]
    [InlineData("", "0123abc", "uncertain")]
    public void The_ref_reading_answers_absent_imported_or_uncertain(string? refCommit, string? kept, string? expected) =>
        Assert.Equal(expected, PartnerReconcile.AfterRef(refCommit, kept ?? "0123abc"));

    [Fact]
    public void The_keeper_s_three_state_words_are_the_literal_words_the_box_writes()
    {
        Assert.Equal("imported", PartnerStates.Imported);
        Assert.Equal("held", PartnerStates.Held);
        Assert.Equal("uncertain", PartnerStates.Uncertain);
        Assert.Contains(PartnerStates.Imported, PartnerStates.All);
        Assert.Contains(PartnerStates.Held, PartnerStates.All);
        Assert.Contains(PartnerStates.Uncertain, PartnerStates.All);
    }

    private string Copy(params (string Path, string Text)[] files)
    {
        var copy = Path.Combine(_home, "p-piece-" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(copy);
        foreach (var (path, text) in files)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(copy, path))!);
            File.WriteAllText(Path.Combine(copy, path), text);
        }

        return copy;
    }

    private string Index() => Path.Combine(_home, "index-" + Guid.NewGuid().ToString("N")[..6]);

    private static PartnerAsk Ask(string id = "p1") => new(id, "lead", "Make it.", [], "stub-b", new string('b', 40), [], PartnerAsk.ByPerson, At);

    /// <summary>The keeper's call with the ask that names its id and base; git answers as <paramref name="answer"/> scripts it.</summary>
    private static async Task<(PieceKept Kept, List<string> Asked)> KeepAsync(
        string copy, string index, Func<IReadOnlyList<string>, (int, string, string)> answer, string id = "p1")
    {
        var asked = new List<string>();
        WorkingTree.Answered.Value = args =>
        {
            asked.Add(string.Join(' ', args));
            return answer(args);
        };
        try
        {
            var ask = Ask(id);
            return (await PieceKeeper.KeepAsync("root", copy, ask.Id, ask.Base, index, At, "piece " + id, CancellationToken.None), asked);
        }
        finally { WorkingTree.Answered.Value = null; }
    }

    [Fact]
    public async Task A_base_the_repository_cannot_list_is_uncertain_with_git_s_words_and_nothing_else_asked()
    {
        var (kept, asked) = await KeepAsync(Copy(("a.txt", "x")), Index(), _ => (128, "", "  fatal: not a tree\n"));

        Assert.Equal(new PieceKept(PartnerStates.Uncertain, null, PartnerWhy.BaseMissing, null, "fatal: not a tree"), kept);
        Assert.Equal([$"ls-tree -r -z --full-tree {new string('b', 40)}"], asked);
    }

    [Fact]
    public async Task A_copy_with_no_work_is_no_state_and_one_left_out_by_the_ignore_rules_is_held()
    {
        var none = await KeepAsync(Copy(), Index(), _ => (0, "", ""));
        Assert.Equal(new PieceKept(null, null, PartnerWhy.NoWork, none.Kept.Judged, null), none.Kept);
        Assert.True(none.Kept.Judged!.Empty);

        var held = await KeepAsync(Copy(("debug.log", "ignored")), Index(), args => args[0] == "check-ignore" ? (0, "debug.log\0", "") : (0, "", ""));
        Assert.Equal(PartnerStates.Held, held.Kept.State);
        Assert.Equal((null, PartnerWhy.ImportRefused, null), (held.Kept.Commit, held.Kept.Code, held.Kept.Why));
        Assert.Equal([new PieceLeftOut("debug.log", PieceImport.WhyIgnored)], held.Kept.Judged!.LeftOut);
        Assert.Equal(["ls-tree", "check-ignore"], held.Asked.Select(line => line.Split(' ')[0]));
    }

    private static (int, string, string) Making(IReadOnlyList<string> args, string standing, int updateRef = 0, string? afterwards = null) => args[0] switch
    {
        "ls-tree" => (0, "100644 blob 1111111111111111111111111111111111111111\tREADME.md\0", ""),
        "check-ignore" => (0, "", ""),
        "read-tree" => (0, "", ""),
        "hash-object" => (0, "2222222222222222222222222222222222222222\n", ""),
        "update-index" => (0, "", ""),
        "write-tree" => (0, "3333333333333333333333333333333333333333\n", ""),
        "commit-tree" => (0, "4444444444444444444444444444444444444444\n", ""),
        "rev-parse" => (standing.Length > 0 ? 0 : 1, standing.Length > 0 ? standing + "\n" : "", ""),
        "update-ref" => (updateRef, "", ""),
        _ => (1, "", "unexpected " + args[0]),
    };

    [Fact]
    public async Task A_kept_piece_is_one_commit_frozen_create_only_and_imported()
    {
        var index = Index();
        File.WriteAllText(index, "an index");
        var copy = Copy(("src/new.txt", "work"));
        var (kept, asked) = await KeepAsync(copy, index, args => Making(args, ""));

        Assert.Equal((PartnerStates.Imported, "4444444444444444444444444444444444444444", null, null), (kept.State, kept.Commit, kept.Code, kept.Why));
        Assert.Equal(
            [
                $"ls-tree -r -z --full-tree {new string('b', 40)}",
                "check-ignore --no-index -z --stdin",
                $"read-tree {new string('b', 40)}",
                $"hash-object -w --no-filters -- {Path.Combine(copy, "src", "new.txt")}",
                "update-index -z --index-info",
                "write-tree",
                $"commit-tree --no-gpg-sign 3333333333333333333333333333333333333333 -p {new string('b', 40)} -m piece p1",
                $"rev-parse --verify --quiet {PieceRefs.Of("p1")}^{{commit}}",
                $"update-ref -m daoris: piece p1 {PieceRefs.Of("p1")} 4444444444444444444444444444444444444444 ",
            ],
            asked);
        Assert.False(File.Exists(index));
    }

    [Theory]
    [InlineData("4444444444444444444444444444444444444444", 0, PartnerStates.Imported, null)]
    [InlineData("5555555555555555555555555555555555555555", 0, PartnerStates.Uncertain, PartnerWhy.RefMoved)]
    public async Task A_ref_already_standing_is_the_same_import_or_never_moved(string standing, int updateRef, string state, string? code)
    {
        var (kept, asked) = await KeepAsync(Copy(("src/new.txt", "work")), Index(), args => Making(args, standing, updateRef));

        Assert.Equal((state, "4444444444444444444444444444444444444444", code), (kept.State, kept.Commit, kept.Code));
        Assert.DoesNotContain(asked, line => line.StartsWith("update-ref", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_ref_that_cannot_be_created_reads_again_and_is_uncertain_where_it_stands_elsewhere_or_not_at_all()
    {
        var calls = 0;
        var elsewhere = await KeepAsync(Copy(("src/new.txt", "work")), Index(), args =>
            args[0] == "rev-parse" ? (calls++ == 0 ? (1, "", "") : (0, "5555555555555555555555555555555555555555\n", "")) : Making(args, "", updateRef: 1));
        Assert.Equal((PartnerStates.Uncertain, PartnerWhy.RefMoved), (elsewhere.Kept.State, elsewhere.Kept.Code));

        var same = await KeepAsync(Copy(("src/new.txt", "work")), Index(), args =>
            args[0] == "rev-parse" ? (calls++ % 2 == 1 ? (1, "", "") : (0, "4444444444444444444444444444444444444444\n", "")) : Making(args, "", updateRef: 1));
        Assert.Equal((PartnerStates.Imported, null), (same.Kept.State, same.Kept.Code));

        var absent = await KeepAsync(Copy(("src/new.txt", "work")), Index(), args => Making(args, "", updateRef: 1));
        Assert.Equal((PartnerStates.Uncertain, PartnerWhy.RefMoved), (absent.Kept.State, absent.Kept.Code));
    }

    [Fact]
    public async Task A_step_that_fails_is_uncertain_with_its_refusal_and_the_index_is_removed()
    {
        foreach (var (failing, code) in new[] { ("read-tree", PartnerWhy.BaseMissing), ("hash-object", PartnerWhy.ImportRefused), ("update-index", PartnerWhy.ImportRefused),
                     ("write-tree", PartnerWhy.ImportRefused), ("commit-tree", PartnerWhy.ImportRefused) })
        {
            var index = Index();
            File.WriteAllText(index, "an index");
            var (kept, asked) = await KeepAsync(Copy(("src/new.txt", "work")), index, args => args[0] == failing ? (1, "", "  refused: " + failing + "\n") : Making(args, ""));

            Assert.Equal((PartnerStates.Uncertain, code, "refused: " + failing), (kept.State, kept.Code, kept.Why));
            Assert.Equal(failing, asked[^1].Split(' ')[0]);
            Assert.False(File.Exists(index));
        }
    }

    [Fact]
    public async Task The_freeze_reads_the_ref_first_so_a_ref_at_the_commit_is_imported_without_a_write()
    {
        WorkingTree.Answered.Value = args => args[0] == "rev-parse" ? (0, "abc\n", "") : (1, "", "");
        try
        {
            Assert.Equal(PartnerStates.Imported, await PieceKeeper.FreezeAsync("root", "p1", "abc", default));
            Assert.Equal(PartnerStates.Uncertain, await PieceKeeper.FreezeAsync("root", "p1", "def", default));
        }
        finally { WorkingTree.Answered.Value = null; }
    }
}

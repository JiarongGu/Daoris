using System.Text.Json;
using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 mod-x, the Workplace-names-Participants cut: the seams the cut adds. Workplace declares the harness bindings' facet and
/// factory, the access setup's resolution and words, the pure helper-wait and piece-state rules; Participants implements and installs
/// them. The cases that need no new seam are <see cref="WorkplaceParticipantsBoundaryTests"/>'.
/// </summary>
[Collection("agent operation ports")]
public sealed class WorkplaceParticipantsPortsTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "workplace-participants-ports", Guid.NewGuid().ToString("N")[..8]);
    private string Sessions => DaorisHome.Entry(_home, HomeEntry.Sessions);

    public WorkplaceParticipantsPortsTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Remove(_home);
    }

    // ——— The lower vocabulary and the pure rules

    [Fact]
    public void The_lower_suffixes_and_state_words_are_the_bytes_the_higher_constants_alias()
    {
        Assert.Equal(".harness.json", SessionHomeFiles.HarnessSuffix);
        Assert.Equal(".imported", SessionHomeFiles.HarnessSetAsideSuffix);
        Assert.Equal(SessionHomeFiles.HarnessSuffix, HarnessConversations.Suffix);
        Assert.Equal(SessionHomeFiles.HarnessSetAsideSuffix, HarnessConversations.SetAsideSuffix);
        Assert.Equal(SessionHomeFiles.HarnessSetAsideSuffix, BindingStore.SetAsideSuffix);
        Assert.Equal((PieceKeptStates.Held, PieceKeptStates.Imported, PieceKeptStates.Uncertain), (PartnerStates.Held, PartnerStates.Imported, PartnerStates.Uncertain));
        Assert.Equal(("held", "imported", "uncertain"), (PieceKeptStates.Held, PieceKeptStates.Imported, PieceKeptStates.Uncertain));
        Assert.Equal(
            ["asked", "waiting", "approved", "preparing", "started", "ending", "held", "imported", "answered", "handed", "taken-in", "not-taken-in", "discarded", "refused", "uncertain"],
            PartnerStates.All);
    }

    [Fact]
    public void The_pure_rules_answer_what_the_higher_names_delegating_to_them_answer()
    {
        foreach (var (refCommit, kept) in new (string?, string)[] { (null, "a"), ("a", "a"), ("A", "a"), ("a", "b"), ("", "a") })
        {
            Assert.Equal(PartnerReconcile.AfterRef(refCommit, kept), PieceKeptStates.AfterRef(refCommit, kept));
        }

        var waiting = new PriorSession("s1", "trees/s-1", "completed") { Quest = "q1", NoteParts = [NoteCodes.EndedAwaitsHelper.Part("a line.", [])], Said = [] };
        foreach (var record in new[]
                 {
                     waiting, waiting with { State = "COMPLETED" }, waiting with { State = "working" }, waiting with { Session = "laptop/s1" },
                     waiting with { NoteParts = [NoteCodes.EndedAwaits.Part("a line.", [])] }, waiting with { NoteParts = null },
                 })
        {
            Assert.Equal(HelperWaits.HoldsTree(record), HelperWaitTrees.HoldsTree(record));
            Assert.Equal(HelperWaits.HoldsTree(record) && !record.WordsWaiting, HelperWaits.Waits(record));
            Assert.Equal(HelperWaits.Ended(record.NoteParts), HelperWaitTrees.Carries(record.NoteParts, NoteCodes.EndedAwaitsHelper));
        }
    }

    // ——— The harness bindings

    [Fact]
    public void The_installed_factory_is_participants_and_opens_the_conversations_over_the_home()
    {
        Assert.IsType<WorkplaceHarnessBindingStoreOperations>(WorkplaceHarnessBindings.Current);
        var opened = WorkplaceHarnessBindings.Current.Open(_home);
        Assert.IsType<HarnessConversations>(opened);
        Assert.NotSame(opened, WorkplaceHarnessBindings.Current.Open(_home));
        Assert.Equal(DesktopStore.LocationIn(_home), opened.StoreLocation);
        Assert.Equal((false, false), opened.Kept("s1"));
        Assert.Empty(opened.BindingsOf(["s1"]));
        Assert.Empty(opened.ForgetBindings(["s1"]));
        Assert.False(File.Exists(DesktopStore.LocationIn(_home)));
    }

    [Fact]
    public void Deletion_opens_the_supplied_factory_once_after_the_files_forgets_then_names_and_ignores_unknown_ids()
    {
        var actual = WorkplaceHarnessBindings.Current;
        var bindings = new Bindings { Forgets = ["s1"] };
        Directory.CreateDirectory(Sessions);
        File.WriteAllText(Path.Combine(Sessions, "s1.events.jsonl"), "abc");
        try
        {
            WorkplaceHarnessBindings.Current = new Stores(bindings);
            var removed = new SessionHomeFiles(_home).Remove(["s1", "s2"], null);

            Assert.Equal([$"open:{_home}", "forget:s1,s2"], bindings.Calls);
            Assert.Equal(["conversation", "harness"], removed.Went["s1"]);
            Assert.Empty(removed.Went["s2"]);
            Assert.Empty(removed.Failed);
        }
        finally { WorkplaceHarnessBindings.Current = actual; }
    }

    [Theory]
    [InlineData("store")]
    [InlineData("sqlite")]
    [InlineData("io")]
    [InlineData("access")]
    public void A_refused_forget_is_a_failure_at_the_store_and_the_held_bindings_stay_under_the_harness_name(string failure)
    {
        var actual = WorkplaceHarnessBindings.Current;
        var bindings = new Bindings { ForgetFails = Failure(failure), Holds = ["s2"] };
        try
        {
            WorkplaceHarnessBindings.Current = new Stores(bindings);
            var removed = new SessionHomeFiles(_home).Remove(["s1", "s2"], null);

            Assert.Equal([$"open:{_home}", "forget:s1,s2", "holding:s1,s2"], bindings.Calls);
            Assert.Equal(["store-here"], removed.Failed);
            Assert.Equal(["harness"], removed.Stayed["s2"]);
            Assert.Empty(removed.Stayed["s1"]);
        }
        finally { WorkplaceHarnessBindings.Current = actual; }
    }

    [Fact]
    public void A_refused_forget_whose_held_read_is_refused_too_holds_every_session_and_other_failures_propagate_unchanged()
    {
        var actual = WorkplaceHarnessBindings.Current;
        try
        {
            WorkplaceHarnessBindings.Current = new Stores(new Bindings { ForgetFails = Failure("store"), HoldingFails = Failure("io") });
            var removed = new SessionHomeFiles(_home).Remove(["s1", "s2"], null);
            Assert.Equal(["store-here"], removed.Failed);
            Assert.Equal(["harness"], removed.Stayed["s1"]);
            Assert.Equal(["harness"], removed.Stayed["s2"]);

            // Neither filter catches a failure of another kind, at the forget or at the read of what is held.
            var other = new InvalidOperationException("not a store's");
            WorkplaceHarnessBindings.Current = new Stores(new Bindings { ForgetFails = other });
            Assert.Same(other, Assert.Throws<InvalidOperationException>(() => new SessionHomeFiles(_home).Remove(["s1"], null)));
            var unread = new InvalidOperationException("not a read's");
            WorkplaceHarnessBindings.Current = new Stores(new Bindings { ForgetFails = Failure("store"), HoldingFails = unread });
            Assert.Same(unread, Assert.Throws<InvalidOperationException>(() => new SessionHomeFiles(_home).Remove(["s1"], null)));
        }
        finally { WorkplaceHarnessBindings.Current = actual; }
    }

    [Fact]
    public void A_missing_factory_is_required_only_where_deletion_reaches_the_bindings_and_where_a_kept_conversation_is_read()
    {
        var actual = WorkplaceHarnessBindings.Current;
        try
        {
            WorkplaceHarnessBindings.Current = null!;

            // Nothing that is a session's id reaches the bindings, and a live or profiled record never asks what is kept.
            Assert.Empty(new SessionHomeFiles(_home).Remove([], null).Went);
            Assert.Equal("session", Assert.Single(ToolHomeLeftovers.Origins(_home, Record("s1", "working"))!).Keeps);
            Assert.Null(Assert.Single(ToolHomeLeftovers.Origins(_home, Record("s1", "completed", profile: "work"))!).Keeps);

            var missing = Assert.Throws<InvalidOperationException>(() => new SessionHomeFiles(_home).Remove(["s1"], null));
            Assert.Contains("Participants installs it", missing.Message);
            Assert.Throws<InvalidOperationException>(() => ToolHomeLeftovers.Origins(_home, Record("s1", "completed")));
        }
        finally { WorkplaceHarnessBindings.Current = actual; }
    }

    [Theory]
    [InlineData(false, false, null)]
    [InlineData(true, true, "resumable")]
    [InlineData(true, false, "record-unread")]
    public void The_origin_keeps_what_the_supplied_conversations_answer_for_each_record_once(bool kept, bool reads, string? expected)
    {
        var actual = WorkplaceHarnessBindings.Current;
        var bindings = new Bindings { KeptAnswer = (kept, reads) };
        try
        {
            WorkplaceHarnessBindings.Current = new Stores(bindings);
            var origins = ToolHomeLeftovers.Origins(_home, JsonSerializer.Serialize(new object[]
            {
                new { id = "s1", state = "completed", tree = "trees/a" },
                new { id = "s2", state = "working", tree = "trees/b" },
                new { id = "s3", state = "completed", profile = "work", tree = "trees/c" },
            }))!;

            Assert.Equal([expected, "session", null], origins.Select(origin => origin.Keeps));
            Assert.Equal([$"open:{_home}", "kept:s1"], bindings.Calls);
        }
        finally { WorkplaceHarnessBindings.Current = actual; }
    }

    // ——— The access setup

    [Fact]
    public void The_installed_setup_is_participants_and_resolves_the_rules_own_answer()
    {
        Assert.IsType<WorkplaceAccessSetupOperations>(WorkplaceAccessSetup.Current);
        var config = DriverConfig.Empty with
        {
            Access = new Dictionary<string, AccessSetting>(StringComparer.OrdinalIgnoreCase) { ["engine"] = Pause("landing", "rule") },
            MachineAccess = AccessSetting.FullAccess,
        };

        var resolved = WorkplaceAccessSetup.Current.Resolve(config, " ENGINE ", "aurora");

        Assert.IsType<ResolvedAccess>(resolved);
        foreach (var pause in AccessKey.Pauses)
        {
            Assert.Equal(AccessRules.Resolve(config, "engine", "aurora").Value(pause.Name), resolved.Value(pause.Name));
            Assert.Equal(AccessRules.Resolve(config, "engine", "aurora").Source(pause.Name), resolved.Source(pause.Name));
        }

        Assert.Equal(("rule", "repository", "unrestricted", "machine"), (resolved.Value("landing"), resolved.Source("landing"), resolved.Value("permissions"), resolved.Source("permissions")));
        Assert.Equal("default", WorkplaceAccessSetup.Current.DefaultSource);
        foreach (var source in new[] { "repository", "workspace", "machine", "default", "other" })
        {
            Assert.Equal(AccessRules.Whose(source), WorkplaceAccessSetup.Current.Whose(source));
        }
    }

    [Theory]
    [InlineData("person", false, 0)]
    [InlineData("rule", true, 0)]
    [InlineData("rule", false, 1)]
    public void The_effective_landing_resolves_once_at_its_point_and_reads_the_source_only_where_it_needs_it(string landing, bool own, int sources)
    {
        var actual = WorkplaceAccessSetup.Current;
        var setup = new Setup { Landing = landing };
        var rule = new LandingRule(LandingForm.Branch, "feature/{quest}", Tidy: true, AutoAccept: own);
        var config = DriverConfig.Empty.WithWorkspaceLanding("aurora", rule);
        try
        {
            WorkplaceAccessSetup.Current = setup;
            var effective = LandingRules.Effective(config, "engine", "aurora");

            Assert.Equal(["resolve:engine/aurora"], setup.Calls.Where(call => call.StartsWith("resolve", StringComparison.Ordinal)));
            Assert.Equal(sources, setup.Calls.Count(call => call == "source:landing"));
            Assert.Equal(sources == 1 ? "its-source" : null, effective.Access);
            Assert.True(effective.Rule.AutoAccept == (landing == "rule" || own));
        }
        finally { WorkplaceAccessSetup.Current = actual; }
    }

    [Fact]
    public void The_merges_note_asks_the_setup_for_its_words_only_where_it_names_them_and_the_other_notes_never_do()
    {
        var actual = WorkplaceAccessSetup.Current;
        var setup = new Setup();
        try
        {
            WorkplaceAccessSetup.Current = setup;

            var named = AutoLandingNotes.Concluded(AutoConcluded.Merges, "somewhere");
            Assert.Contains("whose-somewhere's access setup", named.Text);
            Assert.Equal("somewhere", named.Parts![0].Value("access"));
            Assert.Equal(["whose:somewhere"], setup.Calls);

            setup.Calls.Clear();
            var none = AutoLandingNotes.Concluded(AutoConcluded.Merges);
            Assert.Contains("its access setup", none.Text);
            Assert.Equal("the-default", none.Parts![0].Value("access"));
            Assert.Equal(["default-source"], setup.Calls);

            setup.Calls.Clear();
            WorkplaceAccessSetup.Current = null!;
            Assert.NotNull(AutoLandingNotes.Concluded(AutoConcluded.NoTree, "machine").Text);
            Assert.NotNull(AutoLandingNotes.Concluded(AutoConcluded.NotDone).Text);
            Assert.Contains("Participants installs it", Assert.Throws<InvalidOperationException>(() => AutoLandingNotes.Concluded(AutoConcluded.Merges)).Message);
            Assert.Contains("Participants installs it", Assert.Throws<InvalidOperationException>(() => AutoLandingNotes.Concluded(AutoConcluded.Merges, "machine")).Message);
            Assert.Empty(setup.Calls);
        }
        finally { WorkplaceAccessSetup.Current = actual; }
    }

    [Fact]
    public void A_missing_setup_is_required_only_where_the_effective_landing_resolves()
    {
        var actual = WorkplaceAccessSetup.Current;
        var config = DriverConfig.Empty.WithWorkspaceLanding("aurora", LandingRule.Merge);
        try
        {
            WorkplaceAccessSetup.Current = null!;

            Assert.Equal(LandingSource.Workspace, LandingRules.Choose(config, "engine", "aurora").Source);
            var missing = Assert.Throws<InvalidOperationException>(() => LandingRules.Effective(config, "engine", "aurora"));
            Assert.Contains("Participants installs it", missing.Message);
        }
        finally { WorkplaceAccessSetup.Current = actual; }
    }

    // ——— Doubles

    private static AccessSetting Pause(string pause, string value) =>
        new() { Pauses = new Dictionary<string, string>(StringComparer.Ordinal) { [pause] = value } };

    private static string Record(string id, string state, string? profile = null) =>
        JsonSerializer.Serialize(new[] { new { id, state, profile, tree = "trees/" + id } });

    private static Exception Failure(string kind) => kind switch
    {
        "store" => new DesktopStoreException("busy"),
        "sqlite" => new SqliteException("busy", 5),
        "io" => new IOException("held"),
        _ => new UnauthorizedAccessException("held"),
    };

    private sealed class Stores(Bindings bindings) : IWorkplaceHarnessBindingStores
    {
        public IWorkplaceHarnessBindings Open(string home)
        {
            bindings.Calls.Add("open:" + home);
            return bindings;
        }
    }

    private sealed class Bindings : IWorkplaceHarnessBindings
    {
        public List<string> Calls { get; } = [];
        public string[] Forgets { get; init; } = [];
        public string[] Holds { get; init; } = [];
        public Exception? ForgetFails { get; init; }
        public Exception? HoldingFails { get; init; }
        public (bool Kept, bool Reads) KeptAnswer { get; init; }

        public string StoreLocation => "store-here";

        public (bool Kept, bool Reads) Kept(string session)
        {
            Calls.Add("kept:" + session);
            return KeptAnswer;
        }

        public IReadOnlyList<string> ForgetBindings(IReadOnlyCollection<string> sessions)
        {
            Calls.Add("forget:" + string.Join(',', sessions));
            return ForgetFails is null ? Forgets : throw ForgetFails;
        }

        public IReadOnlyList<string> BindingsOf(IReadOnlyCollection<string> sessions)
        {
            Calls.Add("holding:" + string.Join(',', sessions));
            return HoldingFails is null ? Holds : throw HoldingFails;
        }
    }

    private sealed class Setup : IWorkplaceAccessSetup
    {
        public List<string> Calls { get; } = [];
        public string Landing { get; init; } = "person";

        public IWorkplaceResolvedAccess Resolve(DriverConfig config, string repository, string? workspace)
        {
            Calls.Add($"resolve:{repository}/{workspace}");
            return new Resolved(this);
        }

        public string Whose(string source)
        {
            Calls.Add("whose:" + source);
            return "whose-" + source;
        }

        public string DefaultSource
        {
            get
            {
                Calls.Add("default-source");
                return "the-default";
            }
        }
    }

    private sealed class Resolved(Setup owner) : IWorkplaceResolvedAccess
    {
        public string Value(string pause)
        {
            owner.Calls.Add("value:" + pause);
            return owner.Landing;
        }

        public string Source(string pause)
        {
            owner.Calls.Add("source:" + pause);
            return "its-source";
        }
    }
}

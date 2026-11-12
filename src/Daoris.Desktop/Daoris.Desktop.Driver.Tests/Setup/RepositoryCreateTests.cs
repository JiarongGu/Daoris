using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>repository create</c> (the grow-department design §3, §4): a department made by one press whose hand ends before any
/// content. The plan reads and writes nothing; the apply makes the folder, an empty history with a real <c>git init</c>, the registration, the
/// drive and the set-up quest, its receipt written before the first effect and a re-press resumed from it. The folders are real, in the
/// repository's gitignored <c>_fixtures</c>; the registry, the identities and the quest door are stand-ins.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class RepositoryCreateTests : IDisposable
{
    private static readonly CreateAuthor Fixture = new("Fixture", "fixture@example.test");

    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "repository-create", Guid.NewGuid().ToString("N")[..8]);

    public RepositoryCreateTests() => Directory.CreateDirectory(_scratch);

    public void Dispose() => GitSeed.Remove(_scratch);

    private string Home => Path.Combine(_scratch, "home");

    private string Work(params string[] parts) => Path.Combine([_scratch, "aurora", .. parts]);

    private static CreateAsk Ask(string name = "ledger", string workspace = "aurora") => new(name, "keeps the books") { Workspace = workspace };

    /// <summary>Two repositories of workspace <c>aurora</c> side by side, each a real checkout on <c>main</c>.</summary>
    private async Task<Stand> MakeAsync(Action<Stand>? shape = null)
    {
        await GitSeed.PlainAsync(Work("atlas"), "atlas");
        await GitSeed.PlainAsync(Work("engine"), "engine");
        var stand = new Stand(Home);
        stand.Registry.Add(new RepoView("atlas", false, Work("atlas"), "aurora"));
        stand.Registry.Add(new RepoView("engine", true, Work("engine"), "aurora"));
        stand.Authors["atlas"] = Fixture;
        stand.Authors["engine"] = Fixture;
        shape?.Invoke(stand);
        return stand;
    }

    private static Task<CreatePlanned> Plan(Stand stand, CreateAsk ask) => RepositoryCreatePlans.PlanAsync(stand.Doors(), ask);

    private static async Task<(CreatePlanned Plan, CreateResult Result)> Apply(Stand stand, CreateAsk ask)
    {
        var plan = await Plan(stand, ask);
        return (plan, await RepositoryCreates.ApplyAsync(stand.Doors(), ask, plan.Digest));
    }

    private static string Refused(CreatePlanned plan, string code) =>
        plan.Refusals.FirstOrDefault(each => each.Code == code)?.Sentence
        ?? throw new Xunit.Sdk.XunitException($"no `{code}` refusal among: {string.Join("; ", plan.Refusals.Select(r => $"{r.Code}: {r.Sentence}"))}");

    // ---- the plan reads and writes nothing, and derives the folder from the registry ----

    [Fact]
    public async Task A_plan_derives_the_folder_under_the_parent_most_of_the_workspaces_roots_share_and_writes_nothing()
    {
        var stand = await MakeAsync(each => each.Registry.Add(new RepoView("odd", false, Path.Combine(_scratch, "elsewhere", "odd"), "aurora")));

        var plan = await Plan(stand, Ask());

        Assert.True(plan.Applies, string.Join("; ", plan.Refusals.Select(r => r.Sentence)));
        Assert.Equal(Work("ledger"), plan.Folder);
        Assert.Equal("main", plan.Line);
        Assert.Equal(Fixture, plan.Author);
        Assert.True(PlaceDigests.Is(plan.Digest));
        Assert.Equal(["folder", "history", "registration", "driving", "setup"], plan.Steps.Select(each => each.Id));
        Assert.False(Directory.Exists(Work("ledger")));
        Assert.False(Directory.Exists(Home));
        Assert.Empty(stand.Bodies);
        Assert.Empty(stand.Setup.Published);
        Assert.Equal(0, stand.Changes);
    }

    [Fact]
    public async Task A_tie_between_parents_goes_to_the_one_holding_the_newest_root()
    {
        var stand = await MakeAsync(each =>
        {
            each.Registry.Clear();
            each.Registry.Add(new RepoView("atlas", false, Work("atlas"), "aurora"));
            each.Registry.Add(new RepoView("odd", false, Path.Combine(_scratch, "elsewhere", "odd"), "aurora"));
            each.Created[Work("atlas")] = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            each.Created[Path.Combine(_scratch, "elsewhere", "odd")] = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        });
        Directory.CreateDirectory(Path.Combine(_scratch, "elsewhere"));

        var plan = await Plan(stand, Ask());

        Assert.Equal(Path.Combine(_scratch, "elsewhere", "ledger"), plan.Folder);
    }

    [Fact]
    public async Task Under_names_the_parent_where_no_root_can_be_read_and_wins_over_the_derived_one()
    {
        var stand = await MakeAsync();
        var under = Path.Combine(_scratch, "elsewhere");
        Directory.CreateDirectory(under);

        var plan = await Plan(stand, Ask() with { Under = under });

        Assert.Equal(Path.Combine(under, "ledger"), plan.Folder);
    }

    // ---- every refusal, with nothing written ----

    [Fact]
    public async Task A_name_already_registered_in_any_workspace_is_refused_whatever_its_case()
    {
        var stand = await MakeAsync(each => each.Registry.Add(new RepoView("Ledger", true, Path.Combine(_scratch, "billing", "Ledger"), "billing")));

        var plan = await Plan(stand, Ask("ledger"));

        Assert.False(plan.Applies);
        Assert.Contains("`Ledger`", Refused(plan, CreateRefusals.NameRegistered));
        Assert.Contains("billing", Refused(plan, CreateRefusals.NameRegistered));
    }

    [Theory]
    [InlineData("")]
    [InlineData("two words")]
    [InlineData("a/b")]
    [InlineData("..")]
    [InlineData("-flag")]
    [InlineData("bad:name")]
    public async Task A_name_that_cannot_be_a_folder_and_a_registered_name_is_refused(string name)
    {
        var stand = await MakeAsync();

        var plan = await Plan(stand, Ask(name));

        Assert.False(plan.Applies);
        Assert.NotEmpty(Refused(plan, CreateRefusals.NameUnusable));
    }

    [Fact]
    public async Task A_workspace_that_does_not_exist_is_refused_and_the_default_one_always_does()
    {
        var stand = await MakeAsync();

        var missing = await Plan(stand, Ask(workspace: "nowhere"));
        var implicit_ = await Plan(stand, new CreateAsk("ledger", "keeps the books") { Under = Path.Combine(_scratch, "aurora") });

        Assert.Contains("`nowhere`", Refused(missing, CreateRefusals.NoWorkspace));
        Assert.DoesNotContain(implicit_.Refusals, each => each.Code == CreateRefusals.NoWorkspace);
        Assert.Equal(RemoteTarget.DefaultWorkspace, implicit_.Workspace);
    }

    [Fact]
    public async Task A_workspace_with_no_repository_to_infer_a_folder_from_is_refused_without_under()
    {
        var stand = await MakeAsync(each => each.Registry.Add(new RepoView("notes", false, null, "side")));

        var plan = await Plan(stand, Ask(workspace: "side"));

        Assert.Contains("--under", Refused(plan, CreateRefusals.NoFolder));
        Assert.Contains("`side`", Refused(plan, CreateRefusals.NoFolder));
    }

    [Fact]
    public async Task An_under_that_is_not_a_folder_here_is_refused()
    {
        var stand = await MakeAsync();

        var plan = await Plan(stand, Ask() with { Under = Path.Combine(_scratch, "not-there") });

        Assert.NotEmpty(Refused(plan, CreateRefusals.UnderMissing));
    }

    [Fact]
    public async Task A_folder_that_exists_and_is_not_empty_is_refused_and_an_empty_one_is_taken()
    {
        var stand = await MakeAsync();
        Directory.CreateDirectory(Work("ledger"));
        Assert.True((await Plan(stand, Ask())).Applies, "an existing empty folder is taken");
        await File.WriteAllTextAsync(Work("ledger", "notes.txt"), "mine\n");

        var plan = await Plan(stand, Ask());

        Assert.False(plan.Applies);
        Assert.Contains(Work("ledger"), Refused(plan, CreateRefusals.FolderExists));
    }

    [Theory]
    [InlineData("-x")]
    [InlineData("bad..line")]
    [InlineData("has space")]
    [InlineData("ends.lock")]
    public async Task An_unusable_line_name_is_refused(string line)
    {
        var stand = await MakeAsync();

        var plan = await Plan(stand, Ask() with { Line = line });

        Assert.Contains($"`{line}`", Refused(plan, CreateRefusals.LineUnusable));
    }

    [Fact]
    public async Task No_git_identity_among_the_workspaces_repositories_is_refused_naming_git_use()
    {
        var stand = await MakeAsync(each => each.Authors.Clear());

        var plan = await Plan(stand, Ask());

        Assert.False(plan.Applies);
        Assert.Contains("daoris git use", Refused(plan, CreateRefusals.NoIdentity));
    }

    [Fact]
    public async Task Git_absent_is_refused_for_a_repository_and_a_folder_needs_none()
    {
        var stand = await MakeAsync();
        WorkingTree.Answered.Value = args => args is ["--version"] ? (-1, "", "git did not start") : null;
        try
        {
            var git = await Plan(stand, Ask());
            var folder = await Plan(stand, Ask() with { Kind = PlaceKind.Folder });

            Assert.NotEmpty(Refused(git, CreateRefusals.NoGit));
            Assert.DoesNotContain(folder.Refusals, each => each.Code == CreateRefusals.NoGit);
        }
        finally
        {
            WorkingTree.Answered.Value = null;
        }
    }

    [Fact]
    public async Task A_service_that_is_not_on_this_machine_is_refused_since_a_folder_here_means_nothing_there()
    {
        var stand = await MakeAsync(each => each.LocalService = false);

        Assert.NotEmpty(Refused(await Plan(stand, Ask()), CreateRefusals.ServiceNotLocal));
    }

    [Fact]
    public async Task A_purpose_a_kind_and_a_line_on_a_folder_are_judged_with_the_rest_at_once()
    {
        var stand = await MakeAsync();

        var plan = await Plan(stand, new CreateAsk("ledger", "  ") { Workspace = "aurora", Kind = "system", Line = "main" });

        Assert.Contains(plan.Refusals, each => each.Code == CreateRefusals.NoPurpose);
        Assert.Contains(plan.Refusals, each => each.Code == CreateRefusals.Kind);
        var folder = await Plan(stand, Ask() with { Kind = PlaceKind.Folder, Line = "main" });
        Assert.NotEmpty(Refused(folder, CreateRefusals.LineFolder));
    }

    // ---- the apply: a real repository with one empty commit, registered, driven, its set-up asked ----

    [Fact]
    public async Task An_apply_makes_a_real_repository_with_one_empty_commit_registers_it_drives_it_and_asks_its_set_up_once()
    {
        var stand = await MakeAsync();
        stand.OnRegister = () =>
        {
            var kept = new CreateReceipts(Home).Unfinished("ledger")!;
            Assert.Equal(
                (PlaceOutcomes.Done, PlaceOutcomes.Done, PlaceOutcomes.Uncertain),
                (kept.Of(CreateOperations.Folder)!.Outcome, kept.Of(CreateOperations.History)!.Outcome, kept.Of(CreateOperations.Registration)!.Outcome));
        };

        var (plan, result) = await Apply(stand, Ask());

        Assert.True(result.Done, result.Message);
        Assert.Null(result.Refused);
        var folder = Work("ledger");
        Assert.Equal("main", (await GitFixture.GitAsync(folder, "symbolic-ref", "--short", "HEAD")).Trim());
        Assert.Equal("1", (await GitFixture.GitAsync(folder, "rev-list", "--count", "HEAD")).Trim());
        Assert.Equal("Fixture|fixture@example.test|Fixture|fixture@example.test", (await GitFixture.GitAsync(folder, "log", "-1", "--format=%an|%ae|%cn|%ce")).Trim());
        Assert.Contains("keeps the books", await GitFixture.GitAsync(folder, "log", "-1", "--format=%s"));
        Assert.Equal("", (await GitFixture.GitAsync(folder, "ls-tree", "-r", "HEAD")).Trim());
        Assert.Equal("", (await GitFixture.GitAsync(folder, "remote")).Trim());
        Assert.Equal([".git"], Directory.EnumerateFileSystemEntries(folder).Select(Path.GetFileName));

        var body = Assert.Single(stand.Bodies);
        Assert.Equal(("ledger", "aurora", PlaceKind.Git, folder, false), ((string)body["repository"]!, (string)body["workspace"]!, (string)body["kind"]!, (string)body["root"]!, (bool)body["adopted"]!));
        Assert.True(stand.Config.Drivable.Contains("ledger"));
        Assert.DoesNotContain("ledger", stand.Config.Holds);

        var published = Assert.Single(stand.Setup.Published);
        Assert.Equal(("aurora", "ledger"), (published.Workspace, published.To));
        Assert.Contains("keeps the books", published.Sentence);
        Assert.Contains(RepositoryCreates.KeyOf(plan.Digest), published.Sentence);

        var receipt = new CreateReceipts(Home).Read(plan.Digest)!;
        Assert.All(receipt.Operations, each => Assert.Equal(PlaceOutcomes.Done, each.Outcome));
        Assert.Equal((await GitFixture.GitAsync(folder, "rev-parse", "HEAD")).Trim(), receipt.Commit);
        Assert.Equal("q-1", receipt.Of(CreateOperations.SetUp)!.Quest);
        Assert.Contains("#q-1", result.Message);
        Assert.Null(new CreateReceipts(Home).Unfinished("ledger"));
        Assert.False(File.Exists(Path.Combine(folder, "daoris.json")));
    }

    // ---- the work follows the set-up as the set-up quest's one step ----

    /// <summary>
    /// The person's work is the set-up quest's one chained step: a quest to the same repository, built from the request and the purpose with the
    /// parent's id left for the service to put in, and never part of the set-up's own words. The service publishes it when the set-up closes done.
    /// </summary>
    [Fact]
    public async Task The_work_is_the_set_up_quests_one_step_to_the_same_repository_and_not_part_of_its_words()
    {
        var stand = await MakeAsync();
        var ask = Ask() with { Work = "Build the first monthly report.\nTotals per month, as a table." };

        var (plan, result) = await Apply(stand, ask);

        Assert.True(result.Done, result.Message);
        var published = Assert.Single(stand.Setup.Published);
        Assert.Equal("ledger", published.To);
        Assert.DoesNotContain("monthly report", published.Sentence);
        Assert.Contains(RepositoryCreates.KeyOf(plan.Digest), published.Sentence);
        var then = Assert.Single(stand.Setup.Thens);
        Assert.NotNull(then);
        Assert.Equal("Build the first monthly report.", then.Title);
        Assert.StartsWith("Build the first monthly report.\nTotals per month, as a table.", then.Body);
        Assert.Contains("{parent}", then.Body);
        Assert.Contains("keeps the books", then.Body);
        Assert.Contains("`ledger`", then.Body);
        Assert.Equal("q-1", new CreateReceipts(Home).Read(plan.Digest)!.Of(CreateOperations.SetUp)!.Quest);
    }

    [Fact]
    public async Task A_title_is_one_line_cut_at_a_word_where_the_work_runs_long()
    {
        var stand = await MakeAsync();
        var ask = Ask() with { Work = string.Join(' ', Enumerable.Repeat("report", 40)) };

        var (_, result) = await Apply(stand, ask);

        Assert.True(result.Done, result.Message);
        var title = Assert.Single(stand.Setup.Thens)!.Title;
        Assert.InRange(title.Length, 1, 101);
        Assert.EndsWith("…", title);
        Assert.DoesNotContain('\n', title);
    }

    [Fact]
    public async Task A_creation_with_no_work_asks_a_set_up_with_no_step()
    {
        var stand = await MakeAsync();

        var (_, result) = await Apply(stand, Ask());

        Assert.True(result.Done, result.Message);
        Assert.Single(stand.Setup.Published);
        Assert.Null(Assert.Single(stand.Setup.Thens));
    }

    /// <summary>A retry that finds the set-up quest by its key asks nothing again, so the chain it carries is not a second one.</summary>
    [Fact]
    public async Task A_resume_that_finds_the_set_up_quest_by_its_key_asks_no_second_chain()
    {
        var stand = await MakeAsync();
        var ask = Ask() with { Work = "Build the first monthly report." };
        var (plan, first) = await Apply(stand, ask);
        Assert.True(first.Done, first.Message);
        var receipts = new CreateReceipts(Home);
        var receipt = receipts.Read(plan.Digest)!;
        receipts.Write(receipt.With(CreateOperations.SetUp, each => each with { Outcome = PlaceOutcomes.Uncertain, Quest = null }));

        var again = await RepositoryCreates.ApplyAsync(stand.Doors(), ask, plan.Digest);

        Assert.True(again.Done, again.Message);
        Assert.Single(stand.Setup.Published);
        Assert.Equal("q-1", receipts.Read(plan.Digest)!.Of(CreateOperations.SetUp)!.Quest);
    }

    /// <summary>A folder department has no set-up quest for the work to follow, so work asked with it is refused rather than dropped.</summary>
    [Fact]
    public async Task Work_asked_of_a_folder_department_is_refused_since_it_has_no_set_up_to_follow()
    {
        var stand = await MakeAsync();

        var plan = await Plan(stand, Ask("notes") with { Kind = PlaceKind.Folder, Work = "File the notes." });

        Assert.False(plan.Applies);
        Assert.Contains("set-up", Refused(plan, CreateRefusals.WorkNeedsSetUp));
        Assert.Empty(stand.Setup.Published);
    }

    [Fact]
    public async Task The_terminals_plan_says_the_work_follows_the_set_up_as_a_quest()
    {
        var stand = await MakeAsync();

        var lines = RepositoryCreateCommand.Lines(await Plan(stand, Ask() with { Work = "Build the first monthly report." }));

        Assert.Contains(lines, line => line.StartsWith("  work    ", StringComparison.Ordinal) && line.Contains("follows the set-up") && line.Contains("Build the first monthly report."));
        Assert.DoesNotContain(lines, line => line.Contains("kept on the receipt"));
    }

    [Fact]
    public async Task The_line_is_the_one_most_of_the_workspaces_repositories_land_on_else_main_and_a_line_asked_wins()
    {
        var stand = await MakeAsync(each =>
        {
            each.Lines["atlas"] = "trunk";
            each.Lines["engine"] = "trunk";
        });

        Assert.Equal("trunk", (await Plan(stand, Ask())).Line);
        Assert.Equal("develop", (await Plan(stand, Ask() with { Line = "develop" })).Line);
        var (_, result) = await Apply(stand, Ask());

        Assert.True(result.Done, result.Message);
        Assert.Equal("trunk", (await GitFixture.GitAsync(Work("ledger"), "symbolic-ref", "--short", "HEAD")).Trim());
        stand.Lines.Clear();
        Assert.Equal("main", (await Plan(stand, Ask("other"))).Line);
    }

    [Fact]
    public async Task Where_the_inherited_workflow_needs_a_remote_the_drive_stays_off_and_the_receipt_says_which_step_and_the_next_press()
    {
        var stand = await MakeAsync(each => each.RemoteStep = "push");

        var (plan, result) = await Apply(stand, Ask());

        Assert.True(result.Done, result.Message);
        Assert.DoesNotContain("ledger", stand.Config.Drivable);
        Assert.Equal("push", plan.RemoteStep);
        var driving = new CreateReceipts(Home).Read(plan.Digest)!.Of(CreateOperations.Driving)!;
        Assert.Contains("`push`", driving.Note);
        Assert.Contains("place ledger --preset <delivery> --apply", driving.Note);
        Assert.Contains("place ledger --preset <delivery> --apply", result.Message);
    }

    [Fact]
    public async Task A_folder_department_is_a_folder_registered_as_one_with_no_history_and_no_set_up()
    {
        var stand = await MakeAsync();
        var ask = Ask("notes") with { Kind = PlaceKind.Folder };

        var (plan, result) = await Apply(stand, ask);

        Assert.True(result.Done, result.Message);
        Assert.Equal(["folder", "registration", "driving"], plan.Steps.Select(each => each.Id));
        Assert.True(Directory.Exists(Work("notes")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Work("notes")));
        Assert.Equal((PlaceKind.Folder, Work("notes"), false), ((string)stand.Bodies[0]["kind"]!, (string)stand.Bodies[0]["root"]!, (bool)stand.Bodies[0]["adopted"]!));
        Assert.Empty(stand.Setup.Published);
        Assert.True(stand.Config.Drivable.Contains("notes"));
    }

    [Fact]
    public async Task A_stale_digest_is_refused_moved_with_nothing_written()
    {
        var stand = await MakeAsync();
        var plan = await Plan(stand, Ask());
        stand.Authors["atlas"] = new CreateAuthor("Other", "other@example.test");
        stand.Authors["engine"] = new CreateAuthor("Other", "other@example.test");

        var result = await RepositoryCreates.ApplyAsync(stand.Doors(), Ask(), plan.Digest);

        Assert.False(result.Done);
        Assert.Equal(PlaceApplyRefusals.Moved, result.Refused);
        Assert.False(Directory.Exists(Work("ledger")));
        Assert.False(Directory.Exists(Home));
        Assert.Empty(stand.Bodies);
    }

    [Fact]
    public async Task A_plan_that_refuses_is_never_applied_and_a_word_that_is_no_digest_is_refused()
    {
        var stand = await MakeAsync(each => each.Authors.Clear());
        var plan = await Plan(stand, Ask());

        var refused = await RepositoryCreates.ApplyAsync(stand.Doors(), Ask(), plan.Digest);
        var word = await RepositoryCreates.ApplyAsync(stand.Doors(), Ask(), "not-a-digest");

        Assert.Equal(PlaceApplyRefusals.Refused, refused.Refused);
        Assert.Contains("daoris git use", refused.Message);
        Assert.Equal(PlaceApplyRefusals.Digest, word.Refused);
        Assert.False(Directory.Exists(Work("ledger")));
    }

    // ---- a failure leaves the receipt and no undoing; a re-press resumes ----

    [Fact]
    public async Task A_failed_registration_leaves_the_folder_and_the_receipt_and_a_re_press_goes_on_from_the_step_after_the_last_done()
    {
        var stand = await MakeAsync(each => each.RegisterAnswers.Enqueue((false, "the registry door answered 500")));
        var ask = Ask();
        var (first, stopped) = await Apply(stand, ask);

        Assert.False(stopped.Done);
        Assert.Contains("the registry door answered 500", stopped.Message);
        Assert.True(Directory.Exists(Work("ledger", ".git")));
        var kept = new CreateReceipts(Home).Read(first.Digest)!;
        Assert.Equal(
            [PlaceOutcomes.Done, PlaceOutcomes.Done, PlaceOutcomes.Failed, PlaceOutcomes.Pending, PlaceOutcomes.Pending],
            kept.Operations.Select(each => each.Outcome));
        var commit = (await GitFixture.GitAsync(Work("ledger"), "rev-parse", "HEAD")).Trim();
        Assert.Equal(commit, kept.Commit);

        // The same press again: its folder holds exactly the empty commit its receipt names.
        var again = await Plan(stand, ask);
        Assert.True(again.Applies, string.Join("; ", again.Refusals.Select(r => r.Sentence)));
        Assert.Equal(first.Digest, again.Digest);
        Assert.Equal(first.Digest, again.Resumes);
        var resumed = await RepositoryCreates.ApplyAsync(stand.Doors(), ask, again.Digest);

        Assert.True(resumed.Done, resumed.Message);
        Assert.Equal(commit, (await GitFixture.GitAsync(Work("ledger"), "rev-parse", "HEAD")).Trim());
        Assert.Equal("1", (await GitFixture.GitAsync(Work("ledger"), "rev-list", "--count", "HEAD")).Trim());
        Assert.All(new CreateReceipts(Home).Read(first.Digest)!.Operations, each => Assert.Equal(PlaceOutcomes.Done, each.Outcome));
        Assert.Single(stand.Bodies.Where(body => (string)body["repository"]! == "ledger"));
        Assert.Single(stand.Setup.Published);
    }

    [Fact]
    public async Task A_resume_whose_registration_landed_with_its_answer_lost_finds_it_by_the_registry_and_registers_nothing_twice()
    {
        var stand = await MakeAsync(each => each.RegisterThenLose = true);
        var ask = Ask();
        var (first, stopped) = await Apply(stand, ask);
        Assert.False(stopped.Done);
        Assert.Equal(PlaceOutcomes.Uncertain, new CreateReceipts(Home).Read(first.Digest)!.Of(CreateOperations.Registration)!.Outcome);

        var again = await Plan(stand, ask);
        Assert.True(again.Applies, string.Join("; ", again.Refusals.Select(r => r.Sentence)));
        var resumed = await RepositoryCreates.ApplyAsync(stand.Doors(), ask, again.Digest);

        Assert.True(resumed.Done, resumed.Message);
        Assert.Single(stand.Bodies);
    }

    [Fact]
    public async Task A_re_press_on_a_folder_that_holds_more_than_the_receipts_empty_commit_is_refused_and_the_folder_is_never_deleted()
    {
        var stand = await MakeAsync(each => each.RegisterAnswers.Enqueue((false, "no")));
        var ask = Ask();
        await Apply(stand, ask);
        await GitFixture.GitAsync(Work("ledger"), "-c", "user.name=a", "-c", "user.email=a@b.test", "commit", "--allow-empty", "-q", "-m", "mine");

        var again = await Plan(stand, ask);

        Assert.False(again.Applies);
        Assert.Contains(Work("ledger"), Refused(again, CreateRefusals.FolderExists));
        Assert.True(Directory.Exists(Work("ledger", ".git")));
    }

    [Fact]
    public async Task A_re_press_on_an_unrelated_folder_with_a_receipt_of_no_such_commit_is_refused()
    {
        var stand = await MakeAsync(each => each.RegisterAnswers.Enqueue((false, "no")));
        var ask = Ask();
        await Apply(stand, ask);
        GitSeed.Remove(Work("ledger"));
        await GitSeed.PlainAsync(Work("ledger"), "ledger");

        var again = await Plan(stand, ask);

        Assert.False(again.Applies);
        Assert.NotEmpty(Refused(again, CreateRefusals.FolderExists));
    }

    [Fact]
    public async Task A_failed_history_is_a_failed_step_with_git_s_words_and_nothing_after_it_runs()
    {
        var stand = await MakeAsync();
        var ask = Ask();
        var plan = await Plan(stand, ask);
        WorkingTree.Answered.Value = args => args.Contains("commit") ? (1, "", "fatal: refused by a hook") : null;
        CreateResult result;
        try
        {
            result = await RepositoryCreates.ApplyAsync(stand.Doors(), ask, plan.Digest);
        }
        finally
        {
            WorkingTree.Answered.Value = null;
        }

        Assert.False(result.Done);
        Assert.Contains("refused by a hook", result.Message);
        Assert.Empty(stand.Bodies);
        Assert.True(Directory.Exists(Work("ledger")));
        var receipt = new CreateReceipts(Home).Read(plan.Digest)!;
        Assert.Equal(PlaceOutcomes.Failed, receipt.Of(CreateOperations.History)!.Outcome);
        Assert.Equal(PlaceOutcomes.Pending, receipt.Of(CreateOperations.Registration)!.Outcome);
    }

    // ---- the inherited workflow's remote step ----

    [Fact]
    public void A_graph_holding_a_push_step_or_a_plugins_pull_request_names_it_and_one_without_names_none()
    {
        var pushing = WorkflowDefaults.All.Select(each => NodeGraphs.Read(each.GraphJson).Graph!).First(graph => graph.Nodes.Any(node => node.Kind == NodeKinds.Push));
        var local = WorkflowDefaults.All.Select(each => NodeGraphs.Read(each.GraphJson).Graph!).First(graph => graph.Nodes.All(node => node.Kind != NodeKinds.Push));

        Assert.Equal(NodeKinds.Push, CreateRemoteSteps.In(pushing));
        Assert.Null(CreateRemoteSteps.In(local));
        Assert.Null(CreateRemoteSteps.In(null));
    }

    // ---- the terminal's words ----

    [Fact]
    public void The_terminal_reads_its_flags_and_says_what_is_wrong_with_the_rest()
    {
        var (words, problem) = RepositoryCreateCommand.Parse(
            ["ledger", "--kind", "folder", "--workspace", "aurora", "--under", "rel", "--line", "main", "--purpose", "keeps the books", "--work", "first entry", "--plan"], _scratch);

        Assert.Null(problem);
        Assert.True(words!.Plan);
        Assert.Equal(
            ("ledger", "keeps the books", PlaceKind.Folder, "aurora", Path.Combine(_scratch, "rel"), "main", "first entry"),
            (words.Ask.Name, words.Ask.Purpose, words.Ask.Kind, words.Ask.Workspace, words.Ask.Under, words.Ask.Line, words.Ask.Work));
        Assert.Equal(PlaceKind.Git, RepositoryCreateCommand.Parse(["ledger", "--purpose", "x"], _scratch).Words!.Ask.Kind);
        Assert.NotNull(RepositoryCreateCommand.Parse([], _scratch).Problem);
        Assert.Contains("--purpose", RepositoryCreateCommand.Parse(["ledger"], _scratch).Problem);
        Assert.Contains("`--bogus`", RepositoryCreateCommand.Parse(["ledger", "--purpose", "x", "--bogus"], _scratch).Problem);
        Assert.Contains("two names", RepositoryCreateCommand.Parse(["a", "b", "--purpose", "x"], _scratch).Problem);
    }

    [Fact]
    public async Task The_plan_twin_says_what_it_would_do_with_its_digest_and_says_nothing_was_written()
    {
        var stand = await MakeAsync();
        var output = new StringWriter { NewLine = "\n" };

        var code = await RepositoryCreateCommand.PlanAsync(Ask(), stand.Doors(), output);

        Assert.Equal(0, code);
        var said = output.ToString();
        Assert.Contains($"repository create: `ledger` in `aurora`", said);
        Assert.Contains(Work("ledger"), said);
        Assert.Contains("main", said);
        Assert.Contains("Fixture <fixture@example.test>", said);
        Assert.Contains("no remote", said);
        Assert.Contains("--plan: nothing was written, and nothing was asked.", said);
        Assert.False(Directory.Exists(Work("ledger")));
    }

    [Fact]
    public async Task A_refused_plan_is_said_in_the_terminals_words_with_exit_one()
    {
        var stand = await MakeAsync(each => each.Authors.Clear());
        var output = new StringWriter { NewLine = "\n" };

        var code = await RepositoryCreateCommand.PlanAsync(Ask(), stand.Doors(), output);

        Assert.Equal(1, code);
        Assert.Contains("refused:", output.ToString());
        Assert.Contains("daoris git use", output.ToString());
    }

    [Fact]
    public async Task The_terminals_apply_prints_the_plan_then_applies_that_plan_and_says_the_receipt()
    {
        var stand = await MakeAsync();
        var output = new StringWriter { NewLine = "\n" };

        var code = await RepositoryCreateCommand.ApplyAsync(Ask(), null, stand.Doors(), output);

        Assert.Equal(0, code);
        var said = output.ToString();
        Assert.Contains("repository create: folder made", said);
        Assert.Contains("  quest  #q-1 for `ledger`", said);
        Assert.Contains("  commit ", said);
        Assert.True(Directory.Exists(Work("ledger", ".git")));
    }

    /// <summary>The verb hands its result to the caller that asked for it, which is how the window's press says what it made.</summary>
    [Fact]
    public async Task The_terminals_apply_hands_its_result_to_the_caller_that_asked_for_it_and_the_receipt_says_what_was_made()
    {
        var stand = await MakeAsync();
        CreateResult? made = null;

        var code = await RepositoryCreateCommand.ApplyAsync(Ask(), null, stand.Doors(), new StringWriter(), made: result => made = result);

        Assert.Equal(0, code);
        var receipt = PressReceipt.Of(made!);
        Assert.Equal(("opened", "ledger", "aurora", Work("ledger"), "main", true), (receipt.Kind, receipt.Repository, receipt.Workspace, receipt.Folder, receipt.Line, receipt.Done));
        Assert.Equal(["q-1"], receipt.Quests);
    }

    /// <summary>The department's own place of work: the registry, the identities, the lines, the registration door and the set-up press's world.</summary>
    private sealed class Stand : ICreateWorld
    {
        public Stand(string home)
        {
            Home = home;
            Setup = new SetupStand { Registry = () => [.. Registry] };
        }

        public string Home { get; }

        public SetupStand Setup { get; }

        public List<RepoView> Registry { get; } = [];

        public Dictionary<string, CreateAuthor> Authors { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string> Lines { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, DateTime> Created { get; } = [];

        public List<JsonObject> Bodies { get; } = [];

        public Queue<(bool Ok, string Message)> RegisterAnswers { get; } = new();

        public bool RegisterThenLose { get; set; }

        public Action? OnRegister { get; set; }

        public string? RemoteStep { get; set; }

        public bool LocalService { get; set; } = true;

        public DriverConfig Config { get; set; } = DriverConfig.Empty with { Actors = StaffedRoles.On("claude-code-acp") };

        public int Changes { get; private set; }

        public CreateDoors Doors() => new(this, Setup, Home, true, () => Config, Change, new DateOnly(2026, 11, 11));

        private void Change(Func<DriverConfig, DriverConfig> change)
        {
            Config = change(Config);
            Changes++;
        }

        public Task<IReadOnlyList<RepoView>> RegistryAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<RepoView>>([.. Registry]);

        public DateTime CreatedAt(string root) => Created.TryGetValue(root, out var at) ? at : DateTime.UnixEpoch;

        public Task<string?> LineOfAsync(RepoView row, DriverConfig config, CancellationToken ct) => Task.FromResult(Lines.GetValueOrDefault(row.Repository));

        public Task<CreateAuthor?> AuthorOfAsync(RepoView row, CancellationToken ct) => Task.FromResult(Authors.TryGetValue(row.Repository, out var who) ? who : null);

        public string? RemoteStepOf(DriverConfig config, string repository, string workspace) => RemoteStep;

        public Task<(bool Ok, string Message)> RegisterAsync(JsonObject body, CancellationToken ct)
        {
            OnRegister?.Invoke();
            if (RegisterAnswers.TryDequeue(out var answer) && !answer.Ok) return Task.FromResult(answer);
            Bodies.Add((JsonObject)body.DeepClone());
            Registry.Add(new RepoView((string)body["repository"]!, (bool)body["adopted"]!, (string?)body["root"], (string?)body["workspace"] ?? RemoteTarget.DefaultWorkspace)
            {
                Kind = (string)body["kind"]!,
            });
            if (RegisterThenLose)
            {
                RegisterThenLose = false;
                throw new HttpRequestException("the answer was lost");
            }

            return Task.FromResult((true, ""));
        }
    }

    /// <summary>The set-up press's world: the registry as the stand's, a line with a README, the tools, and an ask door that keeps what it is asked.</summary>
    private sealed class SetupStand : ISetupWorld
    {
        private readonly LayoutFacts _facts = new LayoutReaderTests.Scratch().File("README.md", "# Ledger\n").Read();

        public Func<IReadOnlyList<RepoView>>? Registry { get; set; }

        public List<(string Workspace, string Sentence, string To)> Published { get; } = [];

        /// <summary>The step each publication carried behind its quest, in the order they were asked; null for none.</summary>
        public List<SetupThen?> Thens { get; } = [];

        public List<QuestView> Quests { get; } = [];

        public IsolateStep? IsolationOf(DriverConfig config, RepoView repository) => IsolateStep.Trees;

        public Task<IReadOnlyList<RepoView>> RegistryAsync(CancellationToken ct) => Task.FromResult(Registry?.Invoke() ?? []);

        public Task<LineReading> ReadLineAsync(RepoView repository, DriverConfig config, CancellationToken ct) => Task.FromResult(new LineReading(_facts, null));

        public Task<SetupTools> ToolsAsync(CancellationToken ct) => Task.FromResult(new SetupTools("node", "v22.11.0", null, "daoris", "0.0.1", null));

        public Task<DoctrineReading> StatusAsync(RepoView repository, CancellationToken ct) => Task.FromResult(new DoctrineReading(new DoctrineStatus { Synced = true }, null));

        public Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<QuestView>>([.. Quests]);

        public Task<IReadOnlyList<string>> EntriesAsync(string repository, CancellationToken ct) => Task.FromResult<IReadOnlyList<string>>([]);

        public RepositoryDescription? Describe(string root) => null;

        public Task<AskAnswer> PublishAsync(string workspace, string sentence, string to, SetupThen? then, CancellationToken ct)
        {
            Published.Add((workspace, sentence, to));
            Thens.Add(then);
            var id = $"q-{Published.Count}";
            Quests.Add(new QuestView(id, "work", to, sentence.Split('\n')[0], sentence[(sentence.IndexOf('\n') + 1)..], "Open"));
            return Task.FromResult(new AskAnswer(true, $"Asked as `#a{id}`.", $"a{id}", id));
        }
    }
}

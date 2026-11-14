using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTSPLACE1b2 (the Agents-place design §4 and §7 row 7): a place's recoverable apply. The receipt is written before the first effect, each
/// operation's outcome is kept, a stop at each boundary leaves a precise outcome that a press of the same digest goes on from, and the adoption
/// quest is asked once however many times an uncertain publication is retried. Fast: the world, the configuration's edit and the ask door are
/// stand-ins, and the receipts are files in a home of the test's own.
/// </summary>
public sealed class PlaceApplyTests : IDisposable
{
    private static readonly SetupPresetsTests.Catalog Plugins = new(new SetupPresetsTests.Entry("example.github-pull-request", [HookPoints.Perform]));

    private readonly string _home = Path.Combine(AppContext.BaseDirectory, "place-apply-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static PlaceAsk Adopt(params string[] repositories) => new(repositories, SetupPresets.Merge) { Adoption = true, Builds = SetupBuilds.Library };

    private Stand Make(Action<Stand>? shape = null)
    {
        var stand = new Stand(_home);
        shape?.Invoke(stand);
        return stand;
    }

    private static async Task<string> DigestOf(Stand stand, PlaceAsk ask) =>
        (await PlacePlans.PlanAsync(stand.Place, stand.Config, stand.Home, Plugins, true, ask)).Digest;

    private static Task<PlaceResult> Apply(Stand stand, PlaceAsk ask, string digest, params PlaceAccept[] accepted) =>
        PlaceApplies.ApplyAsync(stand.Doors(), ask, digest, accepted);

    [Fact]
    public async Task An_apply_writes_its_receipt_first_then_the_configuration_in_one_edit_then_asks_its_quest_once()
    {
        var stand = Make();
        var ask = Adopt("atlas");
        var digest = await DigestOf(stand, ask);
        stand.OnChange = () => Assert.Equal(
            (PlaceOutcomes.Uncertain, PlaceOutcomes.Pending), Outcomes(stand, digest, PlaceOperations.Configuration, PlaceOperations.AdoptionOf("atlas")));
        stand.OnPublish = () => Assert.Equal(
            (PlaceOutcomes.Done, PlaceOutcomes.Uncertain), Outcomes(stand, digest, PlaceOperations.Configuration, PlaceOperations.AdoptionOf("atlas")));

        var result = await Apply(stand, ask, digest);

        Assert.True(result.Done);
        Assert.Null(result.Refused);
        Assert.Equal("workflow `merge-after-accept` saved; configuration applied; adoption asked.", result.Message);
        Assert.Equal(1, stand.Changes);
        Assert.Equal("merge-after-accept", WorkflowSelection.Resolve(stand.Config, "atlas", "aurora", task: null).Workflow);
        var published = Assert.Single(stand.Setup.Published);
        Assert.EndsWith($"{PlaceApplies.KeyOf(digest)}\n", published.Sentence);
        Assert.Null(Assert.Single(stand.Setup.Thens));
        var kept = new PlaceReceipts(_home).Read(digest)!;
        Assert.All(kept.Operations, operation => Assert.Equal(PlaceOutcomes.Done, operation.Outcome));
        Assert.Equal("q-1", kept.Of(PlaceOperations.AdoptionOf("atlas"))!.Quest);
    }

    /// <summary>The copy is saved first as its own operation, the place then follows it, and a second press saves nothing again.</summary>
    [Fact]
    public async Task The_copy_is_saved_before_the_configuration_chooses_it_and_a_second_press_saves_nothing_again()
    {
        var stand = Make();
        var ask = new PlaceAsk(["atlas"], SetupPresets.Merge) { Look = false };
        var digest = await DigestOf(stand, ask);
        Assert.Null(WorkflowPorts.Store(_home).Load("merge-after-accept"));

        var result = await Apply(stand, ask, digest);

        Assert.True(result.Done, result.Message);
        Assert.Equal(PlaceOutcomes.Done, new PlaceReceipts(_home).Read(digest)!.Of(PlaceOperations.WorkflowSaveOf("merge-after-accept"))!.Outcome);
        var saved = WorkflowPorts.Store(_home).Load("merge-after-accept")!.Read.Versions[^1];
        Assert.Equal("merge-after-accept", saved.From!.Default);
        Assert.Equal("merge-after-accept", WorkflowSelection.Resolve(stand.Config, "atlas", "aurora", null).Workflow);

        var again = await Apply(stand, ask, digest);
        Assert.True(again.Done);
        Assert.Equal(1, WorkflowPorts.Store(_home).Load("merge-after-accept")!.Read.Versions.Count);
    }

    [Fact]
    public async Task A_press_of_an_applied_plan_again_changes_nothing_and_asks_no_second_quest()
    {
        var stand = Make();
        var ask = Adopt("atlas");
        var digest = await DigestOf(stand, ask);
        await Apply(stand, ask, digest);

        var again = await Apply(stand, ask, digest);

        Assert.True(again.Done);
        Assert.Equal(1, stand.Changes);
        Assert.Single(stand.Setup.Published);
    }

    [Fact]
    public async Task A_stop_at_the_configuration_leaves_it_uncertain_and_nothing_asked_and_a_press_goes_on_from_there()
    {
        var stand = Make(each => each.FailChange = new IOException("the file was held"));
        var ask = Adopt("atlas");
        var digest = await DigestOf(stand, ask);

        var stopped = await Apply(stand, ask, digest);

        Assert.False(stopped.Done);
        Assert.Equal("workflow `merge-after-accept` saved; configuration uncertain; adoption pending — the file was held", stopped.Message);
        Assert.Empty(stand.Setup.Published);
        Assert.Equal((PlaceOutcomes.Uncertain, PlaceOutcomes.Pending), Outcomes(stand, digest, PlaceOperations.Configuration, PlaceOperations.AdoptionOf("atlas")));

        // The restart reads the receipt: the edit did not land, so the step is pending again, never replayed blind.
        var settled = Assert.Single(await PlaceApplies.SettleAllAsync(stand.Doors()));
        Assert.Equal(PlaceOutcomes.Pending, settled.Of(PlaceOperations.Configuration)!.Outcome);
        Assert.Equal(0, stand.Changes);

        stand.FailChange = null;
        var resumed = await Apply(stand, ask, digest);
        Assert.True(resumed.Done);
        Assert.Single(stand.Setup.Published);
    }

    [Fact]
    public async Task A_stop_with_the_configuration_landed_is_settled_done_by_its_values()
    {
        var stand = Make(each => each.LandThenFail = true);
        var ask = Adopt("atlas");
        var digest = await DigestOf(stand, ask);

        var stopped = await Apply(stand, ask, digest);

        Assert.Equal((PlaceOutcomes.Uncertain, PlaceOutcomes.Pending), Outcomes(stand, digest, PlaceOperations.Configuration, PlaceOperations.AdoptionOf("atlas")));
        Assert.Empty(stand.Setup.Published);
        Assert.False(stopped.Done);

        var settled = Assert.Single(await PlaceApplies.SettleAllAsync(stand.Doors()));
        Assert.Equal(PlaceOutcomes.Done, settled.Of(PlaceOperations.Configuration)!.Outcome);
        Assert.Equal(PlaceOutcomes.Pending, settled.Of(PlaceOperations.AdoptionOf("atlas"))!.Outcome);
        Assert.Empty(stand.Setup.Published);

        var resumed = await Apply(stand, ask, digest);
        Assert.True(resumed.Done);
        Assert.Equal(1, stand.Changes);
        Assert.Single(stand.Setup.Published);
    }

    [Fact]
    public async Task Keep_how_it_lands_applies_driving_and_adoption_and_leaves_a_saved_workflow_and_its_landing_as_chosen()
    {
        var kept = IsolatedPlaces.In(_home);
        var stand = Make(each => each.Config = WorkflowSelection.Apply(each.Config, new WorkflowUse(kept, "atlas", null, null)));
        var ask = new PlaceAsk(["atlas"], SetupPresets.Keep) { Drive = true, Adoption = true, Builds = SetupBuilds.Library };
        var digest = await DigestOf(stand, ask);

        var result = await Apply(stand, ask, digest);

        Assert.True(result.Done);
        Assert.True(stand.Config.Drivable.Contains("atlas"));
        Assert.Equal(kept, WorkflowSelection.Resolve(stand.Config, "atlas", "aurora", task: null).Workflow);
        Assert.Single(stand.Setup.Published);
    }

    [Fact]
    public async Task A_restart_settles_a_kept_plans_configuration_by_its_values_as_it_does_any_plans()
    {
        var kept = IsolatedPlaces.In(_home);
        var stand = Make(each =>
        {
            each.Config = WorkflowSelection.Apply(each.Config, new WorkflowUse(kept, "atlas", null, null));
            each.LandThenFail = true;
        });
        var ask = new PlaceAsk(["atlas"], SetupPresets.Keep) { Drive = true, Adoption = true, Builds = SetupBuilds.Library };
        var digest = await DigestOf(stand, ask);

        await Apply(stand, ask, digest);
        var settled = Assert.Single(await PlaceApplies.SettleAllAsync(stand.Doors()));

        Assert.Equal(PlaceOutcomes.Done, settled.Of(PlaceOperations.Configuration)!.Outcome);
        Assert.Equal(PlaceOutcomes.Pending, settled.Of(PlaceOperations.AdoptionOf("atlas"))!.Outcome);
        Assert.Empty(stand.Setup.Published);
        Assert.True((await Apply(stand, ask, digest)).Done);
        Assert.Equal(1, stand.Changes);
    }

    [Fact]
    public async Task A_stop_at_the_publication_leaves_it_uncertain_and_a_retry_that_finds_the_quest_asks_no_second()
    {
        var stand = Make(each => each.Setup.PublishThenLose = true);
        var ask = Adopt("atlas");
        var digest = await DigestOf(stand, ask);

        var stopped = await Apply(stand, ask, digest);

        Assert.False(stopped.Done);
        Assert.Equal("workflow `merge-after-accept` saved; configuration applied; adoption uncertain — the answer was lost", stopped.Message);
        Assert.Equal((PlaceOutcomes.Done, PlaceOutcomes.Uncertain), Outcomes(stand, digest, PlaceOperations.Configuration, PlaceOperations.AdoptionOf("atlas")));
        Assert.Single(stand.Setup.Published);

        var retried = await Apply(stand, ask, digest);

        Assert.True(retried.Done);
        Assert.Single(stand.Setup.Published);
        Assert.Equal(1, stand.Changes);
        Assert.Equal("q-1", new PlaceReceipts(_home).Read(digest)!.Of(PlaceOperations.AdoptionOf("atlas"))!.Quest);
    }

    [Fact]
    public async Task A_stop_before_the_quest_exists_is_settled_pending_and_a_retry_asks_it_once()
    {
        var stand = Make(each => each.Setup.LoseBefore = true);
        var ask = Adopt("atlas");
        var digest = await DigestOf(stand, ask);

        await Apply(stand, ask, digest);
        Assert.Empty(stand.Setup.Published);
        var settled = Assert.Single(await PlaceApplies.SettleAllAsync(stand.Doors()));
        Assert.Equal(PlaceOutcomes.Pending, settled.Of(PlaceOperations.AdoptionOf("atlas"))!.Outcome);

        stand.Setup.LoseBefore = false;
        var retried = await Apply(stand, ask, digest);

        Assert.True(retried.Done);
        Assert.Single(stand.Setup.Published);
    }

    /// <summary>🔴 A restart reads every receipt: an uncertain publication whose quest is there by its key is done, with no ask made.</summary>
    [Fact]
    public async Task A_restart_settles_an_uncertain_publication_by_the_quest_found_by_its_key()
    {
        var stand = Make();
        var digest = new string('a', 64);
        new PlaceReceipts(_home).Write(new PlaceReceipt(digest, DateTimeOffset.UtcNow, Adopt("atlas"), [],
        [
            new PlaceOperation(PlaceOperations.AdoptionOf("atlas"), PlaceOperations.Adoption, "atlas", "a set-up", PlaceOutcomes.Uncertain),
        ]));
        stand.Setup.Quests.Add(new QuestView("q-9", "work", "atlas", "Set up", $"body\n\n{PlaceApplies.KeyOf(digest)}\n", "Open"));

        var settled = Assert.Single(await PlaceApplies.SettleAllAsync(stand.Doors()));

        var operation = settled.Of(PlaceOperations.AdoptionOf("atlas"))!;
        Assert.Equal((PlaceOutcomes.Done, "q-9"), (operation.Outcome, operation.Quest));
        Assert.Empty(stand.Setup.Published);
        Assert.Equal(PlaceOutcomes.Done, new PlaceReceipts(_home).Read(digest)!.Of(PlaceOperations.AdoptionOf("atlas"))!.Outcome);
    }

    /// <summary>🔴 No early start: where the configuration is not applied the quest is not asked, and its operation says why.</summary>
    [Fact]
    public async Task A_configuration_refused_inside_its_edit_writes_nothing_and_asks_nothing()
    {
        var stand = Make();
        var ask = Adopt("atlas");
        var digest = await DigestOf(stand, ask);
        stand.OnChange = () => stand.Config = WorkflowSelection.Apply(stand.Config, new WorkflowUse("merge-after-accept", "atlas", null, null));

        var result = await Apply(stand, ask, digest);

        Assert.False(result.Done);
        Assert.Equal(PlaceApplyRefusals.Moved, result.Refused);
        Assert.Empty(stand.Setup.Published);
        var kept = new PlaceReceipts(_home).Read(digest)!;
        Assert.Equal(PlaceOutcomes.Failed, kept.Of(PlaceOperations.Configuration)!.Outcome);
        Assert.Equal(PlaceOutcomes.Failed, kept.Of(PlaceOperations.AdoptionOf("atlas"))!.Outcome);
        Assert.Equal("workflow `merge-after-accept` saved; configuration not applied; adoption refused", result.Message[..result.Message.IndexOf(" —", StringComparison.Ordinal)]);
    }

    [Fact]
    public async Task A_plan_that_moved_before_the_apply_is_refused_moved_with_no_receipt_and_no_effect()
    {
        var stand = Make();
        var ask = Adopt("atlas");
        var digest = await DigestOf(stand, ask);
        stand.Config = stand.Config.WithDrivable("atlas", true);

        var result = await Apply(stand, ask, digest);

        Assert.Equal(PlaceApplyRefusals.Moved, result.Refused);
        Assert.Equal(0, stand.Changes);
        Assert.Empty(stand.Setup.Published);
        Assert.Null(new PlaceReceipts(_home).Read(digest));
    }

    /// <summary>The chosen blob is held against the tip read again, after the plan's own read: a declaration that moved between is refused.</summary>
    [Fact]
    public async Task A_chosen_blob_that_is_no_longer_the_tips_is_refused_moved()
    {
        var stand = Make();
        var ask = new PlaceAsk(["engine"], SetupPresets.Integrated);
        var digest = await DigestOf(stand, ask);
        stand.Place.TipAfter = (name, reads) => name == "engine" && reads >= 3 ? stand.Place.Inner.Tips[name] with { Gates = new string('c', 40) } : null;

        var result = await Apply(stand, ask, digest, new PlaceAccept("engine"));

        Assert.Equal(PlaceApplyRefusals.Moved, result.Refused);
        Assert.Contains("another declaration", result.Message);
        Assert.Equal(0, stand.Changes);
        Assert.Null(new PlaceReceipts(_home).Read(digest));
    }

    [Fact]
    public async Task A_chosen_acceptance_is_written_with_the_configuration_in_the_same_edit()
    {
        var stand = Make();
        var ask = new PlaceAsk(["engine"], SetupPresets.Integrated);
        var digest = await DigestOf(stand, ask);

        var result = await Apply(stand, ask, digest, new PlaceAccept("engine"));

        Assert.True(result.Done);
        Assert.Equal(1, stand.Changes);
        Assert.Equal(Blob, IntegrationAcceptance.Of(stand.Config, "engine"));
        Assert.Equal(new SetupLand("integrate", "you", null, "owner"), PlacePlans.SideOf(stand.Config, "engine", "aurora", WorkflowPorts.Store(_home)).Land);
        Assert.Equal(Blob, new PlaceReceipts(_home).Read(digest)!.Accepted.Single().Blob);
    }

    /// <summary>
    /// One acceptance serves every purpose, so naming a repository twice keeps it once, and the plan says it in the checks' words.
    /// </summary>
    [Fact]
    public async Task One_acceptance_per_repository_is_kept_once_however_often_it_is_named()
    {
        var stand = Make();
        var ask = new PlaceAsk(["engine"], SetupPresets.Integrated);
        var digest = await DigestOf(stand, ask);

        var result = await Apply(stand, ask, digest, new PlaceAccept("engine"), new PlaceAccept("Engine"));

        Assert.True(result.Done);
        Assert.Equal(Blob, IntegrationAcceptance.Of(stand.Config, "engine"));
        Assert.Equal(["engine"], new PlaceReceipts(_home).Read(digest)!.Accepted.Select(each => each.Repository));
    }

    /// <summary>
    /// AGENTSPLACE1b3: <c>place … --apply</c> alone prints the plan it applies, applies it with the acceptances the words named, and says the
    /// outcome; the same words the running Daoris's route says for a terminal (<c>said</c>).
    /// </summary>
    [Fact]
    public async Task The_verb_applies_alone_printing_the_plan_then_the_outcome_with_only_the_acceptances_it_names()
    {
        var stand = Make();
        var output = new StringWriter();

        var code = await PlaceCommand.RunAsync(
            ["engine", "--preset", SetupPresets.Integrated, "--apply", "--accept", "engine"], output, stand.Place, stand.Config, _home, Plugins,
            true, instance: null, apply: stand.Doors());

        var lines = output.ToString().ReplaceLineEndings("\n").TrimEnd('\n').Split('\n');
        Assert.Equal(0, code);
        Assert.StartsWith("place: `engine` — ", lines[0], StringComparison.Ordinal);
        Assert.DoesNotContain(lines, line => line.StartsWith("--plan:", StringComparison.Ordinal));
        Assert.Equal("place: workflow `integrated` saved; configuration applied.", lines[^1]);
        Assert.Equal(Blob, IntegrationAcceptance.Of(stand.Config, "engine"));
        Assert.Equal(1, stand.Changes);

        var none = Make();
        var bare = new StringWriter();
        Assert.Equal(0, await PlaceCommand.RunAsync(
            ["engine", "--preset", SetupPresets.Integrated, "--apply"], bare, none.Place, none.Config, _home, Plugins, true, instance: null, apply: none.Doors()));
        Assert.Null(IntegrationAcceptance.Of(none.Config, "engine"));
    }

    [Fact]
    public async Task An_apply_saves_the_pull_request_copy_with_the_plugin_the_plan_found()
    {
        var stand = Make();
        var ask = new PlaceAsk(["web"], SetupPresets.PullRequest);
        var digest = await DigestOf(stand, ask);

        var result = await Apply(stand, ask, digest);

        Assert.True(result.Done, result.Message);
        var saved = WorkflowPorts.Store(_home).Load("pull-request")!.Read.Versions[^1].Graph!;
        Assert.Equal(["example.github-pull-request/pull-request"], saved.Nodes.Select(node => node.Kind).Where(kind => kind.EndsWith("/pull-request", StringComparison.Ordinal)));
        Assert.Equal("example.github-pull-request", new PlaceReceipts(_home).Read(digest)!.Of(PlaceOperations.WorkflowSaveOf("pull-request"))!.Plugin);
    }

    [Fact]
    public async Task The_verb_applying_a_refused_plan_says_why_exits_1_and_writes_nothing()
    {
        var stand = Make();
        var output = new StringWriter();

        var code = await PlaceCommand.RunAsync(
            ["docs", "--preset", SetupPresets.PullRequest, "--apply"], output, stand.Place, stand.Config, _home, Plugins, true, instance: null, apply: stand.Doors());

        Assert.Equal(1, code);
        Assert.Contains("refused:", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("the plan refuses, so nothing was written", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, stand.Changes);
    }

    /// <summary>From a session's terminal the apply, acting alone, is refused and writes nothing, as every other person's press is.</summary>
    [Fact]
    public async Task The_verb_applying_alone_from_a_sessions_terminal_is_refused_and_writes_nothing()
    {
        var stand = Make();
        var output = new StringWriter();
        var terminal = new InstanceWorld
        {
            Home = _home, Environment = name => name == "DAORIS_SESSION_ID" ? "s1a2b3c4" : null, Out = output, Error = output, Verb = "place",
        };

        var code = await PlaceCommand.RunAsync(
            ["engine", "--preset", SetupPresets.Integrated, "--apply", "--accept", "engine"], output, stand.Place, stand.Config, _home, Plugins,
            true, terminal, apply: stand.Doors());

        Assert.Equal(1, code);
        Assert.Contains("`DAORIS_SESSION_ID`", output.ToString(), StringComparison.Ordinal);
        Assert.Null(IntegrationAcceptance.Of(stand.Config, "engine"));
        Assert.Equal(0, stand.Changes);
    }

    /// <summary>Words that are the usage: both of `--plan` and `--apply`, neither, an acceptance without an apply and a purpose that is none.</summary>
    [Theory]
    [InlineData("engine --preset merge-after-accept --plan --apply")]
    [InlineData("engine --preset merge-after-accept")]
    [InlineData("engine --preset merge-after-accept --plan --accept engine")]
    [InlineData("engine --preset merge-after-accept --apply --accept engine:integration")]
    [InlineData("engine --preset merge-after-accept --apply --accept")]
    public async Task Words_the_verb_does_not_take_are_its_usage_exit_2_before_anything_is_planned(string words)
    {
        var stand = Make();
        var output = new StringWriter();

        var code = await PlaceCommand.RunAsync(words.Split(' '), output, stand.Place, stand.Config, _home, Plugins, true, instance: null, apply: stand.Doors());

        Assert.Equal(2, code);
        Assert.Contains(PlaceCommand.Usage, output.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, stand.Changes);
    }

    [Fact]
    public void An_apply_names_each_repository_whose_declaration_it_accepts_and_hands_the_same_words_back_to_the_verb()
    {
        var (words, problem) = PlaceCommand.Parse(["a", "b", "--preset", SetupPresets.Integrated, "--apply", "--accept", "a,b"]);

        Assert.Null(problem);
        Assert.True(words!.Apply);
        Assert.Equal(["a", "b"], words.Accept.Select(each => each.Repository));
        Assert.Equal(words, PlaceCommand.Parse(PlaceCommand.ApplyArgsOf(words.Ask, words.Accept)).Words, new WordsComparer());
    }

    private sealed class WordsComparer : IEqualityComparer<PlaceWords?>
    {
        public bool Equals(PlaceWords? x, PlaceWords? y) =>
            x is not null && y is not null && x.Apply == y.Apply && x.Ask.Preset == y.Ask.Preset && x.Ask.Repositories.SequenceEqual(y.Ask.Repositories)
            && x.Accept.Select(each => each.Repository).SequenceEqual(y.Accept.Select(each => each.Repository));

        public int GetHashCode(PlaceWords? words) => 0;
    }

    [Fact]
    public async Task An_acceptance_the_plan_does_not_offer_a_refused_plan_and_a_word_that_is_no_digest_are_refused_with_nothing_written()
    {
        var stand = Make();
        var ask = Adopt("atlas");
        var digest = await DigestOf(stand, ask);
        var docs = new PlaceAsk(["docs"], SetupPresets.PullRequest);

        var notOffered = await Apply(stand, ask, digest, new PlaceAccept("atlas"));
        var refused = await Apply(stand, docs, await DigestOf(stand, docs));
        var word = await Apply(stand, ask, "not-a-digest");

        Assert.Equal(PlaceApplyRefusals.NotOffered, notOffered.Refused);
        Assert.Equal(PlaceApplyRefusals.Refused, refused.Refused);
        Assert.Equal(PlaceApplyRefusals.Digest, word.Refused);
        Assert.Equal(0, stand.Changes);
        Assert.Empty(stand.Setup.Published);
        var folder = Path.GetDirectoryName(new PlaceReceipts(_home).PathOf(digest))!;
        Assert.False(Directory.Exists(folder) && Directory.EnumerateFiles(folder).Any());
    }

    [Fact]
    public async Task A_receipt_that_does_not_read_is_never_replaced_and_its_apply_is_refused()
    {
        var stand = Make();
        var ask = Adopt("atlas");
        var digest = await DigestOf(stand, ask);
        var path = new PlaceReceipts(_home).PathOf(digest);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ not json");

        var result = await Apply(stand, ask, digest);

        Assert.Equal(PlaceApplyRefusals.Receipt, result.Refused);
        Assert.Equal("{ not json", File.ReadAllText(path));
        Assert.Equal(0, stand.Changes);
    }

    [Fact]
    public async Task The_digest_is_the_plans_own_and_moves_with_what_the_plan_changes()
    {
        var stand = Make();
        var one = await DigestOf(stand, Adopt("atlas"));
        var same = await DigestOf(stand, Adopt("atlas"));
        var other = await DigestOf(stand, new PlaceAsk(["atlas"], SetupPresets.Merge));
        var moved = (await PlacePlans.PlanAsync(stand.Place, stand.Config.WithDrivable("atlas", true), _home, Plugins, true, Adopt("atlas"))).Digest;

        Assert.Equal(one, same);
        Assert.True(PlaceDigests.Is(one));
        Assert.Equal(3, new[] { one, other, moved }.Distinct().Count());
    }

    /// <summary>
    /// An adoption covering more of a workspace's repositories than it asks at once writes that workspace's plan (its rule added
    /// once) and asks nothing itself; the driver's tick publishes it. A second press of the digest writes and asks nothing again.
    /// </summary>
    [Fact]
    public async Task An_adoption_of_more_repositories_than_it_asks_at_once_writes_the_workspaces_plan_and_asks_nothing()
    {
        var stand = Make();
        var ask = Adopt("atlas", "engine", "docs") with { AtOnce = 1, Pilot = 0 };
        var digest = await DigestOf(stand, ask);

        var result = await Apply(stand, ask, digest);

        Assert.True(result.Done, result.Message);
        Assert.Empty(stand.Setup.Published);
        var plan = WorkspaceSetupFile.Load(_home, "aurora").Plan!;
        Assert.Equal(["atlas", "engine", "docs"], plan.Order);
        Assert.Equal((1, 0, true, null), (plan.AtOnce, plan.Pilot, plan.Live, plan.Paused));
        Assert.Equal(SetupPressRules.Rules, PermissionRules.Load(_home).Workspaces["aurora"].Allow);
        var adoptions = result.Operations.Where(each => each.Kind == PlaceOperations.Adoption).ToList();
        Assert.Equal(3, adoptions.Count);
        Assert.All(adoptions, each => Assert.StartsWith("paced: workspace `aurora`'s plan asks it with 2 others, 1 at a time", each.Note));

        var again = await Apply(stand, ask, digest);

        Assert.True(again.Done);
        Assert.Empty(stand.Setup.Published);
        Assert.Equal(plan.Created, WorkspaceSetupFile.Load(_home, "aurora").Plan!.Created);
    }

    /// <summary>An adoption that says no pace is one at a time, the plan's default: two repositories of a workspace are a plan, not two quests.</summary>
    [Fact]
    public async Task An_adoption_that_says_no_pace_of_two_repositories_of_a_workspace_is_paced_one_at_a_time()
    {
        var stand = Make();
        var ask = Adopt("atlas", "engine");
        var digest = await DigestOf(stand, ask);

        var result = await Apply(stand, ask, digest);

        Assert.True(result.Done, result.Message);
        Assert.Empty(stand.Setup.Published);
        var plan = WorkspaceSetupFile.Load(_home, "aurora").Plan!;
        Assert.Equal((1, WorkspaceSetupRules.DefaultPilot), (plan.AtOnce, plan.Pilot));
    }

    [Fact]
    public async Task An_adoption_of_no_more_than_it_asks_at_once_is_asked_now_as_a_single_adoption_is()
    {
        var stand = Make(each => each.Config = each.Config with { Cap = 3 });
        var ask = Adopt("atlas") with { AtOnce = 2 };
        var digest = await DigestOf(stand, ask);

        var result = await Apply(stand, ask, digest);

        Assert.True(result.Done, result.Message);
        Assert.Single(stand.Setup.Published);
        Assert.Null(WorkspaceSetupFile.Load(_home, "aurora").Plan);
    }

    [Theory]
    [InlineData(0, "at-once")]
    [InlineData(9, "at-once")]
    public async Task A_pace_the_cap_does_not_leave_is_refused_and_nothing_is_written(int atOnce, string code)
    {
        var stand = Make();
        var ask = Adopt("atlas", "engine") with { AtOnce = atOnce };

        var plan = await PlacePlans.PlanAsync(stand.Place, stand.Config, stand.Home, Plugins, true, ask);

        Assert.Contains(plan.Refusals, each => each.Code == code);
        Assert.False(plan.Applies);
    }

    [Fact]
    public async Task Pacing_without_adoption_is_refused_and_the_digest_moves_with_it()
    {
        var stand = Make();
        var plain = new PlaceAsk(["atlas"], SetupPresets.Merge);
        var paced = plain with { AtOnce = 1 };

        var plan = await PlacePlans.PlanAsync(stand.Place, stand.Config, stand.Home, Plugins, true, paced);

        Assert.Contains(plan.Refusals, each => each.Code == "pacing");
        Assert.NotEqual(await DigestOf(stand, plain), plan.Digest);
        Assert.Equal(await DigestOf(stand, Adopt("atlas")), await DigestOf(stand, Adopt("atlas") with { AtOnce = null }));
    }

    private const string Blob = "fedcba9876543210fedcba9876543210fedcba98";

    /// <summary>The outcomes the receipt on disk holds now for two operations.</summary>
    private (string, string) Outcomes(Stand stand, string digest, string first, string second)
    {
        var receipt = new PlaceReceipts(_home).Read(digest)!;
        return (receipt.Of(first)!.Outcome, receipt.Of(second)!.Outcome);
    }

    /// <summary>The apply's world as a stand-in: the plan's own world, a configuration held in memory, and the set-up press's world.</summary>
    private sealed class Stand
    {
        public Stand(string home)
        {
            Home = home;
            Place = new Counting(new PlacePlanTests.World());
            Setup = new SetupWorld();
        }

        public string Home { get; }

        public Counting Place { get; }

        public SetupWorld Setup { get; }

        public DriverConfig Config { get; set; } = (DriverConfig.Empty with { Actors = StaffedRoles.On("claude-code-acp") })
            .WithDrivable("engine", true)
            .WithDrivable("docs", true);

        public int Changes { get; private set; }

        public Action? OnChange { get; set; }

        public Action? OnPublish { get => Setup.OnPublish; set => Setup.OnPublish = value; }

        public Exception? FailChange { get; set; }

        public bool LandThenFail { get; set; }

        public PlaceApplyDoors Doors() => new(Place, Setup, Home, Plugins, true, () => Config, Change, new DateOnly(2026, 11, 1));

        private void Change(Func<DriverConfig, DriverConfig> change)
        {
            OnChange?.Invoke();
            if (FailChange is not null) throw FailChange;
            Config = change(Config);
            Changes++;
            if (LandThenFail)
            {
                LandThenFail = false;
                throw new IOException("the answer was lost");
            }
        }
    }

    /// <summary>The plan's world, counting each tip's reads so a tip can move between two of them.</summary>
    private sealed class Counting(PlacePlanTests.World inner) : IPlaceWorld
    {
        private readonly Dictionary<string, int> _reads = new(StringComparer.OrdinalIgnoreCase);

        public PlacePlanTests.World Inner { get; } = inner;

        public Func<string, int, PlaceTip?>? TipAfter { get; set; }

        public Task<IReadOnlyList<RepoView>> RegistryAsync(CancellationToken ct) => Inner.RegistryAsync(ct);

        public Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct) => Inner.QuestsAsync(ct);

        public async Task<PlaceTip> TipAsync(RepoView repository, DriverConfig config, CancellationToken ct)
        {
            _reads[repository.Repository] = _reads.GetValueOrDefault(repository.Repository) + 1;
            return TipAfter?.Invoke(repository.Repository, _reads[repository.Repository]) ?? await Inner.TipAsync(repository, config, ct);
        }

        public Task<PlaceRoomRead> RoomAsync(RepoView repository, DriverConfig config, string path, CancellationToken ct) => Inner.RoomAsync(repository, config, path, ct);
    }

    /// <summary>The set-up press's world: its registry, line, tools and quests, and an ask door that keeps what it is asked.</summary>
    private sealed class SetupWorld : ISetupWorld
    {
        /// <summary>A landing preset's workflow sets its sessions apart, as its plan says; the press reads the same.</summary>
        public IsolateStep? IsolationOf(DriverConfig config, RepoView repository) => IsolateStep.Trees;

        private readonly LayoutFacts _facts = new LayoutReaderTests.Scratch().File("README.md", "# Atlas\n").Read();

        public List<RepoView> Registry { get; } = [new("atlas", false, "/fixture/atlas", "aurora"), new("engine", true, "/fixture/engine", "aurora")];

        public List<QuestView> Quests { get; } = [];

        public List<(string Workspace, string Sentence, string To)> Published { get; } = [];

        public List<SetupThen?> Thens { get; } = [];

        public Action? OnPublish { get; set; }

        /// <summary>The ask reaches the service and its answer is lost.</summary>
        public bool PublishThenLose { get; set; }

        /// <summary>The ask never reaches the service.</summary>
        public bool LoseBefore { get; set; }

        public Task<IReadOnlyList<RepoView>> RegistryAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<RepoView>>(Registry);

        public Task<LineReading> ReadLineAsync(RepoView repository, DriverConfig config, CancellationToken ct) => Task.FromResult(new LineReading(_facts, null));

        public Task<SetupTools> ToolsAsync(CancellationToken ct) => Task.FromResult(new SetupTools("node", "v22.11.0", null, "daoris", "0.0.1", null));

        public Task<DoctrineReading> StatusAsync(RepoView repository, CancellationToken ct) =>
            Task.FromResult(new DoctrineReading(new DoctrineStatus { Synced = true }, null));

        public Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<QuestView>>([.. Quests]);

        public Task<IReadOnlyList<string>> EntriesAsync(string repository, CancellationToken ct) => Task.FromResult<IReadOnlyList<string>>([]);

        public RepositoryDescription? Describe(string root) => null;

        public Task<AskAnswer> PublishAsync(string workspace, string sentence, string to, SetupThen? then, CancellationToken ct)
        {
            OnPublish?.Invoke();
            if (LoseBefore) throw new HttpRequestException("the service did not answer");
            Published.Add((workspace, sentence, to));
            Thens.Add(then);
            var id = $"q-{Published.Count}";
            Quests.Add(new QuestView(id, "work", to, sentence.Split('\n')[0], sentence[(sentence.IndexOf('\n') + 1)..], "Open"));
            if (PublishThenLose) throw new HttpRequestException("the answer was lost");
            return Task.FromResult(new AskAnswer(true, $"Asked as `#a{id}`.", $"a{id}", id));
        }
    }
}

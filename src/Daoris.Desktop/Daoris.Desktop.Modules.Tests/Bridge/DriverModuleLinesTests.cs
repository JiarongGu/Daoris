using System.Text.Json;
using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Each repository's line, its landing rule and the clean-up over the bridge (`LinesRoutes.cs`).
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class DriverModuleLinesTests : DriverModuleBridge
{
    /// <summary>
    /// A repository's line: the screen's half of `daoris place line`, over the same file. The
    /// names ride as rows, not as an object's keys, so no key policy on the bridge can respell one.
    /// </summary>
    [Fact]
    public async Task Setting_a_line_writes_the_same_file_the_terminal_edits()
    {
        var module = Module();
        await AnswerAsync(module, "SET_LINE", new { workspace = "aurora", branch = "develop" });
        var state = await AnswerAsync(module, "SET_LINE", new { repository = "Engine", branch = "release/2026.09" });

        var config = DriverConfig.Load(DriverConfigPath);
        Assert.Equal("release/2026.09", config.Lines["Engine"]);
        Assert.Equal("develop", config.WorkspaceLines["aurora"]);
        Assert.Equal("Engine", state.GetProperty("lines")[0].GetProperty("repository").GetString());
        Assert.Equal("aurora", state.GetProperty("workspaceLines")[0].GetProperty("workspace").GetString());

        var cleared = await AnswerAsync(module, "SET_LINE", new { repository = "engine" });
        Assert.Equal(0, cleared.GetProperty("lines").GetArrayLength());
    }

    [Fact]
    public async Task A_line_git_would_not_take_or_one_with_no_owner_is_refused_in_a_sentence()
    {
        var bad = await RefusalAsync(Module(), "SET_LINE", new { repository = "engine", branch = "a..b" });
        Assert.Contains("not a branch name git would take", bad);

        var neither = await RefusalAsync(Module(), "SET_LINE", new { branch = "develop" });
        Assert.Contains("a `repository` or a `workspace`", neither);
        Assert.False(File.Exists(DriverConfigPath) && DriverConfig.Load(DriverConfigPath).Lines.Count > 0);
    }

    private static JsonElement Body(string json) => JsonDocument.Parse(json).RootElement.Clone();

    /// <summary>Each line row carries the policy that reaches it, its own else its workspace's, and nothing where none is set.</summary>
    [Fact]
    public async Task The_lines_answer_each_lines_policy()
    {
        DriverConfig.Empty
            .WithWorkspaceLinePolicy("aurora", new LandingIntegrate(Order: LandingIntegrate.Arrival))
            .WithLinePolicy("game", new LandingIntegrate(Batch: 4))
            .Save(DriverConfigPath);
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new SessionChanges(new StandInService()))));

        var lines = (await AnswerAsync(module, "LINES")).GetProperty("lines").EnumerateArray().ToList();

        Assert.Equal(4, lines.Single(row => row.GetProperty("repository").GetString() == "game").GetProperty("policy").GetProperty("batch").GetInt32());
        Assert.Equal("arrival", lines.Single(row => row.GetProperty("repository").GetString() == "engine").GetProperty("policy").GetProperty("order").GetString());
    }

    /// <summary>The clean-up reads the registry's checkouts and the sessions in use, so before the driver is up it is the cold-start sentence.</summary>
    [Fact]
    public async Task The_clean_up_before_the_driver_is_up_is_a_sentence()
    {
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "SWEEP_PLAN"));
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "SWEEP", new { only = new[] { "engine:daoris/s-x" } }));
    }

    /// <summary>
    /// A clean-up row says whether its discard is offered beside it, by the driver's own rule
    /// (<see cref="SessionBranchDoors.RemovalOffered"/>, the line `trees clean` prints beside such a row), so the page and the
    /// terminal offer it beside the same rows: a failed or superseded attempt's commits, which no clean-up takes.
    /// </summary>
    [Fact]
    public void A_clean_up_row_says_whether_its_discard_is_offered_beside_it_as_the_terminal_s_list_does()
    {
        var camel = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        SweepItem[] items =
        [
            new("engine", "aurora", "daoris/s-1a2b3c4d", null, SweepKind.Unlanded, 2, null, "a1b2c3d the work"),
            new("engine", "aurora", "daoris/s-2b3c4d5e", "tree", SweepKind.Unlanded, 1, null, "e4f5a6b more work"),
            new("engine", "aurora", "daoris/s-3c4d5e6f", null, SweepKind.Unlanded, 0, null, "git could not tell"),
            new("engine", "aurora", "daoris/s-4d5e6f7a", null, SweepKind.Empty, 0, "main", null),
            new("engine", "aurora", "daoris/s-5e6f7a8b", null, SweepKind.Landed, 3, "feature/x", null),
            new("engine", "aurora", "daoris/s-6f7a8b9c", "tree", SweepKind.Dirty, 0, null, "2 path(s)"),
            new("engine", "aurora", "daoris/s-7a8b9c0d", "tree", SweepKind.InUse, 0, null, null),
        ];

        var offered = items.Select(item => JsonSerializer.SerializeToElement(LinesRoutes.SweepRow(item), camel).GetProperty("discardable").GetBoolean()).ToArray();

        Assert.Equal([true, true, false, false, false, false, false], offered);
        Assert.Equal(items.Select(item => SessionBranchDoors.RemovalOffered(item) is not null), offered);
    }

    /// <summary>
    /// A clean-up row names the session its tree is, by id and how it ended, and says whether its landing is offered,
    /// by the driver's own rule (<see cref="SessionBranchDoors.LandingOffered"/>, the line `trees clean` prints beside it): a tree
    /// still here holding commits no branch of the person's holds, whatever that session's ending. The page's Branches tab says
    /// the same words; a row whose tree is gone, or with no session named, offers none.
    /// </summary>
    [Fact]
    public void A_clean_up_row_names_its_session_and_whether_its_landing_is_offered()
    {
        var camel = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var failed = new SessionRecord("4e6837ed", "engine", "failed") { Tree = "tree" };
        var here = new SweepItem("engine", "aurora", "daoris/s-4e6837ed", "tree", SweepKind.Unlanded, 1, null, "a1b2c3d the work");
        var gone = here with { Tree = null };

        var row = JsonSerializer.SerializeToElement(LinesRoutes.SweepRow(here, failed), camel);
        var treeless = JsonSerializer.SerializeToElement(LinesRoutes.SweepRow(gone, null), camel);
        var unnamed = JsonSerializer.SerializeToElement(LinesRoutes.SweepRow(here), camel);

        Assert.Equal(("4e6837ed", "failed"), (row.GetProperty("session").GetProperty("id").GetString(), row.GetProperty("session").GetProperty("state").GetString()));
        Assert.True(row.GetProperty("landable").GetBoolean());
        Assert.Equal(JsonValueKind.Null, treeless.GetProperty("session").ValueKind);
        Assert.False(treeless.GetProperty("landable").GetBoolean());
        Assert.False(unnamed.GetProperty("landable").GetBoolean());
    }

    /// <summary>
    /// A plan's replay row carries how many commits the line already holds, which the replay
    /// leaves out beside the ones it carries, so the Updates list says it as `trees sync` does; a row with none left out says 0.
    /// The list's row and the press's are the same projection.
    /// </summary>
    [Fact]
    public void A_plans_replay_row_carries_what_it_leaves_out_that_the_line_holds()
    {
        var camel = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var item = new RebaseItem("engine", "aurora", "daoris/s-3c4d5e6f", false, RebaseKind.Replay, "main", "abcd1234", CutBy.Line, null, 2, null);

        var row = JsonSerializer.SerializeToElement(LinesRoutes.RebaseRow(item with { LeftOut = 1 }), camel);
        var none = JsonSerializer.SerializeToElement(LinesRoutes.RebaseRow(item), camel);

        Assert.Equal((2, 1, true), (row.GetProperty("commits").GetInt32(), row.GetProperty("leftOut").GetInt32(), row.GetProperty("replays").GetBoolean()));
        Assert.Equal(0, none.GetProperty("leftOut").GetInt32());
    }

    /// <summary>
    /// A failed or superseded attempt's branch discarded from the page is the terminal's
    /// `trees remove &lt;branch&gt; --repository &lt;name&gt; --force`: it reads the registry's checkout and the sessions in
    /// use, so before the driver is up it is the cold-start sentence, and a discard that names no branch or no repository
    /// is a malformed call.
    /// </summary>
    [Fact]
    public async Task Discarding_a_session_branch_before_the_driver_is_up_is_a_sentence()
    {
        var refusal = await RefusalAsync(Module(), "DISCARD_SESSION_BRANCH",
            new { repository = "engine", branch = "daoris/s-1a2b3c4d", force = true });

        Assert.Contains(Refusals.DriverNotReady, refusal);
        Assert.Contains("still coming up", refusal);
        await Assert.ThrowsAnyAsync<Exception>(() => AnswerAsync(Module(), "DISCARD_SESSION_BRANCH", new { repository = "engine" }));
        await Assert.ThrowsAnyAsync<Exception>(() => AnswerAsync(Module(), "DISCARD_SESSION_BRANCH", new { branch = "daoris/s-1a2b3c4d" }));
    }

    /// <summary>
    /// Withdrawing a landing's branch: before the driver is up it is the cold-start sentence, and a call naming no branch or no
    /// repository is malformed.
    /// </summary>
    [Fact]
    public async Task Withdrawing_a_landing_branch_before_the_driver_is_up_is_a_sentence()
    {
        var refusal = await RefusalAsync(Module(), "WITHDRAW_LANDING_BRANCH",
            new { repository = "engine", branch = "quest/row-1", why = "landed by hand" });

        Assert.Contains(Refusals.DriverNotReady, refusal);
        Assert.Contains("still coming up", refusal);
        await Assert.ThrowsAnyAsync<Exception>(() => AnswerAsync(Module(), "WITHDRAW_LANDING_BRANCH", new { repository = "engine" }));
        await Assert.ThrowsAnyAsync<Exception>(() => AnswerAsync(Module(), "WITHDRAW_LANDING_BRANCH", new { branch = "quest/row-1" }));
    }

    /// <summary>
    /// A withdrawal is an answer, never a throw: a process that does not hold the home's lease deletes nothing and says so before
    /// the checkout is asked, and a plan, which changes nothing, goes on to the checkout, which this repository has none of here.
    /// </summary>
    [Fact]
    public async Task A_withdrawal_is_refused_without_the_home_and_a_plan_asks_the_checkout_and_changes_nothing()
    {
        var service = new DiscardService(root: null);
        var module = await ModuleOverAsync(service);

        var press = await AnswerAsync(module, "WITHDRAW_LANDING_BRANCH", new { repository = "engine", branch = "quest/row-1", why = "landed by hand" });
        Assert.False(press.GetProperty("done").GetBoolean());
        Assert.Equal(RecoveryDropping.WithoutHome(Home), press.GetProperty("message").GetString());
        Assert.DoesNotContain("/api/registry", service.Asked);

        var plan = await AnswerAsync(module, "WITHDRAW_LANDING_BRANCH", new { repository = "engine", branch = "quest/row-1", plan = true });
        Assert.False(plan.GetProperty("done").GetBoolean());
        Assert.Equal("`engine` has no checkout here, so its branch `quest/row-1` cannot be withdrawn from this machine.", plan.GetProperty("message").GetString());
        Assert.Contains("/api/registry", service.Asked);
    }

    /// <summary>
    /// The screen's withdrawal composes none of the driver's pieces itself, so it cannot part from the terminal's door: read from the
    /// modules' sources, the judgement, the keep, the delete and the record are <see cref="LandedBranchWithdrawal"/>'s.
    /// </summary>
    [Fact]
    public void The_screens_withdrawal_is_the_drivers_one_withdrawal()
    {
        var lines = File.ReadAllText(SourceFiles.Find(SourceFiles.ModulesProject, "LinesRoutes.cs"));

        Assert.Contains(".WithdrawAsync(service, repository, branch, why, plan, cancellationToken)", lines, StringComparison.Ordinal);
        Assert.Contains("TreesWords.Withdraw(withdrawal)", lines, StringComparison.Ordinal);
        Assert.DoesNotContain("KeepDiscardedAsync", lines, StringComparison.Ordinal);
        Assert.DoesNotContain("DeleteJudgedAsync", lines, StringComparison.Ordinal);
    }

    /// <summary>
    /// The person's delete of recovery refs: before the driver is up it is the cold-start sentence; a call naming no
    /// repository or no refs is malformed; and a process that does not hold the home deletes nothing and says so, as an answer.
    /// </summary>
    [Fact]
    public async Task Deleting_recovery_refs_is_refused_cold_malformed_or_without_the_home()
    {
        var ref1 = new { @ref = "refs/daoris/discarded/daoris/s-1", commit = new string('a', 40) };
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "RECOVERY_DROP", new { repository = "engine", refs = new[] { ref1 } }));
        await Assert.ThrowsAnyAsync<Exception>(() => AnswerAsync(Module(), "RECOVERY_DROP", new { refs = new[] { ref1 } }));
        var module = await ModuleOverAsync(new DiscardService(root: Home));
        await Assert.ThrowsAnyAsync<Exception>(() => AnswerAsync(module, "RECOVERY_DROP", new { repository = "engine" }));

        var answer = await AnswerAsync(module, "RECOVERY_DROP", new { repository = "engine", refs = new[] { ref1 } });

        Assert.False(answer.GetProperty("done").GetBoolean());
        Assert.Contains("does not hold the home", answer.GetProperty("refused").GetString());
        Assert.Equal(0, answer.GetProperty("dropped").GetArrayLength());
    }

    /// <summary>
    /// The person's word on a held attempt: before the driver is up it is the cold-start sentence; a call naming no
    /// attempt is malformed; and a process that does not hold the home records nothing and says so, as an answer. The work's state lists
    /// none held where no unit keeps a node record, the owner's install among them.
    /// </summary>
    [Fact]
    public async Task A_word_on_a_held_attempt_is_refused_cold_malformed_or_without_the_home_and_none_is_listed_unarmed()
    {
        var asked = new { unit = "quest:q7", node = "deploy", attempt = 1, resolution = "did-not" };
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "WORK_HELD", asked));
        var module = await ModuleOverAsync(new DiscardService(root: Home));
        await Assert.ThrowsAnyAsync<Exception>(() => AnswerAsync(module, "WORK_HELD", new { unit = "quest:q7", node = "deploy", resolution = "stop" }));

        var answer = await AnswerAsync(module, "WORK_HELD", asked);

        Assert.False(answer.GetProperty("done").GetBoolean());
        Assert.Equal("not-held", answer.GetProperty("refused").GetString());
        Assert.Contains("does not hold the home", answer.GetProperty("message").GetString());
        Assert.Equal(0, (await AnswerAsync(module, "WORK_STATE", new { })).GetProperty("held").GetArrayLength());
    }

    /// <summary>
    /// The terminal's <c>workflow fix</c> is the window's <i>Ask for a fix…</i>: asked for its words it is answered the
    /// verb's own line and exit code, a run the home keeps none of refused in the same sentence the window's press is told.
    /// </summary>
    [Fact]
    public async Task Ask_fix_asked_for_its_words_answers_the_verb_s_line_and_a_refusal_for_a_run_that_is_not_kept()
    {
        var module = await ModuleOverAsync(new DiscardService(root: Home));
        var pressed = new { unit = "run:v-00000000000000aa", node = "verify", attempt = 1, resolution = "ask-fix" };

        var said = await AnswerAsync(module, "WORK_HELD", new { pressed.unit, pressed.node, pressed.attempt, pressed.resolution, said = true });
        var window = await AnswerAsync(module, "WORK_HELD", pressed);

        var lines = said.GetProperty("said").GetProperty("out").EnumerateArray().Select(each => each.GetString()).ToArray();
        Assert.Equal(["workflow: that is not a failed verification held for you"], lines);
        Assert.Equal(1, said.GetProperty("said").GetProperty("code").GetInt32());
        Assert.Equal("not-asked", window.GetProperty("refused").GetString());
        Assert.Equal("that is not a failed verification held for you", window.GetProperty("message").GetString());
    }

    /// <summary>
    /// Each held attempt carries, additively, the words the person may say of it: a plugin's three as they were, and on a
    /// held close its recoveries and its stop, never a plugin's word, so the page offers a close only what the close takes.
    /// </summary>
    [Fact]
    public void A_held_attempt_carries_the_words_offered_on_it_a_close_its_own()
    {
        var camel = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var plugin = new HeldAttempt("unit:1", "deploy", 1, "acme.deploy", "acme.deploy/deploy", NodeStates.Unknown, "unknown");
        var close = new HeldAttempt("quest:q7", "close", 1, QuestClosing.Sender, NodeKinds.QuestClose, NodeStates.Unknown, "no-acceptance")
        {
            Outcome = WorkStates.Reached, Words = HeldResolutions.Close,
        };

        var rows = JsonSerializer.SerializeToElement(LinesRoutes.HeldAnswer([plugin, close]), camel).EnumerateArray()
            .Select(row => row.GetProperty("words").EnumerateArray().Select(word => word.GetString()).ToArray()).ToArray();

        Assert.Equal(["happened", "did-not", "stop"], rows[0]);
        Assert.Equal(["record", "resend", "stop"], rows[1]);
        Assert.Contains("was closed", HeldAttempts.WhySaid(close), StringComparison.Ordinal);
    }

    /// <summary>
    /// A held close whose submission moved by the runtime's last read goes on the wire with <c>resend</c> alone and its
    /// reason's code, which the page words; a loop with no last read (unarmed, or before a pass) hands the held list none to judge by.
    /// </summary>
    [Fact]
    public void A_held_close_whose_submission_moved_is_listed_with_resend_alone_and_the_reason()
    {
        var camel = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var moved = new HeldAttempt("quest:q7", "close", 1, QuestClosing.Sender, NodeKinds.QuestClose, NodeStates.Unknown, WorkPerformer.SubmissionMoved)
        {
            Outcome = WorkStates.Reached, Words = [HeldResolutions.Resend],
        };

        var row = JsonSerializer.SerializeToElement(LinesRoutes.HeldAnswer([moved]), camel).EnumerateArray().Single();

        Assert.Equal(["resend"], row.GetProperty("words").EnumerateArray().Select(word => word.GetString()).ToArray());
        Assert.Equal("submission-moved", row.GetProperty("why").GetString());
        Assert.Contains("reopened or replaced", row.GetProperty("said").GetString(), StringComparison.Ordinal);
        Assert.Null(Loop().LastQuests);
    }

    /// <summary>
    /// A node record that does not read is listed by the work's state, by its file and the unit it names, with the driver's
    /// sentence; a unit picked lists its own alone. Its folder's press is refused and said where no record is kept, and opens nothing
    /// without the window's launcher.
    /// </summary>
    [Fact]
    public async Task A_node_record_that_does_not_read_is_listed_by_the_work_s_state_and_its_folder_opened_only_where_it_is()
    {
        var module = await ModuleOverAsync(new DiscardService(root: Home));
        Assert.Contains("no unit keeps a node record", await RefusalAsync(module, "WORK_RECORDS_FOLDER", new { }));

        Directory.CreateDirectory(NodeRecordFiles.FolderOf(Home));
        File.WriteAllText(NodeRecordFiles.PathOf(Home, "quest:q7"), "{ \"unit\": \"quest:q7\" }");
        var listed = (await AnswerAsync(module, "WORK_STATE", new { })).GetProperty("unreadable");

        var record = Assert.Single(listed.EnumerateArray());
        Assert.Equal((NodeRecordFiles.KeyOf("quest:q7") + ".json", "quest:q7"), (record.GetProperty("file").GetString(), record.GetProperty("unit").GetString()));
        Assert.Contains("does not read", record.GetProperty("said").GetString());
        Assert.Single((await AnswerAsync(module, "WORK_STATE", new { unit = "quest:q7" })).GetProperty("unreadable").EnumerateArray());
        Assert.Empty((await AnswerAsync(module, "WORK_STATE", new { unit = "quest:q8" })).GetProperty("unreadable").EnumerateArray());
        Assert.False((await AnswerAsync(module, "WORK_RECORDS_FOLDER", new { })).GetProperty("opened").GetBoolean());
    }

    /// <summary>The route's module over a loop come up on <paramref name="service"/>, standing in for the service.</summary>
    private async Task<DriverModule> ModuleOverAsync(DiscardService service)
    {
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new SessionChanges(service))));
        // The come-up's own reads (the census) are not the press's: settled, then forgotten.
        await loop.Settling;
        service.Forget();
        return new DriverModule(Bus, loop);
    }

    /// <summary>
    /// The branch is removed in its repository's own checkout, which the registry names: with none on this
    /// machine, the terminal's sentence as an answer, and nothing is asked of git.
    /// </summary>
    [Fact]
    public async Task A_session_branch_is_discarded_only_where_its_repository_has_a_checkout_here()
    {
        var module = await ModuleOverAsync(new DiscardService(root: null));

        var answer = await AnswerAsync(module, "DISCARD_SESSION_BRANCH", new { repository = "engine", branch = "daoris/s-1a2b3c4d", force = true });

        Assert.False(answer.GetProperty("done").GetBoolean());
        Assert.Equal("`engine` has no checkout here, so its branch `daoris/s-1a2b3c4d` cannot be removed from this machine.",
            answer.GetProperty("message").GetString());
    }

    /// <summary>
    /// A branch whose tree a session still running or waiting names is kept, whatever the press says: forced,
    /// the driver's removal would take that tree with it. The route ends in the driver's one discard, the terminal's too,
    /// so the answer is its sentence, and the checkout is not asked for.
    /// </summary>
    [Fact]
    public async Task A_branch_whose_tree_a_session_still_holds_is_kept_even_forced()
    {
        var service = new DiscardService(root: Home, live: DaorisHome.Entry(Home, HomeEntry.Trees,"aurora", "Engine", "s-1a2b3c4d"));
        var module = await ModuleOverAsync(service);

        var answer = await AnswerAsync(module, "DISCARD_SESSION_BRANCH", new { repository = "engine", branch = "daoris/s-1a2b3c4d", force = true });

        Assert.False(answer.GetProperty("done").GetBoolean());
        Assert.Equal(SessionBranchDiscard.Held("engine", "daoris/s-1a2b3c4d"), answer.GetProperty("message").GetString());
        Assert.Equal("daoris/s-1a2b3c4d", answer.GetProperty("branch").GetString());
        Assert.Contains("/api/sessions", service.Asked);
        Assert.DoesNotContain("/api/registry", service.Asked);
    }

    /// <summary>
    /// Only Daoris's own branches are discarded here, refused in the driver's own words before git is asked: a branch of
    /// the person's is theirs to delete.
    /// </summary>
    [Fact]
    public async Task Only_a_session_branch_is_discarded_and_its_refusal_is_the_drivers()
    {
        var module = await ModuleOverAsync(new DiscardService(root: Home));

        var answer = await AnswerAsync(module, "DISCARD_SESSION_BRANCH", new { repository = "engine", branch = "feature/0fda18-fix", force = true });

        Assert.False(answer.GetProperty("done").GetBoolean());
        Assert.Contains("is not a session branch", answer.GetProperty("message").GetString());
    }

    /// <summary>
    /// The screen's discard composes none of the driver's pieces itself, so it cannot part from the terminal's door
    /// again. Read from the modules' sources, as a reviewer would: the keep, the checkout and the removal are
    /// <see cref="SessionBranchDiscard"/>'s.
    /// </summary>
    [Fact]
    public void The_screens_discard_is_the_drivers_one_discard()
    {
        // The bridge's files: its domains' handler classes.
        var sources = SourceFiles.All(SourceFiles.ModulesProject, "*Routes.cs")
            .Select(path => (File: Path.GetFileName(path), Text: File.ReadAllText(path))).ToList();

        Assert.Contains(sources, source => source.File == "LinesRoutes.cs" && source.Text.Contains(".DiscardAsync(service, repository, branch, force,", StringComparison.Ordinal));
        Assert.All(sources, source =>
        {
            Assert.DoesNotContain("RemoveBranchAsync(", source.Text);
            Assert.DoesNotContain("BranchOfTree(", source.Text);
        });
    }

    /// <summary>
    /// The service standing in for a discard: the registry with <c>engine</c>'s checkout at <paramref name="root"/> (none where
    /// null), and the sessions running or waiting, one per tree in <paramref name="live"/>. Each path asked is kept.
    /// </summary>
    private sealed class DiscardService(string? root, params string[] live) : HttpMessageHandler
    {
        private readonly List<string> _asked = [];

        public IReadOnlyList<string> Asked
        {
            get { lock (_asked) return [.. _asked]; }
        }

        public void Forget()
        {
            lock (_asked) _asked.Clear();
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            lock (_asked) _asked.Add(path);
            object body = path switch
            {
                "/api/registry" => root is null ? Array.Empty<object>() : new object[] { new { repository = "engine", workspace = "aurora", root } },
                "/api/sessions" => live.Select((tree, at) => (object)new { id = $"s{at + 1}", repository = "engine", state = "working", tree }).ToArray(),
                _ => Array.Empty<object>(),
            };
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }

    /// <summary>
    /// Bringing repositories up to date reads the registry's checkouts and the sessions in use, as the clean-up
    /// does, so before the driver is up both its list and its press are the cold-start sentence — and nothing is fetched.
    /// </summary>
    [Fact]
    public async Task Bringing_up_to_date_before_the_driver_is_up_is_a_sentence()
    {
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "TREES_SYNC_PLAN"));
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "TREES_SYNC_PLAN", new { also = new[] { "game" } }));
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "TREES_SYNC_PLAN", new { all = true }));
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "TREES_SYNC_PLAN", new { all = true, workspace = "aurora" }));
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "TREES_SYNC", new { only = new[] { "engine:main" } }));
        // Which repositories a look would take is read off the registry's checkouts too.
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "TREES_SYNC_SCOPE"));
        // Each unit's milestone is read off the same checkouts and sessions.
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "WORK_STATE"));
    }

    /// <summary>
    /// A look or a press asked for a workspace takes that workspace's checkouts alone, its name matched
    /// without case and a row with none read as the default's, as every door reads a registry row's workspace. Asked for
    /// none, it is the machine's. The install's work workspace looked at every checkout on the machine, another
    /// circle's included. Read off the registry rows alone, since a look at a real checkout runs git. The rows
    /// are the driver tests' one table, <c>fixtures/checkout-scope.json</c>, which TreesCommand.Checkouts is read against
    /// too, so the two readers cannot drift apart unseen.
    /// </summary>
    [Fact]
    public void A_look_asked_for_a_workspace_takes_its_own_checkouts_alone()
    {
        using var table = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            WorkspaceRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "checkout-scope.json")));
        static string? Text(JsonElement row, string name) => row.TryGetProperty(name, out var value) ? value.GetString() : null;
        RepoView[] registry =
        [
            .. table.RootElement.GetProperty("registry").EnumerateArray().Select(row => row.TryGetProperty("workspace", out var space)
                ? new RepoView(Text(row, "repository")!, true, Text(row, "root"), space.GetString()!)
                : new RepoView(Text(row, "repository")!, true, Text(row, "root"))),
        ];

        var asks = table.RootElement.GetProperty("asks").EnumerateArray().ToList();
        Assert.True(asks.Count >= 10, $"the table holds its rows: {asks.Count}");
        foreach (var ask in asks)
        {
            var (repository, workspace) = (Text(ask, "repository"), Text(ask, "workspace"));
            string[] takes = [.. ask.GetProperty("takes").EnumerateArray().Select(name => name.GetString()!)];
            string[] took = [.. LinesRoutes.Checkouts(registry, repository, workspace).Select(each => each.Repository)];
            Assert.True(takes.SequenceEqual(took),
                $"repository `{repository}`, workspace `{workspace}`: the table takes [{string.Join(", ", takes)}], the modules [{string.Join(", ", took)}]");
        }
    }

    /// <summary>
    /// Every door's press asks the sessions in use again inside each repository's hold, so a session a driver
    /// opened since the look is seen: Settings' *Bring up to date* and Ask Daoris's sync card alike. Read from
    /// the modules' sources, as a reviewer would, since only a live driver can open a session between the two.
    /// </summary>
    [Fact]
    public void Every_press_of_bringing_up_to_date_asks_the_sessions_in_use_again()
    {
        var presses = SourceFiles.All(SourceFiles.ModulesProject, "*.cs")
            .SelectMany(path =>
            {
                var source = File.ReadAllText(path);
                return System.Text.RegularExpressions.Regex.Matches(source, @"\.SyncAsync\(repositories\b")
                    .Select(press => (File: Path.GetFileName(path), Call: source[press.Index..source.IndexOf(';', press.Index)]));
            })
            .ToList();

        Assert.Contains(presses, press => press.File == "TreesTidyDesk.cs");
        Assert.Contains(presses, press => press.File == "HelpScreenDoors.cs");
        Assert.All(presses, press => Assert.True(press.Call.Contains("inUseNow:", StringComparison.Ordinal),
            $"{press.File} presses bringing up to date without asking the sessions in use again: {press.Call}"));
    }

    private static string WorkspaceRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }

    /// <summary>What each repository's line is needs the registry's checkouts, so before the driver is up it is the cold-start sentence.</summary>
    [Fact]
    public async Task Asking_the_lines_before_the_driver_is_up_is_a_sentence()
    {
        var refusal = await RefusalAsync(Module(), "LINES");

        Assert.Contains(Refusals.DriverNotReady, refusal);
    }

    /// <summary>
    /// Right after a start, Settings → Workspace said no repository here had a line, and kept saying it. The
    /// lines refuse *still coming up* until the loop's service is up, never answering an empty list for it; the moment it
    /// is, the state says so, the page is told once (<c>DRIVER_READY</c>), which is when it asks again what the driver
    /// refused, and the lines answer every repository the registry holds.
    /// </summary>
    [Fact]
    public async Task The_lines_refuse_until_the_drivers_service_is_up_and_the_page_is_told_when_it_is()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(module, "LINES"));
        Assert.False((await AnswerAsync(module, "STATE")).GetProperty("ready").GetBoolean());
        Assert.DoesNotContain(Raised, message => message.Type == "DRIVER_READY");

        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new SessionChanges(new StandInService()))));

        Assert.True((await AnswerAsync(module, "STATE")).GetProperty("ready").GetBoolean());
        Assert.Single(Raised, message => message.Type == "DRIVER_READY");
        var lines = (await AnswerAsync(module, "LINES")).GetProperty("lines");
        Assert.Equal(["engine", "game"], lines.EnumerateArray().Select(line => line.GetProperty("repository").GetString()));
    }

    /// <summary>The service a driver reads its snapshot from, standing in: two registered repositories, no quest, no session.</summary>
    private sealed class StandInService : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.RequestUri!.AbsolutePath == "/api/registry"
                ? """[{"repository":"game","workspace":"aurora"},{"repository":"engine","workspace":"aurora"}]"""
                : "[]";
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }
}

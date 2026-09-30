using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WSR5b: a branch a landing made can be handed to a landing plugin afterwards — one landed before its
/// workspace named a plugin, or one whose plugin failed. The hand-off speaks D100's own frame for the
/// branch as it stands, with its record, and a hand-off that fails changes nothing.
/// </summary>
/// <remarks>
/// 🔴 <b>Nothing here reaches a network.</b> The plugins are fakes, `origin` is a bare repository under the
/// scratch folder, and the only push is the test's own, to it.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class HandOffTests : LandedFixture
{
    private const string Id = "example.lands";

    /// <summary>
    /// A branch landed while the rule named no plugin; the rule names one now. The hand-off tells it the
    /// branch as it stands — the line as its base, its commits the line lacks, the quest and session the
    /// landing recorded — and keeps the push on the record.
    /// </summary>
    [Fact]
    public async Task A_branch_landed_before_its_workspace_named_a_plugin_is_handed_the_landings_frame()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var b = await LandAsync(new SessionTrees(Home), root, "shared.txt", "one\ntwo\n", new LandingSubject("s2a3b4c5", "q2", "Second part"));
        NamePlugin();
        JsonElement? told = null;
        var trees = new SessionTrees(Home, Plugins(payload =>
        {
            told = JsonSerializer.SerializeToElement(payload);
            return new PluginLanding(Id, true, "https://example.test/example-org/engine/pull/9", "pushed it and opened the pull request.");
        }));
        var entry = Assert.Single(trees.Recorded.Find("s2a3b4c5"));

        var handed = await trees.HandAsync(root, entry);

        Assert.True(handed.Handed, handed.Message);
        Assert.Contains("pushed it and opened the pull request", handed.Message);
        Assert.Contains("https://example.test/example-org/engine/pull/9", handed.Message);
        var frame = Assert.IsType<JsonElement>(told);
        Assert.Equal(b.Branch, frame.GetProperty("branch").GetString());
        Assert.Equal("main", frame.GetProperty("base").GetString());
        Assert.Equal("engine", frame.GetProperty("repository").GetString());
        Assert.Equal("aurora", frame.GetProperty("workspace").GetString());
        Assert.Equal(Path.GetFullPath(root), Path.GetFullPath(frame.GetProperty("root").GetString()!));
        Assert.Equal("s2a3b4c5", frame.GetProperty("session").GetString());
        Assert.Equal("Second part", frame.GetProperty("title").GetString());
        Assert.Equal("q2", frame.GetProperty("quest").GetProperty("id").GetString());
        var commit = Assert.Single(frame.GetProperty("commits").EnumerateArray());
        Assert.Equal((await GitAsync(root, "rev-parse", b.Branch!)).Trim(), commit.GetProperty("sha").GetString());
        Assert.Equal("Second part", commit.GetProperty("subject").GetString());

        var kept = trees.Recorded.Of("engine", b.Branch!)!;
        Assert.True(kept.Pushed);
        Assert.Equal(Id, kept.Plugin);
        Assert.Equal("https://example.test/example-org/engine/pull/9", kept.PullRequest);
        Assert.Equal((await GitAsync(root, "rev-parse", b.Branch!)).Trim(), kept.PushedTip);
    }

    /// <summary>A landing whose plugin did not push leaves a branch the hand-off can take up — where pressing Accept again is refused.</summary>
    [Fact]
    public async Task A_branch_whose_plugin_failed_at_its_landing_can_be_handed_afterwards()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        NamePlugin();
        var failing = new SessionTrees(Home, Plugins(_ => new PluginLanding(Id, false, null, "gh is not signed in — run `gh auth login`.")));
        var b = await LandAsync(failing, root, "shared.txt", "one\ntwo\n", new LandingSubject("s2", "q2", "Second"));
        Assert.False(failing.Recorded.Of("engine", b.Branch!)!.Pushed);

        var trees = new SessionTrees(Home, Plugins(_ => new PluginLanding(Id, true, null, "pushed it.")));
        var handed = await trees.HandAsync(root, trees.Recorded.Of("engine", b.Branch!)!);

        Assert.True(handed.Handed, handed.Message);
        Assert.True(trees.Recorded.Of("engine", b.Branch!)!.Pushed);
    }

    /// <summary>`--plugin` names one for this hand-off, where the rule names none.</summary>
    [Fact]
    public async Task A_plugin_named_for_the_hand_off_is_spoken_to_where_the_rule_names_none()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var b = await LandAsync(new SessionTrees(Home), root, "shared.txt", "one\ntwo\n", new LandingSubject("s2", "q2", "Second"));
        InstallManifest();
        var asked = 0;
        var trees = new SessionTrees(Home, Plugins(_ => { asked++; return new PluginLanding(Id, true, null, "pushed it."); }));
        var entry = trees.Recorded.Of("engine", b.Branch!)!;

        var none = await trees.HandPlanAsync(root, entry);
        Assert.Null(none.Plugin);
        Assert.Contains("names none", none.Problem);

        var handed = await trees.HandAsync(root, entry, plugin: Id);
        Assert.True(handed.Handed, handed.Message);
        Assert.Equal(1, asked);
    }

    /// <summary>
    /// Each refusal in its own sentence, and none of them reaches the plugin: a plugin that cannot land work
    /// here (D100's), a branch no longer the landing's, one gone, one whose work already reads on the line.
    /// </summary>
    [Fact]
    public async Task What_cannot_be_handed_is_refused_in_its_own_words_and_the_plugin_is_never_asked()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var b = await LandAsync(new SessionTrees(Home), root, "shared.txt", "one\ntwo\n", new LandingSubject("s2", "q2", "Second"));
        var asked = 0;
        var trees = new SessionTrees(Home, Plugins(_ => { asked++; return new PluginLanding(Id, true, null, "pushed it."); }));
        LandedBranch Entry() => trees.Recorded.Of("engine", b.Branch!)!;

        NamePlugin(install: false);
        Assert.Contains("not installed", (await trees.HandAsync(root, Entry())).Message);
        InstallManifest();
        PluginState.Disable(Home, Id);
        Assert.Contains($"daoris plugin enable {Id}", (await trees.HandAsync(root, Entry())).Message);
        PluginState.Enable(Home, Id);
        InstallManifest(points: "session/ended");
        Assert.Contains("`work/land`", (await trees.HandAsync(root, Entry())).Message);
        InstallManifest();

        await SquashAsync(root, b.Branch!);
        var onLine = await trees.HandAsync(root, Entry());
        Assert.False(onLine.Handed);
        Assert.Contains("already reads on the line", onLine.Message);

        await GitAsync(root, "branch", "-D", b.Branch!);
        Assert.Contains("is gone", (await trees.HandAsync(root, Entry())).Message);
        await GitAsync(root, "branch", b.Branch!, "main~1");
        await CommitOnAsync(root, b.Branch!, "mine.txt", "mine\n", "the person's own");
        Assert.Contains("not the branch the landing made", (await trees.HandAsync(root, Entry())).Message);

        Assert.Equal(0, asked);
        Assert.Contains("no branch a landing made", SessionTrees.NotLanded("feature/mine"));
    }

    /// <summary>
    /// One already on its remote at this very commit, with a pull request answered for it, is refused: there
    /// is nothing new to push. Once it moves, it can be handed again.
    /// </summary>
    [Fact]
    public async Task A_branch_already_on_its_remote_at_this_commit_with_a_pull_request_is_refused_until_it_moves()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        NamePlugin();
        var pushing = new SessionTrees(Home, Plugins(_ => new PluginLanding(Id, true, "https://example.test/example-org/engine/pull/9", "pushed it.")));
        var b = await LandAsync(pushing, root, "shared.txt", "one\ntwo\n", new LandingSubject("s2", "q2", "Second"));
        // What the plugin's own push left: the branch on origin at the commit it pushed.
        await GitAsync(root, "push", "--quiet", "-u", "origin", b.Branch!);

        var already = await pushing.HandPlanAsync(root, pushing.Recorded.Of("engine", b.Branch!)!);
        Assert.Contains("already on its remote at this commit", already.Problem);
        Assert.Contains("https://example.test/example-org/engine/pull/9", already.Problem);
        Assert.False((await pushing.HandAsync(root, pushing.Recorded.Of("engine", b.Branch!)!)).Handed);

        await CommitOnAsync(root, b.Branch!, "review.txt", "a review's fix\n", "after the review");
        Assert.Null((await pushing.HandPlanAsync(root, pushing.Recorded.Of("engine", b.Branch!)!)).Problem);
    }

    /// <summary>A hand-off the plugin did not complete changes nothing: the branch, the record, the remote as they were.</summary>
    [Theory]
    [InlineData("throws")]
    [InlineData("did-not-push")]
    public async Task A_failed_hand_off_changes_nothing(string how)
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var b = await LandAsync(new SessionTrees(Home), root, "shared.txt", "one\ntwo\n", new LandingSubject("s2", "q2", "Second"));
        NamePlugin();
        var trees = new SessionTrees(Home, Plugins(_ => how == "throws"
            ? throw new DriverException($"plugin `{Id}` did not answer `hook/work/land` within 1s.")
            : new PluginLanding(Id, false, null, "gh is not signed in — run `gh auth login`.")));
        var tip = (await GitAsync(root, "rev-parse", b.Branch!)).Trim();
        var record = await File.ReadAllTextAsync(Path.Combine(Home, LandedBranches.FileName));

        var handed = await trees.HandAsync(root, trees.Recorded.Of("engine", b.Branch!)!);

        Assert.False(handed.Handed);
        Assert.NotNull(handed.Plugin);
        Assert.Contains($"git push -u origin {b.Branch}", handed.Message);
        Assert.Equal(tip, (await GitAsync(root, "rev-parse", b.Branch!)).Trim());
        Assert.Equal(record, await File.ReadAllTextAsync(Path.Combine(Home, LandedBranches.FileName)));
        Assert.Empty((await GitAsync(origin, "branch", "--list", b.Branch!)).Trim());
    }

    /// <summary>The plan says who it would go to before the press, and what stands in the way where something does.</summary>
    [Fact]
    public async Task The_plan_names_the_branch_and_the_plugin_before_the_press()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var b = await LandAsync(new SessionTrees(Home), root, "shared.txt", "one\ntwo\n", new LandingSubject("s2", "q2", "Second"));
        NamePlugin(install: false);
        var trees = new SessionTrees(Home);
        var entry = trees.Recorded.Of("engine", b.Branch!)!;

        var missing = await trees.HandPlanAsync(root, entry);
        Assert.Equal((b.Branch, Id, 1), (missing.Branch, missing.Plugin, missing.Commits));
        Assert.Contains("not installed", missing.Problem);

        InstallManifest();
        Assert.Null((await trees.HandPlanAsync(root, entry)).Problem);
    }

    /// <summary>What the conversation's record keeps of a hand-off: the whole sentence, the plugin's part in it.</summary>
    [Fact]
    public void A_hand_off_is_kept_as_a_note_in_the_conversation()
    {
        var note = LandingRules.HandNote(new TreeHand(true, "handed `feature/x` to plugin `example.lands`. Plugin `example.lands`: pushed it.", "feature/x"));

        Assert.Equal(SessionEventKind.Note, note.Kind);
        Assert.StartsWith("the person handed this work on:", note.Text);
        Assert.Contains("Plugin `example.lands`: pushed it.", note.Text);
    }

    // ——— a plugin, faked

    private sealed class FakeLander(Func<object, PluginLanding> land) : IHookChannel
    {
        public IReadOnlyList<string> Points => [HookPoints.Land];
        public bool Alive => true;
        public Task<HookDecision> ConsiderAsync(object payload, CancellationToken ct) => Task.FromResult(HookDecision.Allow);
        public Task EndedAsync(object payload, CancellationToken ct) => Task.CompletedTask;
        public Task<PluginLanding> LandAsync(object payload, CancellationToken ct) => Task.FromResult(land(payload));
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private LandingPlugins Plugins(Func<object, PluginLanding> land) =>
        new(Home, start: (_, _, _) => Task.FromResult<IHookChannel>(new FakeLander(land)));

    /// <summary>The rule names the plugin now — and the plugin is installed, unless the test says otherwise.</summary>
    private void NamePlugin(bool install = true)
    {
        if (install) InstallManifest();
        DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Id))
            .Save(Path.Combine(Home, "driver.json"));
    }

    private void InstallManifest(string points = "work/land")
    {
        var folder = Path.Combine(Home, "plugins", Id);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"),
            $$"""{ "id": "{{Id}}", "hooks": { "command": ["node", "${plugin}/land.mjs"], "points": ["{{points}}"] } }""");
    }
}

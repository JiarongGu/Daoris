using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WSR4 (D100): a branch rule may name a plugin, and once Daoris has made the branch the plugin is
/// spoken to on <c>work/land</c> — it pushes and opens the pull request for its platform, and Daoris
/// runs neither. A plugin that fails never undoes the branch.
/// </summary>
/// <remarks>
/// Nothing here reaches a network (<see cref="LandingPluginFixture"/>). The plugin that never answers is
/// <see cref="LandingPluginPatienceTests"/>'.
/// </remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class LandingPluginTests : LandingPluginFixture
{
    /// <summary>
    /// A real plugin, started as a landing starts one: it sees the branch already made, pushes it to
    /// origin, and answers with the pull request — and all of it is said where the landing is said.
    /// </summary>
    [Fact]
    public async Task The_plugin_is_spoken_to_once_the_branch_exists_and_its_answer_is_said_and_kept()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        InstallRealPlugin();
        Rule(GraphLand.PullRequest("feature/{quest}-{slug}", Id));
        var trees = new SessionTrees(Home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "work.txt", "the session's work", "the work");

        var landed = await trees.LandAsync(opened.Path, new LandingSubject("s1a2b3c4", "0fda18", "Fix the API gap"), gate: Lands);

        Assert.True(landed.Landed, landed.Message);
        const string branch = "feature/0fda18-fix-the-api-gap";
        Assert.Equal(branch, landed.Branch);
        var plugin = Assert.IsType<PluginLanding>(landed.Plugin);
        Assert.Equal(Id, plugin.Plugin);
        Assert.True(plugin.Pushed);
        Assert.False(plugin.Failed);
        Assert.Equal("https://example.test/example-org/engine/pull/7", plugin.PullRequest);
        Assert.Contains("pushed it to origin", landed.Message);
        Assert.Contains("https://example.test/example-org/engine/pull/7", landed.Message);
        // The person is not told to push what the plugin pushed.
        Assert.DoesNotContain("git push", landed.Message);

        // What the plugin was told, and that the branch was already there when it was.
        var told = JsonDocument.Parse(await File.ReadAllTextAsync(DaorisHome.Entry(Home, HomeEntry.Plugins,".data", Id, "landed.json"))).RootElement;
        Assert.True(told.GetProperty("branchExisted").GetBoolean());
        var frame = told.GetProperty("frame");
        Assert.Equal(branch, frame.GetProperty("branch").GetString());
        Assert.Equal("main", frame.GetProperty("base").GetString());
        Assert.Equal("engine", frame.GetProperty("repository").GetString());
        Assert.Equal("aurora", frame.GetProperty("workspace").GetString());
        Assert.Equal(Path.GetFullPath(root), Path.GetFullPath(frame.GetProperty("root").GetString()!));
        Assert.Equal("s1a2b3c4", frame.GetProperty("session").GetString());
        Assert.Equal("Fix the API gap", frame.GetProperty("title").GetString());
        Assert.Equal("0fda18", frame.GetProperty("quest").GetProperty("id").GetString());
        var commit = Assert.Single(frame.GetProperty("commits").EnumerateArray());
        Assert.Equal("the work", commit.GetProperty("subject").GetString());
        Assert.Equal((await GitAsync(root, "rev-parse", branch)).Trim(), commit.GetProperty("sha").GetString());

        // The plugin's own push, to the bare origin — Daoris ran no push of its own.
        Assert.Equal((await GitAsync(root, "rev-parse", branch)).Trim(), (await GitAsync(origin, "rev-parse", branch)).Trim());
    }

    /// <summary>After — never instead: a landing refused before the branch is made never reaches the plugin.</summary>
    [Fact]
    public async Task A_landing_refused_before_the_branch_is_made_never_speaks_to_the_plugin()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var asked = 0;
        Rule(GraphLand.PullRequest("feature/{quest}-{slug}", Id));
        InstallManifest();
        var trees = new SessionTrees(Home, Plugins(_ => { asked++; return Answer(true, null, "pushed"); }));
        var opened = await trees.OpenAsync(root, "engine", "aurora");

        var nothing = await trees.LandAsync(opened.Path, new LandingSubject("s1a2b3c4", "0fda18", "Fix"), gate: Lands);
        Assert.False(nothing.Landed);

        await CommitAsync(opened.Path, "work.txt", "the session's work", "the work");
        await GitAsync(root, "branch", "feature/0fda18-fix");
        var taken = await trees.LandAsync(opened.Path, new LandingSubject("s1a2b3c4", "0fda18", "Fix"), gate: Lands);
        Assert.False(taken.Landed);
        Assert.Contains("already a branch", taken.Message);

        Assert.Equal(0, asked);
    }

    /// <summary>The plugin checked again at the press: one gone since the rule was set refuses before anything is made.</summary>
    [Fact]
    public async Task A_rule_whose_plugin_is_gone_or_off_is_refused_at_the_press_and_nothing_is_made()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        Rule(GraphLand.PullRequest("feature/{quest}-{slug}", Id));
        var trees = new SessionTrees(Home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "work.txt", "the session's work", "the work");

        var missing = await trees.LandAsync(opened.Path, new LandingSubject("s1a2b3c4", "0fda18", "Fix"), gate: Lands);
        Assert.False(missing.Landed);
        Assert.Contains("not installed", missing.Message);

        InstallManifest();
        PluginState.Disable(Home, Id);
        var off = await trees.LandAsync(opened.Path, new LandingSubject("s1a2b3c4", "0fda18", "Fix"), gate: Lands);
        Assert.False(off.Landed);
        Assert.Contains($"daoris plugin enable {Id}", off.Message);

        Assert.Empty((await GitAsync(root, "branch", "--list", "feature/*")).Trim());
    }

    /// <summary>
    /// GITAUTH1c (the git-auth design §4.1): where Daoris's doors are laid out and its git is older than they need, the landing's push is
    /// one of Daoris's own remote acts and refuses, naming both versions, before its plugin is started; the branch still stands.
    /// </summary>
    [Fact]
    public async Task A_git_older_than_the_doors_need_refuses_the_plugins_push_before_it_starts_and_the_branch_stands()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        InstallManifest();
        Rule(GraphLand.PullRequest("feature/{quest}-{slug}", Id));
        var asked = 0;
        var trees = new SessionTrees(Home, Plugins(_ => { asked++; return Answer(true, null, "pushed"); }));
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "work.txt", "the session's work", "the work");
        // The ssh door laid out beside the home (DaorisHome.InstallBin), as an empty file: nothing here starts it.
        var bin = Path.Combine([Home, .. Tools.InstallBinLayout]);
        Directory.CreateDirectory(bin);
        File.WriteAllText(Path.Combine(bin, GitWiring.SshDoor), "");

        TreeLanding landed;
        using (GitVersions.Pretending(new Version(2, 30, 1)))
        {
            landed = await trees.LandAsync(opened.Path, new LandingSubject("s1a2b3c4", "0fda18", "Fix"), gate: Lands);
        }

        Assert.True(landed.Landed, landed.Message);
        Assert.Equal(0, asked);
        var plugin = Assert.IsType<PluginLanding>(landed.Plugin);
        Assert.True(plugin.Failed);
        Assert.Contains("2.30.1", plugin.Message, StringComparison.Ordinal);
        Assert.Contains("2.31", plugin.Message, StringComparison.Ordinal);
        Assert.Contains("the work", await GitAsync(root, "log", "feature/0fda18-fix", "--oneline"));
    }

    /// <summary>
    /// GITAUTH1f (the git-auth design §6.7): the landing asks the judge before it starts its plugin. Where the repository's git access
    /// denies writes, the plugin is not started and the landing says so, naming origin; the branch stands. Under ask, the person's
    /// Accept is the yes for exactly that push, and the plugin is started.
    /// </summary>
    [Fact]
    public async Task A_git_access_that_denies_writes_refuses_the_plugins_push_before_it_starts_and_ask_takes_the_accept()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        InstallManifest();
        Rule(GraphLand.PullRequest("feature/{quest}-{slug}", Id));
        var asked = 0;
        var trees = new SessionTrees(Home, Plugins(_ => { asked++; return Answer(true, null, "pushed"); }));
        GitAccessActs.Apply(Home, new GitAccessEdit { Repository = "engine", Set = [new("write", "deny")] });
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "work.txt", "the session's work", "the work");

        var landed = await trees.LandAsync(opened.Path, new LandingSubject("s1a2b3c4", "0fda18", "Fix"), gate: Lands);

        Assert.True(landed.Landed, landed.Message);
        Assert.Equal(0, asked);
        var plugin = Assert.IsType<PluginLanding>(landed.Plugin);
        Assert.True(plugin.Failed);
        Assert.Contains("git access denies writes to origin", plugin.Message, StringComparison.Ordinal);
        Assert.Contains("the work", await GitAsync(root, "log", "feature/0fda18-fix", "--oneline"));

        GitAccessActs.Apply(Home, new GitAccessEdit { Repository = "engine", Set = [new("write", "ask")] });
        var again = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(again.Path, "more.txt", "more", "more work");
        var accepted = await trees.LandAsync(again.Path, new LandingSubject("s5e6f7a8", "1abc23", "More"), gate: Lands);
        Assert.True(accepted.Landed, accepted.Message);
        Assert.Equal(1, asked);
    }

    /// <summary>
    /// A plugin that fails, refuses, times out or does not push never undoes the branch: the branch
    /// stands, the landing is still a landing, and the sentence says the plugin's step failed and how
    /// the person does it by hand.
    /// </summary>
    [Theory]
    [InlineData("throws")]
    [InlineData("did-not-push")]
    [InlineData("will-not-start")]
    public async Task A_plugin_that_fails_leaves_the_branch_standing_and_says_so(string how)
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        InstallManifest();
        Rule(GraphLand.PullRequest("feature/{quest}-{slug}", Id));
        var trees = new SessionTrees(Home, how == "will-not-start"
            ? new LandingPlugins(Home, start: (_, _, _) => throw new DriverException($"plugin `{Id}`'s hook process could not start — `node`: not found"))
            : Plugins(_ => how == "throws"
                ? throw new DriverException($"plugin `{Id}` did not answer `hook/work/land` within 1s.")
                : Answer(false, null, "gh is not signed in — run `gh auth login`.")));
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "work.txt", "the session's work", "the work");

        var landed = await trees.LandAsync(opened.Path, new LandingSubject("s1a2b3c4", "0fda18", "Fix"), gate: Lands);

        Assert.True(landed.Landed, landed.Message);
        Assert.Contains("the work", await GitAsync(root, "log", "feature/0fda18-fix", "--oneline"));
        var plugin = Assert.IsType<PluginLanding>(landed.Plugin);
        Assert.False(plugin.Pushed);
        Assert.Equal(how != "did-not-push", plugin.Failed);
        Assert.Contains(how == "did-not-push" ? "did not push it" : "step failed", landed.Message);
        Assert.Contains("git push -u origin feature/0fda18-fix", landed.Message);
        Assert.Contains("The branch stands", landed.Message);
    }

    /// <summary>The review says who pushes before the press, and what stands in the way when something does.</summary>
    [Fact]
    public async Task The_plan_names_the_plugin_and_its_problem_before_the_press()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        Rule(GraphLand.PullRequest("feature/{quest}-{slug}", Id));
        var trees = new SessionTrees(Home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");

        var missing = await trees.PlanAsync(opened.Path, new LandingSubject("s1a2b3c4", "0fda18", "Fix"), process: Lands!.Process);
        Assert.Equal(Id, missing.Plugin);
        Assert.Contains("not installed", missing.Problem);

        InstallManifest();
        var ready = await trees.PlanAsync(opened.Path, new LandingSubject("s1a2b3c4", "0fda18", "Fix"), process: Lands.Process);
        Assert.Equal((LandingForm.Branch, "feature/0fda18-fix", Id, (string?)null), (ready.Form, ready.Target, ready.Plugin, ready.Problem));
    }

    /// <summary>What the conversation's record keeps of a landing: the whole sentence, the plugin's part in it.</summary>
    [Fact]
    public void A_landing_is_kept_as_a_note_in_the_conversation()
    {
        var note = LandingNotes.Note(new TreeLanding(true, "put the work on `feature/x`. Plugin `example.lands`: pushed it.", "feature/x"));

        Assert.Equal(SessionEventKind.Note, note.Kind);
        Assert.StartsWith("the person accepted this work:", note.Text);
        Assert.Contains("Plugin `example.lands`: pushed it.", note.Text);
        // CONVNOTE2c: the lead is a code the page words; the landing's sentence, with git's and the plugin's words, rides as a program's.
        Assert.Equal(["landing.person-accepted", null], note.Parts!.Select(part => part.Code).ToArray());
        Assert.Equal(("put the work on `feature/x`. Plugin `example.lands`: pushed it.", NoteBy.Program), (note.Parts![1].Words, note.Parts![1].By));
        NoteAssert.Holds(note.Text, note.Parts);
    }
}

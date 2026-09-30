using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WSR4 (D100): a branch rule may name a plugin, and once Daoris has made the branch the plugin is
/// spoken to on <c>work/land</c> — it pushes and opens the pull request for its platform, and Daoris
/// runs neither. A plugin that fails never undoes the branch.
/// </summary>
/// <remarks>
/// 🔴 <b>Nothing here reaches a network.</b> `origin` is a bare repository under the scratch folder,
/// and the plugin that pushes pushes there.
/// </remarks>
public sealed class LandingPluginTests : IDisposable
{
    private const string Id = "example.lands";

    private readonly string _scratch;
    private readonly string _home;

    public LandingPluginTests()
    {
        _scratch = Path.Combine(RepoRoot(), "_fixtures", "landing-plugin", Guid.NewGuid().ToString("N")[..8]);
        _home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(_home);
    }

    public void Dispose()
    {
        // A real plugin's process stands in its folder a moment after it is told to go (FLAKE1).
        for (var attempt = 0; Directory.Exists(_scratch); attempt++)
        {
            try
            {
                Directory.Delete(_scratch, recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                if (attempt >= 20) return;
                Thread.Sleep(250);
            }
        }
    }

    /// <summary>
    /// A real plugin, started as a landing starts one: it sees the branch already made, pushes it to
    /// origin, and answers with the pull request — and all of it is said where the landing is said.
    /// </summary>
    [Fact]
    public async Task The_plugin_is_spoken_to_once_the_branch_exists_and_its_answer_is_said_and_kept()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        InstallRealPlugin();
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Tidy: true, Plugin: Id));
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "work.txt", "the session's work", "the work");

        var landed = await trees.LandAsync(opened.Path, new LandingSubject("s1a2b3c4", "0fda18", "Fix the API gap"));

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
        // Branch, then plugin, then the tidy the rule asked for.
        Assert.Contains("Tidied", landed.Message);
        Assert.False(Directory.Exists(opened.Path));

        // What the plugin was told, and that the branch was already there when it was.
        var told = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(_home, "plugins", ".data", Id, "landed.json"))).RootElement;
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
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Id));
        InstallManifest();
        var trees = new SessionTrees(_home, Plugins(_ => { asked++; return Answer(true, null, "pushed"); }));
        var opened = await trees.OpenAsync(root, "engine", "aurora");

        var nothing = await trees.LandAsync(opened.Path, new LandingSubject("s1a2b3c4", "0fda18", "Fix"));
        Assert.False(nothing.Landed);

        await CommitAsync(opened.Path, "work.txt", "the session's work", "the work");
        await GitAsync(root, "branch", "feature/0fda18-fix");
        var taken = await trees.LandAsync(opened.Path, new LandingSubject("s1a2b3c4", "0fda18", "Fix"));
        Assert.False(taken.Landed);
        Assert.Contains("already a branch", taken.Message);

        Assert.Equal(0, asked);
    }

    /// <summary>The plugin checked again at the press: one gone since the rule was set refuses before anything is made.</summary>
    [Fact]
    public async Task A_rule_whose_plugin_is_gone_or_off_is_refused_at_the_press_and_nothing_is_made()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Id));
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "work.txt", "the session's work", "the work");

        var missing = await trees.LandAsync(opened.Path, new LandingSubject("s1a2b3c4", "0fda18", "Fix"));
        Assert.False(missing.Landed);
        Assert.Contains("not installed", missing.Message);

        InstallManifest();
        PluginState.Disable(_home, Id);
        var off = await trees.LandAsync(opened.Path, new LandingSubject("s1a2b3c4", "0fda18", "Fix"));
        Assert.False(off.Landed);
        Assert.Contains($"daoris plugin enable {Id}", off.Message);

        Assert.Empty((await GitAsync(root, "branch", "--list", "feature/*")).Trim());
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
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Id));
        var trees = new SessionTrees(_home, how == "will-not-start"
            ? new LandingPlugins(_home, start: (_, _, _) => throw new DriverException($"plugin `{Id}`'s hook process could not start — `node`: not found"))
            : Plugins(_ => how == "throws"
                ? throw new DriverException($"plugin `{Id}` did not answer `hook/work/land` within 1s.")
                : Answer(false, null, "gh is not signed in — run `gh auth login`.")));
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "work.txt", "the session's work", "the work");

        var landed = await trees.LandAsync(opened.Path, new LandingSubject("s1a2b3c4", "0fda18", "Fix"));

        Assert.True(landed.Landed, landed.Message);
        Assert.Contains("the work", await GitAsync(root, "log", "feature/0fda18-fix", "--oneline"));
        var plugin = Assert.IsType<PluginLanding>(landed.Plugin);
        Assert.False(plugin.Pushed);
        Assert.Equal(how != "did-not-push", plugin.Failed);
        Assert.Contains(how == "did-not-push" ? "did not push it" : "step failed", landed.Message);
        Assert.Contains("git push -u origin feature/0fda18-fix", landed.Message);
        Assert.Contains("The branch stands", landed.Message);
    }

    /// <summary>A real process that never answers is a timeout, and the branch stands.</summary>
    [Fact]
    public async Task A_plugin_that_never_answers_times_out_and_the_branch_stands()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        InstallRealPlugin(silent: true);
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Id));
        var trees = new SessionTrees(_home, new LandingPlugins(_home, patience: TimeSpan.FromSeconds(3)));
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "work.txt", "the session's work", "the work");

        var landed = await trees.LandAsync(opened.Path, new LandingSubject("s1a2b3c4", "0fda18", "Fix"));

        Assert.True(landed.Landed, landed.Message);
        Assert.True(landed.Plugin!.Failed);
        Assert.Contains("did not answer `hook/work/land`", landed.Message);
        Assert.NotEmpty((await GitAsync(root, "branch", "--list", "feature/0fda18-fix")).Trim());
    }

    /// <summary>The review says who pushes before the press, and what stands in the way when something does.</summary>
    [Fact]
    public async Task The_plan_names_the_plugin_and_its_problem_before_the_press()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Id), workspace: true);
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");

        var missing = await trees.PlanAsync(opened.Path, new LandingSubject("s1a2b3c4", "0fda18", "Fix"));
        Assert.Equal(Id, missing.Plugin);
        Assert.Contains("not installed", missing.Problem);

        InstallManifest();
        var ready = await trees.PlanAsync(opened.Path, new LandingSubject("s1a2b3c4", "0fda18", "Fix"));
        Assert.Equal(new LandingPlan(LandingForm.Branch, "feature/0fda18-fix", LandingSource.Workspace, Id), ready);
    }

    /// <summary>What the conversation's record keeps of a landing: the whole sentence, the plugin's part in it.</summary>
    [Fact]
    public void A_landing_is_kept_as_a_note_in_the_conversation()
    {
        var note = LandingRules.Note(new TreeLanding(true, "put the work on `feature/x`. Plugin `example.lands`: pushed it.", "feature/x"));

        Assert.Equal(SessionEventKind.Note, note.Kind);
        Assert.StartsWith("the person accepted this work:", note.Text);
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

    private static PluginLanding Answer(bool pushed, string? pullRequest, string message) => new(Id, pushed, pullRequest, message);

    private LandingPlugins Plugins(Func<object, PluginLanding> land) =>
        new(_home, start: (_, _, _) => Task.FromResult<IHookChannel>(new FakeLander(land)));

    private void InstallManifest() => Install("""{ "id": "example.lands", "hooks": { "command": ["node", "${plugin}/land.mjs"], "points": ["work/land"] } }""");

    /// <summary>
    /// A plugin that answers the way a real one does: it records what it was told and whether the
    /// branch was already there, pushes to origin (a bare repository beside the test), and answers.
    /// </summary>
    private void InstallRealPlugin(bool silent = false)
    {
        InstallManifest();
        File.WriteAllText(Path.Combine(_home, "plugins", Id, "land.mjs"), $$"""
            import { spawnSync } from 'node:child_process';
            import { mkdirSync, writeFileSync } from 'node:fs';
            import { join } from 'node:path';
            import { createInterface } from 'node:readline';
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const silent = {{(silent ? "true" : "false")}};
            for await (const line of createInterface({ input: process.stdin })) {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, points: frame.params.points } });
              } else if (frame.method === 'hook/work/land') {
                if (silent) continue;
                const p = frame.params;
                const existed = spawnSync('git', ['rev-parse', '--verify', '--quiet', 'refs/heads/' + p.branch], { cwd: p.root }).status === 0;
                const data = process.env.DAORIS_PLUGIN_DATA;
                mkdirSync(data, { recursive: true });
                writeFileSync(join(data, 'landed.json'), JSON.stringify({ branchExisted: existed, frame: p }));
                const pushed = spawnSync('git', ['push', '--quiet', '-u', 'origin', p.branch], { cwd: p.root }).status === 0;
                send({ jsonrpc: '2.0', id: frame.id, result: { pushed, pullRequest: 'https://example.test/example-org/engine/pull/7', message: 'pushed it to origin and opened the pull request.' } });
              } else if (frame.method === 'shutdown') {
                process.exit(0);
              }
            }
            """);
    }

    private void Install(string manifest)
    {
        var folder = Path.Combine(_home, "plugins", Id);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"), manifest);
    }

    private void Rule(LandingRule rule, bool workspace = false) =>
        (workspace ? DriverConfig.Empty.WithWorkspaceLanding("aurora", rule) : DriverConfig.Empty.WithLanding("engine", rule))
            .Save(Path.Combine(_home, "driver.json"));

    /// <summary>A repository whose `origin` is a bare repository beside it — somewhere a push lands without leaving the machine.</summary>
    private async Task<(string Root, string Origin)> RepositoryWithOriginAsync(string name)
    {
        var origin = Path.Combine(_scratch, $"{name}-origin.git");
        Directory.CreateDirectory(origin);
        await GitAsync(origin, "init", "--quiet", "--bare", "-b", "main");

        var root = Path.Combine(_scratch, name);
        Directory.CreateDirectory(root);
        await GitAsync(root, "init", "--quiet", "-b", "main");
        await GitAsync(root, "config", "user.email", "fixture@example.test");
        await GitAsync(root, "config", "user.name", "Fixture");
        await CommitAsync(root, "README.md", $"# {name}", "first");
        await GitAsync(root, "remote", "add", "origin", origin);
        return (root, origin);
    }

    private static async Task CommitAsync(string tree, string file, string content, string message)
    {
        await File.WriteAllTextAsync(Path.Combine(tree, file), content + "\n");
        await GitAsync(tree, "add", "-A");
        await GitAsync(tree, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", message);
    }

    private static async Task<string> GitAsync(string cwd, params string[] arguments)
    {
        var info = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return stdout;
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? AppContext.BaseDirectory;
    }
}

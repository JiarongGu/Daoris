using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A plugin's tools checked at a trial (PLUGTOOL1a, D150 point 7; the UX6 design §7.2–§7.3): each declared tool found where
/// the tools say (the way in <c>tools.json</c>, else the system's PATH), asked its version against its range, then each of
/// its checks run one at a time with ten seconds each. A stand-in answers for every program, so nothing here starts one:
/// what a real start does is <see cref="PluginKitTests"/>' (<c>Process</c>), through <c>plugins try</c>.
/// </summary>
/// <remarks>
/// 🔴 <b>A problem is said, never a refusal</b>, and 🔴 <b>Daoris never runs <c>fix</c></b>.
/// </remarks>
public sealed class PluginToolCheckTests : IDisposable
{
    private readonly string _scratch = Path.Combine(Path.GetTempPath(), "daoris-tool-check-" + Guid.NewGuid().ToString("N")[..8]);

    public PluginToolCheckTests()
    {
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(Bin);
        Directory.CreateDirectory(Root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_scratch, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Home => Path.Combine(_scratch, "home");

    /// <summary>The PATH a check finds the system's tools on: this folder alone, so the machine's own never answer.</summary>
    private string Bin => Path.Combine(_scratch, "bin");

    /// <summary>Where a check runs: a trial's scratch folder.</summary>
    private string Root => Path.Combine(_scratch, "root");

    /// <summary>
    /// A program on the test's PATH: an empty file by the name a start would find, never run. Found by PATHEXT, which spells
    /// the extension its own way (<c>.EXE</c>), so a found path is compared as Windows compares it.
    /// </summary>
    private string OnPath(string name)
    {
        var file = Path.Combine(Bin, OperatingSystem.IsWindows() ? name + ".exe" : name);
        File.WriteAllText(file, "");
        return file;
    }

    private static PluginTool Tool(string json)
    {
        using var document = JsonDocument.Parse($"[{json}]");
        return PluginTools.Read(document.RootElement).Tools.Single();
    }

    private Task<PluginToolFound> CheckAsync(PluginTool tool, Runner runner) =>
        PluginToolChecks.CheckAsync(Home, tool, Root, Bin, runner.RunAsync, CancellationToken.None);

    [Fact]
    public async Task A_known_tool_on_the_path_in_its_range_with_its_checks_ready()
    {
        var git = OnPath("git");
        var runner = new Runner(info => info.ArgumentList is ["--version"] ? Said("git version 2.53.0.windows.1\n") : Said(""));
        var tool = Tool("""
            { "id": "git", "versions": ">=2.29", "ready": [
              { "run": ["git", "config", "--global", "user.name"], "says": "It knows who you are", "fix": "git config --global user.name <name>" },
              { "run": ["git", "--version"], "says": "It answers" } ] }
            """);

        var found = await CheckAsync(tool, runner);

        Assert.True(found.Ok, found.Sentence);
        Assert.Equal("Git 2.53.0, the system's, in its range (2.29 or newer).", found.Sentence);
        Assert.Equal(git, found.File, ignoreCase: true);
        Assert.Equal(ToolWay.System, found.Way);
        Assert.Equal("2.53.0", found.Version);
        Assert.Equal(["ready: It knows who you are.", "ready: It answers."], found.Ready.Select(answer => answer.Sentence));
        Assert.All(found.Ready, answer => Assert.True(answer.Ready));

        // Its version is asked of the file found, by Daoris's own question; then each check, one at a time, in order.
        Assert.Equal([["--version"], ["config", "--global", "user.name"], ["--version"]], runner.Calls.Select(call => call.Info.ArgumentList.ToArray()));
        Assert.All(runner.Calls, call => Assert.Equal(git, call.Info.FileName, ignoreCase: true));
        Assert.All(runner.Calls, call => Assert.Equal(TimeSpan.FromSeconds(10), call.Patience));
    }

    /// <summary>A check runs where the trial's scratch is, with git told there is no repository there: git walks up.</summary>
    [Fact]
    public async Task A_check_runs_in_the_scratch_folder_where_git_finds_no_repository_and_nothing_is_typed_to_it()
    {
        OnPath("gh");
        var runner = new Runner(_ => Said(""));

        await CheckAsync(Tool("""{ "id": "gh", "ready": [{ "run": ["gh", "auth", "status"], "says": "Signed in" }] }"""), runner);

        var check = runner.Calls[^1].Info;
        Assert.Equal(Root, check.WorkingDirectory);
        Assert.Equal(Path.Combine(Root, ".git"), check.Environment["GIT_DIR"]);
        Assert.Equal("0", check.Environment["GIT_TERMINAL_PROMPT"]);
        Assert.True(check.RedirectStandardInput);
        Assert.True(check.CreateNoWindow);
        Assert.False(check.UseShellExecute);
    }

    [Fact]
    public async Task A_version_outside_its_range_is_said_in_the_pages_words_and_its_checks_still_run()
    {
        OnPath("az");
        var runner = new Runner(info => info.ArgumentList is ["version"] ? Said("""{ "azure-cli": "2.55.0", "azure-cli-core": "2.55.0" }""") : Said(""));

        var found = await CheckAsync(Tool("""{ "id": "az", "versions": ">=2.60 <3", "ready": [{ "run": ["az", "account", "show"], "says": "Signed in" }] }"""), runner);

        Assert.False(found.Ok);
        Assert.Equal("Azure CLI 2.55.0, the system's: needs 2.60 or newer, below 3; this is 2.55.0.", found.Sentence);
        Assert.True(Assert.Single(found.Ready).Ready);
    }

    [Fact]
    public async Task A_tool_the_path_does_not_have_is_said_in_the_tools_words_and_nothing_is_started()
    {
        var runner = new Runner(_ => throw new InvalidOperationException("nothing is started for a tool that is not there"));

        var found = await CheckAsync(Tool("""{ "id": "gh", "ready": [{ "run": ["gh", "auth", "status"], "says": "Signed in", "fix": "gh auth login" }] }"""), runner);

        Assert.False(found.Ok);
        Assert.StartsWith("`gh` is not on this machine's PATH.", found.Sentence);
        Assert.EndsWith("Its checks were not run.", found.Sentence);
        Assert.Empty(found.Ready);
        Assert.Empty(runner.Calls);
    }

    /// <summary>The way the person chose decides (D121 §2.3): a managed version nobody downloaded refuses, and never falls back to PATH's.</summary>
    [Fact]
    public async Task A_managed_version_nobody_downloaded_is_refused_and_the_paths_is_never_taken()
    {
        OnPath("az");
        File.WriteAllText(Path.Combine(Home, Tools.FileName), """{ "tools": { "az": { "use": "managed", "version": "2.66.0" } } }""");
        var runner = new Runner(_ => throw new InvalidOperationException("nothing is started for a way that cannot run"));

        var found = await CheckAsync(Tool("""{ "id": "az", "versions": ">=2.60" }"""), runner);

        Assert.False(found.Ok);
        Assert.Contains("2.66.0", found.Sentence);
        Assert.Equal(ToolWay.Managed, Tools.Resolve(Home, "az", Bin).Way);
        Assert.Null(found.File);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task A_tool_Daoris_does_not_know_is_found_by_its_command_and_its_version_read_only_where_it_says_how()
    {
        var terraform = OnPath("terraform");
        var runner = new Runner(info => info.ArgumentList is ["version"] ? Said("Terraform v1.9.8\non windows_amd64\n") : Said(""));

        var unread = await CheckAsync(Tool("""{ "id": "tf", "name": "Terraform", "command": "terraform", "versions": ">=1.5" }"""), runner);
        Assert.True(unread.Ok, unread.Sentence);
        Assert.Equal("Terraform, the system's; its version is not read, so `>=1.5` is not checked.", unread.Sentence);
        Assert.Equal(terraform, unread.File, ignoreCase: true);
        Assert.Empty(runner.Calls);

        var read = await CheckAsync(Tool("""{ "id": "tf", "name": "Terraform", "command": "terraform", "versionArguments": ["version"], "versions": ">=1.5" }"""), runner);
        Assert.True(read.Ok, read.Sentence);
        Assert.Equal("Terraform 1.9.8, the system's, in its range (1.5 or newer).", read.Sentence);
    }

    [Fact]
    public async Task A_tool_Daoris_does_not_know_and_the_path_does_not_have_is_found_nowhere_else()
    {
        var found = await CheckAsync(Tool("""{ "id": "terraform" }"""), new Runner(_ => Said("")));

        Assert.False(found.Ok);
        Assert.Equal("`terraform` is not on this machine's PATH: a tool Daoris does not know is found there, and never downloaded.", found.Sentence);
    }

    /// <summary>A version question that answers nothing fails only where a range needs the answer.</summary>
    [Fact]
    public async Task A_version_question_that_answers_nothing_fails_only_a_range()
    {
        OnPath("gh");
        var runner = new Runner(_ => Said("no numbers here"));

        var ranged = await CheckAsync(Tool("""{ "id": "gh", "versions": ">=2.40" }"""), runner);
        Assert.False(ranged.Ok);
        Assert.Equal("GitHub CLI, the system's: it answered no version to `gh --version`, so `>=2.40` is not checked.", ranged.Sentence);

        var any = await CheckAsync(Tool("""{ "id": "gh" }"""), runner);
        Assert.True(any.Ok, any.Sentence);
        Assert.Equal("GitHub CLI, the system's; it answered no version to `gh --version`.", any.Sentence);

        var silent = await CheckAsync(Tool("""{ "id": "gh", "versions": ">=2.40" }"""), new Runner(_ => new CheckRun(true, null, "", "")));
        Assert.Equal("GitHub CLI, the system's: it did not answer `gh --version` within 10s, so `>=2.40` is not checked.", silent.Sentence);
    }

    [Fact]
    public async Task A_check_that_does_not_pass_says_how_it_ended_its_last_words_and_the_fix_the_person_runs()
    {
        OnPath("az");
        var runner = new Runner(info => info.ArgumentList is ["account", "show", "--output", "none"]
            ? new CheckRun(true, 1, "", "WARNING: something\nERROR: Please run 'az login' to setup account.\n")
            : Said("""{ "azure-cli": "2.66.0" }"""));

        var found = await CheckAsync(Tool("""
            { "id": "az", "ready": [
              { "run": ["az", "account", "show", "--output", "none"], "says": "Signed in.", "fix": "az login" },
              { "run": ["az", "extension", "show", "--name", "azure-devops"], "says": "Its devops extension is added" } ] }
            """), runner);

        // The tool itself is there; a check that does not pass is that check's sentence.
        Assert.True(found.Ok, found.Sentence);
        var signedIn = found.Ready[0];
        Assert.False(signedIn.Ready);
        Assert.Equal(
            "not ready: Signed in. `az account show --output none` exited 1: `ERROR: Please run 'az login' to setup account.`. "
            + "Run `az login` to put it right.",
            signedIn.Sentence);
        Assert.True(found.Ready[1].Ready);

        // 🔴 The fix is shown, never run.
        Assert.DoesNotContain(runner.Calls, call => call.Info.ArgumentList is ["login"]);
    }

    [Fact]
    public async Task A_check_that_does_not_answer_in_ten_seconds_or_cannot_start_says_so()
    {
        OnPath("gh");
        var tool = Tool("""{ "id": "gh", "ready": [{ "run": ["gh", "auth", "status"], "says": "Signed in" }] }""");

        var silent = await CheckAsync(tool, new Runner(info => info.ArgumentList is ["--version"] ? Said("gh version 2.63.0") : new CheckRun(true, null, "", "")));
        Assert.Equal("not ready: Signed in. `gh auth status` did not answer within 10s.", Assert.Single(silent.Ready).Sentence);

        var gone = await CheckAsync(tool, new Runner(info => info.ArgumentList is ["--version"] ? Said("gh version 2.63.0") : new CheckRun(false, null, "", "")));
        Assert.Equal("not ready: Signed in. `gh` could not be started.", Assert.Single(gone.Ready).Sentence);
    }

    /// <summary>A check's first word is found as a hook's is (TOOLS5): a name a tool answers for is that tool's file, any other the PATH's.</summary>
    [Fact]
    public async Task A_checks_first_word_is_the_tools_file_where_a_tool_answers_for_it_and_else_the_paths()
    {
        var node = OnPath("node");
        var jq = OnPath("jq");
        var runner = new Runner(_ => Said("v24.1.0"));

        var found = await CheckAsync(Tool("""
            { "id": "terraform", "command": "jq", "ready": [
              { "run": ["node", "-e", "0"], "says": "Node answers" },
              { "run": ["jq", "--version"], "says": "jq answers" },
              { "run": ["nowhere-to-be-found", "--version"], "says": "It is there" } ] }
            """), runner);

        Assert.Equal(node, runner.Calls[0].Info.FileName, ignoreCase: true);
        Assert.Equal(jq, runner.Calls[1].Info.FileName, ignoreCase: true);
        Assert.Equal(2, runner.Calls.Count);
        Assert.Equal("not ready: It is there. `nowhere-to-be-found` is not on this machine's PATH.", found.Ready[2].Sentence);
    }

    [Fact]
    public async Task A_tool_whose_entry_does_not_read_says_its_problem_and_starts_nothing()
    {
        var runner = new Runner(_ => throw new InvalidOperationException("nothing is started for an entry that does not read"));

        var found = await CheckAsync(Tool("""{ "id": "az", "versions": "latest" }"""), runner);

        Assert.False(found.Ok);
        Assert.Equal("tool `az`'s `versions` must be a range: `>=2.60`, `>=2.60 <3`, or one exact version.", found.Sentence);
        Assert.Empty(runner.Calls);
    }

    // ——— the trial's steps

    /// <summary>
    /// At a trial each tool is a step of its own, then each of its checks, so `plugins try` says each problem in its own
    /// sentence; the field's problem is a step too. None of them stops the trial (PLUGTOOL1a).
    /// </summary>
    [Fact]
    public async Task At_a_trial_each_tool_and_each_check_is_a_step_in_its_own_sentence()
    {
        OnPath("git");
        var runner = new Runner(info => info.ArgumentList is ["--version"] ? Said("git version 2.53.0") : new CheckRun(true, 2, "", "nope"));
        var folder = Path.Combine(Home, PluginCatalog.Folder, "acme.lands");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName), """
            { "id": "acme.lands", "hooks": { "command": ["node", "lands.mjs"], "points": ["work/land"] }, "tools": [
              { "id": "git", "versions": ">=2.29", "ready": [{ "run": ["git", "status"], "says": "It reads a repository" }] },
              { "id": 7 },
              { "id": "gh" } ] }
            """);
        var manifest = Assert.Single(PluginCatalog.Load(Home).Plugins).Manifest;

        var steps = await PluginKit.ToolStepsAsync(manifest, Home, Root, Bin, runner.RunAsync, CancellationToken.None);

        Assert.Equal(["tool git", "git check 1", "tool 2", "tool gh"], steps.Select(step => step.Name));
        Assert.Equal([true, false, false, false], steps.Select(step => step.Ok));
        Assert.Equal("not ready: It reads a repository. `git status` exited 2: `nope`.", steps[1].Sentence);
        Assert.Equal("tool 2 in `tools` needs an `id`: a tool's name in lowercase, like `az`.", steps[2].Sentence);
        Assert.StartsWith("`gh` is not on this machine's PATH.", steps[3].Sentence);
    }

    [Fact]
    public async Task At_a_trial_tools_that_are_not_an_array_are_one_step_and_none_declared_are_none()
    {
        var runner = new Runner(_ => Said(""));
        var none = PluginManifest.Empty("acme.none");
        var wrong = none with { ToolsProblem = "`tools` must be an array of the tools the plugin runs, each an object with an `id`." };

        Assert.Empty(await PluginKit.ToolStepsAsync(none, Home, Root, Bin, runner.RunAsync, CancellationToken.None));
        var step = Assert.Single(await PluginKit.ToolStepsAsync(wrong, Home, Root, Bin, runner.RunAsync, CancellationToken.None));
        Assert.Equal("tools", step.Name);
        Assert.False(step.Ok);
        Assert.Empty(runner.Calls);
    }

    // ——— the kit's scaffold

    /// <summary>`plugins new` writes `tools: []`, a manifest the catalogue reads, and the README says how to fill it (§7.2).</summary>
    [Fact]
    public void A_new_plugin_declares_no_tools_and_its_readme_says_how_to_declare_them()
    {
        var plan = PluginKit.Plan("acme.quiet-hours", [HookPoints.QuestConsider], _scratch);
        var manifest = plan.Files.Single(file => file.Name == PluginCatalog.ManifestName).Content;
        var readme = plan.Files.Single(file => file.Name == PluginKit.Readme).Content;

        Assert.Contains("\"tools\": []", manifest);
        using (var document = JsonDocument.Parse(manifest))
        {
            Assert.Equal(JsonValueKind.Array, document.RootElement.GetProperty("tools").ValueKind);
        }

        Assert.Contains("## The tools it runs", readme);
        Assert.Contains("`versions`", readme);
        Assert.Contains("`ready`", readme);
        Assert.Contains("never runs `fix`", readme);
        Assert.Contains("daoris-driver plugins try", readme);
        Assert.DoesNotContain("{{", readme);
        Assert.False(Directory.Exists(plan.Folder));
    }

    private static CheckRun Said(string stdout) => new(true, 0, stdout, "");

    /// <summary>A stand-in for every program a check would start: it records each start and answers from its table.</summary>
    private sealed class Runner(Func<ProcessStartInfo, CheckRun> answer)
    {
        public List<(ProcessStartInfo Info, TimeSpan Patience)> Calls { get; } = [];

        public Task<CheckRun> RunAsync(ProcessStartInfo info, TimeSpan patience, CancellationToken ct)
        {
            var said = answer(info);
            Calls.Add((info, patience));
            return Task.FromResult(said);
        }
    }
}

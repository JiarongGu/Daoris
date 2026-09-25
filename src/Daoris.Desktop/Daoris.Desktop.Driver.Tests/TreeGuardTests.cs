using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The tree guard (PERM3): a PreToolUse hook Daoris ships, refusing a file write outside the session's
/// own tree — the one boundary a permission rule cannot say (design §3).
/// </summary>
/// <remarks>
/// <para>The hook is a node script, because it runs inside every session on the harness's side of the
/// wire. It is tested as what it is: the judgement as a pure function called through node, and the
/// hook as a REAL process reading the harness's JSON on stdin and answering on stdout.</para>
///
/// <para>🔴 It refuses STRUCTURALLY — `permissionDecision: "deny"` on stdout with exit 0 — never by an
/// exit code. HELP3's probe 4 saw PowerShell 5.1 collapse a native exit code, and the harness reads any
/// exit but 2 as a non-blocking error: a guard that failed by exiting would let the write through.</para>
/// </remarks>
public sealed class TreeGuardTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-tree-guard-" + Guid.NewGuid().ToString("N")[..8]);

    public TreeGuardTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    // ——— The script, where the hook names it.

    [Fact]
    public void The_script_is_written_under_the_home_once_and_as_shipped()
    {
        var first = TreeGuard.Install(_home);
        var written = File.GetLastWriteTimeUtc(first);
        var again = TreeGuard.Install(_home);

        Assert.Equal(Path.Combine(_home, TreeGuard.Folder, TreeGuard.ScriptName), first);
        Assert.Equal(first, again);
        Assert.Equal(written, File.GetLastWriteTimeUtc(again));
        var text = File.ReadAllText(first);
        Assert.Equal(TreeGuard.Source, text);
        Assert.DoesNotContain("\r", text);
    }

    [Fact]
    public void A_script_somebody_changed_is_put_back_as_shipped()
    {
        var path = TreeGuard.Install(_home);
        File.WriteAllText(path, "// allow everything\n");

        TreeGuard.Install(_home);

        Assert.Equal(TreeGuard.Source, File.ReadAllText(path));
    }

    // ——— The judgement.

    /// <summary>
    /// Inside or outside, on a Windows machine's paths whatever machine runs the test — the platform is
    /// the judgement's argument, not the test host's. The prefix trap (`engine-old` beside `engine`) is
    /// the classic string-compare bug a path guard ships with.
    /// </summary>
    [Fact]
    public async Task The_judgement_on_windows_paths()
    {
        const string tree = "D:/work/engine";
        (string Target, string? Cwd, bool Outside)[] cases =
        [
            ("D:/work/engine/src/a.ts", null, false),
            ("D:\\work\\engine\\src\\a.ts", null, false),
            ("D:/work/engine", null, false),
            ("D:/work/other/a.ts", null, true),
            ("D:/work/engine/../other/a.ts", null, true),
            ("D:/work/engine-old/a.ts", null, true),
            ("E:/work/engine/a.ts", null, true),
            ("d:/WORK/Engine/README.md", null, false),
            ("\\\\server\\share\\engine\\a.ts", null, true),
            ("src/a.ts", "D:/work/engine", false),
            ("../other/a.ts", "D:/work/engine", true),
            ("src/a.ts", "D:/elsewhere", true),
        ];

        var judged = await Judge("win32", tree, cases.Select(c => (c.Target, c.Cwd)).ToList());

        for (var i = 0; i < cases.Length; i++)
        {
            Assert.True(cases[i].Outside == judged[i], $"`{cases[i].Target}` (cwd {cases[i].Cwd ?? "none"}) judged outside={judged[i]}");
        }
    }

    [Fact]
    public async Task The_judgement_on_posix_paths_is_case_sensitive()
    {
        (string Target, string? Cwd, bool Outside)[] cases =
        [
            ("/srv/engine/a.ts", null, false),
            ("/srv/other/a.ts", null, true),
            ("/srv/Engine/a.ts", null, true),
            ("/srv/engine/../other/a.ts", null, true),
        ];

        var judged = await Judge("posix", "/srv/engine", cases.Select(c => (c.Target, c.Cwd)).ToList());

        for (var i = 0; i < cases.Length; i++) Assert.True(cases[i].Outside == judged[i], cases[i].Target);
    }

    // ——— The hook, as the harness runs it.

    [Fact]
    public async Task A_write_outside_the_tree_is_denied_in_the_harness_own_shape_with_exit_zero()
    {
        var (tree, outside) = Trees();

        var (code, output) = await Hook(tree, Call("Write", new JsonObject { ["file_path"] = Path.Combine(outside, "a.txt") }, tree));

        Assert.Equal(0, code);
        var decision = JsonNode.Parse(output)!["hookSpecificOutput"]!;
        Assert.Equal("PreToolUse", (string?)decision["hookEventName"]);
        Assert.Equal("deny", (string?)decision["permissionDecision"]);
        var reason = (string?)decision["permissionDecisionReason"] ?? "";
        Assert.Contains("outside", reason);
        Assert.Contains("quest", reason);
    }

    /// <summary>Inside, the guard says nothing — never "allow", which would lift the harness's own asking.</summary>
    [Fact]
    public async Task A_write_inside_the_tree_gets_no_decision_at_all()
    {
        var (tree, _) = Trees();

        var (code, output) = await Hook(tree, Call("Edit", new JsonObject { ["file_path"] = Path.Combine(tree, "src", "a.ts") }, tree));

        Assert.Equal(0, code);
        Assert.Equal("", output.Trim());
    }

    [Fact]
    public async Task A_notebook_and_a_multi_edit_are_judged_by_their_own_path_fields()
    {
        var (tree, outside) = Trees();

        var notebook = await Hook(tree, Call("NotebookEdit", new JsonObject { ["notebook_path"] = Path.Combine(outside, "n.ipynb") }, tree));
        var multi = await Hook(tree, Call("MultiEdit", new JsonObject
        {
            ["file_path"] = Path.Combine(tree, "a.ts"),
            ["edits"] = new JsonArray(new JsonObject { ["file_path"] = Path.Combine(outside, "b.ts") }),
        }, tree));

        Assert.Contains("\"deny\"", notebook.Output);
        Assert.Contains("\"deny\"", multi.Output);
    }

    /// <summary>A tool with no path this guard knows how to judge — a shell command — is not its call.</summary>
    [Fact]
    public async Task A_shell_command_is_not_this_guards_to_judge()
    {
        var (tree, outside) = Trees();

        var (_, output) = await Hook(tree, Call("Bash", new JsonObject { ["command"] = $"echo x > \"{Path.Combine(outside, "a.txt")}\"" }, tree));

        Assert.Equal("", output.Trim());
    }

    /// <summary>
    /// Fail closed where the guard is the one deciding: a call it cannot read is refused, and so is a
    /// session whose tree nobody told it.
    /// </summary>
    [Fact]
    public async Task A_call_it_cannot_read_or_a_tree_it_was_not_told_is_refused()
    {
        var garbled = await Hook(Trees().Tree, "not json at all");
        var untold = await Hook(null, Call("Write", new JsonObject { ["file_path"] = "D:/a.txt" }, cwd: null));

        Assert.Contains("\"deny\"", garbled.Output);
        Assert.Contains("\"deny\"", untold.Output);
        Assert.Equal(0, garbled.Code);
        Assert.Equal(0, untold.Code);
    }

    /// <summary>
    /// 🔴 A link inside the tree that leads out of it is outside. The string is inside; the write is not,
    /// and the harness's own directory check reads the string.
    /// </summary>
    [Fact]
    public async Task A_write_through_a_link_that_leads_out_of_the_tree_is_denied()
    {
        var (tree, outside) = Trees();
        var link = Path.Combine(tree, "escape");
        if (OperatingSystem.IsWindows())
        {
            Run("cmd", "/c", "mklink", "/J", link, outside);
        }
        else
        {
            Directory.CreateSymbolicLink(link, outside);
        }

        Assert.True(Directory.Exists(link), "the link was not made, so this proves nothing");

        var (_, output) = await Hook(tree, Call("Write", new JsonObject { ["file_path"] = Path.Combine(link, "new-file.txt") }, tree));

        Assert.Contains("\"deny\"", output);
    }

    /// <summary>
    /// 🔴 REV3: a link whose target does not exist YET fails to resolve like a missing file, so the walk
    /// treated it as a plain name and judged the write inside — and the write went through it, out of
    /// the tree, creating the target. A dangling link is followed, not climbed past.
    /// </summary>
    [Fact]
    public async Task A_write_through_a_link_to_somewhere_that_does_not_exist_yet_is_denied()
    {
        var (tree, outside) = Trees();
        var link = Path.Combine(tree, "later");
        var nowhere = Path.Combine(outside, "not-yet");
        if (OperatingSystem.IsWindows())
        {
            Run("cmd", "/c", "mklink", "/J", link, nowhere);
        }
        else
        {
            Directory.CreateSymbolicLink(link, nowhere);
        }

        Assert.True(new DirectoryInfo(link).LinkTarget is not null, "the link was not made, so this proves nothing");
        Assert.False(Directory.Exists(nowhere), "the link's target exists, so this is the other test");

        var (_, output) = await Hook(tree, Call("Write", new JsonObject { ["file_path"] = Path.Combine(link, "new-file.txt") }, tree));

        Assert.Contains("\"deny\"", output);
    }

    // ——— Helpers.

    private (string Tree, string Outside) Trees()
    {
        var tree = Path.Combine(_home, "work", "engine");
        var outside = Path.Combine(_home, "work", "other");
        Directory.CreateDirectory(Path.Combine(tree, "src"));
        Directory.CreateDirectory(outside);
        return (tree, outside);
    }

    private static string Call(string tool, JsonObject input, string? cwd) => new JsonObject
    {
        ["session_id"] = "s1",
        ["hook_event_name"] = "PreToolUse",
        ["cwd"] = cwd,
        ["permission_mode"] = "acceptEdits",
        ["tool_name"] = tool,
        ["tool_input"] = input,
        ["tool_use_id"] = "toolu_1",
    }.ToJsonString();

    /// <summary>The hook exactly as the harness runs it: exec form, the tree as one argument, JSON on stdin.</summary>
    private async Task<(int Code, string Output)> Hook(string? tree, string stdin)
    {
        var script = TreeGuard.Install(_home);
        var info = new ProcessStartInfo("node")
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        info.ArgumentList.Add(script);
        if (tree is not null) info.ArgumentList.Add(tree);
        // A test must not inherit a real project directory from whatever launched it.
        info.Environment.Remove("CLAUDE_PROJECT_DIR");

        using var process = Process.Start(info)!;
        await process.StandardInput.WriteAsync(stdin);
        process.StandardInput.Close();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, output);
    }

    /// <summary>The pure judgement, through node, on the platform named rather than the one running.</summary>
    private async Task<bool[]> Judge(string platform, string tree, IReadOnlyList<(string Target, string? Cwd)> cases)
    {
        var script = TreeGuard.Install(_home);
        var harness = Path.Combine(_home, "judge.mjs");
        File.WriteAllText(harness, $$"""
            import { judge } from {{JsonSerializer.Serialize(new Uri(script).AbsoluteUri)}};
            let input = '';
            for await (const chunk of process.stdin) input += chunk;
            const { platform, tree, cases } = JSON.parse(input);
            console.log(JSON.stringify(cases.map(([target, cwd]) => judge({ platform, tree, target, cwd }).outside)));
            """);

        var info = new ProcessStartInfo("node", [harness])
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var process = Process.Start(info)!;
        await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new
        {
            platform,
            tree,
            cases = cases.Select(c => new object?[] { c.Target, c.Cwd }),
        }));
        process.StandardInput.Close();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, error);
        return JsonSerializer.Deserialize<bool[]>(output)!;
    }

    private static void Run(string file, params string[] arguments)
    {
        var info = new ProcessStartInfo(file) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
    }
}

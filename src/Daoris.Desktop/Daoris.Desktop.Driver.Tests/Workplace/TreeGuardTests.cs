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
[Trait(Category.Name, Category.Process)]
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

        Assert.Equal(DaorisHome.Entry(_home, HomeEntry.Hooks, TreeGuard.ScriptName), first);
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

    /// <summary>
    /// D107: a declared write target is one more argument after the tree. A write there gets no decision, as
    /// one in the tree does, and a write anywhere else is still refused, naming what the session may write.
    /// </summary>
    [Fact]
    public async Task A_write_into_a_declared_target_is_let_through_and_anywhere_else_still_refused()
    {
        var (tree, target) = Trees();
        var elsewhere = Path.Combine(_home, "work", "third");
        Directory.CreateDirectory(elsewhere);

        var inTarget = await HookWith([tree, target], Call("Write", new JsonObject { ["file_path"] = Path.Combine(target, "a.txt") }, tree));
        var inTree = await HookWith([tree, target], Call("Edit", new JsonObject { ["file_path"] = Path.Combine(tree, "src", "a.ts") }, tree));
        var outside = await HookWith([tree, target], Call("Write", new JsonObject { ["file_path"] = Path.Combine(elsewhere, "a.txt") }, tree));

        Assert.Equal("", inTarget.Output.Trim());
        Assert.Equal("", inTree.Output.Trim());
        var reason = (string?)JsonNode.Parse(outside.Output)!["hookSpecificOutput"]!["permissionDecisionReason"] ?? "";
        Assert.Contains("outside", reason);
        Assert.Contains(target, reason);
    }

    [Fact]
    public void The_guard_for_a_session_names_its_tree_then_each_declared_target()
    {
        var guard = TreeGuard.For(_home, Path.Combine(_home, "work", "engine"), [Path.Combine(_home, "work", "game")]);

        Assert.Equal(Path.GetFullPath(Path.Combine(_home, "work", "engine")), guard.Tree);
        Assert.Equal([Path.GetFullPath(Path.Combine(_home, "work", "game"))], guard.Also);
        Assert.Empty(TreeGuard.For(_home, _home).Also);
    }

    // ——— A shell's writes (TREESHELL1): the targets it can read, judged as an edit tool's path is.

    /// <summary>
    /// Each form the guard reads, aimed outside the tree and inside it. The outside one is refused in the guard's own
    /// sentence; the inside one gets no decision. <c>{0}</c> is the target, a path with forward slashes, which both
    /// shells and both platforms take.
    /// </summary>
    [Theory]
    [InlineData("Bash", "echo x > \"{0}/a.txt\"")]
    [InlineData("Bash", "echo x >> \"{0}/a.txt\"")]
    [InlineData("Bash", "echo x >\"{0}/a.txt\"")]
    [InlineData("Bash", "echo x 2> \"{0}/a.txt\"")]
    [InlineData("Bash", "echo x &> \"{0}/a.txt\"")]
    [InlineData("Bash", "echo x | tee \"{0}/a.txt\"")]
    [InlineData("Bash", "echo x | tee -a \"{0}/a.txt\"")]
    [InlineData("Bash", "cp a.txt \"{0}/a.txt\"")]
    [InlineData("Bash", "cp -r a.txt b.txt \"{0}/\"")]
    [InlineData("Bash", "cp -t \"{0}\" a.txt")]
    [InlineData("Bash", "mv a.txt \"{0}/a.txt\"")]
    [InlineData("Bash", "rm \"{0}/a.txt\"")]
    [InlineData("Bash", "rm -rf -- \"{0}/a.txt\"")]
    [InlineData("Bash", "FOO=1 sudo rm \"{0}/a.txt\"")]
    [InlineData("Bash", "bash -c 'echo x > \"{0}/a.txt\"'")]
    [InlineData("PowerShell", "Set-Content -Path \"{0}/a.txt\" -Value x")]
    [InlineData("PowerShell", "Set-Content \"{0}/a.txt\" x")]
    [InlineData("PowerShell", "'x' | Out-File -FilePath \"{0}/a.txt\"")]
    [InlineData("PowerShell", "Out-File -Append \"{0}/a.txt\" -InputObject x")]
    [InlineData("PowerShell", "Add-Content -LiteralPath \"{0}/a.txt\" -Value x")]
    [InlineData("PowerShell", "'x' >> \"{0}/a.txt\"")]
    [InlineData("PowerShell", "Copy-Item a.txt \"{0}/a.txt\" -Force")]
    [InlineData("PowerShell", "Move-Item -Path a.txt -Destination \"{0}/a.txt\"")]
    [InlineData("PowerShell", "Remove-Item \"{0}/a.txt\" -Recurse -Force")]
    public async Task A_shell_write_to_a_target_it_reads_is_refused_outside_the_tree_and_left_alone_inside(string tool, string form)
    {
        var (tree, outside) = Trees();

        var refused = await Hook(tree, Shell(tool, string.Format(form, Slashed(outside)), tree));
        var allowed = await Hook(tree, Shell(tool, string.Format(form, Slashed(Path.Combine(tree, "src"))), tree));

        Assert.Equal(0, refused.Code);
        var reason = (string?)JsonNode.Parse(refused.Output)!["hookSpecificOutput"]!["permissionDecisionReason"] ?? "";
        Assert.Contains("outside this session's tree", reason);
        Assert.Contains("quest", reason);
        Assert.Equal("", allowed.Output.Trim());
    }

    /// <summary>A <c>mv</c> takes its source out of where it was: a source outside the tree is a delete there.</summary>
    [Fact]
    public async Task A_move_out_of_a_source_outside_the_tree_is_refused_too()
    {
        var (tree, outside) = Trees();

        var (_, output) = await Hook(tree, Shell("Bash", $"mv \"{Slashed(outside)}/a.txt\" src/", tree));

        Assert.Contains("\"deny\"", output);
    }

    /// <summary>
    /// A relative target is the call's working directory's, not the tree's: the harness's own cwd is where the shell
    /// writes it. A <c>..</c> climbs out of the tree from inside it, and a cwd outside makes a plain name outside.
    /// </summary>
    [Fact]
    public async Task A_relative_target_is_resolved_against_the_calls_working_directory()
    {
        var (tree, outside) = Trees();

        var inside = await Hook(tree, Shell("Bash", "echo x > src/a.txt", tree));
        var climbs = await Hook(tree, Shell("Bash", "echo x > ../other/a.txt", tree));
        var elsewhere = await Hook(tree, Shell("Bash", "echo x > a.txt", outside));
        var back = await Hook(tree, Shell("Bash", "echo x > ../engine/src/a.txt", outside));

        Assert.Equal("", inside.Output.Trim());
        Assert.Contains("\"deny\"", climbs.Output);
        Assert.Contains("\"deny\"", elsewhere.Output);
        Assert.Equal("", back.Output.Trim());
    }

    /// <summary>A <c>cd</c> moves the directory every later part of the command writes relative to, and a <c>cd</c> back moves it home.</summary>
    [Fact]
    public async Task A_cd_then_a_write_is_judged_from_where_the_cd_went()
    {
        var (tree, outside) = Trees();
        var out_ = Slashed(outside);

        var away = await Hook(tree, Shell("Bash", $"cd \"{out_}\" && echo x >> notes.md", tree));
        var up = await Hook(tree, Shell("Bash", "cd .. ; cat >> other/notes.md", tree));
        var into = await Hook(tree, Shell("Bash", "cd src && echo x > a.txt", tree));
        var returned = await Hook(tree, Shell("Bash", $"cd \"{out_}\"; cd \"{Slashed(tree)}\"; echo x > a.txt", tree));
        var sub = await Hook(tree, Shell("Bash", $"(cd \"{out_}\" && ls) && echo x > a.txt", tree));
        var power = await Hook(tree, Shell("PowerShell", $"Set-Location \"{out_}\"; 'x' | Out-File a.txt", tree));

        Assert.Contains("\"deny\"", away.Output);
        Assert.Contains("\"deny\"", up.Output);
        Assert.Equal("", into.Output.Trim());
        Assert.Equal("", returned.Output.Trim());
        Assert.Equal("", sub.Output.Trim());
        Assert.Contains("\"deny\"", power.Output);
    }

    [Fact]
    public async Task A_quoted_path_with_spaces_is_one_target()
    {
        var (tree, _) = Trees();
        var spaced = Path.Combine(_home, "work", "out side");
        Directory.CreateDirectory(spaced);
        var inSpaced = Path.Combine(tree, "src", "in side");
        Directory.CreateDirectory(inSpaced);

        var refused = await Hook(tree, Shell("Bash", $"echo x > \"{Slashed(spaced)}/a b.txt\"", tree));
        var refusedSingle = await Hook(tree, Shell("Bash", $"cp 'a b.txt' '{Slashed(spaced)}/c d.txt'", tree));
        var power = await Hook(tree, Shell("PowerShell", $"Set-Content -Path '{Slashed(spaced)}/a b.txt' -Value 'x y'", tree));
        var allowed = await Hook(tree, Shell("Bash", $"echo x > \"{Slashed(inSpaced)}/a b.txt\"", tree));

        Assert.Contains("\"deny\"", refused.Output);
        Assert.Contains("\"deny\"", refusedSingle.Output);
        Assert.Contains("\"deny\"", power.Output);
        Assert.Equal("", allowed.Output.Trim());
    }

    /// <summary>
    /// The incident's shape: a heredoc appended to a file outside, which the harness did not ask about. The body is text, so a
    /// write inside it names no target, and a target after the heredoc's marker is still found.
    /// </summary>
    [Fact]
    public async Task A_heredoc_append_is_judged_by_its_target_and_its_body_names_none()
    {
        var (tree, outside) = Trees();

        var refused = await Hook(tree, Shell("Bash", $"cat >> \"{Slashed(outside)}/notes.md\" <<'EOF'\n## a note\nEOF", tree));
        var markerFirst = await Hook(tree, Shell("Bash", $"cat <<EOF >> \"{Slashed(outside)}/notes.md\"\nbody\nEOF", tree));
        var allowed = await Hook(tree, Shell("Bash", $"cat >> \"{Slashed(tree)}/notes.md\" <<'EOF'\necho x > \"{Slashed(outside)}/z\"\n> /elsewhere\nEOF", tree));
        var after = await Hook(tree, Shell("Bash", $"cat > a.txt <<'EOF'\nbody\nEOF\necho y > \"{Slashed(outside)}/z\"", tree));
        var power = await Hook(tree, Shell("PowerShell", $"@'\nbody > \"{Slashed(outside)}/in-body\"\n'@ | Set-Content src/a.txt", tree));

        Assert.Contains("\"deny\"", refused.Output);
        Assert.Contains("\"deny\"", markerFirst.Output);
        Assert.Equal("", allowed.Output.Trim());
        Assert.Contains("\"deny\"", after.Output);
        Assert.Equal("", power.Output.Trim());
    }

    /// <summary>
    /// A command it cannot read is the harness's boundary's, as before, never refused for being unread: an unclosed quote,
    /// a target a variable or a substitution makes, a cd to one, and an interpreter it does not look into.
    /// </summary>
    [Fact]
    public async Task A_command_it_cannot_read_is_left_to_the_boundary_and_never_refused_for_it()
    {
        var (tree, outside) = Trees();
        var out_ = Slashed(outside);

        foreach (var (tool, command) in new[]
        {
            ("Bash", $"echo x > \"{out_}/a.txt"),
            ("Bash", "echo x > $HOME/a.txt"),
            ("Bash", $"echo x > \"{out_}/$(date).txt\""),
            ("Bash", "echo x > `pwd`/a.txt"),
            ("Bash", "cd $SOMEWHERE && echo x > a.txt"),
            ("Bash", $"cd - && tee a.txt < \"{out_}/b\""),
            ("Bash", $"python -c \"open('{out_}/a.txt', 'w').write('x')\""),
            ("PowerShell", "Set-Content -Path $env:TEMP\\a.txt -Value x"),
            ("PowerShell", $"Set-Content -Path \"{out_}/a.txt -Value x"),
            ("Bash", ""),
        })
        {
            var (code, output) = await Hook(tree, Shell(tool, command, tree));
            Assert.True(code == 0 && output.Trim() == "", $"{tool}: {command} was answered {code} `{output.Trim()}`");
        }
    }

    /// <summary>What a command reads, logs to nowhere or says in a quoted word writes no file outside: all of these pass.</summary>
    [Fact]
    public async Task A_read_a_stream_redirect_and_a_quoted_arrow_are_not_writes()
    {
        var (tree, outside) = Trees();

        foreach (var (tool, command) in new[]
        {
            ("Bash", "ls -la"),
            ("Bash", $"cat \"{Slashed(outside)}/a.txt\""),
            ("Bash", $"grep -n x < \"{Slashed(outside)}/a.txt\""),
            ("Bash", $"cp \"{Slashed(outside)}/a.txt\" src/a.txt"),
            ("Bash", "npm test > /dev/null 2>&1"),
            ("Bash", "make 2>&1 | tee"),
            ("Bash", "echo x >&2"),
            ("Bash", $"git commit -m \"a > {Slashed(outside)}/a.txt\""),
            ("Bash", $"echo 'x > {Slashed(outside)}/a.txt' # > {Slashed(outside)}/b.txt"),
            ("PowerShell", "Get-Content a.txt 2>&1 | Out-Null"),
            ("PowerShell", $"Copy-Item \"{Slashed(outside)}/a.txt\" src/a.txt"),
            ("PowerShell", "'x' | Out-File $null"),
            ("PowerShell", $"Write-Output 'x > {Slashed(outside)}/a.txt'"),
        })
        {
            var (code, output) = await Hook(tree, Shell(tool, command, tree));
            Assert.True(code == 0 && output.Trim() == "", $"{tool}: {command} was answered {code} `{output.Trim()}`");
        }
    }

    /// <summary>One refused part refuses the command, wherever it stands in a chain, and a declared target is the tree's like.</summary>
    [Fact]
    public async Task A_chain_is_refused_for_any_one_outside_write_and_a_declared_target_is_let_through()
    {
        var (tree, outside) = Trees();
        var elsewhere = Path.Combine(_home, "work", "third");
        Directory.CreateDirectory(elsewhere);

        var chain = await Hook(tree, Shell("Bash", $"echo a > src/a.txt && echo b | tee src/b.txt > \"{Slashed(outside)}/c.txt\"", tree));
        var declared = await HookWith([tree, outside], Shell("Bash", $"echo x > \"{Slashed(outside)}/a.txt\"", tree));
        var third = await HookWith([tree, outside], Shell("Bash", $"echo x > \"{Slashed(elsewhere)}/a.txt\"", tree));

        Assert.Contains("\"deny\"", chain.Output);
        Assert.Equal("", declared.Output.Trim());
        var reason = (string?)JsonNode.Parse(third.Output)!["hookSpecificOutput"]!["permissionDecisionReason"] ?? "";
        Assert.Contains(outside, reason);
    }

    /// <summary>🔴 A link inside the tree that leads out of it is outside for a shell's write too: the same resolution an edit's path has.</summary>
    [Fact]
    public async Task A_shell_write_through_a_link_that_leads_out_of_the_tree_is_refused()
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

        var (_, output) = await Hook(tree, Shell("Bash", "echo x >> escape/notes.md", tree));

        Assert.Contains("\"deny\"", output);
    }

    /// <summary>The tools it judges are the edit tools and now the two shells, in the harness's own matcher shape.</summary>
    [Fact]
    public void The_matcher_names_the_edit_tools_and_both_shells()
    {
        Assert.Equal("Edit|Write|MultiEdit|NotebookEdit|Bash|PowerShell", TreeGuard.Matcher);
        Assert.StartsWith(TreeGuardHook.EditMatcher, TreeGuard.Matcher);
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

    /// <summary>A shell tool's call as the harness sends it: the command and the directory the shell stands in.</summary>
    private static string Shell(string tool, string command, string cwd) => Call(tool, new JsonObject { ["command"] = command }, cwd);

    /// <summary>A path with forward slashes, which Git Bash, PowerShell and node all take on Windows as they do elsewhere.</summary>
    private static string Slashed(string path) => path.Replace('\\', '/');

    /// <summary>The hook exactly as the harness runs it: exec form, the tree as one argument, JSON on stdin.</summary>
    private Task<(int Code, string Output)> Hook(string? tree, string stdin) => HookWith(tree is null ? [] : [tree], stdin);

    /// <summary>The same, with the tree and each declared target as one argument each (D107).</summary>
    private async Task<(int Code, string Output)> HookWith(IReadOnlyList<string> trees, string stdin)
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
        foreach (var tree in trees) info.ArgumentList.Add(tree);
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

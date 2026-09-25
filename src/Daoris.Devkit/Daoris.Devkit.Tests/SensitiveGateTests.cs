using Daoris.Devkit;

namespace Daoris.Devkit.Tests;

public sealed class SensitiveGateTests : IDisposable
{
    private readonly Fixture _fx = new("sensitive");

    public void Dispose() => _fx.Dispose();

    private GateContext Context(GateDeclaration? declaration = null) =>
        new(_fx.Path, declaration ?? new GateDeclaration());

    /// <summary>
    /// The fixtures below are ASSEMBLED rather than written as literals.
    /// </summary>
    /// <remarks>
    /// A test proving the scanner catches a machine path cannot do so by putting a machine path in a
    /// tracked file — that is the exact thing `sensitive-info` forbids, and this gate caught both of
    /// these the first time it was run over this repository. Which is the gate working, not misfiring.
    ///
    /// The alternative was an ignore-list for test files, and that is worse: a structural scanner that
    /// can be silenced with "it is only a test" is how leaks reach history in the first place.
    /// Assembling the strings keeps the scanner honest and the test exact — what the gate sees at
    /// runtime is byte-for-byte what it is meant to catch.
    /// </remarks>
    private static string WindowsHomePath() => string.Concat("C:", @"\", "Users", @"\", "someone");

    private static string GitHubToken() => string.Concat("ghp", "_", new string('a', 24), "012345");

    private sealed class FakeGit(params string[] files) : IGit
    {
        public IReadOnlyList<string> StagedFiles() => files;

        public IReadOnlyList<string> TrackedFiles() => files;
    }

    private void Git(params string[] arguments)
    {
        var ran = Process.Run("git", arguments, _fx.Path);
        Assert.True(ran.ExitCode == 0, ran.Error);
    }

    /// <summary>
    /// REV3 tools F6: the pre-commit scan listed the STAGED paths and then read each file's working copy.
    /// A leak staged, then cleaned in the working copy without re-staging, was committed by a scan that
    /// passed. What is scanned is what the commit will hold.
    /// </summary>
    [Fact]
    public void A_staged_scan_reads_what_is_staged_not_the_working_copy()
    {
        Git("init", "-q");
        _fx.Write("notes.md", $"see {WindowsHomePath()}\n");
        Git("add", "notes.md");
        _fx.Write("notes.md", "nothing to see\n");

        var result = new SensitiveGate(ScanScope.Staged, new CommandLineGit(_fx.Path), allowBuiltinsOnly: true)
            .Run(Context());

        Assert.False(result.Passed, result.Detail);
    }

    /// <summary>
    /// REV3 tools F7: git quotes a path outside ASCII (`"\346\226\207.md"`), the quoted name matched no
    /// file on disk, and its content was never read — only the path, which says nothing.
    /// </summary>
    [Fact]
    public void A_file_named_outside_ascii_is_read_not_skipped()
    {
        Git("init", "-q");
        _fx.Write("文档.md", $"see {WindowsHomePath()}\n");
        Git("add", "文档.md");

        var result = new SensitiveGate(ScanScope.Tree, new CommandLineGit(_fx.Path), allowBuiltinsOnly: true)
            .Run(Context());

        Assert.False(result.Passed, result.Detail);
        Assert.Contains("文档.md", result.Detail);
    }

    /// <summary>
    /// The property the eleven copies were inconsistent about. A missing private list used to print a
    /// notice and continue, so on a fresh clone the half of the guard that knows the private names
    /// silently did not run — and nothing in the output distinguished that from a clean scan.
    /// </summary>
    [Fact]
    public void A_missing_private_pattern_list_fails_rather_than_quietly_scanning_less()
    {
        _fx.Write("README.md", "nothing to see");

        var result = new SensitiveGate(ScanScope.Tree, new FakeGit("README.md")).Run(Context());

        Assert.False(result.Passed);
        Assert.Contains("missing", result.Detail);
    }

    /// <summary>
    /// REV3: a Windows home written in a JSON, JS or C# literal has two backslashes between its parts,
    /// and a normalized one has forward slashes. The pattern held only the single-backslash spelling, and
    /// this repository's own gate file carried the escaped one past it.
    /// </summary>
    [Theory]
    [InlineData(@"\")]
    [InlineData(@"\\")]
    [InlineData("/")]
    public void A_windows_home_is_caught_however_its_separator_is_spelled(string separator)
    {
        _fx.Write("README.md", $"see {string.Concat("C:", separator, "Users", separator, "someone")} for it");

        var result = new SensitiveGate(ScanScope.Tree, new FakeGit("README.md"), allowBuiltinsOnly: true)
            .Run(Context());

        Assert.False(result.Passed);
        Assert.Contains("Windows user-home absolute path", result.Detail);
    }

    [Fact]
    public void Opting_out_of_the_private_list_is_explicit_and_then_the_builtins_still_run()
    {
        _fx.Write("README.md", $"see {WindowsHomePath()} for the layout");

        var result = new SensitiveGate(ScanScope.Tree, new FakeGit("README.md"), allowBuiltinsOnly: true)
            .Run(Context());

        Assert.False(result.Passed);
        Assert.Contains("Windows user-home absolute path", result.Detail);
    }

    /// <summary>
    /// A file NAMED after a banned token leaks it in the tree listing, whatever its bytes contain.
    /// Every earlier version scanned content only, so this went straight through.
    /// </summary>
    [Fact]
    public void The_path_is_scanned_as_well_as_the_content()
    {
        _fx.Write("docs/notes.md", "harmless");
        _fx.Write("local/sensitive-patterns.txt", "AcmeSecretProject");

        var git = new FakeGit("docs/AcmeSecretProject-plan.md", "docs/notes.md");
        var result = new SensitiveGate(ScanScope.Tree, git).Run(Context());

        Assert.False(result.Passed);
        Assert.Contains("(path)", result.Detail);
    }

    [Fact]
    public void A_clean_tree_passes_and_says_how_many_patterns_it_used()
    {
        _fx.Write("README.md", "a perfectly ordinary readme with repo-relative paths");
        _fx.Write("local/sensitive-patterns.txt", "# a comment, and a blank line follow\n\nAcmeSecretProject\n");

        var result = new SensitiveGate(ScanScope.Tree, new FakeGit("README.md")).Run(Context());

        Assert.True(result.Passed, result.Detail);
        Assert.Contains("patterns", result.Detail);
    }

    /// <summary>Commit messages are history too, and were the last thing anything looked at.</summary>
    [Fact]
    public void A_commit_message_is_scanned()
    {
        _fx.Write("local/sensitive-patterns.txt", "AcmeSecretProject");
        _fx.Write("msg.txt", "fix: port the AcmeSecretProject adapter");

        var gate = new SensitiveGate(
            ScanScope.Message, new FakeGit(), messageFile: _fx.Absolute("msg.txt"));

        Assert.False(gate.Run(Context()).Passed);
    }

    /// <summary>
    /// A gate that prints what it caught has written the secret to a build log, which is frequently
    /// more public than the commit it just blocked.
    /// </summary>
    [Fact]
    public void The_finding_is_redacted_so_the_report_does_not_leak_it_again()
    {
        var token = GitHubToken();
        _fx.Write("config.txt", $"token={token}");
        _fx.Write("local/sensitive-patterns.txt", "# none needed");

        var result = new SensitiveGate(ScanScope.Tree, new FakeGit("config.txt")).Run(Context());

        Assert.False(result.Passed);
        Assert.DoesNotContain(token, result.Detail);
        Assert.Contains("GitHub token", result.Detail);
    }

    /// <summary>A binary file is not text and scanning it produces noise, not findings.</summary>
    [Fact]
    public void Binary_files_are_skipped()
    {
        _fx.WriteBytes("logo.png", [0x89, 0x50, 0x00, 0x01, 0x02]);
        _fx.Write("local/sensitive-patterns.txt", "# none");

        Assert.True(new SensitiveGate(ScanScope.Tree, new FakeGit("logo.png")).Run(Context()).Passed);
    }

    [Fact]
    public void An_invalid_private_pattern_names_the_file_and_the_line()
    {
        _fx.Write("local/sensitive-patterns.txt", "this is ( not a regex");

        var error = Assert.Throws<DevkitException>(
            () => new SensitiveGate(ScanScope.Tree, new FakeGit()).Run(Context()));

        Assert.Contains("not a valid regex", error.Message);
    }
}

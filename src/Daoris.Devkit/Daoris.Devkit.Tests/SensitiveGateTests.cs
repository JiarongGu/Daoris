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
    /// tracked file — that is the exact thing `sensitive-info` forbids, and this gate would catch it
    /// when run over this repository. That is the gate working, not misfiring.
    ///
    /// An ignore-list for test files would be worse: a structural scanner that
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

    private sealed class BytesGit(string file, byte[] staged) : IGit
    {
        public IReadOnlyList<string> StagedFiles() => [file];

        public IReadOnlyList<string> TrackedFiles() => [file];

        public byte[]? StagedBytes(string path) => staged;
    }

    private void Git(params string[] arguments)
    {
        var ran = Process.Run("git", arguments, _fx.Path);
        Assert.True(ran.ExitCode == 0, ran.Error);
    }

    /// <summary>
    /// A pre-commit scan that lists the STAGED paths and then reads each file's working copy lets through a
    /// leak staged, then cleaned in the working copy without re-staging. What is scanned is what the commit
    /// will hold.
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
    /// Git quotes a path outside ASCII (`"\346\226\207.md"`), and the quoted name matches no
    /// file on disk, so its content would go unread — only the path, which says nothing.
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
    /// A missing private list that only printed a notice and continued would leave, on a fresh clone, the
    /// half of the guard that knows the private names silently not running — and nothing in the output
    /// would distinguish that from a clean scan.
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
    /// A Windows home written in a JSON, JS or C# literal has two backslashes between its parts,
    /// and a normalized one has forward slashes; the pattern must hold each spelling, not only the
    /// single-backslash one.
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

    /// <summary>GitHub's fine-grained token and GitLab's personal one are refused as the classic shapes are, and not printed.</summary>
    [Theory]
    [InlineData("github", "_pat_", "GitHub fine-grained token")]
    [InlineData("glpat", "-", "GitLab token")]
    public void A_fine_grained_GitHub_token_and_a_GitLab_token_are_refused(string prefix, string joint, string why)
    {
        var token = string.Concat(prefix, joint, new string('B', 22), "_x9");
        _fx.Write("config.txt", $"token={token}");
        _fx.Write("local/sensitive-patterns.txt", "# none needed");

        var result = new SensitiveGate(ScanScope.Tree, new FakeGit("config.txt")).Run(Context());

        Assert.False(result.Passed);
        Assert.DoesNotContain(token, result.Detail);
        Assert.Contains(why, result.Detail);
    }

    /// <summary>
    /// A file planted with both shapes is refused for each, neither printed, and a file
    /// naming their prefixes alone, as a document about them does, passes.
    /// </summary>
    [Fact]
    public void A_file_planted_with_both_shapes_is_refused_for_each_and_a_clean_one_passes()
    {
        var github = string.Concat("github", "_pat_", "11", new string('C', 22), "_", new string('d', 59));
        var gitlab = string.Concat("glpat", "-", new string('E', 20));
        _fx.Write("planted.env", $"GH_TOKEN={github}\nGITLAB_TOKEN={gitlab}\n");
        _fx.Write("clean.md", "A fine-grained token starts `github_pat_…` and GitLab's `glpat-…`: neither is written here.\n");
        _fx.Write("local/sensitive-patterns.txt", "# none needed");

        var planted = new SensitiveGate(ScanScope.Tree, new FakeGit("planted.env")).Run(Context());
        var clean = new SensitiveGate(ScanScope.Tree, new FakeGit("clean.md")).Run(Context());

        Assert.False(planted.Passed);
        Assert.Contains("planted.env: GitHub fine-grained token", planted.Detail);
        Assert.Contains("planted.env: GitLab token", planted.Detail);
        Assert.DoesNotContain(github, planted.Detail);
        Assert.DoesNotContain(gitlab, planted.Detail);
        Assert.True(clean.Passed, clean.Detail);
    }

    /// <summary>🔴 A UTF-16 file with a byte-order mark holds a NUL beside every ASCII character yet is text: scanned, not skipped as binary.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_utf16_file_with_a_byte_order_mark_is_read_not_skipped(bool littleEndian)
    {
        var encoding = new System.Text.UnicodeEncoding(bigEndian: !littleEndian, byteOrderMark: true);
        _fx.WriteBytes("notes.md", [.. encoding.GetPreamble(), .. encoding.GetBytes($"see {WindowsHomePath()}\n")]);
        _fx.Write("local/sensitive-patterns.txt", "# none");

        var result = new SensitiveGate(ScanScope.Tree, new FakeGit("notes.md")).Run(Context());

        Assert.False(result.Passed, result.Detail);
    }

    /// <summary>🔴 The staged scope decodes the index's bytes as the tree scope decodes the file: a staged UTF-16 file with a byte-order mark is text.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_staged_utf16_file_with_a_byte_order_mark_is_read_not_skipped(bool littleEndian)
    {
        Git("init", "-q");
        var encoding = new System.Text.UnicodeEncoding(bigEndian: !littleEndian, byteOrderMark: true);
        _fx.WriteBytes("notes.md", [.. encoding.GetPreamble(), .. encoding.GetBytes($"see {WindowsHomePath()}\n")]);
        Git("add", "notes.md");

        var result = new SensitiveGate(ScanScope.Staged, new CommandLineGit(_fx.Path), allowBuiltinsOnly: true)
            .Run(Context());

        Assert.False(result.Passed, result.Detail);
    }

    /// <summary>🔴 Binary is decided on the first block of the index's bytes, as in the tree scope: a leak ahead of a late NUL is found.</summary>
    [Fact]
    public void A_staged_file_whose_first_block_is_text_is_scanned_whatever_follows()
    {
        Git("init", "-q");
        _fx.WriteBytes("notes.md", [.. System.Text.Encoding.UTF8.GetBytes($"see {WindowsHomePath()}\n"), .. new byte[9000].Select(_ => (byte)'a'), 0]);
        Git("add", "notes.md");

        var staged = new SensitiveGate(ScanScope.Staged, new CommandLineGit(_fx.Path), allowBuiltinsOnly: true).Run(Context());
        var tree = new SensitiveGate(ScanScope.Tree, new CommandLineGit(_fx.Path), allowBuiltinsOnly: true).Run(Context());

        Assert.False(tree.Passed, tree.Detail);
        Assert.False(staged.Passed, staged.Detail);
    }

    /// <summary>The seam hands the gate the index's bytes, and the gate decodes them: a fake that holds UTF-16 bytes is read as text.</summary>
    [Fact]
    public void The_staged_scope_decodes_the_bytes_the_seam_returns()
    {
        var encoding = new System.Text.UnicodeEncoding(bigEndian: false, byteOrderMark: true);
        var bytes = (byte[])[.. encoding.GetPreamble(), .. encoding.GetBytes($"see {WindowsHomePath()}\n")];

        var result = new SensitiveGate(ScanScope.Staged, new BytesGit("notes.md", bytes), allowBuiltinsOnly: true).Run(Context());

        Assert.False(result.Passed, result.Detail);
    }

    /// <summary>A staged file with a NUL and no byte-order mark is binary, as in the tree scope.</summary>
    [Fact]
    public void A_staged_binary_file_is_skipped()
    {
        Git("init", "-q");
        _fx.WriteBytes("blob.bin", [.. System.Text.Encoding.UTF8.GetBytes($"see {WindowsHomePath()}"), 0, 1, 2]);
        Git("add", "blob.bin");

        var result = new SensitiveGate(ScanScope.Staged, new CommandLineGit(_fx.Path), allowBuiltinsOnly: true)
            .Run(Context());

        Assert.True(result.Passed, result.Detail);
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

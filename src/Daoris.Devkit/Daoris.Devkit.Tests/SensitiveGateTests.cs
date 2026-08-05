using Daoris.Devkit;

namespace Daoris.Devkit.Tests;

public sealed class SensitiveGateTests : IDisposable
{
    private readonly Fixture _fx = new("sensitive");

    public void Dispose() => _fx.Dispose();

    private GateContext Context(GateDeclaration? declaration = null) =>
        new(_fx.Path, declaration ?? new GateDeclaration());

    private sealed class FakeGit(params string[] files) : IGit
    {
        public IReadOnlyList<string> StagedFiles() => files;

        public IReadOnlyList<string> TrackedFiles() => files;
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

    [Fact]
    public void Opting_out_of_the_private_list_is_explicit_and_then_the_builtins_still_run()
    {
        _fx.Write("README.md", @"see C:\Users\someone\Projects for the layout");

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
        _fx.Write("config.txt", "token=ghp_abcdefghijklmnopqrstuvwxyz012345");
        _fx.Write("local/sensitive-patterns.txt", "# none needed");

        var result = new SensitiveGate(ScanScope.Tree, new FakeGit("config.txt")).Run(Context());

        Assert.False(result.Passed);
        Assert.DoesNotContain("ghp_abcdefghijklmnopqrstuvwxyz012345", result.Detail);
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

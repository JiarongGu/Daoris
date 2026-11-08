using Daoris.Devkit;

namespace Daoris.Devkit.Tests;

public sealed class GitObjectsTests : IDisposable
{
    private readonly Fixture _fx = new("objects");

    public void Dispose() => _fx.Dispose();

    private void Git(params string[] arguments)
    {
        var ran = GitStart.Run(arguments, _fx.Path);
        Assert.True(ran.ExitCode == 0, ran.Error);
    }

    private void Commit(string file, string text)
    {
        _fx.Write(file, text);
        Git("add", file);
        Git("-c", "user.name=t", "-c", "user.email=t@t.invalid", "commit", "-q", "-m", $"add {file}");
    }

    [Fact]
    public void Every_readable_blob_and_commit_message_is_read()
    {
        Git("init", "-q");
        Commit("a.txt", "first text\n");

        var items = new CommandLineGitObjects(_fx.Path).Everything().ToList();

        Assert.Contains(items, item => item.Label.StartsWith("blob") && item.Text.Contains("first text"));
        Assert.Contains(items, item => item.Label.Contains("(message)") && item.Text.Contains("add a.txt"));
    }

    /// <summary>
    /// An audit that proves a rewrite worked cannot skip an object it could not read: git reports the loose
    /// object it cannot unpack on stderr and goes on, so the stream simply ends without it and the scan
    /// reads as a clean one.
    /// </summary>
    [Fact]
    public void An_object_git_cannot_read_fails_the_audit_instead_of_being_skipped()
    {
        Git("init", "-q");
        Commit("a.txt", "first text\n");
        // Unreachable, so `rev-list` is clean and only `cat-file`, which reads every object, meets it.
        _fx.Write("dangling.txt", "never committed\n");
        var made = GitStart.Run(["hash-object", "-w", "dangling.txt"], _fx.Path);
        var sha = made.Output.Trim();
        var loose = Path.Combine(_fx.Path, ".git", "objects", sha[..2], sha[2..]);
        File.SetAttributes(loose, FileAttributes.Normal);
        File.WriteAllText(loose, "garbage");

        var error = Assert.Throws<DevkitException>(() => new CommandLineGitObjects(_fx.Path).Everything().ToList());

        Assert.Contains("cat-file", error.Message);
    }
}

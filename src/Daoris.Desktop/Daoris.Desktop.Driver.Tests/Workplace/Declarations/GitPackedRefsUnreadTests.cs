using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A <c>packed-refs</c> the files cannot say (git's rewrite holds it as Windows' move holds a file) is unknown, never a file with no
/// ref and no replacement in it.
/// </summary>
public sealed class GitPackedRefsUnreadTests : IDisposable
{
    private const string One = "1111111111111111111111111111111111111111";

    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "packed-unread", Guid.NewGuid().ToString("N")[..8]);

    public GitPackedRefsUnreadTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".git", "refs", "heads"));
        File.WriteAllText(Path.Combine(_root, ".git", "HEAD"), "ref: refs/heads/main\n");
    }

    public void Dispose() => GitSeed.Discard(_root);

    private static string? NoSystem(string name) => name == "GIT_CONFIG_NOSYSTEM" ? "1" : null;

    private string Packed => Path.Combine(_root, ".git", "packed-refs");

    [Fact]
    public void A_packed_refs_that_does_not_read_is_no_answer_that_a_ref_is_absent()
    {
        File.WriteAllText(Packed, $"{One} refs/heads/main\n");
        Assert.Equal((true, One), TipOf.FromFiles(_root, "refs/heads/main"));

        using var held = new FileStream(Packed, FileMode.Open, FileAccess.Read, FileShare.None);

        Assert.False(TipOf.FromFiles(_root, "refs/heads/main").Known);
        Assert.False(TipOf.FromFiles(_root, "refs/heads/none").Known);
    }

    [Fact]
    public void A_packed_refs_that_does_not_read_can_hold_a_replacement()
    {
        File.WriteAllText(Packed, $"{One} refs/heads/main\n");
        Assert.False(GitReplacements.CanBeInForce(Path.Combine(_root, ".git"), _ => null));

        using var held = new FileStream(Packed, FileMode.Open, FileAccess.Read, FileShare.None);

        Assert.True(GitReplacements.CanBeInForce(Path.Combine(_root, ".git"), _ => null));
    }

    [Fact]
    public async Task A_stamp_taken_while_packed_refs_does_not_read_equals_no_other()
    {
        File.WriteAllText(Packed, $"{One} refs/heads/main\n");
        using var held = new FileStream(Packed, FileMode.Open, FileAccess.Read, FileShare.None);

        var first = await GitStamp.OfAsync(_root, CancellationToken.None, NoSystem);
        var second = await GitStamp.OfAsync(_root, CancellationToken.None, NoSystem);

        Assert.NotNull(first);
        Assert.NotEqual(first!.Digest, second!.Digest);
    }
}

using Daoris.Driver;
using Daoris.Testing;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// 🔴 A content proof that found nothing holding a tree's work is kept in the home: asked again over what did not move, from this
/// process or after a restart, it asks git nothing; a moved tip, a moved ref or another holder set is another key.
/// </summary>
public sealed class ContentProofMemoTests : IDisposable
{
    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "content-proof-memo", Guid.NewGuid().ToString("N")[..8]);

    private string Common => Path.Combine(_root, "repo", "common");

    private static readonly DateTimeOffset Now = new(2026, 11, 22, 9, 0, 0, TimeSpan.Zero);

    private string Home =>Path.Combine(_root, "home");

    public ContentProofMemoTests()
    {
        Directory.CreateDirectory(Path.Combine(Common, "refs", "heads"));
        Directory.CreateDirectory(Home);
        File.WriteAllText(Path.Combine(Common, "refs", "heads", "main"), "aaa\n");
    }

    public void Dispose()
    {
        ContentProofMemo.Restart(Home);
        GitSeed.Discard(_root);
    }

    private string? Key(string tip = "t1", string? line = "main", params string[] holders) => ContentProofMemo.KeyOf(Common, tip, line, holders);

    [Fact]
    public void A_kept_proof_is_known_again_and_after_a_restart()
    {
        var key = Key()!;
        Assert.False(ContentProofMemo.Knows(Home, key));

        ContentProofMemo.Keep(Home, key, Now);

        Assert.True(ContentProofMemo.Knows(Home, Key()!));
        ContentProofMemo.Restart(Home);
        Assert.True(ContentProofMemo.Knows(Home, Key()!));
    }

    [Fact]
    public void A_moved_tip_ref_line_or_holder_set_is_another_key()
    {
        var key = Key()!;
        Assert.NotEqual(key, Key("t2"));
        Assert.NotEqual(key, Key(line: "dev"));
        Assert.NotEqual(key, Key(holders: "mine"));
        File.WriteAllText(Path.Combine(Common, "refs", "heads", "main"), "bbb\n");
        Assert.NotEqual(key, Key());
    }

    [Fact]
    public void A_repository_whose_refs_are_not_files_keeps_nothing()
    {
        Directory.CreateDirectory(Path.Combine(Common, "reftable"));

        Assert.Null(Key());
    }

    [Fact]
    public void The_memo_is_bounded()
    {
        for (var each = 0; each < ContentProofMemo.Most + 25; each++) ContentProofMemo.Keep(Home, Key($"t{each}")!, Now);

        ContentProofMemo.Restart(Home);
        Assert.False(ContentProofMemo.Knows(Home, Key("t0")!));
        Assert.True(ContentProofMemo.Knows(Home, Key($"t{ContentProofMemo.Most + 24}")!));
    }
}

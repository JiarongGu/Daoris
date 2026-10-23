using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The two records registration following keeps under the home (WSSETUP5, D124 §3): the lines Daoris moved, and what
/// following each repository last came to. A line is due when Daoris moved it after it was last followed.
/// </summary>
public sealed class RegistryFollowingTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-registry-following-" + Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset Noon = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static RegistrationFollowed Followed(string repository, string outcome = RegistryOutcome.Registered) =>
        new(repository, outcome, $"{repository} said", "main", "0123456789abcdef0123456789abcdef01234567", Sent: true);

    [Fact]
    public void A_line_daoris_moved_is_due_until_it_is_followed_and_due_again_when_it_moves_again()
    {
        Assert.Empty(RegistryFollowing.Due(_home));

        RegistryFollowing.Moved(_home, "game", Noon);
        RegistryFollowing.Moved(_home, "engine", Noon);
        Assert.Equal(["engine", "game"], RegistryFollowing.Due(_home));

        RegistryFollowing.Remember(_home, Followed("game"), Noon.AddSeconds(1));
        Assert.Equal(["engine"], RegistryFollowing.Due(_home));

        RegistryFollowing.Moved(_home, "game", Noon.AddMinutes(5));
        Assert.Equal(["engine", "game"], RegistryFollowing.Due(_home));
    }

    [Fact]
    public void A_follow_older_than_the_move_leaves_it_due()
    {
        RegistryFollowing.Remember(_home, Followed("game"), Noon);
        RegistryFollowing.Moved(_home, "game", Noon.AddSeconds(1));

        Assert.Equal(["game"], RegistryFollowing.Due(_home));
    }

    [Fact]
    public void What_following_came_to_is_kept_per_repository_for_its_row()
    {
        RegistryFollowing.Remember(_home, Followed("game", RegistryOutcome.NotSetUp), Noon);
        RegistryFollowing.Remember(_home, Followed("game"), Noon.AddMinutes(1));
        RegistryFollowing.Remember(_home, new RegistrationFollowed("engine", RegistryOutcome.NoLine, "engine has no line"), Noon);

        var read = RegistryFollowing.Read(_home);

        Assert.Equal(2, read.Count);
        Assert.Equal(new FollowedEntry("game", Noon.AddMinutes(1), RegistryOutcome.Registered, "game said", "main", "0123456789abcdef0123456789abcdef01234567"), read["game"]);
        Assert.Equal(new FollowedEntry("engine", Noon, RegistryOutcome.NoLine, "engine has no line", null, null), read["ENGINE"]);
    }

    private const string Followed0 = "0123456789abcdef0123456789abcdef01234567";
    private const string Pulled = "89abcdef0123456789abcdef0123456789abcdef";

    /// <summary>A checkout whose line `main` stands at <paramref name="commit"/>, as git's own files say it: a loose ref.</summary>
    private string Checkout(string commit)
    {
        var root = Path.Combine(_home, "checkout");
        var heads = Path.Combine(root, ".git", "refs", "heads");
        Directory.CreateDirectory(heads);
        File.WriteAllText(Path.Combine(heads, "main"), commit + "\n");
        return root;
    }

    /// <summary>
    /// 🔴 SETUPSTATE1 (the owner's report, 2026-10-23): a set-up landed on a branch for review, its pull request merged on the remote and
    /// the checkout pulled outside Daoris, so its line moved with no Daoris landing to say so, and the follow never registered it: the
    /// repository read not adopted until the next start. The ref watch now hears the line stand where it was not followed at.
    /// </summary>
    [Fact]
    public void A_line_heard_at_another_commit_than_it_was_followed_at_is_due()
    {
        RegistryFollowing.Remember(_home, Followed("game", RegistryOutcome.NotSetUp), Noon);
        var root = Checkout(Pulled);

        Assert.True(RegistryFollowing.Heard(_home, new RefRepository("game", "default", root)));

        Assert.Equal(["game"], RegistryFollowing.Due(_home));
    }

    [Fact]
    public void A_line_heard_where_it_was_followed_is_not_due()
    {
        RegistryFollowing.Remember(_home, Followed("game", RegistryOutcome.NotSetUp), Noon);
        var root = Checkout(Followed0);

        Assert.False(RegistryFollowing.Heard(_home, new RefRepository("game", "default", root)));
        // Never followed, so the start's follow of every line takes it; and a line git's files cannot say is asked nothing.
        Assert.False(RegistryFollowing.Heard(_home, new RefRepository("engine", "default", root)));
        RegistryFollowing.Remember(_home, new RegistrationFollowed("tools", RegistryOutcome.NoLine, "tools has no line"), Noon);
        Assert.False(RegistryFollowing.Heard(_home, new RefRepository("tools", "default", root)));
        Assert.False(RegistryFollowing.Heard(_home, new RefRepository("game", "default", Path.Combine(_home, "nowhere"))));

        Assert.Empty(RegistryFollowing.Due(_home));
    }

    [Fact]
    public void A_line_already_due_is_not_written_again()
    {
        RegistryFollowing.Remember(_home, Followed("game", RegistryOutcome.NotSetUp), Noon);
        var root = Checkout(Pulled);
        Assert.True(RegistryFollowing.Heard(_home, new RefRepository("game", "default", root)));
        var written = File.GetLastWriteTimeUtc(DaorisHome.Entry(_home, HomeEntry.LinesMoved));

        Assert.False(RegistryFollowing.Heard(_home, new RefRepository("game", "default", root)));

        Assert.Equal(written, File.GetLastWriteTimeUtc(DaorisHome.Entry(_home, HomeEntry.LinesMoved)));
        Assert.Equal(["game"], RegistryFollowing.Due(_home));
    }

    [Fact]
    public void A_record_that_does_not_read_is_none_and_never_a_failure()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(DaorisHome.Entry(_home, HomeEntry.LinesMoved), "not json");
        File.WriteAllText(DaorisHome.Entry(_home, HomeEntry.RegistryFollowed), "{\"repositories\":[]}");

        Assert.Empty(RegistryFollowing.Due(_home));
        Assert.Empty(RegistryFollowing.Read(_home));

        RegistryFollowing.Moved(_home, "game", Noon);
        Assert.Equal(["game"], RegistryFollowing.Due(_home));
    }

    [Fact]
    public void Both_records_are_written_whole_LF_and_without_a_byte_order_mark()
    {
        RegistryFollowing.Moved(_home, "game", Noon);
        RegistryFollowing.Remember(_home, Followed("game"), Noon);

        foreach (var entry in new[] { HomeEntry.LinesMoved, HomeEntry.RegistryFollowed })
        {
            var bytes = File.ReadAllBytes(DaorisHome.Entry(_home, entry));
            Assert.False(bytes is [0xEF, 0xBB, 0xBF, ..], $"{entry.New} starts with a byte-order mark");
            Assert.DoesNotContain((byte)'\r', bytes);
            Assert.Equal((byte)'\n', bytes[^1]);
        }
    }
}

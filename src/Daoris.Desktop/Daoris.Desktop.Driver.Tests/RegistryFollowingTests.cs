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

    [Fact]
    public void A_record_that_does_not_read_is_none_and_never_a_failure()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(Path.Combine(_home, RegistryFollowing.MovedFile), "not json");
        File.WriteAllText(Path.Combine(_home, RegistryFollowing.FollowedFile), "{\"repositories\":[]}");

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

        foreach (var file in new[] { RegistryFollowing.MovedFile, RegistryFollowing.FollowedFile })
        {
            var bytes = File.ReadAllBytes(Path.Combine(_home, file));
            Assert.False(bytes is [0xEF, 0xBB, 0xBF, ..], $"{file} starts with a byte-order mark");
            Assert.DoesNotContain((byte)'\r', bytes);
            Assert.Equal((byte)'\n', bytes[^1]);
        }
    }
}

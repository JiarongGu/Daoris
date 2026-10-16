using Daoris.Knowledge;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// 🔴 HOMEID1 (D171, the terminal RPC design §3.2): FINDDAORIS1's discovery took any address whose status named a <c>tier</c>,
/// so another install's service passed. A local host now answers its home's id, <c>home</c>, read from the home it was started
/// with (minted there where it had none), and a terminal takes an address only where that is its own home's id. A shared host
/// answers none (D47): it serves a team, and has no person's home.
/// </summary>
public sealed class HomeStatusTests
{
    [Fact]
    public async Task A_local_host_answers_its_homes_id_minted_where_it_had_none()
    {
        using var host = new DaorisHost(ServiceMode.Local);

        var status = await host.GetAsync("/api/status");

        Assert.Equal(200, status.Status);
        var id = HomeIdentity.Read(host.Home, out var unread);
        Assert.Null(unread);
        Assert.True(HomeIdentity.IsId(id), id);
        Assert.Equal(id, status.Json.GetProperty("home").GetString());
        // Asked again, the same: the id is the home's, not the request's.
        Assert.Equal(id, (await host.GetAsync("/api/status")).Json.GetProperty("home").GetString());
    }

    [Fact]
    public async Task A_home_minted_before_is_answered_as_written()
    {
        var home = Path.Combine(Path.GetTempPath(), "daoris-home-status-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(home);
        try
        {
            File.WriteAllText(HomeIdentity.PathIn(home), "{ \"id\": \"0123456789abcdef0123456789abcdef\", \"minted\": \"2026-10-16T08:00:00+00:00\" }");
            using var host = new DaorisHost(ServiceMode.Local, environment: new Dictionary<string, string?> { [DaorisHome.Variable] = home });

            Assert.Equal("0123456789abcdef0123456789abcdef", (await host.GetAsync("/api/status")).Json.GetProperty("home").GetString());
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    /// <summary>A file that does not read is never replaced (the person's home is not guessed at): the host answers no id.</summary>
    [Fact]
    public async Task A_home_whose_id_does_not_read_answers_none_and_keeps_its_file()
    {
        var home = Path.Combine(Path.GetTempPath(), "daoris-home-status-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(home);
        try
        {
            File.WriteAllText(HomeIdentity.PathIn(home), "{ \"id\": \"not an id\" }");
            using var host = new DaorisHost(ServiceMode.Local, environment: new Dictionary<string, string?> { [DaorisHome.Variable] = home });

            var status = await host.GetAsync("/api/status");

            Assert.Equal(200, status.Status);
            Assert.False(status.Json.TryGetProperty("home", out _));
            Assert.Equal("{ \"id\": \"not an id\" }", File.ReadAllText(HomeIdentity.PathIn(home)));
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public async Task A_shared_host_answers_no_home()
    {
        using var host = new DaorisHost(ServiceMode.Shared);
        var key = (await host.Composed.Keys.MintAsync("anyone@a-machine", TimeSpan.FromDays(1), DateTimeOffset.UtcNow)).Key;

        var status = await host.GetAsync("/api/status", key: key);

        Assert.Equal(200, status.Status);
        Assert.False(status.Json.TryGetProperty("home", out _));
        Assert.False(File.Exists(HomeIdentity.PathIn(host.Home)));
    }
}

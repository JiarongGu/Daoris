using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// The one client of a remote deployment: whatever goes wrong on the way there is a
/// <see cref="RemoteException"/> — the wall every sync pass reports and every take rides on — and never
/// an exception nobody catches.
/// </summary>
public sealed class HttpRemoteTests
{
    /// <summary>
    /// 🔴 REV3: `daoris remote add team --url team.example.com:5177` is accepted, and so is the
    /// environment's url. Such an address parses as a SCHEME (or as relative), and the request threw
    /// NotSupportedException / InvalidOperationException — outside the catch, so a sync pass answered a
    /// bare 500 and a take on a shared quest committed locally and then threw.
    /// </summary>
    [Theory]
    [InlineData("team.example.com:5177")]
    [InlineData("203.0.113.5:5177")]
    public async Task An_address_with_no_scheme_is_the_remote_s_wall_not_a_crash(string url)
    {
        var remote = new HttpRemote(new RemoteConfig(url, "dk_test_key"));

        var wall = await Assert.ThrowsAsync<RemoteException>(() => remote.FetchQuestsAsync(0));

        Assert.Contains("could not be reached", wall.Message);
        Assert.DoesNotContain("dk_test_key", wall.Message);
    }
}

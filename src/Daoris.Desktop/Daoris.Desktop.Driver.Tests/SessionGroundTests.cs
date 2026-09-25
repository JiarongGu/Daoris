using Daoris.Driver;
using StandInService = Daoris.Desktop.Driver.Tests.OrphanedSessionTests.StandInService;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Where a session ran and what it started from (SURF6): the two machine-local facts its review, its
/// merge and its discard are read from.
/// </summary>
/// <remarks>
/// 🔴 GROUND1, found on the window (FRAME6's look): these were read from <c>/api/sessions</c>, which
/// lists ACTIVE sessions, so every session that had ended — the usual moment to review one — answered
/// "no tree on this machine" beside a head that showed its tree.
/// </remarks>
public sealed class SessionGroundTests
{
    [Fact]
    public async Task A_session_that_ended_still_names_its_tree_and_the_commit_it_started_from()
    {
        await using var service = StandInService.Start("D:/fam/engine");
        service.Seed("done1", "completed", tree: "D:/fam/engine", baseCommit: "abc1234");
        service.Seed("live1", "working", tree: "D:/fam/game", baseCommit: "def5678");
        using var client = new ServiceClient(service.Url, null);

        Assert.Equal(("D:/fam/engine", "abc1234"), await client.SessionGroundAsync("done1"));
        Assert.Equal(("D:/fam/game", "def5678"), await client.SessionGroundAsync("live1"));
        Assert.Equal(((string?)null, (string?)null), await client.SessionGroundAsync("nobody"));
    }
}

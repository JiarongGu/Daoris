using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// Registration following its line, the repository row's *Refresh*: the screen's half of
/// <c>daoris repository register --repository &lt;name&gt;</c>. The page's bridge file for
/// this domain is <c>bridge/registry.ts</c>.
/// </summary>
public sealed class RegistryRoutes(DriverLoop loop)
{
    private readonly DriverLoop _loop = loop;

    // One repository registered from what its line declares, as `connect` would send it, through the loop's own client, so
    // its outcome reaches this machine's log as every follow's does. A refusal is an answer, as a landing's is: what the
    // row says, and why nothing is registered. Either way it declares the registry.
    [DriverRoute("REGISTRY_REFRESH", PipeClass.Open)]
    [Publishes("REGISTRY_REFRESH", StateScopes.Registry)]
    private async Task<object?> RegistryRefreshAsync(RegistryRefresh press, CancellationToken cancellationToken)
    {
        var repository = press.Repository;
        var service = _loop.Service ?? throw NotReady();
        var config = DriverConfig.Load(_loop.ConfigPath);

        var report = await RegistrationFollow.FollowAsync(new RegistrationWorld(service, _loop.Home, config), [repository], cancellationToken)
            .ConfigureAwait(false);
        var followed = report.Followed.Single();
        _loop.Nudge();
        // The record `daoris repository register` prints from, read back out of this answer by `RegistrationFollowed.Read`.
        return followed.ToWire(report.Refresh);
    }
}

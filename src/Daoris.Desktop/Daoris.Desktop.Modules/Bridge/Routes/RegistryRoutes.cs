using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// Registration following its line, the repository row's *Refresh* (WSSETUP5, D124 §3.1): the screen's half of
/// <c>daoris-driver register --repository &lt;name&gt;</c>. The row and its button are WSSETUP7's; the page's bridge file for
/// this domain, <c>bridge/registry.ts</c>, comes with them.
/// </summary>
public sealed class RegistryRoutes(DriverLoop loop)
{
    private readonly DriverLoop _loop = loop;

    // One repository registered from what its line declares, as `connect` would send it, through the loop's own client, so
    // its outcome reaches this machine's log as every follow's does. A refusal is an answer, as a landing's is: what the
    // row says, and why nothing was registered. Either way it declares the registry (STATEHUB1h, D169).
    [DriverRoute("REGISTRY_REFRESH")]
    [Publishes("REGISTRY_REFRESH", StateScopes.Registry)]
    private async Task<object?> RegistryRefreshAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var repository = PayloadHelper.GetRequiredValue<string>(request.Payload, "repository");
        var service = _loop.Service ?? throw NotReady();
        var config = DriverConfig.Load(_loop.ConfigPath);

        var report = await RegistrationFollow.FollowAsync(new RegistrationWorld(service, _loop.Home, config), [repository], cancellationToken)
            .ConfigureAwait(false);
        var followed = report.Followed.Single();
        _loop.Nudge();
        // RPCOPEN1: the record `daoris-driver register` prints from, read back out of this answer by `RegistrationFollowed.Read`.
        return followed.ToWire(report.Refresh);
    }
}

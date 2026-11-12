using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// What the person's growth switch says of a press that opens a department (<c>REGISTRY_CREATE</c>, read for the workspace it names) or asks a room
/// (<c>PLACE_APPLY</c> with a <c>room</c>, read for the repository it names, its own value over its workspace's), read from the file now so a change
/// between two presses takes the next (<see cref="PipeGrowth"/>). A file that does not read asks, the safe side.
/// </summary>
internal static class DelegateGrowth
{
    public static async Task<PipeGrowthStanding> OfAsync(DriverLoop loop, IpcRequest request, CancellationToken ct)
    {
        DriverConfig config;
        try
        {
            config = DriverConfig.Load(loop.ConfigPath);
        }
        catch (DriverException)
        {
            return new PipeGrowthStanding(GrowthKey.Asked);
        }

        if (request.Type == "REGISTRY_CREATE")
        {
            var workspace = RemoteTarget.Workspace(PressPayload.Text(request.Payload, "workspace") is { Length: > 0 } named ? named.Trim() : null);
            return Standing(GrowthRules.Resolve(config, null, workspace).Value, GrowthRules.OffOpening(workspace));
        }

        // A room's repository, and its workspace as the registry spells it: one the registry does not hold is the plan's refusal, not the switch's.
        if (PressPayload.Words(request.Payload, "repositories") is not [{ } repository] || loop.Service is not { } service) return new PipeGrowthStanding(GrowthKey.Mode);
        var row = (await service.RegistryAsync(ct).ConfigureAwait(false)).FirstOrDefault(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase));
        if (row is null) return new PipeGrowthStanding(GrowthKey.Mode);
        return Standing(GrowthRules.Resolve(config, row.Repository, RemoteTarget.Workspace(row.Workspace)).Value, GrowthRules.OffRoom(row.Repository));
    }

    private static PipeGrowthStanding Standing(string value, string off) => new(value, value == GrowthKey.Off ? off : null);
}

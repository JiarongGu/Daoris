using Daoris.Driver;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>The repository page's kept slots, read from this machine's store without exposing their folders.</summary>
public sealed class TreePoolRoutes(DriverLoop loop)
{
    [DriverRoute("TREE_POOL", PipeClass.Open)]
    [ByEvent("TREE_POOL", Untold = "the desktop store's kept slots and claims in flight")]
    private Task<object?> TreePoolAsync(TreePoolRead read, CancellationToken cancellationToken)
    {
        var workspace = ActField.Needs(read.Workspace, "workspace");
        var repository = ActField.Needs(read.Repository, "repository");
        return Task.Run<object?>(() =>
        {
            var pool = new TreePool(loop.Home);
            var claims = pool.Claims(workspace, repository).ToDictionary(claim => claim.Number);
            return new
            {
                Slots = pool.Slots(workspace, repository).Select(slot =>
                {
                    var claim = claims.GetValueOrDefault(slot.Number);
                    var restoring = claim is not null && TreeClaimPhases.Restoring.Contains(claim.Phase);
                    return new
                    {
                        slot.Number,
                        State = restoring ? "restoring" : claim is not null ? "busy"
                            : slot.State == KeptStates.Kept && slot.Held ? "held" : slot.State,
                        Line = claim is null ? slot.Line : "",
                        Reason = restoring ? "tree.kept-restoring" : "",
                    };
                }).ToArray(),
            };
        }, cancellationToken);
    }
}

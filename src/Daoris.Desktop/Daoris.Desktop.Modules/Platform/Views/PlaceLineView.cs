using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris place line</c> as the card shows it (<c>SET_LINE</c>): a repository's line, or a whole workspace's, set to a branch or cleared.
/// Refused before any card where the name is not one registered here or the branch is none git would take; a press that names a policy and
/// no branch is another act, shown by its values alone.
/// </summary>
public sealed class PlaceLineView : IPressView
{
    public string Module => DriverModule.Name;

    public string Route => "SET_LINE";

    public async Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        var scoped = await PlaceScope.ReadAsync(world, payload, "a line", true, cancellationToken).ConfigureAwait(false);
        if (scoped.Refusal is not null) return PlaceScope.Refusal(scoped);
        var sentBranch = payload is { ValueKind: JsonValueKind.Object } body && body.TryGetProperty("branch", out _);
        if (!sentBranch) return new PressViewed(null, null);

        var branch = PressPayload.Raw(payload, "branch") is { Length: > 0 } text ? text : null;
        try
        {
            _ = scoped.Circle ? DriverConfig.Empty.WithWorkspaceLine(scoped.Workspace!, branch) : DriverConfig.Empty.WithLine(scoped.Repository!, branch);
        }
        catch (DriverException refused)
        {
            return PressViewed.Refused(refused.Message);
        }

        var facts = scoped.Facts();
        if (branch is not null) facts["branch"] = branch;
        var kind = (scoped.Circle ? "line-workspace-" : "line-") + (branch is null ? "clear" : "set");
        return PressViewed.Shown(new PressView(kind, facts, $"daoris place line {scoped.Words} {branch ?? "--clear"}"));
    }
}

using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris tree clean [--workspace &lt;name&gt;] --yes</c> as the card shows it (<c>SWEEP</c>): the session and landed branches the
/// clean-up's list (<c>SWEEP_PLAN</c>, the page's look and the verb's) clears to go now, each judged again as it goes. A workspace this
/// machine does not have is refused at the ask, so the person never sees it.
/// </summary>
public sealed class SweepView : IPressView
{
    public string Module => DriverModule.Name;

    public string Route => "SWEEP";

    public async Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        var workspace = PressPayload.Text(payload, "workspace");
        if (workspace.Length > 0 && !(await world.RegistryAsync(cancellationToken).ConfigureAwait(false))
                .Any(row => string.Equals(row.Workspace, workspace, StringComparison.OrdinalIgnoreCase)))
        {
            return PressViewed.Refused($"there is no workspace `{workspace}` on this machine — use a workspace's name as Repositories lists it.");
        }

        // The page's press names the rows it listed to go; a verb's names none and takes every row cleared.
        var only = payload is { ValueKind: JsonValueKind.Object } body && body.TryGetProperty("only", out var named) && named.ValueKind == JsonValueKind.Array
            ? named.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.String).Select(each => each.GetString()!).ToHashSet(StringComparer.Ordinal)
            : null;
        var listed = await world.ReadAsync(Module, "SWEEP_PLAN", new { workspace = workspace.Length > 0 ? workspace : null }, cancellationToken)
            .ConfigureAwait(false);
        var rows = PressPayload.Rows(listed, "branches").Concat(PressPayload.Rows(listed, "landed"))
            .Where(row => PressPayload.Flag(row, "removable")
                          && (only is null || only.Contains($"{PressPayload.Field(row, "repository")}:{PressPayload.Field(row, "branch")}")))
            .Select(row => new PressSaid("clean-up-branch", ("repository", PressPayload.Field(row, "repository")), ("branch", PressPayload.Field(row, "branch"))))
            .ToList();
        if (rows.Count == 0) rows.Add(new PressSaid("clean-up-nothing"));

        var command = "daoris tree clean" + (workspace.Length > 0 ? $" --workspace {ShellWord.Of(workspace, ShellWord.Workspace)}" : "") + " --yes";
        return workspace.Length > 0
            ? PressViewed.Shown(new PressView("clean-up-workspace", new Dictionary<string, string>(StringComparer.Ordinal) { ["workspace"] = workspace }, command, rows))
            : PressViewed.Shown(new PressView("clean-up", new Dictionary<string, string>(StringComparer.Ordinal), command, rows));
    }
}

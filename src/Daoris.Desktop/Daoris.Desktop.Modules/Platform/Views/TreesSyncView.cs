using System.Globalization;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris tree sync [--repository &lt;name&gt;] [--workspace &lt;name&gt;] [--all] --yes</c> as the card shows it (<c>TREES_SYNC</c>): the
/// repositories brought up to date after a pull request merged, and each row its list (<c>TREES_SYNC_PLAN</c>, the page's look and the
/// press's own ground) moves: a line fast-forwarded from <c>origin</c>, a branch still at work replayed onto it, a landed branch whose work
/// reached it deleted. A repository or a workspace this machine does not have is refused at the ask, so the person never sees it.
/// </summary>
public sealed class TreesSyncView : IPressView
{
    public string Module => DriverModule.Name;

    public string Route => "TREES_SYNC";

    public async Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        var repository = PressPayload.Text(payload, "repository");
        var workspace = PressPayload.Text(payload, "workspace");
        var all = payload is { } body && PressPayload.Flag(body, "all");
        if (repository.Length > 0 && await world.RegistrationAsync(repository, cancellationToken).ConfigureAwait(false) is null)
        {
            return PressViewed.Refused($"`{repository}` is not registered on this machine — use a repository's name as Repositories lists it.");
        }

        if (workspace.Length > 0 && !(await world.RegistryAsync(cancellationToken).ConfigureAwait(false))
                .Any(row => string.Equals(row.Workspace, workspace, StringComparison.OrdinalIgnoreCase)))
        {
            return PressViewed.Refused($"there is no workspace `{workspace}` on this machine — use a workspace's name as Repositories lists it.");
        }

        var listed = await world.ReadAsync(Module, "TREES_SYNC_PLAN",
            new { repository = Named(repository), workspace = Named(workspace), all = all ? true : (bool?)null }, cancellationToken).ConfigureAwait(false);
        var rows = new List<PressSaid>();
        rows.AddRange(PressPayload.Rows(listed, "lines").Where(row => PressPayload.Flag(row, "moves"))
            .Select(row => new PressSaid("sync-line", ("repository", PressPayload.Field(row, "repository")), ("line", PressPayload.Field(row, "line")), ("commits", Commits(row)))));
        rows.AddRange(PressPayload.Rows(listed, "rebases").Where(row => PressPayload.Flag(row, "replays"))
            .Select(row => new PressSaid("sync-replay", ("repository", PressPayload.Field(row, "repository")), ("branch", PressPayload.Field(row, "branch")), ("commits", Commits(row)))));
        rows.AddRange(PressPayload.Rows(listed, "deletes").Where(row => PressPayload.Flag(row, "removable"))
            .Select(row => new PressSaid("sync-delete", ("repository", PressPayload.Field(row, "repository")), ("branch", PressPayload.Field(row, "branch")))));
        if (rows.Count == 0) rows.Add(new PressSaid("sync-nothing"));

        var command = "daoris tree sync"
            + (repository.Length > 0 ? $" --repository {ShellWord.Of(repository, ShellWord.Name)}" : "")
            + (workspace.Length > 0 ? $" --workspace {ShellWord.Of(workspace, ShellWord.Workspace)}" : "")
            + (all ? " --all" : "") + " --yes";
        var (kind, facts) = repository.Length > 0
            ? ("sync-repository", new Dictionary<string, string>(StringComparer.Ordinal) { ["repository"] = repository })
            : workspace.Length > 0
                ? ("sync-workspace", new Dictionary<string, string>(StringComparer.Ordinal) { ["workspace"] = workspace })
                : ("sync-all", new Dictionary<string, string>(StringComparer.Ordinal));
        return PressViewed.Shown(new PressView(kind, facts, command, rows));
    }

    private static string? Named(string text) => text.Length > 0 ? text : null;

    private static string Commits(JsonElement row) =>
        row.TryGetProperty("commits", out var commits) && commits.ValueKind == JsonValueKind.Number && commits.TryGetInt32(out var count)
            ? count.ToString(CultureInfo.InvariantCulture)
            : "0";
}

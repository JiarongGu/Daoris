using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris tree remove &lt;session|branch&gt; [--repository &lt;name&gt;] [--force]</c> as the card shows it
/// (<c>DISCARD_SESSION_BRANCH</c>): a failed or superseded attempt's session branch discarded with its tree, named by the session or
/// the branch as the verb names it, or by the branch and its repository as the page does. The discard itself keeps a branch a running
/// or waiting session holds, forced or not.
/// </summary>
public sealed class SessionBranchDiscardView : IPressView
{
    public string Module => DriverModule.Name;

    public string Route => "DISCARD_SESSION_BRANCH";

    public Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        var named = PressPayload.Text(payload, "named");
        var branch = named.Length > 0 ? named : PressPayload.Text(payload, "branch");
        if (branch.Length == 0)
        {
            return Task.FromResult(PressViewed.Refused("a discard names one session branch, or its session — `daoris tree remove <session|branch>`."));
        }

        var repository = PressPayload.Text(payload, "repository");
        var force = payload is { } body && PressPayload.Flag(body, "force");
        var command = $"daoris tree remove {ShellWord.Of(branch, ShellWord.Name)}"
            + (repository.Length > 0 ? $" --repository {ShellWord.Of(repository, ShellWord.Name)}" : "") + (force ? " --force" : "");
        var facts = new Dictionary<string, string>(StringComparer.Ordinal) { ["branch"] = branch };
        if (repository.Length > 0) facts["repository"] = repository;
        IReadOnlyList<PressSaid>? rows = force ? [new PressSaid("discard-forced")] : null;
        return Task.FromResult(PressViewed.Shown(new PressView(repository.Length > 0 ? "discard-branch-in" : "discard-branch", facts, command, rows)));
    }
}

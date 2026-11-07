using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The word on a staged update, said as the steward's: <i>now</i> installs it and <i>cancel</i> keeps it staged for that
/// build, the install's own word (<see cref="InstallUpdater.SayAsync"/>, logged <c>update.requested</c> with the door <c>steward</c>). The
/// form is empty: nothing is named, the staged build is the one. The pipe has no person's route for an update (the screen's is a window's
/// and the terminal's is the install's request file), so outside the granted acts it is refused <c>not-granted</c>, never asked.
/// </summary>
public sealed class StewardUpdateAct(string name, string mode) : IRoleAct
{
    public string Name => name;

    /// <summary>The install: an update is no repository's, quest's or session's, so it depends on nothing, as a desk request does.</summary>
    public const string Kind = "update";

    public string TargetKind => Kind;

    public bool IsForm(JsonElement form) => StewardActRows.Exact(form, new Dictionary<string, Func<JsonElement, bool>>());

    public string Target(JsonElement form) => Kind;

    public async Task<object?> RunAsync(RoleActRun run, CancellationToken ct)
    {
        var world = run.World ?? throw new InvalidOperationException("no world was handed to the act.");
        await world.UpdateAsync(mode, ct).ConfigureAwait(false);
        run.Judge.StewardDid(run.Id, name);
        return new { Act = name };
    }
}

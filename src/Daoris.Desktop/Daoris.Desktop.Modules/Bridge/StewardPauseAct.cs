using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// A quest's pause and its resume as the steward's own (STEWARDROLE1d): the person's pause stops each live session as the person's stop, which
/// this never writes. The sessions it stops say the steward paused the quest in a code of their own, and its resume releases them as the
/// steward's release (<c>roleReleases</c>), taken or ended with its operation. It resumes only a pause the steward made this run that
/// nothing of the owner's has touched since; outside the granted acts either asks as the person's <c>WORK_PAUSE</c> or <c>WORK_RESUME</c>.
/// </summary>
public sealed class StewardPauseAct(bool pause) : IRoleAct
{
    public string Name => pause ? StewardActNames.Pause : StewardActNames.Unpause;

    public string? PersonRoute => pause ? "WORK_PAUSE" : "WORK_RESUME";

    public string TargetKind => OwnerKinds.Quest;

    public bool IsForm(JsonElement form) => StewardActRows.Exact(
        form, new Dictionary<string, Func<JsonElement, bool>>(StringComparer.OrdinalIgnoreCase) { ["quest"] = StewardActRows.Named });

    public string Target(JsonElement form) => StewardActRows.Text(form, "quest").TrimStart('#');

    public async Task<string?> UnderGatesAsync(RoleActRun run, CancellationToken ct)
    {
        var world = run.World ?? throw new InvalidOperationException("no world was handed to the act.");
        var quest = Target(run.Form);
        if (await world.QuestStatusAsync(quest, ct).ConfigureAwait(false) is null) return StewardWhy.NoQuest;
        if (pause) return world.Config().PausedQuest(quest) is null ? null : StewardWhy.AlreadyPaused;
        // A pause the person lifted since is no pause to resume: the world says so, and the act is refused.
        return run.Judge.StewardPaused(quest) ? null : StewardWhy.NotOwn;
    }

    public async Task<object?> RunAsync(RoleActRun run, CancellationToken ct)
    {
        var world = run.World ?? throw new InvalidOperationException("no world was handed to the act.");
        var quest = Target(run.Form);
        if (pause)
        {
            await world.PauseQuestAsync(quest, ct).ConfigureAwait(false);
        }
        else
        {
            // The release the resume writes is the steward's operation: one session per quest, the one the pause stopped.
            if (world.Config().PausedQuest(quest)?.Stopped.GetValueOrDefault(quest) is { } session)
            {
                run.Judge.Pend(run.Id, Name, [StewardStages.Released], binds: false, new { Quest = quest, Session = session });
            }

            await world.UnpauseQuestAsync(quest, run.Id, ct).ConfigureAwait(false);
        }

        run.Judge.StewardDid(run.Id, Name, quest: quest);
        return new { Act = Name, Quest = quest };
    }
}

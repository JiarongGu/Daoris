using Daoris.Contracts.Quests;

namespace Daoris.Driver;

/// <summary>
/// A quest's done held by its departure from its brief, answered by the risk policy at the quest's place: the act is <c>work.done</c>, or
/// <c>direction.set</c> where the done departs from every requirement, so its target moved. A pass is posted to the service as the policy's
/// yes, its act, rule and revision the yes's author (<see cref="RiskPassWire"/>), journaled and said; a gate on a role or on the owner waits
/// for that yes. The service keeps the yes and judges nothing. Evidence still awaited beside the departure is a dependency: nothing is judged
/// until it is read.
/// </summary>
internal static class QuestDepartures
{
    /// <summary>The hold a departure's verdict is journaled under.</summary>
    public const string Hold = EvidenceCodes.Departed;

    /// <summary>The act <paramref name="quest"/>'s departure is, or null where nothing but a departure's yes holds its done.</summary>
    public static RiskAct? ActOf(QuestView quest)
    {
        if (quest is not { Held: true, Hold: EvidenceCodes.Departed, AwaitsEvidence: false } || !QuestResults.Judged(quest)) return null;
        var left = quest.Answers.Where(answer => string.IsNullOrWhiteSpace(answer.Met) && !string.IsNullOrWhiteSpace(answer.Departed)).Select(answer => answer.Requirement).Distinct().Count();
        return RiskAct.Of(quest.Requirements.Count > 0 && left >= quest.Requirements.Count ? RiskActs.DirectionSet : RiskActs.WorkDone);
    }

    /// <summary>The judge at <paramref name="quest"/>'s place, its workspace and the repository it is asked of.</summary>
    public static Func<RiskAct, RiskVerdict?> JudgeAt(DriverConfig config, QuestView quest)
    {
        var judge = RiskStepGates.At(config, quest.Workspace, quest.To);
        return act => RiskStepGates.Verdict(judge, act.Kind, null);
    }

    /// <summary>The verdict on <paramref name="quest"/>'s departure, or null where none holds it or no judge is read.</summary>
    public static RiskVerdict? Verdict(QuestView quest, Func<RiskAct, RiskVerdict?>? judge) => judge is not null && ActOf(quest) is { } act ? judge(act) : null;

    /// <summary>
    /// Each held departure of <paramref name="quests"/> the policy passes, posted through <paramref name="accept"/> as its yes and, once the
    /// service kept it, journaled; what each pass says. A yes the service refused is asked again at the next look and said once, as
    /// <paramref name="told"/> keeps for the quest and the policy's revision.
    /// </summary>
    public static async Task<IReadOnlyList<string>> AnswerAsync(
        string home, DriverConfig config, IEnumerable<QuestView> quests, Func<string, RiskPassWire, CancellationToken, Task<(bool Ok, string Message)>> accept,
        ISet<string> told, DateTimeOffset now, CancellationToken ct)
    {
        var said = new List<string>();
        foreach (var quest in quests)
        {
            if (Verdict(quest, JudgeAt(config, quest)) is not { Outcome: RiskVerdict.Pass } verdict) continue;
            var (ok, message) = await accept(quest.Id, new RiskPassWire(verdict.Kind, verdict.Source, verdict.Revision), ct).ConfigureAwait(false);
            if (!ok)
            {
                if (told.Add($"{quest.Id}@{verdict.Revision}")) said.Add($"quest #{quest.Id}: the service refused the risk policy's yes to its departure, and it is asked again at each look: {message}");
                continue;
            }

            told.Remove($"{quest.Id}@{verdict.Revision}");
            var place = RiskPlace.Of(config, quest.Workspace, quest.To);
            try
            {
                RiskJournal.Append(home, new RiskJudged(now, RiskJudged.Released, $"{Hold}:{quest.Id}", Hold, verdict.Kind, verdict.Qualifier, RiskPolicy.Key,
                    place.Workspace, place.Repository, place.Mode, null, null, $"quest {quest.Id}'s departure", verdict.Outcome, verdict.Class, verdict.By,
                    verdict.Source, verdict.Revision, verdict.Reason, PolicyPasses.Today, false));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // The journal is a record of the pass, never a reason to keep the done held: the service kept the yes.
            }

            said.Add($"quest #{quest.Id}: its departure is answered by the risk policy, so its done goes on: {verdict.Why}.");
        }

        return said;
    }
}

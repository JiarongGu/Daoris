namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's <c>delete</c> proposal (HELP6): a quest or an ask made by mistake — its door <c>quest</c> or
/// <c>ask</c>, its target the record's id — judged by the service's own reading of whether the record may go
/// (D95), the one the drawer's and the record's Delete are shown by. The card says what goes.
/// </summary>
/// <remarks>
/// Not D95's rejected <i>delete over MCP</i>: the tool only proposes, and the delete is the person's press,
/// applied through the local host's <c>DELETE /api/quests/{id}</c> and <c>DELETE /api/asks/{id}</c>.
/// </remarks>
internal sealed class HelpDeleteProposals : IHelpProposalKind
{
    public string Kind => "delete";

    public string Tool => "delete_propose";

    public IReadOnlyList<string> Doors { get; } = ["quest", "ask"];

    public HelpPlan Plan(HelpProposal proposal, DriverConfig config, HelpMachineFacts facts)
    {
        var id = proposal.Target?.Trim().TrimStart('#') ?? "";
        HelpPlan Refused(string why, string terminal) => new(why, "", terminal, null);
        string Quest(string quest) => facts.Quests.FirstOrDefault(each => each.Id == quest) is { } found
            ? $"`#{quest}` “{found.Title}”"
            : $"`#{quest}`";

        if (proposal.Door == "quest")
        {
            var terminal = $"daoris-driver quest delete {id}";
            if (facts.Quests.FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.OrdinalIgnoreCase)) is not { } quest)
            {
                return Refused($"there is no quest `#{id}` on this machine.", terminal);
            }

            if (!quest.Deletable) return Refused(Kept(quest), terminal);
            return new HelpPlan(null,
                $"Delete quest `#{quest.Id}` “{quest.Title}”, for `{quest.To}`: it goes from every list, and nothing brings it back.",
                terminal, null);
        }

        if (proposal.Door == "ask")
        {
            var terminal = $"daoris-driver ask --delete {id}";
            if (facts.Asks.FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.OrdinalIgnoreCase)) is not { } ask)
            {
                return Refused($"there is no ask `#{id}` on this machine.", terminal);
            }

            if (!ask.Deletable)
            {
                var standing = ask.Quests
                    .Select(quest => facts.Quests.FirstOrDefault(each => each.Id == quest))
                    .OfType<HelpQuestFacts>()
                    .Where(quest => !quest.Deletable)
                    .Select(quest => $"`#{quest.Id}` is {Spoken(quest)}")
                    .ToList();
                return Refused(
                    $"ask `#{ask.Id}` must stay: a quest it became has been started on"
                    + (standing.Count > 0 ? $" ({string.Join(", ", standing)})" : "")
                    + ", and an ask goes with every quest it became or not at all. Leave it, or close the ask instead, with the reason.",
                    terminal);
            }

            var goes = ask.Quests.Count switch
            {
                0 => ": it became no quest, so it goes alone.",
                1 => $", with the quest it became: {Quest(ask.Quests[0])}.",
                var count => $", with the {count} quests it became: {string.Join(", ", ask.Quests.Select(Quest))}.",
            };
            return new HelpPlan(null, $"Delete ask `#{ask.Id}` “{ask.Sentence}”{goes}", terminal, null);
        }

        return Refused($"`{proposal.Door}` is not a record Daoris deletes — a `quest` or an `ask`.", "");
    }

    public async Task<HelpApplied> ApplyAsync(HelpApplying applying, CancellationToken ct)
    {
        var proposal = applying.Proposal;
        var target = proposal.Target!.Trim().TrimStart('#');
        var (ok, message) = proposal.Door == "quest"
            ? await applying.Doors.DeleteQuestAsync(target, ct).ConfigureAwait(false)
            : await applying.Doors.DeleteAskAsync(target, ct).ConfigureAwait(false);
        return applying.Settled(ok, $"{(ok ? "Applied" : "Not applied")}: `#{applying.Id}` (`{applying.Plan.Terminal}`) — {message}", message);
    }

    /// <summary>Why a quest's record stays, with what to do instead — said as the service's refusal says it (D95).</summary>
    private static string Kept(HelpQuestFacts quest)
    {
        const string Decline = "Decline it instead, with the reason, and the asker hears why.";
        const string Closed = "A closed quest already leaves the list.";
        return Spoken(quest) switch
        {
            "taken" => $"quest `#{quest.Id}` is taken — someone is working it, and its record stays. {Decline}",
            "done" => $"quest `#{quest.Id}` is done — it is the record of work that was answered. {Closed}",
            "declined" => $"quest `#{quest.Id}` is declined — the decline is the trace of a decision, and its reason is the asker's to read. {Closed}",
            _ => $"quest `#{quest.Id}` was started on — a session's record names it, or a taken quest waits on it as its question. {Decline}",
        };
    }

    private static string Spoken(HelpQuestFacts quest) => quest.Status.ToLowerInvariant();
}

/// <summary>A quest as the service answered it, with its own reading of whether it may be deleted (D95).</summary>
/// <param name="Status">`Open`, `Taken`, `Done` or `Declined`, as the service spells it.</param>
public sealed record HelpQuestFacts(string Id, string Title, string To, string Status, bool Deletable);

/// <summary>An ask as the service answered it, with the quests it became and whether it may be deleted (D95).</summary>
public sealed record HelpAskFacts(string Id, string Sentence, string Workspace, string State, IReadOnlyList<string> Quests, bool Deletable);

public sealed partial record HelpMachineFacts
{
    /// <summary>The quests, closed ones included, each with the service's own reading of whether it may be deleted.</summary>
    public IReadOnlyList<HelpQuestFacts> Quests { get; init; } = [];

    /// <summary>The asks, closed ones included, each with the service's own reading of whether it may be deleted.</summary>
    public IReadOnlyList<HelpAskFacts> Asks { get; init; } = [];
}

public static partial class HelpProposals
{
    /// <summary>
    /// The records a delete is judged against (HELP6): every quest and ask, closed ones included, each
    /// with the service's own reading of whether it may go — the answer the drawer's Delete is shown by.
    /// </summary>
    public static async Task<(IReadOnlyList<HelpQuestFacts> Quests, IReadOnlyList<HelpAskFacts> Asks)> RecordsAsync(
        ServiceClient service, CancellationToken ct)
    {
        var quests = await service.EveryQuestAsync(ct).ConfigureAwait(false);
        var asks = await service.EveryAskAsync(ct).ConfigureAwait(false);
        return (
            [.. quests.Select(quest => new HelpQuestFacts(quest.Id, quest.Title, quest.To, quest.Status, quest.Deletable))],
            [.. asks.Select(ask => new HelpAskFacts(ask.Id, ask.Sentence, ask.Workspace, ask.State, ask.Quests, ask.Deletable))]);
    }
}

public partial interface IHelpDoors
{
    /// <summary>The local host's <c>DELETE /api/quests/{id}</c>, the quest drawer's route.</summary>
    Task<(bool Ok, string Message)> DeleteQuestAsync(string id, CancellationToken ct);

    /// <summary>The local host's <c>DELETE /api/asks/{id}</c>, the ask's record's route.</summary>
    Task<(bool Ok, string Message)> DeleteAskAsync(string id, CancellationToken ct);
}

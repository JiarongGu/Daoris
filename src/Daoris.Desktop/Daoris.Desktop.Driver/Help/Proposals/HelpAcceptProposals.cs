namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's <c>accept</c> proposal (DRIFT1d2, D133 §4): the person's yes to a done's departure from what they required —
/// its door <c>accept</c>, its target the quest's id — judged against the quest as the service answers it: held, with the
/// departures its done answered. The card shows each departure with the person's words it relied on, and what the yes lets
/// go on.
/// </summary>
/// <remarks>
/// <para>Not the yes over MCP that D133's DRIFT1d note rules out: the tool only proposes, and the yes is the person's
/// press, applied through the local host's <c>POST /api/quests/{id}/accept</c>, the door the quest page's
/// <i>Accept the departure</i> and <c>daoris-driver quest accept</c> use.</para>
///
/// <para>A refusal says what the service's door would say of the same quest, so the helper hears the route's words and
/// the person is never shown a card the door would refuse.</para>
/// </remarks>
internal sealed class HelpAcceptProposals : IHelpProposalKind
{
    public string Kind => "accept";

    public string Tool => "accept_propose";

    public IReadOnlyList<string> Doors { get; } = ["accept"];

    public HelpPlan Plan(HelpProposal proposal, DriverConfig config, HelpMachineFacts facts)
    {
        var id = proposal.Target?.Trim().TrimStart('#') ?? "";
        var terminal = $"daoris-driver quest accept {id}";
        HelpPlan Refused(string why) => new(why, "", terminal, null);

        if (proposal.Door != "accept") return new($"`{proposal.Door}` is not a door of an accept — `accept`.", "", "", null);
        if (facts.QuestRecords.FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.OrdinalIgnoreCase)) is not { } quest)
        {
            return Refused($"there is no quest `#{id}` on this machine.");
        }

        var departures = Departures(quest);
        if (!quest.Held) return Refused(NotHeld(quest, departures.Count > 0));
        // A yes accepts a departure or its evidence, never a review (REVIEWENV1b2, D154 point 9): the door refuses a quest held
        // for its review alone, and says what does let it go.
        if (quest.Hold == EvidenceCodes.Unreviewed) return Refused(Unreviewed(quest));

        var departed = departures.Count == 1
            ? $"it departed from requirement {departures[0].Requirement}"
            : $"it departed from requirements {Spoken(departures.Select(each => each.Requirement.ToString(System.Globalization.CultureInfo.InvariantCulture)))}";
        return new HelpPlan(null,
            $"Accept the departure on quest `#{quest.Id}` “{quest.Title}”, for `{quest.To}`: {departed}, and what it held goes on — "
            + $"{GoesOn(quest, facts.QuestRecords)}.",
            terminal, null)
        {
            Accept = new HelpAcceptPlan(quest.Id, quest.Title, departures),
        };
    }

    public async Task<HelpApplied> ApplyAsync(HelpApplying applying, CancellationToken ct)
    {
        var target = applying.Proposal.Target!.Trim().TrimStart('#');
        var (ok, message) = await applying.Doors.AcceptDepartureAsync(target, ct).ConfigureAwait(false);
        return applying.Settled(ok, $"{(ok ? "Applied" : "Not applied")}: `#{applying.Id}` (`{applying.Plan.Terminal}`) — {message}", message);
    }

    /// <summary>Each requirement its done departed from, with the person's words the requirement quotes and its check.</summary>
    private static List<HelpDeparture> Departures(QuestView quest) =>
    [
        .. quest.Answers
            .Where(answer => !string.IsNullOrWhiteSpace(answer.Departed))
            .OrderBy(answer => answer.Requirement)
            .Select(answer =>
            {
                var required = answer.Requirement >= 1 && answer.Requirement <= quest.Requirements.Count
                    ? quest.Requirements[answer.Requirement - 1]
                    : new QuestRequirementView("", "");
                return new HelpDeparture(answer.Requirement, required.Quote, required.Check, answer.Departed!, answer.Quote ?? "");
            }),
    ];

    /// <summary>Why no departure holds it, as the service's accept door says it of the same quest.</summary>
    private static string NotHeld(QuestView quest, bool departed) => quest.Status switch
    {
        "Done" when departed => $"quest `#{quest.Id}`'s departure was already accepted: nothing waits for a yes.",
        "Done" => $"quest `#{quest.Id}` closed done departing from none of what you required: nothing waits for a yes.",
        var status => $"quest `#{quest.Id}` is {status.ToLowerInvariant()}: only a quest closed done with a departure from what "
            + "you required waits for your yes.",
    };

    /// <summary>Why a yes does not let a quest held for its review go, as the service's accept door says it of the same quest.</summary>
    private static string Unreviewed(QuestView quest) =>
        $"quest `#{quest.Id}` waits for your review{(quest.SetUpIn is { Length: > 0 } environment ? $" in `{environment}`" : "")}, "
        + "which a yes does not give: say `reviewed` once you have looked at what it shows, or skip the review for this work.";

    /// <summary>
    /// What the yes lets go on: the chain's next step it held, as the service will publish it (<c>{parent}</c> is this
    /// quest's id), and each quest whose taker waits on it (D79); or that nothing follows it.
    /// </summary>
    private static string GoesOn(QuestView quest, IReadOnlyList<QuestView> quests)
    {
        var goes = new List<string>();
        if (quest.Then.FirstOrDefault() is { } next)
        {
            goes.Add($"its next step, “{next.Title.Replace("{parent}", quest.Id, StringComparison.Ordinal)}” to `{next.To}`, is published");
        }

        var waiting = quests
            .Where(each => each.Status == "Taken" && string.Equals(each.Awaits, quest.Id, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (waiting.Count > 0)
        {
            goes.Add($"{Spoken(waiting.Select(each => $"`#{each.Id}` “{each.Title}”"))}, waiting on it, {(waiting.Count == 1 ? "goes" : "go")} on");
        }

        return goes.Count > 0
            ? string.Join(", and ", goes)
            : "nothing follows it, so the yes says you have seen the departure, and the ask it served can read done";
    }

    private static string Spoken(IEnumerable<string> items)
    {
        var all = items.ToList();
        return all.Count <= 1 ? string.Join("", all) : $"{string.Join(", ", all.Take(all.Count - 1))} and {all[^1]}";
    }
}

/// <summary>
/// What an accept's card shows (DRIFT1d2): the quest, and each departure its done answered, which the person reads before
/// their yes. The page's <c>HelpAcceptShown</c> carries the same.
/// </summary>
public sealed record HelpAcceptPlan(string Quest, string Title, IReadOnlyList<HelpDeparture> Departures);

/// <summary>
/// One departure (DRIFT1d, D133 §4): the requirement's number, the person's words it quotes and its check, then the done's
/// reason and the person's words that reason relied on, each as the service answered it.
/// </summary>
public sealed record HelpDeparture(int Requirement, string Required, string Check, string Reason, string Words);

public sealed partial record HelpPlan
{
    /// <summary>For an accept the route takes (DRIFT1d2): the quest and its departures, for the card; null for every other kind.</summary>
    public HelpAcceptPlan? Accept { get; init; }
}

public sealed partial record HelpMachineFacts
{
    /// <summary>
    /// Every quest as the service answers it, closed ones included, with what the person required, how each done answered
    /// and whether a departure holds it (DRIFT1d2): what an accept is judged against, read only when one is pending.
    /// </summary>
    public IReadOnlyList<QuestView> QuestRecords { get; init; } = [];
}

public partial interface IHelpDoors
{
    /// <summary>The local host's <c>POST /api/quests/{id}/accept</c>, the quest page's yes and the terminal's.</summary>
    Task<(bool Ok, string Message)> AcceptDepartureAsync(string id, CancellationToken ct);
}

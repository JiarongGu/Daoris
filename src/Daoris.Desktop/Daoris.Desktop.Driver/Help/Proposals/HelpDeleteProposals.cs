namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's <c>delete</c> proposal (HELP6): a quest or an ask made by mistake — its door <c>quest</c> or
/// <c>ask</c>, its target the record's id — judged by the service's own reading of whether the record may go
/// (D95), the one the drawer's and the record's Delete are shown by. The card says what goes. Since SESSUX1h2b a
/// <c>session</c> door too (D126 §5.4): a conversation that served no quest, judged by both halves as Sessions' *Delete…* is.
/// </summary>
/// <remarks>
/// Not D95's rejected <i>delete over MCP</i>: the tool only proposes, and the delete is the person's press,
/// applied through the local host's <c>DELETE /api/quests/{id}</c> and <c>DELETE /api/asks/{id}</c>, and a session's
/// through Sessions' own delete.
/// </remarks>
public sealed class HelpDeleteProposals : IHelpProposalKind
{
    /// <summary>
    /// The records a delete is judged against (HELP6): every quest and ask, closed ones included, each
    /// with the service's own reading of whether it may go — the answer the drawer's Delete is shown by.
    /// </summary>
    /// <remarks>PART4: here, with the kind that reads it, rather than in a second part of <see cref="HelpProposals"/>; the class is public for the Modules' bridge.</remarks>
    public static async Task<(IReadOnlyList<HelpQuestFacts> Quests, IReadOnlyList<HelpAskFacts> Asks)> RecordsAsync(
        ServiceClient service, CancellationToken ct)
    {
        var quests = await service.EveryQuestAsync(ct).ConfigureAwait(false);
        var asks = await service.EveryAskAsync(ct).ConfigureAwait(false);
        return (
            [.. quests.Select(quest => new HelpQuestFacts(quest.Id, quest.Title, quest.To, quest.Status, quest.Deletable, quest.Workspace))],
            [.. asks.Select(ask => new HelpAskFacts(ask.Id, ask.Sentence, ask.Workspace, ask.State, ask.Quests, ask.Deletable))]);
    }

    public string Kind => "delete";

    public string Tool => "delete_propose";

    // SESSUX1h2b: a session's delete, which D126 §7.3 gave this kind rather than the `session` kind.
    public IReadOnlyList<string> Doors { get; } = ["quest", "ask", "session"];

    /// <summary>ASKSCOPE1c2: the quest, the ask or the session it names by id, filed under the workspace its record says.</summary>
    HelpNames IHelpProposalKind.Named(HelpProposal proposal, HelpMachineFacts facts)
    {
        var id = proposal.Target?.Trim().TrimStart('#') ?? "";
        return new([proposal.Workspace], [proposal.Repository])
        {
            Records = proposal.Door switch
            {
                "quest" => HelpNames.OfQuest(facts, id),
                "ask" => HelpNames.OfAsk(facts, id),
                "session" => HelpNames.OfSession(facts, id),
                _ => [],
            },
        };
    }

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

        if (proposal.Door == "session") return Session(id, facts);

        return Refused($"`{proposal.Door}` is not a record Daoris deletes — a `quest`, an `ask` or a `session`.", "");
    }

    async Task<HelpApplied> IHelpProposalKind.ApplyAsync(HelpApplying applying, CancellationToken ct)
    {
        var proposal = applying.Proposal;
        var target = proposal.Target!.Trim().TrimStart('#');
        var (ok, message) = proposal.Door switch
        {
            "quest" => await applying.Doors.DeleteQuestAsync(target, ct).ConfigureAwait(false),
            "ask" => await applying.Doors.DeleteAskAsync(target, ct).ConfigureAwait(false),
            // SESSUX1h2b: the record's own spelling, as the plan resolved it.
            _ => await applying.Doors.DeleteSessionAsync(applying.Plan.Sessions![0], ct).ConfigureAwait(false),
        };
        return applying.Settled(ok, $"{(ok ? "Applied" : "Not applied")}: `#{applying.Id}` (`{applying.Plan.Terminal}`) — {message}", message);
    }

    /// <summary>
    /// SESSUX1h2b (D126 §5.4): a session's delete, judged in the order <see cref="SessionDeletion.DeleteAsync"/> judges it and
    /// in its words — no such session here, a teammate's, then the record's half as the ledger answers it, then the machine's
    /// half — with Ask Daoris's own conversation refused before either, since its History deletes it (ASKHIST1).
    /// </summary>
    private static HelpPlan Session(string id, HelpMachineFacts facts)
    {
        var terminal = $"daoris-driver sessions delete {id}";
        HelpPlan Refused(string why) => new(why, "", terminal, null);
        if (id.Length == 0) return Refused("a delete names the session by its id, as Sessions lists it.");
        if (facts.Sessions.FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.OrdinalIgnoreCase)) is not { } record)
        {
            return Refused($"there is no session `{id}` on this machine.");
        }

        id = record.Id;
        terminal = $"daoris-driver sessions delete {id}";
        if (record.Teammate)
        {
            return Refused($"Session `{id}` ran on `{id[..id.IndexOf('/')]}`; its record is theirs — archive it here instead.");
        }

        if (WordsNever.IsHelp(record))
        {
            return Refused($"`{id}` is Ask Daoris's own conversation: Ask Daoris → History deletes it, as `daoris-driver help delete {id}` does.");
        }

        if (record.Live) return Refused($"Session `{id}` is still running; stop it first.");

        // The ledger's own sentence where it was asked for this one; else what its record's answer says.
        if (facts.SessionDeletes.GetValueOrDefault(id) is { Taken: false } judged) return Refused(judged.Message);
        if (!facts.SessionDeletes.ContainsKey(id) && !record.Deletable)
        {
            return Refused(record.Quest is { } quest ? $"Session `{id}` worked on `#{quest}`, and its record is that work's; archive it instead."
                : record.Took ? $"Session `{id}` took a quest, and its record is that work's; archive it instead."
                : $"Session `{id}`'s record stays, as the service judges it; archive it instead.");
        }

        if (facts.Kept.Contains(id))
        {
            return Refused($"Session `{id}` is still held on this machine: its tree is here, or a landing names it. Discard the tree or "
                           + "clean it up first; a session a landing names stays with that landing.");
        }

        var where = record.Ask is { } ask ? $", the intake of ask `#{ask}`" : $", a conversation in `{record.Repository}`";
        return new HelpPlan(null,
            $"Delete session `{id}`{where}: its record goes, with what this machine kept of it (its words, its transcript and its "
            + "files), and nothing brings it back.",
            terminal, null)
        {
            Sessions = [id],
        };
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
/// <param name="Workspace">
/// The circle it is held in (D48), as the service answered it, the default where a host answers none: what a conversation bound to
/// one workspace judges a proposal naming it by (ASKSCOPE1c2).
/// </param>
public sealed record HelpQuestFacts(string Id, string Title, string To, string Status, bool Deletable, string Workspace = RemoteTarget.DefaultWorkspace);

/// <summary>An ask as the service answered it, with the quests it became and whether it may be deleted (D95).</summary>
public sealed record HelpAskFacts(string Id, string Sentence, string Workspace, string State, IReadOnlyList<string> Quests, bool Deletable);

public interface IHelpDeleteDoors
{
    /// <summary>The local host's <c>DELETE /api/quests/{id}</c>, the quest drawer's route.</summary>
    Task<(bool Ok, string Message)> DeleteQuestAsync(string id, CancellationToken ct);

    /// <summary>The local host's <c>DELETE /api/asks/{id}</c>, the ask's record's route.</summary>
    Task<(bool Ok, string Message)> DeleteAskAsync(string id, CancellationToken ct);

    /// <summary>
    /// Sessions' own *Delete…* (SESSUX1f, D126 §5.4): <see cref="SessionDeletion.DeleteAsync"/>, which judges both halves again
    /// and removes what this machine kept after the ledger's yes. Its answer comes back as its sentence, a refusal included.
    /// </summary>
    Task<(bool Ok, string Message)> DeleteSessionAsync(string id, CancellationToken ct);
}

using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// The quests held on another agent (MIXAGENT1a-ii, D163): <c>&lt;home&gt;/handed/&lt;quest&gt;.json</c>, written atomically. It is the quest's
/// own override of the machine's adapter, held until the quest leaves this machine (done, declined, taken elsewhere, cleared or
/// abandoned), never for one start; and it is the release of the stop that handed it over: the planner reads a stop whose session it
/// names as released, so no strike counts and driver.json's <c>released</c> is never written.
/// </summary>
/// <remarks>
/// <para><b>This machine's, never the record's.</b> A file that does not read is no override: the quest runs on the machine's agent.</para>
///
/// <para>What is kept is this store's; the files and their format are <see cref="HandOverFiles"/>' (STRUCT3h, D166's note).</para>
/// </remarks>
public sealed class HandedQuests(string home)
{
    /// <summary>Where a quest's file is kept, or null for an id that is no quest's name (never a path out of the folder).</summary>
    public string? PathOf(string quest) => Named(quest) ? DaorisHome.Entry(home, HomeEntry.Handed, quest + ".json") : null;

    /// <summary>Keep the quest as handed, replacing any before. An id that is no quest's name keeps nothing.</summary>
    /// <exception cref="IOException">It could not be written: the asker is told, rather than told it was kept.</exception>
    public void Write(string quest, HandedQuest handed)
    {
        if (PathOf(quest) is not { } path) return;
        HandOverFiles.WriteHanded(path, handed);
    }

    /// <summary>The quest as handed, or null for none, a file that does not read, or an id that is no quest's name.</summary>
    public HandedQuest? Read(string quest) => PathOf(quest) is { } path ? HandOverFiles.ReadHanded(path) : null;

    /// <summary>Every quest held on another agent, by quest id: what a look reads into its snapshot.</summary>
    public IReadOnlyDictionary<string, HandedQuest> All()
    {
        var all = new Dictionary<string, HandedQuest>(StringComparer.OrdinalIgnoreCase);

        // What did not list is read at the next look.
        foreach (var file in HandOverFiles.Leaves(DaorisHome.Entry(home, HomeEntry.Handed),"*.json").Order(StringComparer.Ordinal))
        {
            var quest = Path.GetFileNameWithoutExtension(file);
            if (Read(quest) is { } handed) all[quest] = handed;
        }

        return all;
    }

    /// <summary>Let the quest go back to the machine's agent: it left this machine, or was cleared. Nothing to clear is no failure.</summary>
    public void Clear(string quest)
    {
        // A file left behind is dropped at the next look, which reads the quest's state again.
        if (PathOf(quest) is { } path) HandOverFiles.Delete(path);
    }

    private static bool Named(string quest) => SafeNames.IsPlainId(quest ?? "");
}

/// <summary>
/// *Hand a quest's session over* (MIXAGENT1a-ii, D163 point 1; the mixed-agents design §2): the quest's own session goes on with
/// another agent when its account's allowance runs low. This is the ask's judge for a driven session with a quest, beside
/// <see cref="HandOver"/>'s for a conversation: it refuses what cannot be handed over by a code before anything is kept, and keeps the
/// same <see cref="HandOverBox"/> the driver's next look takes.
/// </summary>
/// <remarks>
/// <b>A driven session works its quest in one turn</b>, so there is no turn's end to wait for: the look stops it, as the person's stop,
/// and the quest is carried on in the same tree on the named agent. The judge is the look's too, run again as the box is taken.
/// </remarks>
public static class QuestHandOver
{
    /// <summary>
    /// The roster as a driven start reads it: the accounts it holds for an agent and the wire its door rides, or null for an agent
    /// this set does not have. The look's and the terminal's both, so the ask and the take judge by one answer.
    /// </summary>
    public static Func<string, HandOverAgent?> Roster(AdapterSet adapters, string home) => agent =>
    {
        try
        {
            var resolved = adapters.Resolve(agent);
            IReadOnlyList<string> accounts = resolved.Toolchain is { } toolchain
                ? [.. HarnessSettings.Profiles(home, toolchain.Owner(resolved.Name))]
                : [];
            return new HandOverAgent(accounts, resolved.Wire);
        }
        catch (DriverException)
        {
            return null;
        }
    };

    /// <summary>Whether a record is a quest's own session: driven, with a quest, never an intake's or a teammate's.</summary>
    public static bool IsQuestSession(SessionRecord record) =>
        record is { Kind: not "chat", Quest: not null, Ask: null, Teammate: false };

    /// <summary>Judge and keep the box for a quest's session of this machine's.</summary>
    public static async Task<HandOverAnswer> AskAsync(
        ServiceClient service, string home, Func<string, HandOverAgent?> drives, string id, string agent, string? account,
        string why, string askedBy, DateTimeOffset now, CancellationToken ct = default)
    {
        var (refused, _) = await JudgeAsync(service, drives, id, agent, account, ct, agentAsk: askedBy == HandOverBy.Agent).ConfigureAwait(false);
        if (refused is not null) return refused;

        var box = new HandOverBox(home);
        if (box.Read(id) is { } waiting)
        {
            return HandOver.Refused(HandOver.Waiting, $"{id} is already waiting to be handed over to {waiting.Agent}.");
        }

        try
        {
            box.Write(id, new HandOverRequest(agent, string.IsNullOrWhiteSpace(account) ? null : account.Trim(), why, askedBy, now));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return new HandOverAnswer(false, null, $"your ask could not be kept: {error.Message}");
        }

        return new HandOverAnswer(true, null, $"it is stopped at the driver's next look, then goes on in the same tree with {agent}.");
    }

    /// <summary>
    /// What can be known of a quest's hand-over before anything is kept or taken: the refusal by its code, or the record as the
    /// ledger holds it. Judged again as the look takes the box, since the quest, its take or the roster may have changed meanwhile.
    /// </summary>
    /// <param name="agentAsk">
    /// The session's own agent asked (MIXAGENT1c): a park it ended its turn on is that ask's, not a question for the person, so it is
    /// handed over rather than refused <see cref="HandOver.Parked"/>.
    /// </param>
    public static async Task<(HandOverAnswer? Refused, PriorSession? Prior)> JudgeAsync(
        ServiceClient service, Func<string, HandOverAgent?> drives, string id, string agent, string? account, CancellationToken ct = default,
        bool agentAsk = false)
    {
        var json = await service.SessionRecordsJsonAsync(ct).ConfigureAwait(false);
        var record = SessionRecords.Parse(json).FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.Ordinal));
        if (record is null) return (HandOver.Refused(WordsNever.NotFound, $"no session here is {id}."), null);

        var last = WordsNever.LastHere(json, record.Quest);
        if (HandOver.Never(record, id, last) is { } never) return (never, null);
        if (!IsQuestSession(record)) return (HandOver.Refused(HandOver.NotConversation, $"{id} is not a quest's session."), null);

        var quest = record.Quest!;
        switch (WordsNever.Judge(record, last))
        {
            case WordsNever.StoodDown:
                return (HandOver.Refused(HandOver.QuestElsewhere,
                    $"it stood down: #{quest} is someone else's, so there is nothing of this machine's to hand over."), null);
            case WordsNever.Superseded:
                return (HandOver.Refused(HandOver.NotLastRun, $"#{quest} went on in a later session here, {last}: hand that one over."), null);
        }

        // SESSCTX1c (D189's amendment): a park whose own agent is no longer here is never answered in its own conversation, so it waits
        // with this hand-over's offer, the person choosing the agent it goes on with.
        if (record.State == "awaiting-person" && !agentAsk && !AgentGone(ServiceClient.ReadRecord(json, id)?.Adapter, drives))
        {
            return (HandOver.Refused(HandOver.Parked, $"{id} is waiting on you: answer it instead."), null);
        }

        var roster = drives(agent);
        if (roster is null) return (HandOver.Refused(HandOver.UnknownAgent, $"no agent here is called {agent}."), null);

        var named = string.IsNullOrWhiteSpace(account) ? null : account.Trim();
        if (named is not null && !roster.Accounts.Contains(named, StringComparer.OrdinalIgnoreCase))
        {
            return (HandOver.Refused(HandOver.NotItsAccount, $"{named} is not an account of {agent}."), null);
        }

        var prior = ServiceClient.ReadRecord(json, id)!;
        if (string.Equals(prior.Adapter, agent, StringComparison.OrdinalIgnoreCase)
            && string.Equals(prior.Profile ?? "", named ?? "", StringComparison.OrdinalIgnoreCase))
        {
            return (HandOver.Refused(HandOver.SameAgent, named is null ? $"{id} already runs on {agent}." : $"{id} already runs on {agent} as {named}."), null);
        }

        var found = await service.FindQuestAsync(quest, ct).ConfigureAwait(false);
        if (found is not { Status: "Open" or "Taken" })
        {
            return (HandOver.Refused(HandOver.QuestClosed, $"#{quest} is not open or taken any more, so there is no work left to hand over."), null);
        }

        if (found.Status == "Taken")
        {
            var (claim, why) = await service.ClaimStandingAsync(quest, ct).ConfigureAwait(false);
            var take = ServiceClient.ReadTakes(json, new Dictionary<string, string> { [quest] = claim }, new Dictionary<string, string?> { [quest] = why })[quest];
            if (Planner.TakenAway(found, prior, take))
            {
                // FABRIC-CLOSE1c20: a take lost to the person's reopen is said as that, never as another machine's.
                if (take.Reopened)
                {
                    return (HandOver.Refused(HandOver.QuestReopened,
                        $"#{quest} lost its take because you reopened the work its step grew from, so its work is not this machine's to hand over."), null);
                }

                return (HandOver.Refused(HandOver.QuestElsewhere,
                    $"#{quest} is not taken by this machine, so its work is not this machine's to hand over."), null);
            }
        }

        var repository = (await service.RegistryAsync(ct).ConfigureAwait(false))
            .FirstOrDefault(each => string.Equals(each.Repository, record.Repository, StringComparison.OrdinalIgnoreCase));
        if (repository is null || (!repository.Adopted && (repository.Root is null || roster.Wire != SessionWire.Acp)))
        {
            return (HandOver.Refused(HandOver.NoDoor, repository is null
                ? $"`{record.Repository}` is not registered on this machine, so there is nowhere to go on."
                : $"{agent} drives only repositories that have adopted Daoris, and `{record.Repository}` has not: "
                  + "a session there would have no connector to take the quest with."), null);
        }

        return (null, prior);
    }

    /// <summary>Whether the agent a record ran on is one this machine no longer drives: named, and not in the roster.</summary>
    private static bool AgentGone(string? ranOn, Func<string, HandOverAgent?> drives) => ranOn is { Length: > 0 } && drives(ranOn) is null;
}

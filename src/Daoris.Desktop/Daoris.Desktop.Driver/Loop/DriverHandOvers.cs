using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A quest's session handed to another agent (MIXAGENT1a-ii, D163 point 1; the mixed-agents design §2): the look takes the boxes the
/// terminal kept, judges each again, stops the session as the person's, and holds the quest on the named agent
/// (<see cref="HandedQuests"/>) until it leaves this machine. The planner reads the stop as released and the next start runs on the
/// agent named, in the same tree.
/// </summary>
internal sealed class DriverHandOvers(LoopContext context)
{
    private readonly LoopContext _context = context;

    /// <summary>
    /// The agents a quest may go on with, as the roster reads them for a start: the accounts it holds for an agent and the wire its
    /// door rides. Null for an agent this build, with its plugins, does not have.
    /// </summary>
    internal HandOverAgent? Drives(string agent) => QuestHandOver.Roster(_context.Adapters, _context.Harnesses.Home)(agent);

    /// <summary>
    /// The quests handed to another agent, at a look: each box a terminal kept for a quest's session is taken, a quest that left this
    /// machine goes back to the machine's agent, and what stands is read into the snapshot for the planner. Nothing is read where no box
    /// and no store is on file. A conversation's box is the shell's chat runner's, and is left to it.
    /// </summary>
    internal async Task<Snapshot> HandOversAsync(Snapshot snapshot, List<string> events, CancellationToken ct)
    {
        var box = new HandOverBox(_context.Home);
        var handed = new HandedQuests(_context.Home);

        foreach (var id in box.Pending())
        {
            try
            {
                await TakeHandOverAsync(id, box, handed, events, ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is HttpRequestException or DriverException or JsonException or IOException
                                              or UnauthorizedAccessException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                events.Add($"hand-over  the hand-over of session `{id}` could not be looked at this look, and is looked at again at the next: {error.Message}");
            }
        }

        var standing = handed.All();
        if (standing.Count == 0) return snapshot;

        // Held until the quest leaves this machine (design §2): done, declined or gone from the open list, or taken elsewhere.
        foreach (var (quest, _) in standing)
        {
            var row = snapshot.Quests.FirstOrDefault(each => string.Equals(each.Id, quest, StringComparison.OrdinalIgnoreCase));
            var closed = row is not { Status: "Open" or "Taken" };
            var away = !closed && snapshot.Takes.TryGetValue(quest, out var take)
                       && snapshot.LastRun.TryGetValue(quest, out var last) && Planner.TakenAway(row!, last, take);
            if (!closed && !away) continue;

            // FABRIC-CLOSE1c20: a take lost to the person's reopen is said as that, never as another machine's.
            var reopened = !closed && snapshot.Takes[quest].Reopened;
            handed.Clear(quest);
            events.Add($"hand-over  `#{quest}` {(closed ? "has closed" : reopened ? "was lost to your reopen of the work it grew from" : "is taken elsewhere")}: it goes back to this machine's agent.");
        }

        return snapshot with { Handed = handed.All() };
    }

    private async Task TakeHandOverAsync(string id, HandOverBox box, HandedQuests handed, List<string> events, CancellationToken ct)
    {
        var service = _context.Service;
        var record = (await service.SessionRecordsAsync(ct).ConfigureAwait(false))
            .FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.Ordinal));
        // A conversation's box, or one for a record that is not here, is not this look's: the shell's chat runner takes the first.
        if (record is null || !QuestHandOver.IsQuestSession(record)) return;
        if (box.Read(id) is not { } ask)
        {
            box.Clear(id);
            return;
        }

        var quest = record.Quest!;
        var (refused, _) = await QuestHandOver.JudgeAsync(service, Drives, id, ask.Agent, ask.Account, ct).ConfigureAwait(false);
        if (refused is not null)
        {
            box.Clear(id);
            events.Add($"hand-over  session `{id}` (#{quest}): {refused.Message}");
            return;
        }

        // A driven session works its quest in one turn, so there is no turn's end to wait for: it is stopped at once, as the
        // person's stop, with a line of its own. One that already ended has nothing to stop; the stop it ended on is released all the same.
        if (record.Live)
        {
            var note = HandOverNotes.QuestHandedOver(ask.Agent);
            bool stopped;
            if (_context.Stops is { } stand)
            {
                stopped = stand(id, note);
            }
            else
            {
                var answer = await SessionMoves.StopAsync(_context.Processes, service, id, ct, note).ConfigureAwait(false);
                stopped = answer.Stopped;
            }

            if (!stopped)
            {
                box.Clear(id);
                events.Add($"hand-over  session `{id}` (#{quest}) runs in another Daoris process on this machine: hand it over there, or stop it first.");
                return;
            }
        }

        handed.Write(quest, new HandedQuest(ask.Agent, ask.Account, id, ask.Why, ask.AskedBy, ask.At));
        box.Clear(id);
        events.Add($"hand-over  session `{id}` (#{quest}): stopped, and `#{quest}` goes on with {ask.Agent}.");
    }
}

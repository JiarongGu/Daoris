using System.Globalization;

namespace Daoris.Driver;

/// <summary>Archive, unarchive and delete: the screen's owners, at the terminal's door.</summary>
internal static class SessionsArchiving
{
    /// <summary><c>archive &lt;id&gt;…</c>: the running Daoris's <c>SESSION_ARCHIVE</c> where one runs, a press its window confirms;
    /// this process's own archive where none runs. Both say <see cref="SessionArchive.ArchiveSaid"/>.</summary>
    internal static async Task<int> ArchiveAsync(SessionsWorld world, IReadOnlyList<string> ids, TextWriter output, CancellationToken ct) =>
        world.Instance is { } instance
            ? await InstanceDoor.PressAsync(
                PersonPresses.SessionArchive, new { ids, archived = true, said = true }, () => ArchiveAloneAsync(world, ids, output, ct),
                TreesSaid.Words(output, instance.Error, "sessions"), instance, ct).ConfigureAwait(false)
            : await ArchiveAloneAsync(world, ids, output, ct).ConfigureAwait(false);

    private static async Task<int> ArchiveAloneAsync(SessionsWorld world, IReadOnlyList<string> ids, TextWriter output, CancellationToken ct)
    {
        // Every part of each conversation is judged: it is archived or not as a whole.
        var parts = ConversationArchive.Parts(ids, await world.Service.SessionRecordsAsync(ct).ConfigureAwait(false));
        var look = await SessionGroups.LookAsync(world.Service, world.Config, world.Unadopted, lastLook: null, world.Home, parts, ct).ConfigureAwait(false);
        var answer = new ConversationArchive(world.Home).Archive(ids, SessionGroups.Read(look, parts), look.Records, DateTimeOffset.UtcNow);
        var said = ConversationArchive.ArchiveSaid(answer.Outcomes);
        said.Print(output, output);
        ConversationArchive.Said(world.Log, answer.Outcomes.Count(outcome => outcome.Verdict == ArchiveVerdict.Archived), WorkplaceDoors.Terminal);
        return said.Code;
    }

    /// <summary>At a terminal: the list first, what it would take and what stays; <c>--yes</c> archives what it holds, each judged again.</summary>
    internal static async Task<int> ArchiveEndedAsync(SessionsWorld world, bool yes, TextWriter output, CancellationToken ct)
    {
        var look = await SessionGroups.LookAsync(world.Service, world.Config, world.Unadopted, lastLook: null, world.Home, ct: ct).ConfigureAwait(false);
        var rows = SessionGroups.Read(look);
        var ended = rows.Where(row => row.Group is SessionGroup.Failed or SessionGroup.Done && !row.Archived).Select(row => row.Session).ToList();
        if (ended.Count == 0)
        {
            output.WriteLine("sessions: nothing that ended is left to archive.");
            return 0;
        }

        if (!yes)
        {
            output.WriteLine($"sessions: would archive {Sessions(ended.Count)} that ended:");
            foreach (var id in ended) output.WriteLine($"  {id}");
            var needsYou = rows.Count(row => row.Group == SessionGroup.NeedsYou);
            if (needsYou > 0) output.WriteLine($"sessions: kept in the list: {needsYou} needing you.");

            output.WriteLine("sessions: `--yes` archives them, each judged again as it goes.");
            return 0;
        }

        var answer = new ConversationArchive(world.Home).Archive(ended, rows, look.Records, DateTimeOffset.UtcNow);
        var archived = answer.Outcomes.Count(outcome => outcome.Verdict == ArchiveVerdict.Archived);
        ConversationArchive.Said(world.Log, archived, WorkplaceDoors.Terminal);
        output.WriteLine(archived == ended.Count
            ? $"sessions: archived {Sessions(archived)} that ended; `--group archived` lists them, and `unarchive` brings one back."
            : $"sessions: archived {archived} of {ended.Count}; the rest changed since the list.");
        return 0;
    }

    /// <summary><c>unarchive &lt;id&gt;…</c>: as <see cref="ArchiveAsync"/>, the same route with <c>archived: false</c>.</summary>
    internal static async Task<int> UnarchiveAsync(SessionsWorld world, IReadOnlyList<string> ids, TextWriter output, CancellationToken ct) =>
        world.Instance is { } instance
            ? await InstanceDoor.PressAsync(
                PersonPresses.SessionArchive, new { ids, archived = false, said = true }, () => UnarchiveAloneAsync(world, ids, output, ct),
                TreesSaid.Words(output, instance.Error, "sessions"), instance, ct).ConfigureAwait(false)
            : await UnarchiveAloneAsync(world, ids, output, ct).ConfigureAwait(false);

    private static async Task<int> UnarchiveAloneAsync(SessionsWorld world, IReadOnlyList<string> ids, TextWriter output, CancellationToken ct)
    {
        var back = new ConversationArchive(world.Home).Unarchive(ids, await world.Service.SessionRecordsAsync(ct).ConfigureAwait(false));
        var said = ConversationArchive.UnarchiveSaid(ids, back.NotArchived);
        said.Print(output, output);
        return said.Code;
    }

    /// <summary>The screen's owner at the terminal's door: 0 deleted, 1 refused, 2 deleted with a file the disk kept, as a clear's is.</summary>
    internal static async Task<int> DeleteAsync(SessionsWorld world, string id, TextWriter output, CancellationToken ct)
    {
        var outcome = await new SessionDeletion(world.Home, world.Remover)
            .DeleteAsync(world.Service, id, WorkplaceDoors.Terminal, world.Log, ct: ct).ConfigureAwait(false);
        output.WriteLine($"sessions: {outcome.Message}");
        return outcome.Verdict != DeleteVerdict.Deleted ? 1 : outcome.Stayed.Count > 0 ? 2 : 0;
    }

    private static string Sessions(int count) => $"{count.ToString(CultureInfo.InvariantCulture)} session{(count == 1 ? "" : "s")}";
}

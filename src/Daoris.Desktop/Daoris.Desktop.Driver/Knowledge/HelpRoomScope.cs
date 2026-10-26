namespace Daoris.Driver;

/// <summary>
/// A workspace's room reads its workspace (ASKSCOPE1b, D158's ASKSCOPE1 note point 4): the machine's facts (D181's, as
/// <see cref="HelpFacts.Read"/> reads them, and the driver's own files beside them) projected to one workspace before any section
/// counts or lists them, so another workspace's never fills its lists. What the machine holds for every workspace (the agents and
/// their accounts, sign-in and usage, the plugins and their approvals, Daoris's browser, the driver and its cap) is kept whole.
/// </summary>
/// <remarks>
/// <para><b>By the record's own workspace, else by its repository's.</b> A record that keeps its workspace (a repository, a line, a
/// session, a quest, an ask, finished work, a landing) is placed by it; one that keeps only its repository (a quest parked or held,
/// a unit of work, a recovery) by that repository's workspace now. A pause is placed by the ask or quest it holds.</para>
///
/// <para><b>Reading no more than the facts</b> (D181): nothing here asks git or the service, so a press reads no more than before.</para>
/// </remarks>
internal static class HelpRoomScope
{
    /// <summary>The facts of <paramref name="whole"/> that are <paramref name="workspace"/>'s, with the machine's kept whole.</summary>
    public static HelpMachineSources Project(HelpMachineSources whole, string workspace)
    {
        bool Ours(string? circle) => string.Equals(RemoteTarget.Workspace(circle), workspace, StringComparison.OrdinalIgnoreCase);

        var registry = whole.Snapshot.Repositories;
        var repositories = registry.Where(known => Ours(known.Workspace)).ToList();
        var own = repositories.Select(known => known.Repository).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // A repository named both here and in another workspace is this workspace's: what is said of it is its own.
        var others = registry.Where(known => !own.Contains(known.Repository)).Select(known => known.Repository)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var standing = whole.Standing.Where(ask => Ours(ask.Workspace)).ToList();
        var quests = whole.Snapshot.Quests.Where(quest => Ours(quest.Workspace)).ToList();

        return whole with
        {
            Workspace = workspace,
            Registry = registry,
            Snapshot = whole.Snapshot with
            {
                Repositories = repositories,
                Quests = quests,
                Active = [.. whole.Snapshot.Active.Where(session => Ours(session.Workspace))],
            },
            Lines = [.. whole.Lines.Where(line => Ours(line.Workspace))],
            Standing = standing,
            // Counted from its own asks, as the module counts the machine's.
            Asks = HelpRoom.AsksWaiting(standing),
            Landed = [.. whole.Landed.Where(landed => Ours(landed.Workspace))],
            Parked = [.. whole.Parked.Where(parked => own.Contains(parked.Repository))],
            Held = [.. whole.Held.Where(held => own.Contains(held.Repository))],
            // What the file keeps by workspace or by ask and quest: the pauses, and the kinds of work a workspace declares. What it
            // keeps by repository is read for this workspace's repositories alone.
            Config = whole.Config with
            {
                PausedAsks = Kept(whole.Config.PausedAsks, standing.Select(ask => ask.Id)),
                PausedQuests = Kept(whole.Config.PausedQuests, quests.Select(quest => quest.Id)),
                WorkspaceWorkflows = whole.Config.WorkspaceWorkflows.Where(pair => Ours(pair.Key))
                    .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            },
            // The Sessions list's groups are read from its records, so its own alone are counted and listed.
            Sessions = whole.Sessions is { } look ? look with { Records = [.. look.Records.Where(record => Ours(record.Workspace))] } : null,
            History = whole.History is { } history ? [.. history.Where(reading => Ours(reading.Workspace))] : null,
            Work = whole.Work is { } units ? [.. units.Where(unit => own.Contains(unit.Repository))] : null,
            // What the inventory could not read names its repository as data (FACTSPLIT1a): another workspace's is that workspace's,
            // whatever the clause's words name; one no workspace registers stays in every room, as before.
            WorkUnread = [.. whole.WorkUnread.Where(unread => !others.Contains(unread.Repository))],
            WorkRecovery = [.. whole.WorkRecovery.Where(recovery => recovery.Repository is { } repository && own.Contains(repository))],
            NotReadYet = whole.NotReadYet with { Repositories = [.. whole.NotReadYet.Repositories.Where(own.Contains)] },
        };
    }

    /// <summary>The pauses of the ids that are the workspace's.</summary>
    private static IReadOnlyDictionary<string, WorkPause> Kept(IReadOnlyDictionary<string, WorkPause> paused, IEnumerable<string> ids)
    {
        var ours = ids.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return paused.Where(pause => ours.Contains(pause.Key)).ToDictionary(pause => pause.Key, pause => pause.Value, StringComparer.Ordinal);
    }
}

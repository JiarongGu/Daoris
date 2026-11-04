using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The machine as Ask Daoris's room says it: the judges the loop's facts keep for each of the room's topics, and the room's reading for
/// a start or a conversation going on. It holds nothing of its own: it reads through the context.
/// </summary>
internal sealed class HelpMachineDesk(DriverContext context)
{
    private readonly DriverLoop _loop = context.Loop;

    /// <summary>
    /// Keep Ask Daoris's facts on the loop's: this module's judges registered for each of the room's topics,
    /// and the room written again, off the judge's thread, as the facts it said were not read yet land.
    /// </summary>
    public static void KeepFacts(DriverContext context)
    {
        var help = new HelpMachineDesk(context);
        var facts = help._loop.Facts;
        HelpFacts.Keep(facts, new HelpFactJudges(help.MachineWholeAsync, help.SessionsAsync, help.HistoryAsync, help.WorkAsync)
        {
            Trees = help.TreesAsync,
            Sweep = help.SweepPartAsync,
            Exchanges = ct => HelpWaitsFact.JudgeAsync(help._loop.Service, help._loop.Home, ct),
        });
        var catchUp = new HelpRoomCatchUp(help._loop.Home, () =>
        {
            var config = DriverConfig.Load(help._loop.ConfigPath);
            return HelpFacts.Read(facts.Current, config) is { } read ? help.HelpMachineOf(config, read) : null;
        }, help._loop.Log);
        facts.Changed += _ => catchUp.Landed();
    }

    /// <summary>
    /// The room's reading for a start or a conversation going on: since PARALLEL1a the facts the judges keep, as last
    /// judged, at once, and this machine's file as it is now. Where the service's answers were never judged it waits for their first
    /// judgement, which the start bounds and does not wait for at all where a room was written before.
    /// </summary>
    public static async Task<HelpMachine?> HelpMachineNowAsync(DriverContext context, CancellationToken cancellationToken)
    {
        var help = new HelpMachineDesk(context);
        var config = DriverConfig.Load(help._loop.ConfigPath);
        return await HelpFacts.ReadAsync(help._loop.Facts, config, cancellationToken).ConfigureAwait(false) is { } read
            ? help.HelpMachineOf(config, read)
            : null;
    }

    /// <summary>
    /// The machine as Ask Daoris's room says it, from the facts the judges keep (<see cref="HelpFacts"/>) and the
    /// driver's own files: its file, the registry and each repository's line, the roster, the asks, the plugins and offers, the
    /// landings, what the loop parked and held, and the browser's files. What every open writes the room from, and since ASKHIST1 a
    /// conversation going on too. Reads no git and asks the service nothing: what the facts have not judged yet is said so.
    /// </summary>
    private HelpMachine HelpMachineOf(DriverConfig config, HelpFactsRead read)
    {
        var whole = read.Whole;
        // The asks by id too, so a delete of one made by mistake can name it; counted by the rule a workspace's room counts
        // its own by.
        var asks = HelpRoom.AsksWaiting(whole.Standing);
        // This machine's sessions as the Sessions list places them, with what holds them as the loop's last look says.
        var sessions = read.Sessions is SessionLook look ? look with { Waits = _loop.Look.Waits } : null;
        // And the branches landings made here, so a hand-off names one the record holds.
        var plugins = PluginCatalog.Load(_loop.Home, AdapterSet.Built().Names);
        return HelpRoom.Describe(
            config, whole.Snapshot, read.Lines, whole.Roster, adapter => _loop.Harnesses.Toolchain(adapter)?.Product, asks, whole.Standing,
            plugins, new LandedBranches(_loop.Home).All(),
            // The install's own plugins, which the helper may propose installing by id.
            PluginOffers.Load(context.OffersFolder, _loop.Home, AdapterSet.Built().Names),
            // What the last tick parked by its strikes, so a retry names a quest the drawer offers Retry on.
            _loop.Parked.Latest,
            // Daoris's browser as its files hold it, so a favorite removed is one kept.
            BrowserModule.HelpFacts(BrowserModule.Home),
            // And what its last look held by the person's stop, so a retry names a stop Try again releases.
            HeldQuest.From(_loop.Look.Latest),
            sessions, read.History,
            // The named workflows saved here, so each repository's Workflow column names the version it follows.
            WorkflowStore.List(_loop.Home),
            // What the person approved of the servers plugins offer it.
            HelpGrants.Read(_loop.Home, plugins), read.Work?.Units, read.Work?.Unread, read.Work?.Recovery,
            read.NotReadYet,
            // What each line waits on.
            waits: read.Waits);
    }

    /// <summary>
    /// The machine's topic's judge: the registry, the quests and active sessions, the asks and the roster, as the service
    /// and the probes answer now; null before the service answers.
    /// </summary>
    private async Task<HelpMachineWhole?> MachineWholeAsync(CancellationToken cancellationToken)
    {
        if (_loop.Service is not { } service) return null;
        var config = DriverConfig.Load(_loop.ConfigPath);
        var snapshot = await service.SnapshotAsync(cancellationToken).ConfigureAwait(false);
        var roster = await _loop.Harnesses.RosterAsync(config, ct: cancellationToken).ConfigureAwait(false);
        var standing = await service.AsksAsync(cancellationToken).ConfigureAwait(false);
        return new HelpMachineWhole(snapshot, standing, roster);
    }

    /// <summary>
    /// The sessions' topic's judge: this machine's sessions as the Sessions list places them, so an archive
    /// or a delete names one it was shown, without their trees since FACTSPLIT1b, which each repository's part judges
    /// (<see cref="TreesAsync"/>): the records, the quests, the verdicts and the marks, with no git. The look this shell took last is
    /// read where it is recent. Null where the look cannot be taken, and the room says so.
    /// </summary>
    private async Task<IKnowledgeSessionLook?> SessionsAsync(CancellationToken cancellationToken)
    {
        if (_loop.Service is not { } service) return null;
        var config = DriverConfig.Load(_loop.ConfigPath);
        var looking = System.Diagnostics.Stopwatch.StartNew();
        var at = DateTimeOffset.UtcNow;
        var kept = _loop.SessionsLooked.Within(HelpRoomLookLog.Recent, at);
        var sessions = kept;
        var times = new LookTimes();
        if (sessions is null)
        {
            try
            {
                // No session's tree judged here (`only` none, as a landing's look whose trees are judged after): the parts judge them.
                sessions = await SessionGroups.LookAsync(
                    service, config, context.Door(config), _loop.Look.Latest, _loop.Home, [], cancellationToken, times: times).ConfigureAwait(false);
                _loop.SessionsLooked.Keep(sessions, at);
            }
            catch (DriverException)
            {
                // Left null: the room says `not read yet`, and a go still names a session the person names.
            }
        }

        HelpRoomLookLog.Said(_loop.Log, looking.Elapsed, sessions is not null, kept: kept is not null, times: times);
        return sessions;
    }

    /// <summary>
    /// The trees' topic's judge, one part at a time: what each of its repository's ended sessions' trees
    /// holds, judged with git in those trees alone (<see cref="HelpFacts.ReadTreesAsync"/>). A part that asked any tree says its walk's
    /// time and how many it asked in the room's look line, as the whole look's trees step said them; one with none ran no git.
    /// </summary>
    private async Task<HelpTrees?> TreesAsync(HelpTreesPart part, CancellationToken cancellationToken)
    {
        var looking = System.Diagnostics.Stopwatch.StartNew();
        var times = new LookTimes();
        var trees = await HelpFacts.ReadTreesAsync(_loop.Home, part, cancellationToken, times).ConfigureAwait(false);
        if (times.Trees > 0) HelpRoomLookLog.Said(_loop.Log, looking.Elapsed, read: true, times: times);
        return trees;
    }

    /// <summary>
    /// The history's topic's judge: what each workspace keeps of finished work, the workspaces
    /// `daoris history` reads; null where the service could not be read, and the room says so.
    /// </summary>
    private async Task<IReadOnlyList<HistoryReading>?> HistoryAsync(CancellationToken cancellationToken)
    {
        if (_loop.Service is not { } service) return null;
        try
        {
            var records = await service.SessionRecordsAsync(cancellationToken).ConfigureAwait(false);
            var quests = await service.EveryQuestAsync(cancellationToken).ConfigureAwait(false);
            var everyAsk = await service.EveryAskAsync(cancellationToken).ConfigureAwait(false);
            var world = HistoryRoutes.WorldOf(_loop);
            var readings = new List<HistoryReading>();
            foreach (var workspace in records.Select(record => record.Workspace)
                         .Concat(quests.Select(quest => quest.Workspace))
                         .Concat(everyAsk.Select(each => each.Workspace))
                         .Select(RemoteTarget.Workspace)
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .Order(StringComparer.OrdinalIgnoreCase))
            {
                var plan = await HistoryClearing.PlanAsync(world, HistoryScope.Workspace, workspace, cancellationToken).ConfigureAwait(false);
                if (plan.Reading is { } reading) readings.Add(reading);
            }

            return readings;
        }
        catch (DriverException)
        {
            return null;
        }
    }

    /// <summary>
    /// The work's topic's judge, one part at a time since FACTSPLIT1a: where each unit's work has got to
    /// (FABRIC-MILESTONE1b), the inventory's read-only derivation as `work state` and the WORK_STATE route read it, with Daoris's own
    /// recoveries no unit names, of the part's repositories alone. Its checkout and the trees in use are the
    /// service's answers the facts hold (<see cref="HelpFacts.PartOf"/>), so a part asks the service nothing but, where a landing the line
    /// does not hold names a quest, which of those are closed (<see cref="ClosedQuestsAsync"/>).
    /// </summary>
    private async Task<HelpWork?> WorkAsync(HelpWorkPart part, CancellationToken cancellationToken) =>
        await HelpFacts.ReadWorkAsync(
            _loop.Home, part with { ClosedQuests = await ClosedQuestsAsync(entry => part.Holds(entry.Repository), cancellationToken).ConfigureAwait(false) },
            cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// The quests the service says are closed among those the part's landings name: the one service read a part's
    /// judge makes, and only where a landing the line does not hold names a quest (<see cref="LandedStanding.ClosedAsync"/>), so a machine
    /// with none asks the service nothing. A service not ready closes nothing.
    /// </summary>
    private async Task<IReadOnlySet<string>> ClosedQuestsAsync(Func<LandedBranch, bool> ours, CancellationToken cancellationToken)
    {
        var service = _loop.Service;
        return service is null
            ? new HashSet<string>()
            : await LandedStanding.ClosedAsync(new LandedBranches(_loop.Home).Entries().Where(ours), service.EveryQuestAsync, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The clean-up's list's topic's judge, one repository at a time: its session branches and its landings' branches
    /// judged with git by the pull-request answers the landing record keeps, asking no plugin, as the pushed <c>sweep</c>
    /// slice reads them. Its checkout, workspace and trees in use are the service's answers the facts hold (<see cref="HelpFacts.SweepPartOf"/>).
    /// </summary>
    private async Task<IKnowledgeSweep?> SweepPartAsync(HelpSweepPart part, CancellationToken cancellationToken) =>
        await HelpFacts.ReadSweepAsync(
            _loop.Home,
            part with
            {
                ClosedQuests = await ClosedQuestsAsync(
                    entry => string.Equals(entry.Repository, part.Repository, StringComparison.OrdinalIgnoreCase), cancellationToken).ConfigureAwait(false),
            },
            cancellationToken).ConfigureAwait(false);
}

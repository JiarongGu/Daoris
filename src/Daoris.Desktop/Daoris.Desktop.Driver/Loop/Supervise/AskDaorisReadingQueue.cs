using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// The second check's reading queue, off the driver loop's awaited path: the supervisors' look offers it the queued ranges and goes on at
/// once. One reading runs on the machine at a time, oldest range first, each through the one walk (<see cref="SupervisePass"/>) on the
/// machine's supervisor deciding the kind, handed the range's writer and held packet-only. A range no choice can take now stays queued
/// with the walk's sentence, and the ranges behind it wait with it; a range queued a day closes unread, and with the check off every
/// queued range closes unread.
/// </summary>
internal sealed class AskDaorisReadingQueue(LoopContext context, Driver driver)
{
    public const string TurnedOff = "Second check is turned off";

    private static readonly ConcurrentDictionary<string, byte> Reading = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The readings begun on a task of their own, or null where one runs on this machine already.</summary>
    public Task? Offer(CancellationToken ct)
    {
        var home = Path.GetFullPath(context.Home);
        if (!Reading.TryAdd(home, 0)) return null;
        return Task.Run(
            async () =>
            {
                try
                {
                    await DrainAsync(ct).ConfigureAwait(false);
                }
                catch (Exception error) when (error is OperationCanceledException or DriverException or IOException or UnauthorizedAccessException
                                                  or HttpRequestException or System.Text.Json.JsonException)
                {
                    context.Log?.Warn("ask-daoris.reading-failed", ("why", error.GetType().Name));
                }
                finally
                {
                    Reading.TryRemove(home, out _);
                }
            }, CancellationToken.None);
    }

    private async Task DrainAsync(CancellationToken ct)
    {
        var reports = new AskDaorisReports(context.Home);
        new AskDaorisRanges(reports, () => DateTimeOffset.UtcNow).CloseStale();
        var queued = reports.InState(AskDaorisStates.Queued).OrderBy(report => report.Opened).ThenBy(report => report.Range, StringComparer.Ordinal).ToList();
        if (queued.Count == 0) return;
        var catalogue = RoleCatalog.ReadHome(context.Home);
        if (!SecondCheck.On(catalogue.Slots))
        {
            foreach (var report in queued) AskDaorisReadingLife.Unread(reports, report.Session, report.Range, TurnedOff, context.Log);
            return;
        }

        if (SupervisorResolution.Find(catalogue.Slots, SupervisorKinds.AskDaoris, new RolePlace()).Role is not { Hold.Holder: RoleHolders.Agent } role) return;
        PacketRedaction? redaction = null;
        foreach (var report in queued)
        {
            ct.ThrowIfCancellationRequested();
            if (!SupervisorGrants.Covers(role, SupervisorKinds.AskDaoris, SupervisorKinds.Report))
            {
                AskDaorisReadingLife.Unread(reports, report.Session, report.Range, $"its reader is not granted `{SupervisorKinds.AskDaoris}.{SupervisorKinds.Report}`", context.Log);
                continue;
            }

            var record = await RecordAsync(report.Session, ct).ConfigureAwait(false);
            var waiting = new SupervisorWaiting(
                SupervisorKinds.AskDaoris, new SupervisorSubject(SupervisorKinds.AskDaoris, report.Session, report.Range), "",
                AgentHold.Generation(role.Id, catalogue.RevisionOf(role.Id)), [SupervisorKinds.Report])
            {
                PacketOnly = true,
                Writers = record?.Adapter is { Length: > 0 } writer ? [writer] : [],
            };
            redaction ??= await PacketRedactions.OfAsync(context.Home, context.Service, ct).ConfigureAwait(false);
            var life = new AskDaorisReadingLife(reports, (taken, fence, _) => Task.FromResult(Packet(taken, record, fence, redaction)), Holds, context.Log);
            if (!await ReadAsync(reports, waiting, life, ct).ConfigureAwait(false)) return;
        }
    }

    // One range through the one walk until it closes, a limited end taking the next choice; false where it stays queued.
    private async Task<bool> ReadAsync(AskDaorisReports reports, SupervisorWaiting waiting, AskDaorisReadingLife life, CancellationToken ct)
    {
        var pass = new SupervisePass(context, driver);
        while (true)
        {
            var (ran, said) = await pass.RunToEndAsync(waiting, life, ct).ConfigureAwait(false);
            var state = reports.Find(waiting.Subject.Session, waiting.Subject.Id)?.State;
            if (state == AskDaorisStates.Queued) return false;
            if (state != AskDaorisStates.Running) return true;
            if (!ran || context.Supervisor.Attempts.Ended(waiting.Subject))
            {
                life.Close(waiting, said ?? "its turn ended with no answer kept");
                return true;
            }
        }
    }

    private AskDaorisPacketText Packet(AskDaorisReport taken, PriorSession? record, string fence, PacketRedaction redaction)
    {
        var access = record?.Access is { Count: > 0 } given ? "; access " + string.Join(", ", given.Select(each => $"{each.Key} {each.Value}")) : "";
        var mode = record?.HelpMode is { } recorded ? recorded + access : null;
        return AskDaorisPacket.Compose(new PacketRange(taken, mode), context.Events.After(taken.Session, 0).Events, fence, redaction);
    }

    private async Task<PriorSession?> RecordAsync(string session, CancellationToken ct)
    {
        try
        {
            return await context.Service.RecordAsync(session, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or IOException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private bool Holds(SupervisorWaiting waiting)
    {
        try
        {
            var catalogue = RoleCatalog.ReadHome(context.Home);
            return SupervisorResolution.Find(catalogue.Slots, SupervisorKinds.AskDaoris, new RolePlace()).Role is { } role
                && AgentHold.Generation(role.Id, catalogue.RevisionOf(role.Id)) == waiting.Generation;
        }
        catch (DriverException)
        {
            return false;
        }
    }
}

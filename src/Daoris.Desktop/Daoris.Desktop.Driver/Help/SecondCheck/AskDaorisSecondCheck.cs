using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The second check's selection over one conversation: its turns read from its events log, the connector's call lines in its
/// <c>lineage.jsonl</c>, its presses and its answered holds, each selected turn written into its ranges, whether the check is on read from
/// the role catalogue at each selection. At a start, the conversations it saw before are caught up; ranges queued a day close unread.
/// </summary>
public sealed class AskDaorisSecondCheck(
    string home, SessionEvents events, Func<string, IReadOnlyList<TurnPress>> presses, Func<string, IReadOnlySet<string>> held, Func<DateTimeOffset> clock)
{
    private readonly DateTimeOffset _running = clock();

    private AskDaorisReports Reports => new(home);

    private AskDaorisRanges Ranges => new(Reports, clock);

    /// <summary>A turn of <paramref name="session"/> ended: what ended past its seen mark is selected.</summary>
    public Task EndedAsync(string session, CancellationToken ct)
    {
        Ranges.Ended(session, Turns(session), On(), _running);
        Ranges.CloseStale();
        return Task.CompletedTask;
    }

    /// <summary>At a start, in the background: every conversation with a seen mark, from that mark.</summary>
    public Task CatchUpAsync(IEnumerable<string> sessions, CancellationToken ct)
    {
        var on = On();
        foreach (var session in sessions.Where(each => Reports.Seen(each) is not null))
        {
            ct.ThrowIfCancellationRequested();
            Ranges.CatchUp(session, Turns(session), on);
        }

        Ranges.CloseStale();
        return Task.CompletedTask;
    }

    private IReadOnlyList<ReadTurn> Turns(string session) => AskDaorisSelection.Turns(
        new TurnEvidence(events.After(session, 0).Events, Lines(ChildLineage.PathOf(home, session)), presses(session)) { Held = held(session) });

    private bool On()
    {
        try
        {
            return SecondCheck.On(RoleCatalog.ReadHome(home).Slots);
        }
        catch (DriverException)
        {
            return false;
        }
    }

    /// <summary>The connector's call lines of a lineage file, in order; a line that does not read is passed over.</summary>
    internal static IReadOnlyList<ConnectorCall> Lines(string path)
    {
        if (!File.Exists(path)) return [];
        var calls = new List<ConnectorCall>();
        string[] lines;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            lines = reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        }
        catch (IOException)
        {
            return [];
        }

        foreach (var line in lines)
        {
            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (Json.Text(root, "by") != "call" || !DateTimeOffset.TryParse(Json.Text(root, "at"), System.Globalization.CultureInfo.InvariantCulture, out var at)) continue;
                calls.Add(new ConnectorCall(at, Json.Text(root, "tool"), Json.Text(root, "act"), Json.Text(root, "toolUse")));
            }
            catch (JsonException)
            {
                // A torn or foreign line is no call.
            }
        }

        return calls;
    }
}

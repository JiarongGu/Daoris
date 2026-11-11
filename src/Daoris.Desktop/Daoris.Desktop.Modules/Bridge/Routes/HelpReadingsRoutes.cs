using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>A conversation's readings asked a page at a time: before the range the last page ended at, at most <c>limit</c> (none, a page's 20); one range with its packet.</summary>
public sealed record HelpReadingsRead(string Session, string? Before = null, int Limit = 0, string? Range = null);

/// <summary>The person's <i>Seen</i> on one flag: its conversation, range and place among the range's findings.</summary>
public sealed record HelpReadingSeen(string Session, string Range, int Finding);

/// <summary>
/// The second check as the window shows it, the page's `bridge/helpReadings.ts` (`docs/2026-11-11-ask-daoris-second-check-design.md` §7):
/// a conversation's readings newest first, each state derived from its report (<see cref="AskDaorisReadings"/>), the check's standing for
/// its chip, and the person's <i>Seen</i> on a flag. Desktop-only: the reports and their packets are this machine's, transcript-class.
/// </summary>
public sealed class HelpReadingsRoutes(DriverContext context)
{
    private readonly DriverLoop _loop = context.Loop;

    // `{ session, before?, limit?, range? }` answers `{ session, check: { on, pending, today: { readings, tokens, unknown } }, readings,
    // next }`, 20 a page; a range named answers that one alone with its `packet` as sent. A read: it declares nothing.
    [DriverRoute("HELP_READINGS", PipeClass.Open)]
    [ByEvent("HELP_READINGS", Untold = "the second check's reports, which its reading queue writes in the conversation's decisions folder")]
    private async Task<object?> HelpReadingsAsync(HelpReadingsRead read, CancellationToken cancellationToken)
    {
        var home = _loop.Home;
        return await Task.Run(() => Answer(home, read, DateTimeOffset.UtcNow), cancellationToken).ConfigureAwait(false);
    }

    // The person's acknowledgment of one flag, kept on its report: never accepted, resolved or answered. The window's alone, so Ask
    // Daoris never marks its own flags seen.
    [DriverRoute("HELP_READING_SEEN", PipeClass.Window)]
    [Owner("HELP_READING_SEEN", Session = "session")]
    [Publishes("HELP_READING_SEEN", StateScopes.Help, Session = "session")]
    private object? HelpReadingSeen(HelpReadingSeen press) =>
        new { press.Session, press.Range, press.Finding, Seen = new AskDaorisReports(_loop.Home).See(press.Session, press.Range, press.Finding, DateTimeOffset.UtcNow) };

    /// <summary>The answer to one ask, from the home's reports and roles alone.</summary>
    public static object Answer(string home, HelpReadingsRead read, DateTimeOffset now)
    {
        var reports = new AskDaorisReports(home);
        var check = new { On = On(home), Pending = AskDaorisReadings.Pending(reports, read.Session), Today = Today(reports, now) };
        if (read.Range is { } range)
        {
            var one = reports.Find(read.Session, range);
            return new
            {
                read.Session, Check = check, Readings = one is null ? [] : new[] { AskDaorisReadings.Shown(one) }, Next = (string?)null,
                Packet = one is null ? null : reports.Packet(read.Session, range),
            };
        }

        var page = AskDaorisReadings.Page(reports, read.Session, read.Before, read.Limit > 0 ? read.Limit : null);
        return new { read.Session, Check = check, page.Readings, page.Next };
    }

    /// <summary>Today's readings on the machine, and the tokens their harnesses reported, those that reported none counted apart.</summary>
    private static object Today(AskDaorisReports reports, DateTimeOffset now)
    {
        var day = now.ToLocalTime().Date;
        var read = new[] { AskDaorisStates.Read, AskDaorisStates.Left, AskDaorisStates.Unread, AskDaorisStates.Running }
            .SelectMany(reports.InState)
            .Where(report => report.Reader is not null && (report.Closed ?? now).ToLocalTime().Date == day)
            .ToList();
        return new
        {
            Readings = read.Count,
            Tokens = read.Sum(report => report.Reader!.Tokens ?? 0),
            Unknown = read.Count(report => report.Reader!.Tokens is null),
        };
    }

    private static bool On(string home)
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
}

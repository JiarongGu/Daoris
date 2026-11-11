using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The second check hooked to the window's loop: its conversations' turn ends watched (<see cref="AskDaorisWatch"/>), and Ask Daoris's
/// presses and its calls held for the person and answered kept for the selection (<see cref="AskDaorisKept"/>), each written on a task of
/// its own so no press or answer waits on the disk. A hold of a session that is no Ask Daoris conversation is kept nowhere.
/// </summary>
public sealed class SecondCheckHookUp : IDisposable
{
    private readonly AskDaorisWatch _watch;
    private readonly HelpPressRecord? _presses;
    private readonly HeldPermissions _held;
    private readonly AskDaorisKept _kept;
    private readonly string _home;

    private readonly Action<string>? _told;

    private SecondCheckHookUp(string home, SessionEvents events, HelpPressRecord? presses, HeldPermissions held, MachineLog? log, Action<string>? told)
    {
        _home = home;
        _presses = presses;
        _held = held;
        _told = told;
        _kept = new AskDaorisKept(home);
        if (presses is not null) presses.Moved += Pressed;
        held.Ended += Answered;
        AskDaorisReports.Changed += Reported;
        _watch = AskDaorisWatch.Of(
            home, events,
            session => _kept.Presses(session, presses?.Now().Where(press => press.Session == session).Select(KeptOf)),
            _kept.Answered, () => DateTimeOffset.UtcNow,
            (session, error) => log?.Warn("ask-daoris.select-failed", ("session", session), ("error", error.GetType().Name)));
    }

    /// <summary>Begin watching, the start's catch-up at once in the background; <paramref name="told"/> hears each conversation a report is written for.</summary>
    public static SecondCheckHookUp Start(
        string home, SessionEvents events, HelpPressRecord? presses, HeldPermissions held, MachineLog? log = null, Action<string>? told = null) =>
        new(home, events, presses, held, log, told);

    private void Reported(string home, string session)
    {
        if (string.Equals(home, _home, StringComparison.Ordinal)) _told?.Invoke(session);
    }

    /// <summary>Complete once the start's catch-up has run.</summary>
    public Task CaughtUp => _watch.CaughtUp;

    /// <summary>A press as the selection keeps it, its place in the terminal's spelling.</summary>
    public static KeptPress KeptOf(HelpPressNow press) =>
        new(press.Id, press.Session, press.At, press.Route, PlaceOf(press.Place), press.Outcome, press.Asked);

    private static string? PlaceOf(IReadOnlyDictionary<string, string>? place)
    {
        if (place is null || !place.TryGetValue("kind", out var kind)) return null;
        place.TryGetValue("workspace", out var workspace);
        place.TryGetValue("repository", out var repository);
        return kind switch
        {
            HelpFocus.MachineKind => HelpFocus.Machine.Said,
            HelpFocus.WorkspaceKind when workspace is not null => HelpFocus.OfWorkspace(workspace).Said,
            HelpFocus.RepositoryKind when workspace is not null && repository is not null => HelpFocus.OfRepository(workspace, repository).Said,
            _ => null,
        };
    }

    private void Pressed(HelpPressNow press)
    {
        var kept = KeptOf(press);
        _ = Task.Run(() => _kept.Press(kept));
    }

    private void Answered(HeldPermission hold, PermissionEnd end)
    {
        if (end.Why != PermissionWhy.Answered || hold.Call is not { Length: > 0 } call) return;
        _ = Task.Run(() =>
        {
            if (AskDaorisWatch.Conversation(_home, hold.Session)) _kept.Answered(hold.Session, call);
        });
    }

    public void Dispose()
    {
        if (_presses is not null) _presses.Moved -= Pressed;
        _held.Ended -= Answered;
        AskDaorisReports.Changed -= Reported;
        _watch.Dispose();
    }
}

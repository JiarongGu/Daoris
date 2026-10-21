using System.Text.Json.Nodes;

namespace Daoris.Desktop;

/// <summary>
/// Which workspace each main window holds, by its name, and which main window a notification's click opens (NOTIFY2c, D196 point 6,
/// the notification design §2.4 and §9): the one whose workspace holds what the notification is about, or the main window last in
/// front where none or several do. Kept here, in plain <c>net10.0</c>, so that is tested; the application brings the window forward.
/// </summary>
/// <remarks>
/// <para><b>Told as data.</b> Each main window's page tells its workspace beside its title (<c>SET_TITLE</c>'s <c>workspace</c>), none
/// for every workspace; it is never read from the title, which is the person's words for it.</para>
///
/// <para><b>What a window holds.</b> A window on one workspace holds that workspace's records, and one on every workspace holds every
/// record, as each window's lists show them (MAINWINDOWS3). A workspace is compared as the service names it (its
/// <c>Workspaces.Normalize</c>, the page's <c>circle</c>): trimmed, none the default's, its ASCII letters folded. What is the machine's (a
/// terminal's press, an account's wait) names no workspace and is every window's.</para>
///
/// <para><b>Several holding it</b> (a window on every workspace beside one on its own, or two on one): the one of them last in front,
/// since that is where the person last looked at it. <b>None holding it</b>: the main window last in front, as a press's balloon opens
/// (§3.3).</para>
///
/// <para><b>Never a window the person has not open.</b> A window closed is forgotten, and the first main window put away while another
/// is open (MAINWINDOWS2) is never picked while it is away; with nothing else known, the first is the answer, which the click takes
/// back as it does today. A utility window (the monitor, a detached session) holds nothing here.</para>
///
/// <para>Told from any thread: each window's page over the bridge, each window's focus on its own; one lock holds the state.</para>
/// </remarks>
/// <param name="mains">Whether the first main window is put away. Null: it never is.</param>
public sealed class MainWindowScopes(MainWindows? mains = null)
{
    private readonly object _gate = new();

    /// <summary>Each main window's workspace as the service names it, or null for every workspace.</summary>
    private readonly Dictionary<string, string?> _held = new(StringComparer.Ordinal);

    /// <summary>The main windows by when each last came in front of the person, the latest first.</summary>
    private readonly List<string> _fronts = [];

    /// <summary>The page of the main window named <paramref name="window"/> holds <paramref name="workspace"/>, or every workspace where null.</summary>
    public void Told(string window, string? workspace)
    {
        if (!MainWindows.IsMain(window)) return;
        lock (_gate) _held[window] = workspace is null ? null : Circle(workspace);
    }

    /// <summary>The main window named <paramref name="window"/> came in front of the person.</summary>
    public void CameForward(string window)
    {
        if (!MainWindows.IsMain(window)) return;
        lock (_gate)
        {
            _fronts.Remove(window);
            _fronts.Insert(0, window);
        }
    }

    /// <summary>The window named <paramref name="window"/> closed: it holds nothing and is never picked.</summary>
    public void Closed(string window)
    {
        lock (_gate)
        {
            _held.Remove(window);
            _fronts.Remove(window);
        }
    }

    /// <summary>
    /// The main window a notification about a record of <paramref name="workspace"/> opens: the one window whose workspace holds it,
    /// or the main window last in front where none or several do. Null is the machine's, which every window holds.
    /// </summary>
    public string Pick(string? workspace)
    {
        var away = mains?.FirstAway == true;
        lock (_gate)
        {
            bool Open(string window) => !(away && window == MainWindows.First);

            if (workspace is not null)
            {
                var circle = Circle(workspace);
                var holders = _held.Where(each => Open(each.Key) && (each.Value is null || each.Value == circle)).Select(each => each.Key).ToList();
                if (holders.Count == 1) return holders[0];
                if (holders.Count > 1) return LastInFront(holders);
            }

            return LastInFront([.. _held.Keys.Concat(_fronts).Distinct(StringComparer.Ordinal).Where(Open)]);
        }
    }

    /// <summary>
    /// The workspace of what a notification is about, as the windows' records hold it: its session's partition in the sessions slice, or
    /// else its quest's in the quests slice. Null where it names neither or the records hold neither, which is every window's.
    /// </summary>
    public static string? WorkspaceOf(SliceSnapshot? sessions, SliceSnapshot? quests, string? session, string? quest) =>
        Holding(sessions, session) ?? Holding(quests, quest);

    /// <summary>The main window a notification about <paramref name="session"/> or <paramref name="quest"/> opens, from the records <paramref name="slices"/> hold.</summary>
    public string PickFor(StateSlices slices, string? session, string? quest) => Pick(WorkspaceOf(
        slices.Find(SessionsStateSlice.Name)?.Snapshot(), slices.Find(QuestsStateSlice.Name)?.Snapshot(), session, quest));

    /// <summary>The partition, a workspace, whose keyed records hold <paramref name="id"/>.</summary>
    private static string? Holding(SliceSnapshot? slice, string? id)
    {
        if (slice is null || string.IsNullOrWhiteSpace(id)) return null;
        return slice.Partitions.FirstOrDefault(each => each.Data is JsonObject keyed && keyed.ContainsKey(id))?.Partition;
    }

    /// <summary>Among <paramref name="windows"/>, the one last in front; the first where none came in front, else the first by name.</summary>
    private string LastInFront(IReadOnlyCollection<string> windows)
    {
        foreach (var front in _fronts)
        {
            if (windows.Contains(front)) return front;
        }

        if (windows.Count == 0 || windows.Contains(MainWindows.First)) return MainWindows.First;
        return windows.Order(StringComparer.Ordinal).First();
    }

    /// <summary>A workspace as the service names it (<c>Workspaces.Normalize</c>): trimmed, none the default's, its ASCII letters folded.</summary>
    internal static string Circle(string workspace)
    {
        var named = workspace.Trim();
        if (named.Length == 0) return "default";
        return string.Create(named.Length, named, (letters, source) =>
        {
            for (var at = 0; at < source.Length; at++) letters[at] = source[at] is >= 'A' and <= 'Z' ? (char)(source[at] + 32) : source[at];
        });
    }
}

namespace Daoris.Driver;

/// <summary>One place a topic hears: a folder, what it hears of it, and the stamps its judge reads afresh.</summary>
/// <param name="Folder">The folder watched.</param>
/// <param name="Subfolders">Whether its subfolders are watched too.</param>
/// <param name="Hears">A move's name under the folder, with <c>/</c> between parts, and its kind: whether it wakes the topic.</param>
/// <param name="Stamps">What the topic's judge reads: each file's full path and its stamp, in whatever order.</param>
public sealed record HomePart(string Folder, bool Subfolders, Func<string, ChangeKind, bool> Hears, Func<IEnumerable<KeyValuePair<string, string>>> Stamps)
{
    /// <summary>One file of a folder, heard by its final name alone: a writer's temporary and its neighbours are not it.</summary>
    public static HomePart File(string path)
    {
        var full = Path.GetFullPath(path);
        var name = Path.GetFileName(full);
        return new(
            Path.GetDirectoryName(full)!,
            Subfolders: false,
            (moved, _) => string.Equals(moved, name, StringComparison.OrdinalIgnoreCase),
            () => [KeyValuePair.Create(full, HomeWrites.Stamp(full))]);
    }

    /// <summary>
    /// The <c>*.json</c> files of one folder, by their names alone (FABRIC-EVENT5a): a file arriving or leaving moves it, a file written
    /// again under its name does not, so a proposal settled, or a card's units kept in it, is no new proposal. A claim, a temporary a
    /// writer renames away and a folder are not heard.
    /// </summary>
    public static HomePart Named(string folder)
    {
        var full = Path.GetFullPath(folder);
        return new(
            full,
            Subfolders: false,
            (moved, _) => !moved.Contains('/') && moved.EndsWith(".json", StringComparison.OrdinalIgnoreCase),
            () => Directory.Exists(full)
                ? Directory.EnumerateFiles(full, "*.json").Select(file => KeyValuePair.Create(file, "present"))
                : []);
    }

    /// <summary>
    /// Every <c>*.json</c> record under <paramref name="folder"/>, by its stamp (FABRIC-END7: the landing gate's files): a record
    /// arriving, leaving or written again moves it, so each writer in this process marks its own (<see cref="HomeWrites"/>).
    /// </summary>
    public static HomePart Records(string folder, bool subfolders)
    {
        var full = Path.GetFullPath(folder);
        var depth = subfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return new(
            full,
            subfolders,
            (moved, _) => (subfolders || !moved.Contains('/')) && moved.EndsWith(".json", StringComparison.OrdinalIgnoreCase),
            () => Directory.Exists(full)
                ? Directory.EnumerateFiles(full, "*.json", depth).Select(file => KeyValuePair.Create(file, HomeWrites.Stamp(file)))
                : []);
    }

    /// <summary>
    /// The files at <paramref name="names"/> under <paramref name="folder"/>, and what is made or removed on the way to them
    /// (<paramref name="ways"/>): a staged build's folder is heard by its manifest and its journal, never by the build's thousand files.
    /// </summary>
    public static HomePart Files(string folder, IReadOnlyList<string> names, IReadOnlyList<string> ways)
    {
        var full = Path.GetFullPath(folder);
        return new(
            full,
            Subfolders: true,
            (moved, _) => names.Contains(moved, StringComparer.OrdinalIgnoreCase) || ways.Contains(moved, StringComparer.OrdinalIgnoreCase),
            () => names.Select(name => KeyValuePair.Create(Path.Combine(full, name), HomeWrites.Stamp(Path.Combine(full, name)))));
    }

    /// <summary>
    /// The plugins folder: a plugin arriving or leaving (a folder made, renamed or removed under it) and its manifest changing, never the
    /// <see cref="PluginCatalog.DataFolder"/> its plugins keep their own state in, nor any folder named with a dot.
    /// </summary>
    public static HomePart Plugins(string folder)
    {
        var full = Path.GetFullPath(folder);
        return new(
            full,
            Subfolders: true,
            (moved, _) =>
            {
                var parts = moved.Split('/');
                return !parts[0].StartsWith('.')
                    && (parts.Length == 1 || (parts.Length == 2 && string.Equals(parts[1], PluginCatalog.ManifestName, StringComparison.OrdinalIgnoreCase)));
            },
            () => Directory.Exists(full)
                ? Directory.EnumerateDirectories(full)
                    .Where(dir => !Path.GetFileName(dir).StartsWith('.'))
                    .Select(dir => KeyValuePair.Create(dir, HomeWrites.Stamp(Path.Combine(dir, PluginCatalog.ManifestName))))
                : []);
    }
}

/// <summary>
/// The home's record files on the change watch (FABRIC-EVENT4, the design §A4.1 and link 7 of §C6): each topic is one thing the
/// loop used to find by looking every <c>pollSeconds</c> or every two seconds, now heard when its file moves, with its read
/// kept as the topic's deadline. A wake says a topic and a name, never what the file holds: the host's <c>wake</c> asks the look
/// that reads it (<see cref="DriverWatch.Nudge(string, string)"/>), or the updater's own poke.
/// </summary>
/// <remarks>
/// <para><b>One judge, a stamp.</b> A wake, and each topic's deadline, reads the topic's files' stamps afresh and compares them with
/// the ones last read: a hint whose stamps did not move, and a deadline that finds nothing, wake nothing, so the reconciliation is
/// no look. A stamp that is the one this process marked (<see cref="HomeWrites"/>) is its own write and wakes nothing.</para>
///
/// <para>The topics are the chain's link 7 (<see cref="ChainRanks.Read"/>): each a <c>home</c> event, its deadline <c>timer(&lt;topic&gt;)</c>.
/// A missed hint, a watcher that overflowed or a folder absent when the watch opened costs latency, never correctness: the topic is
/// judged at its deadline whatever was heard (<see cref="ChangeWatch"/>).</para>
/// </remarks>
public sealed class HomeWatch : IAsyncDisposable
{
    /// <summary>The reconciliation behind every file: its topic is judged this long after its last judgement, whatever was heard.</summary>
    public static readonly TimeSpan Deadline = TimeSpan.FromSeconds(30);

    private readonly ChangeWatch _changes;
    private readonly Chain _chain;
    private readonly Action<string, string> _wake;
    private readonly TimeSpan _every;
    private readonly object _gate = new();
    private readonly Dictionary<string, Heard> _heard = new(StringComparer.Ordinal);

    /// <param name="wake">What a topic that moved asks: its name and the name of what moved, never a path.</param>
    /// <param name="every">Each topic's deadline; <see cref="Deadline"/> unless a test or a host says another.</param>
    /// <param name="source">What watches the folders: the machine's watchers unless a test hands its fake.</param>
    public HomeWatch(Action<string, string> wake, TimeSpan? every = null, IChangeSource? source = null)
    {
        _wake = wake;
        _every = every ?? Deadline;
        _changes = new ChangeWatch(source);
        _chain = new Chain(_changes).Bind(ChainRanks.Read, ReadAsync);
    }

    /// <summary>
    /// The driver's own: <c>driver.json</c> (<paramref name="configPath"/>, wherever the config's own resolution put it),
    /// the plugins folder, <c>lines-moved.json</c> and the held words, each its topic (<see cref="ChangeTopics"/>).
    /// </summary>
    public static HomeWatch ForDriver(
        string home, string configPath, Action<string, string> wake, TimeSpan? every = null, IChangeSource? source = null, MachineLog? log = null)
    {
        var watch = new HomeWatch(wake, every, source) { Log = log };
        watch.Topic(ChangeTopics.Config, Path.GetFileName(configPath), HomePart.File(configPath));
        watch.Topic(ChangeTopics.Plugins, ChangeTopics.Plugins, HomePart.Plugins(DaorisHome.Entry(home, HomeEntry.Plugins)));
        var lines = DaorisHome.Entry(home, HomeEntry.LinesMoved);
        watch.Topic(ChangeTopics.Lines, Path.GetFileName(lines), HomePart.File(lines));
        var words = HeldWordsFile.PathOf(home);
        watch.Topic(ChangeTopics.Words, Path.GetFileName(words), HomePart.File(words));
        // The landing gate's files (FABRIC-END7): what releases a second opinion's or a workflow's hold when another process writes it.
        watch.Topic(ChangeTopics.Gates, ChangeTopics.Gates,
            HomePart.Records(DaorisHome.Entry(home, HomeEntry.OpinionGates), subfolders: false), HomePart.Records(WorkflowStore.FolderOf(home), subfolders: true));
        return watch;
    }

    /// <summary>
    /// Ask Daoris's proposals folder alone (<see cref="ChangeTopics.Help"/>, FABRIC-EVENT5a): the window's own process watches it, whoever
    /// holds the home's lease, since the connector writes there whichever loop drives. Not started: the caller starts it, and tells its
    /// windows once at the start, since a proposal that arrived before the watch opened is in its first read.
    /// </summary>
    public static HomeWatch ForProposals(
        string home, Action<string, string> wake, TimeSpan? every = null, IChangeSource? source = null, MachineLog? log = null)
    {
        var watch = new HomeWatch(wake, every, source) { Log = log };
        watch.Topic(ChangeTopics.Help, "proposals", HomePart.Named(HelpProposals.FolderOf(home)));
        return watch;
    }

    /// <summary>Where each look a hint or a deadline woke is written (<c>change.judged</c>), and each loss; none by default.</summary>
    public MachineLog? Log
    {
        get => _changes.Log;
        init
        {
            _changes.Log = value;
            _chain.Log = value;
        }
    }

    /// <summary>The watch under the topics, which a test steps and settles.</summary>
    internal ChangeWatch Changes => _changes;

    /// <summary>
    /// A topic: <paramref name="subject"/> is the name its wake carries, and <paramref name="parts"/> the places it hears. What its files
    /// hold now is taken as read, so only what moves after is a wake. Before <see cref="Start"/>.
    /// </summary>
    public void Topic(string topic, string subject, params HomePart[] parts)
    {
        lock (_gate) _heard[topic] = new Heard(subject, parts, Read(parts));
        _chain.Topic(topic, ChainEvents.Home, _every);
        foreach (var part in parts) _changes.Watch(topic, topic, part.Folder, part.Hears, part.Subfolders);
    }

    /// <summary>Open every folder and judge whatever is due, until disposed.</summary>
    public void Start() => _changes.Start();

    /// <summary>The machine resumed from sleep: every folder is reopened and every topic judged now.</summary>
    public void Resumed() => _changes.Resumed();

    private static Dictionary<string, string> Read(IEnumerable<HomePart> parts)
    {
        var stamps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in parts)
        {
            foreach (var (key, stamp) in part.Stamps()) stamps[key] = stamp;
        }

        return stamps;
    }

    /// <summary>Link 7 on the home's files: each topic heard is read afresh, and emits nothing into the chain.</summary>
    private Task<ChainFired> ReadAsync(IReadOnlyList<ChainEvent> heard, CancellationToken ct)
    {
        var acted = 0;
        foreach (var topic in heard.Select(@event => @event.Topic ?? @event.Subject).Distinct(StringComparer.Ordinal)) acted += Judge(topic);
        return Task.FromResult(new ChainFired(acted, []));
    }

    /// <summary>Whether <paramref name="topic"/>'s files moved since they were last read, other than by this process; if so, its wake asked.</summary>
    private int Judge(string topic)
    {
        Heard known;
        lock (_gate)
        {
            if (!_heard.TryGetValue(topic, out known!)) return 0;
        }

        var now = Read(known.Parts);
        var moved = false;
        lock (_gate)
        {
            foreach (var (key, stamp) in now)
            {
                if (known.Last.TryGetValue(key, out var before) && before == stamp) continue;
                if (!HomeWrites.Is(key, stamp)) moved = true;
            }

            // A plugin's folder gone, or a file no longer listed, is a move that is nobody's mark.
            if (known.Last.Keys.Any(key => !now.ContainsKey(key))) moved = true;
            known.Last = now;
        }

        if (!moved) return 0;
        _wake(topic, known.Subject);
        return 1;
    }

    public ValueTask DisposeAsync() => _changes.DisposeAsync();

    private sealed class Heard(string subject, HomePart[] parts, Dictionary<string, string> last)
    {
        public string Subject { get; } = subject;

        public HomePart[] Parts { get; } = parts;

        public Dictionary<string, string> Last { get; set; } = last;
    }
}

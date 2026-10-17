using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// When each repository's line was last fetched here, or heard fetched (FABRIC-END6, the design §A4.3): kept in this process for the
/// minute that bounds the next fetch (D148), so a moment fetches only where nothing brought the remote's facts within it. Every fetch
/// of a line, made or failed, is counted by <see cref="TreeRefs.FetchAsync"/>, the landing press's and bringing up to date's among them;
/// the person's own fetch, heard by the ref watch moving origin's line, is counted by the moments.
/// </summary>
public static class RemoteFetches
{
    private static readonly ConcurrentDictionary<string, DateTimeOffset> Last = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The line of the checkout at <paramref name="root"/> was fetched, or heard fetched, at <paramref name="at"/>. Never throws.</summary>
    public static void Fetched(string root, string line, DateTimeOffset at)
    {
        if (Key(root, line) is { } key) Last.AddOrUpdate(key, at, (_, was) => was > at ? was : at);
    }

    /// <summary>When the line was last fetched or heard fetched in this process, or null where it never was.</summary>
    public static DateTimeOffset? When(string root, string line) => Key(root, line) is { } key && Last.TryGetValue(key, out var at) ? at : null;

    /// <summary>The line's fetch, made once at a time per repository and line.</summary>
    internal static Task<(string? Failed, string? Url)> OnceAsync(
        string root, string line, Func<CancellationToken, Task<(string? Failed, string? Url)>> fetch, CancellationToken ct) => fetch(ct);

    /// <summary>When git's files say the line was last fetched, or null.</summary>
    public static DateTimeOffset? InGit(string commonDir, string line) => null;

    private static string? Key(string root, string line)
    {
        try
        {
            return $"{Path.TrimEndingDirectorySeparator(Path.GetFullPath(root))}|{line}";
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }
}

/// <summary>
/// What the moments read and do (FABRIC-END6): the driver's record, git's files, a fetch, the plugin's ask and the ref watch, behind one
/// seam so the fast tests count each without git or a plugin.
/// </summary>
internal interface IPullRequestMomentWorld
{
    /// <summary>The standing landings whose pull request is open and whose end is not held (<see cref="PullRequestMoments.Concerns"/>).</summary>
    IReadOnlyList<LandedBranch> Open();

    /// <summary>The repositories holding Daoris's work, at their main checkouts.</summary>
    Task<IReadOnlyList<RefRepository>> RepositoriesAsync(CancellationToken ct);

    /// <summary>The repository's line as the asks read it, or null where it has none.</summary>
    Task<string?> LineAsync(RefRepository repository, CancellationToken ct);

    /// <summary>The commit origin's line stands at here, read from git's files with no process; null where they cannot say.</summary>
    string? OriginTip(RefRepository repository, string line);

    /// <summary>When the line was last fetched or heard fetched (<see cref="RemoteFetches"/>).</summary>
    DateTimeOffset? Fetched(RefRepository repository, string line);

    /// <summary>Someone else's fetch moved origin's line, heard at <paramref name="at"/>.</summary>
    void HeardFetched(RefRepository repository, string line, DateTimeOffset at);

    /// <summary>Fetch the line from origin, counted as every fetch is.</summary>
    Task FetchAsync(RefRepository repository, string line, CancellationToken ct);

    /// <summary>Ask the plugin about <paramref name="entries"/>, keeping what it answers; how many came back completed where they were not.</summary>
    Task<int> AskAsync(RefRepository repository, IReadOnlyList<LandedBranch> entries, CancellationToken ct);

    /// <summary>The repository's ends judged again now, as Daoris's own move is (<see cref="RefMoves"/>).</summary>
    void Judge(RefRepository repository);
}

/// <summary>
/// A plugin asked about an open pull request when the remote's facts may have moved (FABRIC-END6, the design §A4.3, link 6 of §C6): at
/// origin's line heard moving, right after Daoris pushes a handed branch, and at the window's focus while a pull request is open. The
/// line is fetched only where the moment did not bring it, so a completed answer's merge commit is here for git to confirm (D148 point
/// 4), and both stay within D148's minute.
/// </summary>
/// <remarks>
/// <para><b>The moments</b>, each a wake of link 6 on this chain of its own, never a value:</para>
/// <list type="bullet">
/// <item><b>Origin's line moved</b> (<c>refs</c>): the ref watch tells each repository it judges (<see cref="Moved"/>), and origin's line
/// is read from git's files and compared with the commit last seen here. The person's fetch brought the remote's facts, so nothing is
/// fetched. The first reading of a repository's line is its baseline, and asks nothing.</item>
/// <item><b>Daoris pushed</b> (<c>refs</c>, <see cref="Pushed"/>): after the landing's plugin step and its tidy, whose own ask (LAND3) it
/// counts, and after a hand-off. The landing press's fetch of the line counts as the push's.</item>
/// <item><b>The window's focus</b> (<c>focus</c>, <see cref="Focused"/>): every repository with an open pull request.</item>
/// </list>
///
/// <para><b>The bounds.</b> A pull request is asked at most once a minute: D148's minute over the record's last ask
/// (<see cref="PullRequestAsking.Due"/>), and over this process's own asks, so a plugin not ready is not started again inside it. A
/// repository's line is fetched at most once a minute, every fetch of it counted (<see cref="RemoteFetches"/>). A moment with nothing due
/// fetches nothing and asks nothing. A moment that concerns no open pull request reads no line and starts no process.</para>
///
/// <para><b>Never at a removing press.</b> A <c>press</c> is no moment here, and a removing press's own ref moves (a branch deleted) move
/// no line of origin's. The asks run one at a time, and only in the process holding the home's lease, which alone keeps ends.</para>
///
/// <para><b>An end with no press.</b> Where an answer comes back completed, the repository's ends are judged again at once, as Daoris's own
/// move is (<see cref="RefMoves.Mine"/>), so a merged pull request's work is held (§B) with nobody pressing anything.</para>
///
/// <para><b>What the person sees:</b> nothing new on the window. The machine log has link 6's <c>chain.fired</c> (topic <c>refs</c> or
/// <c>focus</c>, the count asked), the plugin's own lines, and a failure where a moment failed. The topics' deadlines wake no link: the
/// moments add no clock, and D148's occasions stay the reconciliation.</para>
/// </remarks>
public sealed class PullRequestMoments : IAsyncDisposable
{
    /// <summary>The topics' deadline, which wakes no link (link 6 hears no <c>timer</c>): the ref watch's, so this chain adds no clock.</summary>
    public static readonly TimeSpan Deadline = RefWatch.Deadline;

    /// <summary>The focus topic's one subject.</summary>
    public const string Window = "window";

    private static readonly object ListeningGate = new();

    private static readonly List<(string Home, PullRequestMoments Moments)> Listening = [];

    private readonly string _home;
    private readonly IPullRequestMomentWorld _world;
    private readonly Func<DateTimeOffset> _clock;
    private readonly ChangeWatch _changes;
    private readonly Chain _chain;
    private readonly SemaphoreSlim _one = new(1, 1);
    private readonly object _gate = new();
    private readonly Dictionary<string, RefRepository> _known = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _pushed = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string?> _seen = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _asked = new(StringComparer.Ordinal);
    private bool _listening;

    /// <param name="world">What the moments read and do.</param>
    /// <param name="source">What the chain's watch would watch folders with; it has no root, so none is opened.</param>
    /// <param name="clock">What time it is; the system's unless a test hands its own.</param>
    internal PullRequestMoments(string home, IPullRequestMomentWorld world, IChangeSource? source = null, Func<DateTimeOffset>? clock = null)
    {
        _home = Full(home) ?? home;
        _world = world;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _changes = new ChangeWatch(source, clock);
        _chain = new Chain(_changes, clock: clock).Bind(ChainRanks.Ask, AskAsync);
        _chain.Topic(ChangeTopics.Refs, ChainEvents.Refs, Deadline);
        _chain.Topic(ChangeTopics.Focus, ChainEvents.Focus, Deadline);
    }

    /// <summary>The driver's: the record, the registry's checkouts, git and the plugins of <paramref name="home"/>.</summary>
    /// <param name="trees">The trees asked through; the home's own unless a test hands one whose plugins are faked.</param>
    /// <param name="clock">What time it is for the minute's bounds; the system's unless a test hands its own.</param>
    public static PullRequestMoments ForDriver(
        string home, Func<CancellationToken, Task<IReadOnlyList<RepoView>>> registry, MachineLog? log = null, SessionTrees? trees = null,
        Func<DateTimeOffset>? clock = null) =>
        new(home, new DriverMoments(home, registry, trees ?? new SessionTrees(home)), clock: clock) { Log = log };

    /// <summary>
    /// Where link 6's <c>chain.fired</c> and a moment's failure are written; none by default. The watch under the chain writes nothing, so
    /// a focus or a ref move that concerns no pull request is no line.
    /// </summary>
    public MachineLog? Log
    {
        get => _chain.Log;
        init => _chain.Log = value;
    }

    /// <summary>The watch under the chain, which a test steps and settles.</summary>
    internal ChangeWatch Changes => _changes;

    /// <summary>The chain, which a test fires a press at.</summary>
    internal Chain Chain => _chain;

    /// <summary>
    /// Whether an entry's pull request is open and concerns a moment: a standing landing a plugin pushed, whose kept answer is neither
    /// completed nor abandoned, and whose end is not held.
    /// </summary>
    public static bool Concerns(LandedBranch entry) =>
        entry is { GoneAt: null, Held: null, Pushed: true }
        && entry.PullRequestState?.State is not (PullRequestStates.Completed or PullRequestStates.Abandoned);

    /// <summary>The ref watch judged <paramref name="repository"/> under <paramref name="home"/>: its refs moved. Never throws.</summary>
    public static void Moved(string home, RefRepository repository)
    {
        foreach (var moments in Of(home)) moments.Heard(repository);
    }

    /// <summary>Daoris pushed a handed branch of <paramref name="repository"/> under <paramref name="home"/>, and recorded it. Never throws.</summary>
    public static void Pushed(string home, string repository)
    {
        foreach (var moments in Of(home)) moments.HeardPushed(repository);
    }

    /// <summary>The window has the person's focus: every repository with an open pull request is a moment. Never blocks on the work.</summary>
    public void Focused() => _changes.Now(ChangeTopics.Focus, Window);

    /// <summary>Hear the ref watch and Daoris's pushes, and ask whenever a moment is due, until disposed.</summary>
    public void Start()
    {
        Listen();
        _changes.Start();
    }

    /// <summary>Hear the ref watch and Daoris's pushes, without stepping: a test steps by hand.</summary>
    internal void Open()
    {
        Listen();
        _changes.Open();
    }

    /// <summary>A repository's refs moved.</summary>
    internal void Heard(RefRepository repository)
    {
        lock (_gate) _known[repository.Repository] = repository;
        _changes.Now(ChangeTopics.Refs, repository.Repository);
    }

    /// <summary>Daoris pushed in a repository.</summary>
    internal void HeardPushed(string repository)
    {
        lock (_gate) _pushed.Add(repository);
        _changes.Now(ChangeTopics.Refs, repository);
    }

    /// <summary>
    /// Link 6: the moments heard, one at a time, so a moment's ask is on the record before the next one reads it. A press is no moment
    /// (D148 point 2: never at a press that removes); a press that pushes is told by its push. How many pull requests were asked.
    /// </summary>
    private async Task<ChainFired> AskAsync(IReadOnlyList<ChainEvent> heard, CancellationToken ct)
    {
        var focus = heard.Any(each => each.Type == ChainEvents.Focus);
        string[] moved = [.. heard.Where(each => each.Type == ChainEvents.Refs).Select(each => each.Subject).Distinct(StringComparer.OrdinalIgnoreCase)];
        if (!focus && moved.Length == 0) return ChainFired.Nothing;

        await _one.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var asked = 0;
            foreach (var name in moved) asked += await MomentAsync(() => RefsAsync(name, ct), ct).ConfigureAwait(false);
            if (focus) asked += await MomentAsync(() => FocusAsync(ct), ct).ConfigureAwait(false);
            return new ChainFired(asked, []);
        }
        finally
        {
            _one.Release();
        }
    }

    /// <summary>One moment, whose failure is a line and leaves the others running: the next moment asks again.</summary>
    private async Task<int> MomentAsync(Func<Task<int>> moment, CancellationToken ct)
    {
        try
        {
            return await moment().ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            Log?.Failed("a pull request's ask at its moment", error);
            return 0;
        }
    }

    /// <summary>
    /// A repository's refs moved, or Daoris pushed there: a moment where origin's line moved since it was last seen here (nothing fetched,
    /// since that fetch brought the remote's facts), or where Daoris pushed (fetched unless the line was within the minute).
    /// </summary>
    private async Task<int> RefsAsync(string name, CancellationToken ct)
    {
        bool pushed;
        lock (_gate) pushed = _pushed.Remove(name);
        var open = Concerning(name);
        if (open.Count == 0) return 0;
        if (await KnownAsync(name, ct).ConfigureAwait(false) is not { } repository) return 0;
        if (await _world.LineAsync(repository, ct).ConfigureAwait(false) is not { } line) return 0;

        var now = _clock();
        var moved = Seen(repository, line, _world.OriginTip(repository, line));
        // The person's fetch is counted as one, so a moment inside its minute fetches nothing more.
        if (moved) _world.HeardFetched(repository, line, now);
        if (!moved && !pushed) return 0;
        return await AskDueAsync(repository, line, open, fetch: !moved, now, ct).ConfigureAwait(false);
    }

    /// <summary>The window's focus: each repository with an open pull request, its line fetched where the minute allows, then asked.</summary>
    private async Task<int> FocusAsync(CancellationToken ct)
    {
        var open = Concerning(null);
        if (open.Count == 0) return 0;
        var names = open.Select(entry => entry.Repository).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var asked = 0;
        foreach (var repository in await _world.RepositoriesAsync(ct).ConfigureAwait(false))
        {
            if (!names.Remove(repository.Repository)) continue;
            lock (_gate) _known[repository.Repository] = repository;
            if (await _world.LineAsync(repository, ct).ConfigureAwait(false) is not { } line) continue;
            var mine = open.Where(entry => string.Equals(entry.Repository, repository.Repository, StringComparison.OrdinalIgnoreCase)).ToList();
            asked += await AskDueAsync(repository, line, mine, fetch: true, _clock(), ct).ConfigureAwait(false);
        }

        return asked;
    }

    /// <summary>
    /// The entries still due by D148's minute, asked: the line fetched first where <paramref name="fetch"/> and no fetch of it came within
    /// the minute, so a completed answer's merge commit is here for git to confirm; the repository judged again where one completed.
    /// Nothing due: nothing fetched and nothing asked.
    /// </summary>
    private async Task<int> AskDueAsync(
        RefRepository repository, string line, IReadOnlyList<LandedBranch> open, bool fetch, DateTimeOffset now, CancellationToken ct)
    {
        List<LandedBranch> due;
        lock (_gate) due = [.. open.Where(entry => PullRequestAsking.Due(entry, now) && !AskedHere(entry, now))];
        if (due.Count == 0) return 0;

        if (fetch && !(_world.Fetched(repository, line) is { } last && now - last < PullRequestAsking.Fresh))
        {
            await _world.FetchAsync(repository, line, ct).ConfigureAwait(false);
            // The fetch's own write to origin's line is no one else's fetch when the ref watch tells it.
            Seen(repository, line, _world.OriginTip(repository, line));
        }

        lock (_gate)
        {
            foreach (var entry in due) _asked[Key(entry)] = now;
        }

        if (await _world.AskAsync(repository, due, ct).ConfigureAwait(false) > 0) _world.Judge(repository);
        return due.Count;
    }

    /// <summary>The open pull requests a moment concerns: of one repository, or of every one where null.</summary>
    private List<LandedBranch> Concerning(string? repository) =>
        [.. _world.Open().Where(entry => repository is null || string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase))];

    /// <summary>The repository's checkout as the ref watch told it, else the registry's (a push in a repository not yet heard).</summary>
    private async Task<RefRepository?> KnownAsync(string name, CancellationToken ct)
    {
        lock (_gate)
        {
            if (_known.TryGetValue(name, out var known)) return known;
        }

        var found = (await _world.RepositoriesAsync(ct).ConfigureAwait(false))
            .FirstOrDefault(each => string.Equals(each.Repository, name, StringComparison.OrdinalIgnoreCase));
        if (found is not null)
        {
            lock (_gate) _known[name] = found;
        }

        return found;
    }

    /// <summary>
    /// Origin's line read now, kept as the one last seen: whether it moved since. The first reading is a baseline, never a move, so a
    /// start is no moment; a line git's files cannot read is no move.
    /// </summary>
    private bool Seen(RefRepository repository, string line, string? tip)
    {
        var key = $"{repository.Repository}|{line}";
        lock (_gate)
        {
            var moved = tip is not null && _seen.TryGetValue(key, out var was) && was is not null && !string.Equals(was, tip, StringComparison.Ordinal);
            if (tip is not null) _seen[key] = tip;
            return moved;
        }
    }

    /// <summary>Whether this process asked about the entry inside the minute, answered or not: a plugin not ready keeps nothing on the record.</summary>
    private bool AskedHere(LandedBranch entry, DateTimeOffset now) => _asked.TryGetValue(Key(entry), out var at) && now - at < PullRequestAsking.Fresh;

    private static string Key(LandedBranch entry) => $"{entry.Repository}|{entry.Branch}|{entry.Session}|{entry.LandedAt:O}";

    private void Listen()
    {
        lock (ListeningGate)
        {
            if (_listening) return;
            Listening.Add((_home, this));
            _listening = true;
        }
    }

    private static PullRequestMoments[] Of(string home)
    {
        if (Full(home) is not { } full) return [];
        lock (ListeningGate) return [.. Listening.Where(each => string.Equals(each.Home, full, StringComparison.OrdinalIgnoreCase)).Select(each => each.Moments)];
    }

    private static string? Full(string home)
    {
        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(home));
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (ListeningGate) Listening.RemoveAll(each => ReferenceEquals(each.Moments, this));
        await _changes.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>The driver's world: the lease's holder alone asks, as it alone keeps ends and lands.</summary>
    private sealed class DriverMoments(string home, Func<CancellationToken, Task<IReadOnlyList<RepoView>>> registry, SessionTrees trees)
        : IPullRequestMomentWorld
    {
        public IReadOnlyList<LandedBranch> Open() => HomeLease.HeldHere(home) ? [.. trees.Recorded.All().Where(Concerns)] : [];

        public Task<IReadOnlyList<RefRepository>> RepositoriesAsync(CancellationToken ct) => RefWatch.RepositoriesAsync(home, registry, ct);

        public Task<string?> LineAsync(RefRepository repository, CancellationToken ct) =>
            trees.LineOfAsync(repository.Root, repository.Repository, repository.Workspace, ct);

        public string? OriginTip(RefRepository repository, string line) => TreeTips.Named(repository.CommonDir, $"refs/remotes/origin/{line}");

        public DateTimeOffset? Fetched(RefRepository repository, string line) => RemoteFetches.When(repository.Root, line);

        public void HeardFetched(RefRepository repository, string line, DateTimeOffset at) => RemoteFetches.Fetched(repository.Root, line, at);

        public Task FetchAsync(RefRepository repository, string line, CancellationToken ct) => TreeRefs.FetchAsync(repository.Root, line, ct);

        public Task<int> AskAsync(RefRepository repository, IReadOnlyList<LandedBranch> entries, CancellationToken ct) =>
            trees.AskAtMomentAsync(repository.Root, repository.Repository, repository.Workspace, entries, ct);

        public void Judge(RefRepository repository) => RefMoves.Mine(home, repository.Repository);
    }
}

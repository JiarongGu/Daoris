using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// When each repository's line was last fetched here, or heard fetched (FABRIC-END6, the design §A4.3): kept in this process for the
/// minute that bounds the next fetch (D148), so a moment fetches only where nothing brought the remote's facts within it. Every fetch
/// of a line, made or failed, is counted by <see cref="TreeRefs.FetchAsync"/>, the landing press's and bringing up to date's among them;
/// the person's own fetch, heard by the ref watch moving origin's line, is counted by the moments, and git's own files say when it was last
/// fetched across restarts (<see cref="InGit"/>, FABRIC-END6c). A fetch in flight counts as made: every fetch of a line goes through
/// <see cref="OnceAsync"/>, so a second joins it (FABRIC-END6c).
/// </summary>
public static class RemoteFetches
{
    private static readonly ConcurrentDictionary<string, DateTimeOffset> Last = new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<string, Task<(string? Failed, string? Url)>> InFlight = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The line of the checkout at <paramref name="root"/> was fetched, or heard fetched, at <paramref name="at"/>. Never throws.</summary>
    public static void Fetched(string root, string line, DateTimeOffset at)
    {
        if (Key(root, line) is { } key) Last.AddOrUpdate(key, at, (_, was) => was > at ? was : at);
    }

    /// <summary>When the line was last fetched or heard fetched in this process, or null where it never was.</summary>
    public static DateTimeOffset? When(string root, string line) => Key(root, line) is { } key && Last.TryGetValue(key, out var at) ? at : null;

    /// <summary>
    /// The line's fetch, one at a time per repository and line, whichever path asks (FABRIC-END6c): a landing's press, bringing up to
    /// date, a moment. A fetch in flight counts as made: a second caller joins it and answers what it answered, rather than start another
    /// beside it. A fetch its own caller cancelled, or that threw, is no answer for one that joined it, which fetches afresh.
    /// </summary>
    internal static async Task<(string? Failed, string? Url)> OnceAsync(
        string root, string line, Func<CancellationToken, Task<(string? Failed, string? Url)>> fetch, CancellationToken ct)
    {
        if (Key(root, line) is not { } key) return await fetch(ct).ConfigureAwait(false);
        while (true)
        {
            var mine = new TaskCompletionSource<(string? Failed, string? Url)>(TaskCreationOptions.RunContinuationsAsynchronously);
            var flight = InFlight.GetOrAdd(key, mine.Task);
            if (flight != mine.Task)
            {
                try
                {
                    return await flight.WaitAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    continue;
                }
            }

            try
            {
                var fetched = await fetch(ct).ConfigureAwait(false);
                mine.SetResult(fetched);
                return fetched;
            }
            catch
            {
                mine.SetCanceled(CancellationToken.None);
                throw;
            }
            finally
            {
                InFlight.TryRemove(new KeyValuePair<string, Task<(string? Failed, string? Url)>>(key, mine.Task));
            }
        }
    }

    /// <summary>
    /// When git's own files say the line was last fetched here, across restarts, or null (FABRIC-END6c): <c>FETCH_HEAD</c>'s time where it
    /// names the line (every fetch writes it, a fetch that changed nothing too; Daoris's own writes none, and an empty one is a fetch that
    /// failed), and the newest entry of <c>origin/&lt;line&gt;</c>'s log, which every fetch that moved it writes, Daoris's own included. The
    /// later of the two. Read from the main checkout's git dir, with no process; a fetch made in a linked tree writes its own.
    /// </summary>
    public static DateTimeOffset? InGit(string commonDir, string line)
    {
        DateTimeOffset? head = null;
        try
        {
            var file = new FileInfo(Path.Combine(commonDir, "FETCH_HEAD"));
            if (file.Exists && file.Length > 0
                && File.ReadLines(file.FullName).Any(each => each.Contains($"\tbranch '{line}' of ", StringComparison.Ordinal)))
            {
                head = new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // Unreadable is unsaid: the moments' own ledger still counts.
        }

        var moved = LoggedAt(Path.Combine(commonDir, "logs", "refs", "remotes", "origin", line));
        return head is null ? moved : moved is null || head > moved ? head : moved;
    }

    /// <summary>The time of a ref log's newest entry (<c>old new who &lt;mail&gt; seconds zone\tmessage</c>), read from its tail; null where none.</summary>
    private static DateTimeOffset? LoggedAt(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            stream.Seek(Math.Max(0, stream.Length - 4096), SeekOrigin.Begin);
            using var reader = new StreamReader(stream);
            var last = reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            if (last is null) return null;
            var who = last.Split('\t', 2)[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return who.Length >= 2 && long.TryParse(who[^2], out var seconds) ? DateTimeOffset.FromUnixTimeSeconds(seconds) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

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

    /// <summary>When the line was last fetched or heard fetched (<see cref="RemoteFetches"/>), git's own files said as well (<see cref="RemoteFetches.InGit"/>).</summary>
    DateTimeOffset? Fetched(RefRepository repository, string line);

    /// <summary>Someone else's fetch moved origin's line, heard at <paramref name="at"/>.</summary>
    void HeardFetched(RefRepository repository, string line, DateTimeOffset at);

    /// <summary>
    /// Fetch the line from origin, counted as every fetch is: null where it fetched, else why not, a held origin's sentence where it is
    /// held (GITAUTH1a), which connects nothing.
    /// </summary>
    Task<string?> FetchAsync(RefRepository repository, string line, CancellationToken ct);

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
/// fetched. The first reading of a repository's line is its baseline, and asks nothing. A move that came while a pull request was inside
/// its minute is kept for it, and the first wake of that repository past the minute asks it, once (FABRIC-END6b).</item>
/// <item><b>Daoris pushed</b> (<c>refs</c>, <see cref="Pushed"/>): after the landing's plugin step and its tidy, whose own ask (LAND3) it
/// counts, and after a hand-off. The landing press's fetch of the line counts as the push's.</item>
/// <item><b>The window's focus</b> (<c>focus</c>, <see cref="Focused"/>): every repository with an open pull request.</item>
/// </list>
///
/// <para><b>The bounds.</b> A pull request is asked at most once a minute: D148's minute over the record's last ask
/// (<see cref="PullRequestAsking.Due"/>), and over every occasion's asks in this process (<see cref="PullRequestMinute"/>, FABRIC-END6c),
/// so a plugin not ready is not started again inside it and two occasions at once ask once. A
/// repository's line is fetched at most once a minute, every fetch of it counted (<see cref="RemoteFetches"/>). A moment with nothing due
/// fetches nothing and asks nothing. A moment that concerns no open pull request reads no line and starts no process.</para>
///
/// <para><b>Never at a removing press.</b> A <c>press</c> is no moment here, and a removing press's own ref moves (a branch deleted) move
/// no line of origin's. A repository a removing press, bringing it up to date or a landing holds is skipped, and asked once it is let
/// go (<see cref="RepositoryHolds"/>, FABRIC-END6c). The asks run one at a time, and only in the process holding the home's lease,
/// which alone keeps ends.</para>
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
    // The repositories a moment skipped while something held them (FABRIC-END6c), asked once they are let go.
    private readonly HashSet<string> _owed = new(StringComparer.OrdinalIgnoreCase);
    // FABRIC-END6b: when origin's line last moved in each repository while a pull request there was inside its minute, kept until every
    // open pull request of it was asked after that move, so the first wake past the minute asks it.
    private readonly Dictionary<string, DateTimeOffset> _moved = new(StringComparer.OrdinalIgnoreCase);
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

    /// <summary>
    /// What held <paramref name="repository"/> under <paramref name="home"/> let it go (<see cref="RepositoryHolds"/>, FABRIC-END6c): a
    /// moment it skipped is due now. Never throws.
    /// </summary>
    public static void Released(string home, string repository)
    {
        foreach (var moments in Of(home)) moments.HeardReleased(repository);
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

    /// <summary>A hold on a repository was let go: a moment owed there wakes.</summary>
    internal void HeardReleased(string repository)
    {
        bool owed;
        lock (_gate) owed = _owed.Contains(repository);
        if (owed) _changes.Now(ChangeTopics.Refs, repository);
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
    /// since that fetch brought the remote's facts), or where Daoris pushed (fetched unless the line was within the minute). A move that
    /// came while a pull request was inside its minute stays a moment for it (FABRIC-END6b), asked at the first wake past the minute.
    /// </summary>
    private async Task<int> RefsAsync(string name, CancellationToken ct)
    {
        // A push, or a moment skipped while the repository was held (FABRIC-END6c): asked as a push is, fetched where the minute allows.
        bool pushed;
        DateTimeOffset? pending;
        lock (_gate)
        {
            pushed = _pushed.Remove(name) | _owed.Remove(name);
            pending = _moved.TryGetValue(name, out var since) ? since : null;
        }

        var open = Concerning(name);
        if (open.Count == 0)
        {
            lock (_gate) _moved.Remove(name);
            return 0;
        }

        if (await KnownAsync(name, ct).ConfigureAwait(false) is not { } repository) return 0;
        if (await _world.LineAsync(repository, ct).ConfigureAwait(false) is not { } line) return 0;

        var now = _clock();
        var moved = Seen(repository, line, _world.OriginTip(repository, line));
        // The person's fetch is counted as one, so a moment inside its minute fetches nothing more.
        if (moved)
        {
            _world.HeardFetched(repository, line, now);
            pending = now;
        }

        if (pending is null && !pushed) return 0;
        // A push asks every open pull request; a move, each one not asked since it (its minute taken after it, or its record's ask).
        List<LandedBranch> asking = pushed ? [.. open] : [.. open.Where(entry => AskedAt(entry) < pending)];
        var asked = await AskDueAsync(repository, line, asking, fetch: !moved, now, ct).ConfigureAwait(false);

        // One inside its minute is still owed the move; the moments run one at a time, so nothing wrote this since it was read.
        lock (_gate)
        {
            if (pending is { } at && open.Any(entry => AskedAt(entry) < at)) _moved[name] = at;
            else _moved.Remove(name);
        }

        return asked;
    }

    /// <summary>When an open pull request was last asked: its record's last ask, or its minute taken in this process, the later.</summary>
    private DateTimeOffset AskedAt(LandedBranch entry)
    {
        var recorded = PullRequestAsking.LastAsked(entry);
        return PullRequestMinute.TakenAt(_home, entry) is { } taken && taken > recorded ? taken : recorded;
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
    /// Nothing due: nothing fetched and nothing asked. A repository something holds (<see cref="RepositoryHolds"/>) is skipped and owed
    /// (FABRIC-END6c). Each entry's minute is taken just before the ask, shared with every other occasion (<see cref="PullRequestMinute"/>).
    /// </summary>
    private async Task<int> AskDueAsync(
        RefRepository repository, string line, IReadOnlyList<LandedBranch> open, bool fetch, DateTimeOffset now, CancellationToken ct)
    {
        List<LandedBranch> due = [.. open.Where(entry => PullRequestAsking.Due(entry, now) && !PullRequestMinute.Within(_home, entry, now))];
        if (due.Count == 0) return 0;
        if (Owed(repository)) return 0;

        if (fetch && !(_world.Fetched(repository, line) is { } last && now - last < PullRequestAsking.Fresh))
        {
            // GITAUTH1a: the fetch's answer is read. One that failed moved nothing of origin's here, so nothing is seen; a held origin
            // was not tried at all, and its hold, not the minute, keeps the next moment from trying it (the git-auth design §5.2). The
            // plugin is asked all the same: it asks the host's own service, not git.
            // The fetch's own write to origin's line is no one else's fetch when the ref watch tells it.
            if (await _world.FetchAsync(repository, line, ct).ConfigureAwait(false) is null) Seen(repository, line, _world.OriginTip(repository, line));
        }

        // A press or a landing that took the repository during the fetch: skipped too, before anything is asked.
        if (Owed(repository)) return 0;
        var asking = _clock();
        due = [.. due.Where(entry => PullRequestMinute.TryTake(_home, entry, asking))];
        if (due.Count == 0) return 0;

        if (await _world.AskAsync(repository, due, ct).ConfigureAwait(false) > 0) _world.Judge(repository);
        return due.Count;
    }

    /// <summary>
    /// Whether something holds the repository now: the moment is skipped and owed, and asked once the hold is let go. A hold let go
    /// between the look and the owing wakes the moment at once, so none is lost.
    /// </summary>
    private bool Owed(RefRepository repository)
    {
        if (!RepositoryHolds.Held(_home, repository.Repository)) return false;
        lock (_gate) _owed.Add(repository.Repository);
        if (!RepositoryHolds.Held(_home, repository.Repository)) _changes.Now(ChangeTopics.Refs, repository.Repository);
        return true;
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

        // FABRIC-END6c: git's own files as well, so the person's fetch that changed nothing counts, and one made before a restart.
        public DateTimeOffset? Fetched(RefRepository repository, string line) =>
            (RemoteFetches.When(repository.Root, line), RemoteFetches.InGit(repository.CommonDir, line)) switch
            {
                ({ } here, { } git) => here > git ? here : git,
                (var here, var git) => here ?? git,
            };

        public void HeardFetched(RefRepository repository, string line, DateTimeOffset at) => RemoteFetches.Fetched(repository.Root, line, at);

        public async Task<string?> FetchAsync(RefRepository repository, string line, CancellationToken ct) =>
            (await TreeRefs.FetchAsync(repository.Root, line, ct, repository.Repository, repository.Workspace).ConfigureAwait(false)).Failed;

        public Task<int> AskAsync(RefRepository repository, IReadOnlyList<LandedBranch> entries, CancellationToken ct) =>
            trees.AskAtMomentAsync(repository.Root, repository.Repository, repository.Workspace, entries, ct);

        public void Judge(RefRepository repository) => RefMoves.Mine(home, repository.Repository);
    }
}

namespace Daoris.Driver;

/// <summary>A repository the ref watch hears: its name (the watch's subject), its workspace, and its main checkout.</summary>
/// <param name="Root">The main checkout, whose <c>.git</c> folder is the repository's common dir.</param>
public sealed record RefRepository(string Repository, string Workspace, string Root)
{
    /// <summary>The common git dir: every branch of the repository and every tree's <c>HEAD</c> is written under it (§A4.1).</summary>
    public string CommonDir => Path.Combine(Root, ".git");
}

/// <summary>One of Daoris's own ref moves, said once git is done: the home whose work it is, and the repository it moved.</summary>
/// <param name="Workspace">Where a tree was opened: with <paramref name="Root"/>, a repository not yet watched is added.</param>
public sealed record RefMove(string Home, string Repository, string? Workspace, string? Root);

/// <summary>
/// Daoris's own ref moves (FABRIC-END3, the design §B4): a tree opened, <i>Accept</i> and the tidy's removals each say their repository
/// here once git is done, and the home's <see cref="RefWatch"/> judges it at once rather than after its watcher's quiet window. Where
/// no watch of that home listens (a verb alone, a test), nothing hears it, and the next look's tidy judges the repository as before.
/// </summary>
public static class RefMoves
{
    private static readonly object Gate = new();

    private static readonly List<Listener> Listening = [];

    /// <summary>This process moved <paramref name="repository"/>'s refs under <paramref name="home"/>, and git is done. Never throws.</summary>
    public static void Mine(string home, string repository, string? workspace = null, string? root = null)
    {
        string full;
        try
        {
            full = Path.GetFullPath(home);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return;
        }

        Listener[] heard;
        lock (Gate) heard = [.. Listening.Where(each => string.Equals(each.Home, full, StringComparison.OrdinalIgnoreCase))];
        foreach (var each in heard)
        {
            try
            {
                each.Heard(new RefMove(full, repository, workspace, root));
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                // Unheard: the watcher's own hint judges it after its quiet window, which is the cost of a missed move and never a wrong answer.
            }
        }
    }

    /// <summary>Hear the moves made under <paramref name="home"/> until the answer is disposed.</summary>
    internal static IDisposable Listen(string home, Action<RefMove> heard)
    {
        var listener = new Listener(Path.GetFullPath(home), heard);
        lock (Gate) Listening.Add(listener);
        return listener;
    }

    private sealed class Listener(string home, Action<RefMove> heard) : IDisposable
    {
        public string Home { get; } = home;

        public Action<RefMove> Heard { get; } = heard;

        public void Dispose()
        {
            lock (Gate) Listening.Remove(this);
        }
    }
}

/// <summary>
/// The ref watch (FABRIC-END3, the design §A4.1 and §B4, link 1 of §C6): one <see cref="ChangeWatch"/> root on the common git dir of each
/// repository holding Daoris's work, never one per tree, whose hint makes that repository's end judgement due. The judge keeps what the
/// line holds (<see cref="EndKeeping"/>, through <see cref="SessionTrees.KeepEndsAsync"/>): a fact link, which emits only where a
/// <see cref="HeldFact"/> was written.
/// </summary>
/// <remarks>
/// <para><b>A hint is a wake, never a value.</b> The judge is handed the repository alone and reads its refs afresh; a ref told deleted is
/// read like any other move, and a ref missing at one read is never an end (§B7).</para>
///
/// <para><b>The filter</b> (<see cref="Hears"/>): <c>HEAD</c>, <c>packed-refs</c>, <c>refs/heads/**</c>, <c>refs/remotes/**</c>,
/// <c>worktrees/*/HEAD</c>, <c>reftable/**</c> and <c>worktrees/*/reftable/**</c>, by their final names. Git writes each through a
/// temporary renamed onto it (§A5, measured), so the rename is the event, and the temporaries, the objects, the logs and the index are
/// never heard.</para>
///
/// <para><b>Daoris's own moves</b> (<see cref="RefMoves"/>): its repository is judged at once, and the watcher's echo already heard is taken
/// up with it, so the move is judged once (canary 11); a later echo finds the facts as they were and writes nothing.</para>
///
/// <para><b>The reconciliation is the tidy's pass</b>, unchanged at its pace (§B4): it keeps every registered repository's ends before it
/// removes anything. The topic's deadline is that pace. A repository's own deadline reconciles its root (an absent folder tried, a folder
/// replaced at its path reopened, each then judged) and judges nothing a second time; the topic's whole deadline reads the set again.
/// Recovery and bounds are the change watch's: generations, a loss judged at once and reopened, a resume, at most
/// <see cref="ChangeWatch.MostRoots"/> roots, and <see cref="AtOnce"/> repositories judged at once.</para>
///
/// <para><b>Its machine-log line</b> is the change watch's <c>change.judged</c>: <c>topic</c> <c>refs</c>, <c>subject</c> the repository,
/// <c>why</c> (<c>hint</c>, <c>own</c>, or a recovery's word), <c>ms</c> the judgement's time, <c>acted</c> the facts written,
/// <c>waitedMs</c> from the first hint to the judgement's start, and <c>failed</c>. Link 1's <c>chain.fired</c> follows a judgement that
/// wrote a fact.</para>
/// </remarks>
public sealed class RefWatch : IAsyncDisposable
{
    /// <summary>The <c>refs</c> topic's deadline: the tidy's pace, whose pass is the reconciliation (§B4, §C5).</summary>
    public static readonly TimeSpan Deadline = BranchTidying.DefaultPace;

    /// <summary>How many repositories are judged at once: two, under PROC1a's measured four process starts at once (§A3).</summary>
    public const int AtOnce = 2;

    private readonly string _home;
    private readonly Func<CancellationToken, Task<IReadOnlyList<RefRepository>>> _repositories;
    private readonly Func<RefRepository, CancellationToken, Task<int>> _keep;
    private readonly ChangeWatch _changes;
    private readonly Chain _chain;
    private readonly object _gate = new();
    private readonly Dictionary<string, RefRepository> _watched = new(StringComparer.OrdinalIgnoreCase);
    private IDisposable? _listening;

    /// <param name="repositories">The repositories holding Daoris's work now, read at each whole deadline.</param>
    /// <param name="keep">One repository's end judgement: how many facts it wrote.</param>
    /// <param name="every">The topic's deadline; <see cref="Deadline"/> unless a test or a host says another.</param>
    /// <param name="source">What watches the folders: the machine's watchers unless a test hands its fake.</param>
    /// <param name="clock">What time it is; the system's unless a test hands its own.</param>
    public RefWatch(
        string home, Func<CancellationToken, Task<IReadOnlyList<RefRepository>>> repositories, Func<RefRepository, CancellationToken, Task<int>> keep,
        TimeSpan? every = null, IChangeSource? source = null, Func<DateTimeOffset>? clock = null)
    {
        _home = home;
        _repositories = repositories;
        _keep = keep;
        _changes = new ChangeWatch(source, clock);
        _chain = new Chain(_changes, clock: clock).Bind(ChainRanks.Judge, JudgeAsync);
        _chain.Topic(ChangeTopics.Refs, ChainEvents.Refs, every ?? Deadline, AtOnce);
    }

    /// <summary>The driver's: the registry's repositories holding Daoris's work (<see cref="RepositoriesAsync"/>), judged by the end keeper.</summary>
    /// <param name="heard">Told each repository judged (<see cref="Heard"/>): the landing's wake (FABRIC-END7).</param>
    public static RefWatch ForDriver(
        string home, Func<CancellationToken, Task<IReadOnlyList<RepoView>>> registry, TimeSpan? every = null, IChangeSource? source = null,
        MachineLog? log = null, Action<string>? heard = null) =>
        new(
            home,
            ct => RepositoriesAsync(home, registry, ct),
            (repository, ct) => new SessionTrees(home).KeepEndsAsync(repository.Root, repository.Repository, repository.Workspace, ct),
            every,
            source)
        {
            Log = log,
            Heard = heard,
        };

    /// <summary>
    /// Told each repository a hint, Daoris's own move or a root's recovery judges, before its ends are kept (FABRIC-END7): the landing's
    /// wake, which looks at the due entries of that repository alone (<see cref="LandingWakes.Moved"/>). Never told at a deadline.
    /// </summary>
    public Action<string>? Heard { get; init; }

    /// <summary>Where each judgement that was not an idle deadline, each loss and each link that acted is written; none by default.</summary>
    public MachineLog? Log
    {
        get => _changes.Log;
        init
        {
            _changes.Log = value;
            _chain.Log = value;
        }
    }

    /// <summary>The watch under the topic, which a test steps and settles.</summary>
    internal ChangeWatch Changes => _changes;

    /// <summary>The repositories watched now, by name.</summary>
    internal IReadOnlyList<string> Watched
    {
        get
        {
            lock (_gate) return [.. _watched.Keys];
        }
    }

    /// <summary>
    /// Whether a move under a common git dir, named with <c>/</c> between its parts, is one of git's ref writes, whatever its kind: a
    /// deletion is a hint like any other, since the judge reads the refs.
    /// </summary>
    public static bool Hears(string name)
    {
        // A temporary's rename onto its final name is the event (§A5): `<ref>.lock`, `packed-refs.new` and reftable's locks never are.
        if (name.EndsWith(".lock", StringComparison.Ordinal) || name.EndsWith(".new", StringComparison.Ordinal)) return false;
        return name.Split('/') switch
        {
            ["HEAD"] or ["packed-refs"] => true,
            ["refs", "heads" or "remotes", _, ..] => true,
            ["reftable", _, ..] => true,
            ["worktrees", _, "HEAD"] => true,
            ["worktrees", _, "reftable", _, ..] => true,
            _ => false,
        };
    }

    /// <summary>
    /// The repositories holding Daoris's work (§A4.1): those <c>session-branches.json</c> or <c>landings.json</c> keeps a standing entry of,
    /// at the root the registry names, where that root's <c>.git</c> is a folder (a main checkout, as the tidy asks). With no work kept,
    /// the registry is not asked.
    /// </summary>
    internal static async Task<IReadOnlyList<RefRepository>> RepositoriesAsync(
        string home, Func<CancellationToken, Task<IReadOnlyList<RepoView>>> registry, CancellationToken ct)
    {
        var holding = new SessionBranches(home).All().Select(entry => entry.Repository)
            .Concat(new LandedBranches(home).All().Select(entry => entry.Repository))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (holding.Count == 0) return [];
        var rows = await registry(ct).ConfigureAwait(false);
        return
        [
            .. rows
                .Where(row => holding.Contains(row.Repository) && !string.IsNullOrWhiteSpace(row.Root) && Directory.Exists(Path.Combine(row.Root, ".git")))
                .GroupBy(row => row.Repository, StringComparer.OrdinalIgnoreCase)
                .Select(same => same.First())
                .Select(row => new RefRepository(row.Repository, row.Workspace, Path.GetFullPath(row.Root!))),
        ];
    }

    /// <summary>Hear Daoris's own moves, open every root and judge whatever is due, until disposed; the set is read at once.</summary>
    public void Start()
    {
        Listen();
        _changes.Start();
        _changes.Now(ChangeTopics.Refs, ChangeTopics.Refs);
    }

    /// <summary>Hear Daoris's own moves and open every root, without stepping: a test steps by hand.</summary>
    internal void Open()
    {
        Listen();
        _changes.Open();
    }

    /// <summary>The machine resumed from sleep: every repository's root is reopened and judged now.</summary>
    public void Resumed() => _changes.Resumed();

    /// <summary>
    /// The set read again: a repository that no longer holds Daoris's work let go of, one that does now watched. How many changed.
    /// </summary>
    internal async Task<int> RebuildAsync(CancellationToken ct)
    {
        var now = (await _repositories(ct).ConfigureAwait(false))
            .GroupBy(repository => repository.Repository, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(same => same.Key, same => same.First(), StringComparer.OrdinalIgnoreCase);
        lock (_gate)
        {
            // Let go of first, so a place the cap gave back goes to a repository that still holds work.
            string[] gone = [.. _watched.Where(pair => !now.TryGetValue(pair.Key, out var kept) || kept != pair.Value).Select(pair => pair.Key)];
            foreach (var name in gone)
            {
                _changes.Unwatch(ChangeTopics.Refs, _watched[name].Repository);
                _watched.Remove(name);
            }

            RefRepository[] added = [.. now.Values.Where(repository => !_watched.ContainsKey(repository.Repository))];
            foreach (var repository in added) Watch(repository);
            return gone.Length + added.Length;
        }
    }

    private void Watch(RefRepository repository)
    {
        _watched[repository.Repository] = repository;
        _changes.Watch(ChangeTopics.Refs, repository.Repository, repository.CommonDir, (name, _) => Hears(name), subfolders: true);
    }

    private void Listen() => _listening ??= RefMoves.Listen(_home, Moved);

    /// <summary>Daoris's own move: its repository judged now, added first where a tree was opened in one not yet watched.</summary>
    private void Moved(RefMove move)
    {
        string? subject = null;
        lock (_gate)
        {
            if (_watched.TryGetValue(move.Repository, out var known))
            {
                subject = known.Repository;
            }
            else if (move is { Workspace: { } workspace, Root: { } root } && Directory.Exists(Path.Combine(root, ".git")))
            {
                var added = new RefRepository(move.Repository, workspace, Path.GetFullPath(root));
                Watch(added);
                subject = added.Repository;
            }
        }

        if (subject is not null) _changes.Now(ChangeTopics.Refs, subject);
    }

    /// <summary>
    /// Link 1 on the refs: the topic's own subject reads the set again; a repository's hint, its own move or its root's recovery judges
    /// it; a repository's deadline judges nothing, its ends being the tidy pass's. What changed is emitted as <c>work.held</c>.
    /// </summary>
    private async Task<ChainFired> JudgeAsync(IReadOnlyList<ChainEvent> heard, CancellationToken ct)
    {
        var acted = 0;
        List<ChainEvent> emitted = [];
        foreach (var @event in heard)
        {
            if (@event.Topic == ChangeTopics.Refs && @event.Subject == ChangeTopics.Refs)
            {
                acted += await RebuildAsync(ct).ConfigureAwait(false);
                continue;
            }

            if (@event.Type != ChainEvents.Refs) continue;
            RefRepository? repository;
            lock (_gate) _watched.TryGetValue(@event.Subject, out repository);
            if (repository is null) continue;
            Heard?.Invoke(repository.Repository);
            var written = await _keep(repository, ct).ConfigureAwait(false);
            if (written <= 0) continue;
            acted += written;
            emitted.Add(new ChainEvent(ChainEvents.WorkHeld, repository.Repository, ChangeTopics.Refs));
        }

        return new ChainFired(acted, emitted);
    }

    public async ValueTask DisposeAsync()
    {
        _listening?.Dispose();
        await _changes.DisposeAsync().ConfigureAwait(false);
    }
}

using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>
/// Whose moves a take-up takes (the foundation design §3.3, §13.4): an executor kind, and the lines it runs where it runs only some.
/// </summary>
/// <param name="Executor"><see cref="MoveExecutors"/>' word.</param>
/// <param name="Lines">The records left whose moves are this executor's, or null for every one of its kind.</param>
public sealed record MoveTakeUpScope(string Executor, Func<string, bool>? Lines = null)
{
    /// <summary>A shell's: a dead run's conversation moves are any shell's of the home, the first to claim under the write lock.</summary>
    public static MoveTakeUpScope Conversations { get; } = new(MoveExecutors.Conversation);

    /// <summary>The lease holder's look: every dead run's driven move.</summary>
    public static MoveTakeUpScope LeaseHolder { get; } = new(MoveExecutors.Driven);

    /// <summary>A <c>--share</c> loop beside the instance: only the moves of the lines it runs, since the two race for the same quests.</summary>
    public static MoveTakeUpScope Shared(Func<string, bool> lines) => new(MoveExecutors.Driven, lines);

    /// <summary>Whether a move is this scope's.</summary>
    public bool Holds(MoveRecord move) => string.Equals(move.Executor, Executor, StringComparison.Ordinal) && (Lines is null || Lines(move.Left));
}

/// <summary>What one come-up or look found and did (<see cref="MoveTakeUp.LookAsync"/>), which the caller's own sweep reads.</summary>
/// <param name="Imported">The moves an older build's box kept, imported this look.</param>
/// <param name="ImportsHeld">Why the imports wait, the start's confirmation not settled among the reasons; null where none does.</param>
/// <param name="Never">The records whose box is still to be imported: their old part is the course's <c>Never</c>, which no go-on or sweep acts on.</param>
/// <param name="Taken">The dead runs' moves this run took up, to go on from their phase.</param>
/// <param name="Held">The dead runs' moves taken and held for the person: an effect marked and never recorded, never made again by itself.</param>
/// <param name="Gone">The dead runs' moves taken and closed <c>gone</c>: a record they name no longer exists.</param>
/// <param name="Quiet">The live runs' moves said <c>move.quiet</c> this look.</param>
/// <param name="Refused">The takes refused, another taker having claimed first.</param>
public sealed record MoveLook(
    IReadOnlyList<MoveRecord> Imported, string? ImportsHeld, IReadOnlyList<string> Never, IReadOnlyList<MoveTaken> Taken, IReadOnlyList<MoveRecord> Held,
    IReadOnlyList<MoveRecord> Gone, IReadOnlyList<MoveRecord> Quiet, IReadOnlyList<MoveTaken> Refused);

/// <summary>
/// The take-up of dead runs' moves (FOUNDATION1f-i part 2, D212; the foundation design §3.3, §13.4, §13.5, §19.1): one unit an executor calls at
/// its come-up and at each look, before anything else that could act on the same records, in the design's order. Called from nothing yet:
/// rows f-ii to f-vi, h and i place it, a conversation's where <c>LimitMove.TakeUpBoxesAsync</c> runs today and a driven line's where the
/// look settles uncertain words.
/// </summary>
/// <remarks>
/// <para><b>1. The imports</b> of any box an older build wrote, each its importer's, which waits until the start's confirmation is settled and
/// is asked again at every look until then; a record whose box is still to be imported is <c>Never</c>, so neither the take-up below nor the
/// caller's sweep acts on it meanwhile.</para>
/// <para><b>2. The take-up.</b> Every standing move of this unit's kind and lines whose claim's run is dead by the lease's own test is taken,
/// once, by the guarded update that names the run and the generation it was read at, so of two takers the second is refused by the
/// generation. A run that lives, however long it pauses, or that cannot be told dead keeps its moves; so does a move at a phase a newer build
/// wrote. A taken move whose effect was marked and has no outcome is held for the person (<see cref="MoveHoldWhy.Unrecorded"/>), never made
/// again by itself; one whose record left or destination no longer exists closes <c>gone</c>; any other is the caller's to go on from its
/// phase. A live run's move past <c>old-ended</c> whose phase has stood ten minutes is said <c>move.quiet</c> once, on its line and in the
/// log, whichever process looks, and never taken over.</para>
/// <para><b>3. The caller's own sweep</b>, handed what the look found.</para>
/// <para>A shell's come-up first records its run and says once, refusing nothing, that another shell runs on its home (§19.1 point 3).</para>
/// </remarks>
/// <param name="home">The home whose move record this is.</param>
/// <param name="store">The move record, under this process's run.</param>
/// <param name="scope">Whose moves this unit takes up.</param>
/// <param name="imports">The importers of the boxes an older build writes, run first; none until rows f-v and f-vi hand them.</param>
/// <param name="note">Keeps a note on a record's line: how <c>move.quiet</c> is said there.</param>
/// <param name="clock">What time it is, for a phase's quiet.</param>
public sealed class MoveTakeUp(
    string home, MoveStore store, MoveTakeUpScope scope, IReadOnlyList<IMoveBoxImport>? imports = null, Action<string, Noted>? note = null,
    TimeProvider? clock = null)
{
    /// <summary>How long a move's phase past <c>old-ended</c> stands under a live run before it is said quiet (§19.1 point 4).</summary>
    public static readonly TimeSpan QuietAfter = TimeSpan.FromMinutes(10);

    /// <summary>
    /// The phases a quiet is said for: past <c>old-ended</c> and before the end. <c>quiescing</c> waits on the old part's own turn, which its line
    /// shows, and a hold waits on the person.
    /// </summary>
    private static readonly MoveRecordPhase[] Quieting =
        [MoveRecordPhase.Opening, MoveRecordPhase.Opened, MoveRecordPhase.Copied, MoveRecordPhase.Verified, MoveRecordPhase.Delivering];

    private static readonly ConcurrentDictionary<string, byte> ToldShells = new(StringComparer.Ordinal);

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <summary>Called with each dead run's move just before its take, as read: a test's way to let another taker in between.</summary>
    internal Action<MoveRecord>? BeforeTake { get; set; }

    /// <summary>The line a quiet move is said by on its record: its code's English, the page wording it in the reader's language.</summary>
    public static Noted QuietNote(int minutes) =>
        Noted.Of(
            NoteCodes.MoveQuiet,
            $"Its move to a new part has not gone on for {minutes} minutes. The Daoris making it still runs, so the move stays with it; once that Daoris quits, the move is taken up again.",
            ("minutes", minutes));

    /// <summary>
    /// A come-up: a shell records its run and says once that another shell runs on its home, then the look. Refuses nothing.
    /// </summary>
    /// <param name="stands">Whether a record still exists in the ledger; null where it cannot be told, which closes nothing.</param>
    /// <param name="sweep">The caller's own sweep, run last.</param>
    /// <param name="say">Where the other shell is said, on the channel the lease's wait uses.</param>
    public async Task<MoveLook> ComeUpAsync(
        Func<string, bool?> stands, Func<MoveLook, CancellationToken, Task>? sweep = null, Action<string>? say = null, CancellationToken ct = default)
    {
        if (string.Equals(store.Kind, StoreProcess.Shell, StringComparison.Ordinal)) TellAnotherShell(say);
        return await LookAsync(stands, sweep, ct).ConfigureAwait(false);
    }

    /// <summary>One look in the design's order: the imports, the take-up, then <paramref name="sweep"/>, handed what the look found.</summary>
    /// <param name="stands">Whether a record still exists in the ledger; null where it cannot be told, which closes nothing.</param>
    public async Task<MoveLook> LookAsync(Func<string, bool?> stands, Func<MoveLook, CancellationToken, Task>? sweep = null, CancellationToken ct = default)
    {
        // 1. The imports, before anything that could act on a box's record.
        var imported = new List<MoveRecord>();
        var waiting = new HashSet<string>(StringComparer.Ordinal);
        var never = new SortedSet<string>(StringComparer.Ordinal);
        string? importsHeld = null;
        foreach (var import in imports ?? [])
        {
            var pass = import.Import();
            imported.AddRange(pass.Imported);
            waiting.UnionWith(pass.Waiting);
            never.UnionWith(pass.Never);
            importsHeld ??= pass.Held;
        }

        // 2. The take-up of dead runs' moves of this unit's kind and lines, and a live run's quiet.
        var taken = new List<MoveTaken>();
        var held = new List<MoveRecord>();
        var gone = new List<MoveRecord>();
        var quiet = new List<MoveRecord>();
        var refused = new List<MoveTaken>();
        var runs = store.Runs().ToDictionary(run => run.Run, StringComparer.Ordinal);
        foreach (var move in store.StandingOf(scope.Executor).Where(scope.Holds))
        {
            ct.ThrowIfCancellationRequested();
            // A box still to be imported holds its record; a phase a newer build wrote is that build's to go on with.
            if (waiting.Contains(move.Left) || move.Phase is null) continue;
            try
            {
                var run = runs.GetValueOrDefault(move.ClaimRun);
                if (string.Equals(move.ClaimRun, store.Run, StringComparison.Ordinal) || run is null || store.Alive(run))
                {
                    if (run is not null && Quiet(move, run) is { } said) quiet.Add(said);
                    continue;
                }

                BeforeTake?.Invoke(move);
                var take = store.Take(move.Id, move.ClaimGeneration);
                if (take is not { Claim: { } claim, Move: { } now })
                {
                    refused.Add(take);
                    continue;
                }

                if (stands(now.Left) == false || (now.Destination is { } destination && stands(destination) == false))
                {
                    gone.Add(store.Close(claim, MoveCloseWhy.Gone));
                }
                else if (now.Effect is { Outcome: null } && now.Phase != MoveRecordPhase.Held)
                {
                    held.Add(store.Hold(claim, MoveHoldWhy.Unrecorded));
                }
                else
                {
                    taken.Add(take);
                }
            }
            catch (Exception error) when (error is DriverException or SqliteException)
            {
                // The store would not take it now, or the move moved under the write: the next look reads it again.
            }
        }

        var look = new MoveLook(imported, importsHeld, [.. never], taken, held, gone, quiet, refused);

        // 3. The caller's own sweep.
        if (sweep is not null) await sweep(look, ct).ConfigureAwait(false);
        return look;
    }

    /// <summary>The move said quiet now, where its phase past <c>old-ended</c> has stood ten minutes under a live run and no look said it for that phase yet.</summary>
    private MoveRecord? Quiet(MoveRecord move, StoreRun run)
    {
        if (move.Phase is not { } phase || !Quieting.Contains(phase) || !move.Moments.TryGetValue(phase, out var moment)) return null;

        // Said already for this phase, as the look read it: no write is asked for at every look after.
        if (string.Equals(move.QuietAt, moment, StringComparison.Ordinal)) return null;
        if (!DateTimeOffset.TryParse(moment, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var since)) return null;
        var stood = _clock.GetUtcNow() - since;
        if (stood < QuietAfter || !store.SayQuiet(move, moment)) return null;
        var minutes = (int)Math.Floor(stood.TotalMinutes);
        note?.Invoke(move.Left, QuietNote(minutes));
        using var log = new MachineLog(home, "driver");
        log.Warn("move.quiet", ("move", move.Id), ("session", move.Left), ("phase", move.PhaseWord), ("kind", run.Kind), ("pid", run.Pid), ("minutes", minutes));
        return move;
    }

    /// <summary>This shell's run recorded, and another live shell's on the home said once for this run, with its process and since when.</summary>
    private void TellAnotherShell(Action<string>? say)
    {
        try
        {
            store.Enter();
            var other = store.Runs().FirstOrDefault(run =>
                string.Equals(run.Kind, StoreProcess.Shell, StringComparison.Ordinal) && !string.Equals(run.Run, store.Run, StringComparison.Ordinal) && store.Alive(run));
            if (other is null || !ToldShells.TryAdd(home + "\n" + store.Run, 0)) return;
            var since = DateTimeOffset.TryParse(other.Began, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var began)
                ? began.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)
                : other.Began;
            say?.Invoke(
                $"Another Daoris runs on this home: process {other.Pid}, since {since}. Both run here, each keeping the moves it makes; nothing is refused.");
            using var log = new MachineLog(home, "driver");
            log.Warn("home.another-shell", ("pid", other.Pid), ("since", other.Began));
        }
        catch (Exception error) when (error is DriverException or SqliteException)
        {
            // The store would not record the run (a newer Daoris stamped it, or it is busy): nothing is said, and nothing refused.
        }
    }
}

namespace Daoris.Driver;

/// <summary>
/// A move a held file still refused when the wait for it ran out (SWAP2): the swap's <c>busy</c>, and the only one. Any
/// other failure of a move is thrown as it was. Its message names each process found holding it, with when it started and
/// its command line (DEPLOYBUSY1), for the journal's detail.
/// </summary>
public sealed class HeldMoveException(SwapHold hold, Exception refusal, IReadOnlyList<FileHolder>? holders = null)
    : IOException($"{hold.Said} ({refusal.Message.TrimEnd('.')}){Described(holders)}", refusal)
{
    public SwapHold Hold { get; } = hold;

    /// <summary>The most of a command line a detail carries.</summary>
    private const int CommandMost = 300;

    private static string Described(IReadOnlyList<FileHolder>? holders) =>
        holders is { Count: > 0 }
            ? "; " + string.Join("; ", holders.Select(holder =>
                holder.Said
                + (holder.Started is { } started
                    ? " started " + started.ToString("HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture)
                    : "")
                + (holder.Command is { Length: > 0 } command
                    ? ": " + (command.Length > CommandMost ? command[..CommandMost] + "…" : command)
                    : "")))
            : "";
}

/// <summary>
/// The swap itself (D139 §5, §6), run by the launcher while nothing runs from <c>app/</c>: check, move with a journal,
/// start the new application and wait for it to confirm, or put the build before it back.
/// </summary>
/// <remarks>
/// <para>Every effect on a process is handed in, so the tests move real folders in a scratch install and start nothing.</para>
///
/// <para><b>Compiled into the launcher too</b>, beside <see cref="StagedBuild"/> (<c>Daoris.Desktop.Launcher.csproj</c>): so it uses nothing but the base library.</para>
/// </remarks>
public sealed class InstallSwap(
    string install,
    // Starts the application in `app/` with these arguments; its process id, or null when it would not start.
    Func<IReadOnlyList<string>, int?> start,
    // Whether that process still runs.
    Func<int, bool> alive,
    Func<DateTimeOffset>? clock = null,
    Action<TimeSpan>? sleep = null)
{
    /// <summary>How long a new application has to confirm before a launcher stops waiting for it.</summary>
    public TimeSpan ConfirmWithin { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>How often the journal is read while waiting.</summary>
    public TimeSpan Poll { get; init; } = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// How long a move a held file refuses is tried, <see cref="HeldWait"/> apart, before the swap gives it up as
    /// <c>busy</c>, by the clock, for each move. Two minutes (SWAP2, D139's note): ten seconds was outlasted twice by the
    /// deployment rehearsal with a build beside it, and the publish's own staging needed more than ten; two minutes stays
    /// inside the three the launcher already waits for the application to close. Written here, not through the driver's
    /// <c>AtomicFile</c>, because the launcher compiles this file alone.
    /// </summary>
    public TimeSpan HeldWithin { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>The wait between two tries of a held move.</summary>
    public TimeSpan HeldWait { get; init; } = TimeSpan.FromMilliseconds(200);

    /// <summary>The move itself, from and to full paths, and whether a file may be written over; a test hands in one that refuses.</summary>
    public Action<string, string, bool>? Mover { get; init; }

    /// <summary>Every move this swap found held, journalled with each write (SWAP2): the measurement the wait is sized by.</summary>
    private readonly List<SwapHold> _holds = [];

    /// <summary>A folder or a file moved on the disk, as the swap moves it.</summary>
    public static void MoveOnDisk(string source, string target, bool overwrite)
    {
        if (Directory.Exists(source)) Directory.Move(source, target);
        else File.Move(source, target, overwrite);
    }

    /// <summary>
    /// Whether a move's failure is a hold that may give way: access denied, which <c>Directory.Move</c> throws as an
    /// <see cref="IOException"/> of <c>0x80070005</c> for a folder a file inside is held open in (measured, SWAP2), or a
    /// sharing, lock or busy refusal of a file. Anything else (a path missing, a target already there) no wait mends, and
    /// it fails at once. The codes are Win32's; elsewhere an <see cref="IOException"/> carries an errno, and no hold.
    /// </summary>
    public static bool IsHeld(Exception error) =>
        error is UnauthorizedAccessException
        || (OperatingSystem.IsWindows()
            && error is IOException and not FileNotFoundException and not DirectoryNotFoundException
            && (error.HResult & 0xFFFF) is 5 or 32 or 33 or 170);

    /// <summary>
    /// A folder or a file moved, tried again while a held file refuses it, for <see cref="HeldWithin"/>; every hold met is
    /// kept with how long it lasted, and what was found holding it (DEPLOYBUSY1): asked at the first refusal, and again when
    /// the wait gives up, since what holds it at the end is what made it <c>busy</c>. A hold that outlasts the wait throws
    /// <see cref="HeldMoveException"/>; any other failure throws at once.
    /// </summary>
    private void MoveHeld(string source, string target, bool overwrite = false)
    {
        DateTimeOffset? since = null;
        HeldEntry? found = null;
        for (var tries = 1; ; tries++)
        {
            try
            {
                (Mover ?? MoveOnDisk)(source, target, overwrite);
                if (since is { } first) _holds.Add(Hold(source, target, Now - first, tries, released: true, found));
                return;
            }
            catch (Exception refusal) when (IsHeld(refusal))
            {
                if (since is null) found = HeldFiles.Find(source, most: 1).FirstOrDefault();
                since ??= Now;
                if (Now - since.Value >= HeldWithin)
                {
                    found = HeldFiles.Find(source, most: 1).FirstOrDefault() ?? found;
                    var hold = Hold(source, target, Now - since.Value, tries, released: false, found);
                    _holds.Add(hold);
                    throw new HeldMoveException(hold, refusal, found?.By);
                }

                Sleep(HeldWait);
            }
        }
    }

    private SwapHold Hold(string source, string target, TimeSpan held, int tries, bool released, HeldEntry? found) =>
        new(Relative(source), Relative(target), (long)held.TotalMilliseconds, tries, released,
            found is null ? null : Relative(found.Path),
            found is null ? null : string.Join(", ", found.By.Select(holder => holder.Said)));

    private string Relative(string full) => Path.GetRelativePath(install, full).Replace(Path.DirectorySeparatorChar, '/');

    /// <summary>The journal written with every hold this swap has met.</summary>
    private void Journal(SwapRecord record) =>
        StagedBuild.WriteJournal(install, _holds.Count == 0 ? record : record with { Holds = _holds.ToList() });

    private DateTimeOffset Now => (clock ?? (() => DateTimeOffset.UtcNow))();

    private void Sleep(TimeSpan span) => (sleep ?? Thread.Sleep)(span);

    private string Full(string relative) => Path.Combine(install, relative.Replace('/', Path.DirectorySeparatorChar));

    private static string Under(params string[] segments) => string.Join('/', segments);

    /// <summary>
    /// What a launcher that died left (D139 §6), put right before anything else: a journal still <c>swapping</c> is undone;
    /// one <c>started</c> and never confirmed, with nothing running from <c>app/</c>, is a new application that failed, and
    /// is undone too; one confirmed and never marked installed is installed. The phase it put right to, or null when there
    /// was nothing to put right; either way the launcher goes on as it would have. A roll-back that stopped part way left
    /// its reason in the journal, and is finished with it (SWAP2).
    /// </summary>
    public string? Recover(bool appRunning)
    {
        var record = StagedBuild.ReadJournal(install);
        _holds.Clear();
        _holds.AddRange(record?.Holds ?? []);
        switch (record?.Phase)
        {
            case SwapPhase.Swapping:
                Undo(record, record.Reason ?? "interrupted",
                    record.Detail ?? "the launcher stopped while it was swapping; the moves it made were put back.");
                return SwapPhase.RolledBack;

            case SwapPhase.Started when !appRunning:
                Undo(record, record.Reason ?? "exited",
                    record.Detail ?? "the new build never said it came up, and nothing of it runs; the build before it was put back.");
                return SwapPhase.RolledBack;

            case SwapPhase.Confirmed:
                Finish(record);
                return SwapPhase.Installed;

            default:
                return null;
        }
    }

    /// <summary>
    /// The update met an error (D139 §6; SWAP2): a swap under way is put right from its journal, as a launcher that died is,
    /// keeping the reason it was undoing for; one started whose application runs is left to it. With no swap under way, an
    /// update asked for refuses the staged build as <c>error</c>, so the application is not closed for the same build
    /// again. Never a refusal written over a swap under way, and never <c>busy</c>.
    /// </summary>
    public SwapOutcome AfterError(bool appRunning, bool updating)
    {
        if (Recover(appRunning) is { } put)
        {
            return new SwapOutcome(put, StagedBuild.ReadJournal(install)?.Reason, StartOld: put != SwapPhase.Installed);
        }

        // Still under way: started, and its application runs. It is the application now; the launcher starts no other.
        if (StagedBuild.UnderWay(install)) return new SwapOutcome(null, null, StartOld: false);
        return updating ? Held("the launcher met an error before it could swap.", "error") : SwapOutcome.Nothing;
    }

    /// <summary>
    /// Install the staged build: check it, move it in with a journal, start it and wait for it to confirm, or roll back.
    /// <see cref="SwapOutcome.Nothing"/> when nothing is staged.
    /// </summary>
    public SwapOutcome Run(IReadOnlyList<string> arguments)
    {
        _holds.Clear();
        var manifest = StagedBuild.Read(install, out var unread);
        if (manifest is null && unread is null) return SwapOutcome.Nothing;

        // Checked here, by the program that is about to move the files, whatever the application checked before it closed.
        if ((unread ?? StagedBuild.Check(install)) is { } problem) return Refuse(manifest, problem);

        var previous = Full(Under(StagedBuild.Folder, StagedBuild.Previous));
        try
        {
            if (Directory.Exists(previous)) Directory.Delete(previous, recursive: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return Refuse(manifest, new StagedProblem("previous", $"the last swap's update/previous could not be cleared ({error.Message.TrimEnd('.')})."));
        }

        var record = new SwapRecord(SwapPhase.Swapping, manifest!.Id, manifest.Version, manifest.Commit, Now, []);
        Journal(record);
        Directory.CreateDirectory(previous);

        var moves = new List<SwapMove>();
        try
        {
            // The build before it aside, then the staged one in: each move journalled before the next is made.
            foreach (var name in StagedBuild.Swapped)
            {
                if (Exists(name)) Move(name, Under(StagedBuild.Folder, StagedBuild.Previous, name));
            }

            foreach (var name in StagedBuild.Swapped)
            {
                Move(Under(StagedBuild.Folder, StagedBuild.Staged, name), name);
            }
        }
        catch (HeldMoveException held)
        {
            // `busy` is this and only this: a hold that outlasted the wait, and the moves before it put back (SWAP2).
            Undo(record with { Moves = moves }, "busy", $"a file the swap had to move was held: {held.Message}; nothing was changed.");
            return new SwapOutcome(SwapPhase.RolledBack, "busy", StartOld: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Undo(record with { Moves = moves }, "move", $"a move the swap had to make failed ({error.Message.TrimEnd('.')}); nothing was changed.");
            return new SwapOutcome(SwapPhase.RolledBack, "move", StartOld: true);
        }

        // What is left of the staged folder is its manifest, already in the journal: gone, so nothing reads it as staged.
        TryDelete(StagedBuild.StagedOf(install));

        record = record with { Moves = moves, Phase = SwapPhase.Started, At = Now };
        Journal(record);

        var pid = start(arguments);
        if (pid is null)
        {
            Undo(record, "start", "the new build's application would not start; the build before it was put back.");
            return new SwapOutcome(SwapPhase.RolledBack, "start", StartOld: true);
        }

        record = record with { Pid = pid };
        var deadline = Now + ConfirmWithin;
        while (true)
        {
            var read = StagedBuild.ReadJournal(install);
            if (read?.Phase == SwapPhase.Confirmed)
            {
                Finish(read);
                return new SwapOutcome(SwapPhase.Installed, null, StartOld: false);
            }

            if (!alive(pid.Value))
            {
                // One last read: an application that confirmed and then ended is installed; the person may have closed it.
                if (StagedBuild.ReadJournal(install) is { Phase: SwapPhase.Confirmed } late)
                {
                    Finish(late);
                    return new SwapOutcome(SwapPhase.Installed, null, StartOld: false);
                }

                Undo(record, "exited", "the new build's application ended before it came up; the build before it was put back.");
                return new SwapOutcome(SwapPhase.RolledBack, "exited", StartOld: true);
            }

            if (Now >= deadline)
            {
                // Still running and silent: it is the application now, and the record says it never confirmed.
                Finish(record with { Confirmed = false });
                return new SwapOutcome(SwapPhase.Installed, "unconfirmed", StartOld: false);
            }

            Sleep(Poll);
        }

        void Move(string from, string to)
        {
            var source = Full(from);
            var target = Full(to);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            MoveHeld(source, target);
            moves.Add(new SwapMove(from, to));
            Journal(record with { Moves = moves.ToList() });
        }
    }

    /// <summary>
    /// An update asked for while something still runs from <c>app/</c> once the wait is over (D139 §5): the staged build is
    /// refused, <c>busy</c>, and moved aside, so the application started again on the build before it does not drain and
    /// close for the same build again; <paramref name="code"/> names another reason, <c>error</c> for the launcher's.
    /// <see cref="SwapOutcome.Nothing"/> when nothing is staged, or when a swap is under way: its journal is what puts the
    /// install right, and a refusal written over it would lose its moves (SWAP2).
    /// </summary>
    public SwapOutcome Held(string detail, string code = "busy")
    {
        if (StagedBuild.UnderWay(install)) return SwapOutcome.Nothing;
        var manifest = StagedBuild.Read(install, out var unread);
        if (manifest is null && unread is null) return SwapOutcome.Nothing;
        return Refuse(manifest, new StagedProblem(code, detail));
    }

    private bool Exists(string relative) => Directory.Exists(Full(relative)) || File.Exists(Full(relative));

    private SwapOutcome Refuse(StagedManifest? manifest, StagedProblem problem)
    {
        var record = new SwapRecord(
            SwapPhase.Refused, manifest?.Id, manifest?.Version, manifest?.Commit, Now, [], Reason: problem.Code, Detail: problem.Sentence);
        Journal(record);
        // A build refused is not tried again at every start: moved aside, as a failed one is, over the last one aside.
        TryDelete(Full(Under(StagedBuild.Folder, StagedBuild.Failed)));
        var holds = _holds.Count;
        MoveAside(StagedBuild.StagedOf(install));
        if (_holds.Count > holds) Journal(record);
        return new SwapOutcome(SwapPhase.Refused, problem.Code, StartOld: true);
    }

    /// <summary>Done: the build before it removed, and the journal says installed.</summary>
    private void Finish(SwapRecord record)
    {
        TryDelete(Full(Under(StagedBuild.Folder, StagedBuild.Previous)));
        Journal(record with
        {
            Phase = SwapPhase.Installed,
            Confirmed = record.Confirmed ?? true,
            At = Now,
        });
    }

    /// <summary>
    /// Put the build before it back (D139 §6): the journal's moves undone in reverse, a staged file that had moved in going
    /// to <c>update/failed/</c> rather than back to <c>staged/</c>, so it is not tried again.
    /// </summary>
    /// <remarks>
    /// The reason is journalled before the first move back, and the moves still to undo after each one (SWAP2): a move back
    /// that a hold outlasts throws with the journal still under way, so the launcher's <see cref="AfterError"/>, or the next
    /// start, finishes it with the reason it was undoing for and never moves a file it already put back. A journal that
    /// already carries a reason is such a finish, and keeps what the first attempt moved to <c>update/failed/</c>.
    /// </remarks>
    private void Undo(SwapRecord record, string reason, string detail)
    {
        var failed = Full(Under(StagedBuild.Folder, StagedBuild.Failed));
        if (record.Reason is null) TryDelete(failed);
        record = record with { Reason = reason, Detail = detail };
        var remaining = record.Moves.ToList();
        Journal(record with { Moves = remaining.ToList() });

        var stagedPrefix = Under(StagedBuild.Folder, StagedBuild.Staged) + "/";
        while (remaining.Count > 0)
        {
            var move = remaining[^1];
            var back = move.From.StartsWith(stagedPrefix, StringComparison.Ordinal)
                ? Under(StagedBuild.Folder, StagedBuild.Failed, move.From[stagedPrefix.Length..])
                : move.From;
            var source = Full(move.To);
            var target = Full(back);
            if (Directory.Exists(source) || File.Exists(source))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                try
                {
                    MoveHeld(source, target, overwrite: true);
                }
                catch (HeldMoveException)
                {
                    // The hold that stopped it, journalled with what is left to undo, for whoever finishes it.
                    Journal(record with { Moves = remaining.ToList() });
                    throw;
                }
            }

            remaining.RemoveAt(remaining.Count - 1);
            Journal(record with { Moves = remaining.ToList() });
        }

        TryDelete(Full(Under(StagedBuild.Folder, StagedBuild.Previous)));
        MoveAside(StagedBuild.StagedOf(install));
        Journal(record with
        {
            Phase = SwapPhase.RolledBack,
            Moves = [],
            At = Now,
        });
    }

    /// <summary>What is left of a staged build, into <c>update/failed/</c>: never tried again, kept to look at.</summary>
    private void MoveAside(string staged)
    {
        if (!Directory.Exists(staged)) return;
        var failed = Full(Under(StagedBuild.Folder, StagedBuild.Failed));
        try
        {
            Directory.CreateDirectory(failed);
            foreach (var entry in Directory.EnumerateFileSystemEntries(staged))
            {
                var target = Path.Combine(failed, Path.GetFileName(entry));
                TryDelete(target);
                MoveHeld(entry, target, overwrite: true);
            }

            Directory.Delete(staged, recursive: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Left where it is: the journal already says it was not installed, and a next stage replaces it.
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            else if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A leftover the next swap clears first.
        }
    }
}

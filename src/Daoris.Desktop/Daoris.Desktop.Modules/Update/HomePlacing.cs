using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>An entry renamed at a start (HOMELAYOUT3): its name, where it was, where it went, and why it did not where it did not.</summary>
public sealed record HomeMove(string Entry, string From, string? To, string? Reason = null);

/// <summary>
/// What a start's move into D172's places did: what moved, what could not and why, what a start that stopped mid-move left and
/// this one put back, and why nothing was tried this start where nothing was.
/// </summary>
public sealed record HomePlaced(
    IReadOnlyList<HomeMove> Moved, IReadOnlyList<HomeMove> Failed, IReadOnlyList<HomeMove> Recovered, string? Held)
{
    /// <summary>Nothing to move, nothing left mid-move: a home already in its places, or a new one.</summary>
    public static readonly HomePlaced Nothing = new([], [], [], null);

    /// <summary>Whether this start found anything to do or to say.</summary>
    public bool Any => Moved.Count > 0 || Failed.Count > 0 || Recovered.Count > 0 || Held is not null;
}

/// <summary>
/// An install's home moved once into D172's places (HOMELAYOUT3, the install layout design §7, D172 point 6): at the desktop's
/// start, before anything resolves a path, each entry only Daoris names, by a path it derives at each use, renamed from its old
/// place under the install's <c>data/</c> to its new one. What anything else names by path (an account home, a tree, a room, a
/// kept file, <c>bin/</c>) never moves, and the person's records stay in <c>data/</c>, where they are the same place.
/// </summary>
/// <remarks>
/// <para><b>When.</b> Only with the last update at rest (no <c>update/previous/</c>, no swap under way: D139's roll-back reads
/// only old places, so the first start after a swap moves nothing), with no other process of the folder running (a host adopted
/// from an earlier start, a browser, a connector from <c>bin/</c> may hold a path it took once), and under the home's lease, taken
/// without waiting: a start that finds any of these moves nothing and the next start tries again.</para>
///
/// <para><b>How.</b> A rename on the one volume, never a copy; one that fails leaves its entry where it was, read there by D172
/// point 5, and is tried again at the next start. Nothing is deleted but an empty folder at a new place, which holds no entry. An
/// entry whose new place holds it too is left in both and said: its readers refuse it naming both. A held lease is never moved:
/// <c>home-lease</c> and <c>driver-lock</c> are no entries of the move.</para>
///
/// <para><b>The window's memory moves as one</b>: its two folders into <c>profiles/window.moving/</c>, which takes its name once
/// both are in, or both go back. The move's journal (<see cref="HomeEntry.HomeMove"/>) names each rename before it is made, so a
/// start that stopped between the two folders is put right at the next start, before anything resolves: what went into the
/// staging folder goes back where its old place is free (the staging folder alone says so where the journal was lost).</para>
/// </remarks>
public static class HomePlacing
{
    /// <summary>
    /// The entries D172 moves (the design §3's <i>moves</i>), by name. Not the lease and the older lock (held, never renamed under
    /// their holder), not a session's pid (read in one folder, the sessions' own, which stays), and nothing D172 keeps where it is.
    /// </summary>
    public static IReadOnlyList<string> Moves { get; } =
    [
        "keys", "remotes", "browser-engine", "browser-edge", "edge-cookies", "window",
        "usage-read", "brainstorm-rooms",
        "toolchain", "tool-versions",
        "running", "locks", "spawn", "starts", "hooks", "probing", "requests", "edge-record", "pipe",
        "logs",
    ];

    /// <summary>The window's memory's staging folder, beside its new place.</summary>
    public const string Staging = "window.moving";

    /// <summary>
    /// The move for the install at <paramref name="install"/>, whose home <paramref name="home"/> is (its <c>data/</c>).
    /// </summary>
    /// <param name="othersRunning">The processes of the folder other than this one and its launcher, by id.</param>
    public static HomePlaced Place(string install, string home, Func<string, IReadOnlyList<int>> othersRunning)
    {
        var (folder, _) = DaorisHome.FolderOf(home);
        var journal = JournalOf(home);
        var staging = Path.Combine(folder, "profiles", Staging);
        var mid = File.Exists(journal) || Directory.Exists(staging);
        var failed = new List<HomeMove>();
        List<HomeMove> planned = mid ? [] : Plan(home, failed);
        if (!mid && planned.Count == 0) return failed.Count == 0 ? HomePlaced.Nothing : new([], failed, [], null);

        var atRest = AtRest(install);
        if (!atRest && !mid) return new([], [], [], "the last update has not settled yet (D139): the start after it moves");

        var others = othersRunning(install);
        if (others.Count > 0)
        {
            return new([], [], [], $"another process of this folder runs (pid {string.Join(", ", others)}), which may hold a path it took");
        }

        try
        {
            using var lease = HomeLease.TryAcquire(home, LeaseKind.Migration, out var holder);
            if (lease is null) return new([], [], [], $"{holder!.Named} held this home");

            var unrecovered = new List<HomeMove>();
            var recovered = Recover(home, journal, staging, unrecovered);
            if (!atRest) return new([], unrecovered, recovered, null);

            failed = [.. unrecovered];
            planned = Plan(home, failed);
            var moved = new List<HomeMove>();
            if (planned.Count > 0)
            {
                WriteJournal(journal, planned);
                foreach (var group in planned.GroupBy(move => move.Entry))
                {
                    if (group.Key == HomeEntry.Window.Name)
                    {
                        MoveWindow([.. group], staging, DaorisHome.Places(home, HomeEntry.Window)[0], moved, failed);
                    }
                    else
                    {
                        Move(group.Single(), moved, failed);
                    }
                }
            }

            Forget(journal);
            return new(moved, failed, recovered, null);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return new([], failed, [], $"the move could not start ({error.Message.TrimEnd('.')})");
        }
    }

    /// <summary>
    /// The processes running from under <paramref name="install"/> other than this one and the install's launcher (which has
    /// started this application and is ending): the folder's own, which a move must not reach under.
    /// </summary>
    public static IReadOnlyList<int> OthersRunningFrom(string install)
    {
        var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(install)) + Path.DirectorySeparatorChar;
        var launcher = Path.GetFullPath(Path.Combine(install, StagedBuild.Launcher));
        var found = new List<int>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.Id == Environment.ProcessId) continue;
                    if (process.MainModule?.FileName is not { } file || !file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!string.Equals(Path.GetFullPath(file), launcher, StringComparison.OrdinalIgnoreCase)) found.Add(process.Id);
                }
                catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    // Another account's, or a process that ended as it was asked: not one of the folder's.
                }
            }
        }

        return found;
    }

    /// <summary>The last update at rest (D139): nothing of the build before kept, and no swap moving, started or confirmed.</summary>
    private static bool AtRest(string install) =>
        !Directory.Exists(Path.Combine(install, StagedBuild.Folder, StagedBuild.Previous)) && !StagedBuild.UnderWay(install);

    /// <summary>The move's journal: the home's <see cref="HomeEntry.HomeMove"/>, at its one place.</summary>
    private static string JournalOf(string home) => DaorisHome.Places(home, HomeEntry.HomeMove)[0];

    /// <summary>
    /// Each rename this start would make, in the table's order: an entry D172 moves whose old place is there and whose new place
    /// is not (an empty folder there holds no entry). One in both is said in <paramref name="failed"/> and left.
    /// </summary>
    private static List<HomeMove> Plan(string home, List<HomeMove> failed)
    {
        var planned = new List<HomeMove>();
        foreach (var entry in HomeEntry.Table.Where(entry => Moves.Contains(entry.Name, StringComparer.Ordinal)))
        {
            var places = DaorisHome.Places(home, entry);
            if (places.Count < 2) continue;
            var (to, from) = (places[0], places[1]);
            if (entry.Holds.Length > 0)
            {
                // The window's memory: its names under the old home, moved as one into the new place.
                var names = entry.Holds.Where(name => Path.Exists(Path.Combine(from, name))).ToList();
                if (names.Count == 0) continue;
                if (Holds(to))
                {
                    failed.Add(new(entry.Name, from, to, $"both places hold it, {from} and {to}"));
                    continue;
                }

                // Journalled as the renames they are: into the staging folder beside the new place, which then takes its name.
                var staging = Path.Combine(Path.GetDirectoryName(to)!, Staging);
                planned.AddRange(names.Select(name => new HomeMove(entry.Name, Path.Combine(from, name), Path.Combine(staging, name))));
                continue;
            }

            if (!Path.Exists(from)) continue;
            if (Holds(to))
            {
                failed.Add(new(entry.Name, from, to, $"both places hold it, {from} and {to}"));
                continue;
            }

            planned.Add(new(entry.Name, from, to));
        }

        return planned;
    }

    /// <summary>A place that holds something: a file, or a folder that is not empty.</summary>
    private static bool Holds(string place) =>
        File.Exists(place) || (Directory.Exists(place) && Directory.EnumerateFileSystemEntries(place).Any());

    /// <summary>One rename, file or folder, an empty folder at its new place taken away first; a refusal leaves it where it was.</summary>
    private static void Move(HomeMove move, List<HomeMove> moved, List<HomeMove> failed)
    {
        try
        {
            Rename(move.From, move.To!);
            moved.Add(move);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            failed.Add(move with { Reason = error.Message.TrimEnd('.') });
        }
    }

    private static void Rename(string from, string to)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(to)!);
        if (Directory.Exists(to) && !Directory.EnumerateFileSystemEntries(to).Any()) Directory.Delete(to);
        if (Directory.Exists(from)) Directory.Move(from, to);
        else File.Move(from, to, overwrite: false);
    }

    /// <summary>
    /// The window's memory, as one (the design §7.3): each of its folders into the staging folder, which then takes the new
    /// place's name; a refusal on the way puts back what went in, so the window reads its memory where it was.
    /// </summary>
    private static void MoveWindow(IReadOnlyList<HomeMove> steps, string staging, string final, List<HomeMove> moved, List<HomeMove> failed)
    {
        var done = new List<HomeMove>();
        try
        {
            Directory.CreateDirectory(staging);
            foreach (var step in steps)
            {
                Directory.Move(step.From, step.To!);
                done.Add(step);
            }

            if (Directory.Exists(final) && !Directory.EnumerateFileSystemEntries(final).Any()) Directory.Delete(final);
            Directory.Move(staging, final);
            moved.Add(new(HomeEntry.Window.Name, Path.GetDirectoryName(steps[0].From)!, final));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            foreach (var step in Enumerable.Reverse(done))
            {
                try { Directory.Move(step.To!, step.From); }
                catch (Exception back) when (back is IOException or UnauthorizedAccessException) { /* the next start's recovery puts it back */ }
            }

            RemoveIfEmpty(staging);
            failed.Add(new(HomeEntry.Window.Name, Path.GetDirectoryName(steps[0].From)!, final, error.Message.TrimEnd('.')));
        }
    }

    /// <summary>
    /// A start that stopped mid-move, put right before anything resolves: each rename the journal names into the window's staging
    /// folder put back where its old place is free; without a journal, what the staging folder holds of the window's names. What
    /// cannot go back stays in the staging folder and is said. The journal is then forgotten.
    /// </summary>
    private static List<HomeMove> Recover(string home, string journal, string staging, List<HomeMove> failed)
    {
        var recovered = new List<HomeMove>();
        if (Directory.Exists(staging))
        {
            var (_, oldHome) = DaorisHome.FolderOf(home);
            var steps = ReadJournal(journal)
                .Where(step => step.To is { } to && Inside(to, staging))
                .ToList();
            if (steps.Count == 0)
            {
                steps = [.. Directory.EnumerateDirectories(staging)
                    .Where(child => HomeEntry.Window.Holds.Contains(Path.GetFileName(child), StringComparer.OrdinalIgnoreCase))
                    .Select(child => new HomeMove(HomeEntry.Window.Name, Path.Combine(oldHome, Path.GetFileName(child)), child))];
            }

            foreach (var step in steps.Where(step => Directory.Exists(step.To)))
            {
                if (Path.Exists(step.From))
                {
                    failed.Add(step with { Reason = $"left in {step.To}: its old place was made again since a start stopped mid-move" });
                    continue;
                }

                try
                {
                    Directory.Move(step.To!, step.From);
                    recovered.Add(new(step.Entry, step.To!, step.From));
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    failed.Add(step with { Reason = error.Message.TrimEnd('.') });
                }
            }

            RemoveIfEmpty(staging);
        }

        Forget(journal);
        return recovered;
    }

    private static bool Inside(string path, string folder) =>
        Path.GetFullPath(path).StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);

    private static void RemoveIfEmpty(string folder)
    {
        try
        {
            if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // An empty staging folder left is the next start's to remove.
        }
    }

    /// <summary>The journal, written whole before the first rename: each rename this start is about to make.</summary>
    private static void WriteJournal(string journal, IReadOnlyList<HomeMove> planned)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(journal)!);
        var root = new JsonObject
        {
            ["moves"] = new JsonArray([.. planned.Select(move => (JsonNode)new JsonObject
            {
                ["entry"] = move.Entry, ["from"] = move.From, ["to"] = move.To,
            })]),
        };
        AtomicFile.WriteText(journal, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n") + "\n");
    }

    /// <summary>The journal's renames; none where there is none, or it does not read.</summary>
    private static IReadOnlyList<HomeMove> ReadJournal(string journal)
    {
        try
        {
            if (!File.Exists(journal) || JsonNode.Parse(File.ReadAllText(journal))?["moves"] is not JsonArray moves) return [];
            return [.. moves.OfType<JsonObject>()
                .Where(move => move["entry"] is JsonValue && move["from"] is JsonValue && move["to"] is JsonValue)
                .Select(move => new HomeMove(
                    move["entry"]!.GetValue<string>(), move["from"]!.GetValue<string>(), move["to"]!.GetValue<string>()))];
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException
                                          or FormatException)
        {
            return [];
        }
    }

    private static void Forget(string journal)
    {
        try
        {
            if (File.Exists(journal)) File.Delete(journal);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A journal left names renames already made or put back; the next start reads it to nothing.
        }
    }
}

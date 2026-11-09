using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>What the library and git each read for one repository: the answers it gave, the reads that went to git and why.</summary>
/// <param name="Repository">The repository's folder name, never its path.</param>
/// <param name="Library">The questions the library answered.</param>
/// <param name="Git">The questions that fell back to git's binary: the sum of <paramref name="Reasons"/>.</param>
/// <param name="Reasons">Each reason a question went to git (<see cref="GitLibraryAdmission"/>'s words, or the journal's own) and how often.</param>
internal sealed record GitLibraryRow(string Repository, int Library, int Git, IReadOnlyDictionary<string, int> Reasons, DateTimeOffset Last);

/// <summary>
/// What the library did for each repository, kept in the home's <c>run/git/library.json</c> where the program has one: every refusal
/// its admission makes and every read that fell back to git's binary with its reason, and every answer it gave. The Git page lists it.
/// </summary>
/// <remarks>
/// <para><b>Why a tally, not a line a question.</b> A reftable repository is refused at every question, and a review asks dozens; the
/// journal keeps each reason's count and the last time, which is what the page says. A reason new to a repository is written at once,
/// so a fault the process's end would lose is not; counts follow at most every few seconds.</para>
/// <para><b>Machine-local.</b> A repository is its checkout's git directory in the file and its folder's name everywhere it is said.</para>
/// </remarks>
public static class GitLibraryJournal
{
    /// <summary>A question the library declined: a walk's criss-cross base, a revision or a path it leaves to git.</summary>
    internal const string Unanswered = "unanswered";

    /// <summary>The library threw at its open or in its call: an unreadable pack, a folder another account owns, SHA-256.</summary>
    internal const string Failed = "failed";

    /// <summary>No native library lies beside the driver, so every question is git's.</summary>
    internal const string Native = "native-missing";

    /// <summary>A process ended inside a library call here (<see cref="GitLibraryFaults"/>), so git answers while the package is the one that faulted.</summary>
    internal const string NativeFault = "native-fault";

    private static readonly TimeSpan SavedAtMost = TimeSpan.FromSeconds(5);
    private static readonly Lock Gate = new();
    private static readonly Dictionary<string, Entry> Entries = new(StringComparer.OrdinalIgnoreCase);
    private static string? s_home;
    private static DateTime s_saved = DateTime.MinValue;

    /// <summary>One answer the library gave for <paramref name="folder"/>'s repository.</summary>
    internal static void Answered(string folder) => Record(folder, null);

    /// <summary>One question for <paramref name="folder"/>'s repository that git answered instead, and why.</summary>
    internal static void Fell(string folder, string reason) => Record(folder, reason);

    /// <summary>Journals that a process ended inside a call over <paramref name="folder"/>'s repository under <paramref name="package"/>, written at once.</summary>
    internal static void Faulted(string folder, string package)
    {
        var key = KeyOf(folder);
        lock (Gate)
        {
            Join(ProgramHome.Named);
            if (!Entries.TryGetValue(key, out var entry)) Entries[key] = entry = new Entry(NameOf(key));
            entry.Fault = package;
            entry.Add(NativeFault);
            if (s_home is not null) Save(s_home);
        }
    }

    /// <summary>The package a process ended inside a call of <paramref name="folder"/>'s repository under; null where none did.</summary>
    internal static string? FaultOf(string folder)
    {
        lock (Gate)
        {
            Join(ProgramHome.Named);
            return Entries.TryGetValue(KeyOf(folder), out var entry) ? entry.Fault : null;
        }
    }

    /// <summary>What the journal holds for <paramref name="folder"/>'s repository; null where it has asked nothing.</summary>
    internal static GitLibraryRow? Of(string folder)
    {
        lock (Gate) return Entries.TryGetValue(KeyOf(folder), out var entry) ? entry.Row() : null;
    }

    /// <summary>Every repository the journal holds, most recently read first.</summary>
    internal static IReadOnlyList<GitLibraryRow> Read()
    {
        lock (Gate) return [.. Entries.Values.Select(entry => entry.Row()).OrderByDescending(row => row.Last).ThenBy(row => row.Repository, StringComparer.Ordinal)];
    }

    /// <summary>
    /// The Git page's listing: each repository by its folder's name with the answers the library gave, the reads that went to git and
    /// each reason, as an array so no name is a key; never a path.
    /// </summary>
    public static object Page() => Read().Select(each => new
    {
        each.Repository, each.Library, each.Git,
        Reasons = each.Reasons.OrderBy(reason => reason.Key, StringComparer.Ordinal).Select(reason => new { Reason = reason.Key, Count = reason.Value }).ToArray(),
        Last = Moments.Write(each.Last),
    }).ToArray();

    /// <summary>What <paramref name="home"/>'s file holds, as an earlier process kept it; none where it holds none or does not read.</summary>
    internal static IReadOnlyList<GitLibraryRow> Loaded(string home) => [.. Load(home).Values.Select(entry => entry.Row())];

    private static void Record(string folder, string? reason)
    {
        var key = KeyOf(folder);
        lock (Gate)
        {
            Join(ProgramHome.Named);
            if (!Entries.TryGetValue(key, out var entry)) Entries[key] = entry = new Entry(NameOf(key));
            var fresh = entry.Add(reason);
            if (s_home is null) return;
            if (fresh || DateTime.UtcNow - s_saved >= SavedAtMost) Save(s_home);
        }
    }

    /// <summary>The first time the program names a home, what an earlier process kept there joins what this one already counted.</summary>
    private static void Join(string? home)
    {
        if (home is null || string.Equals(home, s_home, StringComparison.Ordinal)) return;
        s_home = home;
        foreach (var (key, kept) in Load(home))
        {
            if (Entries.TryGetValue(key, out var held)) held.Join(kept);
            else Entries[key] = kept;
        }
    }

    private static string KeyOf(string folder) => RefFiles.CommonDirOf(folder) ?? Path.GetFullPath(folder);

    private static string NameOf(string key)
    {
        var trimmed = key.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var leaf = Path.GetFileName(trimmed);
        return leaf == ".git" ? Path.GetFileName(Path.GetDirectoryName(trimmed)) ?? leaf : leaf;
    }

    private static Dictionary<string, Entry> Load(string home)
    {
        var kept = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        var path = DaorisHome.Entry(home, HomeEntry.GitLibrary);
        if (!File.Exists(path)) return kept;
        try
        {
            if (JsonNode.Parse(AtomicFile.ReadText(path))?["repositories"] is not JsonArray list) return kept;
            foreach (var each in list.OfType<JsonObject>())
            {
                if (each["key"]?.GetValue<string>() is not { Length: > 0 } key) continue;
                var entry = new Entry(NameOf(key)) { Library = each["library"]?.GetValue<int>() ?? 0, Fault = each["fault"]?.GetValue<string>() };
                if (each["reasons"] is JsonObject reasons)
                {
                    foreach (var (reason, count) in reasons) entry.Reasons[reason] = count?.GetValue<int>() ?? 0;
                }

                if (DateTimeOffset.TryParse(each["last"]?.GetValue<string>(), out var last)) entry.Last = last;
                kept[key] = entry;
            }
        }
        catch (Exception error) when (error is IOException or System.Text.Json.JsonException or UnauthorizedAccessException or InvalidOperationException or FormatException)
        {
            // An unreadable journal holds nothing: the tally begins again, which loses a count and never a refusal's reason.
        }

        return kept;
    }

    private static void Save(string home)
    {
        s_saved = DateTime.UtcNow;
        var list = new JsonArray();
        foreach (var (key, entry) in Entries)
        {
            var reasons = new JsonObject();
            foreach (var (reason, count) in entry.Reasons) reasons[reason] = count;
            list.Add(new JsonObject { ["key"] = key, ["library"] = entry.Library, ["reasons"] = reasons, ["last"] = entry.Last.ToString("O"), ["fault"] = entry.Fault });
        }

        try
        {
            JsonFile.Write(DaorisHome.Entry(home, HomeEntry.GitLibrary), new JsonObject { ["repositories"] = list });
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A journal that cannot be written is the process's own count alone: no read waits on it.
        }
    }

    private sealed class Entry(string name)
    {
        internal int Library { get; set; }

        internal Dictionary<string, int> Reasons { get; } = new(StringComparer.Ordinal);

        internal DateTimeOffset Last { get; set; } = DateTimeOffset.MinValue;

        /// <summary>The package a process ended inside a call of this repository under, or null.</summary>
        internal string? Fault { get; set; }

        /// <summary>Counts one read; true where it is a reason this repository had none of.</summary>
        internal bool Add(string? reason)
        {
            Last = DateTimeOffset.UtcNow;
            if (reason is null)
            {
                Library++;
                return false;
            }

            var fresh = !Reasons.ContainsKey(reason);
            Reasons[reason] = Reasons.GetValueOrDefault(reason) + 1;
            return fresh;
        }

        internal void Join(Entry kept)
        {
            Library += kept.Library;
            foreach (var (reason, count) in kept.Reasons) Reasons[reason] = Reasons.GetValueOrDefault(reason) + count;
            if (kept.Last > Last) Last = kept.Last;
            Fault ??= kept.Fault;
        }

        internal GitLibraryRow Row() => new(name, Library, Reasons.Values.Sum(), new Dictionary<string, int>(Reasons), Last);
    }
}

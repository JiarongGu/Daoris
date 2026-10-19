using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>A working directory in which Daoris's own records show a session ran (OWNSPACE2), and what keeps its folder from a removal.</summary>
/// <param name="Folder">The working directory, as the record holds it.</param>
/// <param name="Source"><c>tree</c>, <c>help</c> or <c>intake</c>.</param>
/// <param name="Keeps">Null, or why the folder it made must stay: <c>session</c> (live or waiting), <c>resumable</c> (a legacy conversation that goes on in the person's own home), <c>record-unread</c> (its conversation record is there and cannot be read).</param>
/// <param name="By">The session record that names it.</param>
public sealed record ToolOrigin(string Folder, string Source, string? Keeps, string By = "");

/// <summary>A project folder in the person's tool home that Daoris's records show a session of its ran in: its name, its place, and the records that name it.</summary>
public sealed record ToolFolder(string Name, string Path, string Source, string? Keeps, IReadOnlyList<string> By);

/// <summary>What was listed, and why not where nothing could be: <c>Why</c> is a code (<c>linked</c>, <c>relative</c>, <c>inside-daoris</c>, <c>no-tool-home</c>, <c>no-home</c>, <c>records-unread</c>).</summary>
/// <param name="Ambiguous">Folders present whose name two distinct working directories flatten to: listed for neither.</param>
/// <param name="Uncertain">Folders present whose only claimant's path flattens in a way not proven (a non-ASCII letter, an over-long name).</param>
public sealed record ToolListing(IReadOnlyList<ToolFolder> Folders, string? Why, bool CaseInsensitive, int Ambiguous, int Uncertain);

/// <summary>One folder measured: its bytes, its newest file's time, and whether any read failed (so the numbers are not whole).</summary>
public sealed record ToolMeasure(long Bytes, DateTimeOffset? Newest, bool Partial);

/// <summary>
/// What Daoris's sessions left in the person's own tool home (OWNSPACE2, D175 point 3): the project folders Claude Code keyed
/// under <c>projects/</c> by the working directory of a session Daoris ran, LISTED as Daoris's with the record that names each.
/// It removes nothing: the press is its own hardened row (OWNSPACE2e), so a person removes a folder by hand.
/// </summary>
/// <remarks>
/// <para><b>Ownership is provenance, never a pattern.</b> Claude Code names a project folder by the working directory with every
/// character that is not a letter or a digit turned into <c>-</c> (<see cref="Flatten"/>), which loses what the character was. So a
/// folder is Daoris's only where its name EQUALS the flattening of a working directory in which one of Daoris's own session
/// records shows a session ran (<see cref="Origins"/>), inside Daoris's own working roots. A room nobody's record shows a
/// session in claims nothing (a person's <c>daoris-help</c> folder is not the help room's). No prefix is ever matched. Two
/// distinct recorded paths flattening alike, those outside Daoris's roots included, are listed for neither, and a path not proven
/// to flatten as Claude Code does (a non-ASCII letter, a name past 200 characters, a relative path) is not Daoris's.</para>
///
/// <para><b>What may still be used is said.</b> A folder whose record is live or waiting, or whose record began in the tool's own
/// home and has a conversation kept to resume or one that cannot be read, is shown with why it would stay.</para>
///
/// <para><b>Codex is not listed.</b> Its home files a session by the date it ran, and the folder it ran in is said only inside
/// the session's file, which this never reads.</para>
/// </remarks>
public static class ToolHomeLeftovers
{
    /// <summary>The variable naming Claude Code's configuration home: the person's own, where they set one.</summary>
    public const string Variable = "CLAUDE_CONFIG_DIR";

    /// <summary>Claude Code names a folder past this many characters with a hash of its own, which is not proven here.</summary>
    private const int LongestName = 200;

    private static readonly Regex Other = new("[^a-zA-Z0-9]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>A path as Claude Code names its project folder: every character but a letter or a digit becomes <c>-</c>.</summary>
    public static string Flatten(string path) => Other.Replace(path, "-");

    // ---------------------------------------------------------------------------------------------------------------------
    // The person's tool home

    /// <summary>
    /// The person's own tool home, where it was read from and why it cannot be used. <c>process</c> where they set
    /// <see cref="Variable"/> in this process's environment, <c>account</c> where only the account's stored variable names it,
    /// else <c>default</c>, the <c>.claude</c> folder of their profile. A variable that is relative, or that names Daoris's own
    /// folder or a place inside it (a Daoris profile, never the person's), is no tool home: its path is null with the code.
    /// </summary>
    /// <param name="daorisHome">Daoris's own home, which a tool home must not be or be inside; null where none.</param>
    public static (string? Path, string From, string? Why) OwnHome(
        Func<string, string?> process, Func<string, string?>? account, string profile, string? daorisHome)
    {
        foreach (var (read, from) in new[] { (process, "process"), (account, "account") })
        {
            if (read?.Invoke(Variable) is not { } set || string.IsNullOrWhiteSpace(set)) continue;
            var named = set.Trim();
            if (!System.IO.Path.IsPathFullyQualified(named)) return (null, from, "relative");
            var full = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(named));
            foreach (var daoris in new[] { daorisHome, SystemWiring.RootOf(daorisHome) }.Where(each => each is not null).Distinct())
            {
                var top = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(daoris!));
                if (string.Equals(full, top, StringComparison.OrdinalIgnoreCase) || IsUnder(full, top, StringComparer.OrdinalIgnoreCase)) return (null, from, "inside-daoris");
            }

            return (full, from, null);
        }

        return (System.IO.Path.Combine(profile, ".claude"), "default", null);
    }

    /// <summary>The own home as a real program reads it: this process's environment, the account's, the user profile.</summary>
    public static (string? Path, string From, string? Why) OwnHome(WiringWorld world) =>
        OwnHome(world.Process, world.Account, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), world.Home);

    /// <summary>
    /// The tool home a world reads: the one it was handed, or none. 🔴 A world made by hand names none, so a test that builds one
    /// reads no machine's tool home; the window's and the terminal's worlds are handed theirs (<see cref="OwnHome(WiringWorld)"/>).
    /// </summary>
    public static (string? Path, string From, string? Why) ToolHomeOf(WiringWorld world) => (world.ToolHome, world.ToolHomeFrom, world.ToolHomeWhy);

    /// <summary>The folder Claude Code keeps its projects in, in the person's tool home.</summary>
    public static string? ProjectsOf(WiringWorld world) => world.ToolHome is { } tool ? System.IO.Path.Combine(tool, "projects") : null;

    // ---------------------------------------------------------------------------------------------------------------------
    // Provenance

    /// <summary>
    /// The working directories in which Daoris's own session records (<paramref name="recordsJson"/>, the service's) show a
    /// session ran: each record's tree, the help room for a record of Ask Daoris's, and an intake's room for a record of an
    /// intake. A room no record shows a session in claims nothing. Each carries what keeps it. Null where the records could not
    /// be read (none given, or not JSON): with no record of use there is no claim.
    /// </summary>
    public static IReadOnlyList<ToolOrigin>? Origins(string home, string? recordsJson)
    {
        if (recordsJson is null) return null;
        var origins = new List<ToolOrigin>();
        try
        {
            using var document = JsonDocument.Parse(recordsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array) return null;
            foreach (var record in document.RootElement.EnumerateArray())
            {
                if (record.ValueKind != JsonValueKind.Object || Text(record, "id") is not { Length: > 0 } id || id.Contains('/')) continue;
                var live = new SessionRecord(id, "", Text(record, "state") ?? "").Live;
                var keeps = live ? "session"
                    : string.IsNullOrWhiteSpace(Text(record, "profile")) ? ConversationKeeps(home, id)
                    : null;
                if (Text(record, "tree") is { Length: > 0 } tree) origins.Add(new ToolOrigin(tree, "tree", keeps, id));
                if (Text(record, "repository") == "daoris:help") origins.Add(new ToolOrigin(HelpRoom.PathOf(home, Text(record, "workspace")), "help", keeps, id));
                if (Text(record, "ask") is { Length: > 0 })
                {
                    origins.Add(new ToolOrigin(IntakeRoom.PathOf(home, RemoteTarget.Workspace(Text(record, "workspace"))), "intake", keeps, id));
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return origins;
    }

    /// <summary>
    /// What a record that began in the person's own home keeps its folder for: <c>resumable</c> where a conversation is kept to
    /// resume, <c>record-unread</c> where one is there and cannot be read (an access failure or invalid JSON is not an absent
    /// record: <see cref="HarnessConversations.Read"/> answers null for both, so this reads the file's presence itself), null
    /// where there is none. Other callers of the store keep their behaviour.
    /// </summary>
    private static string? ConversationKeeps(string home, string session)
    {
        var conversations = new HarnessConversations(home);
        if (!SessionEventText.IsLocalId(session)) return null;
        try
        {
            using var _ = File.OpenRead(conversations.PathOf(session));
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return "record-unread";
        }

        return conversations.Read(session) is not null ? "resumable" : "record-unread";
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>Daoris's working roots in a home: each <c>work</c> entry's folder where it holds its items, new place and old.</summary>
    public static IReadOnlyList<string> Roots(string home) =>
        [.. HomeEntry.Table.Where(entry => entry.Category == HomeCategory.Work)
            .SelectMany(entry => DaorisHome.Places(home, entry))
            .Select(place => System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(place)))
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    // ---------------------------------------------------------------------------------------------------------------------
    // The list

    /// <summary>
    /// The project folders Daoris's records show its sessions ran in, by name, with the records that name each. Read from the
    /// folder's listing alone, nothing opened. A folder reached through a link is never listed: where any ancestor of the projects
    /// folder is a link, nothing is, and <c>Why</c> is <c>linked</c>. Where the records were not read there is no claim, and
    /// <c>Why</c> is <c>records-unread</c>.
    /// </summary>
    public static ToolListing List(WiringWorld world)
    {
        if (world.Home is not { } home) return new ToolListing([], "no-home", false, 0, 0);
        if (world.ToolHome is not { } tool) return new ToolListing([], world.ToolHomeWhy ?? "no-tool-home", false, 0, 0);
        var projects = System.IO.Path.Combine(tool, "projects");
        if (!Directory.Exists(projects)) return new ToolListing([], null, false, 0, 0);
        if (LinkOnChain(projects) is not null) return new ToolListing([], "linked", false, 0, 0);
        if (world.ToolOrigins is not { } origins) return new ToolListing([], "records-unread", false, 0, 0);

        var insensitive = CaseInsensitive(projects);
        var comparer = insensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        List<DirectoryInfo> present;
        try
        {
            present = [.. new DirectoryInfo(projects).EnumerateDirectories().Where(folder => !folder.Attributes.HasFlag(FileAttributes.ReparsePoint))];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return new ToolListing([], "unread", insensitive, 0, 0);
        }

        var names = present.Select(folder => folder.Name).ToHashSet(comparer);
        var roots = Roots(home);
        var claims = new Dictionary<string, List<(string Full, ToolOrigin Origin, bool Sure, bool Ours)>>(comparer);
        foreach (var origin in origins)
        {
            if (!System.IO.Path.IsPathFullyQualified(origin.Folder)) continue;
            var full = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(origin.Folder));
            // Every recorded path takes part in the collision check, those outside Daoris's working roots included: a record
            // naming the person's own checkout can flatten to the name of one of ours, and then neither is provable.
            var ours = roots.Any(root => comparer.Equals(full, root) || IsUnder(full, root, comparer));
            var flat = Flatten(full);
            var sure = full.All(letter => letter < 128) && flat.Length <= LongestName;
            if (!claims.TryGetValue(flat, out var group)) claims[flat] = group = [];
            group.Add((full, origin, sure, ours));
        }

        var folders = new List<ToolFolder>();
        var ambiguous = 0;
        var uncertain = 0;
        foreach (var (flat, group) in claims.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!names.Contains(flat)) continue;
            // Paths spelled apart are two claims even where this folder's file system would fold them: never merged.
            if (group.Select(claim => claim.Full).Distinct(StringComparer.Ordinal).Count() > 1)
            {
                ambiguous++;
                continue;
            }

            // One path, outside Daoris's roots: a person's own, and nothing of ours.
            if (group.Any(claim => !claim.Ours)) continue;
            if (group.Any(claim => !claim.Sure))
            {
                uncertain++;
                continue;
            }

            var folder = present.First(each => comparer.Equals(each.Name, flat));
            var keeps = group.Select(claim => claim.Origin.Keeps).FirstOrDefault(why => why == "session")
                ?? group.Select(claim => claim.Origin.Keeps).FirstOrDefault(why => why == "resumable")
                ?? group.Select(claim => claim.Origin.Keeps).FirstOrDefault(why => why is not null);
            folders.Add(new ToolFolder(
                folder.Name, folder.FullName, group[0].Origin.Source, keeps,
                [.. group.Select(claim => claim.Origin.By).Where(by => by.Length > 0).Distinct(StringComparer.Ordinal)]));
        }

        return new ToolListing(folders, null, insensitive, ambiguous, uncertain);
    }

    /// <summary>Whether a folder's own file system folds case, read from it: the folder's name with its case swapped is the same folder.</summary>
    private static bool CaseInsensitive(string folder)
    {
        var name = System.IO.Path.GetFileName(folder);
        var swapped = new string([.. name.Select(letter => char.IsUpper(letter) ? char.ToLowerInvariant(letter) : char.ToUpperInvariant(letter))]);
        if (swapped == name) return OperatingSystem.IsWindows();
        return Directory.Exists(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(folder)!, swapped));
    }

    private static bool IsUnder(string path, string root, StringComparer comparer)
    {
        var top = System.IO.Path.TrimEndingDirectorySeparator(root);
        return path.Length > top.Length && comparer.Equals(path[..top.Length], top) && path[top.Length] == System.IO.Path.DirectorySeparatorChar;
    }

    /// <summary>The first link on the way from <paramref name="path"/> up to the root, the path itself included; null where there is none.</summary>
    internal static string? LinkOnChain(string path)
    {
        for (var at = new DirectoryInfo(System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(path))); at is not null; at = at.Parent)
        {
            try
            {
                if (at.Exists && at.Attributes.HasFlag(FileAttributes.ReparsePoint)) return at.FullName;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                return at.FullName;
            }
        }

        return null;
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // Size

    /// <summary>Each folder's size and newest file through the existing measuring (<see cref="WiringSizes.MeasureFolder"/>), by name.</summary>
    public static IReadOnlyDictionary<string, ToolMeasure> Measure(IReadOnlyList<ToolFolder> folders, WiringBudget budget, CancellationToken cancellationToken)
    {
        var measured = new Dictionary<string, ToolMeasure>(StringComparer.Ordinal);
        foreach (var folder in folders)
        {
            if (cancellationToken.IsCancellationRequested) break;
            var (bytes, newest, partial) = WiringSizes.MeasureFolder(folder.Path, budget, cancellationToken);
            measured[folder.Name] = new ToolMeasure(bytes, newest, partial);
        }

        return measured;
    }
}

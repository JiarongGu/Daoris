using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>A project folder in the person's tool home that Daoris's sessions made: its name there and its place.</summary>
public sealed record ToolFolder(string Name, string Path);

/// <summary>One such folder measured: its bytes, its newest file's time, and whether the walk stopped short.</summary>
public sealed record ToolMeasure(long Bytes, DateTimeOffset? Newest, bool Partial);

/// <summary>A folder the press removed, with the bytes it held.</summary>
public sealed record ToolCleared(string Name, long Bytes);

/// <summary>A folder the press kept, and why: <c>not-listed</c> (it no longer matches, or never did) or <c>in-use</c> (written to lately) or <c>not-removed</c> (the disk refused).</summary>
public sealed record ToolKept(string Name, string Why);

/// <summary>What a press did.</summary>
public sealed record ToolClear(IReadOnlyList<ToolCleared> Cleared, IReadOnlyList<ToolKept> Kept);

/// <summary>
/// What Daoris's sessions left in the person's own tool home (OWNSPACE2, D175 point 3; D88's rule for what Daoris made): the
/// project folders Claude Code keyed under <c>projects/</c> by the working directory of a session Daoris ran, listed as Daoris's
/// and removed only by the person's press after the list.
/// </summary>
/// <remarks>
/// <para><b>The match is by Daoris's own working roots, never by a guess.</b> Claude Code names a project folder by the working
/// directory's path with every character that is not a letter or a digit turned into <c>-</c> (<see cref="Flatten"/>). A folder
/// is Daoris's where its name is one of Daoris's working roots flattened, or begins with it and a <c>-</c>: the roots are the
/// places of every entry of D172's table in the <c>work</c> category (its trees, its help room, its intakes, its packets), as
/// <see cref="DaorisHome.Places"/> gives them, new place and old. Any other folder is the person's and is never listed.</para>
///
/// <para><b>Codex is not listed.</b> Its home files a session under the date it ran, and which folder it ran in is said only
/// inside the session's file, which this never reads (D175: sizes and times alone). Daoris hands Codex its own home besides
/// (D175 point 1), so a session it ran leaves nothing under the person's.</para>
///
/// <para><b>In use is judged by the disk.</b> A folder a file in which was written to within <see cref="Quiet"/> may have a
/// session running in it, and stays; the folders Daoris's running sessions work in are not named to this reader.</para>
/// </remarks>
public static class ToolHomeLeftovers
{
    /// <summary>The variable naming Claude Code's configuration home: the person's own, where they set one.</summary>
    public const string Variable = "CLAUDE_CONFIG_DIR";

    /// <summary>How lately a folder was written to for a session to be taken as running in it.</summary>
    public static readonly TimeSpan Quiet = TimeSpan.FromMinutes(10);

    private static readonly Regex Other = new("[^a-zA-Z0-9]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>A path as Claude Code names its project folder: every character but a letter or a digit becomes <c>-</c>.</summary>
    public static string Flatten(string path) => Other.Replace(path, "-");

    /// <summary>
    /// The person's own tool home and where it was read from: <c>variable</c> where they set <see cref="Variable"/> in their
    /// environment (this process's, else the account's), else <c>default</c>, the <c>.claude</c> folder of their profile, as
    /// every start that runs in the tool's own home reads it (<see cref="ClaudeTrust.PathFor"/>). A world naming one is read as named.
    /// </summary>
    public static (string Path, string From) ToolHomeOf(WiringWorld world)
    {
        if (world.ToolHome is { } named) return (named, "named");
        foreach (var read in new[] { world.Process, world.Account })
        {
            if (read?.Invoke(Variable) is { } set && !string.IsNullOrWhiteSpace(set)) return (set.Trim(), "variable");
        }

        return (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude"), "default");
    }

    /// <summary>Daoris's working roots in a home: each <c>work</c> entry's folder where it holds its items, new place and old.</summary>
    public static IReadOnlyList<string> Roots(string home) =>
        [.. HomeEntry.Table.Where(entry => entry.Category == HomeCategory.Work)
            .SelectMany(entry => DaorisHome.Places(home, entry))
            .Select(place => Path.TrimEndingDirectorySeparator(Path.GetFullPath(place)))
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>Whether a project folder's name is a working root of Daoris's, flattened, or one of its descendants.</summary>
    public static bool Matches(string name, IReadOnlyList<string> roots) =>
        roots.Select(Flatten).Any(flat =>
            name.Equals(flat, StringComparison.OrdinalIgnoreCase) || name.StartsWith(flat + "-", StringComparison.OrdinalIgnoreCase));

    /// <summary>The folder Claude Code keeps its projects in, in the person's tool home.</summary>
    public static string ProjectsOf(WiringWorld world) => Path.Combine(ToolHomeOf(world).Path, "projects");

    /// <summary>
    /// The project folders that are Daoris's, by name: read from the folder's listing alone, nothing opened. A link is never
    /// listed, since it is somewhere else. Empty where there is no home, no tool home or no projects folder.
    /// </summary>
    public static IReadOnlyList<ToolFolder> List(WiringWorld world)
    {
        if (world.Home is not { } home) return [];
        var projects = ProjectsOf(world);
        if (!Directory.Exists(projects)) return [];
        var roots = Roots(home);
        try
        {
            return
            [
                .. new DirectoryInfo(projects).EnumerateDirectories()
                    .Where(folder => !folder.Attributes.HasFlag(FileAttributes.ReparsePoint) && Matches(folder.Name, roots))
                    .OrderBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(folder => new ToolFolder(folder.Name, folder.FullName)),
            ];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

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

    /// <summary>
    /// The person's press: remove exactly the folders <paramref name="names"/> names that are still Daoris's, each judged again
    /// right now. A name not in the list now (it stopped matching, is gone, is a link, or is no single folder name) stays,
    /// <c>not-listed</c>; one written to within <see cref="Quiet"/> of <paramref name="now"/> stays, <c>in-use</c>.
    /// </summary>
    public static ToolClear Clear(WiringWorld world, IReadOnlyList<string> names, DateTimeOffset now)
    {
        var listed = List(world).ToDictionary(folder => folder.Name, StringComparer.OrdinalIgnoreCase);
        var cleared = new List<ToolCleared>();
        var kept = new List<ToolKept>();
        foreach (var name in names.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (name.Length == 0 || Path.GetFileName(name) != name || !listed.TryGetValue(name, out var folder))
            {
                kept.Add(new ToolKept(name, "not-listed"));
                continue;
            }

            var (bytes, newest, partial) = WiringSizes.MeasureFolder(folder.Path, WiringBudget.Default, CancellationToken.None);
            var wrote = newest is { } at && at > now - Quiet
                || Directory.GetLastWriteTimeUtc(folder.Path) > (now - Quiet).UtcDateTime;
            if (wrote || partial)
            {
                kept.Add(new ToolKept(folder.Name, "in-use"));
                continue;
            }

            try
            {
                Directory.Delete(folder.Path, recursive: true);
                cleared.Add(new ToolCleared(folder.Name, bytes));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                kept.Add(new ToolKept(folder.Name, "not-removed"));
            }
        }

        return new ToolClear(cleared, kept);
    }
}

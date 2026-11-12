namespace Daoris.Driver;

/// <summary>
/// A repository's line as far as a room is concerned, read as git objects at its tip and never from the working files: what its declaration
/// holds today, and what stands at the path asked. <paramref name="Problem"/> is why the line could not be read, and nothing else is then said.
/// </summary>
/// <param name="Line">The line, and its tip <paramref name="Commit"/>.</param>
/// <param name="Adopted">Whether the line holds <c>daoris.json</c>: the doctrine tool has something to <c>sync</c> and <c>check</c> there.</param>
/// <param name="Declared"><c>daoris.json</c>'s <c>rooms</c>, as the manifest spells each: forward slashes, no <c>./</c>, no trailing slash.</param>
/// <param name="Lanes">The rooms the lanes file declares (<c>LaneRooms</c>): a lane's <c>room</c>, or its own id where it carries <c>builds</c>.</param>
/// <param name="Folders">The folders holding an <c>AGENTS.md</c> of their own, outside the doctrine's folders.</param>
/// <param name="Through">The first part of the path that is a link or a submodule's commit on the line, or a file where more path follows it; null where none is.</param>
/// <param name="IsFile">Whether the path itself is a tracked file.</param>
public sealed record PlaceRoomRead(
    string? Line, string? Commit, bool Adopted, IReadOnlyList<string> Declared, IReadOnlyList<string> Lanes, IReadOnlyList<string> Folders,
    string? Through, bool IsFile, string? Problem = null)
{
    /// <summary>A line that could not be read, and why.</summary>
    public static PlaceRoomRead Unread(string problem) => new(null, null, false, [], [], [], null, false, problem);
}

/// <summary>The codes a room is refused by, beside the place plan's own (<see cref="SetupRefusals"/>, <see cref="PlaceApplyRefusals"/>); each says its door.</summary>
public static class PlaceRoomRefusals
{
    /// <summary>A room asked with any other part: it is a quest to an adopted repository, and the other parts change configuration.</summary>
    public const string Alone = "room-alone";

    /// <summary>A room asked of more than one repository, or of none.</summary>
    public const string One = "room-one";

    /// <summary>A room with no reason, one too long, or a reason with no room.</summary>
    public const string Purpose = "room-purpose";

    /// <summary>A path that is not a folder inside the repository: empty, outside it, reserved, through a link, or a file.</summary>
    public const string Path = "room-path";

    /// <summary>A room the repository already declares or holds at that path.</summary>
    public const string Exists = "room-exists";

    /// <summary>A held repository, which starts nothing new.</summary>
    public const string Held = "held";

    /// <summary>The growth switch is off for the repository, so no room is asked of it.</summary>
    public const string Growth = "growth-off";
}

/// <summary>
/// A room's path as the repository keeps one: forward slashes, no leading or trailing slash, no <c>./</c> and no empty part. What leaves the
/// repository or cannot be a folder in every checkout is refused with the reason, never repaired: a path with <c>..</c>, one rooted or on a drive,
/// one with a character Windows refuses in a name, or one inside <c>.git</c> or the doctrine's own folders.
/// </summary>
public static class PlaceRoomPath
{
    /// <summary>Characters no checkout may hold in a folder's name on every system the repository is cloned to.</summary>
    private const string Reserved = "<>:\"|?*";

    /// <summary>Folders a room is never made in: the repository's history and the doctrine's own.</summary>
    private static readonly string[] Apart = [".git", ".agents", ".claude"];

    /// <summary>The path as it is kept, or why it is not a folder a room may be made in.</summary>
    public static (string? Path, string? Problem) Read(string? typed)
    {
        var text = (typed ?? "").Trim();
        if (text.Length == 0) return (null, "name the folder the room is made in, as `--room <path>`: a folder of the repository, with `/` between its parts.");
        if (text.Any(char.IsControl)) return (null, "the path holds a control character, which no folder's name does.");
        var normal = text.Replace('\\', '/');
        if (normal.StartsWith('/') || normal.StartsWith("//", StringComparison.Ordinal)) return (null, $"`{text}` starts at a root, so it leaves the repository: name a folder inside it, relative to its top.");
        if (normal.Contains(':')) return (null, $"`{text}` names a drive or a stream, which leaves the repository: name a folder inside it, relative to its top.");
        var parts = new List<string>();
        foreach (var part in normal.Split('/'))
        {
            if (part.Length == 0 || part == ".") continue;
            if (part == "..") return (null, $"`{text}` goes up out of a folder with `..`: name the folder from the repository's top, without it.");
            if (part.IndexOfAny(Reserved.ToCharArray()) >= 0) return (null, $"`{part}` holds a character a folder's name cannot hold on every system (`{Reserved}`).");
            if (part.EndsWith('.') || part.EndsWith(' ')) return (null, $"`{part}` ends in a dot or a space, which some systems drop from a folder's name.");
            parts.Add(part);
        }

        if (parts.Count == 0) return (null, "name a folder of the repository: the repository's own top is the root brief's, not a room's.");
        if (Apart.Contains(parts[0], StringComparer.OrdinalIgnoreCase)) return (null, $"`{parts[0]}` is the repository's own history or the doctrine's folder, where no room is made.");
        return (string.Join('/', parts), null);
    }

    /// <summary>Whether two paths, each as <see cref="Read"/> keeps one or as a manifest spells one, are the same folder on a system that ignores case.</summary>
    public static bool Same(string kept, string declared)
    {
        var normal = declared.Replace('\\', '/').Trim();
        while (normal.StartsWith("./", StringComparison.Ordinal)) normal = normal[2..];
        return string.Equals(kept, normal.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
    }
}

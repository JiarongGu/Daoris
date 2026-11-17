namespace Daoris.Assistant.Ports;

/// <summary>
/// The one judgement of a path a port takes relative to a folder (a checkout's file, a room's file), so every side of a port refuses
/// the same paths: a path is inside where it is not blank, not rooted, names no drive and has no segment that climbs out.
/// </summary>
public static class InsidePath
{
    /// <summary>Whether <paramref name="path"/> names a file inside its folder.</summary>
    public static bool Holds(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        if (path[0] is '/' or '\\' || path.Contains(':') || Path.IsPathRooted(path)) return false;
        var segments = path.Split('/', '\\');
        return !segments.Any(segment => segment == "..") && segments[^1].Length > 0;
    }

    /// <summary><paramref name="path"/> with forward slashes where it is inside; else refused (<see cref="ArgumentException"/>), naming it.</summary>
    public static string Require(string? path, string parameter) => Holds(path)
        ? path!.Replace('\\', '/')
        : throw new ArgumentException($"'{path}' is not a path inside its folder: it is blank, rooted, or climbs out", parameter);
}

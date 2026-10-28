namespace Daoris.Driver;

/// <summary>
/// The one way a session's tree path is read as a tree: pure vocabulary the workplace's offers and comparisons share, which
/// <see cref="SessionGroups.Normal"/> keeps under its own name for the readers above the workplace.
/// </summary>
internal static class TreeNames
{
    /// <summary>A tree path as one tree: separators and a trailing one aside, compared without case, as Windows sees it.</summary>
    internal static string Normal(string tree) => tree.Replace('\\', '/').TrimEnd('/');

    /// <summary>Two tree paths are one tree — separators and case aside, as Windows sees it; neither may be empty.</summary>
    internal static bool Same(string? a, string? b) =>
        a is { Length: > 0 } && b is { Length: > 0 }
        && string.Equals(
            a.Replace('\\', '/').TrimEnd('/'), b.Replace('\\', '/').TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
}

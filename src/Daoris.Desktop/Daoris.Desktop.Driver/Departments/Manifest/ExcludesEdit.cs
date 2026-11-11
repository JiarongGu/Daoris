namespace Daoris.Driver;

/// <summary>A folder department's excludes after an edit: what the person's list holds beside the defaults.</summary>
public static class ExcludesEdit
{
    /// <summary>
    /// <paramref name="current"/> without each glob of <paramref name="remove"/>, then each glob of <paramref name="add"/> it does not hold,
    /// every glob trimmed and kept once, in the order of its first holding or adding.
    /// </summary>
    public static IReadOnlyList<string> Apply(IEnumerable<string> current, IEnumerable<string> add, IEnumerable<string> remove)
    {
        var gone = remove.Select(glob => glob.Trim()).ToHashSet(StringComparer.Ordinal);
        List<string> list = [.. current.Select(glob => glob.Trim()).Where(glob => glob.Length > 0 && !gone.Contains(glob)).Distinct(StringComparer.Ordinal)];
        foreach (var glob in add.Select(each => each.Trim()).Where(each => each.Length > 0))
        {
            if (!list.Contains(glob, StringComparer.Ordinal)) list.Add(glob);
        }

        return list;
    }
}

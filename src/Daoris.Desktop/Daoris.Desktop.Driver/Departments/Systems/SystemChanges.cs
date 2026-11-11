namespace Daoris.Driver;

/// <summary>
/// A system place's review: the newest change set a session's turn ended with, each operation a row named by its resource, added,
/// modified or deleted, with its value before and after as a patch, so the window's review shows it as a folder's files are shown.
/// The review's Accept and ask for changes are a <c>review</c>'s, unchanged.
/// </summary>
public static class SystemChanges
{
    /// <summary>The change set <paramref name="record"/>'s newest plan holds, or null where it planned nothing.</summary>
    public static TreeDiff? Of(string home, string record)
    {
        if (new SystemPlans(home).Latest(record) is not { } plan) return null;
        var files = plan.Operations.Select(operation =>
        {
            var (patch, added, removed) = TextPatch.Unified(operation.Resource, Text(operation.Before), operation.Op == "delete" ? null : Text(operation.After));
            var status = operation.Op switch { "create" => "added", "delete" => "deleted", _ => "modified" };
            return new DiffFile(operation.Resource, status, added, removed, patch);
        });
        return new TreeDiff([.. files], null, "");
    }

    /// <summary>A value as the patch shows it: its own lines, ending in one, so a single value reads as one line changed.</summary>
    private static string? Text(string? value) => value is null ? null : value.EndsWith('\n') ? value : value + "\n";
}

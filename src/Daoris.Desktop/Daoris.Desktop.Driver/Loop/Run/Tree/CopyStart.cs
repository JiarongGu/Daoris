namespace Daoris.Driver;

/// <summary>
/// Where a folder department's start goes that its workflow's <c>isolate</c> sets apart: a copy of the folder of its own
/// (<see cref="FolderCopies"/>). A copy refused holds the start with the words saying how to go on, never working in the folder itself
/// in its place, since that would show its edits there before they are accepted. A step naming a git kind holds the start too.
/// </summary>
internal static class CopyStart
{
    /// <summary>The copy made for <paramref name="run"/>'s session.</summary>
    /// <exception cref="DriverException">The step names an isolation a folder has none of, or the copy is refused.</exception>
    public static string Open(string home, RunRound run, string root)
    {
        var start = run.Start;
        if (start.Isolate is { } step && !step.Serves(PlaceKind.Folder))
        {
            throw new DriverException($"`{run.Quest.To}` is a folder, and its workflow's isolate step asks for a `{step.Kind}`, which a folder has none of: "
                + $"set the step's kind to `{IsolateStep.Copy}` or `{IsolateStep.Auto}`.");
        }

        var excludes = PlaceExcludes.Of(run.Registry.FirstOrDefault(view => string.Equals(view.Repository, run.Quest.To, StringComparison.OrdinalIgnoreCase))?.Excludes);
        var made = FolderCopies.Make(home, RemoteTarget.Workspace(start.Workspace), run.Quest.To, root, ManifestBounds.Of(home), excludes);
        return made.Copy?.Folder ?? throw new DriverException(made.Refused!);
    }
}

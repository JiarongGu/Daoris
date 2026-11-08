using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The pipe's caller as the git scheduler reads it: the verb's process and its parents, nearest first, as <see cref="DeskCaller"/>
/// walks them (a parent created after its child is another process given the id since, and ends the walk; the walk never passes this
/// Daoris's own process). A hook of a push, or a diff's textconv helper, that asks the window is so told as beneath that git
/// (<see cref="GitCallers"/>).
/// </summary>
public static class PipeGitCaller
{
    /// <summary>Until disposed, the request's gits are placed beneath the running git <paramref name="client"/> descends from, where one is.</summary>
    public static IDisposable Enter(IProcessTable processes, PipeClientProcess? client, int own) =>
        GitCallers.Beneath(() => AncestorsOf(processes, client, own));

    /// <summary>The client's process and its parents, nearest first; empty where the client's process cannot be read.</summary>
    public static IReadOnlyList<int> AncestorsOf(IProcessTable processes, PipeClientProcess? client, int own)
    {
        if (processes.IdOf(client) is not { } id || processes.Read(id) is not { } verb) return [];
        List<int> ancestors = [verb.Id];
        var child = verb;
        for (var level = 0; level < HolderWalk.Depth && child.Parent > 0 && child.Parent != own; level++)
        {
            if (processes.Read(child.Parent) is not { } parent || parent.Created > child.Created) break;
            ancestors.Add(parent.Id);
            child = parent;
        }

        return ancestors;
    }
}

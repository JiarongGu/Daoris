namespace Daoris.Driver;

/// <summary>The service's long answers read in parts through the flow's git (<see cref="GitRuns"/>): a log walk or a tree's listing.</summary>
internal static class GitServiceParts
{
    /// <summary>The read kinds whose answer may be longer than one message, and which nothing but their order needs whole.</summary>
    internal static readonly IReadOnlySet<string> Kinds = new HashSet<string>(StringComparer.Ordinal) { "log", "cherry", "rev-list", "ls-tree" };

    /// <summary>
    /// <paramref name="arguments"/>' answer in <paramref name="root"/> as numbered parts (<see cref="GitService.PartsAsync"/>), read through
    /// the flow's git as it writes; refused for any read kind but <see cref="Kinds"/>.
    /// </summary>
    /// <exception cref="ArgumentException">The read is not a log walk or a tree's listing.</exception>
    internal static IAsyncEnumerable<GitPart> PartsAsync(this GitService service, string root, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        var kind = GitReadKinds.Of(arguments);
        if (!Kinds.Contains(kind)) throw new ArgumentException($"a {kind} read is answered whole, never in parts", nameof(arguments));
        return service.PartsAsync(root, kind, (take, token) => GitRuns.ReadGitAsync(root, arguments, take, token), ct);
    }
}

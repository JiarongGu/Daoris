using System.Reflection;
using Daoris.Ports.Repositories;

namespace Daoris.Testing;

/// <summary>
/// Many <see cref="MemoryRepository"/> behind one <see cref="IGitRepository"/>, for a family that makes repositories: each ask goes to
/// the repository whose checkout the folder is (one that holds none answers as git does outside a repository), and
/// <c>InitAsync</c> makes a new one. A proxy, so a member the port gains is routed with no line here. An init leaves an empty <c>.git</c>
/// folder on the disk, which is all the application's own disk reads of a repository ask.
/// </summary>
public class MemoryRepositories : DispatchProxy
{
    private readonly List<MemoryRepository> _all = [];
    private readonly MemoryRepository _none = new(Path.Combine(AppContext.BaseDirectory, "no-repository"));

    /// <summary>A host with no repository yet.</summary>
    public static MemoryRepositories New() => (MemoryRepositories)(object)Create<IGitRepository, MemoryRepositories>();

    /// <summary>The port this host is.</summary>
    public IGitRepository Port => (IGitRepository)(object)this;

    /// <summary>Hands a repository the host answers for.</summary>
    public MemoryRepository Add(MemoryRepository repository)
    {
        _all.Add(repository);
        return repository;
    }

    /// <summary>Whether git is absent from the machine, which no ask of it answers.</summary>
    public bool Absent { get; set; }

    /// <summary>Words that refuse the empty commit of each repository made from now on, as a hook's refusal does; null for none.</summary>
    public string? RefuseCommit { get; set; }

    /// <summary>Forgets the repository at <paramref name="folder"/>, as a folder replaced by another repository does.</summary>
    public void Drop(string folder) => _all.RemoveAll(each => each.Holds(folder));

    /// <summary>The repository whose checkout <paramref name="folder"/> is.</summary>
    public MemoryRepository At(string folder) => _all.FirstOrDefault(each => each.Holds(folder)) ?? throw new InvalidOperationException($"{folder} holds no repository here");

    /// <summary>Whether a repository was made or handed at <paramref name="folder"/>.</summary>
    public bool Has(string folder) => _all.Any(each => each.Holds(folder));

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        if (targetMethod.Name == nameof(IGitRepository.Held)) return this;
        if (targetMethod.Name == nameof(IGitRepository.AvailableAsync) && Absent) return Task.FromResult(false);
        if (targetMethod.Name == nameof(IGitRepository.InitAsync) && args is [string folder, string line, CancellationToken ct]) return InitAsync(folder, line, ct);
        var target = args is [string at, ..] ? _all.FirstOrDefault(each => each.Holds(at)) ?? _none : _all.FirstOrDefault() ?? _none;
        try
        {
            return targetMethod.Invoke(target, args);
        }
        catch (TargetInvocationException thrown) when (thrown.InnerException is { } inner)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(inner).Throw();
            throw;
        }
    }

    private Task<GitDone> InitAsync(string folder, string line, CancellationToken ct)
    {
        if (!Has(folder))
        {
            Directory.CreateDirectory(Path.Combine(folder, ".git"));
            _all.Add(new MemoryRepository(folder, line) { RefuseCommit = RefuseCommit });
        }

        return At(folder).InitAsync(folder, line, ct);
    }
}

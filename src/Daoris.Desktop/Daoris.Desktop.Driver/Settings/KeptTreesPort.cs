namespace Daoris.Driver;

/// <summary>
/// Settings' port to the kept slots, which the workplace answers: what a verb that sets a cap or a count asks before it writes.
/// </summary>
public interface IKeptTrees
{
    /// <summary>How many slots of a repository a count of <paramref name="count"/> retires.</summary>
    int Retiring(string workspace, string repository, int count);

    /// <summary>Why the pool cannot serve a repository (a submodule it tracks, no job that holds a claim's processes), or null where it can.</summary>
    string? Withheld(string workspace, string repository);
}

/// <summary>The kept slots' answer, installed by the workplace when the driver loads; unset, it throws rather than answering nothing.</summary>
public static class KeptTreesPort
{
    private static IKeptTrees? _current;

    public static IKeptTrees Current
    {
        get => _current ?? throw new InvalidOperationException("no kept trees answer is installed: Workplace installs it when the driver loads");
        set => _current = value;
    }

    /// <summary>Whether an answer is installed.</summary>
    public static bool IsSet => _current is not null;
}

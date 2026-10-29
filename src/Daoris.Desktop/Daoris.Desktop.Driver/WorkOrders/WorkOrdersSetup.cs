namespace Daoris.Driver;

/// <summary>
/// What a repository's own files say it is, as the intake's room tells it (D77): its summary, what it is built with, and the file the
/// summary came from. Setup's <see cref="RepositoryDescription"/> implements it directly.
/// </summary>
public interface IIntakeDescription
{
    /// <summary>Its title and first paragraph that says anything, cut to a line or two; null when none does.</summary>
    string? Summary { get; }

    /// <summary>What it is built with, read from the files at its top.</summary>
    IReadOnlyList<string> Stack { get; }

    /// <summary>The file the summary came from, so a reader knows it is the repository's word, not Daoris's.</summary>
    string? Source { get; }
}

/// <summary>
/// What the intake's room asks of Setup: a checkout's own words about itself, read from its files at the top (D77). Setup installs it when
/// the driver loads and reads exactly what the room read before it was a port.
/// </summary>
internal interface IWorkOrdersSetup
{
    /// <summary>What the checkout at <paramref name="root"/> says about itself, or null when it says nothing a reader could use, or is not there.</summary>
    IIntakeDescription? Describe(string root);
}

/// <summary>The required Setup operations, resolved only where the room is seeded for a repository that declared nothing.</summary>
internal static class WorkOrdersSetup
{
    private static IWorkOrdersSetup? _current;

    internal static IWorkOrdersSetup Current
    {
        get => _current ?? throw new InvalidOperationException("no work orders setup operations are installed: Setup installs them when the driver loads");
        set => _current = value;
    }
}

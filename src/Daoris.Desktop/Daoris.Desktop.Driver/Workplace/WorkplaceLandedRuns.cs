namespace Daoris.Driver;

/// <summary>The verification runs the workflows' history may let the clear take; the workplace holds the read for its history clear.</summary>
internal interface IWorkplaceLandedRuns
{
    /// <summary>Each file or folder of a run the clear may take: its binding, its folder, and its node record.</summary>
    IReadOnlyList<(string Path, bool Folder)> Left(string home);
}

internal static class WorkplaceLandedRuns
{
    private static IWorkplaceLandedRuns? _current;

    internal static IWorkplaceLandedRuns Current
    {
        get => _current ?? throw new InvalidOperationException("no landed-runs read is installed: the Workflows install it when the driver loads");
        set => _current = value;
    }
}

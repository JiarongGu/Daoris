namespace Daoris.Driver;

/// <summary>
/// The Keeping pass's daily prune of what plugins write for the person to see, as the tidy asks it (<see cref="BranchTidying.PluginOutput"/>).
/// The plugins' folders live above the workplace, so the composition root hands the one that reads them.
/// </summary>
public interface IOutputKeeping
{
    /// <summary>How long a file is kept after its last write.</summary>
    int Days { get; }

    /// <summary>Whether the day's prune is due.</summary>
    bool Due(string home);

    /// <summary>The day's prune: how many files went, and their bytes.</summary>
    (int Removed, long Bytes) Keep(string home);
}

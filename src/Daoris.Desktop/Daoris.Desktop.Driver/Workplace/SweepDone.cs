using static Daoris.Driver.TreeGit;

namespace Daoris.Driver;

/// <summary>What one press of the clean-up did, both groups.</summary>
/// <param name="Folders">The empty folders trees left behind, each removed or still held, in a sentence.</param>
public sealed record SweepDone(IReadOnlyList<SweepResult> Sessions, IReadOnlyList<LandedResult> Landed, IReadOnlyList<string>? Folders = null);

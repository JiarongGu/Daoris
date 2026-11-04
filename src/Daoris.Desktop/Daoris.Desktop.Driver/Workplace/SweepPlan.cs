using static Daoris.Driver.TreeGit;

namespace Daoris.Driver;

/// <summary>The clean-up's whole list (D88, WSR5): the session branches, then the branches landings made.</summary>
public sealed record SweepPlan(IReadOnlyList<SweepItem> Sessions, IReadOnlyList<LandedItem> Landed) : IKnowledgeSweep;

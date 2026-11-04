using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>What the clean-up did with one branch, in the driver's words.</summary>
public sealed record SweepResult(SweepItem Item, bool Removed, string Message)
{
    /// <summary>Kept by the look's own tidy (AUTOTIDY1): why, as the machine log's code (<see cref="TidyKept"/>); null where the press kept it.</summary>
    public string? Kept { get; init; }

    /// <summary>Which <see cref="TidyKept.Unread"/> it was (AUTOTIDY1a2, <see cref="TidyCause"/>); null for any other reason.</summary>
    public string? Cause { get; init; }

    /// <summary>Removed by the look's own tidy (AUTOTIDY1): where its tree's folder was moved aside to; null where it had no folder here.</summary>
    public string? MovedTo { get; init; }

    /// <summary>
    /// Removed though held by content (SQUASHTIDY1c): the ref its commits were kept on, for the look's report and log to name
    /// (AUTOTIDY2a); null where none was kept.
    /// </summary>
    public string? KeptRef { get; init; }
}

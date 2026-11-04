using static Daoris.Driver.TreeGit;

namespace Daoris.Driver;

/// <summary>What the clean-up did with one landed branch, in the driver's words.</summary>
public sealed record LandedResult(LandedItem Item, bool Removed, string Message)
{
    /// <summary>
    /// The session whose work the landing carried, from the record (AUTOTIDY2b): what the look's machine log names the branch by,
    /// since the branch's own name carries the title's words. Null where the record no longer holds it.
    /// </summary>
    public string? Session { get; init; }

    /// <summary>Removed though not merged (SQUASHTIDY1e): the ref its commits were kept on, for the look's report (AUTOTIDY2b); null where none was kept.</summary>
    public string? KeptRef { get; init; }
}

namespace Daoris.Driver;

/// <summary>What an output is, declared by what made it, never read from a path.</summary>
public static class ReviewOutputTypes
{
    /// <summary>A part's handback: its taker's note and the requirements it carried.</summary>
    public const string Handback = "handback";

    /// <summary>The files and commits the work changed.</summary>
    public const string Changes = "changes";

    /// <summary>A system's plan: its operations.</summary>
    public const string Plan = "plan";
}

/// <summary>How an output is drawn: a small vocabulary any type maps to, the raw fallback for one the window does not know.</summary>
public static class ReviewDisplays
{
    public const string ChangeSet = "change-set";

    public const string Document = "document";

    public const string ResourcePlan = "resource-plan";

    public const string Raw = "raw";
}

/// <summary>
/// One output under a review: a stable id across attempts, its type and display, the part it is of, and a digest of the version captured, by
/// which a newer revision says what changed.
/// </summary>
/// <param name="Title">Authored words naming it (a part's title); null where the window words it from its type.</param>
public sealed record ReviewOutput(string Id, string Type, string Display, string? Title)
{
    public string? Part { get; init; }

    public string Digest { get; init; } = "";
}

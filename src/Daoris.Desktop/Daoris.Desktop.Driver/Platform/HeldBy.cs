namespace Daoris.Driver;

/// <summary>
/// What holds a repository's checkout alone (GITLOCK1), as its holder writes it beside the checkout's lock: what a start or a writer it
/// refuses names.
/// </summary>
public static class HeldBy
{
    /// <summary>Bringing its branches up to date: a line's pull and the replays (WSR6), or a tree's own update.</summary>
    public const string Updating = "updating";

    /// <summary>Tidying or removing its session trees, or letting go of their locks.</summary>
    public const string Tidying = "tidying";

    /// <summary>Moving its line to what its integration promoted.</summary>
    public const string Promoting = "promoting";

    /// <summary>A git act of the person's, or the act journal's reconciliation of one (GITAPP2a).</summary>
    public const string Acting = "acting";
}

namespace Daoris.Driver;

/// <summary>
/// The person's choice for a file a folder's return found changed on both sides, by path: the words the return reads, which the node's
/// acts of the same names carry to it.
/// </summary>
public static class ReturnChoices
{
    /// <summary>The folder's version stays; the only choice a refused path takes.</summary>
    public const string KeepYours = "keep-yours";

    /// <summary>The session's version is written over the folder's.</summary>
    public const string TakeSession = "take-session";

    /// <summary>The session's version is written beside the folder's, as <c>&lt;name&gt; (&lt;copy&gt;)&lt;ext&gt;</c>.</summary>
    public const string KeepBoth = "keep-both";

    public static IReadOnlyList<string> All { get; } = [KeepYours, TakeSession, KeepBoth];
}

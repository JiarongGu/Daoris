namespace Daoris.Driver;

/// <summary>
/// The forms a landing rule takes. Pushing and opening a pull request is not a form of its
/// own: a branch rule names the plugin that does it.
/// </summary>
public static class LandingForm
{
    /// <summary>Into the repository's line, in its own checkout — the merge door.</summary>
    public const string Merge = "merge";

    /// <summary>Onto a new branch named by a pattern, from the session's branch, for the person — or the rule's plugin — to push.</summary>
    public const string Branch = "branch";
}

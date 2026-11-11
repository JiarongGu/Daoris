namespace Daoris.Driver;

/// <summary>
/// The boundaries one file's write of a return passes, in order, each a finished change on disk: the journal and a crash's resumption
/// are read against them (<see cref="ReturnJournal"/>), and a test is told each as it is passed.
/// </summary>
public static class ReturnSteps
{
    /// <summary>The file's decision written to the journal, nothing on disk moved.</summary>
    public const string Begun = "begun";

    /// <summary>The session's bytes copied beside the folder's file, under a name of the return's own.</summary>
    public const string Staged = "staged";

    /// <summary>The folder's file renamed aside, the target name empty.</summary>
    public const string SetAside = "set-aside";

    /// <summary>The session's file renamed into the target name.</summary>
    public const string Replaced = "replaced";

    /// <summary>The bytes set aside kept beside the journal, and removed from the folder.</summary>
    public const string Kept = "kept";

    /// <summary>The file's effect written to the journal.</summary>
    public const string Done = "done";

    public static IReadOnlyList<string> All { get; } = [Begun, Staged, SetAside, Replaced, Kept, Done];
}

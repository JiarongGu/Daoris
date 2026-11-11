namespace Daoris.Driver;

/// <summary>
/// One round's call of a declared command, as its frame carries it: the run's id (one per run, the same across its rounds), the arguments
/// by name and typed by their declaration, the answers so far, who runs it (always Daoris's word, never the caller's), the place in force,
/// the viewer's language (<c>en</c> at the terminal), and the session where an agent calls.
/// </summary>
public sealed record CommandCall(
    string Command, string Run, IReadOnlyDictionary<string, object?> Args, IReadOnlyDictionary<string, string> Answers, string From,
    PluginPlace Place, string Language, string? Session = null)
{
    public static string NewRun() => Guid.NewGuid().ToString("N");

    /// <summary>A call with no arguments at the machine, in English: a terminal's or an adoption's first shape.</summary>
    public static CommandCall Plain(string command, IReadOnlyDictionary<string, string> answers, string from) =>
        new(command, NewRun(), new Dictionary<string, object?>(), answers, from, PluginPlace.Machine, "en");
}

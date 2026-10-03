namespace Daoris.Driver;

/// <summary>
/// A session language (LANG1c, D142 point 7, the language design §7): the language a session is asked to write to the
/// person in, as the closed table names it for the agent, and where it was set. The work's, never the window's: the window's
/// language is its viewer's (D142 point 8), and neither sets the other.
/// </summary>
/// <param name="Code">The table's code, <c>en</c> or <c>zh</c>.</param>
/// <param name="Name">What the line calls it for the agent.</param>
/// <param name="Source">Where it was set: <see cref="LanguageSource.Repository"/> or <see cref="LanguageSource.Workspace"/>.</param>
public sealed record SessionLanguage(string Code, string Name, string Source);

/// <summary>Where a repository's session language came from, for the screen and the terminal.</summary>
public static class LanguageSource
{
    public const string Repository = "repository";
    public const string Workspace = "workspace";
}

/// <summary>
/// The closed table of session languages and the one resolution every door reads (LANG1c): what the person set for the
/// repository, else for its workspace, else none. No machine-wide value, and nothing taken from the window.
/// </summary>
/// <remarks>
/// A TWIN with the CLI's <c>driverconfig.ts</c> (<c>SESSION_LANGUAGES</c>, <c>languageFor</c>): <c>SessionLanguageTests</c>
/// holds the table and the reading row for row, and the CLI's <c>driverconfig.test.ts</c> parses both theories to hold them
/// to its own, cell for cell. Adding a language is a row in both tables and needs no window catalogue, since a session may
/// write in a language the window does not speak.
/// </remarks>
public static class SessionLanguages
{
    /// <summary>Each code, and the name the line gives the agent — the CLI's <c>SESSION_LANGUAGES</c>, a deliberate copy.</summary>
    public static readonly IReadOnlyList<(string Code, string Name)> Table =
    [
        ("en", "English"),
        ("zh", "Simplified Chinese (简体中文)"),
    ];

    /// <summary>The table's code a person's spelling names, in any case and without the spaces around it; null for none.</summary>
    public static string? Code(string? spelled)
    {
        var code = spelled?.Trim().ToLowerInvariant();
        return Table.Any(row => row.Code == code) ? code : null;
    }

    /// <summary>The name the line gives the agent for a code of the table.</summary>
    public static string NameOf(string code) => Table.First(row => row.Code == code).Name;

    /// <summary>The refusal every door says for a code the table does not hold.</summary>
    public static string Refusal(string spelled) =>
        $"`{spelled.Trim()}` is not a language a session can be asked to write in here — one of "
        + string.Join(", ", Table.Select(row => $"`{row.Code}`")) + ".";

    /// <summary>
    /// The language a session in <paramref name="repository"/> is asked to write in: the repository's own, else its
    /// workspace's, else null. A repository in no workspace is in the <c>default</c> one (D48 §2), as its line is found.
    /// </summary>
    public static SessionLanguage? Resolve(DriverConfig config, string repository, string? workspace) =>
        config.Languages.TryGetValue(repository.Trim(), out var own)
            ? new SessionLanguage(own, NameOf(own), LanguageSource.Repository)
            : OfWorkspace(config, workspace);

    /// <summary>A workspace's own language, or null: what an intake takes, which answers its ask in no repository.</summary>
    public static SessionLanguage? OfWorkspace(DriverConfig config, string? workspace) =>
        config.WorkspaceLanguages.TryGetValue(RemoteTarget.Workspace(workspace), out var shared)
            ? new SessionLanguage(shared, NameOf(shared), LanguageSource.Workspace)
            : null;
}

/// <summary>
/// The one line naming the session language (LANG1c, the language design §7), handed beside the close instruction it
/// governs, and only where a language is set: unset, every instruction reads byte for byte as it did.
/// </summary>
/// <remarks>
/// The instructions stay one copy, in English (D142 point 6). The driver's checks read the exit code, the quest and the
/// harness's own output, never the agent's prose, so the line changes none of them.
/// </remarks>
public static class SessionLanguageText
{
    /// <summary>The line, in the design's words.</summary>
    public static string Line(SessionLanguage language) =>
        $"Write what you say to a person in {language.Name}: a question to them, a quest's closing note or a decline's "
        + "reason, and your last words when you stop. Keep code, commands, identifiers, file names and anything you quote "
        + "exactly as written.";

    /// <summary>The line as its own paragraph after the close instruction, or nothing where no language is set.</summary>
    internal static string AfterClose(SessionLanguage? language) => language is null ? "" : "\n\n" + Line(language);

    /// <summary>
    /// A resumed conversation's appendix (ANSWER1a, MSG1b) with the line after it, since the setting may have changed since
    /// that conversation was handed it; the appendix as it was where no language is set.
    /// </summary>
    internal static string Resumed(string appendix, SessionLanguage? language) =>
        language is null ? appendix
        : appendix.Length == 0 ? Line(language)
        : $"{appendix}\n\n{Line(language)}";
}

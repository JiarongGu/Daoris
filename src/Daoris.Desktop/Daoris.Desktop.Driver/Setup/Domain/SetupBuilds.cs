namespace Daoris.Driver;

/// <summary>
/// <i>What it builds</i>: library or package, web or service, desktop or game, documentation or
/// tooling. It shapes only the set-up quest's request, the checks the repository's own session is asked to declare in its safe work, and
/// never sets a permission, an appointment, an account or a model.
/// </summary>
public static class SetupBuilds
{
    public const string Library = "library";
    public const string Service = "service";
    public const string Desktop = "desktop";
    public const string Tooling = "tooling";

    public static IReadOnlyList<string> All { get; } = [Library, Service, Desktop, Tooling];

    /// <summary>Whether <paramref name="builds"/> is one of the four.</summary>
    public static bool Is(string? builds) => builds is not null && All.Contains(builds, StringComparer.Ordinal);

    /// <summary>The refusal of a word that is none of the four.</summary>
    public static string Problem(string builds) =>
        $"`{builds}` is not what a repository builds here — `{Library}`, `{Service}`, `{Desktop}` or `{Tooling}`.";

    /// <summary>The kind in the page's words.</summary>
    public static string Named(string builds) => builds switch
    {
        Library => "a library or package",
        Service => "a web app or a service",
        Desktop => "a desktop app or a game",
        _ => "documentation or tooling",
    };

    /// <summary>The sentence the set-up quest's safe-work step adds, in the canon's words: the checks such a repository is asked to declare.</summary>
    public static string Checks(string builds) => $"This repository builds {Named(builds)}: " + builds switch
    {
        Library =>
            "the checks to declare are its build, its tests, its type or lint check where it has one, and the pack or publish dry run that "
            + "shows what a user of it receives; a change to what it exposes is proven against a use of it, not only its own tests.",
        Service =>
            "the checks to declare are its build, its tests and its lint, and the command and address that start it for a person to look at "
            + "before a change lands, where it has them; a check that needs a deployed environment or a credential is named in your close, "
            + "never declared safe.",
        Desktop =>
            "the checks to declare are its build for this machine's platform and its tests; a check that opens a window, plays, or takes "
            + "minutes is declared apart from the quick ones and marked `heavy`, so a session runs the quick ones on every change.",
        _ =>
            "the checks to declare are the ones it has, its link or format check and its tools' own tests; invent no build, deployment or "
            + "stage it does not have.",
    };
}

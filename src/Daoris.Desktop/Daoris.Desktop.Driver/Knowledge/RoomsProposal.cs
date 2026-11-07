namespace Daoris.Driver;

/// <summary>
/// What a repository with no rooms is asked to propose: the set-up's brief and Ask Daoris's room say it in these words, so the two
/// proposals ask the same. The conventions are the canon's knowledge, never code, and a model writes the declaration, so nothing is
/// proposed where none runs. The declaration is the repository's own change, on its own branch, accepted by the person where it
/// lands; Daoris writes none of it. What git does not manage is Daoris's own analysis to read (<see cref="FileAnalysis"/>), so no
/// class of it is proposed.
/// </summary>
internal static class RoomsProposal
{
    /// <summary>The canon knowledge that holds the conventions.</summary>
    internal const string Knowledge = "rooms-and-classes";

    /// <summary>The set-up's step: propose the rooms where the repository declares none.</summary>
    internal static string Step() =>
        "**Propose the rooms** where none are declared: no lane in `daoris.lanes.json` carries `builds`. Read the doctrine's `"
        + Knowledge + "` knowledge first, then this repository's build files, never its folder names: each lane that builds gains "
        + "`builds` (its units, and what its builds read and write outside them), lanes that are one package, one lockfile or one build "
        + "name one `room`, and the laneless groups every build reads name the room they join; with no lanes file, write one whose "
        + "lanes are the rooms. Where the repository declares rooms already, leave them as they are.";

    /// <summary>What the set-up's close says of them.</summary>
    internal const string Close = "the rooms proposed, the rooms whose builds meet, or why none were";

    /// <summary>Ask Daoris's line: how it proposes them for a repository set up already.</summary>
    internal static string Ask() =>
        "  A repository set up already that declares no rooms (no lane with `builds`) is offered them by `ask_propose` at its workspace:\n"
        + "  words naming the repository and asking its session to propose its rooms by the doctrine's `" + Knowledge + "` knowledge, on its own branch,\n"
        + "  for the person to accept where it lands. You draft nothing into the repository yourself; a set-up's adoption asks the same.\n";
}

using System.Collections.ObjectModel;

namespace Daoris.Driver;

/// <summary>
/// What a door's spawn answers (the agent-provider design §4.6): its program, its argument vector and the variables its process takes of
/// its own, such as a posture read at boot. Plain data, naming none of Daoris's own variables: the transports' one shell (<see cref="Shell"/>)
/// completes it with them, the tools, the session's git, the account and the binary, in that order.
/// </summary>
/// <param name="Program">The program as the door resolved it: the machine's command, else the door's own binary.</param>
/// <param name="Arguments">Its argument vector, in order.</param>
public sealed record ProcessRecipe(string Program, IReadOnlyList<string> Arguments)
{
    /// <summary>
    /// Whether its stdin is open: a protocol door's frames go on it and a native door's prompt does. A conversation's always is.
    /// </summary>
    public bool Input { get; init; }

    /// <summary>
    /// Whether what is written on its stdin is BOM-less UTF-8: a native prompt's first character would otherwise be the mark, and the
    /// program's own default for a pipe is not Daoris's to guess.
    /// </summary>
    public bool Utf8Input { get; init; }

    /// <summary>The door's own variables, set after Daoris's identity variables and before the account's.</summary>
    public IReadOnlyDictionary<string, string> Variables { get; init; } = ReadOnlyDictionary<string, string>.Empty;

    /// <summary>A recipe of a resolved command: its first word the program, the rest and then <paramref name="more"/> its arguments.</summary>
    public static ProcessRecipe Of(IReadOnlyList<string> command, IEnumerable<string>? more = null) =>
        new(command[0], [.. command.Skip(1), .. more ?? []]);
}

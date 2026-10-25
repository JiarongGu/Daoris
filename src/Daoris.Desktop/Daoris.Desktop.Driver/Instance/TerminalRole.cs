namespace Daoris.Driver;

/// <summary>
/// Whether this process started in a role's terminal, and what a verb says where it refuses for it (STEWARDROLE1a-i, D209 §3.3;
/// FOUNDATION1 mod-vi). The door reads both, to refuse a press that would be the owner's and a verb that would act alone, before it
/// connects; the roles write the mark at the entry (<see cref="HolderKey.Take"/>) and keep their own list of the verbs such a terminal
/// runs (<see cref="RoleTerminal"/>), so the instance names none of theirs.
/// </summary>
public static class TerminalRole
{
    /// <summary>The refusal's code.</summary>
    public const string Code = "role-terminal";

    /// <summary>What the refusal says.</summary>
    public const string Sentence = "this terminal holds a role: act as the role, or press from another terminal";

    /// <summary>
    /// The entry's marker: that the role's key variable was present when the process took it, whatever its value, so this process started
    /// in a role's terminal. Every guard reads this, never the environment after the entry, which the take has already emptied.
    /// </summary>
    public static bool Held { get; internal set; }

    /// <summary>The refusal as a verb says it, naming its code.</summary>
    public static string Refusal(string verb) => $"{verb}: {Sentence} ({Code})";
}

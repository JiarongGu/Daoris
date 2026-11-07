namespace Daoris.Driver;

/// <summary>
/// A door a plugin's manifest declares: the fields a declaration has a place for, and no others. It has
/// no field for rules, skills, limit, window, sign-in, refused or error words, a usage question, children or lineage, confinement,
/// steering, queueing, resume, a dialect or a conversation home, so a manifest naming one reaches nothing and no door reads it. Plugins
/// converts a manifest into it; <see cref="DeclaredDoors"/> makes it a door.
/// </summary>
/// <param name="Name">The door's id.</param>
/// <param name="DeclaredBy">The plugin it came from, which the roster shows beside it and no session record carries.</param>
/// <param name="Command">The program a session starts, its first word the binary the roster asks about.</param>
/// <param name="Posture">The posture in the harness's own vocabulary, or null when its wire carries none.</param>
/// <param name="ProfileVariable">The variable naming the home an account is handed in, or null where none can be.</param>
/// <param name="Package">The package the program installs from.</param>
/// <param name="Install">The command that installs it.</param>
/// <param name="VersionArguments">The version question, or null where presence alone is asked of the file or PATH.</param>
/// <param name="AccountOf">The agent whose accounts the door runs as, verbatim, or null for none.</param>
/// <param name="Product">What the plugin says its tool is.</param>
/// <param name="Maker">Who makes it, which the commit trailer and the reviewer's choice read.</param>
/// <param name="OwnHome">The plugin's word that its agent's own home is nobody's.</param>
/// <param name="FullPosture">The harness's own mode that asks nothing, or null where none is declared.</param>
/// <param name="AskingPosture">The harness's own mode that asks the person, or null where none is declared.</param>
public sealed record DeclaredDoor(
    string Name,
    string DeclaredBy,
    IReadOnlyList<string> Command,
    string? Posture = null,
    string? ProfileVariable = null,
    string? Package = null,
    IReadOnlyList<string>? Install = null,
    IReadOnlyList<string>? VersionArguments = null,
    string? AccountOf = null,
    string? Product = null,
    string? Maker = null,
    bool OwnHome = false,
    string? FullPosture = null,
    string? AskingPosture = null);

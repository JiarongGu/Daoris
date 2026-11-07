namespace Daoris.Driver;

/// <summary>
/// The refusal of a door id this build does not know: asking whether a
/// repository that has not adopted Daoris may be driven names a door, and a name no registration answers to is no door, so no answer
/// is given rather than reading it as the pipe. It carries the id, so a surface says which one and a caller tells it from any other
/// refusal.
/// </summary>
public sealed class UnknownDoorException(string door) : DriverException(Words(door))
{
    /// <summary>The id nothing in this build answers to.</summary>
    public string Door { get; } = door;

    /// <summary>The sentence every surface says: the id, and where the agents this machine has are listed.</summary>
    public static string Words(string door) =>
        $"`{door}` is not a door this build knows, so whether it may drive a repository that has not adopted Daoris is not answered — "
        + "`daoris agent list` names the agents this machine has; set Build's order to one of them.";
}

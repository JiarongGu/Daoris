namespace Daoris.Driver;

/// <summary>
/// How the machine's memory stands beside the reserve (<see cref="ReserveSetting"/>), for the setting's row to show: the machine's memory in
/// all and free commit, the commit of Daoris's own processes and the reserve in force, each in bytes and null where it cannot be read.
/// </summary>
public sealed record MachineUse(long? Total, long? FreeCommit, long? Daoris, long Reserve)
{
    /// <summary>Nothing read: what a world with no reader says.</summary>
    public static MachineUse Unknown { get; } = new(null, null, null, 0);

    /// <summary>A byte count as GB, one place, for the words a person reads.</summary>
    public static string Gb(long? bytes) => bytes is { } value ? (Math.Floor(value * 10.0 / (1L << 30)) / 10).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) : "?";
}

using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// What the machine keeps free at least for the person's own work: <c>reserveGb</c> in <c>driver.json</c>, a whole number of GB, or
/// absent for <c>auto</c>, no floor. It is a floor under the room's measured headroom: the machine keeps free the larger of this
/// and how far the person's own use has swung above its present in the last ten minutes, plus a margin (<see cref="PersonMeasure"/>).
/// A heavy run, a check or a build is admitted only while the machine's free commit stays at or above that after the run's own need, and
/// a new admission waits while it is under it (<see cref="CheckAdmission.Judge"/>), saying so; 0 is no floor, the measured room alone.
/// Read at each look by the heavy executor, the integration's gated lines and the merge tool (<c>tools/merge-branch/memory.mjs</c> reads
/// the same field), so a change needs no restart. Written only when set, so the list tells auto from a choice.
/// </summary>
public static class ReserveSetting
{
    private static readonly MachineSetting Declared = MachineSettings.Of("reserve")!;

    /// <summary>Its field in <c>driver.json</c>.</summary>
    public const string File = "reserveGb";

    /// <summary>The setting's one name: the terminal's verb and the window's route.</summary>
    public const string Key = "reserve";

    /// <summary>The word for the default: a third of the machine's physical memory.</summary>
    public const string Auto = "auto";

    /// <summary>The fewest GB it takes, as the list declares it: none, which keeps nothing back.</summary>
    public static int Least => Declared.Least!.Value;

    /// <summary>The most GB it takes, as the list declares it.</summary>
    public static int Most => Declared.Most!.Value;

    /// <summary>The refusal every door says for a value it does not take.</summary>
    public static readonly string Refusal = string.Create(CultureInfo.InvariantCulture,
        $"what the machine keeps free for your own work is auto or a whole number of GB from {Least} to {Most} — e.g. `daoris settings reserve auto` or `daoris settings reserve 24`.");

    /// <summary>Whether a value is one the setting takes.</summary>
    public static bool Takes(long gb) => gb >= Least && gb <= Most;

    /// <summary>The file's value as read: a whole number in bounds, or null for auto.</summary>
    public static int? Read(long? stored) => stored is { } gb && Takes(gb) ? (int)gb : null;

    /// <summary>The floor in bytes: the pinned GB, else none (<c>auto</c> is the measured room alone).</summary>
    public static long Bytes(int? gb) => gb is { } pinned ? (long)pinned << 30 : 0;

    /// <summary>The value in the file at <paramref name="path"/>, or auto where it does not read.</summary>
    public static int? At(string path)
    {
        try
        {
            return DriverConfig.Load(path).ReserveGb;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            return null;
        }
    }

    /// <summary>Pin the reserve, or clear it with null for auto.</summary>
    /// <exception cref="DriverException">A value out of its bounds.</exception>
    public static DriverConfig WithReserve(this DriverConfig config, int? gb) =>
        gb is { } value && !Takes(value) ? throw new DriverException(Refusal) : config with { ReserveGb = gb };

    /// <summary>The value as its doors spell it: the number, or <c>auto</c>.</summary>
    public static string Said(int? gb) => gb?.ToString(CultureInfo.InvariantCulture) ?? Auto;
}

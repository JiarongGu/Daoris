using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// How long a staged build's drain may hold new work: <c>drainMinutes</c> in <c>driver.json</c>, the one bound the application's
/// updater reads at each look. Past it the drain defers itself: new work starts again, and the build installs at the first look that
/// finds nothing running, so one long turn never keeps a new ask's intake waiting. Written only when set, so the list tells the
/// default from a choice.
/// </summary>
public static class DrainSetting
{
    private static readonly MachineSetting Declared = MachineSettings.Of("drain")!;

    /// <summary>Its field in <c>driver.json</c>.</summary>
    public const string File = "drainMinutes";

    /// <summary>The fewest minutes it takes, as the list declares it.</summary>
    public static int Least => Declared.Least!.Value;

    /// <summary>The most minutes it takes, as the list declares it.</summary>
    public static int Most => Declared.Most!.Value;

    /// <summary>
    /// The bound where the file sets none: ten minutes, long enough for the sessions running to end or park, short enough that a
    /// person's new ask is answered within the time they would look again.
    /// </summary>
    public const int Default = 10;

    /// <summary>The refusal every door says for a value out of its bounds.</summary>
    public static readonly string Refusal = string.Create(CultureInfo.InvariantCulture,
        $"how long an update's drain may hold new work is a whole number of minutes from {Least} to {Most} — e.g. `daoris settings drain 10`.");

    /// <summary>Whether a value is one the setting takes.</summary>
    public static bool Takes(long minutes) => minutes >= Least && minutes <= Most;

    /// <summary>The bound the updater reads: the file's minutes, or the default.</summary>
    public static TimeSpan Of(DriverConfig config) => TimeSpan.FromMinutes(config.DrainMinutes ?? Default);

    /// <summary>The bound in the file at <paramref name="path"/>, or the default where it does not read.</summary>
    public static TimeSpan At(string path)
    {
        try
        {
            return Of(DriverConfig.Load(path));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            return TimeSpan.FromMinutes(Default);
        }
    }

    /// <summary>The file's value as read: a whole number in bounds, or null for the default.</summary>
    public static int? Read(long? stored) => stored is { } minutes && Takes(minutes) ? (int)minutes : null;

    /// <summary>Set how long a drain may hold new work, or clear it with null to take the default.</summary>
    /// <exception cref="DriverException">A value out of its bounds.</exception>
    public static DriverConfig WithDrain(this DriverConfig config, int? minutes) =>
        minutes is { } value && !Takes(value) ? throw new DriverException(Refusal) : config with { DrainMinutes = minutes };
}

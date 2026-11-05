using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// How long a session may say nothing, with nothing it runs using the machine, before Daoris looks in on it (D200, the session-watch
/// design §3): <c>quietMinutes</c> in <c>driver.json</c>, the one quiet setting, which the watcher's bound Q is read from. Written only
/// when set, so the list tells the default from a choice.
/// </summary>
public static class QuietSetting
{
    private static readonly MachineSetting Declared = MachineSettings.Of("quiet")!;

    /// <summary>Its field in <c>driver.json</c>.</summary>
    public const string File = "quietMinutes";

    /// <summary>The fewest minutes it takes, as the list declares it.</summary>
    public static int Least => Declared.Least!.Value;

    /// <summary>The most minutes it takes, as the list declares it.</summary>
    public static int Most => Declared.Most!.Value;

    /// <summary>Q where the file sets none: ten minutes, longer than the longest foreground command Claude Code allows.</summary>
    public const int Default = 10;

    /// <summary>The refusal every door says for a value out of its bounds.</summary>
    public static readonly string Refusal = string.Create(CultureInfo.InvariantCulture,
        $"how long a session may be quiet is a whole number of minutes from {Least} to {Most} — e.g. `daoris settings quiet 15`.");

    /// <summary>Whether a value is one the setting takes.</summary>
    public static bool Takes(long minutes) => minutes >= Least && minutes <= Most;

    /// <summary>The bound this machine's watcher reads: the file's minutes, or the default.</summary>
    public static TimeSpan Of(DriverConfig config) => TimeSpan.FromMinutes(config.QuietMinutes ?? Default);

    /// <summary>The file's value as read: a whole number in bounds, or null for the default.</summary>
    public static int? Read(long? stored) => stored is { } minutes && Takes(minutes) ? (int)minutes : null;

    /// <summary>Set how long a session may be quiet, or clear it with null to take the default.</summary>
    /// <exception cref="DriverException">A value out of its bounds.</exception>
    public static DriverConfig WithQuiet(this DriverConfig config, int? minutes) =>
        minutes is { } value && !Takes(value) ? throw new DriverException(Refusal) : config with { QuietMinutes = minutes };
}

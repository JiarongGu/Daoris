using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// How long a run's step may stand vacant (<see cref="StepVacancies"/>) before Daoris stops it with what it tried: <c>vacancyHours</c> in
/// <c>driver.json</c>, a day by default, long enough to see it listed and cast the role, short enough that no run stands for a week.
/// Written only when set, so the list tells the default from a choice.
/// </summary>
public static class VacancySetting
{
    private static readonly MachineSetting Declared = MachineSettings.Of(Key)!;

    public const string Key = "vacancy";

    public const string File = "vacancyHours";

    public const int Default = 24;

    public static int Least => Declared.Least!.Value;

    public static int Most => Declared.Most!.Value;

    public static readonly string Refusal = string.Create(CultureInfo.InvariantCulture,
        $"how long a step may stand vacant is a whole number of hours from {Least} to {Most} — e.g. `daoris settings vacancy {Default}`.");

    public static TimeSpan Of(DriverConfig? config) => TimeSpan.FromHours(config?.VacancyHours ?? Default);

    public static bool Takes(long hours) => hours >= Least && hours <= Most;

    /// <summary>The file's value: a whole number in bounds, or null for the default.</summary>
    public static int? Read(long? stored) => stored is { } hours && Takes(hours) ? (int)hours : null;

    public static DriverConfig WithVacancy(this DriverConfig config, int? hours) =>
        hours is { } value && !Takes(value) ? throw new DriverException(Refusal) : config with { VacancyHours = hours };

    public static string Said(TimeSpan bound) =>
        string.Create(CultureInfo.InvariantCulture, $"{(int)bound.TotalHours} hours (`{File}` in driver.json, {Default} by default)");
}

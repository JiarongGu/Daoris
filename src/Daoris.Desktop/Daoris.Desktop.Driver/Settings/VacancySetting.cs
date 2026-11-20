using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// How long a run's step may stand vacant (<see cref="StepVacancies"/>) before Daoris stops it with what it tried: <c>vacancyHours</c> in
/// <c>driver.json</c>, a day by default, long enough to see it listed and cast the role, short enough that no run stands for a week.
/// </summary>
public static class VacancySetting
{
    public const string File = "vacancyHours";

    public const int Least = 1;

    public const int Most = 720;

    public const int Default = 24;

    public static TimeSpan Of(DriverConfig? config) => TimeSpan.FromHours(config?.VacancyHours ?? Default);

    /// <summary>The file's value: a whole number in bounds, or null for the default.</summary>
    public static int? Read(long? stored) => stored is { } hours && hours >= Least && hours <= Most ? (int)hours : null;

    public static string Said(TimeSpan bound) =>
        string.Create(CultureInfo.InvariantCulture, $"{(int)bound.TotalHours} hours (`{File}` in driver.json, {Default} by default)");
}

using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// How many checks the machine runs at once: <c>checks</c> in <c>driver.json</c>, a pinned number, or absent for <c>auto</c>, which
/// admits another while the machine's memory holds one more (<see cref="CheckAdmission"/>). Read by the heavy executor and the
/// integration's gated lines at each look, so a change needs no restart. Written only when pinned, so the list tells auto from a choice.
/// </summary>
public static class ChecksSetting
{
    private static readonly MachineSetting Declared = MachineSettings.Of("checks")!;

    /// <summary>Its field in <c>driver.json</c>.</summary>
    public const string File = "checks";

    /// <summary>The word for the default: as many as the memory admits.</summary>
    public const string Auto = "auto";

    /// <summary>The fewest it takes, as the list declares it.</summary>
    public static int Least => Declared.Least!.Value;

    /// <summary>The most it takes, as the list declares it.</summary>
    public static int Most => Declared.Most!.Value;

    /// <summary>The refusal every door says for a value it does not take.</summary>
    public static readonly string Refusal = string.Create(CultureInfo.InvariantCulture,
        $"how many checks the machine runs at once is auto or a whole number from {Least} to {Most} — e.g. `daoris settings checks auto` or `daoris settings checks 2`.");

    /// <summary>Whether a value is one the setting takes.</summary>
    public static bool Takes(long checks) => checks >= Least && checks <= Most;

    /// <summary>The file's value as read: a whole number in bounds, or null for auto.</summary>
    public static int? Read(long? stored) => stored is { } checks && Takes(checks) ? (int)checks : null;

    /// <summary>The value in the file at <paramref name="path"/>, or auto where it does not read.</summary>
    public static int? At(string path)
    {
        try
        {
            return DriverConfig.Load(path).Checks;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            return null;
        }
    }

    /// <summary>
    /// The <c>checks</c> the file at <paramref name="path"/> holds where it holds one the reader takes as auto (not a whole number in
    /// bounds), as written there, so the list can say it does not read; null where it holds none, holds one that reads, or the file does not read.
    /// </summary>
    public static string? Unread(string path)
    {
        try
        {
            if (!System.IO.File.Exists(path)) return null;
            using var document = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllBytes(path));
            var root = document.RootElement;
            if (root.ValueKind != System.Text.Json.JsonValueKind.Object || !root.TryGetProperty(File, out var value)) return null;
            var number = value.ValueKind == System.Text.Json.JsonValueKind.Number && value.TryGetInt64(out var pinned) ? pinned : (long?)null;
            return Read(number) is null ? value.GetRawText() : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>Pin how many checks run at once, or clear it with null for auto.</summary>
    /// <exception cref="DriverException">A value out of its bounds.</exception>
    public static DriverConfig WithChecks(this DriverConfig config, int? checks) =>
        checks is { } value && !Takes(value) ? throw new DriverException(Refusal) : config with { Checks = checks };

    /// <summary>The value as its doors spell it: the number, or <c>auto</c>.</summary>
    public static string Said(int? checks) => checks?.ToString(CultureInfo.InvariantCulture) ?? Auto;
}

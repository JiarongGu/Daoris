using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// The most a repository's trees may hold on this machine before the tree ledger's reading writes its budget notice: <c>treeBudgetGb</c>
/// in <c>driver.json</c>, a whole number of GB, or absent for none. The drive's floor is not this one's: it is the space keeping's one
/// floor (<c>keep.floor</c>, <see cref="FloorPolicy"/>), which the ledger reads. A repository's own file narrows the budget once the keeper
/// reads one.
/// </summary>
public static class TreeBudgetSetting
{
    /// <summary>Its field in <c>driver.json</c>.</summary>
    public const string File = "treeBudgetGb";

    /// <summary>The terminal's word for it: <c>daoris settings trees budget</c>.</summary>
    public const string Key = "trees";

    /// <summary>The word for no budget.</summary>
    public const string None = "none";

    public const int Least = 1;

    public const int Most = 4096;

    /// <summary>The refusal every door says for a value it does not take.</summary>
    public static readonly string Refusal = string.Create(CultureInfo.InvariantCulture,
        $"a repository's trees' budget is none or a whole number of GB from {Least} to {Most} — e.g. `daoris settings trees budget 40` or `daoris settings trees budget none`.");

    public static bool Takes(long gb) => gb is >= Least and <= Most;

    /// <summary>The file's value as read: a whole number in bounds, or null for none.</summary>
    public static int? Read(long? stored) => stored is { } gb && Takes(gb) ? (int)gb : null;

    /// <summary>The budget in bytes; null for none.</summary>
    public static long? Bytes(int? gb) => gb is { } set ? (long)set << 30 : null;

    /// <summary>The value in the file at <paramref name="path"/>, or none where it does not read.</summary>
    public static int? At(string path)
    {
        try
        {
            return DriverConfig.Load(path).TreeBudgetGb;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            return null;
        }
    }

    /// <summary>Set the budget, or clear it with null for none.</summary>
    /// <exception cref="DriverException">A value out of its bounds.</exception>
    public static DriverConfig WithTreeBudget(this DriverConfig config, int? gb) =>
        gb is { } value && !Takes(value) ? throw new DriverException(Refusal) : config with { TreeBudgetGb = gb };
}

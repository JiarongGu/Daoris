using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// The staffing the store holds, as the five fields of a loaded <see cref="DriverConfig"/> hold it (FOUNDATION1w-s part 3, the design §19.15):
/// the three first-agent keys, none where the store holds none, and the actors' orders with the entries that do not read.
/// </summary>
public sealed record StaffingHeld(
    string? Adapter,
    string? IntakeAdapter,
    string? HelperAdapter,
    IReadOnlyDictionary<string, IReadOnlyList<ActorChoice>> Actors,
    IReadOnlyDictionary<string, string> ActorErrors);

/// <summary>
/// Settings' port to the roles' staffing (§19.15, <c>IRoleStaffing</c>): what the home's own <c>driver.json</c> load asks for the staffing
/// instead of the file's. Roles implements it over <c>role_staffing</c>; Settings names nothing of Roles'.
/// </summary>
public interface IRoleStaffing
{
    /// <summary>The store's staffing, or null before the import, where the file's is read as it always was.</summary>
    StaffingHeld? Held();
}

/// <summary>
/// Which <c>driver.json</c> reads its staffing through a port (FOUNDATION1w-s part 3): a file a composition root wired, the home's own and
/// never one <c>DAORIS_DRIVER_CONFIG</c> names, which is read and written as today with no store. Keyed by the file, so two homes in one
/// process (a test's) never share a port.
/// </summary>
public static class RoleStaffingPort
{
    private static readonly ConcurrentDictionary<string, IRoleStaffing> Wired =
        new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    /// <summary>Wire <paramref name="port"/> for <paramref name="file"/>, once before its first load; a second wiring replaces the first.</summary>
    public static void Wire(string file, IRoleStaffing port) => Wired[Path.GetFullPath(file)] = port;

    /// <summary>Whether <paramref name="file"/> is wired: what the census of the composition roots asks.</summary>
    public static bool IsWired(string file) => Wired.ContainsKey(Path.GetFullPath(file));

    /// <summary>Forget a wiring: a test's, at its end.</summary>
    public static void Unwire(string file) => Wired.TryRemove(Path.GetFullPath(file), out _);

    /// <summary>
    /// <paramref name="config"/>, read from <paramref name="file"/>, with the staffing the store holds where the file is wired and the store
    /// holds one. The file's own staffing stays on the record as <see cref="DriverConfig"/> read it, which a save writes back as found: the
    /// staffing's four paths are written through the file step alone. A store that cannot be read leaves the file's staffing.
    /// </summary>
    internal static DriverConfig Fill(DriverConfig config, string file)
    {
        if (!Wired.TryGetValue(Path.GetFullPath(file), out var port)) return config;
        StaffingHeld? held;
        try
        {
            held = port.Held();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // Whatever the store's own failure is, a load never throws for it: the file's staffing is what it read.
            return config;
        }

        if (held is null) return config;
        return config with
        {
            Adapter = held.Adapter ?? DriverConfig.Empty.Adapter,
            IntakeAdapter = held.IntakeAdapter,
            HelperAdapter = held.HelperAdapter,
            Actors = held.Actors,
            ActorErrors = held.ActorErrors,
            AsFile = new FileStaffing(config.Adapter, config.IntakeAdapter, config.HelperAdapter, config.Actors),
        };
    }
}

/// <summary>The staffing a config's file held when it was read, which a save writes back untouched (<see cref="DriverConfig.AsFile"/>).</summary>
internal sealed record FileStaffing(
    string Adapter, string? IntakeAdapter, string? HelperAdapter, IReadOnlyDictionary<string, IReadOnlyList<ActorChoice>> Actors);

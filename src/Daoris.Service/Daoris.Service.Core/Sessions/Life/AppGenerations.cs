namespace Daoris.Knowledge;

/// <summary>One start of the application: its number, which only grows, and when the application's process started.</summary>
public sealed record AppGeneration(long Number, DateTimeOffset Started);

/// <summary>
/// The application's generations, kept in the service's store beside the journals: each start of the application, as its
/// driver's loop first looks, begins one, and every fact appended after is stamped with it. A lease names the generation its process
/// started in (<see cref="Of"/>), so a process that survived a restart holds a lease of an earlier generation, which the fold voids.
/// </summary>
/// <remarks>
/// The application's start, not the host's: a host is adopted by a later start (<c>HostSupervisor</c>), so a host's own start says
/// nothing of the processes the application runs.
/// </remarks>
public sealed class AppGenerations
{
    private readonly ConnectionGate _db;

    internal AppGenerations(ConnectionGate db) => _db = db;

    /// <summary>Begin a generation for an application whose process started at <paramref name="started"/>.</summary>
    public Task<AppGeneration> BeginAsync(DateTimeOffset started, DateTimeOffset now, CancellationToken ct = default) =>
        _db.InTransactionAsync(async (_, inside) =>
        {
            await using var command = _db.Command();
            command.CommandText = "INSERT INTO app_generations (started, begun) VALUES ($started, $begun) RETURNING generation";
            command.Parameters.AddWithValue("$started", Moments.Write(started));
            command.Parameters.AddWithValue("$begun", Moments.Write(now));
            return new AppGeneration(Convert.ToInt64(await command.ExecuteScalarAsync(inside).ConfigureAwait(false)), started);
        }, ct);

    /// <summary>Every generation, oldest first.</summary>
    public Task<IReadOnlyList<AppGeneration>> AllAsync(CancellationToken ct = default) =>
        _db.RunAsync<IReadOnlyList<AppGeneration>>(async () =>
        {
            await using var command = _db.Command();
            command.CommandText = "SELECT generation, started FROM app_generations ORDER BY generation";
            var all = new List<AppGeneration>();
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                all.Add(new(reader.GetInt64(0), Moments.Read(reader.GetString(1)) ?? DateTimeOffset.MinValue));
            }

            return all;
        }, ct);

    /// <summary>The generation the store holds now, inside the caller's step; 0 where no start has begun one.</summary>
    internal static async Task<long> CurrentAsync(ConnectionGate db, CancellationToken ct)
    {
        await using var command = db.Command();
        command.CommandText = "SELECT COALESCE(MAX(generation), 0) FROM app_generations";
        return Convert.ToInt64(await command.ExecuteScalarAsync(ct).ConfigureAwait(false));
    }

    /// <summary>
    /// The generation a process that started at <paramref name="processStarted"/> belongs to: the newest begun by an application that
    /// started no later than it; 0 for one older than every generation.
    /// </summary>
    public static long Of(DateTimeOffset processStarted, IReadOnlyList<AppGeneration> all) =>
        all.Where(generation => generation.Started <= processStarted).Select(generation => generation.Number).DefaultIfEmpty(0).Max();
}

namespace Daoris.Knowledge;

/// <summary>
/// One start of the application: its number, which only grows, when the application's process started, and when it last showed itself (its
/// newest tick's audit), null before its first.
/// </summary>
public sealed record AppGeneration(long Number, DateTimeOffset Started, DateTimeOffset? Seen = null);

/// <summary>
/// The application's generations, kept in the service's store beside the journals. Only the start that holds the home's lease begins
/// one, as its driver's loop first looks; a driver beside it writes in the holder's. A fact names the generation its writer writes in
/// (<see cref="FactWriter"/>), and each tick's audit marks its generation seen, so the spans with the application down run from a
/// generation's last sign to the next start (<see cref="SessionBuckets.Down"/>).
/// </summary>
/// <remarks>
/// The application's start, not the host's: a host is adopted by a later start (<c>HostSupervisor</c>), so a host's own start says
/// nothing of the processes the application runs.
/// </remarks>
public sealed class AppGenerations
{
    private readonly ConnectionGate _db;

    internal AppGenerations(ConnectionGate db) => _db = db;

    /// <summary>The store's step: the generations' table.</summary>
    internal static async Task EnsureAsync(ConnectionGate db, CancellationToken ct)
    {
        await using var command = db.Command();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS app_generations (
              generation INTEGER PRIMARY KEY AUTOINCREMENT,
              started    TEXT NOT NULL,
              begun      TEXT NOT NULL,
              seen       TEXT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

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

    /// <summary>The newest generation begun; 0 where none is.</summary>
    public Task<long> CurrentAsync(CancellationToken ct = default) =>
        _db.RunAsync(async () =>
        {
            await using var command = _db.Command();
            command.CommandText = "SELECT COALESCE(MAX(generation), 0) FROM app_generations";
            return Convert.ToInt64(await command.ExecuteScalarAsync(ct).ConfigureAwait(false));
        }, ct);

    /// <summary>Mark <paramref name="generation"/> seen at <paramref name="now"/>: its application ticked then. A generation not begun is left.</summary>
    public Task SeenAsync(long generation, DateTimeOffset now, CancellationToken ct = default) =>
        _db.InTransactionAsync(async (_, inside) =>
        {
            await using var command = _db.Command();
            command.CommandText = "UPDATE app_generations SET seen = $seen WHERE generation = $generation";
            command.Parameters.AddWithValue("$seen", Moments.Write(now));
            command.Parameters.AddWithValue("$generation", generation);
            return await command.ExecuteNonQueryAsync(inside).ConfigureAwait(false);
        }, ct);

    /// <summary>Every generation, oldest first; none where the store has no table of them.</summary>
    /// <exception cref="InvalidDataException">A generation whose time this build cannot read: refused, never read as another moment.</exception>
    public Task<IReadOnlyList<AppGeneration>> AllAsync(CancellationToken ct = default) =>
        _db.RunAsync<IReadOnlyList<AppGeneration>>(async () =>
        {
            await using (var table = _db.Command())
            {
                table.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'app_generations'";
                if (await table.ExecuteScalarAsync(ct).ConfigureAwait(false) is null) return [];
            }

            await using var command = _db.Command();
            command.CommandText = "SELECT generation, started, seen FROM app_generations ORDER BY generation";
            var all = new List<AppGeneration>();
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var number = reader.GetInt64(0);
                var started = Moments.Read(reader.GetString(1))
                    ?? throw new InvalidDataException($"generation {number} started at '{reader.GetString(1)}', which this build does not read");
                DateTimeOffset? seen = reader.IsDBNull(2)
                    ? null
                    : Moments.Read(reader.GetString(2))
                      ?? throw new InvalidDataException($"generation {number} was seen at '{reader.GetString(2)}', which this build does not read");
                all.Add(new(number, started, seen));
            }

            return all;
        }, ct);
}

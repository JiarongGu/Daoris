using System.Text.Json;
using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// The driver's line key repeats this service's walk (<see cref="Lines.WalkAsync"/>), and
/// the driver has no reference to the service, so both are held to <c>fixtures/line-counts.json</c> among the driver's tests. A row that
/// names a service walk is made here, and the walk from its first part visits exactly the records the driver's key joins on one line.
/// </summary>
public sealed class LineKeysFixtureTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private SessionStore _sessions = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _sessions = await SessionStore.OpenAsync(_connection);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    public static TheoryData<string> Rows => [.. Fixture().GetProperty("keys").EnumerateArray()
        .Where(row => row.TryGetProperty("service", out _)).Select(row => row.GetProperty("name").GetString()!)];

    [Theory]
    [MemberData(nameof(Rows))]
    public async Task The_walk_from_the_first_part_visits_the_records_the_driver_s_key_joins(string name)
    {
        var row = Fixture().GetProperty("keys").EnumerateArray().Single(each => each.GetProperty("name").GetString() == name);
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        var minute = 0;
        foreach (var record in row.GetProperty("records").EnumerateArray())
        {
            var quest = record.TryGetProperty("quest", out var q) ? q.GetString() : null;
            var from = record.TryGetProperty("from", out var f) ? names[f.GetString()!] : null;
            var made = await _sessions.CreateAsync(
                quest, "Owner", "stub", Now.AddMinutes(minute++),
                kind: record.GetProperty("kind").GetString() == "driven" ? SessionKind.Driven : SessionKind.Chat, from: from);
            names[record.GetProperty("id").GetString()!] = made.Id;
        }

        var service = row.GetProperty("service");
        var first = await _sessions.FindAsync(names[service.GetProperty("from").GetString()!]);
        var walk = await Lines.WalkAsync(_sessions, first!);

        Assert.Equal(LineEnd.Head, walk.End);
        Assert.Equal(
            service.GetProperty("members").EnumerateArray().Select(member => names[member.GetString()!]),
            walk.Records.Select(record => record.Id));
        Assert.Equal(
            row.GetProperty("line").EnumerateArray().Select(member => names[member.GetString()!]).Order(StringComparer.Ordinal),
            walk.Records.Select(record => record.Id).Order(StringComparer.Ordinal));
    }

    private static JsonElement Fixture()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "daoris.json"))) directory = directory.Parent;
        var root = directory?.FullName ?? throw new InvalidOperationException("no workspace root above the test binaries");
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(root, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "line-counts.json")));
        return document.RootElement.Clone();
    }
}

using System.Text.Json;
using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// A record's <c>line</c>, its conversation's id: set once as the record opens, and given once by the ledger's step to each record an
/// older ledger kept without one, both held to the keys of <c>fixtures/line-counts.json</c> among the driver's tests. A line longer
/// than any walk keeps its id, and a walk the step finds cut keys the part to itself.
/// </summary>
public sealed class SessionLinesTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private SessionStore _sessions = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-12T09:00:00Z");

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _sessions = await SessionStore.OpenAsync(_connection);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    public static TheoryData<string> Opened => [.. Rows().Where(row => !Cut(row)).Select(row => row.GetProperty("name").GetString()!)];

    public static TheoryData<string> Every => [.. Rows().Select(row => row.GetProperty("name").GetString()!)];

    [Theory]
    [MemberData(nameof(Opened))]
    public async Task A_record_opened_carries_its_conversation_s_id(string name)
    {
        var row = Row(name);
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        var minute = 0;
        foreach (var record in row.GetProperty("records").EnumerateArray())
        {
            var made = await _sessions.CreateAsync(
                Quest(record), "Owner", "stub", Now.AddMinutes(minute++), kind: Kind(record),
                from: record.TryGetProperty("from", out var from) ? Named(names, from.GetString()!) : null, fromWhy: "press");
            names[record.GetProperty("id").GetString()!] = made.Id;
            Assert.Equal(made.Line, (await _sessions.FindAsync(made.Id))!.Line);
        }

        await AssertKeys(row, names);
    }

    [Theory]
    [MemberData(nameof(Every))]
    public async Task The_ledger_s_step_gives_an_older_record_its_conversation_s_id_once(string name)
    {
        var row = Row(name);
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        var minute = 0;
        foreach (var record in row.GetProperty("records").EnumerateArray())
            names[record.GetProperty("id").GetString()!] = (await _sessions.CreateAsync(Quest(record), "Owner", "stub", Now.AddMinutes(minute++), kind: Kind(record))).Id;
        foreach (var record in row.GetProperty("records").EnumerateArray())
        {
            if (record.TryGetProperty("from", out var from))
                await Sql($"UPDATE sessions SET from_session = '{Named(names, from.GetString()!)}', from_why = 'press' WHERE id = '{names[record.GetProperty("id").GetString()!]}'");
        }

        await Sql("UPDATE sessions SET line = NULL");
        _sessions = await SessionStore.OpenAsync(_connection);
        await AssertKeys(row, names);

        _sessions = await SessionStore.OpenAsync(_connection);
        await AssertKeys(row, names);
    }

    [Fact]
    public async Task A_line_longer_than_any_walk_keeps_its_first_part_s_id()
    {
        var first = await _sessions.CreateAsync(null, "Owner", "stub", Now, kind: SessionKind.Chat);
        var at = first;
        for (var part = 1; part < Lines.Bound + 40; part++)
            at = await _sessions.CreateAsync(null, "Owner", "stub", Now.AddSeconds(part), kind: SessionKind.Chat, from: at.Id, fromWhy: "press");

        Assert.Equal(first.Id, (await _sessions.FindAsync(at.Id))!.Line);
    }

    [Fact]
    public async Task A_walk_the_step_finds_past_the_bound_keys_the_part_to_itself()
    {
        var ids = new List<string> { (await _sessions.CreateAsync(null, "Owner", "stub", Now, kind: SessionKind.Chat)).Id };
        for (var part = 1; part < Lines.Bound + 2; part++)
            ids.Add((await _sessions.CreateAsync(null, "Owner", "stub", Now.AddSeconds(part), kind: SessionKind.Chat, from: ids[^1], fromWhy: "press")).Id);
        await Sql("UPDATE sessions SET line = NULL");

        _sessions = await SessionStore.OpenAsync(_connection);

        Assert.Equal(ids[0], (await _sessions.FindAsync(ids[Lines.Bound]))!.Line);
        Assert.Equal(ids[^1], (await _sessions.FindAsync(ids[^1]))!.Line);
    }

    private async Task AssertKeys(JsonElement row, Dictionary<string, string> names)
    {
        foreach (var named in row.GetProperty("named").EnumerateArray())
        {
            if (!names.TryGetValue(named.GetProperty("session").GetString()!, out var id)) continue;
            Assert.Equal(Named(names, named.GetProperty("key").GetString()!), (await _sessions.FindAsync(id))!.Line);
        }
    }

    private async Task Sql(string text)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = text;
        await command.ExecuteNonQueryAsync();
    }

    private static string Named(Dictionary<string, string> names, string id) => names.GetValueOrDefault(id, id);

    private static string? Quest(JsonElement record) => record.TryGetProperty("quest", out var quest) ? quest.GetString() : null;

    private static SessionKind Kind(JsonElement record) =>
        record.GetProperty("kind").GetString() == "driven" ? SessionKind.Driven : SessionKind.Chat;

    private static bool Cut(JsonElement row) =>
        row.GetProperty("named").EnumerateArray().Any(named => named.GetProperty("cut").ValueKind == JsonValueKind.String);

    private static JsonElement Row(string name) => Rows().Single(row => row.GetProperty("name").GetString() == name);

    private static IEnumerable<JsonElement> Rows()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "daoris.json"))) directory = directory.Parent;
        var root = directory?.FullName ?? throw new InvalidOperationException("no workspace root above the test binaries");
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(root, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "line-counts.json")));
        return [.. document.RootElement.Clone().GetProperty("keys").EnumerateArray()];
    }
}

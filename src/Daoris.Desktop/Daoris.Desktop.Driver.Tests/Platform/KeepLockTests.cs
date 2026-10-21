using System.Globalization;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// CONFIGLOCK1 (D173's CONFIG1 note): a keeping write holds <c>&lt;file&gt;.lock</c> from its re-read to its rename, so a change another
/// writer makes in between is never lost. The stale rules are one table both twins read, <c>fixtures/driver-config-keeps.json</c>'s
/// <c>lock</c>, row for row with the CLI's <c>keeplock.test.ts</c>; <see cref="KeepLockProcessTests"/> holds it across the two runtimes.
/// </summary>
public sealed class KeepLockTests : IDisposable
{
    /// <summary>A process id no process has, as the CLI's twin uses 2147483647.</summary>
    private const int Gone = int.MaxValue;

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "daoris-keeplock-" + Guid.NewGuid().ToString("N")[..8]);

    public KeepLockTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [MemberData(nameof(Rows))]
    public void The_shared_table_judges_a_lock_as_the_terminal_does(string name, string text, bool live, bool stale) =>
        Assert.Equal((name, stale), (name, KeepLock.IsStale(text, Now, _ => live)));

    public static TheoryData<string, string, bool, bool> Rows()
    {
        using var table = JsonDocument.Parse(File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "driver-config-keeps.json")));
        var rows = new TheoryData<string, string, bool, bool>();
        foreach (var row in table.RootElement.GetProperty("lock").EnumerateArray())
        {
            var text = row.TryGetProperty("text", out var said)
                ? said.GetString()!
                : $"{{\"pid\":4242,\"since\":\"{Moment(Now - TimeSpan.FromSeconds(row.GetProperty("age").GetInt32()))}\"}}\n";
            var live = !row.TryGetProperty("pid", out var pid) || pid.GetString() != "gone";
            rows.Add(row.GetProperty("name").GetString()!, text, live, row.GetProperty("stale").GetBoolean());
        }

        return rows;
    }

    private static string Moment(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    /// <summary>A lock at <paramref name="file"/>'s place as a holder with this process id wrote it, its time now.</summary>
    private static string Plant(string file, int pid)
    {
        var text = $"{{\"pid\":{pid},\"since\":\"{Moment(DateTimeOffset.UtcNow)}\"}}\n";
        File.WriteAllText(KeepLock.PathOf(file), text);
        return text;
    }

    [Fact]
    public void The_lock_is_the_files_full_path_with_lock_after_it_there_only_while_held_saying_who_holds_it_since_when()
    {
        var file = Path.Combine(_folder, "driver.json");
        Assert.Equal(Path.GetFullPath(file) + ".lock", KeepLock.PathOf(file));
        using (KeepLock.Take(file))
        {
            using var said = JsonDocument.Parse(File.ReadAllText(KeepLock.PathOf(file)));
            Assert.Equal(Environment.ProcessId, said.RootElement.GetProperty("pid").GetInt32());
            Assert.Matches(@"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d\.\d{3}Z$", said.RootElement.GetProperty("since").GetString());
        }

        Assert.False(File.Exists(KeepLock.PathOf(file)));
    }

    [Fact]
    public void A_lock_a_live_writer_holds_is_waited_for_then_refused_naming_it_and_left_as_it_is()
    {
        var file = Path.Combine(_folder, "driver.json");
        var text = Plant(file, Environment.ProcessId);
        var refusal = Assert.Throws<IOException>(() => KeepLock.Take(file, TimeSpan.FromMilliseconds(100)));
        Assert.Contains($"process {Environment.ProcessId}", refusal.Message);
        Assert.Contains(KeepLock.PathOf(file), refusal.Message);
        Assert.Equal(text, File.ReadAllText(KeepLock.PathOf(file)));
    }

    [Fact]
    public void A_lock_whose_process_is_gone_is_removed_and_taken()
    {
        var file = Path.Combine(_folder, "driver.json");
        Plant(file, Gone);
        using (KeepLock.Take(file, TimeSpan.FromMilliseconds(100)))
        {
            using var said = JsonDocument.Parse(File.ReadAllText(KeepLock.PathOf(file)));
            Assert.Equal(Environment.ProcessId, said.RootElement.GetProperty("pid").GetInt32());
        }
    }

    [Fact]
    public void A_release_leaves_a_lock_another_holder_wrote_since()
    {
        var file = Path.Combine(_folder, "driver.json");
        var held = KeepLock.Take(file);
        var other = Plant(file, Gone);
        held.Dispose();
        Assert.Equal(other, File.ReadAllText(KeepLock.PathOf(file)));
    }

    /// <summary>
    /// 🔴 A writer holds the lock from its re-read to its rename, and renames what it read then with its own change; this edit, made
    /// meanwhile, waits for the lock, then reads the file again and keeps that writer's change. Without the lock it landed inside the
    /// other's window, and the other's rename dropped it.
    /// </summary>
    [Fact]
    public async Task A_driver_json_edit_waits_for_a_writer_holding_the_lock_and_neither_change_is_lost()
    {
        var path = Path.Combine(_folder, "driver.json");
        File.WriteAllText(path, "{\n  \"drivable\": [\n    \"engine\"\n  ],\n  \"notify\": true\n}\n");
        var config = DriverConfig.Load(path);

        var text = await HeldAcross(path, () => config.WithDrivable("orders", true).Save(path), read => read.Replace("\"notify\": true", "\"notify\": false"));

        Assert.Equal("{\n  \"drivable\": [\n    \"engine\",\n    \"orders\"\n  ],\n  \"notify\": false\n}\n", text);
    }

    [Fact]
    public async Task Another_home_files_edit_waits_for_a_writer_holding_the_lock_and_neither_change_is_lost()
    {
        var path = Path.Combine(_folder, "plugins.json");
        File.WriteAllText(path, "{\n  \"disabled\": [\n    \"acme.pr\"\n  ]\n}\n");

        var text = await HeldAcross(
            path,
            () => JsonKeeping.Save(path, found: true, "{\"disabled\":[\"acme.pr\"]}", "{\"disabled\":[\"acme.pr\",\"acme.ci\"]}"),
            read => read.Replace("\n}", ",\n  \"theme\": \"dark\"\n}"));

        Assert.Equal("{\n  \"disabled\": [\n    \"acme.pr\",\n    \"acme.ci\"\n  ],\n  \"theme\": \"dark\"\n}\n", text);
    }

    /// <summary>
    /// The edit made while another writer of this process's id holds the lock (written as a holder writes it, so the gate is free), that
    /// writer renaming <paramref name="mine"/> of what it read then and letting go; the file as both leave it.
    /// </summary>
    private static async Task<string> HeldAcross(string path, Action edit, Func<string, string> mine)
    {
        Plant(path, Environment.ProcessId);
        var read = File.ReadAllText(path);
        var editing = Task.Run(edit);
        await Task.Delay(400);
        Assert.False(editing.IsCompleted, "the edit was made while another writer held the lock");
        File.WriteAllText(path, mine(read));
        File.Delete(KeepLock.PathOf(path));
        await editing;
        Assert.False(File.Exists(KeepLock.PathOf(path)));
        return File.ReadAllText(path);
    }
}

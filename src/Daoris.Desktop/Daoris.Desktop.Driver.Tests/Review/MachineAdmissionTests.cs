using System.Globalization;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// HEAVYQUEUE1: the machine's heavy-run admission, the driver's side. The merge queue's heavy gates and a driven session's heavy runs take
/// one place each in one file of the home before they start a heavy process, so the machine never runs more of them at once than it
/// admits; a run that waits says what for, and a holder that dies releases. Held cell for cell to ONE table,
/// <c>fixtures/machine-admission.json</c>, which <c>tools/machine-admission.test.mjs</c> carries too. Files only, no process.
/// </summary>
public sealed class MachineAdmissionTests : IDisposable
{
    private static readonly JsonElement Table = JsonDocument.Parse(File.ReadAllText(Path.Combine(
        WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "machine-admission.json"))).RootElement;

    private static readonly DateTimeOffset Now = DateTimeOffset.Parse(Table.GetProperty("now").GetString()!, CultureInfo.InvariantCulture);

    private readonly HeavyFixture _fixture = new("machine-admission");

    public void Dispose() => _fixture.Dispose();

    private static string Name(JsonElement row) => row.GetProperty("name").GetString()!;

    public static TheoryData<string, int, string?, int> CapacityRows()
    {
        var data = new TheoryData<string, int, string?, int>();
        foreach (var row in Table.GetProperty("capacity").EnumerateArray())
        {
            data.Add(Name(row), row.GetProperty("cpus").GetInt32(), row.GetProperty("slots").GetString(), row.GetProperty("expect").GetInt32());
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(CapacityRows))]
    public void The_capacity_is_the_shared_table_s(string name, int processors, string? slots, int expect)
    {
        Assert.True(name.Length > 0);
        Assert.Equal(expect, MachineAdmission.CapacityOf(variable => variable == MachineAdmission.CapacityVariable ? slots : null, processors));
    }

    /// <summary>An entry as the table writes one: <c>dead</c> is pid 0, which the table's <c>alive</c> reads as a process that is gone.</summary>
    private static AdmissionEntry Entry(JsonElement row) => new(
        row.TryGetProperty("id", out var id) ? id.GetString()! : "x", "session", row.TryGetProperty("dead", out var dead) && dead.GetBoolean() ? 0 : 4242, "a test run",
        Now.AddSeconds(row.TryGetProperty("since", out var since) ? since.GetInt32() : -10),
        Now.AddSeconds(-(row.TryGetProperty("beatAgo", out var beat) ? beat.GetInt32() : 5)), null, null, null);

    private static bool Alive(int pid) => pid != 0;

    public static TheoryData<string> LiveRows()
    {
        var data = new TheoryData<string>();
        foreach (var row in Table.GetProperty("live").EnumerateArray()) data.Add(Name(row));
        return data;
    }

    [Theory]
    [MemberData(nameof(LiveRows))]
    public void An_entry_counts_while_its_process_runs_and_it_beats(string name)
    {
        var row = Table.GetProperty("live").EnumerateArray().Single(each => Name(each) == name);
        Assert.Equal(row.GetProperty("expect").GetBoolean(), MachineAdmission.Live(Entry(row), Now, Alive));
    }

    public static TheoryData<string> JudgeRows()
    {
        var data = new TheoryData<string>();
        foreach (var row in Table.GetProperty("judge").EnumerateArray()) data.Add(Name(row));
        return data;
    }

    [Theory]
    [MemberData(nameof(JudgeRows))]
    public void A_run_is_admitted_as_the_shared_table_says(string name)
    {
        var row = Table.GetProperty("judge").EnumerateArray().Single(each => Name(each) == name);
        var (holds, waits) = MachineAdmission.Pruned(
            ([.. row.GetProperty("holds").EnumerateArray().Select(Entry)], [.. row.GetProperty("waits").EnumerateArray().Select(Entry)]), Now, Alive);

        var (admitted, ahead) = MachineAdmission.Judge(holds, waits, row.GetProperty("id").GetString()!, row.GetProperty("capacity").GetInt32());

        var expect = row.GetProperty("expect");
        Assert.Equal(expect.GetProperty("admitted").GetBoolean(), admitted);
        Assert.Equal(expect.GetProperty("ahead").GetInt32(), ahead);
        Assert.Equal(expect.GetProperty("holds").GetInt32(), holds.Count);
    }

    public static TheoryData<string> WholeRows()
    {
        var data = new TheoryData<string>();
        foreach (var row in Table.GetProperty("whole").EnumerateArray()) data.Add(Name(row));
        return data;
    }

    /// <summary>
    /// INTEGRATE1b (design §4.5, C7): the merge tool's whole take is N ordinary entries with one <c>since</c> and ids that sort together.
    /// The driver reads such a record as the tool does: the machine is full while they are held, a session's ask waits behind every one of
    /// them and is admitted once they are let go, and a live hold that is not among them is what the tool reads as a doubt.
    /// </summary>
    [Theory]
    [MemberData(nameof(WholeRows))]
    public void A_whole_take_reads_as_the_shared_table_says(string name)
    {
        var row = Table.GetProperty("whole").EnumerateArray().Single(each => Name(each) == name);
        var (holds, waits) = MachineAdmission.Pruned(
            ([.. row.GetProperty("holds").EnumerateArray().Select(Entry)], [.. row.GetProperty("waits").EnumerateArray().Select(Entry)]), Now, Alive);
        var capacity = row.GetProperty("capacity").GetInt32();
        var ids = row.GetProperty("ids").EnumerateArray().Select(id => id.GetString()!).ToList();

        var (admitted, ahead) = MachineAdmission.Judge(holds, waits, row.GetProperty("id").GetString()!, capacity);

        var expect = row.GetProperty("expect");
        Assert.Equal(expect.GetProperty("admitted").GetBoolean(), admitted);
        Assert.Equal(expect.GetProperty("ahead").GetInt32(), ahead);
        Assert.Equal(expect.GetProperty("holds").GetInt32(), holds.Count);
        Assert.Equal(expect.GetProperty("full").GetBoolean(), holds.Count >= capacity);
        Assert.Equal(expect.GetProperty("lost").GetInt32(), ids.Count(id => holds.All(hold => hold.Id != id)));
        Assert.Equal(
            [.. expect.GetProperty("others").EnumerateArray().Select(id => id.GetString()!)],
            holds.Where(hold => !ids.Contains(hold.Id)).Select(hold => hold.Id));
    }

    private static AdmissionWaiting Quick(Func<int, bool>? alive = null) =>
        new(TimeSpan.FromMilliseconds(10), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5), () => DateTimeOffset.UtcNow, alive ?? (_ => true));

    private static string None(string name) => name == MachineAdmission.InsideVariable ? "" : "";

    /// <summary>HEAVYQUEUE1's contract: two holders, one waits, says what for, and starts when the first ends.</summary>
    [Fact]
    public async Task The_second_run_waits_saying_what_for_and_starts_when_the_first_ends()
    {
        var first = await MachineAdmission.TakeAsync(
            _fixture.Home, new AdmissionRequest("heavy gates `build`") { Session = "s-1" }, new SaidWriter(), CancellationToken.None, Quick(), 1, None);
        var said = new SaidWriter();

        var second = MachineAdmission.TakeAsync(
            _fixture.Home, new AdmissionRequest("dotnet test --filter Foo") { Session = "s-2" }, said, CancellationToken.None, Quick(), 1, None);

        await Poll.Until(() => said.Text.Contains("waiting for the machine's heavy-run admission"), () => said.Text);
        Assert.False(second.IsCompleted, "it must not start while the first holds the only place");
        Assert.Contains("before dotnet test --filter Foo: the machine runs 1 of 1 heavy runs: session s-1's heavy gates `build`", said.Text);
        Assert.Contains("it never times out", said.Text);
        var look = MachineAdmission.Look(_fixture.Home, _ => true, capacity: 1);
        Assert.Equal(["heavy gates `build`"], look.Holds.Select(entry => entry.What));
        Assert.Equal(["dotnet test --filter Foo"], look.Waits.Select(entry => entry.What));

        first.Dispose();
        using var held = await second.WaitAsync(Poll.Patience);

        Assert.Contains("the machine's heavy-run admission is this run's, after waiting", said.Text);
        Assert.Equal(["dotnet test --filter Foo"], MachineAdmission.Look(_fixture.Home, _ => true, capacity: 1).Holds.Select(entry => entry.What));
    }

    /// <summary>Letting go takes the entry out, once however often it is asked.</summary>
    [Fact]
    public async Task Letting_go_takes_the_entry_out_once()
    {
        var hold = await MachineAdmission.TakeAsync(_fixture.Home, new AdmissionRequest("a run"), new SaidWriter(), CancellationToken.None, Quick(), 1, None);
        var other = await MachineAdmission.TakeAsync(_fixture.Home, new AdmissionRequest("a run"), new SaidWriter(), CancellationToken.None, Quick(), 2, None);
        hold.Dispose();
        hold.Dispose();

        Assert.Single(MachineAdmission.Look(_fixture.Home, _ => true, capacity: 2).Holds);
        other.Dispose();
        var look = MachineAdmission.Look(_fixture.Home, _ => true, capacity: 2);
        Assert.Empty(look.Holds);
        Assert.Empty(look.Waits);
    }

    /// <summary>A holder that dies releases: its entry, written by a process that is gone, holds no place.</summary>
    [Fact]
    public async Task A_holder_whose_process_is_gone_holds_no_place()
    {
        var file = MachineAdmission.PathOf(_fixture.Home);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var now = DateTimeOffset.UtcNow;
        File.WriteAllText(file, MachineAdmission.Serialize(
            [new AdmissionEntry("0-dead", "queue", 0, "driver gate", now.AddMinutes(-9), now.AddSeconds(-1), null, null, null)], []));

        using var hold = await MachineAdmission.TakeAsync(
            _fixture.Home, new AdmissionRequest("dotnet test"), new SaidWriter(), CancellationToken.None, Quick(Alive), 1, None).WaitAsync(Poll.Patience);

        Assert.Equal(["dotnet test"], MachineAdmission.Look(_fixture.Home, Alive, capacity: 1).Holds.Select(entry => entry.What));
    }

    /// <summary>A holder that stopped beating releases too: a reused process id cannot hold a place past two minutes.</summary>
    [Fact]
    public async Task A_holder_that_stopped_beating_holds_no_place()
    {
        var file = MachineAdmission.PathOf(_fixture.Home);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var now = DateTimeOffset.UtcNow;
        File.WriteAllText(file, MachineAdmission.Serialize(
            [new AdmissionEntry("1-silent", "session", Environment.ProcessId, "dotnet test", now.AddMinutes(-30), now.AddMinutes(-5), "s-9", null, null)], []));

        using var hold = await MachineAdmission.TakeAsync(
            _fixture.Home, new AdmissionRequest("driver"), new SaidWriter(), CancellationToken.None, Quick(), 1, None).WaitAsync(Poll.Patience);

        Assert.Equal(["driver"], MachineAdmission.Look(_fixture.Home, _ => true, capacity: 1).Holds.Select(entry => entry.What));
    }

    /// <summary>A wait that is stopped holds nothing and leaves nothing waiting.</summary>
    [Fact]
    public async Task A_wait_that_is_stopped_leaves_nothing()
    {
        using var first = await MachineAdmission.TakeAsync(_fixture.Home, new AdmissionRequest("a"), new SaidWriter(), CancellationToken.None, Quick(), 1, None);
        using var stop = new CancellationTokenSource();
        var said = new SaidWriter();
        var second = MachineAdmission.TakeAsync(_fixture.Home, new AdmissionRequest("b"), said, stop.Token, Quick(), 1, None);
        await Poll.Until(() => said.Text.Contains("waiting for the machine"), () => said.Text);

        await stop.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second).WaitAsync(Poll.Patience);

        var look = MachineAdmission.Look(_fixture.Home, _ => true, capacity: 1);
        Assert.Equal(["a"], look.Holds.Select(entry => entry.What));
        Assert.Empty(look.Waits);
    }

    /// <summary>A run started inside a hold takes no second place, which would wait on the run that started it.</summary>
    [Fact]
    public async Task A_run_inside_a_hold_takes_no_second_place()
    {
        using var outer = await MachineAdmission.TakeAsync(_fixture.Home, new AdmissionRequest("a"), new SaidWriter(), CancellationToken.None, Quick(), 1, None);

        var inside = await MachineAdmission.TakeAsync(
            _fixture.Home, new AdmissionRequest("b"), new SaidWriter(), CancellationToken.None, Quick(), 1,
            name => name == MachineAdmission.InsideVariable ? outer.Id : null).WaitAsync(Poll.Patience);

        Assert.True(inside.IsInside);
        inside.Dispose();
        Assert.Single(MachineAdmission.Look(_fixture.Home, _ => true, capacity: 1).Holds);
    }

    /// <summary>What the merge tool writes reads here, and what is written here reads there: the queue's entry is named as the queue's.</summary>
    [Fact]
    public void The_merge_tool_s_entry_reads_and_is_named_in_the_sentence()
    {
        var file = MachineAdmission.PathOf(_fixture.Home);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, """
            { "schema": 1,
              "holds": [ { "id": "812-ab12cd34", "kind": "queue", "pid": 812, "what": "driver gate", "since": "2026-10-28T10:00:00.000Z", "beat": "2026-10-28T10:00:20.000Z" } ],
              "waits": [ { "id": "9-1", "kind": "session", "pid": 9, "what": "dotnet test", "since": "2026-10-28T10:00:30.000Z", "beat": "2026-10-28T10:00:31.000Z", "session": "s-4", "repository": "Daoris", "tree": "t" } ] }
            """);

        var (holds, waits) = MachineAdmission.Read(file);

        Assert.Equal("driver gate", Assert.Single(holds).What);
        Assert.Equal("queue", holds[0].Kind);
        Assert.Equal(("s-4", "Daoris"), (Assert.Single(waits).Session, waits[0].Repository));
        Assert.Equal(
            "the machine runs 1 of 3 heavy runs: the merge queue's driver gate (2m 5s); 2 runs asked before this one",
            MachineAdmission.Sentence(holds, 2, 3, DateTimeOffset.Parse("2026-10-28T10:02:05Z", CultureInfo.InvariantCulture)));
    }

    /// <summary>An entry that does not read as one counts for nothing, and a file that does not read is an empty machine.</summary>
    [Fact]
    public void What_does_not_read_counts_for_nothing()
    {
        var file = MachineAdmission.PathOf(_fixture.Home);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, """{ "holds": [ { "id": "a" }, null, 7, { "id": "b", "kind": "queue", "pid": 1, "what": "x", "since": "never", "beat": "never" } ], "waits": "no" }""");
        var (holds, waits) = MachineAdmission.Read(file);
        Assert.Empty(holds);
        Assert.Empty(waits);

        File.WriteAllText(file, "{ not json");
        Assert.Empty(MachineAdmission.Read(file).Holds);
        Assert.Empty(MachineAdmission.Read(Path.Combine(_fixture.Folder, "missing.json")).Holds);
    }

    /// <summary>The file is laid out as the merge tool lays it out, so either reads the other's.</summary>
    [Fact]
    public void The_file_is_written_whole_with_the_merge_tool_s_fields()
    {
        var entry = new AdmissionEntry("1-aa", "session", 1, "dotnet test", Now, Now.AddSeconds(3), "s-1", "Daoris", "t");

        using var document = JsonDocument.Parse(MachineAdmission.Serialize([entry], []));

        var written = document.RootElement.GetProperty("holds")[0];
        Assert.Equal(1, document.RootElement.GetProperty("schema").GetInt32());
        Assert.Equal("2026-10-28T10:00:00.000Z", written.GetProperty("since").GetString());
        Assert.Equal("2026-10-28T10:00:03.000Z", written.GetProperty("beat").GetString());
        Assert.Equal(
            ["id", "kind", "pid", "what", "since", "beat", "session", "repository", "tree"], written.EnumerateObject().Select(property => property.Name));
        Assert.Empty(document.RootElement.GetProperty("waits").EnumerateArray());
    }
}

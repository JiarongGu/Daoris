using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// INTEGRATE1f part f2: the driver's whole take of the machine's admission, the twin of <c>tools/machine-admission.mjs</c>'s
/// <c>takeWhole</c> (the integration design §4.5, C7, F34). N ordinary entries of today's schema go in one locked write, first come, so a
/// take waits behind the live holds; a live hold not its own that shows while it runs makes it inconclusive; its own process's place is
/// let go before it. Files only, no process: the entries of another process are written with a pid the test says is alive.
/// </summary>
public sealed class WholeAdmissionTests : IDisposable
{
    private readonly HeavyFixture _fixture = new("whole-admission");

    public void Dispose() => _fixture.Dispose();

    private static AdmissionWaiting Quick() =>
        new(TimeSpan.FromMilliseconds(10), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5), () => DateTimeOffset.UtcNow, _ => true);

    private static string None(string name) => name == MachineAdmission.InsideVariable ? "" : "";

    private static AdmissionEntry Other(string id, string kind = "session") =>
        new(id, kind, 4242, "a session's heavy run", DateTimeOffset.UtcNow.AddSeconds(-30), DateTimeOffset.UtcNow, "s-1", null, null);

    private void Write(IReadOnlyList<AdmissionEntry> holds, IReadOnlyList<AdmissionEntry>? waits = null)
    {
        var file = MachineAdmission.PathOf(_fixture.Home);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, MachineAdmission.Serialize(holds, waits ?? []));
    }

    private Task<WholeTake> Take(int capacity, Func<bool>? halted = null, SaidWriter? said = null, CancellationToken ct = default) =>
        MachineAdmission.TakeWholeAsync(
            _fixture.Home, new AdmissionRequest("a confirm run") { Session = "s-9", Repository = "Daoris" }, said ?? new SaidWriter(), halted ?? (() => false), ct,
            Quick(), capacity, None);

    private AdmissionLook Look(int capacity) => MachineAdmission.Look(_fixture.Home, _ => true, capacity: capacity);

    /// <summary>The take of N: every place the machine admits is one ordinary entry, the kind the tool's is, sharing one <c>since</c>.</summary>
    [Fact]
    public async Task A_free_machine_is_taken_as_that_many_ordinary_entries_of_the_queue_kind()
    {
        var taken = await Take(3).WaitAsync(Poll.Patience);

        Assert.True(taken.Held, taken.Why);
        Assert.Equal(3, taken.Ids.Count);
        var look = Look(3);
        Assert.Equal(taken.Ids, look.Holds.Select(entry => entry.Id).Order(StringComparer.Ordinal));
        Assert.All(look.Holds, entry =>
        {
            Assert.Equal("queue", entry.Kind);
            Assert.Equal(Environment.ProcessId, entry.Pid);
            Assert.Equal(("s-9", "Daoris"), (entry.Session, entry.Repository));
        });
        Assert.Single(look.Holds.Select(entry => entry.Since).Distinct());
        Assert.Equal(["a confirm run (place 1 of 3)", "a confirm run (place 2 of 3)", "a confirm run (place 3 of 3)"], look.Holds.OrderBy(entry => entry.Id, StringComparer.Ordinal).Select(entry => entry.What));
        Assert.All(taken.Ids, id => Assert.Matches($@"^{Environment.ProcessId}-[0-9a-f]{{8}}-0[1-3]$", id));
        Assert.Empty(look.Waits);

        var evidence = taken.End!();

        Assert.True(evidence!.Conclusive, string.Join("; ", evidence.Doubts.Select(doubt => doubt.Why)));
        Assert.Equal(taken.Ids, evidence.Ids);
        Assert.Empty(Look(3).Holds);
    }

    /// <summary>A session's ask beside the whole take waits behind every one of its places, and is admitted once they are let go.</summary>
    [Fact]
    public async Task A_session_s_ask_waits_behind_every_place_and_is_admitted_when_they_are_let_go()
    {
        var taken = await Take(2).WaitAsync(Poll.Patience);
        Assert.True(taken.Held, taken.Why);
        var said = new SaidWriter();

        var session = MachineAdmission.TakeAsync(
            _fixture.Home, new AdmissionRequest("dotnet test --filter Foo") { Session = "s-2" }, said, CancellationToken.None, Quick(), 2, None);
        await Poll.Until(() => said.Text.Contains("waiting for the machine", StringComparison.Ordinal), () => said.Text);
        Assert.False(session.IsCompleted);
        Assert.Contains("the merge queue's a confirm run (place 1 of 2)", said.Text);

        Assert.True(taken.End!()!.Conclusive);
        using var held = await session.WaitAsync(Poll.Patience);
        Assert.Equal(["dotnet test --filter Foo"], Look(2).Holds.Select(entry => entry.What));
    }

    /// <summary>First come, first served: a take behind a live hold waits for it, with its places standing together in the waits.</summary>
    [Fact]
    public async Task A_take_waits_behind_a_live_hold_and_starts_once_it_is_let_go()
    {
        Write([Other("h-1")]);
        var said = new SaidWriter();

        var taking = Take(2, said: said);
        await Poll.Until(() => said.Text.Contains("whole heavy-run admission", StringComparison.Ordinal), () => said.Text);

        Assert.False(taking.IsCompleted, "the whole admission cannot be held while a session's run holds a place");
        var look = Look(2);
        Assert.Contains("h-1", look.Holds.Select(entry => entry.Id));
        Assert.Contains("session s-1's a session's heavy run", said.Text);
        Assert.Contains("the merge queue's a confirm run (place 1 of 2)", said.Text);

        var (holds, waits) = MachineAdmission.Read(MachineAdmission.PathOf(_fixture.Home));
        Write([.. holds.Where(entry => entry.Id != "h-1")], waits);
        var taken = await taking.WaitAsync(Poll.Patience);

        Assert.True(taken.Held, taken.Why);
        var evidence = taken.End!();
        Assert.True(evidence!.Conclusive, string.Join("; ", evidence.Doubts.Select(doubt => doubt.Why)));
        Assert.True(evidence.WaitedMs >= 0);
    }

    /// <summary>A live hold that is not its own, at its end, is the doubt that makes the confirm run inconclusive.</summary>
    [Fact]
    public async Task A_live_hold_not_its_own_at_its_end_makes_it_inconclusive_and_every_place_is_let_go()
    {
        var taken = await Take(2).WaitAsync(Poll.Patience);
        Assert.True(taken.Held, taken.Why);
        var file = MachineAdmission.PathOf(_fixture.Home);
        var (holds, waits) = MachineAdmission.Read(file);
        Write([.. holds, Other("n-1", "queue")], waits);

        var evidence = taken.End!();

        Assert.False(evidence!.Conclusive);
        var doubt = Assert.Single(evidence.Doubts);
        Assert.Equal("end", doubt.At);
        Assert.Equal("another live hold stands beside it: the merge queue's a session's heavy run", doubt.Why);
        Assert.Equal(["n-1"], Look(2).Holds.Select(entry => entry.Id));
    }

    /// <summary>A place that left the record while it was held is a doubt too, and ending twice answers the same evidence.</summary>
    [Fact]
    public async Task A_place_no_longer_held_is_a_doubt_and_ending_twice_answers_the_same()
    {
        var taken = await Take(2).WaitAsync(Poll.Patience);
        Assert.True(taken.Held, taken.Why);
        var (holds, waits) = MachineAdmission.Read(MachineAdmission.PathOf(_fixture.Home));
        Write([holds[0]], waits);

        var first = taken.End!();
        var second = taken.End!();

        Assert.False(first!.Conclusive);
        Assert.StartsWith("1 of its 2 places is no longer held (", Assert.Single(first.Doubts).Why, StringComparison.Ordinal);
        Assert.Same(first, second);
        Assert.Empty(Look(2).Holds);
    }

    /// <summary>The failing gate's own place is let go before the take: a take beside its own process's place would wait on itself, so it refuses at once.</summary>
    [Fact]
    public async Task A_process_that_holds_a_place_cannot_take_the_whole_machine_and_writes_nothing()
    {
        using var own = await MachineAdmission.TakeAsync(_fixture.Home, new AdmissionRequest("the failing gate"), new SaidWriter(), CancellationToken.None, Quick(), 3, None);
        var before = File.ReadAllText(MachineAdmission.PathOf(_fixture.Home));

        var taken = await Take(3).WaitAsync(Poll.Patience);

        Assert.False(taken.Held);
        Assert.False(taken.Dropped);
        Assert.Contains("its own process holds or asks a place already (the failing gate)", taken.Why);
        Assert.False(taken.Evidence!.Conclusive);
        Assert.Equal("take", Assert.Single(taken.Evidence.Doubts).At);
        Assert.Equal(before, File.ReadAllText(MachineAdmission.PathOf(_fixture.Home)));
    }

    /// <summary>A hold that does not read as an entry means who holds the machine is unknown: refused, nothing written.</summary>
    [Fact]
    public async Task A_hold_that_does_not_read_refuses_the_take()
    {
        var file = MachineAdmission.PathOf(_fixture.Home);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, """{ "schema": 1, "holds": [ { "id": "x" } ], "waits": [] }""");

        var taken = await Take(2).WaitAsync(Poll.Patience);

        Assert.False(taken.Held);
        Assert.Equal("the record keeps 1 hold that does not read as an entry, so who holds the machine is unknown", taken.Why);
        Assert.Contains("\"id\": \"x\"", File.ReadAllText(file));
    }

    /// <summary>A record that does not read at all is a doubt, not an empty machine: the weak reading would show the machine free.</summary>
    [Fact]
    public async Task A_record_that_does_not_read_refuses_the_take()
    {
        var file = MachineAdmission.PathOf(_fixture.Home);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, "{ not json");

        var taken = await Take(2).WaitAsync(Poll.Patience);

        Assert.False(taken.Held);
        Assert.StartsWith("the machine's record could not be written (", taken.Why, StringComparison.Ordinal);
    }

    /// <summary>A run inside a hold cannot take the whole machine: its own hold is a place it cannot give up.</summary>
    [Fact]
    public async Task A_run_inside_a_hold_refuses_the_take()
    {
        Write([Other("outer")]);

        var taken = await MachineAdmission.TakeWholeAsync(
            _fixture.Home, new AdmissionRequest("a confirm run"), new SaidWriter(), () => false, CancellationToken.None, Quick(), 2,
            name => name == MachineAdmission.InsideVariable ? "outer" : null).WaitAsync(Poll.Patience);

        Assert.False(taken.Held);
        Assert.Equal("it runs inside the hold outer, which holds a place it cannot take", taken.Why);
    }

    /// <summary>A drop ends the wait: every place it asked is let go and the take answers dropped, not inconclusive.</summary>
    [Fact]
    public async Task A_drop_while_it_waits_lets_every_place_go_and_answers_dropped()
    {
        Write([Other("h-1")]);
        var drop = false;
        var said = new SaidWriter();

        var taking = Take(2, () => Volatile.Read(ref drop), said);
        await Poll.Until(() => said.Text.Contains("whole heavy-run admission", StringComparison.Ordinal), () => said.Text);
        Volatile.Write(ref drop, true);
        var taken = await taking.WaitAsync(Poll.Patience);

        Assert.False(taken.Held);
        Assert.True(taken.Dropped);
        var look = Look(2);
        Assert.Equal(["h-1"], look.Holds.Select(entry => entry.Id));
        Assert.Empty(look.Waits);
    }

    /// <summary>The beat touches only its own places, and one a newcomer showed beside is a doubt at the next beat.</summary>
    [Fact]
    public async Task A_beat_keeps_its_places_and_reads_the_record_again()
    {
        var waiting = new AdmissionWaiting(TimeSpan.FromMilliseconds(10), TimeSpan.FromMinutes(5), TimeSpan.FromMilliseconds(40), () => DateTimeOffset.UtcNow, _ => true);
        var taken = await MachineAdmission.TakeWholeAsync(
            _fixture.Home, new AdmissionRequest("a confirm run"), new SaidWriter(), () => false, CancellationToken.None, waiting, 2, None).WaitAsync(Poll.Patience);
        Assert.True(taken.Held, taken.Why);
        var file = MachineAdmission.PathOf(_fixture.Home);
        var first = MachineAdmission.Read(file).Holds.Min(entry => entry.Beat);

        await Poll.Until(() => MachineAdmission.Read(file).Holds.Min(entry => entry.Beat) > first, () => "the places were not beaten");
        // Under the record's own lock: the beat is a writer too, and an edit beside it would be lost.
        MachineAdmission.Change(file, waiting, (holds, waits) => ([.. holds, Other("n-2")], waits));
        var added = MachineAdmission.Read(file).Holds.Min(entry => entry.Beat);
        await Poll.Until(() => MachineAdmission.Read(file).Holds.Where(entry => entry.Id != "n-2").Min(entry => entry.Beat) > added, () => "the beat did not run again");
        var evidence = taken.End!();

        Assert.False(evidence!.Conclusive);
        Assert.Contains(evidence.Doubts, doubt => doubt.At is "beat" or "end" && doubt.Why.StartsWith("another live hold stands beside it", StringComparison.Ordinal));
    }

    /// <summary>What the tool writes is read here as it reads its own: a record the take wrote parses as the plain schema.</summary>
    [Fact]
    public async Task The_record_the_take_writes_is_the_plain_schema()
    {
        var taken = await Take(2).WaitAsync(Poll.Patience);
        Assert.True(taken.Held, taken.Why);

        using var document = JsonDocument.Parse(File.ReadAllText(MachineAdmission.PathOf(_fixture.Home)));

        Assert.Equal(1, document.RootElement.GetProperty("schema").GetInt32());
        Assert.Equal(2, document.RootElement.GetProperty("holds").GetArrayLength());
        Assert.Equal(["id", "kind", "pid", "what", "since", "beat", "session", "repository"], document.RootElement.GetProperty("holds")[0].EnumerateObject().Select(property => property.Name));
        taken.End!();
    }
}

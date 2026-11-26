using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The pins an ask across repositories owes its consumer part: where a family's consumer and producer both hold a part of one ask,
/// the consumer's part lands with its pin moved to the commit the producer's part landed on its line. The members are <c>core</c> and
/// <c>web</c>: names, with nothing about what either is.
/// </summary>
public sealed class AskPinsTests : IDisposable
{
    private static readonly string A = new('a', 40);
    private static readonly string B = new('b', 40);
    private static readonly string PinOfB = $$"""{ "pins": [{ "producer": "core", "commit": "{{B}}" }] }""";

    private readonly string _folder = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "ask-pins", Guid.NewGuid().ToString("N")[..8]);
    private readonly Doors _doors = new();

    public void Dispose() => GitSeed.Discard(_folder);

    private sealed class Doors : IAskPinDoors
    {
        public readonly Dictionary<string, string?> Landed = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, LockText> Locks = new(StringComparer.OrdinalIgnoreCase);
        public readonly List<(string Consumer, string Producer, string Commit)> Written = [];
        public string WrittenKind = TrainPinKinds.Written;

        public Task<string?> LandedAsync(RepoView producer, string ask, CancellationToken ct) => Task.FromResult(Landed.GetValueOrDefault(producer.Repository));

        public Task<LockText> LockAsync(RepoView consumer, string ask, CancellationToken ct) =>
            Task.FromResult(Locks.GetValueOrDefault(consumer.Repository, new LockText(true, null)));

        public Task<TrainPinned> WriteAsync(RepoView consumer, string ask, string producer, string commit, CancellationToken ct)
        {
            Written.Add((consumer.Repository, producer, commit));
            return Task.FromResult(new TrainPinned(WrittenKind, $"pinned {producer}"));
        }
    }

    private IReadOnlyList<RepoView> Family(string family = "{ \"layers\": [[\"core\"], [\"web\"]] }")
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "daoris.family.json"), family);
        foreach (var (name, uses) in new[] { ("core", "[]"), ("web", "[\"core\"]") })
        {
            Directory.CreateDirectory(Path.Combine(_folder, name));
            File.WriteAllText(Path.Combine(_folder, name, "daoris.json"), $$"""{ "domain": { "uses": {{uses}} } }""");
        }

        return [new RepoView("core", true, Path.Combine(_folder, "core")), new RepoView("web", true, Path.Combine(_folder, "web"))];
    }

    private static PartsReport Report(IReadOnlySet<string> landed, params (string Part, string Repository)[] parts) =>
        new("a1", 1, [.. parts.Select(each => each.Part)], landed) { Repositories = parts.ToDictionary(each => each.Part, each => each.Repository) };

    private static readonly (string, string)[] TwoParts = [("p1", "core"), ("p2", "web")];

    [Fact]
    public async Task A_producers_landed_part_is_owed_to_the_consumer_as_its_pin_where_the_branch_pins_nothing()
    {
        var registry = Family();
        _doors.Landed["core"] = B;

        var pins = await AskPins.ReadAsync(registry, "web", "a1", Report(new HashSet<string>(), TwoParts).Repositories.Values, _doors, default);

        var pin = Assert.Single(pins);
        Assert.Equal((new PinEdge("web", "core"), B, AskPinStates.Owed), (pin.Edge, pin.Landed, pin.State));
    }

    [Fact]
    public async Task A_producer_whose_part_has_not_landed_on_its_line_is_waited_for_and_owes_no_commit()
    {
        var registry = Family();

        var pin = Assert.Single(await AskPins.ReadAsync(registry, "web", "a1", ["core", "web"], _doors, default));

        Assert.Equal(AskPinStates.Waiting, pin.State);
        Assert.Null(pin.Landed);
    }

    [Fact]
    public async Task A_branch_whose_lock_names_the_landed_commit_has_the_pin_moved()
    {
        var registry = Family();
        _doors.Landed["core"] = B;
        _doors.Locks["web"] = new LockText(true, PinOfB);

        var pin = Assert.Single(await AskPins.ReadAsync(registry, "web", "a1", ["core", "web"], _doors, default));

        Assert.Equal(AskPinStates.Pinned, pin.State);
    }

    [Fact]
    public async Task A_branch_pinning_an_older_commit_is_owed_the_landed_one()
    {
        var registry = Family();
        _doors.Landed["core"] = B;
        _doors.Locks["web"] = new LockText(true, PinOfB.Replace(B, A, StringComparison.Ordinal));

        var pin = Assert.Single(await AskPins.ReadAsync(registry, "web", "a1", ["core", "web"], _doors, default));

        Assert.Equal((AskPinStates.Owed, A), (pin.State, pin.Pinned));
    }

    [Fact]
    public async Task A_lock_that_is_no_lock_is_left_as_it_is_and_waited_on()
    {
        var registry = Family();
        _doors.Landed["core"] = B;
        _doors.Locks["web"] = new LockText(true, "not a lock");

        var pin = Assert.Single(await AskPins.ReadAsync(registry, "web", "a1", ["core", "web"], _doors, default));

        Assert.Equal(AskPinStates.Unreadable, pin.State);
    }

    [Fact]
    public async Task Only_a_producer_the_plan_gives_a_part_to_and_the_family_lets_the_consumer_use_is_a_pin()
    {
        var registry = Family();
        _doors.Landed["core"] = B;

        Assert.Empty(await AskPins.ReadAsync(registry, "web", "a1", ["web"], _doors, default));
        Assert.Empty(await AskPins.ReadAsync(registry, "web", "a1", ["other", "web"], _doors, default));
        Assert.Empty(await AskPins.ReadAsync(registry, "core", "a1", ["core", "web"], _doors, default));
        Assert.Empty(await AskPins.ReadAsync(
            Family("{ \"layers\": [[\"core\"], [\"web\"]], \"denied\": [{ \"consumer\": \"web\", \"producer\": \"core\" }] }"), "web", "a1", ["core", "web"], _doors, default));
    }

    [Fact]
    public async Task A_folder_with_no_family_file_has_no_pins()
    {
        var registry = Family();
        File.Delete(Path.Combine(_folder, "daoris.family.json"));
        _doors.Landed["core"] = B;

        Assert.Empty(await AskPins.ReadAsync(registry, "web", "a1", ["core", "web"], _doors, default));
    }

    [Fact]
    public async Task The_pass_moves_the_pin_once_the_consumers_own_parts_landed_and_not_before()
    {
        var registry = Family();
        _doors.Landed["core"] = B;
        var runs = new[] { new AskRunOf("a1", "web") };

        var early = await AskPinning.RunAsync(registry, Reports(landed: ["p1"]), runs, _doors, default);
        Assert.Empty(_doors.Written);
        Assert.Empty(early);

        var done = await AskPinning.RunAsync(registry, Reports(landed: ["p1", "p2"]), runs, _doors, default);
        Assert.Equal([("web", "core", B)], _doors.Written);
        Assert.Contains("pinned core", Assert.Single(done));
    }

    [Fact]
    public async Task The_pass_writes_nothing_for_a_pin_already_moved_a_producer_not_landed_or_a_lock_that_is_no_lock()
    {
        var registry = Family();
        var runs = new[] { new AskRunOf("a1", "web") };

        await AskPinning.RunAsync(registry, Reports(landed: ["p1", "p2"]), runs, _doors, default);
        _doors.Landed["core"] = B;
        _doors.Locks["web"] = new LockText(true, PinOfB);
        await AskPinning.RunAsync(registry, Reports(landed: ["p1", "p2"]), runs, _doors, default);
        _doors.Locks["web"] = new LockText(true, "not a lock");
        await AskPinning.RunAsync(registry, Reports(landed: ["p1", "p2"]), runs, _doors, default);

        Assert.Empty(_doors.Written);
    }

    [Fact]
    public async Task What_an_open_run_carries_is_the_commit_its_pin_names_or_is_owed_and_nothing_where_it_waits()
    {
        var registry = Family();
        var runs = new[] { new AskRunOf("a1", "web") };

        Assert.Empty(await AskPinning.CarriedAsync(registry, Reports(landed: []), runs, _doors, default));

        _doors.Landed["core"] = B;
        var carried = await AskPinning.CarriedAsync(registry, Reports(landed: []), runs, _doors, default);

        Assert.Equal(B, carried[new PinEdge("web", "core")]);
    }

    [Fact]
    public async Task The_parts_node_waits_for_the_pin_after_every_part_landed_and_is_reached_once_it_is_moved()
    {
        var registry = Family();
        var runs = new[] { new AskRunOf("a1", "web") };
        var node = new NodeDeclaration("parts", NodeKinds.Parts, 1, NodeReach.Observed);
        var self = new WorkUnit("run:r", WorkUnitKind.Run, new WorkOwner("web", "default", null, WorkRecord.DriverWriter),
            new WorkSubject("r", null), new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "running"), []);
        var binding = new NodeAcceptance("run:r", null, "graph-1", "parts", 1, 1);
        async Task<NodeObservation> ReadAsync()
        {
            var waits = await AskPinning.WaitsAsync(registry, Reports(landed: ["p1", "p2"]), runs, _doors, default);
            var report = (await AskPinning.WithWaitsAsync(registry, Reports(landed: ["p1", "p2"]), runs, _doors, default))["a1"];
            Assert.Equal(waits.ContainsKey("a1"), report.PinWaits.Count > 0);
            return PartsReading.Observe(new NodeReading(self, node, binding) { Parts = report.For("web") });
        }

        var waiting = await ReadAsync();
        Assert.Null(waiting.Outcome);
        Assert.Contains("core", waiting.Said);

        _doors.Landed["core"] = B;
        Assert.Null((await ReadAsync()).Outcome);
        Assert.Contains("pin", (await ReadAsync()).Said);

        _doors.Locks["web"] = new LockText(true, PinOfB);
        Assert.Equal(NodeStates.Reached, (await ReadAsync()).Outcome);
    }

    private static IReadOnlyDictionary<string, PartsReport> Reports(string[] landed) =>
        new Dictionary<string, PartsReport> { ["a1"] = Report(new HashSet<string>(landed), TwoParts) };
}

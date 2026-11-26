using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// One look's fan-out over a family on disk: a producer's landing opens its consumers' pin-move quests, a second landing retargets them, a
/// breaking one keeps the old pin and files the break quest, and nothing is ever written into a consumer's checkout. The members are
/// <c>core</c> and <c>web</c>: names, with nothing about what either is.
/// </summary>
public sealed class FamilyFanoutTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);
    private static readonly string A = new('a', 40);
    private static readonly string B = new('b', 40);
    private static readonly string C = new('c', 40);

    private readonly string _folder = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "family-fanout", Guid.NewGuid().ToString("N")[..8]);
    private readonly Doors _doors = new();

    public void Dispose() => GitSeed.Discard(_folder);

    private sealed class Doors : IFamilyDoors
    {
        public readonly List<RequestAsk> Published = [];
        public readonly List<(string Id, string Title, string Body)> Revised = [];
        public bool Refuse;

        public Task<RequestPublished> PublishAsync(RequestAsk ask, CancellationToken ct)
        {
            Published.Add(ask);
            return Task.FromResult(Refuse ? new RequestPublished(false, "refused", "no", null) : new RequestPublished(true, null, "ok", new QuestView("q" + Published.Count, ask.From, ask.To, ask.Title, ask.Body, "Open")));
        }

        public Task<(bool Ok, string Message)> ReviseWordsAsync(string id, string title, string body, CancellationToken ct)
        {
            Revised.Add((id, title, body));
            return Task.FromResult((true, "ok"));
        }
    }

    private IReadOnlyList<RepoView> Family(string family = "{ \"layers\": [[\"core\"], [\"web\"]], \"edges\": [{ \"consumer\": \"web\", \"producer\": \"core\", \"lag\": \"3d\" }] }", string? pin = null)
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "daoris.family.json"), family);
        foreach (var (name, uses) in new[] { ("core", "[]"), ("web", "[\"core\"]") })
        {
            Directory.CreateDirectory(Path.Combine(_folder, name));
            File.WriteAllText(Path.Combine(_folder, name, "daoris.json"), $$"""{ "domain": { "uses": {{uses}} } }""");
        }

        if (pin is not null) File.WriteAllText(Path.Combine(_folder, "web", "daoris.family.lock"), $$"""{ "pins": [{ "producer": "core", "commit": "{{pin}}" }] }""");
        return [new RepoView("core", true, Path.Combine(_folder, "core")), new RepoView("web", true, Path.Combine(_folder, "web"))];
    }

    private static Func<RepoView, CancellationToken, Task<string?>> Tips(string core) =>
        (repo, _) => Task.FromResult<string?>(repo.Repository == "core" ? core : "f".PadLeft(40, 'f'));

    private static Task<LockText> Locks(RepoView repo, CancellationToken ct)
    {
        var file = Path.Combine(repo.Root!, "daoris.family.lock");
        return Task.FromResult(new LockText(true, File.Exists(file) ? File.ReadAllText(file) : null));
    }

    private string Snapshot() =>
        string.Join("\n", Directory.GetFiles(Path.Combine(_folder, "web"), "*", SearchOption.AllDirectories).Order().Select(file => file + "=" + File.ReadAllText(file)));

    [Fact]
    public async Task A_landing_opens_the_consumers_quest_from_the_producer_and_writes_nothing_into_the_consumer()
    {
        var registry = Family(pin: A);
        var before = Snapshot();

        var round = await FamilyFanout.RunAsync(registry, [], _doors, Tips(B), Locks, T0, default);

        var ask = Assert.Single(_doors.Published);
        Assert.Equal(("core", "web", PinMoves.KeyOf(new PinEdge("web", "core"), 1)), (ask.From, ask.To, ask.Key));
        Assert.Contains(B, ask.Body);
        Assert.Empty(_doors.Revised);
        Assert.Equal(before, Snapshot());
        Assert.Contains(round.Said, line => line.Contains("web", StringComparison.Ordinal) && line.Contains("core", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_second_landing_retargets_that_quest_and_opens_no_other()
    {
        var registry = Family(pin: A);
        var words = PinMoves.Words(new PinEdge("web", "core"), B);
        var open = new QuestView("q1", "core", "web", words.Title, words.Body, "Open") { Request = PinMoves.KeyOf(new PinEdge("web", "core"), 1) };

        await FamilyFanout.RunAsync(registry, [open], _doors, Tips(C), Locks, T0, default);

        Assert.Empty(_doors.Published);
        var moved = Assert.Single(_doors.Revised);
        Assert.Equal("q1", moved.Id);
        Assert.Contains(C, moved.Body);
        Assert.DoesNotContain(B, moved.Body);
    }

    [Fact]
    public async Task A_consumer_already_at_the_producers_line_is_owed_nothing_and_the_look_repeated_changes_nothing()
    {
        var registry = Family(pin: B);

        await FamilyFanout.RunAsync(registry, [], _doors, Tips(B), Locks, T0, default);
        var open = new QuestView("q1", "core", "web", "t", PinMoves.Words(new PinEdge("web", "core"), C).Body, "Open") { Request = PinMoves.KeyOf(new PinEdge("web", "core"), 1) };
        await FamilyFanout.RunAsync(registry, [open], _doors, Tips(C), Locks, T0, default);

        Assert.Empty(_doors.Published);
        Assert.Empty(_doors.Revised);
    }

    [Fact]
    public async Task A_breaking_landing_keeps_the_old_pin_and_files_the_break_quest_to_the_producer_once()
    {
        var registry = Family(pin: A);
        var edge = new PinEdge("web", "core");
        var red = new QuestView("q1", "core", "web", "t", PinMoves.Words(edge, B).Body, "Declined")
        {
            Request = PinMoves.KeyOf(edge, 1), Note = "the frame cap test fails", Updated = T0,
        };
        var before = Snapshot();

        await FamilyFanout.RunAsync(registry, [red], _doors, Tips(B), Locks, T0.AddHours(1), default);

        var broke = Assert.Single(_doors.Published);
        Assert.Equal(("web", "core", PinMoves.BreakKeyOf(edge, 1)), (broke.From, broke.To, broke.Key));
        Assert.Contains("the frame cap test fails", broke.Body);
        Assert.Equal(before, Snapshot());
        Assert.Contains($"\"commit\": \"{A}\"", File.ReadAllText(Path.Combine(_folder, "web", "daoris.family.lock")));

        var filed = new QuestView("b1", "web", "core", "t", "b", "Open") { Request = PinMoves.BreakKeyOf(edge, 1) };
        _doors.Published.Clear();
        await FamilyFanout.RunAsync(registry, [red, filed], _doors, Tips(B), Locks, T0.AddHours(2), default);
        Assert.Empty(_doors.Published);
    }

    [Fact]
    public async Task The_person_is_told_of_a_hold_only_past_the_edges_lag_bound()
    {
        var registry = Family(pin: A);
        var edge = new PinEdge("web", "core");
        var red = new QuestView("q1", "core", "web", "t", PinMoves.Words(edge, B).Body, "Declined") { Request = PinMoves.KeyOf(edge, 1), Updated = T0 };
        var filed = new QuestView("b1", "web", "core", "t", "b", "Open") { Request = PinMoves.BreakKeyOf(edge, 1) };

        var young = await FamilyFanout.RunAsync(registry, [red, filed], _doors, Tips(B), Locks, T0.AddDays(2), default);
        var old = await FamilyFanout.RunAsync(registry, [red, filed], _doors, Tips(B), Locks, T0.AddDays(4), default);

        Assert.Empty(young.Told);
        Assert.Equal(new FamilyHold("web", "core", B, T0, 1), Assert.Single(old.Told));
    }

    [Fact]
    public async Task A_hold_on_an_edge_with_no_declared_lag_tells_no_one()
    {
        var registry = Family("{ \"layers\": [[\"core\"], [\"web\"]] }", pin: A);
        var edge = new PinEdge("web", "core");
        var red = new QuestView("q1", "core", "web", "t", PinMoves.Words(edge, B).Body, "Declined") { Request = PinMoves.KeyOf(edge, 1), Updated = T0 };
        var filed = new QuestView("b1", "web", "core", "t", "b", "Open") { Request = PinMoves.BreakKeyOf(edge, 1) };

        var round = await FamilyFanout.RunAsync(registry, [red, filed], _doors, Tips(B), Locks, T0.AddDays(90), default);

        Assert.Empty(round.Told);
    }

    [Fact]
    public async Task No_family_file_no_family_and_a_file_of_the_wrong_shape_is_left_to_the_check()
    {
        var registry = Family();
        File.Delete(Path.Combine(_folder, "daoris.family.json"));
        await FamilyFanout.RunAsync(registry, [], _doors, Tips(B), Locks, T0, default);
        File.WriteAllText(Path.Combine(_folder, "daoris.family.json"), "{ \"layers\": 3 }");
        await FamilyFanout.RunAsync(registry, [], _doors, Tips(B), Locks, T0, default);

        Assert.Empty(_doors.Published);
        Assert.Empty(_doors.Revised);
    }

    [Fact]
    public async Task An_edge_the_check_would_refuse_is_not_fanned_out_along()
    {
        var registry = Family("{ \"layers\": [[\"web\"], [\"core\"]] }", pin: A);

        await FamilyFanout.RunAsync(registry, [], _doors, Tips(B), Locks, T0, default);

        Assert.Empty(_doors.Published);
    }

    [Fact]
    public async Task A_producer_whose_line_reads_no_commit_and_a_refused_publish_are_said_not_thrown()
    {
        var registry = Family(pin: A);
        await FamilyFanout.RunAsync(registry, [], _doors, (_, _) => Task.FromResult<string?>(null), Locks, T0, default);
        Assert.Empty(_doors.Published);

        _doors.Refuse = true;
        var round = await FamilyFanout.RunAsync(registry, [], _doors, Tips(B), Locks, T0, default);

        Assert.Contains(round.Said, line => line.Contains("refused", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task A_member_with_no_checkout_here_is_no_edge()
    {
        var registry = Family(pin: A);

        await FamilyFanout.RunAsync([registry[0], new RepoView("web", true, null)], [], _doors, Tips(B), Locks, T0, default);

        Assert.Empty(_doors.Published);
    }

    [Fact]
    public async Task A_lock_the_consumers_line_does_not_hold_is_no_pin()
    {
        var registry = Family(pin: B);

        await FamilyFanout.RunAsync(registry, [], _doors, Tips(B), (_, _) => Task.FromResult(new LockText(true, "{ \"pins\": [{ \"producer\": \"core\", \"commit\": \"" + A + "\" }] }")), T0, default);

        Assert.Equal(PinMoves.KeyOf(new PinEdge("web", "core"), 1), Assert.Single(_doors.Published).Key);
    }

    [Fact]
    public async Task A_line_that_cannot_be_read_acts_on_nothing_and_tells_nothing()
    {
        var registry = Family(pin: A);
        var edge = new PinEdge("web", "core");
        var red = new QuestView("q1", "core", "web", "t", PinMoves.Words(edge, B).Body, "Declined") { Request = PinMoves.KeyOf(edge, 1), Updated = T0 };
        var filed = new QuestView("b1", "web", "core", "t", "b", "Open") { Request = PinMoves.BreakKeyOf(edge, 1) };

        var round = await FamilyFanout.RunAsync(registry, [red, filed], _doors, Tips(B), (_, _) => Task.FromResult(new LockText(false, null)), T0.AddDays(90), default);

        Assert.Empty(_doors.Published);
        Assert.Empty(round.Told);
    }

    [Fact]
    public async Task A_hold_ends_where_the_lock_was_moved_by_another_path_to_the_tip()
    {
        var registry = Family(pin: B);
        var edge = new PinEdge("web", "core");
        var red = new QuestView("q1", "core", "web", "t", PinMoves.Words(edge, B).Body, "Declined") { Request = PinMoves.KeyOf(edge, 1), Updated = T0 };
        var filed = new QuestView("b1", "web", "core", "t", "b", "Open") { Request = PinMoves.BreakKeyOf(edge, 1) };

        var round = await FamilyFanout.RunAsync(registry, [red, filed], _doors, Tips(C), Locks, T0.AddDays(9), default);

        Assert.Empty(round.Told);
    }

    [Fact]
    public void Only_a_registered_checkout_beside_a_family_file_makes_a_family_worth_reading()
    {
        var registry = Family();
        Assert.True(FamilyFanout.HasFamily(registry));

        File.Delete(Path.Combine(_folder, "daoris.family.json"));
        Assert.False(FamilyFanout.HasFamily(registry));
        Assert.False(FamilyFanout.HasFamily([new RepoView("core", true, null)]));
    }
}

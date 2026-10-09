using Daoris.Knowledge.Http;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// The class table (PERSONDOOR1a; the person-door design §3.2, D156 point 3): every route either host maps, by its class,
/// listed here whole, as <c>docs/index/routes.md</c> lists them, and held to <see cref="PersonDoors.Table"/> and to the
/// routes each host maps, which that index is generated from. A route added tomorrow fails here until it is classed; until
/// then the gate refuses it without the key, as the person's. The hosts' tables are read rather than the index: a change to
/// <c>Program.cs</c> runs this suite at a merge, and a change to the index alone does not.
/// </summary>
public sealed class PersonDoorTableTests
{
    private const DoorClass Open = DoorClass.Open;
    private const DoorClass Agent = DoorClass.Agent;
    private const DoorClass Driver = DoorClass.Driver;
    private const DoorClass Person = DoorClass.Person;
    private const DoorClass Shared = DoorClass.Shared;

    /// <summary>
    /// Every route at PERSONDOOR1b, written out: the design's 59 at <c>4004d041</c>, XAGENT1c's four, and the window's
    /// confirmation's five (design §4.2): what waits and one of them are reads, asking is any caller's, and confirming and
    /// refusing are the person's.
    /// </summary>
    private static readonly (string Method, string Pattern, DoorClass Class)[] Routes =
    [
        ("GET", "/api/status", Open),
        ("GET", "/api/repositories", Open),
        ("GET", "/api/search", Open),
        ("GET", "/api/entry", Open),
        ("GET", "/api/entries", Open),
        ("GET", "/api/convergence", Open),
        ("GET", "/api/quests", Open),
        ("GET", "/api/quests/{id}/attachments/{sha256}", Open),
        ("GET", "/api/quests/{id}/claim", Open),
        ("GET", "/api/asks", Open),
        ("GET", "/api/asks/{id}", Open),
        ("GET", "/api/sessions", Open),
        ("GET", "/api/sessions/{id}/deletable", Open),
        ("GET", "/api/history", Open),
        ("GET", "/api/registry", Open),
        ("GET", "/api/registry/retired", Open),
        ("GET", "/api/code-map/{repository}", Open),
        ("GET", "/api/sync", Open),
        ("GET", "/api/opinions", Open),
        ("GET", "/api/opinions/{id}", Open),
        ("GET", "/api/confirmations", Open),
        ("GET", "/api/confirmations/{id}", Open),

        ("POST", "/api/quests/{id}/respond", Agent),
        ("POST", "/api/quests", Agent),
        ("POST", "/api/asks/{id}/publish", Agent),
        ("POST", "/api/confirmations", Agent),

        ("POST", "/api/quests/{id}/evidence", Driver),
        ("POST", "/api/quests/{id}/set-up", Driver),
        ("POST", "/api/sessions", Driver),
        ("POST", "/api/sessions/chat", Driver),
        ("POST", "/api/sessions/intake", Driver),
        ("POST", "/api/sessions/help", Driver),
        ("POST", "/api/sessions/{id}/state", Driver),
        ("POST", "/api/sessions/{id}/taken", Driver),
        ("POST", "/api/sync", Driver),
        ("DELETE", "/api/registry/retired/{repository}", Driver),
        ("POST", "/api/registry", Driver),
        ("POST", "/api/refresh", Driver),
        ("POST", "/api/opinions", Driver),
        ("POST", "/api/opinions/{id}/hand", Driver),

        ("POST", "/api/quests/{id}/review", Person),
        ("POST", "/api/quests/{id}/set-up-step", Person),
        ("POST", "/api/asks", Person),
        ("POST", "/api/asks/{id}/review", Person),
        ("POST", "/api/quests/{id}/accept", Person),
        ("POST", "/api/quests/{id}/done", Person),
        ("POST", "/api/asks/{id}/go-aheads/{number}", Person),
        ("POST", "/api/sessions/{id}/added", Person),
        ("POST", "/api/sessions/{id}/say", Person),
        ("POST", "/api/sessions/{id}/answer", Person),
        ("POST", "/api/asks/{id}/close", Person),
        ("DELETE", "/api/asks/{id}", Person),
        ("DELETE", "/api/quests/{id}", Person),
        ("DELETE", "/api/sessions/{id}", Person),
        ("POST", "/api/history/clear", Person),
        ("POST", "/api/quests/{id}/conflicts/dismiss", Person),
        ("DELETE", "/api/registry/{repository}", Person),
        ("POST", "/api/registry/{repository}/workspace", Person),
        ("POST", "/api/registry/import", Person),
        ("POST", "/api/confirmations/{id}/confirm", Person),
        ("POST", "/api/confirmations/{id}/refuse", Person),

        ("POST", "/api/feed/sessions", Shared),
        ("GET", "/api/sessions/since", Shared),
        ("POST", "/api/feed/entries", Shared),
        ("POST", "/api/feed/code-map", Shared),
        ("GET", "/api/feed/held", Shared),
        ("GET", "/api/quests/operations", Shared),
        ("POST", "/api/quests/operations", Shared),
    ];

    private static IEnumerable<(string Method, string Pattern)> Sorted(IEnumerable<(string Method, string Pattern)> routes) =>
        routes.OrderBy(route => route.Pattern, StringComparer.Ordinal).ThenBy(route => route.Method, StringComparer.Ordinal);

    [Fact]
    public void The_table_classes_every_route_as_the_design_does()
    {
        Assert.Equal(Routes, PersonDoors.Table.Select(door => (door.Method, door.Pattern, door.Class)));
        Assert.Equal(PersonDoors.Table.Count, PersonDoors.Table.Select(door => (door.Method, door.Pattern)).Distinct().Count());

        // The design's counts (§3.2), with XAGENT1c's two reads and two driver's posts, the set-up door, which is the
        // driver's naming a session and the person's own with where to look, and the confirmation's two reads, its ask and
        // the person's two answers (PERSONDOOR1b).
        Assert.Equal(22, Routes.Count(route => route.Class == Open));
        Assert.Equal(4, Routes.Count(route => route.Class == Agent));
        Assert.Equal(14, Routes.Count(route => route.Class == Driver));
        Assert.Equal(21, Routes.Count(route => route.Class == Person));
        Assert.Equal(7, Routes.Count(route => route.Class == Shared));
    }

    /// <summary>Every gated door has its own name, which a refusal says; a read and a shared host's door have none.</summary>
    [Fact]
    public void Every_gated_door_is_named_and_the_forms_are_the_designs()
    {
        Assert.All(PersonDoors.Table, door =>
            Assert.True(door.Class is Open or Shared ? door.Act is null : !string.IsNullOrWhiteSpace(door.Act), $"{door.Method} {door.Pattern}"));

        var forms = PersonDoors.Table.Where(door => door.Form is not null)
            .Select(door => (door.Method, door.Pattern, door.Form!.Field, door.Form.Class)).ToList();
        Assert.Equal(
            new (string, string, string, DoorClass)[]
            {
                ("POST", "/api/quests/{id}/respond", "whileOpen", Person),
                ("POST", "/api/quests/{id}/set-up", "look", Person),
            },
            forms);
        Assert.Same(PersonDoors.Respond, PersonDoors.Find("POST", "/api/quests/{id}/respond"));
        Assert.Same(PersonDoors.SetUp, PersonDoors.Find("POST", "/api/quests/{id}/set-up"));
        Assert.Equal("a review's verdict", PersonDoors.Find("POST", "/api/quests/{id}/review")!.Act);
        Assert.Equal("the yes to a departure", PersonDoors.Find("post", "/api/quests/{id}/accept")!.Act);
        Assert.Equal("a go-ahead's answer", PersonDoors.Find("POST", "/api/asks/{id}/go-aheads/{number}")!.Act);
        Assert.Null(PersonDoors.Find("POST", "/api/nothing-here"));
    }

    /// <summary>
    /// The hosts' own route tables: every route a local host maps is classed and none is a shared host's alone; every
    /// route a shared host maps that a local host does not is one; and the table holds nothing neither maps.
    /// </summary>
    [Fact]
    public void Every_route_each_host_maps_is_in_the_table()
    {
        IReadOnlyList<(string Method, string Pattern)> local, shared;
        using (var host = new LocalHost()) local = host.Routes();
        using (var host = new SharedHost()) shared = host.Routes();

        var unclassed = local.Concat(shared).Where(route => PersonDoors.Find(route.Method, route.Pattern) is null).ToList();
        Assert.True(unclassed.Count == 0, "not classed:\n" + string.Join("\n", unclassed));

        Assert.All(local, route => Assert.NotEqual(Shared, PersonDoors.Find(route.Method, route.Pattern)!.Class));
        Assert.All(shared.Except(local), route => Assert.Equal(Shared, PersonDoors.Find(route.Method, route.Pattern)!.Class));
        Assert.Equal(
            Sorted(local.Union(shared)),
            Sorted(PersonDoors.Table.Select(door => (door.Method, door.Pattern))));
    }

    /// <summary>The gate's judgement (design §3.1), row by row, without a host.</summary>
    [Theory]
    // A read is answered whatever was presented.
    [InlineData("GET", "/api/quests", Presented.None, null)]
    [InlineData("GET", "/api/quests", Presented.Stale, null)]
    // An agent's door answers a keyless call; a key from another start is never taken as an agent's.
    [InlineData("POST", "/api/quests", Presented.None, null)]
    [InlineData("POST", "/api/quests", Presented.Key, null)]
    [InlineData("POST", "/api/quests", Presented.Stale, PersonDoors.Stale)]
    // The driver's and the person's want the key.
    [InlineData("POST", "/api/sessions/{id}/state", Presented.None, PersonDoors.DriverOnly)]
    [InlineData("POST", "/api/sessions/{id}/state", Presented.Key, null)]
    [InlineData("POST", "/api/sessions/{id}/state", Presented.Stale, PersonDoors.Stale)]
    [InlineData("POST", "/api/quests/{id}/review", Presented.None, PersonDoors.PersonOnly)]
    [InlineData("POST", "/api/quests/{id}/review", Presented.Key, null)]
    [InlineData("POST", "/api/quests/{id}/review", Presented.Stale, PersonDoors.Stale)]
    // A door with a form is its route's to judge, but for a stale key.
    [InlineData("POST", "/api/quests/{id}/set-up", Presented.None, null)]
    [InlineData("POST", "/api/quests/{id}/set-up", Presented.Stale, PersonDoors.Stale)]
    [InlineData("POST", "/api/quests/{id}/respond", Presented.None, null)]
    // A write nobody classed is the person's; a read or a preflight nobody classed is answered.
    [InlineData("POST", "/api/a-door-added-tomorrow", Presented.None, PersonDoors.PersonOnly)]
    [InlineData("DELETE", "/api/a-door-added-tomorrow", Presented.None, PersonDoors.PersonOnly)]
    [InlineData("POST", "/api/a-door-added-tomorrow", Presented.Key, null)]
    [InlineData("GET", "/api/a-read-added-tomorrow", Presented.None, null)]
    [InlineData("OPTIONS", "/api/quests/{id}/accept", Presented.None, null)]
    public void The_gate_judges_by_class_and_by_what_was_presented(string method, string pattern, Presented presented, string? code)
    {
        var refused = PersonGate.Judge(PersonDoors.Find(method, pattern), method, presented);

        Assert.Equal(code, refused?.Code);
        if (refused is { } said && PersonDoors.Find(method, pattern) is null) Assert.Equal(PersonDoors.Unclassified, said.Act);
    }
}

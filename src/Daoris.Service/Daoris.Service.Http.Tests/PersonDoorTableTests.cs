using Daoris.Knowledge.Http;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// The class table: every route either host maps, by its class,
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
    /// Every route, written out. The confirmation's five: what waits and one of them are reads, asking is any caller's,
    /// and confirming and refusing are the person's.
    /// </summary>
    private static readonly (string Method, string Pattern, DoorClass Class)[] Routes =
    [
        ("GET", "/api/status", Open),
        ("GET", "/api/protocol", Open),
        ("GET", "/api/repositories", Open),
        ("GET", "/api/search", Open),
        ("GET", "/api/entry", Open),
        ("GET", "/api/entries", Open),
        ("GET", "/api/convergence", Open),
        ("GET", "/api/quests", Open),
        ("GET", "/api/quests/{id}/attachments/{sha256}", Open),
        ("GET", "/api/quests/{id}/claim", Open),
        // A quest's moves (FLOWCHART1a2): a read by allow-list, on both hosts.
        ("GET", "/api/quests/{id}/moves", Open),
        ("GET", "/api/asks", Open),
        ("GET", "/api/asks/{id}", Open),
        ("GET", "/api/asks/{id}/channel", Open),
        ("GET", "/api/channel/elsewhere", Open),
        ("GET", "/api/channel/addressed", Open),
        ("GET", "/api/channel/line", Open),
        ("GET", "/api/sessions", Open),
        ("GET", "/api/sessions/{id}", Open),
        ("GET", "/api/sessions/changes", Open),
        ("GET", "/api/sessions/{id}/deletable", Open),
        ("GET", "/api/sessions/{id}/record", Open),
        // A session's memory changes for its record's page; a local host's read.
        ("GET", "/api/sessions/{id}/memory", Open),
        // Every memory wait on the person; a local host's read.
        ("GET", "/api/memory/waits", Open),
        // One quest's handback whole, by the quest's id or its session's; a local host's read.
        ("GET", "/api/handbacks/{id}", Open),
        ("GET", "/api/history", Open),
        ("GET", "/api/registry", Open),
        ("GET", "/api/registry/retired", Open),
        ("GET", "/api/code-map/{repository}", Open),
        ("GET", "/api/sync", Open),
        ("GET", "/api/opinions", Open),
        ("GET", "/api/opinions/{id}", Open),
        // The brainstorms on this machine, and one of them, answered to loopback only.
        ("GET", "/api/brainstorms", Open),
        ("GET", "/api/brainstorms/{id}", Open),
        ("GET", "/api/confirmations", Open),
        ("GET", "/api/confirmations/{id}", Open),
        // What waits, held until it moves.
        ("GET", "/api/confirmations/waiting", Open),
        ("GET", "/api/changes", Open),
        ("GET", "/api/journal", Open),

        ("POST", "/api/quests/{id}/respond", Agent),
        ("POST", "/api/quests", Agent),
        ("POST", "/api/asks/{id}/publish", Agent),
        ("POST", "/api/confirmations", Agent),
        // An asker's withdrawal of its own ask, by the token its ask got.
        ("POST", "/api/confirmations/{id}/withdraw", Agent),

        ("POST", "/api/quests/{id}/evidence", Driver),
        ("POST", "/api/quests/{id}/set-up", Driver),
        // A closure graph's bind and close, the driver's under the unit's lock; a local host's alone.
        ("POST", "/api/quests/{id}/close", Driver),
        // What became of a close's key, the driver's question before it ever sends that close again.
        ("POST", "/api/quests/{id}/close/outcome", Driver),
        ("POST", "/api/sessions", Driver),
        ("POST", "/api/sessions/chat", Driver),
        ("POST", "/api/sessions/intake", Driver),
        ("POST", "/api/sessions/help", Driver),
        ("POST", "/api/sessions/{id}/state", Driver),
        ("POST", "/api/sessions/{id}/taken", Driver),
        // What a line read, rechecked for its brief and kept as read at the revision answered.
        ("POST", "/api/sessions/{id}/memory/read", Driver),
        // A word's attempt, marked by the dispatcher that writes it to the harness, before the wire.
        ("POST", "/api/sessions/{id}/attempt", Driver),
        // HELPMODE1a2: the mode an Ask Daoris conversation going on runs under, which the driver that spawns it knows.
        ("POST", "/api/sessions/{id}/help-mode", Driver),
        // AUTONOMY1b2: the setup a resumed driven record now runs under, which the driver that spawns it knows.
        ("POST", "/api/sessions/{id}/access", Driver),
        // The downloaded versions a start of the record is handed, which the driver that spawns it knows.
        ("POST", "/api/sessions/{id}/resolved", Driver),
        ("POST", "/api/sessions/{id}/actor", Driver),
        // A finished tree's record, posted by the driver that moved it aside.
        ("POST", "/api/trees/record", Driver),
        ("POST", "/api/trees/record/age", Driver),
        ("POST", "/api/sessions/{id}/restore", Driver),
        // An entry's acknowledgment of a channel message, the driver's as it hands one to a part.
        ("POST", "/api/asks/{id}/channel/ack", Driver),
        ("POST", "/api/channel/addressed/ack", Driver),
        ("POST", "/api/sync", Driver),
        ("DELETE", "/api/registry/retired/{repository}", Driver),
        ("POST", "/api/registry", Driver),
        ("POST", "/api/refresh", Driver),
        ("POST", "/api/opinions", Driver),
        ("POST", "/api/opinions/{id}/hand", Driver),
        ("POST", "/api/helpers", Driver),
        ("POST", "/api/helpers/{helper}/hand", Driver),
        // A partner's record, opened by the driver for one piece.
        ("POST", "/api/partners", Driver),
        // A piece's answer, handed to its lead's line by the driver.
        ("POST", "/api/partners/{partner}/hand", Driver),

        ("POST", "/api/quests/{id}/review", Person),
        ("POST", "/api/quests/{id}/set-up-step", Person),
        ("POST", "/api/asks", Person),
        ("POST", "/api/asks/{id}/review", Person),
        ("POST", "/api/asks/{id}/workflow", Person),
        // WORKFLOW1e2: a quest's own kind and workflow, where no ask published it; a local host's alone.
        ("POST", "/api/quests/{id}/workflow", Person),
        ("POST", "/api/asks/{id}/channel", Person),
        ("POST", "/api/channel/line", Person),
        // The person's admission of an outside agent, and that member's read, post and leave, judged as its own.
        ("POST", "/api/channel/members", Person),
        ("GET", "/api/channel/members", Driver),
        ("GET", "/api/channel/members/{id}/messages", Agent),
        ("POST", "/api/channel/members/{id}/messages", Agent),
        ("POST", "/api/channel/members/{id}/leave", Agent),
        ("POST", "/api/asks/{id}/channel/read", Person),
        ("POST", "/api/quests/{id}/accept", Person),
        ("POST", "/api/quests/{id}/done", Person),
        // A sealed submission's reopen, and the word to go on with the step it held; a local host's alone.
        ("POST", "/api/quests/{id}/reopen", Person),
        ("POST", "/api/quests/{id}/continue", Person),
        ("POST", "/api/asks/{id}/go-aheads/{number}", Person),
        ("POST", "/api/sessions/{id}/added", Person),
        ("POST", "/api/sessions/{id}/say", Person),
        ("POST", "/api/sessions/{id}/take-back", Person),
        // The person's held word moved to the head; a local host's alone.
        ("POST", "/api/sessions/{id}/move", Person),
        // A memory end's approval, and an ended session's staged changes kept or dropped; a local host's alone.
        ("POST", "/api/memory/{id}/decide", Person),
        ("POST", "/api/sessions/{id}/memory", Person),
        ("POST", "/api/asks/{id}/close", Person),
        ("DELETE", "/api/asks/{id}", Person),
        ("DELETE", "/api/quests/{id}", Person),
        ("DELETE", "/api/sessions/{id}", Person),
        ("POST", "/api/history/clear", Person),
        // Only the person starts one, sends its packet and clears it; an agent asks them instead.
        ("POST", "/api/brainstorms", Person),
        ("POST", "/api/brainstorms/{id}/send", Person),
        ("POST", "/api/brainstorms/{id}/stop", Person),
        ("DELETE", "/api/brainstorms/{id}", Person),
        ("POST", "/api/quests/{id}/conflicts/dismiss", Person),
        // The person's bring-forward of their machine's lost take; a local host's alone.
        ("POST", "/api/quests/{id}/conflicts/forward", Person),
        ("POST", "/api/sync/machines/{machine}/retire", Person),
        ("DELETE", "/api/registry/{repository}", Person),
        ("POST", "/api/registry/{repository}/workspace", Person),
        ("POST", "/api/registry/import", Person),
        ("POST", "/api/confirmations/{id}/confirm", Person),
        ("POST", "/api/confirmations/{id}/refuse", Person),

        ("POST", "/api/feed/sessions", Shared),
        ("GET", "/api/sessions/since", Shared),
        ("POST", "/api/feed/channel", Shared),
        ("GET", "/api/channel/since", Shared),
        ("POST", "/api/feed/entries", Shared),
        ("POST", "/api/feed/code-map", Shared),
        ("GET", "/api/feed/held", Shared),
        ("GET", "/api/quests/operations", Shared),
        ("POST", "/api/quests/operations", Shared),
        ("POST", "/api/quests/machines/{machine}/retire", Shared),
    ];

    private static IEnumerable<(string Method, string Pattern)> Sorted(IEnumerable<(string Method, string Pattern)> routes) =>
        routes.OrderBy(route => route.Pattern, StringComparer.Ordinal).ThenBy(route => route.Method, StringComparer.Ordinal);

    [Fact]
    public void The_table_classes_every_route_as_the_design_does()
    {
        Assert.Equal(Routes, PersonDoors.Table.Select(door => (door.Method, door.Pattern, door.Class)));
        Assert.Equal(PersonDoors.Table.Count, PersonDoors.Table.Select(door => (door.Method, door.Pattern)).Distinct().Count());

        Assert.Equal(39, Routes.Count(route => route.Class == Open));
        // An outside agent's read, post and leave, and the person's admission of it.
        Assert.Equal(8, Routes.Count(route => route.Class == Agent));
        // The listing of a channel's members, which holds the ids only the key may read.
        Assert.Equal(32, Routes.Count(route => route.Class == Driver));
        Assert.Equal(38, Routes.Count(route => route.Class == Person));
        // A shared host's two channel doors, a feed and a page, and its retirement of a machine.
        Assert.Equal(10, Routes.Count(route => route.Class == Shared));
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
    /// Every person's door and form declares what its effect marks on a quest's records, one of the four
    /// kinds with its evidence or none with why; no other door declares one; a publication, an ask's own added words and a session's
    /// delete never mark; the person's done, answer and words do.
    /// </summary>
    [Fact]
    public void Every_person_door_declares_what_it_marks_on_a_quests_records()
    {
        var person = PersonDoors.Table.Where(door => door.Class == Person)
            .Select(door => ($"{door.Method} {door.Pattern}", door.Marks))
            .Concat(PersonDoors.Table.Where(door => door.Form is { Class: Person })
                .Select(door => ($"{door.Method} {door.Pattern} {door.Form!.Field}", door.Form.Marks)))
            .ToList();

        Assert.Empty(person.Where(each => each.Marks is null).Select(each => each.Item1));
        Assert.All(person, each => Assert.Contains(each.Marks!.Kind, OwnerMarkKinds.All));
        Assert.All(person, each => Assert.False(string.IsNullOrWhiteSpace(each.Marks!.Evidence), each.Item1));
        Assert.Empty(PersonDoors.Table.Where(door => door.Class != Person && door.Marks is not null).Select(door => door.Pattern));

        Assert.Equal(OwnerMarkKinds.None, PersonDoors.Find("POST", "/api/asks")!.Marks!.Kind);
        Assert.Equal(OwnerMarkKinds.None, PersonDoors.Find("POST", "/api/sessions/{id}/added")!.Marks!.Kind);
        Assert.Equal(OwnerMarkKinds.None, PersonDoors.Find("POST", "/api/quests/{id}/set-up-step")!.Marks!.Kind);
        Assert.Equal(OwnerMark.Of(OwnerMarkKinds.Status, "note:quest.person-done"), PersonDoors.Find("POST", "/api/quests/{id}/done")!.Marks);
        Assert.Equal(OwnerMarkKinds.SessionWord, PersonDoors.Find("POST", "/api/sessions/{id}/say")!.Marks!.Kind);
        // A park is answered through the say door alone; its own answer route is gone.
        Assert.Null(PersonDoors.Find("POST", "/api/sessions/{id}/answer"));
        Assert.Equal("instance:WORK_ABANDON", PersonDoors.Respond.Form!.Marks!.Evidence);
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

    /// <summary>The gate's judgement, row by row, without a host.</summary>
    [Theory]
    // A read is answered whatever is presented.
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

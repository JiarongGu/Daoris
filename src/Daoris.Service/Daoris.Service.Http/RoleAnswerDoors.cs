using System.Text.Json;

namespace Daoris.Knowledge.Http;

/// <summary>
/// The person's doors that are a workflow's accept steps, which an appointed role answers at low and medium risk: a session's staged
/// memory kept or dropped, a memory end approved or declined, and a departure's yes. Each is the person's, and its <c>role</c> form the
/// driver's, the role's answer released through the desktop's role door: posted with the key and kept as the role's, never the person's,
/// so a keyless agent or terminal is refused either way. Every other person's door stays the person's alone, as an unclassed write does.
/// </summary>
public static class RoleAnswerDoors
{
    /// <summary>The field of the body that names the role, as <c>RoleAnswerWire</c> carries it.</summary>
    public const string Field = "role";

    public static readonly Door Keep = new("POST", "/api/sessions/{id}/memory", DoorClass.Person, "an ended session's memory changes kept or dropped")
    {
        Marks = OwnerMark.None("a memory row, no quest"),
        Form = new DoorForm(Field, DoorClass.Driver, "a role's keep or drop of an ended session's memory changes"),
    };

    public static readonly Door Decide = new("POST", "/api/memory/{id}/decide", DoorClass.Person, "a memory end's approval")
    {
        Marks = OwnerMark.None("a memory row, no quest"),
        Form = new DoorForm(Field, DoorClass.Driver, "a role's approval of a memory end"),
    };

    public const string PolicyField = "policy";

    public static readonly Door Accept = new("POST", "/api/quests/{id}/accept", DoorClass.Person, "the yes to a departure")
    {
        Marks = OwnerMark.Of(OwnerMarkKinds.Status, "move:accepted"),
        Form = new DoorForm(Field, DoorClass.Driver, "a role's or the risk policy's yes to a departure"),
    };

    public static readonly Door GoAhead = new("POST", "/api/asks/{id}/go-aheads/{number}", DoorClass.Person, "a go-ahead's answer")
    {
        Marks = OwnerMark.Of(OwnerMarkKinds.SessionMove, "session-note:ledger.answered"),
        Form = new DoorForm(Field, DoorClass.Driver, "a role's answer to a go-ahead"),
    };

    /// <summary>The four, in the table's order.</summary>
    public static IReadOnlyList<Door> All { get; } = [Accept, GoAhead, Decide, Keep];

    /// <summary>Whether <paramref name="body"/> makes <paramref name="door"/>'s driver form: a <c>role</c>, or at the departure a <c>policy</c>, that is an object.</summary>
    public static bool Makes(Door door, JsonElement? body) =>
        All.Contains(door) && body is { ValueKind: JsonValueKind.Object } found
        && (Names(found, Field) || (door == Accept && Names(found, PolicyField)));

    private static bool Names(JsonElement body, string field) =>
        body.EnumerateObject().LastOrDefault(each => string.Equals(each.Name, field, StringComparison.OrdinalIgnoreCase)) is { Value.ValueKind: JsonValueKind.Object };
}

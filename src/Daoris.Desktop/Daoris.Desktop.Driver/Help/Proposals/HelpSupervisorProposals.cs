using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What Ask Daoris's <c>role</c> proposal reads of an appointed supervisor: the kinds it decides (none is its scope's generic supervisor),
/// its granted acts, its holder and its window, written flat in the press as the record's own fields. A field the press leaves out keeps
/// the role's own; a new supervisor names what it decides, holds its place by the person's rules and grants nothing. The role door's
/// judge decides what the record may be, so a press outside its shape is refused with the judge's sentence.
/// </summary>
internal static class HelpSupervisorProposals
{
    /// <summary>Whether the press names the record's <c>kind</c>, which is how a proposal says it is of an appointed role.</summary>
    public static bool Names(JsonElement? press) =>
        press is { ValueKind: JsonValueKind.Object } given && given.TryGetProperty("kind", out var kind) && kind.ValueKind == JsonValueKind.String;

    /// <summary>The supervisor the press sets <paramref name="id"/> to, or why it is not one: nothing is built from a press that does not read.</summary>
    public static (RoleRecord? Role, string? Refusal) Read(string id, JsonElement? press, AuthoredText? name, RoleScope scope, RoleRecord? current)
    {
        var given = press ?? default;
        var kind = given.ValueKind == JsonValueKind.Object && Text(given, "kind") is { } said ? said : current?.Hold?.Kind;
        if (kind != AppointedRoles.Supervisor) return (null, $"`{id}` is not a supervisor: a proposal of an appointed role is a supervisor's alone, and the steward is the person's.");
        if (given.ValueKind == JsonValueKind.Object && given.TryGetProperty("takes", out var takes) && takes.ValueKind == JsonValueKind.Array && takes.GetArrayLength() > 0)
        {
            return (null, $"`{id}` is a supervisor, which takes no work: it decides kinds of decision.");
        }

        var held = current?.Hold;
        if (!Strings(given, "decides", held?.Decides, out var decides)) return (null, "`decides` is a list of kinds of decision, `[]` for every decision no other supervisor here takes.");
        if (current?.Hold is null && !(given.ValueKind == JsonValueKind.Object && given.TryGetProperty("decides", out _)))
        {
            return (null, "a new supervisor names what it decides: kinds of decision, or `[]` for every decision no other supervisor in its scope takes.");
        }

        if (!Strings(given, "authority", held?.Acts, out var acts)) return (null, "`authority` is a list of acts, each `<kind>.<act>`.");
        var holder = given.ValueKind == JsonValueKind.Object && Text(given, "holder") is { } chosen ? chosen : held?.Holder ?? RoleHolders.Rules;
        if (!RoleHolders.All.Contains(holder, StringComparer.Ordinal)) return (null, $"`{id}` is held by `{holder}`, a holder this build does not know.");
        var window = held?.Window ?? RoleHold.DefaultWindow;
        if (given.ValueKind == JsonValueKind.Object && given.TryGetProperty("window", out var minutes))
        {
            if (!minutes.TryGetInt64(out var set)) return (null, $"`{id}`'s window is whole minutes.");
            // The judge's own bound reads a number outside the window's range, so an oversized one is never narrowed here.
            window = (int)Math.Clamp(set, int.MinValue, int.MaxValue);
        }

        var hold = new RoleHold(AppointedRoles.Supervisor, acts, holder, window) { Decides = decides };
        return (new RoleRecord(id, name ?? current?.Name ?? AuthoredText.Of(""), RoleRecord.Appointed, [], scope, hold), null);
    }

    /// <summary>The card's sentence for a supervisor set: its kinds, its place, its holder, its acts and its window.</summary>
    public static string Describe(RoleRecord role, string place)
    {
        var hold = role.Hold!;
        var decides = hold.Generic ? "every decision no other supervisor here takes" : string.Join(", ", hold.Decides);
        var acts = hold.Acts.Count == 0 ? "granting no act, so it leaves every decision to you" : $"granting {string.Join(", ", hold.Acts.Select(act => $"`{act}`"))}";
        var by = hold.Holder switch { RoleHolders.Terminal => "a terminal", RoleHolders.Agent => "an agent, its choices in order", _ => "your rules, then you" };
        return $"Set the supervisor `{role.Id}` ({string.Join(" · ", role.Name.Texts)}): it decides {decides} {place}, held by {by}, {acts}; "
            + $"an answer is held {hold.Window} minutes for your overrule. Running work goes on.";
    }

    /// <summary>The <c>daoris role set</c> command that makes the same change, <paramref name="name"/> the name's flags.</summary>
    public static string Terminal(RoleRecord role, string name)
    {
        var hold = role.Hold!;
        var scope = role.Scope.Kind == RoleScope.Machine ? RoleScope.Machine : $"{role.Scope.Kind}:{role.Scope.Id}";
        return $"daoris role set {role.Id} {name} --scope {scope} --decides {(hold.Generic ? "every" : string.Join(",", hold.Decides))} "
            + $"--acts {(hold.Acts.Count == 0 ? "\"\"" : string.Join(",", hold.Acts))} --holder {hold.Holder} --window {hold.Window}";
    }

    /// <summary>What the card shows: the supervisor after the change, and before it where the role stands now.</summary>
    public static HelpSupervisorView View(RoleRecord role, RoleRecord? before) =>
        new(role.Id, role.Name.Texts, role.Scope, role.Hold!, before?.Hold is { Kind: AppointedRoles.Supervisor } now ? now : null);

    /// <summary>A named list of strings, <paramref name="kept"/> where the press leaves it out; false where it names something else.</summary>
    private static bool Strings(JsonElement press, string name, IReadOnlyList<string>? kept, out IReadOnlyList<string> list)
    {
        list = kept ?? [];
        if (press.ValueKind != JsonValueKind.Object || !press.TryGetProperty(name, out var element)) return true;
        if (element.ValueKind != JsonValueKind.Array || element.EnumerateArray().Any(each => each.ValueKind != JsonValueKind.String)) return false;
        list = [.. element.EnumerateArray().Select(each => each.GetString()!.Trim())];
        return true;
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text ? text.Trim() : null;
}

/// <summary>
/// What the card of a supervisor shows, as fields the page words in its catalogue: the role's names as written, its scope, and its
/// record after the change and before it (none for a new one).
/// </summary>
public sealed record HelpSupervisorView(string Id, IReadOnlyList<string> Names, RoleScope Scope, RoleHold Hold, RoleHold? Before);

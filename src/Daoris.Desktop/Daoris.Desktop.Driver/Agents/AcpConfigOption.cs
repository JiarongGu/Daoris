using System.Text.Json;

namespace Daoris.Driver;

/// <summary>One value a config option offers, in the agent's own words.</summary>
public sealed record AcpConfigChoice(string Value, string Name, string? Description = null);

/// <summary>
/// One of a session's config options as the agent offers it: what it is called, what kind
/// it is, what it is set to, and what it may be set to. Only a choice among values is kept.
/// </summary>
/// <param name="Id">What <c>session/set_config_option</c> names it by.</param>
/// <param name="Name">What the agent calls it — content, never translated.</param>
/// <param name="Category">
/// The protocol's word for what it is — <c>mode</c>, <c>model</c>, <c>thought_level</c>, or another —
/// or null where the agent said none.
/// </param>
/// <param name="Current">What it is set to now.</param>
/// <param name="Choices">What it may be set to, a grouped list flattened: a group is a heading, not a value.</param>
public sealed record AcpConfigOption(
    string Id, string Name, string? Category, string Current, IReadOnlyList<AcpConfigChoice> Choices,
    string? Description = null)
{
    /// <summary>
    /// Whether this is one the person may set from Daoris: the model, and the effort the protocol
    /// calls a thought level. 🔴 Never the mode — that is the posture Daoris runs a session under,
    /// and a person widening it from a menu is refused. An option with no category
    /// is not offered: nothing says what it is.
    /// </summary>
    public bool ThePersons => Category is "model" or "thought_level";

    /// <summary>
    /// The options a <c>configOptions</c> list holds, read without trusting its shape: an entry that is not
    /// a choice among values — a boolean, a malformed one — is left out, never a throw.
    /// </summary>
    public static IReadOnlyList<AcpConfigOption> Read(JsonElement list)
    {
        if (list.ValueKind != JsonValueKind.Array) return [];

        var read = new List<AcpConfigOption>();
        foreach (var option in list.EnumerateArray())
        {
            if (option.ValueKind != JsonValueKind.Object
                || Json.Text(option, "type") != "select"
                || Json.Text(option, "id") is not { Length: > 0 } id
                || Json.Text(option, "currentValue") is not { } current
                || !option.TryGetProperty("options", out var values) || values.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var choices = new List<AcpConfigChoice>();
            foreach (var entry in values.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object) continue;
                if (entry.TryGetProperty("options", out var grouped) && grouped.ValueKind == JsonValueKind.Array)
                {
                    foreach (var inner in grouped.EnumerateArray()) Choice(inner, choices);
                }
                else
                {
                    Choice(entry, choices);
                }
            }

            read.Add(new AcpConfigOption(
                id, Json.Text(option, "name") ?? id, Json.Text(option, "category"), current, choices,
                Json.Text(option, "description")));
        }

        return read;
    }

    private static void Choice(JsonElement entry, List<AcpConfigChoice> into)
    {
        if (entry.ValueKind == JsonValueKind.Object && Json.Text(entry, "value") is { Length: > 0 } value)
        {
            into.Add(new AcpConfigChoice(value, Json.Text(entry, "name") ?? value, Json.Text(entry, "description")));
        }
    }
}

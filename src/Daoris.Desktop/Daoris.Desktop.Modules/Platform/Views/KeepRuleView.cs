using System.Globalization;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris settings keep &lt;kind&gt; &lt;rule&gt;</c> as the card shows it (<c>SET_KEEP</c>): one kind's keep rule, judged by the one
/// judgement the terminal makes (<see cref="KeepRules.Apply"/>) so a value out of its kind's bounds is refused at the ask in its words, and said
/// in the driver's own clause. A press that sets a kind's days and its size together has no single terminal word, and is refused.
/// </summary>
public sealed class KeepRuleView : IPressView
{
    public string Module => DriverModule.Name;

    public string Route => "SET_KEEP";

    public async Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        var kind = PressPayload.Text(payload, "kind");
        var days = Number(payload, "days");
        var gb = Number(payload, "gb");
        var percent = Number(payload, "percent");
        var never = Flag(payload, "never");
        var off = Flag(payload, "off");
        var isDefault = Flag(payload, "default");
        bool? whole = payload is { ValueKind: JsonValueKind.Object } body && body.TryGetProperty("whole", out var sent) && sent.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? sent.ValueKind == JsonValueKind.True
            : null;

        var set = new KeepSet(kind, Clamped(days), never, whole, isDefault, Clamped(gb), Clamped(percent), off);
        DriverConfig after;
        try
        {
            after = KeepRules.Apply((await world.MachineAsync(cancellationToken).ConfigureAwait(false)).Config, set);
        }
        catch (DriverException refused)
        {
            return PressViewed.Refused(refused.Message);
        }

        if (days is not null && gb is not null)
        {
            return PressViewed.Refused("set a kind's days and its size one at a time — `daoris settings keep <kind> <days>`, then `<n>GB`.");
        }

        var word = isDefault ? "default"
            : off ? "off"
            : percent is { } share ? $"{share.ToString(CultureInfo.InvariantCulture)}%"
            : never ? "never"
            : whole is { } kept ? kept ? "whole" : "slim"
            : days is { } count ? count.ToString(CultureInfo.InvariantCulture)
            : $"{gb!.Value.ToString(CultureInfo.InvariantCulture)}GB";
        var said = KeepRules.Said(set.Kind, after.Keep);
        var facts = new Dictionary<string, string>(StringComparer.Ordinal) { ["kind"] = set.Kind, ["rule"] = said };
        return PressViewed.Shown(new PressView("keep", facts, $"daoris settings keep {set.Kind} {word}"));
    }

    private static long? Number(JsonElement? payload, string name) =>
        payload is { ValueKind: JsonValueKind.Object } body && body.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number) ? number : null;

    private static bool Flag(JsonElement? payload, string name) =>
        payload is { ValueKind: JsonValueKind.Object } body && body.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static int? Clamped(long? value) => value is { } number ? (int)Math.Clamp(number, int.MinValue, int.MaxValue) : null;
}

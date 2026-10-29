using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>The run settings a gate's command names, as the commit holds them: the path from the repository's root, and the text byte for byte (null where the commit holds no such file).</summary>
internal sealed record RunSettingsText(string Path, string? Text);

/// <summary>
/// What a gate ran with, so one of its failures can be repeated as composed (INTEGRATE1f part f1; the integration design §4.5, §5.9; D215's
/// note on INTEGRATE1j): <c>units</c>, the classes it ran where it ran by class (sorted; null where it ran whole), and <c>settings</c>, the
/// gate as the declaration writes it, the run settings its command names as the commit holds them, the classes its run settings left out
/// and the environment's rule. <c>digest</c> is <c>sha256:</c> over the canonical JSON of <c>{ units, settings }</c>, every object's keys
/// sorted, so one composition digests alike however its keys were written, on either side.
/// </summary>
/// <remarks>
/// <b>A twin</b> of <c>canonicalJson</c>, <c>compositionDigest</c> and <c>verifyComposition</c> in <c>tools/merge-branch.mjs</c>, held to
/// <c>fixtures/gate-environment.json</c>' compositions (<c>GateEnvironmentTests</c>, <c>integration-gates.test.ts</c>): both digests agree
/// on every row. A runner's own composition is read as it gave it; this one is for the direct run, where the caller is its own runner.
/// </remarks>
internal sealed record GateComposition(IReadOnlyList<string>? Units, JsonObject Settings, string Digest)
{
    /// <summary>A composition of a gate: its units sorted (ordinally, as JavaScript sorts), its settings in the shape the tool's runner records.</summary>
    internal static GateComposition Of(
        string gate, string run, string? cwd, RunSettingsText? runsettings, IEnumerable<string> narrowed, IEnumerable<string>? units, JsonNode environment)
    {
        var sorted = units is null ? null : (IReadOnlyList<string>)[.. units.OrderBy(unit => unit, StringComparer.Ordinal)];
        var settings = new JsonObject
        {
            ["gate"] = gate,
            ["run"] = run,
            ["cwd"] = cwd,
            ["runsettings"] = runsettings is null ? null : new JsonObject { ["path"] = runsettings.Path, ["text"] = runsettings.Text },
            ["narrowed"] = new JsonArray([.. narrowed.OrderBy(unit => unit, StringComparer.Ordinal).Select(unit => (JsonNode?)JsonValue.Create(unit))]),
            ["environment"] = environment.DeepClone(),
        };
        return new GateComposition(sorted, settings, DigestOf(sorted, settings));
    }

    /// <summary>The digest of a composition, its units and its settings together, which a probe compares with the one it would build.</summary>
    internal static string DigestOf(IReadOnlyList<string>? units, JsonNode settings)
    {
        var whole = new JsonObject
        {
            ["units"] = units is null ? null : new JsonArray([.. units.Select(unit => (JsonNode?)JsonValue.Create(unit))]),
            ["settings"] = settings.DeepClone(),
        };
        return "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(whole))));
    }

    /// <summary>A value as JSON with every object's keys in order (ordinally), strings written as JavaScript's <c>JSON.stringify</c> writes them.</summary>
    internal static string Canonical(JsonNode? value)
    {
        var text = new StringBuilder();
        Write(text, value);
        return text.ToString();
    }

    private static void Write(StringBuilder text, JsonNode? value)
    {
        switch (value)
        {
            case JsonArray array:
                text.Append('[');
                for (var at = 0; at < array.Count; at += 1)
                {
                    if (at > 0) text.Append(',');
                    Write(text, array[at]);
                }

                text.Append(']');
                break;
            case JsonObject members:
                text.Append('{');
                var first = true;
                foreach (var key in members.Select(member => member.Key).OrderBy(key => key, StringComparer.Ordinal))
                {
                    if (!first) text.Append(',');
                    first = false;
                    text.Append(FailureReader.Quote(key)).Append(':');
                    Write(text, members[key]);
                }

                text.Append('}');
                break;
            case JsonValue scalar:
                switch (scalar.GetValueKind())
                {
                    case JsonValueKind.String: text.Append(FailureReader.Quote(scalar.GetValue<string>())); break;
                    case JsonValueKind.True: text.Append("true"); break;
                    case JsonValueKind.False: text.Append("false"); break;
                    case JsonValueKind.Number: text.Append(scalar.ToJsonString()); break;
                    default: text.Append("null"); break;
                }

                break;
            default:
                text.Append("null");
                break;
        }
    }
}

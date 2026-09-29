using System.Text.Json;
using System.Text.RegularExpressions;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// The page's report into the machine log (LOG1b, D94): what is used, and what fails, on the screen.
/// </summary>
/// <remarks>
/// <para><b>One request</b>, <c>EVENT</c>, carrying <c>{ event, data }</c>. The answer is always nothing:
/// the report is fire-and-forget on the page, and a report that could fail would be a reason for the page
/// to handle one.</para>
///
/// <para>🔴 <b>This is where D94's list of what is never logged is enforced.</b> The page is the one writer
/// that could pass a word through by mistake, so the module takes only the catalogue's events
/// (<c>docs/2026-09-30-machine-log-design.md</c> §4), each with only its declared fields, each field only
/// in its declared kind: a name (an identifier, never a sentence), a short text, a count, a flag. Anything
/// else (another event, another field, a sentence where a name goes, words where a count goes) is dropped
/// without a word. A message is its length and how many files it carried; never its text, never a name.</para>
/// </remarks>
public sealed class LogModule(MachineLog log) : ModuleBase
{
    /// <summary>The longest string a field keeps; longer is cut and ends in an ellipsis.</summary>
    public const int MaxText = 120;

    public override string ModuleName => "DAORIS.LOG";

    /// <summary>What a field may hold.</summary>
    private enum Kind
    {
        /// <summary>An identifier: a view, a command, a region, a session id. Never a sentence.</summary>
        Name,

        /// <summary>A short text, cut at <see cref="MaxText"/>: the one free string, a caught error's message.</summary>
        Text,

        /// <summary>A whole number, zero or more.</summary>
        Count,

        /// <summary>True or false.</summary>
        Flag,
    }

    /// <summary>The page's events, each with its fields and each field's kind — the design's §4, and nothing more.</summary>
    private static readonly IReadOnlyDictionary<string, (string Level, (string Field, Kind Kind)[] Fields)> Catalogue =
        new Dictionary<string, (string, (string, Kind)[])>(StringComparer.Ordinal)
        {
            ["view.opened"] = ("info", [("view", Kind.Name)]),
            ["command.run"] = ("info", [("command", Kind.Name)]),
            ["panel.moved"] = ("info", [("view", Kind.Name), ("region", Kind.Name)]),
            ["message.sent"] = ("info", [("session", Kind.Name), ("kind", Kind.Name), ("length", Kind.Count), ("files", Kind.Count)]),
            ["proposal.settled"] = ("info", [("applied", Kind.Flag)]),
            ["page.error"] = ("error", [("where", Kind.Name), ("message", Kind.Text)]),
        };

    /// <summary>An identifier: letters, digits and the joiners ids use (a mirrored session is `origin/id`), no spaces.</summary>
    private static readonly Regex Identifier = new("^[A-Za-z0-9][A-Za-z0-9_.:/-]*$", RegexOptions.CultureInvariant);

    protected override Task<object?> RouteMessageAsync(IpcRequest request, IModuleContext context, CancellationToken cancellationToken)
    {
        switch (request.Type)
        {
            case "EVENT":
                Take(request.Payload);
                return Done();

            default:
                throw UnknownType(request);
        }
    }

    private void Take(JsonElement? payload)
    {
        if (payload is not { ValueKind: JsonValueKind.Object } report
            || !report.TryGetProperty("event", out var named) || named.ValueKind != JsonValueKind.String
            || !Catalogue.TryGetValue(named.GetString()!, out var entry))
        {
            return;
        }

        var data = report.TryGetProperty("data", out var given) && given.ValueKind == JsonValueKind.Object ? given : default;
        var kept = new List<(string Key, object? Value)>();
        foreach (var (field, kind) in entry.Fields)
        {
            if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty(field, out var value) && Keep(value, kind) is { } fit)
            {
                kept.Add((field, fit));
            }
        }

        log.Write(entry.Level, named.GetString()!, kept);
    }

    /// <summary>The value as the log may hold it, or null when it is not of its field's kind.</summary>
    private static object? Keep(JsonElement value, Kind kind) => kind switch
    {
        Kind.Name when value.ValueKind == JsonValueKind.String && value.GetString() is { } name && Identifier.IsMatch(name) => Cut(name),
        Kind.Text when value.ValueKind == JsonValueKind.String => Cut(value.GetString()!),
        Kind.Count when value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var count) && count >= 0 => count,
        Kind.Flag when value.ValueKind is JsonValueKind.True or JsonValueKind.False => value.GetBoolean(),
        _ => null,
    };

    private static string Cut(string text) => text.Length <= MaxText ? text : $"{text[..MaxText]}…";
}

using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// An agent supervisor's answer: the one fenced block its turn's last message holds, in the decision door's form less its generation and
/// proof. A decide names its act (<c>do</c>, with <c>choice</c> where the act takes one); a leave names its bound. One to five reasons,
/// each with its source.
/// </summary>
public sealed record SupervisorBlock(
    string Kind, SupervisorSubject Subject, string? Do, string? Choice, string? Leave, IReadOnlyList<SupervisorReason> Reasons)
{
    /// <summary>The fence's opening line and its close.</summary>
    public const string Opening = "<<< supervisor decision", Closing = ">>>";

    /// <summary>
    /// The block <paramref name="lastMessage"/> holds, or why none reads: no block, two of them, words that are no form, an act and a
    /// bound together or neither, a bound or reasons out of shape.
    /// </summary>
    public static (SupervisorBlock? Block, string? Why) Read(string? lastMessage)
    {
        var (body, fenced) = Body(lastMessage);
        if (body is null) return (null, fenced);
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || Json.Text(root, "kind") is not { } kind || !SupervisorKinds.Known(kind)) return (null, "its block names no kind of decision");
            if (!root.TryGetProperty("subject", out var wire) || SupervisorSubject.Read(kind, wire) is not { } subject) return (null, "its block names no subject of its kind");
            var (act, leave) = (Json.Text(root, "do"), Json.Text(root, "leave"));
            if ((act is null) == (leave is null)) return (null, "its block names an act or a bound, one of them");
            if (leave is not null && !SupervisorBounds.All.Contains(leave, StringComparer.Ordinal)) return (null, $"`{leave}` is no bound: one of {string.Join(", ", SupervisorBounds.All)}");
            var reasons = Json.Items(root, "reasons").Select(each => new SupervisorReason(Json.Text(each, "says") ?? "", Json.Text(each, "source") ?? "")).ToList();
            if (reasons.Count is < 1 or > 5 || reasons.Any(each => each.Says.Trim().Length == 0 || each.Source.Trim().Length == 0))
            {
                return (null, "its block gives one to five reasons, each saying something and naming its source");
            }

            // The decision door's own rule: a choice is named exactly where the act chooses an offered label.
            var choice = Json.Text(root, "choice");
            if (act is not null && SupervisorDecideForm.Chooses(kind, act) != (choice is not null))
            {
                return (null, SupervisorDecideForm.Chooses(kind, act) ? "its answer names no `choice`" : "its block names a `choice` for an act that takes none");
            }

            return (new SupervisorBlock(kind, subject, act, choice, leave, reasons), null);
        }
        catch (JsonException)
        {
            return (null, "its block is not one form");
        }
    }

    /// <summary>The words inside the one fence of <paramref name="lastMessage"/>, or why there is no one fence.</summary>
    public static (string? Body, string? Why) Body(string? lastMessage)
    {
        var text = (lastMessage ?? "").Replace("\r\n", "\n", StringComparison.Ordinal);
        var start = text.IndexOf(Opening, StringComparison.Ordinal);
        if (start < 0) return (null, "its last message holds no supervisor decision block");
        if (text.IndexOf(Opening, start + Opening.Length, StringComparison.Ordinal) >= 0) return (null, "its last message holds more than one block");
        var body = text[(start + Opening.Length)..];
        var end = body.IndexOf("\n" + Closing, StringComparison.Ordinal);
        return end < 0 ? (null, "its block is not closed") : (body[..end], null);
    }
}

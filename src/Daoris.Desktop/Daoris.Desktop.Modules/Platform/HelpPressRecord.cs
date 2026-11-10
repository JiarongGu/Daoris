using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// One press Ask Daoris made this run: the route as the window words it (<paramref name="Module"/> and <paramref name="Route"/>), the
/// conversation that pressed it, what the press named, when, and <paramref name="Outcome"/>: for one run with no card <c>running</c>,
/// <c>done</c> or <c>failed</c>; for one that asked the person on a card (<see cref="Asked"/>) its card's state, <c>waiting</c>,
/// <c>allowed</c>, <c>refused</c>, <c>changed</c> or <c>unanswered</c>.
/// </summary>
/// <param name="Scope">The field that named what the press acted on (<c>quest</c>, <c>ask</c>, <c>repository</c> or <c>session</c>), which a reverse press carries back.</param>
/// <param name="Target">What the press named in that field.</param>
public sealed record HelpPressNow(
    string Id, string Module, string Route, string Session, string? Scope, string? Target, DateTimeOffset At, string Outcome)
{
    /// <summary>The workspace or repository the press named (<see cref="HelpPressPlace"/>); null where it named none, or until placed.</summary>
    public IReadOnlyDictionary<string, string>? Place { get; init; }

    /// <summary>Whether <see cref="Place"/> lay beyond its conversation's focus at the press.</summary>
    public bool Beyond { get; init; }

    /// <summary>That it asked the person on a card in restricted mode rather than ran: the receipt's and Notifications' <i>Asked you</i>.</summary>
    public bool Asked { get; init; }
}

/// <summary>
/// Ask Daoris's presses of this run, for the window's group in Notifications and the turns' receipts: those run with no card, and
/// those put to the person on a card, apart. Memory alone, so a restart empties it, as the steward's acts are. The newest
/// <see cref="Kept"/> are held.
/// </summary>
public sealed class HelpPressRecord(TimeProvider clock)
{
    /// <summary>How many presses are held; an older one is dropped.</summary>
    public const int Kept = 100;

    public const string Running = "running";
    public const string Done = "done";
    public const string Failed = "failed";

    public const string Waiting = "waiting";
    public const string Allowed = "allowed";
    public const string Refused = "refused";
    public const string Changed = "changed";
    public const string Unanswered = "unanswered";

    private readonly object _gate = new();
    private readonly List<HelpPressNow> _presses = [];

    /// <summary>A covered press begun on <paramref name="session"/>'s conversation: its id, for <see cref="Ended"/>.</summary>
    public string Began(IpcRequest request, string session) => Keep(request, session, Running, null, asked: false);

    /// <summary>
    /// A press <paramref name="session"/>'s conversation asked the person on a card, with the place it named where one reads: its id,
    /// for <see cref="Answered"/>.
    /// </summary>
    public string Asked(IpcRequest request, string session, HelpPressPlace? place) => Keep(request, session, Waiting, place, asked: true);

    /// <summary>An asked press's card ended: its state from how.</summary>
    public void Answered(string id, PipeConfirmationEnd end) => Settle(id, end switch
    {
        PipeConfirmationEnd.Confirmed => Allowed,
        PipeConfirmationEnd.Refused => Refused,
        PipeConfirmationEnd.Changed => Changed,
        _ => Unanswered,
    });

    private string Keep(IpcRequest request, string session, string outcome, HelpPressPlace? place, bool asked)
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        var (scope, target) = Subject(request);
        var press = new HelpPressNow(id, request.Module ?? "", request.Type ?? "", session, scope, target, clock.GetUtcNow(), outcome)
        {
            Place = place?.Place.Wire(),
            Beyond = place?.Beyond ?? false,
            Asked = asked,
        };
        lock (_gate)
        {
            _presses.Add(press);
            if (_presses.Count > Kept) _presses.RemoveAt(0);
        }

        return id;
    }

    private void Settle(string id, string outcome)
    {
        lock (_gate)
        {
            var at = _presses.FindIndex(each => each.Id == id);
            if (at >= 0) _presses[at] = _presses[at] with { Outcome = outcome };
        }
    }

    /// <summary>The press's answer came: <c>done</c> where it succeeded, else <c>failed</c>.</summary>
    public void Ended(string id, bool succeeded) => Settle(id, succeeded ? Done : Failed);

    /// <summary>The place the press named, read against the registry and its conversation's focus once it began.</summary>
    public void Placed(string id, HelpPressPlace place)
    {
        lock (_gate)
        {
            var at = _presses.FindIndex(each => each.Id == id);
            if (at >= 0) _presses[at] = _presses[at] with { Place = place.Place.Wire(), Beyond = place.Beyond };
        }
    }

    /// <summary>The presses this run, newest first.</summary>
    public IReadOnlyList<HelpPressNow> Now()
    {
        lock (_gate) return [.. Enumerable.Reverse(_presses)];
    }

    /// <summary>Whether an answer's text, as the pipe serialized it, says the press succeeded.</summary>
    public static bool Succeeded(string answer)
    {
        try
        {
            using var read = JsonDocument.Parse(answer);
            return read.RootElement.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.True;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static readonly string[] s_scopes = ["quest", "ask", "repository", "session"];

    /// <summary>What the press named and the field that named it (<c>quest</c>, <c>ask</c>, <c>repository</c> or <c>session</c>), the first it carried.</summary>
    private static (string? Scope, string? Target) Subject(IpcRequest request)
    {
        foreach (var scope in s_scopes)
        {
            if (Named(request, scope) is { } target) return (scope, target);
        }

        return (null, null);
    }

    private static string? Named(IpcRequest request, string field) =>
        request.Payload is { ValueKind: JsonValueKind.Object } payload && payload.TryGetProperty(field, out var value)
        && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text ? text : null;
}

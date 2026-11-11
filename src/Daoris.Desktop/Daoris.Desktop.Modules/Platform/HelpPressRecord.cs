using System.Text.Json;
using System.Text.Json.Serialization;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// One press Ask Daoris made this run: the route as the window words it, its conversation, what it named, when, and <paramref name="Outcome"/>:
/// <c>running</c>, <c>done</c> or <c>failed</c> for one run with no card; for one <see cref="Asked"/> on a card its state.
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

    /// <summary>What a press that opened a place says it made, once its answer is known: the line under its row and under its turn.</summary>
    public PressReceipt? Receipt { get; init; }

    /// <summary>The words a rule refused the press in, before any run or card: the growth switch's among them.</summary>
    public string? Why { get; init; }

    [JsonIgnore]
    public string? Card { get; init; }
}

/// <summary>
/// Ask Daoris's presses of this run, run with no card or asked on one, for Notifications and the turns' receipts: memory alone, so a
/// restart empties it, as the steward's acts are. The newest <see cref="Kept"/> are held.
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

    public event Action<HelpPressNow>? Moved;

    /// <summary>A covered press begun on <paramref name="session"/>'s conversation: its id, for <see cref="Ended"/>.</summary>
    public string Began(IpcRequest request, string session) => Keep(request, session, Running, null, asked: false);

    /// <summary>A press <paramref name="session"/>'s conversation asked on a card, with the place it named: its id, for <see cref="Answered"/>.</summary>
    public string Asked(IpcRequest request, string session, HelpPressPlace? place, string? card = null) => Keep(request, session, Waiting, place, asked: true, card);

    public string Barred(IpcRequest request, string session, string why) => Keep(request, session, Refused, null, asked: false, why: why);

    /// <summary>An asked press's card ended: its state from how.</summary>
    public void Answered(string id, PipeConfirmationEnd end) => Settle(id, end switch
    {
        PipeConfirmationEnd.Confirmed => Allowed,
        PipeConfirmationEnd.Refused => Refused,
        PipeConfirmationEnd.Changed => Changed,
        _ => Unanswered,
    });

    public void Received(string id, PressReceipt receipt) => Change(id, press => press with { Receipt = receipt });

    /// <summary>What an asked press made, found by the card it waited on; a card this run holds no press for says nothing.</summary>
    public void ReceivedByCard(string card, PressReceipt receipt)
    {
        string? id;
        lock (_gate) id = _presses.FirstOrDefault(each => each.Card == card)?.Id;
        if (id is not null) Received(id, receipt);
    }

    private string Keep(IpcRequest request, string session, string outcome, HelpPressPlace? place, bool asked, string? card = null, string? why = null)
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        var (scope, target) = Subject(request);
        var press = new HelpPressNow(id, request.Module ?? "", request.Type ?? "", session, scope, target, clock.GetUtcNow(), outcome)
        {
            Place = place?.Place.Wire(),
            Beyond = place?.Beyond ?? false,
            Asked = asked,
            Card = card,
            Why = why,
        };
        lock (_gate)
        {
            _presses.Add(press);
            if (_presses.Count > Kept) _presses.RemoveAt(0);
        }

        Tell(press);
        return id;
    }

    private void Settle(string id, string outcome) => Change(id, press => press with { Outcome = outcome });

    private void Change(string id, Func<HelpPressNow, HelpPressNow> change)
    {
        HelpPressNow? moved = null;
        lock (_gate)
        {
            var at = _presses.FindIndex(each => each.Id == id);
            if (at >= 0) moved = _presses[at] = change(_presses[at]);
        }

        if (moved is not null) Tell(moved);
    }

    private void Tell(HelpPressNow press)
    {
        try
        {
            Moved?.Invoke(press);
        }
        catch (Exception)
        {
            // A hearer's failure is its own: the press is kept here.
        }
    }

    /// <summary>The press's answer came: <c>done</c> where it succeeded, else <c>failed</c>.</summary>
    public void Ended(string id, bool succeeded) => Settle(id, succeeded ? Done : Failed);

    /// <summary>The place the press named, read against the registry and its conversation's focus once it began.</summary>
    public void Placed(string id, HelpPressPlace place) => Change(id, press => press with { Place = place.Place.Wire(), Beyond = place.Beyond });

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
    internal static (string? Scope, string? Target) Subject(IpcRequest request)
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

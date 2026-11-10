using System.Text.Json;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// One press Ask Daoris made in full access this run, run with no card: the route as the window words it
/// (<paramref name="Module"/> and <paramref name="Route"/>), the conversation that pressed it, what the press named, when, and
/// <paramref name="Outcome"/>: <c>running</c>, <c>done</c> or <c>failed</c>.
/// </summary>
/// <param name="Scope">The scope a work press names (<c>WORK_PAUSE</c>'s), which its reverse press carries back.</param>
/// <param name="Target">What the press named: its id, quest, repository or session, whichever it carried first.</param>
public sealed record HelpPressNow(
    string Id, string Module, string Route, string Session, string? Scope, string? Target, DateTimeOffset At, string Outcome);

/// <summary>
/// Ask Daoris's covered presses of this run, for the window's group in Notifications: memory alone, so a restart empties it, as the
/// steward's acts are. The newest <see cref="Kept"/> are held.
/// </summary>
public sealed class HelpPressRecord(TimeProvider clock)
{
    /// <summary>How many presses are held; an older one is dropped.</summary>
    public const int Kept = 100;

    public const string Running = "running";
    public const string Done = "done";
    public const string Failed = "failed";

    private readonly object _gate = new();
    private readonly List<HelpPressNow> _presses = [];

    /// <summary>A covered press begun on <paramref name="session"/>'s conversation: its id, for <see cref="Ended"/>.</summary>
    public string Began(IpcRequest request, string session)
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        var press = new HelpPressNow(id, request.Module ?? "", request.Type ?? "", session, Named(request, "scope"), Target(request), clock.GetUtcNow(), Running);
        lock (_gate)
        {
            _presses.Add(press);
            if (_presses.Count > Kept) _presses.RemoveAt(0);
        }

        return id;
    }

    /// <summary>The press's answer came: <c>done</c> where it succeeded, else <c>failed</c>.</summary>
    public void Ended(string id, bool succeeded)
    {
        lock (_gate)
        {
            var at = _presses.FindIndex(each => each.Id == id);
            if (at >= 0) _presses[at] = _presses[at] with { Outcome = succeeded ? Done : Failed };
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

    private static string? Target(IpcRequest request) =>
        Named(request, "id") ?? Named(request, "quest") ?? Named(request, "repository") ?? Named(request, "session");

    private static string? Named(IpcRequest request, string field) =>
        request.Payload is { ValueKind: JsonValueKind.Object } payload && payload.TryGetProperty(field, out var value)
        && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text ? text : null;
}

using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The person's grant (TERMTRUST1a, D201, the terminal-trust design §4, §5): its record read at the start and written through on each
/// grant and revoke, a terminal's press judged and admitted, and the hold's memory. It reaches the standing confirmation's memory
/// (<see cref="StandingState"/>) under its one lock, each read-modify-write of the record and the memory one step there.
/// </summary>
internal sealed class StandingGrant(StandingConfirmation owner, StandingState state, string machine)
{
    private readonly MachineLog _log = owner.Log;
    private readonly TimeProvider _clock = owner.Clock;

    /// <summary>
    /// Read the record (§4.2): once, as the instance's loop takes the home and before its pipe serves. What it said is logged by code,
    /// <c>person.standing</c> with <see cref="StandingRead"/>'s <c>state</c> and the presses' names, never a value of the record.
    /// </summary>
    public void Load()
    {
        string read;
        IReadOnlyList<string> presses = [];
        DateTimeOffset? since = null;
        var newer = false;
        string? text;
        try
        {
            text = File.Exists(owner.RecordPath) ? AtomicFile.ReadText(owner.RecordPath) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            text = "";
        }

        if (text is null)
        {
            // Absent: off, and nothing to say but that.
            read = StandingRead.Found;
        }
        else
        {
            (read, presses, since) = Parse(text, machine);
            newer = read == StandingRead.Newer;
        }

        lock (state.Gate)
        {
            state.Presses = read == StandingRead.Found ? presses : [];
            state.Since = read == StandingRead.Found && presses.Count > 0 ? since : null;
            state.Unread = read == StandingRead.Found ? null : read;
            state.Newer = newer;
            state.Held.Clear();
            state.Holds.Clear();
            state.Holders.Clear();
            state.Answers.Clear();
        }

        _log.Info(StandingConfirmation.LogEvent, ("state", read), ("presses", Names(read == StandingRead.Found ? presses : [])));
    }

    /// <summary>The record's text read as the instance reads it: its state, its presses in the build's order (unknown names kept out), and its moment.</summary>
    public static (string State, IReadOnlyList<string> Presses, DateTimeOffset? Since) Parse(string text, string machine)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return (StandingRead.Unread, [], null);
            if (!root.TryGetProperty("schemaVersion", out var version) || !version.TryGetInt32(out var number) || number < 1)
            {
                return (StandingRead.Unread, [], null);
            }

            if (number > StandingConfirmation.SchemaVersion) return (StandingRead.Newer, [], null);
            if (Json.Text(root, "machine") is not { } bound || !string.Equals(bound, machine, StringComparison.OrdinalIgnoreCase))
            {
                return (StandingRead.OtherMachine, [], null);
            }

            if (!root.TryGetProperty("presses", out var named) || named.ValueKind != JsonValueKind.Array) return (StandingRead.Unread, [], null);
            var names = named.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.String).Select(each => each.GetString()!).ToHashSet(StringComparer.Ordinal);
            var since = Moments.Read(Json.Text(root, "since"));

            // A later build's press this build does not know is kept out: its table cannot judge it.
            return (StandingRead.Found, [.. StandingPresses.Built.Where(names.Contains)], since);
        }
        catch (JsonException)
        {
            return (StandingRead.Unread, [], null);
        }
    }

    // Under the lock: on where this build offers it and the record names a press.
    public bool IsOn => owner.Offered && state.Presses.Count > 0;

    /// <summary>
    /// The window's grant or revoke (<c>SET_STANDING_CONFIRMATION</c>, §4.1): <paramref name="presses"/> the presses ticked, none to turn it
    /// off. A revoke clears the memory under the lock before its record is written, and stands for this run where the write fails; a
    /// grant whose write fails turns nothing on. A grant this build does not offer, or one naming a press its table does not, is refused
    /// as data and changes nothing. A press waiting on a card keeps its card: a grant confirms no press already asked.
    /// </summary>
    public StandingChange Set(IReadOnlyList<string> presses)
    {
        if (presses.FirstOrDefault(press => !StandingPresses.Built.Contains(press, StringComparer.Ordinal)) is { } unknown)
        {
            return new StandingChange(false, StandingConfirmation.UnknownPress, $"This Daoris has no press `{unknown}` for its standing confirmation.");
        }

        var granted = StandingPresses.Built.Where(press => presses.Contains(press, StringComparer.Ordinal)).ToList();
        if (granted.Count == 0) return Revoke();
        if (!owner.Offered)
        {
            return new StandingChange(false, StandingConfirmation.NotOffered, "This Daoris does not offer the standing confirmation yet.");
        }

        // One mutation step (§2.3): the whole record built from memory, written and installed under the lock, so a grant never
        // interleaves with a revoke.
        lock (state.Gate)
        {
            if (state.Newer)
            {
                return new StandingChange(false, StandingRead.Newer, "Its record is from a newer Daoris, which this one does not write over.");
            }

            var since = _clock.GetUtcNow();
            try
            {
                Write(granted, since);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                _log.Warn(StandingConfirmation.LogEvent, ("state", StandingConfirmation.Unwritten), ("presses", Names(granted)));
                return new StandingChange(false, StandingConfirmation.Unwritten, "Its record could not be written, so nothing was turned on.");
            }

            state.Presses = granted;
            state.Since = since;
            state.Unread = null;
            state.Unwritten = false;
        }

        _log.Info(StandingConfirmation.LogEvent, ("state", "granted"), ("presses", Names(granted)));
        return new StandingChange(true, null, null);
    }

    private StandingChange Revoke()
    {
        bool unwritten;
        lock (state.Gate)
        {
            // At once, before the write (§4.3): a press judged after this asks.
            state.Presses = [];
            state.Since = null;
            state.Held.Clear();
            unwritten = !TryWrite([], null, onlyWhereFound: true);
            state.Unwritten = unwritten;
        }

        _log.Info(StandingConfirmation.LogEvent, ("state", unwritten ? StandingConfirmation.Unwritten : "revoked"), ("presses", ""));
        return unwritten
            ? new StandingChange(true, StandingConfirmation.Unwritten, "Turned off; its record could not be written, so the next start may read it as on.")
            : new StandingChange(true, null, null);
    }

    /// <summary>
    /// Under the lock: the record written as memory now holds it, false where the write failed. A newer record, or another machine's,
    /// is left as it is (§4.2): it is off here already. <paramref name="onlyWhereFound"/> writes nothing where there is no record yet.
    /// </summary>
    private bool TryWrite(IReadOnlyList<string> presses, DateTimeOffset? since, bool onlyWhereFound)
    {
        if (state.Newer || state.Unread == StandingRead.OtherMachine) return true;
        try
        {
            if (!onlyWhereFound || File.Exists(owner.RecordPath)) Write(presses, since);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void Write(IReadOnlyList<string> presses, DateTimeOffset? since) => JsonFile.Write(owner.RecordPath, writer =>
    {
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", StandingConfirmation.SchemaVersion);
        writer.WriteStartArray("presses");
        foreach (var press in presses) writer.WriteStringValue(press);
        writer.WriteEndArray();
        if (since is { } at) writer.WriteString("since", Moments.Write(at));
        writer.WriteString("machine", machine);
        writer.WriteEndObject();
    });

    /// <summary>
    /// Any change of a repository's hold but a covered one (the window's press, a confirmed press): the run forgets it held it, so a
    /// covered resume of it asks (§4.2).
    /// </summary>
    public void Forget(string repository)
    {
        lock (state.Gate) state.Held.Remove(repository);
    }

    /// <summary>
    /// Judge a terminal's press of the person's (§5 step 3) and, where it is covered, admit it under the lock: its use counted, a hold
    /// remembered, a resume's memory taken. Codes only reach the log, never a value.
    /// </summary>
    /// <param name="module">The press's module.</param>
    /// <param name="route">Its route.</param>
    /// <param name="payload">Its payload as the terminal sent it.</param>
    /// <param name="client">The terminal's process, held by the pipe from the connection's taking.</param>
    public StandingJudgement Judge(string module, string route, JsonElement? payload, PipeClientProcess? client)
    {
        var press = PressOf(module, route, payload);
        long seq;
        DateTimeOffset at;
        lock (state.Gate)
        {
            if (!IsOn) return StandingJudgement.Asks(StandingWhy.Off, press, said: false);
            if (press is null || !state.Presses.Contains(press, StringComparer.Ordinal)) return StandingJudgement.Asks(StandingWhy.NotNamed, press, said: false);
            if (!IsForm(press, payload)) return StandingJudgement.Asks(StandingWhy.Form, press, said: true);

            // Read under the lock, so no revoke slips between the judgement and the admission; the look reads the system, never a file.
            var place = owner.Caller.Place(client);
            if (place == StandingPlace.InAgentJob) return StandingJudgement.Asks(StandingWhy.InAgentJob, press, said: true);
            if (place != StandingPlace.Outside) return StandingJudgement.Asks(StandingWhy.Untold, press, said: true);

            var subject = Text(payload, press is StandingPresses.Hold or StandingPresses.Resume ? "repository" : "id")!;
            switch (press)
            {
                case StandingPresses.Finish when owner.RunsHere(subject):
                    return StandingJudgement.Asks(StandingWhy.RunningHere, press, said: true);
                case StandingPresses.Resume when !state.Held.ContainsKey(subject) || !owner.HeldNow(subject):
                    // A hold the file no longer says was changed by someone else: forgotten, and the resume asks.
                    state.Held.Remove(subject);
                    return StandingJudgement.Asks(StandingWhy.NotHeldHere, press, said: true);
                case StandingPresses.Resume:
                    state.Held.Remove(subject);
                    break;
                case StandingPresses.Hold when !owner.HeldNow(subject):
                    at = _clock.GetUtcNow();
                    state.Held[subject] = at;
                    break;
            }

            state.Used++;
            seq = ++state.Seq;
            at = _clock.GetUtcNow();
        }

        owner.Used?.Invoke(seq, at);
        return new StandingJudgement(true, null, press, Said: true);
    }

    /// <summary>The press of the table a route is, whatever its form, or null: a route the table does not name.</summary>
    public static string? PressOf(string module, string route, JsonElement? payload)
    {
        if (!string.Equals(module, StandingConfirmation.DriverModuleName, StringComparison.OrdinalIgnoreCase)) return null;
        return route switch
        {
            "STOP_SESSION" => StandingPresses.Stop,
            "RESOLVE_SESSION" => StandingPresses.Finish,
            "SET_HOLD" => Field(payload, "held") is { ValueKind: JsonValueKind.False } ? StandingPresses.Resume : StandingPresses.Hold,
            _ => null,
        };
    }

    /// <summary>
    /// Whether the payload is exactly the press's form (§2.1): every name, read as the bridge reads names (ignoring case, each once), one
    /// the form allows, of its type and value, and every name the form needs there. Anything more or less is not the form.
    /// </summary>
    public static bool IsForm(string press, JsonElement? payload)
    {
        if (payload is not { ValueKind: JsonValueKind.Object } body) return false;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var form = press switch
        {
            StandingPresses.Stop => new Dictionary<string, Func<JsonElement, bool>>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = NamedText, ["said"] = value => value.ValueKind == JsonValueKind.True,
            },
            StandingPresses.Finish => new(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = NamedText,
                ["state"] = value => value.ValueKind == JsonValueKind.String && value.GetString() == "completed",
                ["said"] = value => value.ValueKind == JsonValueKind.True,
            },
            StandingPresses.Hold => new(StringComparer.OrdinalIgnoreCase)
            {
                ["repository"] = NamedText, ["held"] = value => value.ValueKind == JsonValueKind.True,
            },
            StandingPresses.Resume => new(StringComparer.OrdinalIgnoreCase)
            {
                ["repository"] = NamedText, ["held"] = value => value.ValueKind == JsonValueKind.False,
            },
            _ => null,
        };
        if (form is null) return false;
        foreach (var property in body.EnumerateObject())
        {
            if (!seen.Add(property.Name)) return false;
            if (!form.TryGetValue(property.Name, out var allowed) || !allowed(property.Value)) return false;
        }

        return seen.Count == form.Count;

        static bool NamedText(JsonElement value) => value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text
            && text.Trim().Length == text.Length;
    }

    private static JsonElement? Field(JsonElement? payload, string name)
    {
        if (payload is not { ValueKind: JsonValueKind.Object } body) return null;
        foreach (var property in body.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) return property.Value;
        }

        return null;
    }

    private static string? Text(JsonElement? payload, string name) =>
        Field(payload, name) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;

    private static string Names(IReadOnlyList<string> presses) => string.Join(",", presses);
}

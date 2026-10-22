using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>The presses the standing confirmation may name (D201 point 2, the terminal-trust design §2.1), by the names its record keeps.</summary>
public static class StandingPresses
{
    /// <summary>Stop a session: <c>STOP_SESSION { id, said: true }</c>.</summary>
    public const string Stop = "stop";

    /// <summary>Finish a session that waits on the person, with none of their words: <c>RESOLVE_SESSION { id, state: "completed", said: true }</c>.</summary>
    public const string Finish = "finish";

    /// <summary>Hold a repository: <c>SET_HOLD { repository, held: true }</c>.</summary>
    public const string Hold = "hold";

    /// <summary>Resume a repository a covered hold held during this run: <c>SET_HOLD { repository, held: false }</c>.</summary>
    public const string Resume = "resume";

    /// <summary>
    /// The presses this build's table names, in the order the window offers them. A grant names its own (§2.3): a press a later build
    /// adds (TERMTRUST1b's <c>done</c>) covers nothing until the person grants it.
    /// </summary>
    public static IReadOnlyList<string> Built { get; } = [Stop, Finish, Hold, Resume];
}

/// <summary>Why a press the standing confirmation could have covered asks (the terminal-trust design §5 step 3, §7.2), by code.</summary>
public static class StandingWhy
{
    /// <summary>The grant is off, or its record is not honoured.</summary>
    public const string Off = "off";

    /// <summary>No press of the build's table, or one the record does not name.</summary>
    public const string NotNamed = "not-named";

    /// <summary>The press is not its verb's form: another field, another value, a name twice.</summary>
    public const string Form = "form";

    /// <summary>A resume of a hold this run's memory does not hold.</summary>
    public const string NotHeldHere = "not-held-here";

    /// <summary>A finish of a session with a process on this machine.</summary>
    public const string RunningHere = "running-here";

    /// <summary>The terminal's process is in a job a Daoris on this Windows session made for its agents.</summary>
    public const string InAgentJob = "in-agent-job";

    /// <summary>The judge cannot tell where the terminal's process stands, or an agent of some Daoris here runs uncontained.</summary>
    public const string Untold = "untold";
}

/// <summary>How the standing confirmation's record was read at the instance's start (the machine log's <c>person.standing</c> <c>state</c>).</summary>
public static class StandingRead
{
    /// <summary>A record of this machine and account, read.</summary>
    public const string Found = "found";

    /// <summary>A record that does not read: off.</summary>
    public const string Unread = "unread";

    /// <summary>A record of a newer <c>schemaVersion</c>: off, and never written over by this build.</summary>
    public const string Newer = "newer";

    /// <summary>A record of another machine or account (a <c>data/</c> folder copied here): off, and left.</summary>
    public const string OtherMachine = "other-machine";
}

/// <summary>One judgement: covered, or asks with why (<see cref="StandingWhy"/>); <see cref="Press"/> the press of the table it was, or null.</summary>
/// <param name="Said">Whether the <c>asked</c> line says <see cref="Why"/>: only where the grant is on and names such a press (§7.2).</param>
public sealed record StandingJudgement(bool Covered, string? Why, string? Press, bool Said)
{
    internal static StandingJudgement Asks(string why, string? press, bool said) => new(false, why, press, said);
}

/// <summary>What the window's row reads (<c>STATE</c>'s <c>standingConfirmation</c>, the terminal-trust design §6.1).</summary>
/// <param name="Offered">Whether this build offers the grant (D201's note on order: once part (i) has landed and been looked at on the install).</param>
/// <param name="On">Whether it is on now, honoured, and names a press.</param>
/// <param name="Presses">The presses granted, in the build's order.</param>
/// <param name="Built">The presses this build's table names.</param>
/// <param name="Since">When the grant was made, or null.</param>
/// <param name="Unread">Why the record found is not honoured (<see cref="StandingRead"/>'s <c>unread</c>, <c>newer</c>, <c>other-machine</c>), or null.</param>
/// <param name="Unwritten">That the last revoke could not write its record, so the next start may read it as on.</param>
/// <param name="Used">How many presses it covered since this Daoris started.</param>
/// <param name="Held">The repositories a covered hold held during this run, which a covered resume may release.</param>
public sealed record StandingNow(
    bool Offered, bool On, IReadOnlyList<string> Presses, IReadOnlyList<string> Built, DateTimeOffset? Since, string? Unread,
    bool Unwritten, int Used, IReadOnlyList<string> Held);

/// <summary>A grant's or a revoke's answer: done, or refused as data with a code the page words.</summary>
public sealed record StandingChange(bool Done, string? Why, string? Message);

/// <summary>
/// The person's standing confirmation (TERMTRUST1a part (ii), D201; the terminal-trust design): a grant made in the window, with the
/// person's own press, that lets the presses it names, in exactly their verbs' forms and conditions, from a terminal's process in no job
/// any Daoris on this Windows session made for an agent, run without a card. Everything else asks as today.
/// </summary>
/// <remarks>
/// <para><b>The record</b> (§4.2): <c>data/standing-confirmation.json</c> (<see cref="HomeEntry.StandingConfirmation"/>), <c>presses</c>,
/// <c>since</c>, and <c>machine</c>, a SHA-256 of this machine's name and the account's SID. Read at the instance's start
/// (<see cref="Load"/>, before the pipe serves) and kept in memory, written through on each grant and revoke, atomically, and by
/// nothing else; a write by another process while Daoris runs is not read until the next start. Absent, unreadable, newer or another
/// machine's: off, said in the log and under the row, and never written over by a build that cannot read it.</para>
///
/// <para><b>The hold's memory</b> (§4.2): a covered <c>hold</c> that changed a repository from not held to held is remembered for this
/// run, with its moment; any other change of that repository's hold forgets it (<see cref="Forget"/>), as does a hold the file no longer
/// says, and a start remembers nothing. A covered <c>resume</c> is covered only for a repository remembered so, never a hold the
/// person made, one made alone, or one from before the start.</para>
///
/// <para><b>One lock</b> (§3.3 point 5, §4.3): a press is judged and admitted under it, its use counted and a hold remembered there, so a
/// revoke after it does not recall it and one before it is heard; a revoke clears the memory under it before its record is written.</para>
///
/// <para><b>Until part (i) lands</b> every agent of this process is uncontained, so the caller's look (<see cref="StandingCaller"/>)
/// answers <see cref="StandingWhy.Untold"/> and this covers nothing (D201's note on order). The grant is offered only where
/// <see cref="Offered"/> says so.</para>
/// </remarks>
public sealed class StandingConfirmation
{
    /// <summary>The machine log's event for a grant, a revoke, and what a start found (§7.2).</summary>
    public const string LogEvent = "person.standing";

    /// <summary>The record's version this build reads and writes.</summary>
    public const int SchemaVersion = 1;

    /// <summary>The event that tells every window a press ran under it (§5 step 4), which the line in <i>Notifications</i> counts.</summary>
    public const string UsedEvent = "STANDING_USED";

    /// <summary>
    /// Whether this build offers the grant on This Daoris (D201's note on order, 2026-10-23): not until part (i), the containment, has
    /// landed and its case 4 has been looked at on the install. A build that offers it sets this true; until then the row says it is
    /// not offered yet and the grant's route refuses <c>not-offered</c>, while a revoke is always taken.
    /// </summary>
    public const bool OfferedInThisBuild = false;

    /// <summary>The refusal of a grant this build does not offer yet.</summary>
    public const string NotOffered = "not-offered";

    /// <summary>The refusal of a grant naming a press this build's table does not name.</summary>
    public const string UnknownPress = "unknown-press";

    /// <summary>A grant whose record could not be written, so nothing was turned on; or a revoke's, turned off for this run alone.</summary>
    public const string Unwritten = "unwritten";

    private readonly string _home;
    private readonly MachineLog _log;
    private readonly TimeProvider _clock;
    private readonly string _machine;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, DateTimeOffset> _held = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<string> _presses = [];
    private DateTimeOffset? _since;
    private string? _unread;
    private bool _unwritten;
    private bool _newer;
    private int _used;
    private long _seq;

    /// <param name="home">The home whose record this is.</param>
    /// <param name="log">The machine log.</param>
    /// <param name="caller">Where a terminal's process stands (<see cref="StandingCaller"/> on Windows); a test hands its own.</param>
    /// <param name="clock">The clock a grant's moment is read by.</param>
    /// <param name="offered">Whether the grant is offered (<see cref="OfferedInThisBuild"/> where none is named).</param>
    /// <param name="machine">This machine and account, as the record binds them (<see cref="ThisMachine"/> where none is named).</param>
    public StandingConfirmation(
        string home, MachineLog? log = null, IStandingCaller? caller = null, TimeProvider? clock = null, bool? offered = null,
        string? machine = null)
    {
        _home = home;
        _log = log ?? MachineLog.None;
        Caller = caller ?? new StandingCaller();
        _clock = clock ?? TimeProvider.System;
        Offered = offered ?? OfferedInThisBuild;
        _machine = machine ?? ThisMachine();
    }

    /// <summary>Whether this instance offers the grant (D201's note on order).</summary>
    public bool Offered { get; }

    /// <summary>Where a terminal's process stands.</summary>
    public IStandingCaller Caller { get; }

    /// <summary>Whether a process for a session runs on this machine (the loop's registry, which the loop hands in); none runs nothing.</summary>
    public Func<string, bool> RunsHere { get; set; } = _ => false;

    /// <summary>Whether <c>driver.json</c> holds a repository now (the loop's file, which the loop hands in); none holds nothing.</summary>
    public Func<string, bool> HeldNow { get; set; } = _ => false;

    /// <summary>Told a press it covered, with the window's count (<see cref="UsedEvent"/>); none tells nobody.</summary>
    public Action<long, DateTimeOffset>? Used { get; set; }

    /// <summary>The record's path.</summary>
    public string RecordPath => DaorisHome.Entry(_home, HomeEntry.StandingConfirmation);

    /// <summary>
    /// Read the record (§4.2): once, as the instance's loop takes the home and before its pipe serves. What it said is logged by code,
    /// <c>person.standing</c> with <see cref="StandingRead"/>'s <c>state</c> and the presses' names, never a value of the record.
    /// </summary>
    public void Load()
    {
        string state;
        IReadOnlyList<string> presses = [];
        DateTimeOffset? since = null;
        var newer = false;
        string? text;
        try
        {
            text = File.Exists(RecordPath) ? AtomicFile.ReadText(RecordPath) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            text = "";
        }

        if (text is null)
        {
            // Absent: off, and nothing to say but that.
            state = StandingRead.Found;
        }
        else
        {
            (state, presses, since) = Parse(text, _machine);
            newer = state == StandingRead.Newer;
        }

        lock (_gate)
        {
            _presses = state == StandingRead.Found ? presses : [];
            _since = state == StandingRead.Found && presses.Count > 0 ? since : null;
            _unread = state == StandingRead.Found ? null : state;
            _newer = newer;
            _held.Clear();
        }

        _log.Info(LogEvent, ("state", state), ("presses", Names(state == StandingRead.Found ? presses : [])));
    }

    /// <summary>The record's text read as the instance reads it: its state, its presses in the build's order (unknown names kept out), and its moment.</summary>
    internal static (string State, IReadOnlyList<string> Presses, DateTimeOffset? Since) Parse(string text, string machine)
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

            if (number > SchemaVersion) return (StandingRead.Newer, [], null);
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

    /// <summary>What the window's row reads now.</summary>
    public StandingNow Now()
    {
        lock (_gate)
        {
            return new StandingNow(
                Offered, IsOn, _presses, StandingPresses.Built, _since, _unread, _unwritten, _used,
                [.. _held.Keys.Order(StringComparer.OrdinalIgnoreCase)]);
        }
    }

    // Under the lock: on where this build offers it and the record names a press.
    private bool IsOn => Offered && _presses.Count > 0;

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
            return new StandingChange(false, UnknownPress, $"This Daoris has no press `{unknown}` for its standing confirmation.");
        }

        var granted = StandingPresses.Built.Where(press => presses.Contains(press, StringComparer.Ordinal)).ToList();
        if (granted.Count == 0) return Revoke();
        if (!Offered)
        {
            return new StandingChange(false, NotOffered, "This Daoris does not offer the standing confirmation yet.");
        }

        lock (_gate)
        {
            if (_newer)
            {
                return new StandingChange(false, StandingRead.Newer, "Its record is from a newer Daoris, which this one does not write over.");
            }
        }

        var since = _clock.GetUtcNow();
        try
        {
            Write(granted, since);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            _log.Warn(LogEvent, ("state", Unwritten), ("presses", Names(granted)));
            return new StandingChange(false, Unwritten, "Its record could not be written, so nothing was turned on.");
        }

        lock (_gate)
        {
            _presses = granted;
            _since = since;
            _unread = null;
            _unwritten = false;
        }

        _log.Info(LogEvent, ("state", "granted"), ("presses", Names(granted)));
        return new StandingChange(true, null, null);
    }

    private StandingChange Revoke()
    {
        bool left;
        lock (_gate)
        {
            // At once, before the write (§4.3): a press judged after this asks.
            _presses = [];
            _since = null;
            _held.Clear();
            // A newer record, or another machine's, is left as it is (§4.2): it is off here already.
            left = _newer || _unread == StandingRead.OtherMachine;
        }

        var unwritten = false;
        try
        {
            if (!left && File.Exists(RecordPath)) Write([], null);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            unwritten = true;
        }

        lock (_gate)
        {
            _unwritten = unwritten;
        }

        _log.Info(LogEvent, ("state", unwritten ? Unwritten : "revoked"), ("presses", ""));
        return unwritten
            ? new StandingChange(true, Unwritten, "Turned off; its record could not be written, so the next start may read it as on.")
            : new StandingChange(true, null, null);
    }

    private void Write(IReadOnlyList<string> presses, DateTimeOffset? since) => JsonFile.Write(RecordPath, writer =>
    {
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", SchemaVersion);
        writer.WriteStartArray("presses");
        foreach (var press in presses) writer.WriteStringValue(press);
        writer.WriteEndArray();
        if (since is { } at) writer.WriteString("since", Moments.Write(at));
        writer.WriteString("machine", _machine);
        writer.WriteEndObject();
    });

    /// <summary>
    /// Any change of a repository's hold but a covered one (the window's press, a confirmed press): the run forgets it held it, so a
    /// covered resume of it asks (§4.2).
    /// </summary>
    public void Forget(string repository)
    {
        lock (_gate) _held.Remove(repository);
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
        lock (_gate)
        {
            if (!IsOn) return StandingJudgement.Asks(StandingWhy.Off, press, said: false);
            if (press is null || !_presses.Contains(press, StringComparer.Ordinal)) return StandingJudgement.Asks(StandingWhy.NotNamed, press, said: false);
            if (!IsForm(press, payload)) return StandingJudgement.Asks(StandingWhy.Form, press, said: true);

            // Read under the lock, so no revoke slips between the judgement and the admission; the look reads the system, never a file.
            var place = Caller.Place(client);
            if (place == StandingPlace.InAgentJob) return StandingJudgement.Asks(StandingWhy.InAgentJob, press, said: true);
            if (place != StandingPlace.Outside) return StandingJudgement.Asks(StandingWhy.Untold, press, said: true);

            var subject = Text(payload, press is StandingPresses.Hold or StandingPresses.Resume ? "repository" : "id")!;
            switch (press)
            {
                case StandingPresses.Finish when RunsHere(subject):
                    return StandingJudgement.Asks(StandingWhy.RunningHere, press, said: true);
                case StandingPresses.Resume when !_held.ContainsKey(subject) || !HeldNow(subject):
                    // A hold the file no longer says was changed by someone else: forgotten, and the resume asks.
                    _held.Remove(subject);
                    return StandingJudgement.Asks(StandingWhy.NotHeldHere, press, said: true);
                case StandingPresses.Resume:
                    _held.Remove(subject);
                    break;
                case StandingPresses.Hold when !HeldNow(subject):
                    at = _clock.GetUtcNow();
                    _held[subject] = at;
                    break;
            }

            _used++;
            seq = ++_seq;
            at = _clock.GetUtcNow();
        }

        Used?.Invoke(seq, at);
        return new StandingJudgement(true, null, press, Said: true);
    }

    /// <summary>The press of the table a route is, whatever its form, or null: a route the table does not name.</summary>
    internal static string? PressOf(string module, string route, JsonElement? payload)
    {
        if (!string.Equals(module, DriverModuleName, StringComparison.OrdinalIgnoreCase)) return null;
        return route switch
        {
            "STOP_SESSION" => StandingPresses.Stop,
            "RESOLVE_SESSION" => StandingPresses.Finish,
            "SET_HOLD" => Field(payload, "held") is { ValueKind: JsonValueKind.False } ? StandingPresses.Resume : StandingPresses.Hold,
            _ => null,
        };
    }

    /// <summary>The bridge's driver module, whose routes the table names (the census's <c>DAORIS.DRIVER</c>).</summary>
    internal const string DriverModuleName = "DAORIS.DRIVER";

    /// <summary>
    /// Whether the payload is exactly the press's form (§2.1): every name, read as the bridge reads names (ignoring case, each once), one
    /// the form allows, of its type and value, and every name the form needs there. Anything more or less is not the form.
    /// </summary>
    internal static bool IsForm(string press, JsonElement? payload)
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

    /// <summary>
    /// This machine and account, as the record binds them (§4.2): a SHA-256, in hex, of the machine's name and the account's SID (its name
    /// off Windows), so a <c>data/</c> folder copied to another machine or account is not honoured there.
    /// </summary>
    public static string ThisMachine()
    {
        string account;
        if (OperatingSystem.IsWindows())
        {
            using var identity = WindowsIdentity.GetCurrent();
            account = identity.User?.Value ?? Environment.UserName;
        }
        else
        {
            account = Environment.UserName;
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{Environment.MachineName}\n{account}")));
    }
}

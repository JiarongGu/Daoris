using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Shenora.Core.Events;

namespace Daoris.Desktop;

/// <summary>
/// The session records as every window holds them, the push path's third slice: every record the service answers,
/// closed ones and every workspace's included, allow-listed to the fields the page reads, so a tick reads <c>/api/sessions</c> again in
/// no window, and a record a connector wrote reaches the windows with no desktop dispatch.
/// </summary>
/// <remarks>
/// <para><b>One entry, its variants read from it.</b> The page holds the widest list (every repository, closed included, every
/// workspace) and each view's list is derived from it.</para>
///
/// <para><b>A partition per workspace, keyed by session</b> (<see cref="StateSlice.Keyed"/>): a record's move pushes that record alone,
/// a delta on the number the window holds, never its workspace's history again; a record deleted is named among the keys gone.</para>
///
/// <para><b>Its own read.</b> Every record as the list kept by its change holds it (<see cref="SessionListKept"/>), asking the service
/// only what moved since its last mark, not the look's snapshot, which holds only the open ones; null before the service answers, which
/// keeps the slice not judged.</para>
///
/// <para><b>Its wakes.</b> A change a press or the driver declared to the sessions (the driver's own record moves among them), a
/// conversation's end and the service coming up, on the kit's bus; and the change stream's <c>sessions</c> family, heard where the
/// watch tells the loop its moves (<see cref="DriverLoop.HeardMove"/>), which is how a connector's write arrives.</para>
///
/// <para><b>Desktop-only.</b> A record's tree is a machine path and its words waiting are the person's own, which only this machine's
/// windows read; the transcript and the rest the page does not read stay the pull's.</para>
/// </remarks>
public static class SessionsStateSlice
{
    /// <summary>The slice's name, in the page's words.</summary>
    public const string Name = "sessions";

    /// <summary>
    /// What each record carries: the fields of the page's <c>Session</c> type, as the service names them. A field the page comes to
    /// read is pushed once it is named here, which <c>SessionsStateSliceTests</c> holds against the page's type.
    /// </summary>
    public static readonly IReadOnlyList<string> Fields =
    [
        "id", "quest", "repository", "door", "state", "kind", "note", "evidence", "created", "updated", "workspace", "doorVersion",
        "profile", "tree", "baseCommit", "ask", "opinion", "serves", "from", "fromWhy", "fromBy", "access", "noteParts", "piece", "holdsPlace",
        // The words waiting, each with whose it is, which the page reads a park's answer from.
        "said",
        // An account's limit made its failure: every list words it limited.
        "limit",
        // The machine-local provenance, absent on a mirror and a sync.
        "actor", "choice", "asked", "override", "ran", "chosen",
        // The work its marks read as, judged in .NET and answered on loopback, beside the kind; absent on a record a
        // host answers none for, and then the page reads today's predicates.
        "work",
        // The quest a chat took through its own connector, which the at-work view reads a chat's wait by.
        "tookQuest",
    ];

    /// <summary>
    /// What the service answers of a record that is never pushed: the transcript's path, and what no view reads from the list
    /// (whether its quest is taken by its connector, a stop's cause, its delete, its line of work, its mode, and the downloaded
    /// versions its starts were handed, which the downloads' rule reads; and the part its line ends at, which the groups
    /// slice's entries carry for the page; and <c>decides</c>, which the page reads as the pushed <c>work</c>).
    /// </summary>
    public static readonly IReadOnlyList<string> PulledOnly =
        ["transcript", "took", "interrupted", "deletable", "entry", "helpMode", "resolved", "head", "decides"];

    /// <summary>
    /// What wakes it on the kit's bus: a change declared to the sessions, a conversation's end and the service coming up. The
    /// change stream's family wakes it too (<see cref="HearLedger"/>).
    /// </summary>
    public static readonly IReadOnlyList<SliceWake> WakesOn =
    [
        new(StatePublisher.Type, payload => payload is StateChanged changed && changed.Scopes.Contains("sessions")),
        new(DriverLoop.EndedType),
        new("DRIVER_READY"),
    ];

    /// <summary>
    /// The slice the shell's loop owns, reading <paramref name="loop"/>'s service once it answers. The kept list is the same object
    /// while nothing moved, which is judged once (<see cref="JudgedOnce{TList}"/>): a period with no change does no work.
    /// </summary>
    public static StateSlice Of(DriverLoop loop, IEventBus bus)
    {
        var judged = new JudgedOnce<IReadOnlyList<JsonObject>>(Judge);
        return new(Name, bus, async ct => loop.Service is { } service ? judged.Of(await service.SessionList.Kept.ReadAsync(ct).ConfigureAwait(false)) : null)
        {
            Keyed = true,
            WakesOn = WakesOn,
            Hears = [wake => HearLedger(loop, wake)],
            // The service away or refusing: the windows keep the last judgement, and the next wake reads it again.
            Failed = error => loop.Log?.Warn("state.slice", ("slice", Name), ("message", error.Message)),
        };
    }

    /// <summary>
    /// One judgement of what the service answered: a partition per workspace, named by it, each record under its id with the fields
    /// the page reads. A record with no id is no record; one with no workspace is the default's, as the service names it.
    /// </summary>
    public static IReadOnlyList<SliceJudged> Judge(string answered) =>
        Judge([.. (JsonNode.Parse(answered) as JsonArray ?? throw new JsonException("the service answered the sessions as no list")).OfType<JsonObject>()]);

    /// <summary>One judgement of the records as the list kept by its change holds them (<see cref="SessionListKept"/>), which it only reads.</summary>
    public static IReadOnlyList<SliceJudged> Judge(IReadOnlyList<JsonObject> records)
    {
        var parts = new SortedDictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            if (Text(record, "id") is not { } id) continue;
            var workspace = Text(record, "workspace") ?? "default";
            var picked = new JsonObject();
            foreach (var field in Fields)
            {
                if (record.TryGetPropertyValue(field, out var value)) picked[field] = value?.DeepClone();
            }

            if (!parts.TryGetValue(workspace, out var part)) parts[workspace] = part = new JsonObject();
            part[id] = picked;
        }

        return [.. parts.Select(each => new SliceJudged(each.Key, each.Value))];
    }

    /// <summary>
    /// Wake the slice (<paramref name="wake"/>) where the loop hears the change stream name the <c>sessions</c> family: a record
    /// written by any door, a connector's among them. Another family, or another topic, wakes nothing. Disposed, it hears no more.
    /// </summary>
    public static IDisposable HearLedger(DriverLoop loop, Action wake)
    {
        void Heard(string topic, string subject)
        {
            if (topic == ChangeTopics.Ledger
                && subject.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Contains(Name, StringComparer.Ordinal))
            {
                wake();
            }
        }

        loop.MoveHeard += Heard;
        return new Hearing(() => loop.MoveHeard -= Heard);
    }

    private static string? Text(JsonObject record, string field) =>
        record[field] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text : null;

    private sealed class Hearing(Action stop) : IDisposable
    {
        private int _stopped;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _stopped, 1) == 0) stop();
        }
    }
}

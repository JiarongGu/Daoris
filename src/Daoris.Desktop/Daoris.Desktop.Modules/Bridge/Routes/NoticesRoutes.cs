using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// <i>Done and failed</i>'s doors on the page, `bridge/notices.ts` (NOTIFY2f, D196 point 6; the notification design §2.3, §2.5): the
/// person's read and dismissal of the notices the record keeps, each written under the record's keep lock by this process and pushed
/// by it. The notices themselves are read from the <c>notices</c> slice, never answered here to a page; a terminal's
/// <c>daoris-driver notices</c> reads them, with what else came to the person, as lines (<c>NOTIFICATIONS</c>, NOTIFY2i).
/// </summary>
/// <remarks>
/// <para><b>The person's own</b>: the pipe's census classes both <c>Person</c>, since a terminal that marked the person's news read
/// would hide it from them; Ask Daoris's coverage holds them exempt, the person's own reading.</para>
///
/// <para><b>A write refused is an answer</b>, <c>{ written: false }</c>: the record's lock held past its wait, or a record this build
/// will not write over. The machine log says why; the notice stays as it was, and the press may be made again.</para>
/// </remarks>
public sealed class NoticesRoutes(DriverLoop loop)
{
    private readonly DriverLoop _loop = loop;

    /// <summary>The notices of <c>ids</c> read, as the view shows them or its <i>Mark all read</i> names them.</summary>
    [DriverRoute("NOTICES_READ")]
    [Publishes("NOTICES_READ", StateScopes.Notices)]
    private Task<object?> NoticesReadAsync(IpcRequest request, CancellationToken cancellationToken) =>
        Task.FromResult<object?>(new NoticesWritten(_loop.Notices.MarkRead(Ids(request)) is null));

    /// <summary>
    /// The notice <c>id</c> dismissed; or with <c>all</c>, every notice of the window's <c>workspace</c> and every machine-wide one,
    /// every notice where it names none.
    /// </summary>
    [DriverRoute("NOTICE_DISMISS")]
    [Publishes("NOTICE_DISMISS", StateScopes.Notices)]
    private Task<object?> NoticeDismissAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> ids = Flag(request, "all")
            ? _loop.Notices.Scoped(Optional(request, "workspace"))
            : [Optional(request, "id") ?? throw new ArgumentException("NOTICE_DISMISS names the notice it dismisses (id), or all.")];
        return Task.FromResult<object?>(new NoticesWritten(_loop.Notices.Dismiss(ids) is null));
    }

    /// <summary>
    /// What came to the person, as <c>daoris-driver notices</c> prints it (NOTIFY2i, D196 point 8): the presses waiting for confirmation,
    /// what needs them, their unread messages and the notices, read from what this process holds for its windows and changing nothing.
    /// A terminal's read alone: it answers its lines (<c>said</c>) and nothing a page reads, since the windows hold all of it already.
    /// </summary>
    /// <remarks>
    /// <para><b>An open read</b> in the pipe's census, though <c>PERSON_CONFIRMATIONS</c> is the window's: a press is said here by its act
    /// and its session, never a value it carries nor its asker, so a terminal learns what waits and can answer none of it.</para>
    /// <para><b>Judged nothing for the asking</b>: the slices as last judged, from memory, as <c>STATE_SLICE</c> answers them; the record
    /// read as the slice reads it.</para>
    /// </remarks>
    [DriverRoute("NOTIFICATIONS")]
    private Task<object?> NotificationsAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var notices = Notices.Read(_loop.Home, now);
        var look = notices is null
            ? null
            : Look(_loop.Pipe?.Confirmations.Now(), _loop.ServiceConfirmations.Now(), _loop.Slices.Find(AttentionStateSlice.Name)?.Snapshot(),
                _loop.Slices.Find(NoticesStateSlice.Name)?.Snapshot(), notices);
        return Task.FromResult<object?>(new { Said = NoticesRead.Said(look, Flag(request, "json"), now).ToWire() });
    }

    /// <summary>
    /// The look <c>notices</c> says from what the windows hold: each press of either source by its act and session; each row of <i>What
    /// needs you</i> but a press's own, whose card is the press, in its group; each ask the <c>messages</c> partition holds (none read where
    /// it holds none yet); and <paramref name="notices"/>.
    /// </summary>
    public static NoticesLook Look(
        PipeConfirmationsNow? pipe, ServiceConfirmationsNow service, SliceSnapshot? attention, SliceSnapshot? held, IReadOnlyList<Notice> notices)
    {
        List<PressWaiting> presses = [];
        foreach (var press in pipe?.Waiting ?? [])
        {
            var act = press.Module == DriverModule.Name ? press.Route : $"{press.Module.Replace("DAORIS.", "", StringComparison.Ordinal)}.{press.Route}";
            var named = press.Named ?? PipeWording.Named(press.Module, press.Route, press.Fields);
            var session = named.FirstOrDefault(each => each.Role == "session")?.Value;
            presses.Add(new("terminal", press.Id, act, session, press.Asked, press.Expires, press.ShownAt is not null));
        }

        foreach (var press in service.Waiting)
        {
            if (Json.Text(press, "state") != "waiting" || Json.Text(press, "id") is not { } id || Moment(Json.Text(press, "expires")) is not { } expires) continue;
            var act = Json.Text(press, "act") ?? $"{Json.Text(press, "method")} {Json.Text(press, "route")}".Trim();
            presses.Add(new("service", id, act, null, Moment(Json.Text(press, "asked")), expires, false));
        }

        List<NeedWaiting> needs = [];
        foreach (var (_, node) in Partition(attention, AttentionStateSlice.Rows) ?? [])
        {
            if (node is not JsonObject row || Json.Text(row, "kind") is not { } kind || kind == "confirmation") continue;
            var group = NoticesRead.Group(kind, row["stands"]?.GetValueKind() == JsonValueKind.True, row["stale"] is JsonObject);
            var subject = Json.Text(row, "session") ?? Json.Text(row, "quest") ?? Json.Text(row, "ask") ?? Json.Text(row, "id") ?? "";
            var says = (row["note"] is JsonObject note ? Json.Text(note, "note") : null) ?? Json.Text(row, "detail") ?? Json.Text(row, "title");
            needs.Add(new(group, kind, subject, Json.Text(row, "workspace"), Moment(Json.Text(row, "since")), FirstLine(says)));
        }

        var messages = Partition(held, NoticesStateSlice.Messages);
        List<AskUnread>? asks = messages is null ? null : [];
        foreach (var (_, node) in messages ?? [])
        {
            if (node is not JsonObject ask || Json.Text(ask, "ask") is not { } id) continue;
            var newest = ask["newest"] as JsonObject;
            var unread = ask["unread"] is JsonValue count && count.TryGetValue<int>(out var n) ? n : 0;
            asks!.Add(new(id, Json.Text(ask, "workspace"), ask["teammate"]?.GetValueKind() == JsonValueKind.True, unread,
                newest is null ? null : Json.Text(newest, "who"), newest is null ? null : Json.Text(newest, "line"),
                newest is null ? null : Moment(Json.Text(newest, "at"))));
        }

        return new(presses, needs, asks, notices);
    }

    /// <summary>A slice's partition as last judged, keyed; null where the slice holds none.</summary>
    private static JsonObject? Partition(SliceSnapshot? slice, string partition) =>
        slice?.Partitions.FirstOrDefault(each => each.Partition == partition)?.Data as JsonObject;

    private static DateTimeOffset? Moment(string? text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at) ? at : null;

    /// <summary>Words' first line, cut at 160 characters.</summary>
    private static string? FirstLine(string? words)
    {
        if (string.IsNullOrWhiteSpace(words)) return null;
        var line = words.Trim().Split('\n')[0].TrimEnd('\r');
        return line.Length > 160 ? $"{line[..159]}…" : line;
    }

    private static List<string> Ids(IpcRequest request) =>
        request.Payload is { } payload && payload.TryGetProperty("ids", out var items) && items.ValueKind == JsonValueKind.Array
            ? [.. items.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!)]
            : [];
}

/// <summary>A notice press's answer: whether its write went in; one refused is said in the machine log.</summary>
public sealed record NoticesWritten(bool Written);

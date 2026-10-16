using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// That the line held a branch's committed work, and the evidence it rests on (FABRIC-END2; the fabric events design §B5 and §C7):
/// kept on the session branch's entry (<see cref="GrownBranch.Held"/>) and on the landing's (<see cref="LandedBranch.Held"/>). Written
/// only by the end judgement's keeper (<see cref="EndKeeping"/>), in the process that holds the home's lease. <i>Ended</i> is never
/// stored: a reader derives it from this and the effective cycle.
/// </summary>
/// <param name="At">The last judgement that wrote it.</param>
/// <param name="Since">When it was first held at this tip, line and repository: the origin a deadline counts from (§C3).</param>
/// <param name="By">The proof's code (<see cref="EndBy"/>): <c>empty</c>, <c>merged</c>, <c>on-line</c>, <c>pull-request</c>, <c>landed</c>, <c>carried</c>.</param>
/// <param name="Tip">The commit judged.</param>
/// <param name="Line">The line the cycle named.</param>
/// <param name="On">Where the proof held: the form of the line (as <see cref="LandedBranch.RemovedOn"/> names it), or the landing branch that carried it.</param>
/// <param name="OnAt">That form's commit when judged: a line rewound or replaced under it no longer descends from it.</param>
/// <param name="Repository">The repository judged, by its common dir's folder identity (<see cref="FolderIdentity"/>), so one replaced at the same path is never read as the same.</param>
public sealed record HeldFact(
    DateTimeOffset At, DateTimeOffset Since, string By, string Tip, string? Line, string On, string OnAt, string Repository)
{
    /// <summary>
    /// Whether the fact still holds (§B5): the branch is still at <see cref="Tip"/>, the cycle still names <see cref="Line"/>, the repository
    /// is the one judged, and <see cref="On"/> still descends from <see cref="OnAt"/>. Anything else, an unread answer included, makes the
    /// end not known, never not ended. Asked of a standing branch only: a trace is never judged again (§C6).
    /// </summary>
    /// <param name="onDescends">Whether <see cref="On"/>'s commit now is <see cref="OnAt"/> or descends from it; null where git could not say.</param>
    public bool HoldsAt(string? tip, string? line, string? repository, bool? onDescends) =>
        string.Equals(tip, Tip, StringComparison.Ordinal)
        && string.Equals(line, Line, StringComparison.Ordinal)
        && string.Equals(repository, Repository, StringComparison.Ordinal)
        && onDescends == true;

    /// <summary>Written by hand, as the driver's other files are, for the AOT reason <see cref="DriverConfig"/> gives.</summary>
    internal static void Write(Utf8JsonWriter writer, HeldFact held)
    {
        writer.WriteStartObject("held");
        writer.WriteString("at", Moments.Write(held.At));
        writer.WriteString("since", Moments.Write(held.Since));
        writer.WriteString("by", held.By);
        writer.WriteString("tip", held.Tip);
        if (held.Line is not null) writer.WriteString("line", held.Line);
        writer.WriteString("on", held.On);
        writer.WriteString("onAt", held.OnAt);
        writer.WriteString("repository", held.Repository);
        writer.WriteEndObject();
    }

    /// <summary>
    /// The fact an entry keeps, whole or none: one lacking any of its evidence is no fact, since a reader would say <i>on the line</i> on
    /// it. Absent, which is every entry from before FABRIC-END2, reads as not yet judged.
    /// </summary>
    internal static HeldFact? Read(JsonElement element) =>
        element.TryGetProperty("held", out var held) && held.ValueKind == JsonValueKind.Object
        && Moments.Read(Text(held, "at")) is { } at && Moments.Read(Text(held, "since")) is { } since
        && Text(held, "by") is { } by && Text(held, "tip") is { } tip && Text(held, "on") is { } on && Text(held, "onAt") is { } onAt
        && Text(held, "repository") is { } repository
            ? new HeldFact(at, since, by, tip, Text(held, "line"), on, onAt, repository)
            : null;
}

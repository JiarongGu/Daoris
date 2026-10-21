using System.Text.Json.Nodes;
using Daoris.Driver;
using Shenora.Core.Events;

namespace Daoris.Desktop;

/// <summary>
/// A tool call a session holds for the person's answer, announced as a park is (NOTIFY2d, D196, the notification design §5, §9): on
/// <c>SESSION_ATTENTION</c>, in the shape the shell's notifier and the page's toast read, once, where the attention slice first sees
/// its row, while the notify switch is on.
/// </summary>
/// <remarks>
/// <para><b>Why here.</b> Its bound refuses a held call by itself (PERMUNTIL1), so it waits on the person with a clock, as a park waits
/// on them; until NOTIFY2d it was counted and drawn, and said nowhere, so a call held while the person was in another program ran out
/// unseen. Its row is judged in .NET once for every window (<see cref="NeedsAPerson"/>, D182), so its arrival is judged there too: a
/// row's key the last judgement did not hold.</para>
///
/// <para><b>Seen either way, said under the switch.</b> Every row is seen whatever the switch says, so a call held while the switch
/// was off is not said when it is turned on: the switch is about being told, as the tick's attention is (<c>DriverLoop</c>). What is
/// kept is the rows held now, so it is bounded by what is held, and a call answered or released is forgotten with its row.</para>
///
/// <para><b>Nothing older than this run.</b> What a session holds is the driver's memory of this process (<c>HeldPermissions</c>), so a
/// row seen at the first judgement was held in this run, and a restart holds none to say again.</para>
/// </remarks>
public sealed class HeldCallAnnouncements(IEventBus bus, Func<bool> notifying)
{
    /// <summary>The kind of attention a held tool call is told as.</summary>
    public const string AttentionKind = "Holding";

    /// <summary>The kind of <i>What needs you</i>'s row a held tool call is (<see cref="NeedsAPerson"/>).</summary>
    private const string RowKind = "permission";

    private readonly Lock _gate = new();
    private HashSet<string> _seen = new(StringComparer.Ordinal);

    /// <summary>One judgement of the attention slice: each held call's row not seen at the last is said, while the switch is on.</summary>
    public void Judged(IReadOnlyList<SliceJudged> judged)
    {
        if (judged.FirstOrDefault(each => each.Partition == AttentionStateSlice.Rows)?.Data is not JsonObject rows) return;
        var held = rows
            .Select(each => each.Value as JsonObject)
            .OfType<JsonObject>()
            .Where(row => Json.Text(row, "kind") == RowKind && Json.Text(row, "id") is not null)
            .ToList();

        List<JsonObject> arrived;
        lock (_gate)
        {
            arrived = [.. held.Where(row => !_seen.Contains(Json.Text(row, "id")!))];
            _seen = new HashSet<string>(held.Select(row => Json.Text(row, "id")!), StringComparer.Ordinal);
        }

        if (arrived.Count == 0 || !notifying()) return;
        foreach (var row in arrived) bus.Emit(StatePublisher.Module, PipeConfirmations.AttentionType, Attention(row));
    }

    /// <summary>
    /// The row told as attention: its session, which a notification's click opens, its repository, and the call's title as the agent
    /// wrote it, the balloon's body as a park's question is; <c>Id</c>, the row's own, so a window says it once. The headline is the
    /// English catalogue's, for a reader that reads it.
    /// </summary>
    private static object Attention(JsonObject row)
    {
        var session = Json.Text(row, "session");
        var repository = Json.Text(row, "where") is { Length: > 0 } where ? where : session ?? "";
        return new
        {
            Kind = AttentionKind,
            Id = Json.Text(row, "id"),
            Session = session,
            Repository = repository,
            Headline = SessionBalloon.Words(new BalloonFacts(AttentionKind) { Repository = repository }).Title,
            Detail = row["permission"] is JsonObject call && Json.Text(call, "title") is { Length: > 0 } title ? title : null,
        };
    }
}

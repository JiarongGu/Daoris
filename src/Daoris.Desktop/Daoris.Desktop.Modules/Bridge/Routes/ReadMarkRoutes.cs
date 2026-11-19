using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// Where the person left each conversation on this machine, the page's `bridge/sessions/readMark.ts`: the session's page opens a
/// conversation with anything after its mark at the mark, under what happened since, and moves the mark as the person reads.
/// </summary>
public sealed class ReadMarkRoutes(DriverContext context)
{
    // The mark the conversation's page opens at; none where the person never read it here or the store does not read.
    [DriverRoute("READ_MARK", PipeClass.Open)]
    [ByEvent("READ_MARK", Reads = StateScopes.ReadMarks)]
    private object? ReadMark(ReadMarkRead press) =>
        new { press.Conversation, Mark = Said(new ReadMarks(context.Loop.Home).Read(press.Conversation)) };

    // The newest event the person had on screen while the window had focus; a mark never moves back, and the answer is the one that
    // stands. Read again by each window as it opens the conversation, so no other window's state moves with it.
    [DriverRoute("READ_MARK_MOVE", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Publishes("READ_MARK_MOVE", StateScopes.ReadMarks)]
    [Owner("READ_MARK_MOVE", None = OwnerNone.ActsOnNothing)]
    [Timeless("READ_MARK_MOVE", "a read mark, which never moves back")]
    private object? ReadMarkMove(ReadMarkMove press)
    {
        var at = Moments.Read(press.At) ?? throw new DriverException($"`{press.At}` is no time, so no mark moved.");
        var mark = new ReadMarks(context.Loop.Home).Move(press.Conversation, new ReadMark(press.Part, press.Seq, at, DateTimeOffset.UtcNow, (int?)press.Steps));
        return new { press.Conversation, Mark = Said(mark) };
    }

    private static object? Said(ReadMark? mark) => mark is null ? null : new
    {
        mark.Part, mark.Seq, At = Moments.Write(mark.At), Read = Moments.Write(mark.Read), mark.Steps,
    };
}

using System.Text;

namespace Daoris.Driver;

/// <summary>What a trace prints of a quest's moves, in the driver's English.</summary>
internal static class TraceMovesWords
{
    /// <summary>
    /// A quest's moves, what its page draws on its flowchart: in the order they replay, never
    /// by time, each with its machine and time and the remote's number or none; one the replay did not apply said recorded and not
    /// applied, the hold one set or let go, and a dismissal matched to its conflict by its reference. A host from before the door keeps
    /// no path, and says so.
    /// </summary>
    internal static void Moves(StringBuilder text, TraceQuestLink quest)
    {
        if (quest.MovesMissing == TraceMovesGaps.NoDoor)
        {
            text.Append("  its moves: this service answers none, a host from before the moves door, so its path is not kept here\n");
            return;
        }

        if (quest.MovesMissing == TraceMovesGaps.Unread)
        {
            text.Append($"  its moves could not be read: {quest.MovesProblem}\n");
            return;
        }

        if (quest.Moves is not { } moves) return;
        if (moves.Count == 0)
        {
            text.Append("  its moves: the service's history of it holds none\n");
            return;
        }

        text.Append($"  its moves ({moves.Count}), in the order they replay (the service's history, GET /api/quests/{quest.Id}/moves):\n");
        for (var index = 0; index < moves.Count; index++)
        {
            var move = moves[index];
            text.Append($"    {index + 1} · {MoveSaid(move, moves)}, on {move.Machine}, {TraceWords.When(move.At)}")
                .Append(move.Number is { } number ? $" · the remote's #{number}" : " · only here: no remote has numbered it yet")
                .Append(move.Applied ? "" : $" · recorded, not applied: it stood {move.From ?? "at no quest"}")
                .Append(move.Hold switch
                {
                    null => "",
                    "none" => " · let go what held it",
                    var hold => $" · held: {hold}",
                })
                .Append('\n');
        }
    }

    /// <summary>What one move did, in the driver's words, by its kind; a kind this driver does not know is shown as recorded.</summary>
    private static string MoveSaid(TraceMove move, IReadOnlyList<TraceMove> moves) => move.Kind switch
    {
        "published" or "taken" or "done" or "declined" or "deleted" => move.Kind,
        "conflict" => $"its {move.Attempted ?? "move, its status not recorded,"} kept as a conflict",
        "dismissed" => move.Dismisses is not { } named ? "dismissed a conflict not recorded"
            : ConflictNamed(moves, named) is { } at ? $"dismissed the conflict of move {at + 1}"
            : $"dismissed {named.Machine}'s conflict {named.Sequence}, not in this history",
        "waited" => move.WaitsOn is { } on ? $"waited on quest #{on}" : "waited on a quest not recorded",
        "accepted" => "accepted: the person's yes to what held it",
        "evidenced" => move.Evidence switch
        {
            "found" => "its evidence read: every item found",
            "missing" => "its evidence read: an item not found",
            "commit" => "its submission's commit read",
            _ => "its evidence read, what was found not recorded",
        },
        "setup" => move.SetUp switch
        {
            "local" => "set up for review in a local environment",
            "deployed" => "set up for review in a deployed environment",
            _ => "set up for review",
        },
        "verdict" => move.Verdict is { } said ? $"the person's verdict: {said}" : "the person's verdict, not recorded",
        "bound" => "bound to a graph that closes it",
        "submitted" => move.Session is { } session ? $"submitted by session {session}" : "submitted, by a session not recorded",
        "reopened" => "the person reopened its submission",
        "rebound" => "grew again from its source's submission",
        "chosen" => "the person chose its kind and workflow",
        _ => $"`{move.Kind}`, shown as recorded",
    };

    /// <summary>Where the conflict a dismissal names stands among the moves, by the machine and sequence it keeps; null where it is not among them.</summary>
    private static int? ConflictNamed(IReadOnlyList<TraceMove> moves, TraceMoveRef named)
    {
        for (var index = 0; index < moves.Count; index++)
        {
            if (moves[index] is { Kind: "conflict" } conflict && conflict.Machine == named.Machine && conflict.Sequence == named.Sequence) return index;
        }

        return null;
    }
}

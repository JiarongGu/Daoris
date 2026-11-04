namespace Daoris.Driver;

using Piece = TargetPrompt.Piece;

/// <summary>
/// The pieces of the instruction that claims a quest, and of a retry of it.
/// </summary>
internal static class ClaimedPrompt
{
    /// <summary>A first start (D46 §3): the quest, to take, work and close, or stand down from where it is someone else's.</summary>
    internal static IReadOnlyList<Piece> ClaimingPieces(SessionTarget target) =>
    [
        TargetPrompt.Head(target, $"Your target is quest `#{target.QuestId}`, asked by `{target.Asker}`:"),
        .. TargetPrompt.Beneath(target),
        new(HandedSections.WrittenTo, WrittenTo(target)),
        // A further part of a session is told the parts before it (SESSONE1d), counted as what the sessions before it left.
        new(HandedSections.CarryOn, AgentContext.Current.Parts(target)),
        .. RetriedPieces(target),
        .. TargetPrompt.Tail(target, target.Retried is not null
            ? $"\nFirst take the quest (respond to `#{target.QuestId}` with `take`), then finish its work from there rather\n"
              + "than starting again, inside this repository under its own doctrine and gates, then "
              + (target.Ready
                  ? "say it is ready:\n`ready` when its work is done, with your note and an answer for each requirement. "
                    + $"{TargetPrompt.ReadyWords} Or `decline` with the reason — the\nreason is the part the asker can act on. If the quest is "
                    + "already taken or closed, stand down and\nfinish without changing anything."
                  : "close it:\n`done` when it has landed, or `decline` with the reason — the reason is the part the asker can act\n"
                    + "on. If the quest is already taken or closed, stand down and finish without changing anything.")
              + " Commit as you go, so a\nsecond cut-off loses less."
            : target.Ready
            ? $"\nFirst take the quest (respond to `#{target.QuestId}` with `take`), then do the work inside this\n"
              + "repository under its own doctrine and gates, then say it is ready: `ready` when its work is done,\n"
              + $"with your note and an answer for each requirement. {TargetPrompt.ReadyWords} Or `decline` with the reason — the\n"
              + "reason is the part the asker can act on. If the quest is already taken or closed, stand down and\n"
              + "finish without changing anything."
            : $"\nFirst take the quest (respond to `#{target.QuestId}` with `take`), then do the work inside this\n"
              + "repository under its own doctrine and gates, then close it: `done` when it has landed, or\n"
              + "`decline` with the reason — the reason is the part the asker can act on. If the quest is already\n"
              + "taken or closed, stand down and finish without changing anything."),
    ];

    /// <summary>
    /// What an earlier session on a still-open quest left (RETRYWORK1): it committed work in this tree and failed before it took the
    /// quest, so this session is told its record, its commits on this branch and Daoris's brief of its line, as a carry-on of a take is
    /// (<see cref="CarryingOnPieces"/>). Nothing for every other first start.
    /// </summary>
    internal static IEnumerable<Piece> RetriedPieces(SessionTarget target) => target.Retried is not { } retried
        ? []
        : target.Brief is { } brief
            ?
            [
                new(HandedSections.CarryOn,
                    $"\nAn earlier session on this quest committed work in this tree and failed before it took the quest: {retried} "
                    + "What it did is in this tree — its commits are on this branch, and Daoris's brief\n"
                    + "of its line follows, the tree read as you start.\n\n"),
                .. brief.Pieces.Select(piece => new Piece(piece.Section, piece.Text)),
                new(HandedSections.CarryOn, $"{ResumedPrompt.AccountChanged(target)}\n"),
            ]
            :
            [
                new(HandedSections.CarryOn,
                    $"\nAn earlier session on this quest committed work in this tree and failed before it took the quest: {retried} "
                    + $"What it did is in this tree — its commits are on this branch,\nand {ResumedPrompt.InFlight(target)}{ResumedPrompt.AccountChanged(target)}\n"),
            ];

    /// <summary>
    /// For a first start handed the words the person wrote to an earlier session on its quest after it ended, which could
    /// not go on with them (MSG1b, D137 §2.2): their words, verbatim, quoted line by line. Nothing for any other start: a
    /// carry-on says them in its own paragraph (<see cref="Before"/>).
    /// </summary>
    internal static string WrittenTo(SessionTarget target) => target is { PersonSaid: { } said, CutOff: null, Answered: null }
        ? "\nThe person wrote to an earlier session on this quest after it ended, and it could not go on with their words, so "
          + "they are yours to act on:\n\n"
          + TextBounds.Quote(said, indent: "  ")
          + "\n"
        : "";
}

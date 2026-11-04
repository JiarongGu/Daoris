using System.Text;

namespace Daoris.Driver;

using Piece = TargetPrompt.Piece;

/// <summary>
/// The pieces of the instruction that resumes a session on an answer, or carries on a cut-off one, and what it says of the part before.
/// </summary>
internal static class ResumedPrompt
{
    /// <summary>
    /// A session resuming a quest its own earlier session took and waited on (D79): the quest again,
    /// what was asked and what came back, and — the part a claiming instruction would get wrong — that
    /// the quest is already this session's, so taking it again is refused and standing down is wrong.
    /// </summary>
    internal static IReadOnlyList<Piece> ResumingPieces(SessionTarget target, QuestView answered) =>
    [
        TargetPrompt.Head(target,
            $"You are resuming quest `#{target.QuestId}`, asked by `{target.Asker}`. It is already taken, and it\n"
            + "is yours: do not take it again, and do not stand down."),
        .. TargetPrompt.Beneath(target),
        new(HandedSections.Answered,
            $"\nAn earlier session on this quest needed something only `{answered.To}` could answer, asked it, and\n"
            + "waited. What it did is in this tree — read its commits before you go on. The question was quest\n"
            + $"`#{answered.Id}`, \"{answered.Title}\", and {Answer(answered)}\n"),
        new(HandedSections.WrittenTo, WhileItWaited(target)),
        .. TargetPrompt.Tail(target, target.Ready
            ? "\nCarry on from there, inside this repository under its own doctrine and gates, then say\n"
              + $"`#{target.QuestId}` is ready: `ready` when its work is done, with your note and an answer for each\n"
              + $"requirement. {TargetPrompt.ReadyWords} Or `decline` with the reason — the reason is the part the asker can act on."
            : "\nCarry on from there, inside this repository under its own doctrine and gates, then close\n"
              + $"`#{target.QuestId}`: `done` when it has landed, or `decline` with the reason — the reason is the part\n"
              + "the asker can act on."),
    ];

    /// <summary>
    /// A session carrying on a quest its own earlier session took and was cut off from (D80): the
    /// quest again, what cut the last one off, and what it left in the tree — so it finishes the work
    /// rather than starting it again, and does not take or stand down from a quest that is its own.
    /// </summary>
    internal static IReadOnlyList<Piece> CarryingOnPieces(SessionTarget target, string cutOff) =>
    [
        TargetPrompt.Head(target,
            $"You are carrying on quest `#{target.QuestId}`, asked by `{target.Asker}`. It is already taken, and\n"
            + "it is yours: do not take it again, and do not stand down."),
        .. TargetPrompt.Beneath(target),
        // SESSCTX1b: Daoris's brief of its line follows what the session before left, each of its sections counted on its own (§3.4),
        // the outgoing agent's note among them (MIXAGENT1c); with none, what the tree holds uncommitted and the note, as before.
        .. target.Brief is { } brief
            ? (IEnumerable<Piece>)
            [
                new(HandedSections.CarryOn,
                    $"\n{TargetPrompt.Before(target, cutOff)} What it did is in this tree — any commits it made are on this branch, and Daoris's brief\n"
                    + "of its line follows, the tree read as you start.\n\n"),
                .. brief.Pieces.Select(piece => new Piece(piece.Section, piece.Text)),
                new(HandedSections.CarryOn, $"{AccountChanged(target)}\n"),
            ]
            :
            [
                new(HandedSections.CarryOn,
                    $"\n{TargetPrompt.Before(target, cutOff)} What it did is in this tree — any commits it made are on this branch,\n"
                    + $"and {InFlight(target)}{HandedNote(target)}{AccountChanged(target)}\n"),
            ],
        // LIMITSWITCH3: Daoris's own words that waited on the session this carry-on goes on from, which its cooling account did not take.
        new(HandedSections.WrittenTo, WhileItWaited(target)),
        .. TargetPrompt.Tail(target, target.Ready
            ? "\nFinish from there rather than starting again, inside this repository under its own doctrine and\n"
              + $"gates, then say `#{target.QuestId}` is ready: `ready` when its work is done, with your note and an\n"
              + $"answer for each requirement. {TargetPrompt.ReadyWords} Or `decline` with the reason — the reason is the part\n"
              + "the asker can act on. Commit as you go, so a second cut-off loses less."
            : "\nFinish from there rather than starting again, inside this repository under its own doctrine and\n"
              + $"gates, then close `#{target.QuestId}`: `done` when it has landed, or `decline` with the reason —\n"
              + "the reason is the part the asker can act on. Commit as you go, so a second cut-off loses less."),
    ];

    /// <summary>
    /// Daoris's own words that waited on a record, said to a part that goes on from it (SAYHELD1; LIMITSWITCH3): as Daoris's words and not
    /// the person's, quoted line by line, for the agent to weigh. The one text a resume's instruction, a carry-on's and the ended limit
    /// move's message say them in.
    /// </summary>
    internal static string DaorisWaited(IReadOnlyList<SaidWordView> words) =>
        "While it waited, Daoris told that session what follows, and it never took it, so it is yours to weigh, as Daoris's "
        + "words and not the person's:\n\n"
        + string.Join("\n\n", words.Select(word => TextBounds.Quote(word.Text, indent: "  ")));

    /// <summary>
    /// For a resume (D79), the words said to the session that asked while it waited, which it never took (SAYHELD1): the person's,
    /// verbatim and quoted line by line, then Daoris's own, said as Daoris's. Nothing where it was handed none.
    /// </summary>
    internal static string WhileItWaited(SessionTarget target)
    {
        var persons = target.Handed.Where(word => word.Persons).ToList();
        var daoris = target.Handed.Where(word => word.Daoris).ToList();
        var text = new StringBuilder();
        if (persons.Count > 0)
        {
            text.Append("\nWhile it waited, the person wrote to that session, and it never took their words, so they are yours to act on, ")
                .Append("oldest first:\n\n")
                .Append(string.Join("\n\n", persons.Select(word => TextBounds.Quote(word.Text, indent: "  "))))
                .Append('\n');
        }

        if (daoris.Count > 0)
        {
            text.Append('\n').Append(DaorisWaited(daoris)).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>The tree's uncommitted changes as the driver read them, or that there were none.</summary>
    internal static string InFlight(SessionTarget target) => target.InFlight.Count == 0
        ? "it left nothing uncommitted."
        : "these are the changes it had not committed yet:\n\n"
          + string.Join("\n", target.InFlight.Select(line => $"    {line}"));

    /// <summary>
    /// The note the agent handed over from left (MIXAGENT1c), as its claim, where no brief carries it (SESSCTX1b puts it in the brief's
    /// own section); nothing for any other start.
    /// </summary>
    internal static string HandedNote(SessionTarget target) =>
        target.HandedOver is not null && AgentCourse.Current.HandOverText(target.HandedNote) is { } said ? "\n\n" + said : "";

    /// <summary>
    /// After an account's limit (TOOL4f, D125 §3.5): the session before ran on another account, which is cooling, so
    /// nothing of its own conversation carries over. The harness keeps that in the first account's home, which Daoris
    /// never reads, so the tree and what Daoris recorded are all a carry-on has.
    /// </summary>
    internal static string AccountChanged(SessionTarget target) => target.AccountChanged
        ? "\n\nIt ran on another account, which reached its limit and is cooling, and you run on a different one: nothing "
          + "of its own conversation carries over to you. This tree, the quest and what is above are what it left."
        : "";

    /// <summary>What came back, in the answerer's own words where it gave any.</summary>
    internal static string Answer(QuestView answered)
    {
        var closed = string.Equals(answered.Status, "Declined", StringComparison.OrdinalIgnoreCase)
            ? $"`{answered.To}` declined it"
            : $"`{answered.To}` closed it done";
        return answered.Note is { Length: > 0 } note
            ? $"{closed}, saying:\n\n{TextBounds.Quote(note, bareBlank: false)}"
            : $"{closed} without a note — read that quest, and what `{answered.To}` landed, for the answer.";
    }

    /// <summary>
    /// What the asker gave beside their words, said plainly — each link, each file where it lies, and a
    /// file this machine does not hold said to be elsewhere rather than listed as if a path would open
    /// it. Empty when the quest carries nothing, so an ordinary target reads exactly as it always has.
    /// </summary>
    internal static string Carried(SessionTarget target)
    {
        if (target.Links.Count == 0 && target.Attachments.Count == 0 && target.Parent is null && target.Then.Count == 0)
        {
            return "";
        }

        var text = new StringBuilder();

        // Every line ends in LF, as the rest of the instruction does: AppendLine would end these in the platform's own
        // newline, so the instruction's bytes would differ between machines and no golden could hold it (KNOWUSE1c).
        void Line(string line = "") => text.Append(line).Append('\n');

        // Where it sits in a chain the asker composed: what it follows, and what its close publishes —
        // so a verifying session knows whose work it checks, and a developing one that a check comes.
        if (target.Parent is { } parent)
        {
            Line();
            Line($"It follows quest `#{parent}`, which is done — read that quest for the work this one builds on."
                + (target.GrewFrom is { } branch
                    ? $" That work is in this tree: it grew from `{branch}`, the branch `#{parent}` landed on, "
                      + "which is not merged yet — so there is no merge to wait for or to make."
                    : ""));
        }

        if (target.Then.Count > 0)
        {
            var next = target.Then[0];
            Line();
            Line($"When you close it `done`, the asker's next step is published to `{next.To}`: \"{next.Title}\""
                + (target.Then.Count > 1 ? $", with {target.Then.Count - 1} more after it." : ".")
                + " Close it `done` only when that step can start from what you landed.");
        }

        if (target.Links.Count > 0)
        {
            Line();
            Line("Links the asker gave with it — read them; they are part of the ask:");
            foreach (var link in target.Links) Line($"- {link}");
        }

        if (target.Attachments.Count > 0)
        {
            Line();
            Line(target.AttachmentsDirectory is { } directory
                ? $"Files the asker attached, kept for you in `{directory}` (also `DAORIS_QUEST_ATTACHMENTS`) — read them, never edit them:"
                : "Files the asker attached:");
            foreach (var file in target.Attachments)
            {
                Line(file.Path is { } path
                    ? $"- `{file.Name}` — {path}"
                    : $"- `{file.Name}` — not on this machine: it stayed where the quest was published. Ask for "
                      + "what it shows if the work needs it.");
            }
        }

        return text.ToString();
    }
}

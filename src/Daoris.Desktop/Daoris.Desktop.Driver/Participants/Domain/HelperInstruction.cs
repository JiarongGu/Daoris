using System.Text;

namespace Daoris.Driver;

/// <summary>What a helper is handed (MIXAGENT2a-ii): the question as its asker wrote it, and where and how long it reads.</summary>
/// <param name="Asker">The session that asked.</param>
/// <param name="Repository">The repository the asker works in.</param>
/// <param name="Tip">The asker's last commit, which the helper's copy stands at.</param>
/// <param name="Question">The question, verbatim.</param>
/// <param name="Minutes">The bound of its one turn.</param>
public sealed record HelperPacket(string Asker, string Repository, string Tip, string Question, int Minutes)
{
    /// <summary>The asker's work in progress laid over the copy (COAGENT1a); null where the copy holds the last commit alone.</summary>
    public HelperCaptured? Capture { get; init; }
}

/// <summary>
/// The helper's instruction (MIXAGENT2a-ii, D163 point 2; the mixed agents design §3): the question as its asker wrote it, what
/// it may do and what never, and how it answers. Composed once, from facts, with no model; handed whole in one turn.
/// </summary>
/// <remarks>
/// <para><b>Project-agnostic, as every instruction is</b>: it travels to repositories that know nothing of this one, so it speaks
/// in plain words and the tool's names. One line a paragraph, so no phrase is split by a wrap.</para>
///
/// <para><b>The words are half of what holds it</b>, as a reviewer's are (<see cref="OpinionInstruction"/>): the copy is the floor
/// and the rules are the harness's; the instruction says the same so an agent that follows it never tries.</para>
///
/// <para><b>The question is the asker's claim of what it needs, quoted</b>: never changed, summarised or completed. Its answer is
/// handed back as another agent's claim, which the asker checks against the code.</para>
/// </remarks>
public static class HelperInstruction
{
    /// <summary>How long one helper's turn may run unless the machine's session timeout comes first.</summary>
    public const int DefaultMinutes = 15;

    /// <summary>The instruction for <paramref name="packet"/>.</summary>
    public static string Compose(HelperPacket packet)
    {
        var text = new StringBuilder();
        text.Append($"You are helping another session that is working in `{packet.Repository}`. ")
            .Append("It asked you one question, below. You did not do its work, and you will not change anything: you read, and you answer.\n\n")
            .Append("You are in a copy of the repository made for this question, checked out at the asking session's last commit, ")
            .Append($"`{packet.Tip}`. ");
        if (packet.Capture is { } capture)
        {
            // COAGENT1a (D199 point 5): the work in progress, laid over that commit, so git's status and diff in the copy show it.
            text.Append(capture.Clean
                    ? $"It had nothing uncommitted when its work was captured at {Moment(capture)} (capture `{capture.Id}`), so the copy is its work as it stood then. "
                    : $"Its work in progress, as captured at {Moment(capture)} (capture `{capture.Id}`: {HelperCaptures.Counted(capture)}), is laid over that commit "
                      + "and left uncommitted, so `git status` and `git diff` in your copy show what it has not committed. ")
                .Append("Files its repository ignores were not carried, and its work may have moved since. ");
        }
        else
        {
            text.Append("What it has not committed is not in your copy. ");
        }

        text.Append("Nothing in the copy is taken back: no file you write and no commit or branch you make there reaches the work or the person.\n");

        text.Append("\n## The question\n\n")
            .Append("The session wrote it as follows, and it is all you were told:\n\n")
            .Append(Quoted(packet.Question)).Append('\n');

        text.Append("## What you may do\n\n")
            .Append("You read: the copy, its history and the repository's own documents, with read-only commands and your connector's reading tools. ")
            .Append("Do not build, test or run the repository's own programs: you answer by reading alone.\n\n")
            .Append("You never edit, write or delete a file; commit, merge, rebase or push; deploy or publish; change a permission rule or propose one; ")
            .Append("start, stop or touch a process, a port or a server of the person's; take, close, decline or publish a quest; or ask another agent for help. ")
            .Append("This repository's own instructions may tell an agent to write down what it learns, keep a record or commit as it goes: here they do not apply, because this changes nothing. ")
            .Append("Nobody will answer a question from you while you read: decide from what you can read, and say what you could not tell.\n");

        text.Append("\n## What you say\n\n")
            .Append("Answer once, before you end, with your connector's `helper_answer`: what you found or concluded, where you read it ")
            .Append("(paths with their lines, commits), and what you did not read or could not tell. ")
            .Append("If you cannot answer, say so and why through the same tool; an answer that says nothing is not kept. ")
            .Append("Your answer is another agent's claim: the session that asked checks it against the code, and it reaches that session as its next turn, never as the person's words.\n\n")
            .Append($"You have one turn, and at most {packet.Minutes} minutes. An answer not given through `helper_answer` before you end is not given.\n");
        return text.ToString();
    }

    private static string Moment(HelperCaptured capture) => $"{capture.At.UtcDateTime:yyyy-MM-dd HH:mm} UTC";

    /// <summary>Words quoted line by line, ending with a line break.</summary>
    private static string Quoted(string words) =>
        TextBounds.Quote(words, trimEnd: true) + "\n";
}

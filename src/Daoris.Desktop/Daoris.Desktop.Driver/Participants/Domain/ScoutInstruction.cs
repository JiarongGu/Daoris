using System.Text;

namespace Daoris.Driver;

/// <summary>
/// What a scout is handed: the helper's instruction turned to orient one part of a plan, composed once from facts with no model. The scout is a
/// helper by every rule (a copy of its own, reads only, one turn, <c>helper_answer</c>), so only what it is asked and how it answers differ.
/// </summary>
/// <remarks>
/// Project-agnostic, as every instruction is, and one line a paragraph. The lead's question is quoted as it wrote it; the scout's pack is another
/// agent's claim, which the lead checks against the code before it writes a plan from it.
/// </remarks>
public static class ScoutInstruction
{
    /// <summary>The instruction for <paramref name="packet"/>, which names the part.</summary>
    public static string Compose(HelperPacket packet)
    {
        var scout = packet.Scout ?? throw new ArgumentException("A scout's instruction needs the part it orients.", nameof(packet));
        var text = new StringBuilder();
        text.Append($"You are a scout for the lead of a team building one ask across repositories. The lead asked you to orient one part of its plan: `{scout.Part}`, ")
            .Append($"in `{scout.Repository}`. You did not plan it and you will not build it: you read, and you answer with a pack the builder reads first.\n\n")
            .Append($"You are in a copy of the repository made for this question, checked out at the line's commit, `{packet.Tip}`. ")
            .Append("Nothing in the copy is taken back: no file you write and no commit or branch you make there reaches the work or the person.\n");

        text.Append("\n## What the lead asked\n\n")
            .Append("The lead wrote it as follows, and it is all you were told about the part:\n\n")
            .Append(TextBounds.Quote(packet.Question, trimEnd: true)).Append("\n\n");

        text.Append("## What a pack holds\n\n")
            .Append("Say, one line each and each citing a path with its lines: the area document that describes the code the part changes; the knowledge that applies; ")
            .Append("the files the builder will change; a mirror or a contract the part must stay beside; the tests to run, with their filter; any words other suites read ")
            .Append("that the builder will not run; the paths a generator writes; the paths another part is likely to change too; and what you could not tell.\n\n")
            .Append("Read; do not build, test or run the repository's own programs. You never edit, write or delete a file, commit, push, publish, start or stop a process, ")
            .Append("take or publish a quest, or ask another agent for help. This repository's own instructions to write down what you learn do not apply here, because this changes nothing. ")
            .Append("Nobody will answer a question from you while you read: decide from what you can read.\n");

        text.Append("\n## What you say\n\n")
            .Append("Answer once, before you end, with your connector's `helper_answer`: the pack, in at most 20000 characters. ")
            .Append("It is kept with the plan for the lead to read, and recorded as yours; it is not handed to the lead as a turn. ")
            .Append($"You have one turn, and at most {packet.Minutes} minutes. An answer not given through `helper_answer` before you end is not given.\n");
        return text.ToString();
    }
}

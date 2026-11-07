using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>What became of a held or said word, written to the session's conversation where the person wrote it.</summary>
internal sealed class WordsShown(DriverLoop loop, Action<string> tell)
{
    /// <summary>
    /// The kept words in the session's conversation, the moment the record keeps them: the person's, under the
    /// id the record gave them, with the reach <c>resume</c> and the door; then the loop is nudged so a look takes them up
    /// now rather than at its next interval.
    /// </summary>
    public void Kept(string id, SayAnswer said, HeldSay word)
    {
        if (said.Word is { } kept)
        {
            loop.Output.Append(id, $"— the person said, which the same session goes on with: {kept.Text}");
            loop.Events.Keep(id, new SessionEvent
            {
                Kind = SessionEventKind.User,
                // Shown as whose the record kept it, never the person's by default.
                Origin = kept.Persons ? "person" : "target",
                From = kept.Daoris ? DaorisWords.From : kept.Supervisors ? SupervisorHanded.From : kept.From,
                Id = kept.Id,
                Text = kept.Text,
                Files = kept.Files.Count > 0 ? kept.Files : null,
                Reaches = "resume",
                Door = word.Door,
                // Pressed as an answer of its park's question, shown as that answer chosen.
                Choice = word.Choice is { } choice && kept.Text.StartsWith(choice, StringComparison.Ordinal) ? choice : null,
            }, line => loop.Output.Append(id, line));
            // The record's waiting words are the queue's now: told again, so the box's list offers this one too.
            tell(id);
        }

        loop.Said(id);
    }

    /// <summary>Held words the record would not keep once it ended, said where the person wrote them, never dropped silently.</summary>
    public void Lost(string id, SayAnswer said) =>
        Lost(id, said.Refusal is { } refusal ? WordsNever.Code(refusal) : null, said.Message);

    /// <summary>Held words nothing would take, by the refusal's code where there is one, said where the person wrote them.</summary>
    public void Lost(string id, string? why, string? message)
    {
        // Both ways: the `Why` makes the page say it cannot go on here (it wins where it reads), and the parts word the line
        // by code where there is none or the page does not know it.
        var line = WordsNotes.NotKept(message ?? $"nothing takes them ({why ?? "refused"}).");
        loop.Output.Append(id, line.Note);
        loop.Events.Keep(id, new SessionEvent { Kind = SessionEventKind.Note, Text = line.Note, Parts = line.Parts, Why = why }, null);
    }
}

namespace Daoris.Driver;

/// <summary>
/// The words a native run goes on with: what the person said while it worked, joined into the one
/// argument the native door takes, and the record's opening for the run that takes them.
/// </summary>
internal static class NativeWords
{
    /// <summary>
    /// Every word in the order said, joined by a blank line, each with where its files are kept, as a
    /// conversation's message names them on the native door.
    /// </summary>
    public static string Prompt(IReadOnlyList<ChatMessage> words) =>
        string.Join("\n\n", words.Select(word => word.PromptWithFiles));

    /// <summary>
    /// The words a native run leaves waiting when its turn failed: what the harness said failed, its <c>result</c>'s
    /// reason (<see cref="HarnessWords.Failed"/>), never its exit alone, which ends a run no word is lost to. The words the run
    /// carried and those held behind it, in the order said, with the inbox closed so a word said as it winds up is refused for
    /// the record's door to keep. Null where the run did not fail: it goes on with what is held.
    /// </summary>
    public static IReadOnlyList<ChatMessage>? Left(HarnessWords? own, int? exit, DrivenInbox held, IReadOnlyList<ChatMessage> carried) =>
        own?.Ended(exit).Failure is { Length: > 0 }
            ? [.. carried.Where(word => !word.IsWatch), .. held.Close().Where(word => !word.IsWatch)]
            : null;

    /// <summary>The opening of a carry-on's run: its note alone, since its words are Daoris's own prompt, never a message.</summary>
    public static IReadOnlyList<SessionEvent> Opening(Noted carry) =>
        [new SessionEvent { Kind = SessionEventKind.Note, Text = carry.Note, Parts = carry.Parts.Count > 0 ? carry.Parts : null }];

    /// <summary>
    /// The run's opening in the record: the driver's line, then the same words again under the ids they were shown with
    /// while they waited, now that the session took them.
    /// </summary>
    /// <remarks>Daoris's own words alone are said as Daoris's, never as what the person said.</remarks>
    public static IReadOnlyList<SessionEvent> Opening(string adapter, IReadOnlyList<ChatMessage> words)
    {
        var daoris = words.Count > 0 && words.All(word => word.Daoris is not null);
        var line = daoris
            ? $"— what Daoris told it while it worked is the next turn of its own conversation, resumed on `{adapter}`."
            : $"— what you said while it worked is the next turn of its own conversation, resumed on `{adapter}`.";
        return
        [
            new SessionEvent
            {
                Kind = SessionEventKind.Note,
                Text = line,
                Parts = [(daoris ? StartNoteCodes.ResumedNativeDaoris : StartNoteCodes.ResumedNative).Part(line, ("door", adapter))],
            },
            .. words.Select(ChatMessage.Words),
        ];
    }
}

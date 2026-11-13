namespace Daoris.Driver;

/// <summary>
/// Daoris's word to a session whose harness compacted its context: what the person requires of the quest, quoted as the start handed
/// them, then Daoris's own brief of the line (<see cref="ContextBrief"/>: the words, the plan, the requests still open, the state of
/// the tree), which is the one way a context is handed back. A summary keeps neither the person's words nor the settled work.
/// </summary>
internal static class CompactedWords
{
    /// <summary>What <see cref="ContextBrief"/> says the line "moved" because of: read after <c>because</c>.</summary>
    internal const string Why = "its harness compacted its context";

    /// <summary>The word: the opening, the requirements the target carries, then the brief's own text.</summary>
    internal static string Of(SessionTarget target, string brief) =>
        "Daoris: your harness compacted your context, and a summary keeps neither the person's words nor what was settled. What follows is "
        + "Daoris's record of this quest, which is true whatever your summary says. Do not check again what it shows settled, and read its "
        + "open requests before you file another.\n"
        + RequirementsPrompt.Required(target)
        + "\n" + brief;
}

using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// Words that name a policy proposal still waiting are the owner's alone to say: a press that is not the owner's own (Ask Daoris's, a working
/// session's, a terminal's) never carries one into a conversation, so no agent writes the turn that confirms its own proposal. The owner's
/// window and card are the owner's.
/// </summary>
public static class PolicyWordsGuard
{
    public const string Refusal = "policy-proposal";

    public const string Sentence =
        "These words name a change to the risk policy that waits for the owner: only the owner's own turn may say them, so an agent's press does not carry them. Nothing was sent.";

    /// <summary>Whether words pressed by <paramref name="by"/> (<see cref="TerminalTierBy"/>) name a waiting proposal and are refused.</summary>
    public static bool Refuses(string by, PolicyChanges changes, string text) =>
        by is not (TerminalTierBy.Window or TerminalTierBy.Card) && !string.IsNullOrEmpty(text) && changes.Mentions(text);
}

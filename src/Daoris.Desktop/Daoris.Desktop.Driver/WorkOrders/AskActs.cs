namespace Daoris.Driver;

/// <summary>
/// The <c>ask</c> group: an ask is made from its words, and each act on one is a sub-verb the tree turns into the word the host's ask
/// command reads; <c>channel</c> and <c>at-work</c> are read as typed.
/// </summary>
[CommandGroup("ask", "publish", "close", "delete", "pause", "resume", "abandon", "clear", "go-ahead", "review", "workflow", "through", "channel", "at-work",
    Takes = "\"…\"")]
internal static class AskActs
{
    /// <summary>Each act's sub-verb, and the word the ask's command reads for it.</summary>
    public static IReadOnlyDictionary<string, string> Words { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["publish"] = "--publish", ["close"] = "--close", ["delete"] = "--delete", ["pause"] = "--pause", ["resume"] = "--resume",
        ["abandon"] = "--abandon", ["clear"] = "--clear", ["go-ahead"] = "--go-ahead", ["review"] = "--set-review", ["workflow"] = "--set-workflow",
        ["through"] = "--set-through",
    };
}

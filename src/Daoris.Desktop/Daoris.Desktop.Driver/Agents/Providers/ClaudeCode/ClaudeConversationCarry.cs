namespace Daoris.Driver;

/// <summary>
/// Claude Code's conversation carried between two of its account homes: the conversation's file under the tree's project folder and the
/// folder of the same id beside it (its tool results and subagents), which both doors' <c>--resume</c> and <c>session/load</c> read there.
/// </summary>
internal sealed class ClaudeConversationCarry : IConversationCarry
{
    /// <summary>
    /// Measured on the install: a driven session starts at about 65,000 tokens of context and a briefed one at about 68,000, while a carry
    /// writes its whole context to the new account's cache at its first request and reads it back at every request after. Against a
    /// compaction (one uncached read, a summary written at the output's price, then a context near the start's), carrying whole costs less
    /// up to about 170,000 to 200,000 tokens over the ten or so requests a briefed part's first turn made; 200,000 is also the window of a
    /// model below the long-context one, so past it only a long-context conversation is compacted.
    /// </summary>
    public const long Bound = 200_000;

    /// <summary>The variable that sets the share of the window at which Claude Code compacts a conversation by itself.</summary>
    public const string CompactVariable = "CLAUDE_AUTOCOMPACT_PCT_OVERRIDE";

    public static ClaudeConversationCarry Carry { get; } = new();

    /// <inheritdoc/>
    public long WholeBound => Bound;

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string>? Compacting(long bound, long? size) =>
        size is { } window && window > bound
            ? new Dictionary<string, string> { [CompactVariable] = Math.Clamp(bound * 100 / window, 1, 99).ToString(System.Globalization.CultureInfo.InvariantCulture) }
            : null;

    /// <inheritdoc/>
    public string? Copy(string from, string to, string tree, string conversation)
    {
        var layout = ClaudeConversationHome.Home.Projects!;
        var folder = layout.Flatten(Path.GetFullPath(tree));
        var (source, target) = (Path.Combine(from, layout.Folder, folder), Path.Combine(to, layout.Folder, folder));
        var file = conversation + ".jsonl";
        if (!File.Exists(Path.Combine(source, file))) return "missing";

        Directory.CreateDirectory(target);
        CarriedFiles.Replace(Path.Combine(source, file), Path.Combine(target, file));
        if (Directory.Exists(Path.Combine(source, conversation))) CarriedFiles.Folder(Path.Combine(source, conversation), Path.Combine(target, conversation));
        return null;
    }

    /// <inheritdoc/>
    public void Remove(string to, string tree, string conversation)
    {
        var layout = ClaudeConversationHome.Home.Projects!;
        var target = Path.Combine(to, layout.Folder, layout.Flatten(Path.GetFullPath(tree)));
        File.Delete(Path.Combine(target, conversation + ".jsonl"));
        if (Directory.Exists(Path.Combine(target, conversation))) Directory.Delete(Path.Combine(target, conversation), recursive: true);
    }
}

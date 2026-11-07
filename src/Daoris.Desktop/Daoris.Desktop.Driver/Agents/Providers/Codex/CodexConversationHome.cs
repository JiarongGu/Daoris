namespace Daoris.Driver;

/// <summary>
/// What Codex keeps of a conversation under an account's home, and how it is carried to another: a thread is one rollout file under
/// <c>sessions/</c>, filed by the date it began and named with the thread's id, which <c>session/load</c> reads by that id. It names
/// no working directory in its path, so it declares no project layout. Codex compacts by itself near its own window, and Daoris has no
/// way to ask it to sooner on its protocol door, so a context past the bound is briefed.
/// </summary>
internal sealed class CodexConversationHome : IConversationHome, IConversationCarry
{
    /// <summary>The same break-even as Claude Code's, read on its measured start and carry costs; a Codex window holds about 272,000.</summary>
    public const long Bound = 200_000;

    public static CodexConversationHome Home { get; } = new();

    /// <inheritdoc/>
    public IReadOnlyList<string> Keeps(string? version) => ["sessions"];

    /// <inheritdoc/>
    public IConversationCarry? Carry => this;

    /// <inheritdoc/>
    public long WholeBound => Bound;

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string>? Compacting(long bound, long? size) => null;

    /// <inheritdoc/>
    public string? Copy(string from, string to, string tree, string conversation)
    {
        var sessions = Path.Combine(from, "sessions");
        if (!Directory.Exists(sessions)) return "missing";
        var file = Directory.EnumerateFiles(sessions, "*.jsonl", SearchOption.AllDirectories)
            .FirstOrDefault(each => Path.GetFileNameWithoutExtension(each).EndsWith(conversation, StringComparison.OrdinalIgnoreCase));
        if (file is null) return "missing";

        var placed = Path.Combine(to, Path.GetRelativePath(from, file));
        Directory.CreateDirectory(Path.GetDirectoryName(placed)!);
        CarriedFiles.Replace(file, placed);
        return null;
    }

    /// <inheritdoc/>
    public void Remove(string to, string tree, string conversation)
    {
        var sessions = Path.Combine(to, "sessions");
        if (!Directory.Exists(sessions)) return;
        foreach (var file in Directory.EnumerateFiles(sessions, "*.jsonl", SearchOption.AllDirectories)
                     .Where(each => Path.GetFileNameWithoutExtension(each).EndsWith(conversation, StringComparison.OrdinalIgnoreCase)))
        {
            File.Delete(file);
        }
    }
}

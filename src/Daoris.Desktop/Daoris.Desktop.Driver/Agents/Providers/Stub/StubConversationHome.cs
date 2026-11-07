namespace Daoris.Driver;

/// <summary>
/// What the stand-in keeps of a conversation under its account's home, a file named by the conversation's id in <c>stub-sessions</c>,
/// and how it is carried to another of its accounts. Its bound and its compaction variable are its own, so a carry past the bound can be
/// gated with no model.
/// </summary>
internal sealed class StubConversationHome : IConversationHome, IConversationCarry
{
    /// <summary>The stand-in's own bound, half Claude Code's, so a test's context lies either side of it.</summary>
    public const long Bound = 100_000;

    /// <summary>The variable the stand-in's process reads as the context it compacts past.</summary>
    public const string CompactVariable = "DAORIS_STUB_COMPACT_AT";

    public const string Folder = "stub-sessions";

    public static StubConversationHome Home { get; } = new();

    /// <inheritdoc/>
    public IReadOnlyList<string> Keeps(string? version) => [Folder];

    /// <inheritdoc/>
    public IConversationCarry? Carry => this;

    /// <inheritdoc/>
    public long WholeBound => Bound;

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string>? Compacting(long bound, long? size) =>
        new Dictionary<string, string> { [CompactVariable] = bound.ToString(System.Globalization.CultureInfo.InvariantCulture) };

    /// <inheritdoc/>
    public string? Copy(string from, string to, string tree, string conversation)
    {
        var source = Path.Combine(from, Folder, conversation);
        if (!File.Exists(source)) return "missing";
        Directory.CreateDirectory(Path.Combine(to, Folder));
        CarriedFiles.Replace(source, Path.Combine(to, Folder, conversation));
        return null;
    }
}

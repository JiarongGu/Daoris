namespace Daoris.Driver;

/// <summary>
/// What the second stand-in keeps of a conversation under its home: one folder, <c>alt-sessions</c>, a file for each session, filed by no
/// working directory. It declares no project layout, so the person's-home leftovers read the one provider that does and never this.
/// </summary>
internal sealed class AltStubConversationHome : IConversationHome
{
    public static AltStubConversationHome Home { get; } = new();

    /// <inheritdoc/>
    public IReadOnlyList<string> Keeps(string? version) => ["alt-sessions"];
}

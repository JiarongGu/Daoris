namespace Daoris.Driver;

/// <summary>
/// What Codex says of its children (AGENTPROVIDER1 row l): only the connector's call line naming a thread with its parent and the root.
/// It says no child's end, so a child open in a process alive is never working, and its end stays unknown.
/// </summary>
internal sealed class CodexChildVocabulary : IChildVocabulary
{
    public static CodexChildVocabulary Vocabulary { get; } = new();

    /// <inheritdoc/>
    public IReadOnlyList<string> HookLines { get; } = [];

    /// <inheritdoc/>
    public IReadOnlyList<string> CallKeys { get; } = ["thread", "parent", "root"];

    /// <inheritdoc/>
    public IReadOnlyList<string> StreamSignals { get; } = [];

    /// <inheritdoc/>
    public bool SaysEnd => false;
}

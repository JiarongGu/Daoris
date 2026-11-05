namespace Daoris.Driver;

/// <summary>
/// What Claude Code says of its children (AGENTPROVIDER1 row l): a hook line per call that names the subagent's <c>agent_id</c>, a stream
/// whose <c>task_started</c> and <c>task_notification</c> lines say a subagent's start and end, and a connector call joined to its agent
/// by the hook's line beside it. It says a child's end, so one whose run closed with no end word was lost, never unsaid.
/// </summary>
internal sealed class ClaudeChildVocabulary : IChildVocabulary
{
    public static ClaudeChildVocabulary Vocabulary { get; } = new();

    /// <inheritdoc/>
    public IReadOnlyList<string> HookLines { get; } = ["hook"];

    /// <inheritdoc/>
    public IReadOnlyList<string> CallKeys { get; } = ["toolUse", "caller"];

    /// <inheritdoc/>
    public IReadOnlyList<string> StreamSignals { get; } = ["stream"];

    /// <inheritdoc/>
    public bool SaysEnd => true;

    /// <inheritdoc/>
    public IChildLines Lines() => new ClaudeChildLines();

    /// <inheritdoc/>
    public ChildCall? Wake(System.Text.Json.Nodes.JsonObject line) => ClaudeChildLines.Wake(line);
}

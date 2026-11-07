namespace Daoris.Driver;

/// <summary>
/// What the course's executors ask of Ask Daoris's conversations as the chat runner keeps them: the shell's reading of
/// the machine the room is written from, the conversation of a workspace set aside before another starts, and the room as it stands at an
/// open. Course declares it and Help's <c>ChatHelp</c> implements it; an executor reaches it as <c>Of&lt;IChatHelping&gt;()</c>, and a test
/// stands in for it.
/// </summary>
internal interface IChatHelping : IExecutor
{
    /// <summary>The machine as the room says it: the shell's reading, which the room is written from; null where nothing hands one.</summary>
    Func<CancellationToken, Task<HelpMachine?>>? DescribeHelp { get; }

    /// <summary>Set aside the running Ask Daoris conversation of a workspace; the sentence it is refused in, or null where it is set aside.</summary>
    Task<string?> SetAsideHelpAsync(string? goingOn, string? workspace, CancellationToken ct);

    /// <summary>The room as it stands now for a conversation, and what is read to write it.</summary>
    Task<(string Room, IReadOnlyList<HelpRead> Reads)> HelpRoomNowAsync(string sessionId, string? workspace, CancellationToken ct);
}

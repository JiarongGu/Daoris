namespace Daoris.Driver;

/// <summary>
/// What the course's executors ask of Ask Daoris's conversations as the chat runner keeps them (ASKHIST1, HELPSTART1): the shell's reading of
/// the machine the room is written from, the conversation of a workspace set aside before another starts, and the room as it stands at an
/// open. Course declares it and the chat runner's <c>ChatHelp</c> in Loop implements it.
/// </summary>
internal interface IChatHelping
{
    /// <summary>The machine as the room says it: the shell's reading, which the room is written from; null where nothing hands one.</summary>
    Func<CancellationToken, Task<HelpMachine?>>? DescribeHelp { get; }

    /// <summary>Set aside the running Ask Daoris conversation of a workspace; the sentence it is refused in, or null where it was set aside.</summary>
    Task<string?> SetAsideHelpAsync(string? goingOn, string? workspace, CancellationToken ct);

    /// <summary>The room as it stands now for a conversation, and what was read to write it.</summary>
    Task<(string Room, IReadOnlyList<HelpRead> Reads)> HelpRoomNowAsync(string sessionId, string? workspace, CancellationToken ct);
}

namespace Daoris.Driver;

/// <summary>
/// What the course's executors and the chat runner ask of a conversation's turns (CONV3b, CONV4a): the one dispatcher a word goes through, the
/// hold a hand-over or a limit takes while it moves a conversation, and the queue the page reads. Course declares it and the chat runner's
/// <c>ChatTurns</c> in Loop implements it, as it did before it was a port; the members are exactly those named across the boundary.
/// </summary>
internal interface IChatTurns
{
    /// <summary>Whether the shell has crossed its close boundary, where the runner says so.</summary>
    Func<bool>? Closing { get; set; }

    /// <summary>Told how many words were left unsent when the conversation ended.</summary>
    Action<int>? Unsent { get; set; }

    /// <summary>The door the turns keep their words through, named once.</summary>
    void Keeps(ISaidDoor door);

    /// <summary>The answers-alone bound's judge, named once by whoever says a helper's answer (COWORK1e).</summary>
    void Bounds(Func<ChatMessage, Task<bool>> alone);

    /// <summary>A word kept on the record through its one dispatcher, then taken.</summary>
    Task<TurnKept?> KeepAsync(ISaidDoor door, ChatMessage message, CancellationToken ct);

    /// <summary>The queue as the page reads it.</summary>
    ChatQueue State { get; }

    /// <summary>Whether a turn is in flight.</summary>
    bool Running { get; }

    /// <summary>Whether the turn in flight was stopped.</summary>
    bool Stopped { get; }

    /// <summary>Whether the account's limit refused the last turn and the words wait.</summary>
    bool Limited { get; }

    /// <summary>A waiting word reworded or taken back.</summary>
    QueueEdit EditQueued(string id, string? text, MessageLevel? level = null);

    /// <summary>A waiting word reworded or taken back, its record written first.</summary>
    Task<QueueEdit> EditQueuedAsync(string id, string? text, MessageLevel? level = null, CancellationToken ct = default);

    /// <summary>Hold the turns for a take; false where one is running.</summary>
    bool Hold();

    /// <summary>Hold the turns at the driver's close.</summary>
    bool HoldForClose();

    /// <summary>Let the hold go.</summary>
    void Release();

    /// <summary>The turn's end is the account's limit: the conversation waits for it to lift.</summary>
    bool WaitForLimit();

    /// <summary>The limit lifted: the words that waited go as one message.</summary>
    bool WakeFromLimit(Func<IReadOnlyList<ChatMessage>, ChatMessage> compose);

    /// <summary>The words that waited on a limit, held for a move; null where the turns cannot be held.</summary>
    IReadOnlyList<ChatMessage>? HoldLimited();

    /// <summary>The person's press of a second opinion's turn.</summary>
    Task<PressHold> PressAsync(CancellationToken ct = default);

    /// <summary>The press went.</summary>
    void Pressed();

    /// <summary>The words waiting, moved out of the turns to another.</summary>
    IReadOnlyList<ChatMessage> MoveOut(Func<ChatMessage, bool> onward);

    /// <summary>A hand's word sent back through the turns.</summary>
    Task<SendBack> SendBackAsync(int pressed, Func<int> history, Func<CancellationToken, Task<ChatMessage?>> hand, CancellationToken ct = default);

    /// <summary>A word taken under the hold a take put on the turns, its turn started; null where none could.</summary>
    Task? TakeHeld(ChatMessage message);

    /// <summary>A word taken: where it reaches, or null where nothing took it.</summary>
    TurnReach? Take(ChatMessage message);

    /// <summary>
    /// A word a keyed open moved onto the record, taken as the record's own word under its id there, through its door (FOUNDATION1d-ii part 5):
    /// where it reaches, or null where nothing took it.
    /// </summary>
    TurnReach? TakeMoved(ISaidDoor door, ChatMessage message);

    /// <summary>A word steered into the turn in flight, or taken at once where none runs.</summary>
    Task<SentNow?> TakeNowAsync(ChatMessage message);

    /// <summary>The turn stopped, its waiting words kept on the record.</summary>
    Task<TurnStop> StopKeepingAsync();

    /// <summary>The turn stopped.</summary>
    Task<TurnStop> StopAsync();

    /// <summary>The harness has gone: nothing waiting will be sent.</summary>
    void Gone(bool personStopped = false);
}

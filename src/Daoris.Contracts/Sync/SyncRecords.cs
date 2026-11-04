using Daoris.Contracts.Quests;

namespace Daoris.Contracts.Sync;

// A machine's sync pass (D69, SYNC4), as its driver reads it. Neither operations nor records cross these doors: the remote's own doors
// speak the quest and session wires, and these only say what a pass did.

public sealed record QuestConflictNote(string Quest, string Attempted);

public sealed record QuestPushRefusalWire(string Quest, string Reason);

/// <summary>
/// What a pass did: what the quest half pushed, the moves of this machine that became conflicts, what the remote would not keep, what is
/// still behind; how many session records went up and came down; and the wall, if there was one. <c>Machine</c> is this store's id;
/// <c>CodeMapsFetched</c> is how many of the team's code maps came down, new or moved (MAP3e).
/// </summary>
public sealed record SyncResponse(
    string Workspace, string Machine, bool Wired, int Pushed, IReadOnlyList<QuestConflictNote> Conflicts,
    IReadOnlyList<QuestPushRefusalWire> Refused, IReadOnlyList<string> Behind,
    int SessionsPushed, int SessionsFetched, string? Problem, int CodeMapsFetched);

/// <summary>
/// Where a circle stands on this machine (SYNC6a): what it has not pushed, the quests the last pass left behind, the quests carrying a
/// conflict, when a pass last reached the remote and last tried, and the wall it hit. A circle with no remote here answers <c>wired: false</c>
/// and nothing else. <c>Undeclared</c> is the machines of the workspace its remote last said had not declared they know a bound quest
/// (FABRIC-CLOSE1i2): empty where every one did, absent where no pass heard the handshake.
/// </summary>
public sealed record SyncStandingResponse(
    string Workspace, bool Wired, int Ahead, IReadOnlyList<string> Behind, IReadOnlyList<string> Conflicts,
    DateTimeOffset? Synced, DateTimeOffset? Tried, string? Problem, IReadOnlyList<string>? Undeclared = null);

/// <summary>A person's retirement of a machine at a circle's remote: whether the remote retired one, and the machines it still counts undeclared.</summary>
public sealed record MachineRetiredResponse(
    string Workspace, string Machine, bool Retired, IReadOnlyList<string> Undeclared, string Message);

/// <summary>
/// Where this machine's claim on one quest stands: none, held, unconfirmed or lost (D68 §4). <c>Why</c> is why a lost one lost
/// (<c>source-reopened</c>, FABRIC-CLOSE1c18); <c>Forward</c> the take this machine holds the quest by where the person's bring-forward
/// made it (SYNCMERGE1b0); <c>Taken</c> when the take the quest stands taken by was made, on its machine's clock (CARRY3).
/// </summary>
public sealed record QuestClaimResponse(
    string Quest, string Claim, string? Why = null, QuestForwardResponse? Forward = null, DateTimeOffset? Taken = null);

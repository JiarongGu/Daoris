namespace Daoris.Driver;

/// <summary>
/// What the run found and grew where its session runs, for the stages after the tree: how the tree stands, what the start goes on
/// with and the look at git that stays open until the record is.
/// </summary>
/// <param name="EntryTree">The tree a further part of a session is planned into, when the repository opts in.</param>
/// <param name="Opened">The tree made for this start, or null where it goes on in one that stands.</param>
/// <param name="ResumedIn">The tree a resume or carry-on goes on in, standing.</param>
/// <param name="JoinedIn">The tree a further part of a session went into, standing.</param>
/// <param name="Resuming">Whether the question the quest waits on came back with an answer to resume with.</param>
/// <param name="CarryingOn">Whether the start carries on a session that ended or is cut off, rather than resuming a question.</param>
/// <param name="Answered">The quest the resumed question is put in, read once, or null where the start resumes none.</param>
/// <param name="HandsOn">The words kept on the record that asked, which go to the session this start opens.</param>
/// <param name="InSync">What is done to bring the tree up to date with the line, or null.</param>
/// <param name="OneLook">git's guess at the checkout's line, asked once for this start, let go when the record's instruction is composed.</param>
internal sealed record TreeFound(
    string? EntryTree,
    TreeStanding? PriorStands,
    TreeStanding? EntryStands,
    TreeOpened? Opened,
    string? ResumedIn,
    string? JoinedIn,
    bool Resuming,
    bool CarryingOn,
    QuestView? Answered,
    PriorSession? HandsOn,
    TreeStartSync? InSync,
    TreeLook OneLook);

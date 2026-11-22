namespace Daoris.Driver;

/// <summary>
/// A kind whose red is told to the session that made the work it acts on, not to the step's own (<c>check</c>): it names that session
/// from the run's quests and the revision its attempt acted on, so the runtime asks the kind and names none.
/// </summary>
internal interface ITellsMaker
{
    /// <summary>The session that made the work at <paramref name="revision"/>; null where none is named.</summary>
    string? MakerOf(IReadOnlyList<QuestView> quests, string? revision);
}

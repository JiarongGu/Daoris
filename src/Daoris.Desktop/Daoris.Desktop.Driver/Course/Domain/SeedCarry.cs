namespace Daoris.Driver;

/// <summary>
/// The Seed form asked where a limit's move cannot carry its conversation: the session written as the new door's agent's own conversation in
/// the new account's home, which the new part resumes. The brief stays the floor where a seed cannot help: a context past the bound (a
/// written one is as large), a home Daoris does not own, a door whose provider binds no seeding, or a seed that was refused or failed.
/// </summary>
internal static class SeedCarry
{
    /// <summary>The carry's codes a seed answers: the conversation is not there to copy, but the session's events are.</summary>
    private static readonly HashSet<string> Answers = new(StringComparer.Ordinal) { LimitCarry.Unable, LimitCarry.Unkept, LimitCarry.Missing, LimitCarry.Failed };

    /// <summary>The plan once a briefed one has asked <paramref name="seeding"/> to write <paramref name="ask"/>; any other plan as it is.</summary>
    public static CarryPlan Seeded(CarryPlan plan, IConversationSeeding? seeding, SeedAsk ask)
    {
        if (plan is not CarryPlan.Briefed { Why: var why } || !Answers.Contains(why) || seeding is null) return plan;
        SeedPlacement placed;
        try
        {
            placed = seeding.Place(ask);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or FormatException)
        {
            return plan;
        }

        return placed.Refused is null ? new CarryPlan.Seeded(placed.Conversation, placed) : plan;
    }

    /// <summary>Takes a seed out of the home it was written in, where the door would not resume it.</summary>
    public static void Withdrawn(IConversationSeeding? seeding, string home, CarryPlan.Seeded seeded)
    {
        try
        {
            seeding?.Remove(home, seeded.Placed);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
        }
    }
}

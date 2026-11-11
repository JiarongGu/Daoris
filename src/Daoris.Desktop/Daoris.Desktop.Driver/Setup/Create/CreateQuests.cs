namespace Daoris.Driver;

/// <summary>The first lines of one quest a creation publishes: its <paramref name="Role"/> (<c>setup</c> or <c>work</c>), its title and its opening.</summary>
public sealed record CreateQuestHead(string Role, string Title, string Head);

/// <summary>
/// The quests <c>repository create</c> publishes, said by their first lines for the card the person confirms: the set-up quest, opening as the
/// set-up brief opens (<see cref="SetupRequest"/>, the one source of those words), and the person's work, which follows it as the chained
/// step (<see cref="RepositoryCreateWork"/>). A folder department publishes none: it has no set-up to ask.
/// </summary>
public static class CreateQuests
{
    public const string SetUp = "setup";
    public const string Work = "work";

    /// <summary>How many characters of the person's request the card shows before it is cut.</summary>
    public const int HeadLength = 400;

    /// <summary>The quests a creation of <paramref name="ask"/> publishes on <paramref name="day"/>, in the order they are asked.</summary>
    public static IReadOnlyList<CreateQuestHead> Of(CreateAsk ask, DateOnly day)
    {
        if (ask.Kind != PlaceKind.Git) return [];
        var heads = new List<CreateQuestHead> { new(SetUp, SetupQuests.Title(SetupQuests.SetUp, day), QuestHeads.Plain(SetupRequest.Of(SetupCase.Whole))) };
        if (RepositoryCreateWork.StepOf(ask) is { } step && ask.Work is { } work)
        {
            heads.Add(new(Work, step.Title, TextBounds.Clip(QuestHeads.Plain(work), HeadLength, trimEnd: true)));
        }

        return heads;
    }
}

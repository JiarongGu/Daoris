using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A limit's move whose conversation cannot be carried asks the Seed form of the new door's provider: the session written as that agent's own
/// conversation in the new account's home and resumed there, with no brief. A context past the bound, a home Daoris does not own, a door whose
/// agent takes none in and a seed that fails keep the brief, the floor.
/// </summary>
public sealed class SeedCarryTests
{
    private sealed class Seeding(string? refuses = null) : IConversationSeeding
    {
        public List<SeedAsk> Asked { get; } = [];

        public bool Seeds(string door) => true;

        public SeedPlacement Place(SeedAsk ask)
        {
            Asked.Add(ask);
            return refuses is null ? new SeedPlacement(ask.Conversation, [$"stub-sessions/{ask.Conversation}"], null) : SeedPlacement.Not(ask.Conversation, refuses);
        }

        public void Remove(string home, SeedPlacement placed) { }
    }

    private static readonly ContextEntry[] Context = [new(ContextKind.Work, "the work"), new(ContextKind.Standing, "where it stands")];

    private static SeedAsk Ask => new("stub", "home", "tree", "seed-1", [new SessionEvent { Kind = SessionEventKind.User, Text = "the work" }], Context, DateTimeOffset.UnixEpoch);

    /// <summary>🔴 A conversation its door cannot carry, kept or not, is written as the agent's own and resumed, with no brief.</summary>
    [Theory]
    [InlineData(LimitCarry.Unable)]
    [InlineData(LimitCarry.Unkept)]
    [InlineData(LimitCarry.Missing)]
    [InlineData(LimitCarry.Failed)]
    public void A_conversation_its_door_cannot_carry_is_seeded_and_resumed(string why)
    {
        var seeding = new Seeding();

        var plan = SeedCarry.Seeded(new CarryPlan.Briefed(why), seeding, Ask);

        var seeded = Assert.IsType<CarryPlan.Seeded>(plan);
        Assert.Equal("seed-1", seeded.Conversation);
        Assert.Equal(["stub-sessions/seed-1"], seeded.Placed.Files);
        Assert.Equal(Context, Assert.Single(seeding.Asked).Context);
    }

    /// <summary>A carry is never replaced; past the bound, in a home Daoris does not own, with no seeding or a refused seed, the brief stands.</summary>
    [Fact]
    public void A_carry_a_large_context_an_unowned_home_and_a_refused_seed_keep_their_plan()
    {
        var whole = new CarryPlan.Whole("conv", 10);
        Assert.Same(whole, SeedCarry.Seeded(whole, new Seeding(), Ask));
        Assert.Equal(new CarryPlan.Briefed(LimitCarry.Large), SeedCarry.Seeded(new CarryPlan.Briefed(LimitCarry.Large), new Seeding(), Ask));
        Assert.Equal(new CarryPlan.Briefed(LimitCarry.Unhomed), SeedCarry.Seeded(new CarryPlan.Briefed(LimitCarry.Unhomed), new Seeding(), Ask));
        Assert.Equal(new CarryPlan.Briefed(LimitCarry.Unable), SeedCarry.Seeded(new CarryPlan.Briefed(LimitCarry.Unable), null, Ask));
        Assert.Equal(new CarryPlan.Briefed(LimitCarry.Unable), SeedCarry.Seeded(new CarryPlan.Briefed(LimitCarry.Unable), new Seeding(SeedPlacement.Failed), Ask));
    }

    /// <summary>The new part says it continues from a written conversation, never that one is carried whole.</summary>
    [Fact]
    public void A_seeded_part_says_its_conversation_was_written_for_it()
    {
        var said = CarriedNotes.From("s1", "stub", "account-2", HandOverWhy.Limit, new CarryPlan.Seeded("seed-1", new SeedPlacement("seed-1", [], null)));

        Assert.Contains(said.Parts, part => part.Code == "moved.seeded-from");
        Assert.DoesNotContain("whole", said.Note);
    }

    /// <summary>🔴 The part a seeded move left says its conversation was written for the new agent, never that it was carried.</summary>
    [Fact]
    public void The_part_a_seeded_move_left_says_its_conversation_was_written_never_carried()
    {
        var said = CarriedNotes.On("s2", "codex-acp", "account-2", HandOverWhy.Press, new CarryPlan.Seeded("seed-1", new SeedPlacement("seed-1", [], null)));

        Assert.Contains(said.Parts, part => part.Code == "moved.seeded-on" && Equals(part.Value("why"), "press"));
        Assert.DoesNotContain("carried", said.Note);
        Assert.Contains(CarriedNotes.On("s2", "stub", "account-2", HandOverWhy.Limit, new CarryPlan.Whole("conv", 10)).Parts, part => part.Code == "moved.carried-on");
    }

    /// <summary>🔴 Daoris's word after a carried part's words says the move's reason: the limit's for its own conversation, else why it goes on there.</summary>
    [Fact]
    public void The_carry_s_word_takes_the_move_s_reason()
    {
        var seeded = new CarryPlan.Seeded("seed-1", new SeedPlacement("seed-1", [], null));

        Assert.Equal(LimitCarry.Words, CarryWords.Of(HandOverWhy.Limit, new CarryPlan.Whole("conv", 10)));
        Assert.Equal("This conversation goes on here because the person asked for it. Go on with the work where it stopped.", CarryWords.Of(HandOverWhy.Press, seeded));
        Assert.Contains("its account reached its limit", CarryWords.Of(HandOverWhy.Limit, seeded));
    }
}

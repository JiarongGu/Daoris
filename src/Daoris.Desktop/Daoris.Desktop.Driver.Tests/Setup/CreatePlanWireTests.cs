using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A creation's plan as the window reads it (<c>REGISTRY_CREATE_PLAN</c>'s answer): the ask as it resolved, the folder, line and identity, the
/// steps in order, every refusal at once, the digest the Confirm pins and the first lines of each quest. It is the plan's own values in the
/// plan's own words; nothing is judged here.
/// </summary>
public sealed class CreatePlanWireTests
{
    private static readonly DateOnly Day = new(2026, 11, 12);

    private static JsonElement Wire(CreatePlanned plan) => JsonSerializer.SerializeToElement(CreatePlanWire.Of(plan, Day), new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static CreatePlanned Planned(IReadOnlyList<CreateRefusal>? refusals = null) => new(
        new CreateAsk("ledger", "keeps the books") { Workspace = "aurora", Work = "Build the first screen." },
        "aurora", "/work/aurora/ledger", "main", new CreateAuthor("Fixture", "fixture@example.test"),
        [new CreateStep(CreateOperations.Folder, "make the folder /work/aurora/ledger"), new CreateStep(CreateOperations.SetUp, "ask its own session to set it up")],
        refusals ?? [])
    { RemoteStep = "push" };

    [Fact]
    public void A_plan_is_said_with_every_value_a_card_names()
    {
        var plan = Planned();

        var wire = Wire(plan);

        Assert.Equal(("ledger", "git", "aurora"), (wire.GetProperty("asked").GetProperty("name").GetString(), wire.GetProperty("asked").GetProperty("kind").GetString(), wire.GetProperty("workspace").GetString()));
        Assert.Equal(("/work/aurora/ledger", "main", "push"), (wire.GetProperty("folder").GetString(), wire.GetProperty("line").GetString(), wire.GetProperty("remoteStep").GetString()));
        Assert.Equal(("Fixture", "fixture@example.test"), (wire.GetProperty("author").GetProperty("name").GetString(), wire.GetProperty("author").GetProperty("email").GetString()));
        Assert.Equal([CreateOperations.Folder, CreateOperations.SetUp], wire.GetProperty("steps").EnumerateArray().Select(step => step.GetProperty("id").GetString()));
        Assert.Equal(plan.Digest, wire.GetProperty("digest").GetString());
        Assert.True(wire.GetProperty("applies").GetBoolean());
        Assert.Equal(["setup", "work"], wire.GetProperty("quests").EnumerateArray().Select(quest => quest.GetProperty("role").GetString()));
    }

    [Fact]
    public void A_refused_plan_says_each_refusal_in_its_code_and_sentence()
    {
        var wire = Wire(Planned([new CreateRefusal(CreateRefusals.NoIdentity, "no git identity is chosen.")]));

        Assert.False(wire.GetProperty("applies").GetBoolean());
        var refusal = Assert.Single(wire.GetProperty("refusals").EnumerateArray());
        Assert.Equal((CreateRefusals.NoIdentity, "no git identity is chosen."), (refusal.GetProperty("code").GetString(), refusal.GetProperty("sentence").GetString()));
    }

    /// <summary>What moved between the ask and the Confirm moves the digest, which is all the press's ground reads.</summary>
    [Fact]
    public void The_digest_moves_with_what_the_plan_names()
    {
        var one = Wire(Planned());
        var other = Wire(Planned() with { Folder = "/work/aurora/other" });

        Assert.NotEqual(one.GetProperty("digest").GetString(), other.GetProperty("digest").GetString());
    }
}

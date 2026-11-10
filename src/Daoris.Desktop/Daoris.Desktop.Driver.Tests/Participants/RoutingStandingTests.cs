using Daoris.Contracts.Registry;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The routing standing is judged once, from the drive, the lanes the
/// registry declares and the trees, in the order not-driven, no-lanes, one-at-a-time, open; and it is told only to the works that can publish to their own repository's lanes (build, design, chat).
/// </summary>
public sealed class RoutingStandingTests
{
    private static readonly string[] Lanes = ["core", "web"];

    private static RepoView Repository(params string[] lanes) => new("engine", true, "/work/engine") { Lanes = lanes };

    private static DriverConfig Config(bool driven) => DriverConfig.Empty.WithDrivable("engine", driven);

    [Theory]
    [InlineData(false, false, false, RoutingStanding.NotDriven)]
    [InlineData(false, true, true, RoutingStanding.NotDriven)]
    [InlineData(false, true, false, RoutingStanding.NotDriven)]
    [InlineData(true, false, false, RoutingStanding.NoLanes)]
    [InlineData(true, false, true, RoutingStanding.NoLanes)]
    [InlineData(true, true, false, RoutingStanding.OneAtATime)]
    [InlineData(true, true, true, RoutingStanding.Open)]
    public void The_standing_is_the_first_of_the_four_that_holds_in_section_6_4_s_order(bool driven, bool lanes, bool trees, string standing)
    {
        var repository = Repository(lanes ? Lanes : []);

        Assert.Equal(standing, RoutingStanding.Of(Config(driven), repository, apart: trees).Standing);
    }

    [Fact]
    public void The_registry_s_lanes_are_read_by_their_ids_and_a_host_from_before_lanes_answers_none()
    {
        var read = new[]
        {
            Registered("engine", [new LaneWire("core", "Core", null, null), new LaneWire("web", null, null, null), new LaneWire(null, "no id", null, null)]),
            Registered("game", null),
            Registered("tools", []),
        }.Select(RegistryDoors.ViewOf).ToList();

        Assert.Equal(["core", "web"], read[0].Lanes);
        Assert.Empty(read[1].Lanes);
        Assert.Empty(read[2].Lanes);
    }

    private static RegistrationResponse Registered(string repository, IReadOnlyList<LaneWire>? lanes) =>
        new(repository, true, true, null, [], [], [], 0, "/work/" + repository, false, false, "default", null, false, [], lanes);

    [Fact]
    public void A_repository_the_registry_answered_no_lanes_for_declares_none()
    {
        var older = new RepoView("engine", true, "/work/engine");

        Assert.Equal(RoutingStanding.NoLanes, RoutingStanding.Of(Config(driven: true), older, apart: true).Standing);
    }

    [Fact]
    public void The_drive_is_read_whatever_the_case_the_repository_is_named_in()
    {
        var config = DriverConfig.Empty.WithDrivable("Engine", true);

        Assert.Equal(RoutingStanding.Open, RoutingStanding.Of(config, Repository("core"), apart: true).Standing);
    }

    [Fact]
    public void The_judge_keeps_the_lanes_the_registry_declares()
    {
        Assert.Equal(Lanes, RoutingStanding.Of(Config(true), Repository(Lanes), apart: true).Lanes);
        Assert.Equal(Lanes, RoutingStanding.Of(Config(false), Repository(Lanes), apart: true).Lanes);
    }

    [Theory]
    [InlineData(ParticipantWork.Build, true)]
    [InlineData(ParticipantWork.Design, true)]
    [InlineData(ParticipantWork.Exact, true)]
    [InlineData(ParticipantWork.Chat, true)]
    [InlineData(ParticipantWork.Help, false)]
    [InlineData(ParticipantWork.Partner, false)]
    [InlineData(ParticipantWork.Review, false)]
    [InlineData(ParticipantWork.Intake, false)]
    [InlineData(ParticipantWork.AskDaoris, false)]
    [InlineData(ParticipantWork.Brainstorm, false)]
    [InlineData(null, false)]
    public void Only_build_design_and_chat_are_told_it(string? work, bool told)
    {
        var line = RoutingStanding.Told(work, Config(true), Repository(Lanes), apart: true);

        Assert.Equal(told, line is not null);
    }

    [Fact]
    public void Every_work_the_participant_rule_knows_is_decided_here_one_way_or_the_other()
    {
        var tells = ParticipantWork.Codes.Where(work => RoutingStanding.Told(work, Config(true), Repository(Lanes), apart: true) is not null);

        Assert.Equal([ParticipantWork.Build, ParticipantWork.Design, ParticipantWork.Exact, ParticipantWork.Chat], tells);
    }

    [Fact]
    public void A_repository_the_registry_does_not_name_is_told_nothing()
    {
        Assert.Null(RoutingStanding.Told(ParticipantWork.Chat, Config(true), null, apart: true));
    }
}

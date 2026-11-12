using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A room is asked, never made. <c>place &lt;repository&gt; --room &lt;path&gt; --purpose "…"</c> is a
/// part of the one place plan, judged with every refusal at once (a path outside the repository, an existing room, a repository not
/// registered, held or not adopted), and its whole is one quest to that repository's own session. Fast: the world is a stand-in.
/// </summary>
public sealed partial class PlaceRoomTests
{
    private const string Why = "the web app needs conventions of its own";

    private static readonly SetupPresetsTests.Catalog Plugins = new(new SetupPresetsTests.Entry("example.github-pull-request", [HookPoints.Perform]));

    private static readonly DriverConfig Config = (DriverConfig.Empty with { Actors = StaffedRoles.On("claude-code-acp") }).WithDrivable("engine", true);

    private readonly string _home = Path.Combine(AppContext.BaseDirectory, "place-room-" + Guid.NewGuid().ToString("N")[..8]);

    internal static PlaceAsk Room(string repository = "engine", string path = "tools/web", string? purpose = Why) =>
        new([repository], "") { Room = path, Purpose = purpose };

    private Task<PlacePlanned> PlanAsync(PlacePlanTests.World world, PlaceAsk ask, DriverConfig? config = null) =>
        PlacePlans.PlanAsync(world, config ?? Config, _home, Plugins, true, ask);

    private static string Sentences(PlacePlanned plan) => string.Join(" | ", plan.Refusals.Select(each => $"{each.Code}: {each.Sentence}"));

    // ---- the plan: one quest, nothing written ----

    [Fact]
    public async Task A_room_of_an_adopted_registered_repository_is_one_change_the_quest_and_the_plan_writes_and_asks_nothing()
    {
        var world = new PlacePlanTests.World();

        var plan = await PlanAsync(world, Room());

        Assert.True(plan.Applies, Sentences(plan));
        var engine = Assert.Single(plan.Repositories);
        var change = Assert.Single(engine.Changes);
        Assert.Equal((PlacePlans.RoomPressKey, "room", "no room", "asked"), (change.Key, change.Part, change.Before, change.After));
        Assert.Contains("`tools/web`", change.Said);
        Assert.Equal([PlacePlans.RoomPressKey], plan.Changes.Select(each => each.Change.Key));
        Assert.Equal(["engine"], plan.Changes[0].Repositories);
        Assert.Empty(plan.Saves);
        Assert.Equal(["registry", "quests", "room engine tools/web"], world.Asked);
        Assert.Equal($"daoris place engine --room tools/web --purpose \"{Why}\" --plan", plan.Terminal);
        Assert.True(PlaceDigests.Is(plan.Digest));
    }

    [Fact]
    public async Task The_path_is_said_as_the_repository_keeps_it_whichever_way_it_was_typed()
    {
        foreach (var typed in new[] { "tools/web", "tools\\web", "tools/web/", "./tools/web", "tools//web" })
        {
            var plan = await PlanAsync(new PlacePlanTests.World(), Room(path: typed));

            Assert.True(plan.Applies, $"{typed}: {Sentences(plan)}");
            Assert.Equal("tools/web", plan.Asked.Room);
        }

        var one = await PlanAsync(new PlacePlanTests.World(), Room());
        var other = await PlanAsync(new PlacePlanTests.World(), Room(path: "tools\\web\\"));
        Assert.Equal(one.Digest, other.Digest);
    }

    [Fact]
    public async Task The_digest_moves_with_the_path_the_reason_and_the_repository_alone()
    {
        var world = () => new PlacePlanTests.World();
        var one = (await PlanAsync(world(), Room())).Digest;

        Assert.Equal(one, (await PlanAsync(world(), Room())).Digest);
        Assert.NotEqual(one, (await PlanAsync(world(), Room(path: "tools/api"))).Digest);
        Assert.NotEqual(one, (await PlanAsync(world(), Room(purpose: "another reason"))).Digest);
        Assert.NotEqual(one, (await PlanAsync(world(), Room(repository: "docs"))).Digest);
    }

    [Fact]
    public async Task A_repository_not_driven_here_has_its_room_quest_admitted_alone_and_the_plan_says_so()
    {
        var plan = await PlanAsync(new PlacePlanTests.World(), Room(repository: "docs"));

        Assert.True(plan.Applies, Sentences(plan));
        Assert.Equal([PlacePlans.AloneSaid], plan.Repositories[0].Changes.Skip(1).Select(change => change.Said));
        Assert.Equal("its room quest alone", plan.Repositories[0].Changes[1].After);
    }

    // ---- refusals, every one at once with its code ----

    [Theory]
    [InlineData("", PlaceRoomRefusals.Path)]
    [InlineData("   ", PlaceRoomRefusals.Path)]
    [InlineData(".", PlaceRoomRefusals.Path)]
    [InlineData("..", PlaceRoomRefusals.Path)]
    [InlineData("../elsewhere", PlaceRoomRefusals.Path)]
    [InlineData("tools/../../elsewhere", PlaceRoomRefusals.Path)]
    [InlineData("tools/../web", PlaceRoomRefusals.Path)]
    [InlineData("/etc/room", PlaceRoomRefusals.Path)]
    [InlineData("\\windows\\room", PlaceRoomRefusals.Path)]
    [InlineData("C:/room", PlaceRoomRefusals.Path)]
    [InlineData("C:\\room", PlaceRoomRefusals.Path)]
    [InlineData("c:room", PlaceRoomRefusals.Path)]
    [InlineData("\\\\server\\share\\room", PlaceRoomRefusals.Path)]
    [InlineData("tools/we:b", PlaceRoomRefusals.Path)]
    [InlineData(".git", PlaceRoomRefusals.Path)]
    [InlineData(".git/hooks", PlaceRoomRefusals.Path)]
    [InlineData(".claude/room", PlaceRoomRefusals.Path)]
    [InlineData(".agents/room", PlaceRoomRefusals.Path)]
    [InlineData("tools/we\u0000b", PlaceRoomRefusals.Path)]
    public async Task A_path_that_is_not_a_folder_inside_the_repository_is_refused_with_nothing_asked_of_it(string path, string code)
    {
        var world = new PlacePlanTests.World();

        var plan = await PlanAsync(world, Room(path: path));

        Assert.False(plan.Applies);
        Assert.Contains(plan.Refusals, each => each.Code == code && each.Part == PlacePlans.Room);
        Assert.Empty(plan.Changes);
        Assert.DoesNotContain(world.Asked, each => each.StartsWith("room ", StringComparison.Ordinal));
    }

    /// <summary>A path through a link on the line leaves the repository however it reads, and a file is no folder to make a room of.</summary>
    [Fact]
    public async Task A_path_through_a_link_or_onto_a_file_on_the_line_is_refused()
    {
        var world = new PlacePlanTests.World();
        world.Rooms["engine"] = new PlaceRoomRead("main", "c0ffee", true, [], [], [], Through: "tools", IsFile: false);

        var linked = await PlanAsync(world, Room());

        Assert.Contains(linked.Refusals, each => each.Code == PlaceRoomRefusals.Path && each.Sentence.Contains("`tools`", StringComparison.Ordinal)
            && each.Sentence.Contains("link", StringComparison.Ordinal));

        world.Rooms["engine"] = new PlaceRoomRead("main", "c0ffee", true, [], [], [], Through: null, IsFile: true);
        var file = await PlanAsync(world, Room());

        Assert.Contains(file.Refusals, each => each.Code == PlaceRoomRefusals.Path && each.Sentence.Contains("file", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("rooms", "daoris.json")]
    [InlineData("folders", "AGENTS.md")]
    [InlineData("lanes", "daoris.lanes.json")]
    public async Task A_room_already_there_is_refused_by_the_declaration_that_holds_it(string where, string named)
    {
        var world = new PlacePlanTests.World();
        world.Rooms["engine"] = where switch
        {
            "rooms" => new PlaceRoomRead("main", "c0ffee", true, ["docs", "Tools/Web/"], [], [], null, false),
            "folders" => new PlaceRoomRead("main", "c0ffee", true, [], [], ["tools/web"], null, false),
            _ => new PlaceRoomRead("main", "c0ffee", true, [], ["tools/web"], [], null, false),
        };

        var plan = await PlanAsync(world, Room());

        var exists = Assert.Single(plan.Refusals, each => each.Code == PlaceRoomRefusals.Exists);
        Assert.Contains("`tools/web`", exists.Sentence);
        Assert.Contains(named, exists.Sentence);
        Assert.False(plan.Applies);
        Assert.Empty(plan.Changes);
    }

    [Fact]
    public async Task A_neighbouring_room_is_no_room_there()
    {
        var world = new PlacePlanTests.World();
        world.Rooms["engine"] = new PlaceRoomRead("main", "c0ffee", true, ["tools/web-old", "tools"], ["web"], ["tools/web/deep"], null, false);

        var plan = await PlanAsync(world, Room());

        Assert.True(plan.Applies, Sentences(plan));
    }

    [Fact]
    public async Task A_repository_not_registered_is_refused_naming_the_door_that_registers_it()
    {
        var plan = await PlanAsync(new PlacePlanTests.World(), Room(repository: "ghost"));

        var refusal = Assert.Single(plan.Refusals);
        Assert.Equal((SetupRefusals.NotOnRegistry, "ghost"), (refusal.Code, refusal.Repository));
        Assert.Contains("repository import", refusal.Sentence);
    }

    [Fact]
    public async Task A_repository_not_adopted_is_refused_naming_the_set_up_that_comes_first()
    {
        // The registry's row says not adopted (`atlas`), and a row that says adopted whose line holds no manifest says it the same way.
        var asked = await PlanAsync(new PlacePlanTests.World(), Room(repository: "atlas"));
        var world = new PlacePlanTests.World();
        world.Rooms["engine"] = new PlaceRoomRead("main", "c0ffee", Adopted: false, [], [], [], null, false);
        var line = await PlanAsync(world, Room());

        foreach (var plan in new[] { asked, line })
        {
            var refusal = Assert.Single(plan.Refusals, each => each.Code == SetupRefusals.NotAdopted);
            Assert.Contains(SetupQuests.SetUp, refusal.Sentence);
            Assert.Empty(plan.Changes);
        }
    }

    [Fact]
    public async Task A_held_repository_is_refused_with_the_door_that_resumes_it()
    {
        var plan = await PlanAsync(new PlacePlanTests.World(), Room(), Config.WithHold("engine", true));

        var refusal = Assert.Single(plan.Refusals);
        Assert.Equal((PlaceRoomRefusals.Held, "engine"), (refusal.Code, refusal.Repository));
        Assert.Contains("daoris place resume engine", refusal.Sentence);
    }

    [Fact]
    public async Task A_repository_with_no_checkout_here_and_one_whose_line_cannot_be_read_are_refused()
    {
        var world = new PlacePlanTests.World();
        world.Registry.Add(new RepoView("rootless", true, null, "aurora"));
        world.Tips["rootless"] = new PlaceTip(null, null, null, null, null);
        world.Rooms["docs"] = new PlaceRoomRead(null, null, false, [], [], [], null, false, Problem: "`docs`'s line is not known here.");

        var rootless = await PlanAsync(world, Room(repository: "rootless"));
        var unread = await PlanAsync(world, Room(repository: "docs"));

        Assert.Contains(rootless.Refusals, each => each.Code == SetupRefusals.NoCheckout);
        Assert.Equal([PlaceApplyRefusals.Unread], unread.Refusals.Select(each => each.Code));
        Assert.Contains("line is not known here", unread.Refusals[0].Sentence);
    }

    [Fact]
    public async Task A_set_up_already_open_for_the_repository_is_refused_as_the_set_up_press_refuses_a_second()
    {
        var world = new PlacePlanTests.World();
        world.Quests.Add(new QuestView("q9", "work", "engine", $"{SetupQuests.SetUp} (2026-11-01)", "", "Taken"));

        var plan = await PlanAsync(world, Room());

        Assert.Contains(plan.Refusals, each => each.Code == SetupRefusals.Open && each.Sentence.Contains("`#q9`", StringComparison.Ordinal));
    }

    [Fact]
    public async Task One_repository_a_reason_and_no_other_part_are_what_a_room_asks_and_every_refusal_is_said_at_once()
    {
        var one = await PlanAsync(new PlacePlanTests.World(), Room() with { Repositories = ["engine", "docs"] });
        var none = await PlanAsync(new PlacePlanTests.World(), Room(purpose: null));
        var blank = await PlanAsync(new PlacePlanTests.World(), Room(purpose: "  "));
        var long_ = await PlanAsync(new PlacePlanTests.World(), Room(purpose: new string('x', 1001)));
        var stray = await PlanAsync(new PlacePlanTests.World(), new PlaceAsk(["engine"], SetupPresets.Merge) { Purpose = Why });
        var all = await PlanAsync(new PlacePlanTests.World(), Room(path: "../x", purpose: null) with { Repositories = ["engine", "ghost"] });

        Assert.Contains(one.Refusals, each => each.Code == PlaceRoomRefusals.One);
        Assert.Contains(none.Refusals, each => each.Code == PlaceRoomRefusals.Purpose);
        Assert.Contains(blank.Refusals, each => each.Code == PlaceRoomRefusals.Purpose);
        Assert.Contains(long_.Refusals, each => each.Code == PlaceRoomRefusals.Purpose);
        Assert.Contains(stray.Refusals, each => each.Code == PlaceRoomRefusals.Purpose && each.Sentence.Contains("--room", StringComparison.Ordinal));
        Assert.Equal(
            [PlaceRoomRefusals.One, PlaceRoomRefusals.Purpose, PlaceRoomRefusals.Path],
            all.Refusals.Select(each => each.Code).Distinct().Take(3));
    }

    /// <summary>A room is a quest to an adopted repository; the other parts change configuration, so a room never rides with them.</summary>
    [Theory]
    [InlineData("preset")]
    [InlineData("adoption")]
    [InlineData("drive")]
    [InlineData("opinion")]
    [InlineData("look")]
    [InlineData("builds")]
    [InlineData("pacing")]
    public async Task A_room_asked_with_any_other_part_is_refused_room_alone_and_writes_nothing(string part)
    {
        var ask = part switch
        {
            "preset" => Room() with { Preset = SetupPresets.Keep },
            "adoption" => Room() with { Adoption = true },
            "drive" => Room() with { Drive = true },
            "opinion" => Room() with { Opinion = true },
            "look" => Room() with { Look = true },
            "builds" => Room() with { Builds = SetupBuilds.Library },
            _ => Room() with { AtOnce = 1 },
        };

        var plan = await PlanAsync(new PlacePlanTests.World(), ask);

        Assert.False(plan.Applies);
        var refusal = Assert.Single(plan.Refusals, each => each.Code == PlaceRoomRefusals.Alone);
        Assert.Equal(PlacePlans.Room, refusal.Part);
        Assert.Contains("room", refusal.Sentence);
        Assert.Empty(plan.Changes);
        Assert.Empty(plan.Saves);
    }

    // ---- the terminal's twin ----

    [Fact]
    public void The_words_of_a_room_are_its_repository_its_path_and_its_reason_with_no_delivery_asked_for()
    {
        var (words, problem) = PlaceCommand.Parse(["engine", "--room", "tools/web", "--purpose", Why, "--plan"]);

        Assert.Null(problem);
        Assert.False(words!.Apply);
        Assert.Equal(["engine"], words.Ask.Repositories);
        Assert.Equal(("", "tools/web", Why), (words.Ask.Preset, words.Ask.Room, words.Ask.Purpose));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_room_ask_round_trips_through_the_words_the_route_hands_the_verb(bool apply)
    {
        var ask = Room();

        var args = apply ? PlaceCommand.ApplyArgsOf(ask, []) : PlaceCommand.ArgsOf(ask);
        var (words, problem) = PlaceCommand.Parse(args);

        Assert.Null(problem);
        Assert.Equal(apply, words!.Apply);
        Assert.Equal(ask, words.Ask with { Repositories = [.. words.Ask.Repositories] }, new AskComparer());
        Assert.DoesNotContain("--preset", args);
        Assert.Contains("--room", args);
        Assert.Contains(Why, args);
    }

    [Fact]
    public void A_room_ask_with_other_parts_keeps_them_in_the_words_so_the_plan_can_refuse_them()
    {
        var ask = Room() with { Adoption = true, Drive = true };

        var (words, problem) = PlaceCommand.Parse(PlaceCommand.ArgsOf(ask));

        Assert.Null(problem);
        Assert.True(words!.Ask.Adoption && words.Ask.Drive);
        Assert.Equal("tools/web", words.Ask.Room);
    }

    [Theory]
    [InlineData("engine --room")]
    [InlineData("engine --room tools/web --purpose")]
    [InlineData("--room tools/web --plan")]
    [InlineData("engine --room tools/web --plan --apply")]
    [InlineData("engine --room tools/web")]
    public async Task Words_a_room_does_not_take_are_the_usage_exit_2_before_anything_is_planned(string words)
    {
        var world = new PlacePlanTests.World();
        var output = new StringWriter();

        var code = await PlaceCommand.RunAsync(words.Split(' '), output, world, Config, _home, Plugins, true, instance: null);

        Assert.Equal(2, code);
        Assert.Contains(PlaceCommand.Usage, output.ToString(), StringComparison.Ordinal);
        Assert.Empty(world.Asked);
    }

    [Fact]
    public async Task The_plan_in_the_terminal_says_the_room_the_reason_the_one_change_and_each_refusal()
    {
        var world = new PlacePlanTests.World();
        var output = new StringWriter();

        var code = await PlaceCommand.RunAsync(["engine", "--room", "tools/web", "--purpose", Why, "--plan"], output, world, Config, _home, Plugins, true, instance: null);

        var lines = output.ToString().ReplaceLineEndings("\n").TrimEnd('\n').Split('\n');
        Assert.Equal(0, code);
        Assert.Equal($"place: `engine` — a room at `tools/web`: {Why}", lines[0]);
        Assert.Contains("changes, each said once:", lines);
        Assert.Contains(lines, line => line.Contains("`engine`: a room at `tools/web` asked of its repository's own session, no room → asked", StringComparison.Ordinal));
        Assert.Equal("--plan: nothing was written, and nothing was asked.", lines[^1]);

        var refused = new StringWriter();
        var exit = await PlaceCommand.RunAsync(["ghost", "--room", "../x", "--purpose", Why, "--plan"], refused, world, Config, _home, Plugins, true, instance: null);
        Assert.Equal(1, exit);
        Assert.Contains("refused:", refused.ToString());
        Assert.Contains("is not on this machine's registry", refused.ToString());
    }

    private sealed class AskComparer : IEqualityComparer<PlaceAsk>
    {
        public bool Equals(PlaceAsk? x, PlaceAsk? y) =>
            x is not null && y is not null && x.Repositories.SequenceEqual(y.Repositories) && x.Preset == y.Preset && x.Room == y.Room && x.Purpose == y.Purpose
            && x.Adoption == y.Adoption && x.Drive == y.Drive;

        public int GetHashCode(PlaceAsk ask) => 0;
    }

    // ---- the quest's words: the repository's, in the canon's ----

    private static RoomBriefInput Input(string path = "tools/web", string purpose = Why) =>
        new("engine", path, purpose, "main", "0123456789abcdef0123456789abcdef01234567", ["docs", "src/api"], ["web", "api"], ["docs", "src/api"]);

    [Fact]
    public void The_quest_names_the_repository_the_path_the_reason_the_convention_and_the_repositorys_own_declaration()
    {
        var (title, body) = RoomBrief.Compose(Input());

        Assert.Equal("Add a room for tools/web", title);
        Assert.Contains("`tools/web`", body);
        Assert.Contains($"> {Why}", body);
        Assert.Contains("`rooms-and-classes`", body);
        Assert.Contains("`main` at commit `0123456789ab`", body);
        Assert.Contains("`daoris.json`'s `rooms`: `docs`, `src/api`.", body);
        Assert.Contains("`daoris.lanes.json`'s lanes name these rooms: `web`, `api`.", body);
        Assert.Contains("Folders with an `AGENTS.md` of their own: `docs`, `src/api`.", body);
        Assert.Contains("Daoris never writes a room's text", body);
        Assert.Contains("on this branch", body);
        Assert.Contains("landed by the workspace's rule", body);
        foreach (var verb in RoomBrief.Verbs) Assert.Contains($"`{verb}`", body);
    }

    [Fact]
    public void A_repository_declaring_none_is_said_to_declare_none()
    {
        var (_, body) = RoomBrief.Compose(Input() with { Declared = [], Lanes = [], Folders = [] });

        Assert.Contains("`daoris.json`'s `rooms`: none.", body);
        Assert.Contains("`daoris.lanes.json`'s lanes name no room.", body);
        Assert.Contains("Folders with an `AGENTS.md` of their own: none.", body);
    }

    /// <summary>The convention is the canon's: each line the quest quotes of it is in the canon's own text, so a change there fails here until the quest says it too.</summary>
    [Fact]
    public void The_lines_the_quest_quotes_of_the_convention_are_in_the_canons_text()
    {
        var canon = Flat(File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "canon", "core", "knowledge", "rooms-and-classes.md")));
        var (_, body) = RoomBrief.Compose(Input());

        Assert.NotEmpty(RoomBrief.Quoted);
        foreach (var line in RoomBrief.Quoted)
        {
            Assert.Contains(line, canon);
            Assert.Contains($"> {line}", Flat(body));
        }
    }

    [Fact]
    public void The_quest_asks_only_for_verbs_its_press_allows_and_the_rule_is_those_verbs_exactly()
    {
        var (_, body) = RoomBrief.Compose(Input());
        var asked = body[..body.IndexOf("**What this writes", StringComparison.Ordinal)];

        foreach (var verb in Regex.Matches(asked, "`(daoris [^`]+)`").Select(match => match.Groups[1].Value).Distinct())
        {
            Assert.True(SetupPressRules.RoomVerbs.Contains(verb), $"the body asks for `{verb}`, which the room's press does not allow.");
        }

        Assert.Equal(["daoris --version", "daoris sync", "daoris check"], SetupPressRules.RoomVerbs);
        Assert.Equal(SetupPressRules.RoomVerbs.Select(verb => $"Bash({verb})"), SetupPressRules.RoomRules);
        Assert.Equal(SetupPressRules.RoomRules, SetupPressRules.RulesOf(new SetupPlan("engine") { Case = SetupCase.Room }));
        Assert.All(SetupPressRules.RoomRules, rule => Assert.Null(PermissionRules.Refusal(rule)));
    }

    /// <summary>🔴 Read in a repository that may know nothing of Daoris: no decision number, no path of Daoris's own, no machine path.</summary>
    [Fact]
    public void The_quest_is_in_the_canons_words()
    {
        var (title, body) = RoomBrief.Compose(Input());
        var text = title + "\n" + body;

        Assert.DoesNotMatch(DecisionNumber(), text);
        Assert.DoesNotMatch(@"\b(LAYOUT|WSSETUP|TOOLS|UNBLOCK|DOC|GROWDEPT)\d", text);
        foreach (var path in new[] { "docs/2", "src/Daoris", "adoption.md", "DAORIS_HOME", "daoris-driver", @"C:\", "/Users/", "grow-department" })
        {
            Assert.DoesNotContain(path, text);
        }
    }

    [Fact]
    public void The_steps_run_tool_checks_text_declaration_verify_commit_and_the_close_says_what_to_say()
    {
        var (_, body) = RoomBrief.Compose(Input());

        string[] order = ["**The tool.**", "**The checks first.**", "**Write the room's own `AGENTS.md`", "**Declare it", "**Verify.**", "**Commit**", "**How to close it.**"];
        var at = order.Select(words => body.IndexOf(words, StringComparison.Ordinal)).ToList();

        Assert.DoesNotContain(-1, at);
        Assert.Equal(at.Order(), at);
        Assert.Contains("`tools/web/AGENTS.md`", body);
        Assert.Contains("Never push, merge or open a pull request", body);
        Assert.Contains("never publish a request to another repository", body);
    }

    private static string Flat(string text) =>
        string.Join(" ", text.ReplaceLineEndings("\n").Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0));

    [GeneratedRegex(@"\bD\d{1,3}\b")]
    private static partial Regex DecisionNumber();
}

using System.Text.Json;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A department opened as its press's card shows it (<c>REGISTRY_CREATE</c>): the plan the route's own code reads
/// (<c>REGISTRY_CREATE_PLAN</c>, the press's own ground) said as what the press means and the command it is, above every value (name,
/// workspace, kind, the full folder, the line, the identity, that no remote is made), each step in order and the first lines of each quest it
/// publishes. A plan its own rules refuse is refused at the ask in the plan's words, with no card.
/// </summary>
public sealed class RegistryCreateViewTests
{
    private static readonly object[] NoRefusals = [];

    private static StubGrounds Planned(object plan, List<JsonElement>? asked = null)
    {
        var grounds = new StubGrounds();
        grounds.Reads["REGISTRY_CREATE_PLAN"] = payload =>
        {
            asked?.Add(payload!.Value);
            return plan;
        };
        return grounds;
    }

    private static Task<PressViewed> View(StubGrounds grounds, object payload) =>
        new RegistryCreateView().ViewAsync(grounds, JsonSerializer.SerializeToElement(payload), CancellationToken.None);

    private static object Git(object[]? refusals = null, string? remoteStep = null, string? resumes = null, object[]? quests = null) => new
    {
        asked = new { name = "ledger", kind = "git", workspace = "aurora" },
        workspace = "aurora",
        folder = "/work/aurora/ledger",
        line = "main",
        author = new { name = "Fixture", email = "fixture@example.test" },
        remoteStep,
        resumes,
        steps = new object[]
        {
            new { id = "folder", said = "make the folder /work/aurora/ledger" },
            new { id = "history", said = "git init" },
            new { id = "registration", said = "register" },
            new { id = "driving", said = "drive" },
            new { id = "setup", said = "ask" },
        },
        refusals = refusals ?? NoRefusals,
        applies = (refusals ?? NoRefusals).Length == 0,
        digest = new string('a', 64),
        quests = quests ?? [new { role = "setup", title = "Set up this repository for every agent (2026-11-12)", head = "**What is asked, and whose it is.** Take up the shared doctrine" }],
    };

    private static readonly object Press = new { name = "ledger", purpose = "keeps the books", workspace = "aurora", said = true };

    /// <summary>The card names every value the press carries, the steps in order and the quests it publishes, and the command it is.</summary>
    [Fact]
    public async Task A_departments_card_names_every_value_its_steps_in_order_and_the_quests_it_publishes()
    {
        var asked = new List<JsonElement>();

        var viewed = await View(Planned(Git(), asked), Press);

        var sent = Assert.Single(asked);
        Assert.Equal(("ledger", "keeps the books", "aurora"), (sent.GetProperty("name").GetString(), sent.GetProperty("purpose").GetString(), sent.GetProperty("workspace").GetString()));
        var view = viewed.View!;
        Assert.Equal("repository-create", view.Kind);
        Assert.Equal(
            ("ledger", "aurora", "/work/aurora/ledger", "main", "Fixture <fixture@example.test>", "keeps the books"),
            (view.Facts["name"], view.Facts["workspace"], view.Facts["folder"], view.Facts["line"], view.Facts["author"], view.Facts["purpose"]));
        Assert.Equal(
            [
                "repository-create-no-remote",
                "repository-create-step-folder", "repository-create-step-history", "repository-create-step-registration", "repository-create-step-driving",
                "repository-create-step-setup", "repository-create-quest-setup",
            ],
            view.Also!.Select(said => said.Kind));
        Assert.Equal(["1", "2", "3", "4", "5"], view.Also!.Where(said => said.Kind.StartsWith("repository-create-step-", StringComparison.Ordinal)).Select(said => said.Facts["n"]));
        var quest = view.Also!.Single(said => said.Kind == "repository-create-quest-setup").Facts;
        Assert.Equal(("Set up this repository for every agent (2026-11-12)", "**What is asked, and whose it is.** Take up the shared doctrine"), (quest["title"], quest["head"]));
        Assert.Equal("daoris repository create ledger --workspace aurora --purpose \"keeps the books\"", view.Command);
    }

    /// <summary>The work follows the set-up as a second quest, and the command carries it.</summary>
    [Fact]
    public async Task The_work_a_department_is_opened_for_is_a_second_quest_and_part_of_the_command()
    {
        var plan = Git(quests:
        [
            new { role = "setup", title = "Set up", head = "head one" },
            new { role = "work", title = "Build the first screen.", head = "Build the first screen." },
        ]);

        var viewed = await View(Planned(plan), new { name = "ledger", purpose = "keeps the books", work = "Build the first screen.", said = true });

        Assert.Equal(["repository-create-quest-setup", "repository-create-quest-work"], viewed.View!.Also!.TakeLast(2).Select(said => said.Kind));
        Assert.Equal("Build the first screen.", viewed.View.Also![^1].Facts["head"]);
        Assert.Equal("daoris repository create ledger --purpose \"keeps the books\" --work \"Build the first screen.\"", viewed.View.Command);
    }

    /// <summary>A step the inherited workflow cannot take without a remote leaves the drive off, and the card says which.</summary>
    [Fact]
    public async Task A_drive_left_off_for_a_missing_remote_names_the_step_and_a_resumed_creation_says_so()
    {
        var viewed = await View(Planned(Git(remoteStep: "push", resumes: new string('b', 64))), Press);

        var also = viewed.View!.Also!;
        Assert.Contains(also, said => said.Kind == "repository-create-step-driving-off" && said.Facts["step"] == "push" && said.Facts["name"] == "ledger");
        Assert.DoesNotContain(also, said => said.Kind == "repository-create-step-driving");
        Assert.Contains(also, said => said.Kind == "repository-create-resumes" && said.Facts["plan"] == new string('b', 12));
    }

    /// <summary>A folder department has no history, no remote line and no quest: its card is its own sentence.</summary>
    [Fact]
    public async Task A_folder_department_has_a_card_of_its_own_with_no_history_and_no_quest()
    {
        var plan = new
        {
            asked = new { name = "notes", kind = "folder", workspace = "aurora" },
            workspace = "aurora",
            folder = "/work/aurora/notes",
            line = (string?)null,
            author = (object?)null,
            steps = new object[]
            {
                new { id = "folder", said = "make" }, new { id = "registration", said = "register" }, new { id = "driving", said = "drive" },
            },
            refusals = NoRefusals,
            applies = true,
            digest = new string('c', 64),
            quests = NoRefusals,
        };

        var viewed = await View(Planned(plan), new { name = "notes", purpose = "loose files", kind = "folder", said = true });

        var view = viewed.View!;
        Assert.Equal("repository-create-folder", view.Kind);
        Assert.False(view.Facts.ContainsKey("line"));
        Assert.Equal(
            ["repository-create-step-folder", "repository-create-step-registration-folder", "repository-create-step-driving"], view.Also!.Select(said => said.Kind));
        Assert.Equal("daoris repository create notes --kind folder --purpose \"loose files\"", view.Command);
    }

    /// <summary>The route's own refusals reach the person at the ask in the plan's words, with no card: the view adds none of its own.</summary>
    [Fact]
    public async Task A_plan_its_rules_refuse_is_refused_at_the_ask_in_the_plans_words_with_no_card()
    {
        var plan = Git([
            new { code = "name-registered", sentence = "`ledger` is registered already." },
            new { code = "no-identity", sentence = "no git identity is chosen." },
        ]);

        var viewed = await View(Planned(plan), Press);

        Assert.Null(viewed.View);
        Assert.Equal("`ledger` is registered already. no git identity is chosen.", viewed.Refusal);
    }

    [Fact]
    public void It_is_the_view_of_the_press()
    {
        Assert.IsType<RegistryCreateView>(PressViews.Find(DriverModule.Name, "REGISTRY_CREATE"));
    }
}

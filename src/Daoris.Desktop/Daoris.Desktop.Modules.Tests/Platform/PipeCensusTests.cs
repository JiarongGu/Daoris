using System.Runtime.CompilerServices;
using System.Text.Json;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The pipe's census: every route the pipe accepts is classed <c>open</c>, <c>person</c>, <c>window</c> or <c>role</c> by its mark,
/// so the same account opening the pipe gains no person's authority. An unclassed press is the person's and an unclassed module is
/// refused.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class PipeCensusTests
{
    /// <summary>
    /// Every route is marked: the census is read off the marks at its first use, and refuses a person's or a role's press with no
    /// drain, a person's or a window's route with no owner mark, a person's press resting on nothing and not timeless, a setting
    /// naming no value it sets with no reason it is never already, and a mark naming a route its member does not answer.
    /// </summary>
    [Fact]
    public void Every_route_is_marked()
    {
        var census = PipeCensus.Daoris;

        Assert.Equal(
            DriverModule.Routes.Order(StringComparer.Ordinal),
            census.Routes.Where(each => each.Module == DriverModule.Name).Select(each => each.Route!).Order(StringComparer.Ordinal));
        Assert.All(census.Routes.Where(each => each.Class is PipeClass.Person or PipeClass.Role && each.Route is not null),
            each => Assert.NotNull(census.DrainOf(each.Module, each.Route)));
        Assert.All(census.Routes.Where(each => each.Class is PipeClass.Person or PipeClass.Window || each.When?.Class is PipeClass.Person or PipeClass.Window),
            each => Assert.NotNull(census.Targets.Mark(each.Module, each.Route)));
        Assert.All(census.Routes.Where(each => each.Route is not null && (each.Class == PipeClass.Person || each.When?.Class == PipeClass.Person)),
            each => Assert.NotNull(census.Targets.Row(each.Module, each.Route!)));
        // A route a route-by-route module leaves to the person by default has no row, and is refused `ungrounded` (PipeConfirmTests).
        Assert.Null(census.Targets.Row(DriverModule.Name, "A_ROUTE_NOBODY_CLASSED"));
    }

    /// <summary>
    /// The driver's twin of the census's classes (<c>RouteClass</c>) names each as the census does, so a verb reading its
    /// route's class before it looks for the instance reads the census's word.
    /// </summary>
    [Fact]
    public void The_driver_names_the_classes_as_the_census_does() =>
        Assert.Equal(Enum.GetNames<PipeClass>(), Enum.GetNames<Daoris.Driver.RouteClass>());

    /// <summary>
    /// Each press of the <c>trees</c> verbs names its route and the class the census gives it:
    /// a verb whose route the census opens becomes the instance's client, so a route opened here fails until its verb's client
    /// path is built, its words from the route's answer held to the alone path's, and the table says so.
    /// </summary>
    [Fact]
    public void Each_trees_press_names_its_route_as_the_census_classes_it()
    {
        Assert.Equal(DriverModule.Name, Daoris.Driver.TreesRoutes.DriverModule);
        foreach (var (verb, route) in Daoris.Driver.TreesRoutes.Presses)
        {
            Assert.Contains(route.Route, DriverModule.Routes);
            Assert.True(
                PipeCensus.Daoris.Of(route.Module, route.Route)?.ToString() == route.Class.ToString(),
                $"`{verb}` names {route.Route} {route.Class}, and the census classes it {PipeCensus.Daoris.Of(route.Module, route.Route)}.");
        }
    }

    /// <summary>
    /// The supervisor's verb sends its answer as the role's act the census classes it, and reads what waits
    /// as an open read; the appointment is the window's alone, never a verb's. Its defer is the role's second act, and the
    /// role's acts are the only <c>Role</c> routes.
    /// </summary>
    [Fact]
    public void The_supervisors_verb_names_its_routes_as_the_census_classes_them()
    {
        var decide = Daoris.Driver.SessionsSuperviseCommand.Decide;
        var leave = Daoris.Driver.SessionsSuperviseCommand.Leave;
        var waiting = Daoris.Driver.SessionsSuperviseCommand.Waiting;
        Assert.Equal((DriverModule.Name, PipeClass.Role, Daoris.Driver.RouteClass.Role), (decide.Module, PipeCensus.Daoris.Of(decide.Module, decide.Route), decide.Class));
        Assert.Equal((DriverModule.Name, PipeClass.Role, Daoris.Driver.RouteClass.Role), (leave.Module, PipeCensus.Daoris.Of(leave.Module, leave.Route), leave.Class));
        Assert.Equal((PipeClass.Open, Daoris.Driver.RouteClass.Open), (PipeCensus.Daoris.Of(waiting.Module, waiting.Route), waiting.Class));
        Assert.Equal(PipeClass.Window, PipeCensus.Daoris.Of(DriverModule.Name, Daoris.Driver.RoleRoutes.StartTerminal));
        // The supervisor's one door is two routes beside the steward's one, and nothing else of the class.
        Assert.Equal(
            ["ROLE_ACT", "SUPERVISOR_DECIDE", "SUPERVISOR_LEAVE"],
            PipeCensus.Daoris.Routes.Where(route => route.Class == PipeClass.Role).Select(route => route.Route).Order());
        Assert.DoesNotContain(PipeCensus.Daoris.Routes, route => route.Route is "SUPERVISOR_ANSWER" or "SUPERVISOR_DEFER" or "SUPERVISOR_COURSE");
    }

    /// <summary>
    /// The terminal's door reads the pipe's words for a confirmation as the pipe says them, a twin the driver library cannot
    /// see from its side: the notification that says a press waits, and the two endings that are the person's word (exit 1).
    /// </summary>
    [Fact]
    public void The_terminal_s_door_reads_the_pipe_s_confirmation_words_as_the_pipe_says_them()
    {
        Assert.Equal(InstancePipe.Module, Daoris.Driver.InstanceClient.PipeModule);
        Assert.Equal(InstancePipe.Confirming, Daoris.Driver.InstanceDoor.Confirming);
        Assert.Equal(InstancePipe.Refused, Daoris.Driver.InstanceDoor.Refused);
        Assert.Equal(InstancePipe.Expired, Daoris.Driver.InstanceDoor.Expired);
    }

    /// <summary>
    /// The person's presses the host's verbs send over the pipe: each names a route of the driver's module that the census
    /// makes the person's, so the instance shows it in the window and runs it on the person's Confirm alone. A route the census opened
    /// would run unconfirmed, and one it made the window's would be refused: either fails here, naming the verb.
    /// </summary>
    [Fact]
    public void Each_person_press_a_verb_sends_names_a_route_the_census_makes_the_person_s()
    {
        Assert.NotEmpty(Daoris.Driver.PersonPresses.Presses);
        foreach (var (verb, route) in Daoris.Driver.PersonPresses.Presses)
        {
            Assert.Equal(DriverModule.Name, route.Module);
            Assert.Contains(route.Route, DriverModule.Routes);
            Assert.True(
                PipeCensus.Daoris.Of(route.Module, route.Route) == PipeClass.Person && route.Class == Daoris.Driver.RouteClass.Person,
                $"`{verb}` sends {route.Route} as the person's, and the census classes it {PipeCensus.Daoris.Of(route.Module, route.Route)}");
        }
    }

    /// <summary>
    /// The standing choices <c>daoris place</c> and <c>settings</c> send: each a route of the driver's module the census
    /// makes the person's, so the window confirms it, and each verb no route takes says why it acts alone.
    /// </summary>
    [Fact]
    public void Each_standing_choice_names_a_route_the_census_makes_the_person_s_and_each_alone_says_why()
    {
        Assert.NotEmpty(Daoris.Driver.ChoiceRoutes.Verbs);
        Assert.Equal(Daoris.Driver.ChoiceRoutes.Verbs.Count, Daoris.Driver.ChoiceRoutes.Verbs.Select(each => each.Verb).Distinct().Count());
        foreach (var (verb, route, reason) in Daoris.Driver.ChoiceRoutes.Verbs)
        {
            Assert.False(string.IsNullOrWhiteSpace(reason), $"`{verb}` says no reason");
            if (route is null)
            {
                Assert.StartsWith("alone:", reason, StringComparison.Ordinal);
                continue;
            }

            Assert.Equal(DriverModule.Name, route.Module);
            Assert.Contains(route.Route, DriverModule.Routes);
            Assert.True(
                PipeCensus.Daoris.Of(route.Module, route.Route) == PipeClass.Person && route.Class == Daoris.Driver.RouteClass.Person,
                $"`{verb}` sends {route.Route} as the person's, and the census classes it {PipeCensus.Daoris.Of(route.Module, route.Route)}");
        }
    }

    /// <summary>
    /// The tools' presses <c>daoris tool</c> sends: each a route of the driver's module the census classes
    /// as the verb reads it, the person's changes the person's and the look open, and each verb no route takes says why it acts alone.
    /// </summary>
    [Fact]
    public void Each_tool_press_names_a_route_the_census_classes_as_the_verb_reads_it_and_each_alone_says_why()
    {
        Assert.NotEmpty(Daoris.Driver.ToolRoutes.Verbs);
        Assert.Equal(Daoris.Driver.ToolRoutes.Verbs.Count, Daoris.Driver.ToolRoutes.Verbs.Select(each => each.Verb).Distinct().Count());
        foreach (var (verb, route, reason) in Daoris.Driver.ToolRoutes.Verbs)
        {
            Assert.False(string.IsNullOrWhiteSpace(reason), $"`{verb}` says no reason");
            if (route is null)
            {
                Assert.StartsWith("alone:", reason, StringComparison.Ordinal);
                continue;
            }

            Assert.Equal(DriverModule.Name, route.Module);
            Assert.Contains(route.Route, DriverModule.Routes);
            var census = PipeCensus.Daoris.Of(route.Module, route.Route);
            Assert.True(
                (census, route.Class) is (PipeClass.Person, Daoris.Driver.RouteClass.Person) or (PipeClass.Open, Daoris.Driver.RouteClass.Open),
                $"`{verb}` sends {route.Route} as {route.Class}, and the census classes it {census}");
        }

        Assert.Equal(Daoris.Driver.RouteClass.Open, Daoris.Driver.ToolRoutes.Look.Class);
    }

    /// <summary>The route <c>daoris integrate</c>'s acts press is a driver route the census classes as the person's.</summary>
    [Fact]
    public void The_integrate_verbs_route_is_the_persons_in_the_census()
    {
        var route = Daoris.Driver.IntegrateCommand.Route;
        Assert.Equal(DriverModule.Name, route.Module);
        Assert.Contains(route.Route, DriverModule.Routes);
        Assert.Equal(PipeClass.Person, PipeCensus.Daoris.Of(route.Module, route.Route));
        Assert.Equal(Daoris.Driver.RouteClass.Person, route.Class);
    }

    /// <summary>
    /// The agents' presses <c>daoris agent</c> sends: each a route of the driver's module the census
    /// classes as the person's, and each verb no route takes says why it acts alone.
    /// </summary>
    [Fact]
    public void Each_agent_press_names_a_route_the_census_classes_as_the_person_s_and_each_alone_says_why()
    {
        Assert.NotEmpty(Daoris.Driver.AgentRoutes.Verbs);
        Assert.Equal(Daoris.Driver.AgentRoutes.Verbs.Count, Daoris.Driver.AgentRoutes.Verbs.Select(each => each.Verb).Distinct().Count());
        foreach (var (verb, route, reason, _) in Daoris.Driver.AgentRoutes.Verbs)
        {
            Assert.False(string.IsNullOrWhiteSpace(reason), $"`{verb}` says no reason");
            if (route is null)
            {
                Assert.StartsWith("alone:", reason, StringComparison.Ordinal);
                continue;
            }

            Assert.Equal(DriverModule.Name, route.Module);
            Assert.Contains(route.Route, DriverModule.Routes);
            Assert.True(
                PipeCensus.Daoris.Of(route.Module, route.Route) == PipeClass.Person && route.Class == Daoris.Driver.RouteClass.Person,
                $"`{verb}` sends {route.Route} as the person's, and the census classes it {PipeCensus.Daoris.Of(route.Module, route.Route)}");
        }
    }

    /// <summary>
    /// The plugins' presses <c>daoris plugin</c> sends: each a route of the driver's module the census
    /// classes as the person's in the form the command sends it (<c>PLUGIN_UPDATE</c> with <c>apply: true</c>), and the replace no route
    /// takes says why it acts alone.
    /// </summary>
    [Fact]
    public void Each_plugin_press_names_a_route_the_census_classes_as_the_person_s_and_the_replace_says_why_it_is_alone()
    {
        Assert.NotEmpty(Daoris.Driver.PluginRoutes.Verbs);
        Assert.Equal(Daoris.Driver.PluginRoutes.Verbs.Count, Daoris.Driver.PluginRoutes.Verbs.Select(each => each.Verb).Distinct().Count());
        using var sent = System.Text.Json.JsonDocument.Parse("""{ "apply": true }""");
        foreach (var (verb, route, reason) in Daoris.Driver.PluginRoutes.Verbs)
        {
            Assert.False(string.IsNullOrWhiteSpace(reason), $"`{verb}` says no reason");
            if (route is null)
            {
                Assert.StartsWith("alone:", reason, StringComparison.Ordinal);
                continue;
            }

            Assert.Equal(DriverModule.Name, route.Module);
            Assert.Contains(route.Route, DriverModule.Routes);
            var census = PipeCensus.Daoris.Of(route.Module, route.Route, sent.RootElement);
            Assert.True(census == PipeClass.Person && route.Class == Daoris.Driver.RouteClass.Person,
                $"`{verb}` sends {route.Route} as the person's, and the census classes it {census}");
        }

        Assert.Single(Daoris.Driver.PluginRoutes.Verbs, each => each.Route is null);
    }

    /// <summary>
    /// The open reads and presses the host's verbs send: each names a route of the driver's module that the census
    /// opens, and each verb of the scope left off the pipe says why. A route the census closes fails here, naming the verb that
    /// would send a press the instance refuses.
    /// </summary>
    [Fact]
    public void Each_open_verb_names_an_open_route_and_each_verb_left_off_the_pipe_says_why()
    {
        Assert.NotEmpty(Daoris.Driver.OpenRoutes.Verbs);
        Assert.Equal(Daoris.Driver.OpenRoutes.Verbs.Count, Daoris.Driver.OpenRoutes.Verbs.Select(each => each.Verb).Distinct().Count());
        foreach (var (verb, route, reason) in Daoris.Driver.OpenRoutes.Verbs)
        {
            Assert.False(string.IsNullOrWhiteSpace(reason), $"`{verb}` says no reason");
            if (route is null) continue;
            Assert.Equal(DriverModule.Name, route.Module);
            Assert.Contains(route.Route, DriverModule.Routes);
            Assert.True(
                PipeCensus.Daoris.Of(route.Module, route.Route)?.ToString() == nameof(Daoris.Driver.RouteClass.Open) && route.Class == Daoris.Driver.RouteClass.Open,
                $"`{verb}` sends {route.Route}, which the census classes {PipeCensus.Daoris.Of(route.Module, route.Route)}");
        }

        // The verbs left off are exactly those with no route, and each is a read: no press of the person's waits here.
        Assert.All(
            Daoris.Driver.OpenRoutes.Verbs.Where(each => each.Route is null),
            each => Assert.StartsWith("alone:", each.Reason, StringComparison.Ordinal));
    }

    /// <summary>
    /// Of the host's verbs with a window twin, only these stay off the pipe, each with
    /// its reason. By design, a loop (<c>drive</c>), the updater's request (<c>update</c>) and the service's own doors; and, until a decision lets this machine's paths travel over the
    /// local pipe, the reads whose words name this machine's folders or quote its trees, with the few no route answers. A verb
    /// that becomes a client leaves this list in the change that sends its route, and one that joins it says why here.
    /// </summary>
    [Fact]
    public void Only_the_verbs_that_stay_alone_are_off_the_pipe_each_with_its_reason()
    {
        string[] off =
        [
            "drive", "update",
            "quest delete|accept|done|reopen|go-on|check|review", "ask \"…\"", "sync status", "ask channel <id>",
            "trace", "git branches", "workflow", "logs",
            // The bench's verb times the git its own process starts.
            "git review <session>",
            "plugins show <id>", "plugins activity <id>", "plugins try <folder|id>", "plugins install <file>", "plugins new <id>",
            "help delete <id>", "place --workspace <name> --adoption",
            // A session's pieces, read off the box under the home, which says the same with or without the instance.
            "sessions partner <id> --list",
        ];

        var left = Daoris.Driver.OpenRoutes.Verbs.Where(each => each.Route is null).ToList();

        Assert.Equal(off.Order(StringComparer.Ordinal), left.Select(each => each.Verb).Order(StringComparer.Ordinal));
        Assert.All(left, each => Assert.True(each.Reason.Length > "alone: ".Length, $"`{each.Verb}` says no reason"));
        // Every family the row names sends at least one route: work, history, help, opinion, setup, register, sync, plugins
        // and the channel.
        var sent = Daoris.Driver.OpenRoutes.Verbs.Where(each => each.Route is not null).Select(each => each.Verb)
            .Concat(Daoris.Driver.PersonPresses.Presses.Select(each => each.Verb)).ToList();
        foreach (var family in new[] { "quest pause", "history", "help", "opinion", "place", "register", "sync", "plugins", "ask channel" })
        {
            Assert.Contains(sent, verb => verb.StartsWith(family, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// Every module this library answers for is named by the census, whole or route by route, so none is refused only because
    /// nobody looked at it; the kit's own modules are not Daoris's to class, and stay refused.
    /// </summary>
    [Fact]
    public void Every_module_of_the_desktop_is_named()
    {
        var modules = typeof(DriverModule).Assembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsClass: true } && typeof(IIpcModule).IsAssignableFrom(type))
            .Select(type => ((IIpcModule)RuntimeHelpers.GetUninitializedObject(type)).ModuleName)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Contains(DriverModule.Name, modules);
        Assert.All(modules, module => Assert.True(PipeCensus.Daoris.Names(module), $"{module} is no module of the census"));
        Assert.False(PipeCensus.Daoris.Names("SHENORA.REQUESTS"));
    }

    [Theory]
    // Reads, and what is left to the driver: run as asked.
    [InlineData("DAORIS.DRIVER", "SESSION_GROUPS", PipeClass.Open)]
    // What came to the person, read by a terminal, a press by its act alone; the presses' own read stays the window's.
    [InlineData("DAORIS.DRIVER", "NOTIFICATIONS", PipeClass.Open)]
    [InlineData("DAORIS.DRIVER", "UNDER_WAY", PipeClass.Open)]
    [InlineData("DAORIS.DRIVER", "SYNC_NOW", PipeClass.Open)]
    [InlineData("DAORIS.DRIVER", "ACCOUNTS_READ", PipeClass.Open)]
    // Changed after a second agent's review: a trees sync fast-forwards lines and deletes branches, a second opinion runs on an
    // account, and Read again lets a refused account through and ends a cool-off; each is the person's.
    [InlineData("DAORIS.DRIVER", "TREES_SYNC", PipeClass.Person)]
    [InlineData("DAORIS.DRIVER", "ASK_OPINION", PipeClass.Person)]
    [InlineData("DAORIS.DRIVER", "HARNESSES_REFRESH", PipeClass.Person)]
    // The person's alone: a yes, a delete, a clear, their words, a standing choice.
    [InlineData("DAORIS.DRIVER", "LAND_SESSION_TREE", PipeClass.Person)]
    [InlineData("DAORIS.DRIVER", "DISCARD_SESSION_TREE", PipeClass.Person)]
    // A ref made in a repository and its undo write the person's repository through git.
    [InlineData("DAORIS.DRIVER", "GIT_REF_CREATE", PipeClass.Person)]
    [InlineData("DAORIS.DRIVER", "GIT_REF_UNDO", PipeClass.Person)]
    [InlineData("DAORIS.DRIVER", "GIT_REF_DELETE", PipeClass.Person)]
    [InlineData("DAORIS.DRIVER", "GIT_REF_RENAME", PipeClass.Person)]
    [InlineData("DAORIS.DRIVER", "GIT_REF_FINISH", PipeClass.Person)]
    [InlineData("DAORIS.DRIVER", "GIT_FETCH", PipeClass.Person)]
    [InlineData("DAORIS.DRIVER", "GIT_FETCH_PLAN", PipeClass.Open)]
    // One ref pushed to origin moves origin under the person's grant; its plan is a read.
    [InlineData("DAORIS.DRIVER", "GIT_PUSH", PipeClass.Person)]
    [InlineData("DAORIS.DRIVER", "GIT_PUSH_PLAN", PipeClass.Open)]
    // A stash of Daoris's own Save dropped removes an entry of the shared stash.
    [InlineData("DAORIS.DRIVER", "GIT_STASH_DROP", PipeClass.Person)]
    // A branch's history edited moves the person's branch; its plan form is open (a rule on `plan`).
    [InlineData("DAORIS.DRIVER", "GIT_HISTORY_EDIT", PipeClass.Person)]
    [InlineData("DAORIS.DRIVER", "SWEEP", PipeClass.Person)]
    [InlineData("DAORIS.DRIVER", "RECOVERY_DROP", PipeClass.Person)]
    [InlineData("DAORIS.DRIVER", "WORK_HELD", PipeClass.Person)]
    [InlineData("DAORIS.DRIVER", "HISTORY_CLEAR", PipeClass.Person)]
    [InlineData("DAORIS.DRIVER", "SESSION_INPUT", PipeClass.Person)]
    [InlineData("DAORIS.DRIVER", "OPINION_MYSELF", PipeClass.Person)]
    [InlineData("DAORIS.DRIVER", "SET_REVIEW", PipeClass.Person)]
    // The commands this machine runs for a repository's heavy gates are the person's to write.
    [InlineData("DAORIS.DRIVER", "SET_HEAVY_GATES", PipeClass.Person)]
    [InlineData("DAORIS.DRIVER", "SET_KEEP", PipeClass.Person)]
    // Which checks a repository's sessions run is the person's standing choice, and light lowers their part.
    [InlineData("DAORIS.DRIVER", "SET_SESSION_CHECKS", PipeClass.Person)]
    // What only a window does: the pickers, the file manager, Daoris's browser, its frame, its shells.
    [InlineData("DAORIS.DRIVER", "TOOLS_PICK", PipeClass.Window)]
    [InlineData("DAORIS.DRIVER", "SESSION_OPEN_FOLDER", PipeClass.Window)]
    [InlineData("DAORIS.DRIVER", "SYSTEM_OPEN_FOLDER", PipeClass.Window)]
    [InlineData("DAORIS.DRIVER", "SYSTEM_PATH", PipeClass.Window)]
    [InlineData("DAORIS.DRIVER", "SYSTEM_SERVICE_ADDRESS", PipeClass.Window)]
    [InlineData("DAORIS.DRIVER", "WORK_RECORDS_FOLDER", PipeClass.Window)]
    // A terminal never reads or answers what waits for the window, and never reads the person key.
    [InlineData("DAORIS.DRIVER", "PERSON_CONFIRMATIONS", PipeClass.Window)]
    [InlineData("DAORIS.DRIVER", "PERSON_CONFIRM", PipeClass.Window)]
    [InlineData("DAORIS.DRIVER", "PERSON_CONFIRM_SHOWN", PipeClass.Window)]
    // The window is never a holder; a terminal lets go of its own hold by the pipe's LET_GO.
    [InlineData("DAORIS.DRIVER", "SESSION_HOLDS", PipeClass.Window)]
    [InlineData("DAORIS.DRIVER", "SESSION_LET_GO", PipeClass.Window)]
    [InlineData("DAORIS.DRIVER", "FILES", PipeClass.Window)]
    [InlineData("DAORIS.DRIVER", "FILES_READ", PipeClass.Window)]
    [InlineData("DAORIS.DRIVER", "FILES_SET", PipeClass.Window)]
    [InlineData("DAORIS.DRIVER", "FILES_REMOVE", PipeClass.Window)]
    [InlineData("DAORIS.DRIVER", "FILES_SETTLE", PipeClass.Window)]
    [InlineData("DAORIS.DRIVER", "FILES_PERMIT", PipeClass.Window)]
    [InlineData("DAORIS.DRIVER", "PERSON_KEY", PipeClass.Window)]
    // Nor what waits at the service's door as the window is told it; the service's own list stays its own door's.
    [InlineData("DAORIS.DRIVER", "SERVICE_CONFIRMATIONS", PipeClass.Window)]
    [InlineData("DAORIS.WINDOWS", "OPEN", PipeClass.Window)]
    [InlineData("DAORIS.TERMINAL", "INPUT", PipeClass.Window)]
    [InlineData("DAORIS.BROWSER", "SET_LINKS", PipeClass.Window)]
    [InlineData("DAORIS.REGISTRY", "PICK_FOLDER", PipeClass.Window)]
    [InlineData("DAORIS.REMOTES", "STATE", PipeClass.Open)]
    [InlineData("DAORIS.REMOTES", "SET", PipeClass.Person)]
    // A route a route-by-route module does not name is a press, and so the person's.
    [InlineData("DAORIS.REMOTES", "SET_EVERYTHING", PipeClass.Person)]
    [InlineData("DAORIS.DRIVER", "LAND_EVERYTHING", PipeClass.Person)]
    // Modules are matched as the kit's dispatcher matches them, ignoring case; routes as their table does.
    [InlineData("daoris.driver", "STATE", PipeClass.Open)]
    [InlineData("DAORIS.DRIVER", "state", PipeClass.Person)]
    public void A_route_is_classed_as_D156_classes_its_door(string module, string route, PipeClass expected) =>
        Assert.Equal(expected, PipeCensus.Daoris.Of(module, route));

    /// <summary>
    /// One route that is a look in one form and a press in another is classed by its payload: a comparison of gits Daoris already
    /// knows is a look, and of a file named by its path runs that file; an update's preview is a look, and its <c>apply</c> the
    /// update. Every spelling of the field counts, and the stricter class wins, so no casing or repeat of a name opens a press;
    /// a payload naming no form the rule opens is the stricter.
    /// </summary>
    [Theory]
    [InlineData("TOOLS_GIT", """{"way":"system"}""", PipeClass.Open)]
    [InlineData("TOOLS_GIT", """{"way":"managed","version":"2.47.1"}""", PipeClass.Open)]
    [InlineData("TOOLS_GIT", """{"way":"file","file":"C:/elsewhere/git.exe"}""", PipeClass.Person)]
    [InlineData("TOOLS_GIT", """{"way":"system","Way":"file"}""", PipeClass.Person)]
    [InlineData("TOOLS_GIT", """{"way":"sys\u0074em"}""", PipeClass.Open)]
    [InlineData("TOOLS_GIT", """{"way":7}""", PipeClass.Person)]
    [InlineData("TOOLS_GIT", """{}""", PipeClass.Person)]
    [InlineData("TOOLS_GIT", "null", PipeClass.Person)]
    [InlineData("PLUGIN_UPDATE", """{"id":"acme.watch"}""", PipeClass.Open)]
    [InlineData("PLUGIN_UPDATE", """{"id":"acme.watch","apply":false}""", PipeClass.Open)]
    [InlineData("PLUGIN_UPDATE", """{"id":"acme.watch","apply":true}""", PipeClass.Person)]
    [InlineData("PLUGIN_UPDATE", """{"id":"acme.watch","apply":false,"Apply":true}""", PipeClass.Person)]
    // Free up space's list is open; every other form starts a run removing what it names, the person's delete.
    [InlineData("SYSTEM_SPACE_FREE", """{"plan":true}""", PipeClass.Open)]
    [InlineData("SYSTEM_SPACE_FREE", """{"plan":false,"units":[]}""", PipeClass.Person)]
    [InlineData("SYSTEM_SPACE_FREE", """{"units":[{"kind":"logs","id":"2026-09-01.driver.jsonl"}]}""", PipeClass.Person)]
    [InlineData("SYSTEM_SPACE_FREE", """{"plan":true,"Plan":"yes"}""", PipeClass.Person)]
    // A history edit's plan is a look; its press, pinned or not, moves the branch.
    [InlineData("GIT_HISTORY_EDIT", """{"repository":"engine","edit":"rebase","branch":"topic","target":"main","plan":true}""", PipeClass.Open)]
    [InlineData("GIT_HISTORY_EDIT", """{"repository":"engine","edit":"rebase","branch":"topic","target":"main"}""", PipeClass.Person)]
    [InlineData("GIT_HISTORY_EDIT", """{"edit":"pick","plan":false,"said":true}""", PipeClass.Person)]
    // Another casing, a name said twice, or `said` beside `plan` opens nothing the handler would read as the press.
    [InlineData("GIT_HISTORY_EDIT", """{"repository":"engine","edit":"rebase","branch":"topic","target":"main","Plan":true}""", PipeClass.Person)]
    [InlineData("GIT_HISTORY_EDIT", """{"repository":"engine","edit":"rebase","branch":"topic","target":"main","plan":true,"said":true}""", PipeClass.Person)]
    [InlineData("GIT_HISTORY_EDIT", """{"edit":"pick","plan":true,"plan":true}""", PipeClass.Person)]
    [InlineData("SYSTEM_SPACE_FREE", """{"Plan":true,"units":[{"kind":"logs","id":"a"}]}""", PipeClass.Person)]
    [InlineData("SYSTEM_SPACE_FREE", """{"plan":true,"plan":true}""", PipeClass.Person)]
    [InlineData("TOOLS_GIT", """{"Way":"system"}""", PipeClass.Person)]
    [InlineData("PLUGIN_UPDATE", """{"id":"acme.watch","Apply":true}""", PipeClass.Person)]
    [InlineData("PLUGIN_UPDATE", """{"id":"acme.watch","apply":true,"apply":false}""", PipeClass.Person)]
    public void A_route_that_is_a_look_and_a_press_is_classed_by_its_payload(string route, string payload, PipeClass expected)
    {
        using var document = JsonDocument.Parse(payload);
        Assert.Equal(expected, PipeCensus.Daoris.Of(DriverModule.Name, route, document.RootElement));
    }

    /// <summary>With no payload at all, a route classed by its payload takes the class it gives a payload naming no form.</summary>
    [Fact]
    public void A_route_classed_by_its_payload_with_none_takes_its_otherwise()
    {
        Assert.Equal(PipeClass.Person, PipeCensus.Daoris.Of(DriverModule.Name, "TOOLS_GIT"));
        Assert.Equal(PipeClass.Open, PipeCensus.Daoris.Of(DriverModule.Name, "PLUGIN_UPDATE"));
    }

    [Fact]
    public void A_module_the_census_does_not_name_is_classed_nothing_and_so_refused()
    {
        Assert.Null(PipeCensus.Daoris.Of("SHENORA.REQUESTS", "CANCEL"));
        Assert.Null(PipeCensus.Daoris.Of("TEST.ELSEWHERE", "ANYTHING"));
    }

    /// <summary><c>ROUTES</c> answers itself from the census: each classed route, and a module classed whole once, with no route.</summary>
    [Fact]
    public void The_routes_it_lists_are_the_census()
    {
        var routes = PipeCensus.Daoris.Routes;

        Assert.Equal(DriverModule.Routes.Count, routes.Count(each => each.Module == DriverModule.Name));
        Assert.Contains(new PipeRoute(DriverModule.Name, "LAND_SESSION_TREE", PipeClass.Person), routes);
        Assert.Contains(new PipeRoute("DAORIS.WINDOWS", null, PipeClass.Window), routes);
        Assert.Equal(routes.Count, routes.Select(each => (each.Module, each.Route)).Distinct().Count());
        var git = Assert.Single(routes, each => each.Route == "TOOLS_GIT");
        Assert.Equal(PipeClass.Person, git.Class);
        Assert.Equal("way", git.When!.Field);
        Assert.Equal(PipeClass.Open, git.When.Class);
    }

    /// <summary>A test's census classes a route of its own beside the desktop's, and changes none of them.</summary>
    [Fact]
    public void A_census_with_one_more_route_keeps_the_rest()
    {
        var census = PipeCensus.Daoris.With("TEST.PRESS", "MOVE", PipeClass.Open);

        Assert.Equal(PipeClass.Open, census.Of("TEST.PRESS", "MOVE"));
        Assert.Equal(PipeClass.Person, census.Of("TEST.PRESS", "OTHER"));
        Assert.Equal(PipeClass.Person, census.Of(DriverModule.Name, "LAND_SESSION_TREE"));
        Assert.Null(PipeCensus.Daoris.Of("TEST.PRESS", "MOVE"));
    }

    /// <summary>
    /// While an update drains, a press waits for the restart only where it starts work the drain waits on, starts a process of
    /// its own that runs on past its answer, or changes a repository through git; a stop, a take-back, an answer to a session under way and
    /// a word whose resume is the loop's, which the drain holds, go on. A conversation's go-on after an answer alone is the chat runner's at
    /// once, so that press waits too. A role's act waits until each act is classed.
    /// </summary>
    [Theory]
    [InlineData("SESSION_QUEUE_EDIT", PipeDrain.Passes)]
    [InlineData("STOP_SESSION", PipeDrain.Passes)]
    [InlineData("RESOLVE_SESSION", PipeDrain.Passes)]
    [InlineData("STOP_OPINION", PipeDrain.Passes)]
    [InlineData("STOP_TASK", PipeDrain.Passes)]
    [InlineData("CANCEL_TURN", PipeDrain.Passes)]
    [InlineData("END_CHAT", PipeDrain.Passes)]
    [InlineData("SESSION_PERMISSION_ANSWER", PipeDrain.Passes)]
    [InlineData("SESSION_GO_AHEAD", PipeDrain.Passes)]
    [InlineData("RETRY_QUEST", PipeDrain.Passes)]
    [InlineData("SET_LANGUAGE", PipeDrain.Passes)]
    [InlineData("SUPERVISOR_DECIDE", PipeDrain.Passes)]
    [InlineData("SUPERVISOR_LEAVE", PipeDrain.Passes)]
    // A review's findings: Ask Daoris is told a dismissal as a turn, and a setting can release work the look starts outside the drain's
    // holds of quests (a helper asked under `ask`, now `on`).
    [InlineData("HELP_DISMISS", PipeDrain.Waits)]
    [InlineData("SET_HELPERS", PipeDrain.Waits)]
    [InlineData("SESSION_ANSWER_ALONE", PipeDrain.Waits)]
    [InlineData("SESSION_INPUT", PipeDrain.Waits)]
    // A word moved to an ended head goes on with it at once, as one said there does.
    [InlineData("SESSION_QUEUE_MOVE", PipeDrain.Waits)]
    [InlineData("START_CHAT", PipeDrain.Waits)]
    [InlineData("SESSION_HELPER_ASK", PipeDrain.Waits)]
    [InlineData("SESSION_PARTNER_ASK", PipeDrain.Waits)]
    [InlineData("SESSION_PARTNER_ACT", PipeDrain.Waits)]
    [InlineData("ASK_OPINION", PipeDrain.Waits)]
    [InlineData("PLUGIN_TRY", PipeDrain.Waits)]
    [InlineData("PLUGIN_TOOLS", PipeDrain.Waits)]
    [InlineData("PLUGIN_TOOL_FIX", PipeDrain.Waits)]
    [InlineData("TOOLS_DOWNLOAD", PipeDrain.Waits)]
    [InlineData("LAND_SESSION_TREE", PipeDrain.Waits)]
    [InlineData("LAND_ASK", PipeDrain.Waits)]
    [InlineData("DISCARD_SESSION_TREE", PipeDrain.Waits)]
    [InlineData("SYSTEM_SPACE_FREE", PipeDrain.Waits)]
    [InlineData("GIT_REF_CREATE", PipeDrain.Waits)]
    [InlineData("GIT_REF_UNDO", PipeDrain.Waits)]
    [InlineData("GIT_REF_DELETE", PipeDrain.Waits)]
    [InlineData("GIT_REF_RENAME", PipeDrain.Waits)]
    [InlineData("GIT_REF_FINISH", PipeDrain.Waits)]
    [InlineData("GIT_FETCH", PipeDrain.Waits)]
    [InlineData("GIT_PUSH", PipeDrain.Waits)]
    [InlineData("GIT_STASH_DROP", PipeDrain.Waits)]
    [InlineData("GIT_HISTORY_EDIT", PipeDrain.Waits)]
    [InlineData("ROLE_ACT", PipeDrain.Waits)]
    public void A_drain_holds_what_starts_work_or_changes_a_repository_and_lets_the_rest_through(string route, PipeDrain expected)
    {
        Assert.Equal(expected, PipeCensus.Daoris.DrainOf(DriverModule.Name, route));
        Assert.Equal(expected == PipeDrain.Waits, PipeCensus.Daoris.WaitsForDrain(DriverModule.Name, route, PipeCensus.Daoris.Of(DriverModule.Name, route)));
    }

    /// <summary>
    /// Every setting writes the driver's file and asks the look at once, and a draining look holds only
    /// quests, answered parks and intakes (<c>InstallUpdate.Drained</c>). So a press that can let the look start anything else waits:
    /// - helpers, which no repository's hold reaches: their rule, the cap's slot, the actor they run on, an account ready again;
    /// - a hand-over, a second opinion, a landing (its rule, its review, its line, the opinion's answer over the gate);
    /// - a workflow node's next step or a run's gates;
    /// - the keeping pass's removals;
    /// - a start held for a folder's trust, or by a plugin.
    /// One that changes only how later sessions run, or what they say, goes on.
    /// </summary>
    [Fact]
    public void A_press_that_can_release_work_the_drain_does_not_hold_waits_and_an_inert_setting_goes_on()
    {
        string[] releases =
        [
            "SET_HELPERS", "SET_CAP", "SET_ROLE", "SET_ROLES", "SET_COOLOFF", "ACCOUNT_USE", "HARNESSES_REFRESH", "SET_HAND_OVER", "SET_OPINION",
            "SET_REVIEW", "SET_LINE", "OPINION_ANYWAY", "OPINION_MYSELF", "WORK_HELD", "WORKFLOW_NODE", "WORK_RESUME",
            "WORKFLOW_LANDED", "WORKFLOW_SAVE", "WORKFLOW_USE", "WORKFLOW_KIND", "WORKFLOW_VERIFY", "SET_KEEP", "TRUST_FOLDER", "PLUGIN_ACTION", "PLUGIN_ADD", "PLUGIN_INSTALL", "PLUGIN_UPDATE",
        ];
        string[] inert =
        [
            "SET_HOLD", "SET_DRIVABLE", "SET_TREES", "SET_STANDING", "SET_COMMITS", "SET_ACCESS", "SET_READ_ACROSS",
            "SET_WRITE_ACROSS", "SET_HEAVY_GATES", "SET_SESSION_CHECKS", "SET_SESSION_OPTION", "SET_AGENT_SETTINGS", "SET_HELP_MODE",
            "SET_HELP_GRANT", "SET_NOTIFY", "SET_QUIET", "SET_LANGUAGE", "RULE_ACTION", "RULE_PROPOSAL", "TOOLS_LOCATION", "TOOLS_DELETE", "TOOLS_GIT",
            // An acceptance of a repository's integration declaration, a standing choice that starts nothing.
            "INTEGRATION_GATES",
        ];

        Assert.All(releases, route => Assert.Equal((route, PipeDrain.Waits), (route, PipeCensus.Daoris.DrainOf(DriverModule.Name, route))));
        Assert.All(inert, route => Assert.Equal((route, PipeDrain.Passes), (route, PipeCensus.Daoris.DrainOf(DriverModule.Name, route))));
    }

    /// <summary>
    /// A read waits for no drain, whatever its route; the other modules' presses go on; a person's route the drain does not
    /// class waits, the strictest, as an unclassed press is the person's.
    /// </summary>
    [Fact]
    public void A_read_never_waits_for_a_drain_and_an_unclassed_press_does()
    {
        Assert.False(PipeCensus.Daoris.WaitsForDrain(DriverModule.Name, "STATE", PipeClass.Open));
        Assert.False(PipeCensus.Daoris.WaitsForDrain(DriverModule.Name, "SYSTEM_SPACE_FREE", PipeClass.Open));
        Assert.Equal(PipeDrain.Passes, PipeCensus.Daoris.DrainOf("DAORIS.REGISTRY", "WRITE_DECLARATION"));
        Assert.Equal(PipeDrain.Passes, PipeCensus.Daoris.DrainOf("daoris.remotes", "SET"));
        Assert.Null(PipeCensus.Daoris.DrainOf(DriverModule.Name, "LAND_EVERYTHING"));
        Assert.True(PipeCensus.Daoris.WaitsForDrain(DriverModule.Name, "LAND_EVERYTHING", PipeClass.Person));
    }

    /// <summary>
    /// The person's routes whose payload carries a key kept in
    /// <c>profiles/</c> alone are keyed, kept in no record across a restart: today <c>DAORIS.REMOTES</c> <c>SET</c> alone, whose
    /// <c>key</c> is the workspace's deployment key (<c>remotes</c>, a <c>profiles</c> entry); each keyed route is a person's.
    /// </summary>
    /// <summary>A pasted token over the pipe would be kept in the waiting record and shown on the card, so the route refuses it.</summary>
    [Fact]
    public void A_git_identity_press_carrying_a_token_is_refused_whole()
    {
        var with = System.Text.Json.JsonDocument.Parse("""{"action":"add-https","host":"github.com","token":"x"}""").RootElement;
        var without = System.Text.Json.JsonDocument.Parse("""{"action":"add-https","host":"github.com"}""").RootElement;

        Assert.Contains("never sent over the pipe", PipeCensus.SecretRefusal("GIT_IDENTITY", with));
        Assert.Null(PipeCensus.SecretRefusal("GIT_IDENTITY", without));
        Assert.Null(PipeCensus.SecretRefusal("GIT_USE", with));
    }

    [Fact]
    public void The_keyed_routes_are_the_remotes_set_alone_and_each_is_the_person_s()
    {
        Assert.Equal(new[] { "DAORIS.REMOTES.SET", $"{DriverModule.Name.ToUpperInvariant()}.GIT_IDENTITY" }.Order(StringComparer.Ordinal), PipeCensus.KeyedRoutes.Order(StringComparer.Ordinal));
        Assert.True(PipeCensus.Keyed(DriverModule.Name, "GIT_IDENTITY"));
        Assert.Equal(Daoris.Driver.HomeCategory.Profiles, Daoris.Driver.HomeEntry.Remotes.Category);
        Assert.True(PipeCensus.Keyed("daoris.remotes", "SET"));
        Assert.False(PipeCensus.Keyed("DAORIS.REMOTES", "REMOVE"));
        Assert.False(PipeCensus.Keyed(DriverModule.Name, "SET_DRIVABLE"));
        foreach (var keyed in PipeCensus.KeyedRoutes)
        {
            var at = keyed.LastIndexOf('.');
            Assert.Equal(PipeClass.Person, PipeCensus.Daoris.Of(keyed[..at], keyed[(at + 1)..]));
        }
    }
}

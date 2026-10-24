using System.Runtime.CompilerServices;
using System.Text.Json;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The pipe's census (RPC1a, D171 point 3, the terminal RPC design §4.2): every route the pipe accepts is classed <c>open</c>,
/// <c>person</c> or <c>window</c>, the driver's route by route against D156 §3, so the same account opening the pipe gains no
/// person's authority. An unclassed press is the person's and an unclassed module is refused, and the census fails on a route
/// it does not class, so a route added later is classed in the change that adds it (`bridge-presses.md`).
/// </summary>
public sealed class PipeCensusTests
{
    [Fact]
    public void Every_route_of_the_driver_is_classed_and_every_class_names_a_route()
    {
        Assert.Empty(PipeCensus.Daoris.Unclassed(DriverModule.Name, DriverModule.Routes));
        Assert.Empty(PipeCensus.Daoris.Stale(DriverModule.Name, DriverModule.Routes));
    }

    /// <summary>
    /// The driver's twin of the census's classes (RPC1b, <c>RouteClass</c>) names each as the census does, so a verb reading its
    /// route's class before it looks for the instance reads the census's word.
    /// </summary>
    [Fact]
    public void The_driver_names_the_classes_as_the_census_does() =>
        Assert.Equal(Enum.GetNames<PipeClass>(), Enum.GetNames<Daoris.Driver.RouteClass>());

    /// <summary>
    /// Each press of the <c>trees</c> verbs names its route and the class the census gives it (RPC1b, the terminal RPC design §7.2):
    /// a verb whose route the census opens becomes the instance's client, so a route opened here fails until its verb's client
    /// path is built, its words from the route's answer held to the alone path's (§7.3), and the table says so.
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
    /// SUPERVISOR1a-ii (D208 §3.1): the supervisor's verb sends its answer as the role's act the census classes it, and reads what waits
    /// as an open read; the appointment is the window's alone, never a verb's.
    /// </summary>
    [Fact]
    public void The_supervisors_verb_names_its_routes_as_the_census_classes_them()
    {
        var answer = Daoris.Driver.SessionsSuperviseCommand.Answer;
        var waiting = Daoris.Driver.SessionsSuperviseCommand.Waiting;
        Assert.Equal((DriverModule.Name, PipeClass.Role, Daoris.Driver.RouteClass.Role), (answer.Module, PipeCensus.Daoris.Of(answer.Module, answer.Route), answer.Class));
        Assert.Equal((PipeClass.Open, Daoris.Driver.RouteClass.Open), (PipeCensus.Daoris.Of(waiting.Module, waiting.Route), waiting.Class));
        Assert.Equal(PipeClass.Window, PipeCensus.Daoris.Of(DriverModule.Name, Daoris.Driver.SupervisorRoutes.Appoint));
        Assert.Single(PipeCensus.Daoris.Routes, route => route.Class == PipeClass.Role);
    }

    /// <summary>
    /// RPC1c: the terminal's door reads the pipe's words for a confirmation as the pipe says them, a twin the driver library cannot
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
    /// The person's presses the host's verbs send over the pipe (RPC1c): each names a route of the driver's module that the census
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
    /// The standing choices <c>daoris-driver driver</c> sends (PUSH1f2a, D194 point 5): each a route of the driver's module the census
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
    /// The tools' presses <c>daoris-driver tool</c> sends (PUSH1f2c, D194 point 5): each a route of the driver's module the census classes
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

    /// <summary>
    /// The agents' presses <c>daoris-driver agent</c> sends (PUSH1f2d, D194 point 5): each a route of the driver's module the census
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
    /// The plugins' presses <c>daoris-driver plugin</c> sends (PUSH1f2f, D194 point 5): each a route of the driver's module the census
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
    /// The open reads and presses the host's verbs send (RPCOPEN1, RPC1e): each names a route of the driver's module that the census
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

        // The verbs left off are exactly those with no route, and each is a read: no press of the person's waits on RPC1c here.
        Assert.All(
            Daoris.Driver.OpenRoutes.Verbs.Where(each => each.Route is null),
            each => Assert.StartsWith("alone:", each.Reason, StringComparison.Ordinal));
    }

    /// <summary>
    /// RPC1e's proof (the terminal RPC design §1.2, §7.1): of the host's verbs with a window twin, only these stay off the pipe, each with
    /// its reason. By design, a conversation the terminal runs (<c>chat</c>, <c>help resume</c>, until RPC1g), a loop (<c>drive</c>), the
    /// updater's request (<c>update</c>) and the service's own doors; and, until a decision lets this machine's paths travel over the
    /// local pipe (§4.3), the reads whose words name this machine's folders or quote its trees, with the few no route answers. A verb
    /// that becomes a client leaves this list in the change that sends its route, and one that joins it says why here.
    /// </summary>
    [Fact]
    public void Only_the_verbs_that_stay_alone_are_off_the_pipe_each_with_its_reason()
    {
        string[] off =
        [
            "chat", "help resume <id>", "drive", "update",
            "quest delete|accept|done|reopen|go-on|check|review", "answer", "ask \"…\"", "sync status", "ask channel <id>",
            "trace", "git branches", "workflow", "logs",
            // GITAPP1a-i: the bench's verb times the git its own process starts.
            "git review <session>",
            "plugins show <id>", "plugins activity <id>", "plugins try <folder|id>", "plugins install <file>", "plugins new <id>",
            "help delete <id>", "setup --workspace <name>",
            // COAGENT1b: a session's pieces, read off the box under the home, which says the same with or without the instance.
            "sessions partner <id> --list",
        ];

        var left = Daoris.Driver.OpenRoutes.Verbs.Where(each => each.Route is null).ToList();

        Assert.Equal(off.Order(StringComparer.Ordinal), left.Select(each => each.Verb).Order(StringComparer.Ordinal));
        Assert.All(left, each => Assert.True(each.Reason.Length > "alone: ".Length, $"`{each.Verb}` says no reason"));
        // Every family of §1.2 the row names sends at least one route: work, history, help, opinion, setup, register, sync, plugins
        // and the channel.
        var sent = Daoris.Driver.OpenRoutes.Verbs.Where(each => each.Route is not null).Select(each => each.Verb)
            .Concat(Daoris.Driver.PersonPresses.Presses.Select(each => each.Verb)).ToList();
        foreach (var family in new[] { "quest pause", "history", "help", "opinion", "setup", "register", "sync", "plugins", "ask channel" })
        {
            Assert.Contains(sent, verb => verb.StartsWith(family, StringComparison.Ordinal));
        }
    }

    /// <summary>The census fails on a route it does not class, and on a class that names no route: the check the test above runs.</summary>
    [Fact]
    public void The_census_names_a_route_it_does_not_class_and_a_class_that_names_none()
    {
        var routes = DriverModule.Routes.Append("LAND_EVERYTHING").Where(route => route != "STATE").ToList();

        Assert.Equal(["LAND_EVERYTHING"], PipeCensus.Daoris.Unclassed(DriverModule.Name, routes));
        Assert.Equal(["STATE"], PipeCensus.Daoris.Stale(DriverModule.Name, routes));
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
    // Reads, and what D156 §3 leaves to the driver: run as asked.
    [InlineData("DAORIS.DRIVER", "SESSION_GROUPS", PipeClass.Open)]
    // NOTIFY2i: what came to the person, read by a terminal, a press by its act alone; the presses' own read stays the window's.
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
    [InlineData("DAORIS.DRIVER", "SWEEP", PipeClass.Person)]
    [InlineData("DAORIS.DRIVER", "RECOVERY_DROP", PipeClass.Person)]
    [InlineData("DAORIS.DRIVER", "WORK_HELD", PipeClass.Person)]
    [InlineData("DAORIS.DRIVER", "HISTORY_CLEAR", PipeClass.Person)]
    [InlineData("DAORIS.DRIVER", "SESSION_INPUT", PipeClass.Person)]
    [InlineData("DAORIS.DRIVER", "OPINION_MYSELF", PipeClass.Person)]
    [InlineData("DAORIS.DRIVER", "SET_REVIEW", PipeClass.Person)]
    // HEAVYRUN1b2b: the commands this machine runs for a repository's heavy gates are the person's to write.
    [InlineData("DAORIS.DRIVER", "SET_HEAVY_GATES", PipeClass.Person)]
    [InlineData("DAORIS.DRIVER", "SET_KEEP", PipeClass.Person)]
    // FABRIC-PIPELINE1b: which checks a repository's sessions run is the person's standing choice, and light lowers their part.
    [InlineData("DAORIS.DRIVER", "SET_SESSION_CHECKS", PipeClass.Person)]
    // What only a window does: the pickers, the file manager, Daoris's browser, its frame, its shells.
    [InlineData("DAORIS.DRIVER", "TOOLS_PICK", PipeClass.Window)]
    [InlineData("DAORIS.DRIVER", "SESSION_OPEN_FOLDER", PipeClass.Window)]
    [InlineData("DAORIS.DRIVER", "WORK_RECORDS_FOLDER", PipeClass.Window)]
    // RPC1c, PERSONDOOR1f: a terminal never reads or answers what waits for the window, and never reads the person key (D156 point 2).
    [InlineData("DAORIS.DRIVER", "PERSON_CONFIRMATIONS", PipeClass.Window)]
    [InlineData("DAORIS.DRIVER", "PERSON_CONFIRM", PipeClass.Window)]
    [InlineData("DAORIS.DRIVER", "PERSON_CONFIRM_SHOWN", PipeClass.Window)]
    [InlineData("DAORIS.DRIVER", "PERSON_KEY", PipeClass.Window)]
    // NOTIFY2j: nor what waits at the service's door as the window is told it; the service's own list stays its own door's.
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
    // KEEP1e: Free up space's list is open; every other form starts a run removing what it names, the person's delete.
    [InlineData("SYSTEM_SPACE_FREE", """{"plan":true}""", PipeClass.Open)]
    [InlineData("SYSTEM_SPACE_FREE", """{"plan":false,"units":[]}""", PipeClass.Person)]
    [InlineData("SYSTEM_SPACE_FREE", """{"units":[{"kind":"logs","id":"2026-09-01.driver.jsonl"}]}""", PipeClass.Person)]
    [InlineData("SYSTEM_SPACE_FREE", """{"plan":true,"Plan":"yes"}""", PipeClass.Person)]
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
}

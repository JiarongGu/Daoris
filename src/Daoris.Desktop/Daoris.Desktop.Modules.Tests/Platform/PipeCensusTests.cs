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
    [InlineData("DAORIS.DRIVER", "HISTORY_CLEAR", PipeClass.Person)]
    [InlineData("DAORIS.DRIVER", "SESSION_INPUT", PipeClass.Person)]
    [InlineData("DAORIS.DRIVER", "OPINION_MYSELF", PipeClass.Person)]
    [InlineData("DAORIS.DRIVER", "SET_REVIEW", PipeClass.Person)]
    // What only a window does: the pickers, the file manager, Daoris's browser, its frame, its shells.
    [InlineData("DAORIS.DRIVER", "TOOLS_PICK", PipeClass.Window)]
    [InlineData("DAORIS.DRIVER", "SESSION_OPEN_FOLDER", PipeClass.Window)]
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

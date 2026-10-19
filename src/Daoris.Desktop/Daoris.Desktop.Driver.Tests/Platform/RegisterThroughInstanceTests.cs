using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris-driver register</c> through the running Daoris (RPCOPEN1, RPC1e, the terminal RPC design §7.2 and §7.3): each repository
/// a <c>REGISTRY_REFRESH</c>, the verb's words the same as alone, case for case, against a stand-in of the pipe that answers as the
/// route does, from the same follow.
/// </summary>
public sealed class RegisterThroughInstanceTests
{
    private const string Declared = """{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":["play"],"accepts":[]}}""";

    /// <summary>A world with a repository that registers, one that is not set up and one that is already as its line says.</summary>
    private static RegistrationStandIn World()
    {
        var world = new RegistrationStandIn();
        world.Rows.Add(RegistrationStandIn.Row("game", "/checkouts/game"));
        world.Rows.Add(RegistrationStandIn.Row("engine", "/checkouts/engine"));
        world.Lines["game"] = RegistrationStandIn.OnLine(Declared);
        world.Lines["engine"] = RegistrationStandIn.OnLine(null);
        return world;
    }

    /// <summary>The route's answer for one repository, built as <c>REGISTRY_REFRESH</c> builds it, from the same follow.</summary>
    private static async Task<object?> Routed(JsonElement payload, string? refresh = null)
    {
        var name = payload.GetProperty("repository").GetString()!;
        var report = await RegistrationFollow.FollowAsync(World(), [name]);
        return report.Followed.Single().ToWire(refresh ?? report.Refresh);
    }

    [Theory]
    [InlineData("")]
    [InlineData("--repository game")]
    [InlineData("--repository engine")]
    [InlineData("--repository ghost")]
    public async Task Through_the_instance_the_words_and_the_exit_are_the_alone_paths(string words)
    {
        if (!OperatingSystem.IsWindows()) return;
        var args = words.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var alone = new StringWriter();
        var aloneExit = await RegisterCommand.RunAsync(args, alone, World());

        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        standIn.Respond = (_, payload) => Routed(payload).GetAwaiter().GetResult();
        var routed = new StringWriter();
        var theWorld = new RegistrationStandIn();
        theWorld.Rows.AddRange(World().Rows);

        var exit = await RegisterCommand.RunAsync(args, routed, theWorld, home.World("register"));

        Assert.Equal(aloneExit, exit);
        Assert.Equal(alone.ToString(), routed.ToString());
        Assert.All(standIn.Presses, press =>
        {
            Assert.Equal("DAORIS.DRIVER", press.GetProperty("module").GetString());
            Assert.Equal("REGISTRY_REFRESH", press.GetProperty("type").GetString());
        });
        // Nothing followed in this process: the instance did, and this process sent nothing to the service.
        Assert.Empty(theWorld.Sent);
        Assert.Equal("", home.Error.ToString());
    }

    [Fact]
    public async Task Every_repository_with_a_checkout_is_one_press_in_the_order_alone_says_them()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        standIn.Respond = (_, payload) => Routed(payload).GetAwaiter().GetResult();

        await RegisterCommand.RunAsync([], new StringWriter(), World(), home.World("register"));

        Assert.Equal(
            ["engine", "game"],
            standIn.Presses.Select(press => press.GetProperty("payload").GetProperty("repository").GetString()).ToArray());
    }

    [Fact]
    public async Task A_refresh_that_failed_is_said_once_as_alone_says_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var aloneWorld = World();
        aloneWorld.RefreshFails = "the service is gone";
        var alone = new StringWriter();
        await RegisterCommand.RunAsync([], alone, aloneWorld);

        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        standIn.Respond = (_, payload) => Routed(payload, "the service is gone").GetAwaiter().GetResult();
        var routed = new StringWriter();
        await RegisterCommand.RunAsync([], routed, World(), home.World("register"));

        Assert.Contains("register: the index was not read again after registering: the service is gone", routed.ToString());
        Assert.Equal(1, routed.ToString().Split("the index was not read again").Length - 1);
        Assert.Equal(alone.ToString().Split("registered").Length, routed.ToString().Split("registered").Length);
    }

    [Fact]
    public async Task An_instance_that_does_not_answer_is_said_and_nothing_is_registered_alone()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        using var running = RunningAddress.Write(home.Home, "http://localhost:5177", home.Pipe);
        var world = World();
        var output = new StringWriter();
        var instance = home.World("register");

        var exit = await RegisterCommand.RunAsync([], output, world, new InstanceWorld
        {
            Home = instance.Home, Environment = instance.Environment, BaseDirectory = instance.BaseDirectory, Status = instance.Status,
            Out = instance.Out, Error = instance.Error, Verb = "register", Answer = TimeSpan.FromSeconds(1),
        });

        Assert.Equal(2, exit);
        Assert.Empty(world.Sent);
        Assert.Contains("did not answer; nothing was done.", home.Error.ToString());
    }

    [Fact]
    public async Task With_no_instance_it_is_the_alone_path_whole()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var world = World();
        var routed = new StringWriter();

        var exit = await RegisterCommand.RunAsync([], routed, world, home.World("register"));

        var alone = new StringWriter();
        var other = World();
        Assert.Equal(await RegisterCommand.RunAsync([], alone, other), exit);
        Assert.Equal(alone.ToString(), routed.ToString());
        Assert.Equal(other.Sent.Count, world.Sent.Count);
        Assert.Equal(1, world.Refreshes);
    }
}

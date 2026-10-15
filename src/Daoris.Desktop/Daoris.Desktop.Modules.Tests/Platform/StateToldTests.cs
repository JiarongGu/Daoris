using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A press from the terminal reaches an open window (STATEHUB1e, D169 point 8): `daoris-driver trees …` runs in another process with
/// no bus, tells its press in the request folder, and the shell's loop, hearing it at its watch's next look, publishes the same
/// <c>STATE_CHANGED</c> the window's press of that route publishes.
/// </summary>
public sealed class StateToldTests : DriverModuleBridge
{
    private static readonly JsonSerializerOptions Wire = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private IReadOnlyList<EventMessage> Changed() => [.. Raised.Where(message => message.Type == StatePublisher.Type)];

    private static string Shape(object? payload) => JsonSerializer.Serialize(payload, Wire);

    /// <summary>The window's press of <paramref name="route"/> about <paramref name="session"/>, as the page sends it.</summary>
    private static IpcRequest Pressed(string route, string? session) => new()
    {
        Id = "a1b2c3d4",
        Module = DriverModule.Name,
        Type = route,
        // Every session field a declaration names, so whichever the route reads is there.
        Payload = session is null ? null : JsonSerializer.SerializeToElement(new { id = session, session }),
    };

    /// <summary>The shell's loop's watch, as it is wired (`DriverLoop`): what it hears, published on the bus.</summary>
    private SessionRequestWatch Watch() =>
        new(Home, runsHere: _ => false, stop: _ => true, service: () => null) { Told = press => StatePublisher.Publish(Bus, press) };

    [Fact]
    public async Task A_landing_recorded_by_another_process_publishes_landing()
    {
        // As `daoris-driver trees land s1a2b3c4` does in its own process, once it has landed.
        Assert.True(new PressesTold(Home).Tell(new PressTold(PressesTold.Land, "s1a2b3c4", DateTimeOffset.UtcNow)));
        await using var watch = Watch();

        Assert.Single(watch.Hear());

        await UntilAsync(() => Changed().Count == 1);
        var changed = Assert.Single(Changed());
        Assert.Equal(StatePublisher.Module, changed.Module);
        Assert.Equal("""{"scopes":["sessions","landing","diff","handOff","sweep","treesSync"],"sessions":["s1a2b3c4"]}""", Shape(changed.Payload));
    }

    [Fact]
    public void Each_press_a_terminal_tells_publishes_what_the_window_s_press_of_its_route_publishes()
    {
        foreach (var route in PressesTold.Routes)
        {
            // A route a terminal names that no mark declares would publish nothing: a renamed route fails here.
            var change = DriverModule.Declared(Pressed(route, null));
            Assert.True(change is { Scopes: not StateScopes.None }, $"`{route}` declares no change");

            foreach (var session in new[] { "s1a2b3c4", null })
            {
                Assert.Equal(
                    Shape(StatePublisher.Event(change!, Pressed(route, session))),
                    Shape(StatePublisher.Told(new PressTold(route, session, DateTimeOffset.UtcNow))));
            }
        }
    }

    [Fact]
    public async Task A_press_told_of_a_route_that_declares_nothing_publishes_nothing()
    {
        new PressesTold(Home).Tell(new PressTold("LANDING", "s1a2b3c4", DateTimeOffset.UtcNow));
        new PressesTold(Home).Tell(new PressTold("NO_SUCH_ROUTE", null, DateTimeOffset.UtcNow));
        await using var watch = Watch();

        Assert.Equal(2, watch.Hear().Count);

        Assert.Null(StatePublisher.Told(new PressTold("LANDING", "s1a2b3c4", DateTimeOffset.UtcNow)));
        await Task.Delay(100);
        Assert.Empty(Changed());
    }
}

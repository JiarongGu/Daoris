using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The doctrine console's two holds: Ctrl+C is held for as long as the carried CLI runs and not a moment longer, and the driver's
/// Node floor, its words and the CLI's own agree.
/// </summary>
public sealed class DoctrineConsoleTests
{
    /// <summary>A stand-in for the console's signal: what subscribed and has not yet let go, and a way to press.</summary>
    private sealed class Signal
    {
        private readonly List<Action<InterruptPress>> _handlers = [];

        public int Subscribed => _handlers.Count;

        public IDisposable Subscribe(Action<InterruptPress> handler)
        {
            _handlers.Add(handler);
            return new Release(() => _handlers.Remove(handler));
        }

        public InterruptPress Press()
        {
            var press = new InterruptPress();
            foreach (var handler in _handlers.ToArray()) handler(press);
            return press;
        }

        private sealed class Release(Action release) : IDisposable
        {
            public void Dispose() => release();
        }
    }

    /// <summary>A Ctrl+C reaches the child too, which ends its command: while it runs, the press is cancelled here so this process waits for that end.</summary>
    [Fact]
    public void A_press_while_held_is_cancelled_so_the_wait_goes_on()
    {
        var signal = new Signal();

        using (InterruptHold.Take(signal.Subscribe))
        {
            Assert.Equal(1, signal.Subscribed);
            Assert.True(signal.Press().Cancel);
        }
    }

    /// <summary>The hold ends with the child: letting go removes the handler, so a later press ends this process as it would have.</summary>
    [Fact]
    public void Letting_go_removes_the_handler()
    {
        var signal = new Signal();

        InterruptHold.Take(signal.Subscribe).Dispose();

        Assert.Equal(0, signal.Subscribed);
        Assert.False(signal.Press().Cancel);
    }

    /// <summary>The hold is taken before the child starts and never by a bare event subscription, which nothing removes.</summary>
    [Fact]
    public void The_console_holds_before_it_starts_the_child_and_subscribes_nowhere_itself()
    {
        var source = File.ReadAllText(Path.Combine(GitSeed.RepoRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Host", "DoctrineConsole.cs"));

        var held = source.IndexOf("InterruptHold.Take(", StringComparison.Ordinal);
        Assert.True(held > 0, "the console takes no hold");
        Assert.True(held < source.IndexOf("StartAsync(", StringComparison.Ordinal), "the hold is taken after the child starts");
        Assert.DoesNotContain("CancelKeyPress", source);
    }

    /// <summary>The floor the CLI's entry checks, the one the set-up refuses below, and the words both say it in are the one number.</summary>
    [Fact]
    public void The_floor_the_cli_checks_is_the_floor_the_driver_names()
    {
        var floor = File.ReadAllText(Path.Combine(GitSeed.RepoRoot(), "src", "Daoris.Cli", "bin", "node-floor.mjs"));
        var cli = int.Parse(Regex.Match(floor, @"NODE_FLOOR = (\d+);").Groups[1].Value);
        Assert.Equal(cli, SetupPressRules.NodeFloor);

        var said = $"Node.js {cli} or later";
        Assert.Contains(said, CommandWords.NoNode(null));
        Assert.Contains("Node.js ${NODE_FLOOR} or later", floor);

        var refusal = SetupPressRules.NodeRefusal(new SetupTools("node", "20.1.0", null, "daoris", "0.0.1", null))!;
        Assert.Contains($"older than {cli}", refusal.Sentence);
    }
}

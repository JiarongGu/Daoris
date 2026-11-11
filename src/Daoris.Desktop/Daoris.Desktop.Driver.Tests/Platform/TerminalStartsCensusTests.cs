using System.Reflection;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Every terminal verb whose own process can start one asks the running Daoris first, or is named with why it may not: so what a
/// session runs is the application's child, under it in the system's tree, unless this list says why not.
/// </summary>
/// <remarks>
/// <para><b>Read from the built program.</b> Each console of the driver's host (a type named <c>…Console</c> or <c>…Verb</c>) is
/// followed through the calls its IL makes (<see cref="CallReach"/>) to a start: the start gate, a heavy run's gate, a git act's
/// step or a terminal's shell. One that reaches a start is listed below: <c>null</c> where it asks the running Daoris before it acts
/// (it reaches <see cref="InstanceDoor.FindAsync"/>), and starts its own only where none takes the work, or its reason.</para>
/// <para>Its limit is the reach's: a start behind an interface or an abstract method is not followed, nor a verb the host's entry
/// handles inline.</para>
/// </remarks>
[Trait(Category.Name, Category.Census)]
public sealed class TerminalStartsCensusTests
{
    private const string Reads =
        "git's reads and a tool's version, seconds long and waited on, so they end before the verb does: a read is the terminal's own";

    private static readonly Dictionary<string, string?> Starts = new(StringComparer.Ordinal)
    {
        ["AgentsConsole"] = null,
        ["AskAtWorkConsole"] = null,
        ["BrainstormMeasureConsole"] = "a measurement the person starts on purpose, run in this process because it holds the loop's records for the run's length",
        ["DoctrineConsole"] = "the doctrine CLI, the program's other half: doctrine works with no Daoris running, and the verb waits on it at its console",
        ["DriveConsole"] = "the headless loop is a running Daoris of its own, started only where no window holds the home",
        ["FilesConsole"] = Reads,
        ["GitConsole"] = null,
        ["HeavyConsole"] = null,
        ["HelpConsole"] = null,
        ["IntegrateConsole"] = null,
        ["OpinionConsole"] = null,
        ["PlaceConsole"] = null,
        ["QuestCheckConsole"] = Reads,
        ["QuestReviewConsole"] = null,
        ["SessionsConsole"] = null,
        ["ToolsConsole"] = Reads,
        ["TreesCleanVerb"] = null,
        ["TreesConsole"] = null,
        ["TreesEntryLandVerb"] = null,
        ["TreesRemoveVerb"] = "git's removal of one tree under the home's lease, seconds long and waited on",
        ["TreesStateVerb"] = Reads,
        ["TreesSyncVerb"] = null,
        ["VerifyConsole"] = Reads,
        ["WorkConsole"] = null,
        ["WorkflowConsole"] = null,
    };

    private static readonly string[] Sinks = ["ProcessStarts", "HeavyStep", "HeldChild", "PseudoConsole"];

    private static readonly Lazy<Dictionary<string, (IReadOnlyList<string> Through, bool Asks)>> Read = new(() =>
    {
        var host = Assembly.LoadFrom(DriverHostTests.HostDll());
        var reach = new CallReach(host, typeof(InstanceDoor).Assembly);
        var find = typeof(InstanceDoor).GetMethod(nameof(InstanceDoor.FindAsync))!;
        return host.GetTypes()
            .Where(type => !type.IsNested && type.Namespace == "Daoris.Driver.Host" && (type.Name.EndsWith("Console", StringComparison.Ordinal)
                || type.Name.EndsWith("Verb", StringComparison.Ordinal)))
            .ToDictionary(type => type.Name, type =>
            {
                var reached = reach.From(CallReach.Declarations(type));
                IReadOnlyList<string> through =
                    [.. reached.Where(method => reach.CalleesOf(method).Any(Starting)).Select(Owner).Distinct().Order(StringComparer.Ordinal)];
                return (through, reached.Any(method => method.Module == find.Module && method.MetadataToken == find.MetadataToken));
            });
    });

    private static bool Starting(MethodBase method) =>
        method.DeclaringType is { Namespace: "Daoris.Driver" } type && Sinks.Contains(type.Name.Split('`')[0])
        || method is { Name: "Start", DeclaringType.FullName: "System.Diagnostics.Process" };

    /// <summary>The type a method is written in: a lambda's or a state machine's outermost.</summary>
    private static string Owner(MethodBase method)
    {
        var type = method.DeclaringType!;
        while (type.DeclaringType is { } outer) type = outer;
        return type.Name.Split('`')[0];
    }

    [Fact]
    public void Every_verb_that_can_start_a_process_asks_the_running_app_first_or_is_named_with_why_it_may_not()
    {
        var problems = new List<string>();
        foreach (var (console, (through, asks)) in Read.Value.OrderBy(each => each.Key, StringComparer.Ordinal))
        {
            var starts = through.Count > 0;
            if (starts && !Starts.ContainsKey(console))
            {
                problems.Add($"{console} can start a process through {string.Join(", ", through)} and is not listed "
                    + $"({(asks ? "it asks the running Daoris first" : "it never asks the running Daoris")}): ask it first, or say why not");
            }

            if (!starts && Starts.ContainsKey(console)) problems.Add($"{console} starts no process now: take it off the list");
            if (starts && Starts.TryGetValue(console, out var why) && why is null && !asks) problems.Add($"{console} is listed as asking the running Daoris and never looks for it");
        }

        problems.AddRange(Starts.Keys.Where(console => !Read.Value.ContainsKey(console)).Select(console => $"{console} is no console of the host now"));
        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    [Fact]
    public void A_gate_run_asks_the_running_app_before_it_starts_a_gate_of_its_own()
    {
        Assert.Contains("HeavyRun", Read.Value["HeavyConsole"].Through);
        Assert.True(Read.Value["HeavyConsole"].Asks);
        Assert.Null(Starts["HeavyConsole"]);
    }
}

using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The doors census: every setting the window writes has a door an agent can use, a <c>daoris</c> verb Ask Daoris and a session run,
/// or the reason it has none beside its route (<c>[NoTwin]</c>). A setting the window writes is a person's press that rests on a
/// setting's value (a <see cref="PipeGroundKind.Setting"/> ground) or is named <c>SET_…</c>. Its verb is the one a verb's table says
/// sends its route (<see cref="ChoiceRoutes"/>, <see cref="ToolRoutes"/>, <see cref="AgentRoutes"/>, <see cref="PluginRoutes"/>,
/// <see cref="PersonPresses"/>, <see cref="Daoris.Driver.TreesRoutes"/>, the integrate verbs), so the window's route judges the verb's press, or a
/// verb that writes the same file through the route's own judge alone (<see cref="SettingTwins.Alone"/>).
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class SettingDoorsCensusTests
{
    /// <summary>The doors the census holds the settings to, as one build has them, each route by its <see cref="PipeTargets.Key"/>.</summary>
    internal sealed record Doors(
        IReadOnlyCollection<string> Settings,
        IReadOnlyCollection<string> Presses,
        IReadOnlyList<(string Verb, string Route)> Twins,
        IReadOnlyDictionary<string, string> NoTwin);

    /// <summary>What this build's settings, verbs and reasons are.</summary>
    internal static Doors Built()
    {
        var targets = PipeTargets.Daoris;
        var settings = targets.Rows.Where(row => row.Value.Grounds.Any(ground => ground.Kind == PipeGroundKind.Setting)).Select(row => row.Key)
            .Concat(DriverModule.Routes.Where(route => route.StartsWith("SET_", StringComparison.Ordinal)))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
        IEnumerable<(string Verb, InstanceRoute? Route)> sent =
        [
            .. ChoiceRoutes.Verbs.Select(each => (each.Verb, each.Route)),
            .. ToolRoutes.Verbs.Select(each => (each.Verb, each.Route)),
            .. AgentRoutes.Verbs.Select(each => (each.Verb, each.Route)),
            .. PluginRoutes.Verbs.Select(each => (each.Verb, each.Route)),
            .. PersonPresses.Presses.Select(each => (each.Verb, (InstanceRoute?)each.Route)),
            .. Daoris.Driver.TreesRoutes.Presses.Select(each => (each.Verb, (InstanceRoute?)each.Route)),
            ("integrate <repository>", IntegrateCommand.Route),
            ("integrate <repository> --accept|--withdraw", IntegrateGatesCommand.Press),
        ];
        return new Doors(
            settings,
            [.. targets.Rows.Keys],
            [
                .. sent.Where(each => each.Route is not null).Select(each => (each.Verb, PipeTargets.Key(each.Route!.Module, each.Route.Route))),
                .. SettingTwins.Alone.Select(each => (each.Verb, PipeTargets.Key(each.Route.Module, each.Route.Route))),
            ],
            targets.NoTwin);
    }

    /// <summary>Each way a setting and its doors part: a sentence a problem, none where each has one door or one reason.</summary>
    internal static IReadOnlyList<string> Problems(Doors doors)
    {
        var problems = new List<string>();
        var twinned = doors.Twins.Select(each => each.Route).ToHashSet(StringComparer.Ordinal);
        foreach (var setting in doors.Settings)
        {
            var reason = doors.NoTwin.TryGetValue(setting, out var why) ? why : null;
            if (!twinned.Contains(setting) && reason is null)
            {
                problems.Add($"{setting}, a setting the window writes, has no verb that writes it and says no reason it has none ([NoTwin])");
            }
            else if (twinned.Contains(setting) && reason is not null)
            {
                problems.Add($"{setting} has a verb (`{doors.Twins.First(each => each.Route == setting).Verb}`) and says why it has none");
            }
            else if (reason is { } said && string.IsNullOrWhiteSpace(said))
            {
                problems.Add($"{setting} says an empty reason it has no verb");
            }
        }

        foreach (var stray in doors.NoTwin.Keys.Where(route => !doors.Settings.Contains(route)))
        {
            problems.Add($"{stray} says why no verb writes it, and is no setting the window writes");
        }

        foreach (var (verb, route) in SettingTwins.Alone.Select(each => (each.Verb, PipeTargets.Key(each.Route.Module, each.Route.Route))))
        {
            if (!doors.Presses.Contains(route)) problems.Add($"`{verb}` writes {route} alone, and the window has no such press");
        }

        return problems;
    }

    [Fact]
    public void Every_setting_the_window_writes_has_a_verb_or_says_why_it_has_none()
    {
        var problems = Problems(Built());
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>The settings it counts are the window's standing choices, the line's policy among them, and none of the acts.</summary>
    [Fact]
    public void It_counts_the_window_s_settings_and_none_of_its_acts()
    {
        var settings = Built().Settings;

        Assert.Contains("SET_LINE", settings);
        Assert.Contains("SET_SESSION_CHECKS", settings);
        Assert.Contains("DAORIS.REMOTES.SET", settings);
        Assert.DoesNotContain("SESSION_INPUT", settings);
        Assert.DoesNotContain("SWEEP", settings);
    }

    [Fact]
    public void A_setting_with_neither_a_verb_nor_a_reason_fails()
    {
        var built = Built();
        var doors = built with { Twins = [.. built.Twins.Where(each => each.Route != "SET_NOTIFY")] };

        Assert.Contains("SET_NOTIFY, a setting the window writes, has no verb that writes it and says no reason it has none ([NoTwin])", Problems(doors));
    }

    [Fact]
    public void A_setting_with_a_verb_and_a_reason_fails()
    {
        var built = Built();
        var doors = built with { NoTwin = new Dictionary<string, string>(built.NoTwin) { ["SET_NOTIFY"] = "the window's alone" } };

        Assert.Contains("SET_NOTIFY has a verb (`driver notify on|off`) and says why it has none", Problems(doors));
    }

    [Fact]
    public void A_reason_on_no_setting_fails()
    {
        var built = Built();
        var doors = built with { NoTwin = new Dictionary<string, string>(built.NoTwin) { ["SESSION_INPUT"] = "words" } };

        Assert.Contains("SESSION_INPUT says why no verb writes it, and is no setting the window writes", Problems(doors));
    }
}

using System.Reflection;
using System.Text.RegularExpressions;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The settings census: every machine setting is declared once, in <see cref="MachineSettings.All"/>, and has one key, which
/// each of its doors spells. The terminal's verb is the key, in the <c>settings</c> group; its route is <c>SET_&lt;KEY&gt;</c>, a
/// machine's setting that says the driver's state changed; and the terminal sends that route from that verb alone. A setting listed twice,
/// two keys on one field of <c>driver.json</c>, or a second verb on one setting's route fails. The page's half is <c>settings/everySetting.test.ts</c>.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class MachineSettingsCensusTests
{
    /// <summary>The doors the census holds the list to, as one build has them.</summary>
    internal sealed record SettingDoors(
        IReadOnlyCollection<string> Verbs,
        IReadOnlyCollection<string> Group,
        IReadOnlyCollection<string> Routes,
        IReadOnlyCollection<string> MachineRoutes,
        IReadOnlyCollection<string> DriverRoutes,
        IReadOnlyList<(string Verb, string Route)> Sends);

    private static readonly Regex OneWord = new("^[a-z]+$");

    /// <summary>What this build's doors are.</summary>
    internal static SettingDoors Built()
    {
        var group = typeof(DriverChoicesCommand).GetCustomAttributes<CommandGroupAttribute>().Single(each => each.Name == "settings");
        return new SettingDoors(
            DriverChoicesCommand.Verbs,
            group.Verbs,
            DriverModule.Routes,
            [.. PipeTargets.Daoris.Marks.Where(mark => mark.Value.None == OwnerNone.MachineSetting).Select(mark => mark.Key)],
            [.. DriverModule.Routes.Where(route =>
                StatePublisher.Declared(new IpcRequest { Id = "census", Module = DriverModule.Name, Type = route }) is { } change
                && change.Scopes.HasFlag(StateScopes.Driver))],
            [.. ChoiceRoutes.Verbs.Where(each => each.Route is not null)
                .SelectMany(each => each.Verb.Split(' ')[1].Split('|').Select(word => (word, each.Route!.Route)))]);
    }

    /// <summary>Each way the list and the doors part: a sentence a problem, none where each setting is one declaration and one key.</summary>
    internal static IReadOnlyList<string> Problems(IReadOnlyList<MachineSetting> settings, SettingDoors doors)
    {
        var problems = new List<string>();
        foreach (var twice in settings.GroupBy(setting => setting.Key, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1))
        {
            problems.Add($"`{twice.Key}` is declared {twice.Count()} times");
        }

        foreach (var shared in settings.GroupBy(setting => setting.File, StringComparer.Ordinal).Where(group => group.Count() > 1))
        {
            problems.Add($"{string.Join(" and ", shared.Select(setting => $"`{setting.Key}`"))} are two keys for one setting, driver.json's `{shared.Key}`");
        }

        foreach (var setting in settings)
        {
            var key = setting.Key;
            if (!OneWord.IsMatch(key)) problems.Add($"`{key}` is no key every door can spell: one lower-case word");
            if (!doors.Verbs.Contains(key) || !doors.Group.Contains(key)) problems.Add($"`{key}` has no terminal verb `daoris settings {key}`");
            if (!doors.Routes.Contains(setting.Route)) problems.Add($"`{key}` has no route {setting.Route}");
            else if (!doors.MachineRoutes.Contains(setting.Route)) problems.Add($"{setting.Route} is not marked a machine's setting");
            else if (!doors.DriverRoutes.Contains(setting.Route)) problems.Add($"{setting.Route} does not say the driver's state changed");
            if (!doors.Sends.Contains((key, setting.Route))) problems.Add($"`daoris settings {key}` does not send {setting.Route}");
            foreach (var (verb, _) in doors.Sends.Where(send => send.Route == setting.Route && send.Verb != key))
            {
                problems.Add($"`{verb}` is a second key for `{key}`: both send {setting.Route}");
            }

        }

        return problems;
    }

    [Fact]
    public void Every_setting_is_one_declaration_and_one_key_that_each_of_its_doors_spells()
    {
        var problems = Problems(MachineSettings.All, Built());
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void Each_field_the_list_names_is_the_one_the_file_writes()
    {
        var written = DriverConfig.Empty.WithNotify(false).WithCap(3).WithCoolOff(30).WithQuiet(15).WithTerminal(TerminalTiers.Act).ToJson();
        foreach (var setting in MachineSettings.All.Where(setting => setting.Key != "keep"))
        {
            Assert.Contains($"\"{setting.File}\":", written);
        }

        Assert.Equal(KeepChoices.Key, MachineSettings.Of("keep")!.File);
        Assert.Equal(QuietSetting.File, MachineSettings.Of("quiet")!.File);
        Assert.Equal(TerminalTiers.File, MachineSettings.Of("terminal")!.File);
    }

    [Fact]
    public void A_setting_declared_twice_fails()
    {
        MachineSetting[] twice = [.. MachineSettings.All, MachineSettings.Of("quiet")!];

        Assert.Contains("`quiet` is declared 2 times", Problems(twice, Built()));
    }

    [Fact]
    public void Two_keys_on_one_field_fail()
    {
        MachineSetting[] two = [.. MachineSettings.All, new("timeout", "quietMinutes", Card: null)];

        Assert.Contains("`quiet` and `timeout` are two keys for one setting, driver.json's `quietMinutes`", Problems(two, Built()));
    }

    [Fact]
    public void A_second_verb_on_one_settings_route_fails()
    {
        var built = Built();
        var doors = built with { Sends = [.. built.Sends, ("timeout", "SET_QUIET")] };

        Assert.Contains("`timeout` is a second key for `quiet`: both send SET_QUIET", Problems(MachineSettings.All, doors));
    }

}

using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris settings checks &lt;n&gt;|auto</c>: how many checks the machine runs at once (<see cref="ChecksSetting"/>), a press of
/// <c>SET_CHECKS</c> like the rest; <c>auto</c> clears the pinned number.
/// </summary>
public sealed class ChecksVerb : ChoiceVerb
{
    public override string Word => ChecksSetting.File;

    public override int? Said(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world) =>
        CheckBudgetsCommand.Asks(args) && !CheckBudgetsCommand.Changes(args) ? CheckBudgetsCommand.Said(args, world) : null;

    public override ChoicePress Plan(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        if (CheckBudgetsCommand.Asks(args)) return CheckBudgetsCommand.Plan(args, world);
        var path = world.ConfigPath;
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var said = args.Skip(1).FirstOrDefault();
        int? checks = said == ChecksSetting.Auto ? null
            : int.TryParse(said, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var number) && ChecksSetting.Takes(number) ? number
            : throw new ChoiceWords(ChecksSetting.Refusal);
        return new(Word, ChoiceRoutes.Checks, new JsonObject { ["checks"] = checks }, _ =>
        {
            StandingChoices.Write(path, config => config.WithChecks(checks));
            return Task.FromResult<object?>(null);
        }, _ => null, _ =>
        {
            Say(checks is { } pinned
                ? $"daoris: this machine runs at most {pinned} heavy run(s) at once, whatever its memory, and never more than its heavy-run admission holds."
                : "daoris: this machine runs another heavy run while its free memory holds one more (8 GB each, 8 GB more for each still in its first step), up to a quarter of its processors.");
            Say("  Checks, sessions' heavy runs and integrations' gates count alike; a repository's run side by side unless its daoris.gates.json says fewer (`heavySlots`).");
            Say($"  Written to {path} — read at the next look, so nothing restarts.");
            return 0;
        });
    }

    /// <summary>The setting as the list says it: a pinned number, auto, or auto because the file's value does not read, which the reader takes as auto.</summary>
    public override void Listed(DriverConfig choices, DriverChoicesWorld world) => world.Instance.Out.WriteLine(choices.Checks is { } pinned
        ? $"  checks     {pinned} at once  (set — `daoris settings checks auto` runs as many as the memory admits)"
        : ChecksSetting.Unread(world.ConfigPath) is { } unread
            ? $"  checks     auto  (the file's `checks` is {unread}, which does not read as auto or 1 to {ChecksSetting.Most}, so the default runs — `daoris settings checks <n>|auto` writes one that reads)"
            : "  checks     auto  (as many at once as the memory admits, the default — `daoris settings checks <n>` pins a number)");
}

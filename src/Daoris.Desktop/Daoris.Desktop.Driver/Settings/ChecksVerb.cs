using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris settings checks &lt;n&gt;|auto</c>: how many checks the machine runs at once (<see cref="ChecksSetting"/>), a press of
/// <c>SET_CHECKS</c> like the rest; <c>auto</c> clears the pinned number.
/// </summary>
public sealed class ChecksVerb : ChoiceVerb
{
    public override string Word => ChecksSetting.File;

    public override ChoicePress Plan(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
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
                ? $"daoris: this machine runs at most {pinned} check(s) at once, whatever its memory."
                : "daoris: this machine runs another check while its free memory holds one more (6 GB a check), up to a quarter of its processors.");
            Say("  One repository's checks run side by side by the same rule, and each check's steps still take a place in the machine's heavy-run admission.");
            Say($"  Written to {path} — read at the next look, so nothing restarts.");
            return 0;
        });
    }

    public override void Listed(DriverConfig choices, DriverChoicesWorld world) => world.Instance.Out.WriteLine(choices.Checks is { } pinned
        ? $"  checks     {pinned} at once  (set — `daoris settings checks auto` runs as many as the memory admits)"
        : "  checks     auto  (as many at once as the memory admits, the default — `daoris settings checks <n>` pins a number)");
}

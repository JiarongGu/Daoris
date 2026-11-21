using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris settings vacancy &lt;hours&gt;</c>: how long a step nobody's session answers stands before Daoris gives it up
/// (<see cref="VacancySetting"/>), a press of <c>SET_VACANCY</c> like the rest.
/// </summary>
public sealed class VacancyVerb : ChoiceVerb
{
    public override string Word => VacancySetting.Key;

    public override ChoicePress Plan(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        var path = world.ConfigPath;
        var said = args.Skip(1).FirstOrDefault();
        var hours = int.TryParse(said, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var number) && VacancySetting.Takes(number)
            ? number
            : throw new ChoiceWords(VacancySetting.Refusal);
        return new(Word, ChoiceRoutes.Vacancy, new JsonObject { ["hours"] = hours }, _ =>
        {
            StandingChoices.Write(path, config => config.WithVacancy(hours));
            return Task.FromResult<object?>(null);
        }, _ => null, _ =>
        {
            world.Instance.Out.WriteLine($"daoris: a step nobody's session answers is given up {hours} hours after it became vacant, with what Daoris tried.");
            world.Instance.Out.WriteLine($"  Written to {path} — read at the next look, so nothing restarts.");
            return 0;
        });
    }

    public override void Listed(DriverConfig choices, DriverChoicesWorld world) => world.Instance.Out.WriteLine(
        $"  vacancy    {choices.VacancyHours ?? VacancySetting.Default} hours before Daoris gives up a step nobody's session answers  "
        + $"({(choices.VacancyHours is null ? "the default" : "set")} — `daoris settings vacancy <hours>` changes it)");
}

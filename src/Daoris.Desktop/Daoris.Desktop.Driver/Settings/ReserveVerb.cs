using System.Globalization;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris settings reserve &lt;gb&gt;|auto</c>: the memory the machine keeps free for the person's own work (<see cref="ReserveSetting"/>),
/// a press of <c>SET_RESERVE</c> like the rest; <c>auto</c> clears the pinned number.
/// </summary>
public sealed class ReserveVerb : ChoiceVerb
{
    public override string Word => ReserveSetting.Key;

    public override ChoicePress Plan(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        var path = world.ConfigPath;
        var said = args.Skip(1).FirstOrDefault();
        int? gb = said == ReserveSetting.Auto ? null
            : int.TryParse(said, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && ReserveSetting.Takes(number) ? number
            : throw new ChoiceWords(ReserveSetting.Refusal);
        return new(Word, ChoiceRoutes.Reserve, new JsonObject { ["gb"] = gb }, _ =>
        {
            StandingChoices.Write(path, config => config.WithReserve(gb));
            return Task.FromResult<object?>(null);
        }, _ => null, _ =>
        {
            world.Instance.Out.WriteLine(gb switch
            {
                0 => "daoris: this machine keeps no memory back for your own work: a heavy run, a check or a build waits only for its own need.",
                { } kept => $"daoris: a heavy run, a check or a build starts only while {kept} GB stays free for your own work after its own need.",
                _ => "daoris: a heavy run, a check or a build starts only while a third of this machine's memory stays free for your own work after its own need.",
            });
            world.Instance.Out.WriteLine($"  Written to {path} — read at the next look, so nothing restarts.");
            return 0;
        });
    }

    public override void Listed(DriverConfig choices, DriverChoicesWorld world)
    {
        var use = world.Use(DriverConfig.HomeOf(world.ConfigPath), choices.ReserveGb);
        var how = choices.ReserveGb is null ? "the default, a third of the machine's memory" : "set";
        world.Instance.Out.WriteLine($"  reserve    {MachineUse.Gb(use.Reserve)} GB kept free for your own work  ({how} — `daoris settings reserve <gb>|auto` changes it; 0 keeps none)");
        world.Instance.Out.WriteLine($"             now: {MachineUse.Gb(use.Total)} GB of memory, {MachineUse.Gb(use.FreeCommit)} GB free commit, Daoris's own processes hold {MachineUse.Gb(use.Daoris)} GB");
    }
}

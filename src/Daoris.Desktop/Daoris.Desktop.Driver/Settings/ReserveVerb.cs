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
                0 => "daoris: this machine's floor for your own work is none: a heavy run, a check or a build keeps free only what your own work is measured to take back.",
                { } kept => $"daoris: a heavy run, a check or a build starts only while at least {kept} GB stays free for your own work after its own need, more where your own work is measured to swing higher.",
                _ => "daoris: a heavy run, a check or a build starts only while what your own work is measured to take back stays free after its own need.",
            });
            world.Instance.Out.WriteLine($"  Written to {path} — read at the next look, so nothing restarts.");
            return 0;
        });
    }

    public override void Listed(DriverConfig choices, DriverChoicesWorld world)
    {
        var use = world.Use(DriverConfig.HomeOf(world.ConfigPath), choices.ReserveGb);
        var how = choices.ReserveGb is null ? "auto, measured from your own work" : $"a floor of {choices.ReserveGb} GB, more where your own work swings higher";
        world.Instance.Out.WriteLine($"  reserve    {MachineUse.Gb(use.Reserve)} GB kept free for your own work  ({how} — `daoris settings reserve <gb>|auto` sets the floor; 0 is none)");
        world.Instance.Out.WriteLine($"             now: {MachineUse.Gb(use.Total)} GB of memory, {MachineUse.Gb(use.FreeCommit)} GB free commit, Daoris's own processes hold {MachineUse.Gb(use.Daoris)} GB");
        if (use.Why is { } why) foreach (var line in Said(why)) world.Instance.Out.WriteLine(line);
    }

    /// <summary>Why the room is what it is, in two lines: what keeps memory free, then how many runs the machine admits and what a gate needs.</summary>
    internal static IEnumerable<string> Said(RoomWhy why)
    {
        yield return $"             why: your own work holds {MachineUse.Gb(why.Person)} GB and has swung {MachineUse.Gb(why.Swing)} GB above that in the last 10 minutes, plus a {MachineUse.Gb(why.Margin)} GB margin; kept by the {why.KeepBy}";
        var signal = why.Signal switch
        {
            RoomLimit.Memory => "backed off: free memory under what is kept",
            RoomLimit.Cpu => "backed off: the processors saturated",
            RoomLimit.Timeouts => "backed off: a run's tests timed out under load",
            RoomLimit.Clear => "grown back: the pressure cleared",
            _ => "no pressure seen",
        };
        var busy = why.Busy is { } value ? $", processors {Math.Round(value * 100)}% busy" : "";
        yield return $"             room: {why.Limit} of {why.Ceiling} heavy runs at once ({signal}{busy}); a gate needs {MachineUse.Gb(why.Need)} GB ({why.NeedSource}{(why.NeedRuns > 0 ? $", from {why.NeedRuns} runs" : "")})";
    }
}

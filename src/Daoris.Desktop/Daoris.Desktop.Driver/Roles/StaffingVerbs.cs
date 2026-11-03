using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris settings actor found</c> (FOUNDATION1w-s part 5, the design §19.15's "Its own verbs"): the found changes of the staffing's copy,
/// listed, taken or dismissed, on a home the copy still reaches (the register's <c>staffing-copy</c>). It acts alone: the staffing's lock
/// serializes it with the window's <i>Take it</i> and <i>Dismiss</i>.
/// </summary>
internal static class StaffingVerbs
{
    /// <summary>The verb's run: its exit, its words said on the terminal.</summary>
    // transitional: staffing-copy
    public static int Run(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        try
        {
            return Found(args, world);
        }
        catch (ChoiceWords refused)
        {
            world.Instance.Error.WriteLine($"{DriverChoicesCommand.Prefix}: {refused.Message}");
            return refused.Exit;
        }
        catch (DriverException refused)
        {
            world.Instance.Error.WriteLine($"{DriverChoicesCommand.Prefix}: {refused.Message}");
            return 2;
        }
    }

    /// <summary><c>actor found</c>, <c>found take &lt;n&gt;</c> and <c>found dismiss &lt;n&gt;</c>.</summary>
    private static int Found(IReadOnlyList<string> args, DriverChoicesWorld world)
    {
        var said = world.Instance.Out;
        var staffing = RoleStaffing.Of(world.ConfigPath);
        if (args is [_, _, "take" or "dismiss", var number] && long.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
        {
            if (Session(world) is { } refused) return refused;
            if (staffing is null) throw new ChoiceWords(Unkept(world));
            if (args[2] == "take")
            {
                staffing.Take(id);
                said.WriteLine($"daoris: #{id} taken: the roles are staffed as that file showed them, and driver.json says so again.");
            }
            else
            {
                staffing.Dismiss(id);
                said.WriteLine($"daoris: #{id} dismissed: the roles are staffed as they were, and driver.json keeps saying so.");
            }

            return 0;
        }

        if (args.Count > 2) throw new ChoiceWords("`settings actor found` takes nothing, or `take <n>` or `dismiss <n>`.");
        if (staffing is null)
        {
            said.WriteLine($"daoris: {Unkept(world)}");
            return 0;
        }

        var found = staffing.Found();
        if (found.Count == 0)
        {
            said.WriteLine("daoris: no change of driver.json's staffing waits on you.");
            return 0;
        }

        said.WriteLine("daoris: changes of driver.json's staffing another program made, each waiting on you:");
        foreach (var row in found)
        {
            said.WriteLine($"  #{row.Id}  found {row.At}");
            foreach (var change in row.Changes)
            {
                said.WriteLine($"    {change.Role}: {ActorPress.Said(change.Was)} → {ActorPress.Said(change.Now)}"
                    + (change.Applied ? " (a turn-off: applied at once)" : ""));
            }
        }

        said.WriteLine("  `daoris settings actor found take <n>` takes one as it stands; `dismiss <n>` leaves the roles as they are.");
        return 0;
    }

    private static string Unkept(DriverChoicesWorld world) =>
        $"{world.ConfigPath}'s staffing is the file's own here (not this home's store), so nothing is found to take or dismiss.";

    /// <summary>A change from a session's terminal, refused as every person's change is (D194 point 5); null where it may go on.</summary>
    private static int? Session(DriverChoicesWorld world)
    {
        if (world.Instance.Environment(DriverChoicesCommand.SessionVariable) is not { Length: > 0 }) return null;
        world.Instance.Error.WriteLine($"{DriverChoicesCommand.Prefix}: {DriverChoicesCommand.SessionsTerminal}");
        return 1;
    }
}

using System.Globalization;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris-driver driver actor export|import|found</c> (FOUNDATION1w-s part 5, the design §19.15's "Its own verbs"): the staffing's own
/// verbs, the seeded eight alone until row y-i. Each acts alone, as the workflows' writes do: the staffing's lock serializes it with the
/// window's presses, and the window reads what it did at its next read.
/// </summary>
internal static class StaffingVerbs
{
    /// <summary>The verb's run: its exit, its words said on the terminal.</summary>
    public static int Run(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        try
        {
            return args[1] switch
            {
                "export" => Export(args, before, world),
                "import" => Import(args, world),
                _ => Found(args, world),
            };
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

    /// <summary><c>actor export [&lt;file&gt;]</c>: every readable actor's order as D205's <c>actors</c>, to the file or the terminal.</summary>
    private static int Export(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        var text = JsonFile.Serialize(writer =>
        {
            writer.WriteStartObject();
            if (before.Actors.Count == 0)
            {
                writer.WriteStartObject("actors");
                writer.WriteEndObject();
            }
            else ActorChoices.Write(writer, before.Actors);

            writer.WriteEndObject();
        });
        foreach (var (actor, why) in before.ActorErrors) world.Instance.Error.WriteLine($"  {actor} is left out: driver.json's `actors.{actor}` {why}.");
        if (args.Count < 3)
        {
            world.Instance.Out.Write(text);
            return 0;
        }

        var file = Path.GetFullPath(Path.Combine(world.Folder, args[2]));
        AtomicFile.WriteText(file, text);
        world.Instance.Out.WriteLine($"daoris: the actors' orders written to {file}; `daoris driver actor import` reads them back.");
        return 0;
    }

    /// <summary>
    /// <c>actor import &lt;file&gt;</c>: a file of D205's <c>actors</c> read back as one write of the staffing, each actor it names set to its
    /// order and every other one as before, each judged by the actors' one judge; nothing written where any is refused.
    /// </summary>
    private static int Import(IReadOnlyList<string> args, DriverChoicesWorld world)
    {
        if (Session(world) is { } refused) return refused;
        if (args.Count < 3) throw new ChoiceWords("`driver actor import` needs <file>, one `daoris driver actor export` wrote.");
        var file = Path.GetFullPath(Path.Combine(world.Folder, args[2]));
        IReadOnlyList<(string Actor, IReadOnlyList<ActorChoice>? Choices)> changes;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("actors", out var named) || named.ValueKind != JsonValueKind.Object)
            {
                throw new ChoiceWords($"{file} holds no `actors` object; nothing was written.");
            }

            if (named.EnumerateObject().Select(each => each.Name).FirstOrDefault(name => !ActorChoices.Ids.Contains(name, StringComparer.Ordinal))
                is { } unknown)
            {
                throw new ChoiceWords($"`{unknown}` is not an actor — one of {string.Join(", ", ActorChoices.Ids)}; nothing was written.");
            }

            var (actors, errors) = ActorChoices.Read(document.RootElement);
            if (errors.FirstOrDefault() is { Key: { } actor } error) throw new ChoiceWords($"{file}'s `actors.{actor}`: {error.Value}; nothing was written.");
            changes = [.. ActorChoices.Ids.Select(id => (id, actors.TryGetValue(id, out var choices) ? choices : null))];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new ChoiceWords($"{file} could not be read: {error.Message}");
        }

        RoleStaffing.SetAt(world.ConfigPath, changes);
        world.Instance.Out.WriteLine($"daoris: the actors' orders imported from {file}: each actor it names as it names it, every other as before.");
        return 0;
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
                said.WriteLine($"daoris: #{id} taken: the actors are as that file showed them, and driver.json says so again.");
            }
            else
            {
                staffing.Dismiss(id);
                said.WriteLine($"daoris: #{id} dismissed: the actors are as they were, and driver.json keeps saying so.");
            }

            return 0;
        }

        if (args.Count > 2) throw new ChoiceWords("`driver actor found` takes nothing, or `take <n>` or `dismiss <n>`.");
        if (staffing is null)
        {
            said.WriteLine($"daoris: {Unkept(world)}");
            return 0;
        }

        var found = staffing.Found();
        if (found.Count == 0)
        {
            said.WriteLine("daoris: no change of driver.json's actors waits on you.");
            return 0;
        }

        said.WriteLine("daoris: changes of driver.json's actors another program made, each waiting on you:");
        foreach (var row in found)
        {
            said.WriteLine($"  #{row.Id}  found {row.At}");
            foreach (var change in row.Changes)
            {
                said.WriteLine($"    {change.Role}: {ActorPress.Said(change.Was)} → {ActorPress.Said(change.Now)}"
                    + (change.Applied ? " (a turn-off: applied at once)" : ""));
            }
        }

        said.WriteLine("  `daoris driver actor found take <n>` takes one as it stands; `dismiss <n>` leaves the actors as they are.");
        return 0;
    }

    private static string Unkept(DriverChoicesWorld world) =>
        $"{world.ConfigPath}'s actors are the file's own here (not this home's store), so nothing is found to take or dismiss.";

    /// <summary>A change from a session's terminal, refused as every person's change is (D194 point 5); null where it may go on.</summary>
    private static int? Session(DriverChoicesWorld world)
    {
        if (world.Instance.Environment(DriverChoicesCommand.SessionVariable) is not { Length: > 0 }) return null;
        world.Instance.Error.WriteLine($"{DriverChoicesCommand.Prefix}: {DriverChoicesCommand.SessionsTerminal}");
        return 1;
    }
}

using System.Text.Json;
using System.Text.Json.Nodes;
using static Daoris.Driver.ChoiceArgs;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris-driver driver keep</c> (KEEP1d, D195 point 2, KEEP1f; FOUNDATION1 mod-iii): the workplace's own verb, the settings' port
/// (<see cref="ChoiceVerb"/>) implemented beside the rules it sets (<see cref="KeepRules"/>), the free-space floor among them, whose volume it reads.
/// </summary>
public sealed class KeepVerb : ChoiceVerb
{
    public override string Word => "keep";

    /// <summary>
    /// The person's keep rules (KEEP1d, D195 point 2; the cleanup design §2.2): one kind's days, `whole` or `slim`, `never`, or its
    /// default again. Judged here by the one judgement the window's SET_KEEP makes, so a value out of its bounds is refused before
    /// the window is asked, in the line both paths say (`… (KEEP_OUT_OF_RANGE)`).
    /// </summary>
    public override ChoicePress Plan(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        var verb = Word;
        var path = world.ConfigPath;
        void Say(string line) => world.Instance.Out.WriteLine(line);
        ChoicePress Change(InstanceRoute? route, JsonObject? payload, Func<DriverConfig, DriverConfig> change, Func<int> words) =>
            new(verb, route, payload, _ =>
            {
                StandingChoices.Write(path, change);
                return Task.FromResult<object?>(null);
            }, _ => null, _ => words());

        var set = KeepAsked(args, world);
        DriverConfig after;
        try
        {
            after = KeepRules.Apply(before, set);
        }
        catch (DriverException refused)
        {
            throw new ChoiceWords(refused is ChoiceRefusal ? $"{refused.Message} ({ChoiceCodes.Of(refused)})" : refused.Message);
        }

        var payload = new JsonObject { ["kind"] = set.Kind };
        if (set.Days is { } days) payload["days"] = days;
        if (set.Never) payload["never"] = true;
        if (set.Whole is { } whole) payload["whole"] = whole;
        if (set.Default) payload["default"] = true;
        // KEEP1f: the floor's size, share or `off`; the window judges a size against its own read of the volume.
        if (set.Gb is { } gb) payload["gb"] = gb;
        if (set.Percent is { } percent) payload["percent"] = percent;
        if (set.Off) payload["off"] = true;
        return Change(ChoiceRoutes.Keep, payload, config => KeepRules.Apply(config, set), () =>
        {
            Say($"daoris: {KeepRules.Said(set.Kind, after.Keep)}.");
            Say($"  Written to {path} — the look's next pass keeps by it, so nothing restarts.");
            return 0;
        });
    }

    /// <summary><c>keep</c>: each kind's rule the person sets, and whether it is the default (KEEP1d).</summary>
    public override int? Said(IReadOnlyList<string> args, DriverConfig choices, DriverChoicesWorld world)
    {
        var write = world.Instance.Out;
        write.WriteLine($"daoris: {world.ConfigPath}");
        foreach (var kind in KeepRules.Settable)
        {
            var said = KeepRules.Said(kind.Id, choices.Keep);
            write.WriteLine(choices.Keep.IsDefault(kind.Id)
                ? $"  {kind.Id,-10} {said}  (the default — `daoris driver keep {kind.Id} …` changes it)"
                : $"  {kind.Id,-10} {said}  (set — `daoris driver keep {kind.Id} default` takes the default again)");
        }

        // KEEP1f: the free-space floor, set beside the kinds' rules.
        var floor = KeepRules.Said(KeepRules.Floor, choices.Keep);
        write.WriteLine(choices.Keep.IsDefault(KeepRules.Floor)
            ? $"  {KeepRules.Floor,-10} {floor}  (the default — `daoris driver keep floor …` changes it)"
            : $"  {KeepRules.Floor,-10} {floor}  (set — `daoris driver keep floor default` takes the default again)");
        return 0;
    }

    /// <summary><c>keep &lt;kind&gt; &lt;value&gt;</c>'s words read as the rule they set (KEEP1d); anything else is its usage.</summary>
    private static KeepSet KeepAsked(IReadOnlyList<string> args, DriverChoicesWorld world)
    {
        const string Usage = "`driver keep` needs a kind and its rule — `set-aside <days>|whole|slim|default`, `downloads <days>|never|default`, "
            + "`logs <days>|default`, `history <days>|never|default` or `floor <n>GB|<n>%|off|default`; e.g. `daoris driver keep set-aside 30`.";
        if (args.Count != 3) throw new ChoiceWords(Usage);
        try
        {
            if (!KeepRules.IsFloor(args[1])) KeepRules.Kind(args[1]);
        }
        catch (DriverException refused)
        {
            throw new ChoiceWords($"{refused.Message} {Usage}");
        }

        try
        {
            var set = KeepRules.Asked(args[1], args[2]);
            if (!KeepRules.IsFloor(set.Kind) || set.Gb is null) return set;
            // KEEP1f: a size is at most half the Daoris folder's volume, read here so the refusal comes before the window is asked.
            var volume = world.FloorVolume is { } read
                ? read()
                : world.Instance.Home is { } home ? SpaceVolumes.Read(DaorisHome.FolderOf(home).Folder)?.Total : null;
            return set with { Volume = volume };
        }
        catch (DriverException refused)
        {
            throw new ChoiceWords(refused.Message);
        }
    }
}

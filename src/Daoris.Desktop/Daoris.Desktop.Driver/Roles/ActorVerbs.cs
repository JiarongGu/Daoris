using System.Text.Json.Nodes;
using static Daoris.Driver.ChoiceArgs;

namespace Daoris.Driver;

/// <summary>
/// What the actors' verbs share (MODELROLE1c, D205 §9.4; FOUNDATION1 mod-iii): the press that sets an actor's order, its words, and
/// whether this machine manages a toolchain for an agent's name.
/// </summary>
internal static class ActorPress
{
    /// <summary>
    /// The press that sets <paramref name="actor"/>'s order, judged here by the judge <c>SET_ACTOR</c> takes, so a refused order is said
    /// before anything is sent; its words name the order, what it replaced, and the actors a change of Build's agent made explicit.
    /// </summary>
    public static ChoicePress Change(
        string verb, string actor, IReadOnlyList<ActorChoice>? choices, DriverConfig before, DriverChoicesWorld world, Action<Action> words)
    {
        if (ActorChoices.Judge(actor, choices) is { } refused) throw new ChoiceWords($"`driver {verb}`: {refused}");
        var replaced = ActorView.Of(actor, before);
        ActorChoices.With(before, actor, choices, out var kept);
        var path = world.ConfigPath;
        var write = world.Instance.Out;
        var payload = new JsonObject
        {
            ["actor"] = actor,
            ["choices"] = choices is null ? null : new JsonArray([.. choices.Select(choice => (JsonNode)new JsonObject
            {
                ["agent"] = choice.Agent, ["model"] = choice.Model, ["effort"] = choice.Effort,
            })]),
        };
        return new(verb, ChoiceRoutes.Actor, payload, _ =>
        {
            StandingChoices.Write(path, config => ActorChoices.With(config, actor, choices));
            return Task.FromResult<object?>(null);
        }, _ => null, _ =>
        {
            words(() =>
            {
                var after = choices is null ? "as before" : choices.Count == 0 ? "off" : Order(choices.Select((choice, i) => (choice, i + 1)));
                write.WriteLine($"  {actor}: {after}; it was {Said(replaced)}.");
                if (kept.Count > 0)
                {
                    write.WriteLine($"  {string.Join(" and ", kept.Select(each => each == "chat" ? "Chat" : "Brainstorm"))} keep `{before.Adapter}`, written for them: a change of Build's agent no "
                        + "longer moves them.");
                }

                write.WriteLine($"  Written to {path} — the driver re-reads it every tick, so nothing restarts.");
            });
            return 0;
        });
    }


    /// <summary>One actor's order in the terminal's words.</summary>
    public static string Said(ActorView view) => view.State switch
    {
        "unreadable" => $"unreadable: driver.json's `actors.{view.Id}` {view.Error}",
        "off" => "off",
        _ when view.Choices.Count == 0 => view.Id == "helper"
            ? "as before: every agent of another maker than the asker's, at account default"
            : "as before: the agents a rule or an ask names, at account default",
        _ => string.Join(", ", view.Choices.Select((entry, i) => Entry(i + 1, entry.Agent, entry.Model, entry.Effort)))
             + (view.State == "before" ? " (as before)" : ""),
    };

    private static string Order(IEnumerable<(ActorChoice Choice, int At)> entries) =>
        string.Join(", ", entries.Select(each => Entry(each.At, each.Choice.Agent, each.Choice.Model, each.Choice.Effort)));

    private static string Entry(int at, string agent, string? model, string? effort) =>
        $"{at} {agent} · {model ?? "account default"} · {effort ?? "account default"}";

    /// <summary>Whether this machine's Daoris manages a toolchain for an agent's name: the CLI's <c>TOOLCHAINS</c>, the gate stubs aside.</summary>
    public static bool Managed(string name) =>
        AdapterSet.Built().Reserved.Contains(name, StringComparer.Ordinal) && name is not ("stub" or "acp-stub");
}

/// <summary>
/// <c>daoris-driver driver adapter</c> (MODELROLE1c, D205 §6.4; FOUNDATION1 mod-iii): the roles' own verb, Build's order at account default,
/// the settings' port (<see cref="ChoiceVerb"/>) implemented beside the actors' judge (<see cref="ActorChoices"/>).
/// </summary>
public sealed class AdapterVerb : ChoiceVerb
{
    public override string Word => "adapter";

    public override ChoicePress Plan(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        var verb = Word;
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var adapter = Named(args, verb);
        // Not refused against a list: the driver errors naming what exists at its start (D23), the side that knows. Build's
        // one-choice order at account default, through SET_ACTOR (MODELROLE1c, D205 §6.4): Chat and Brainstorm stay where they were.
        return ActorPress.Change(verb, "build", [new ActorChoice(adapter)], before, world, said =>
        {
            Say($"daoris: sessions on this machine spawn via `{adapter}`.");
            if (!ActorPress.Managed(adapter)) Say("  Daoris manages no toolchain for that name — `daoris agent list` shows the ones it does.");
            said();
        });
    }
}

/// <summary>
/// <c>daoris-driver driver helper</c> (MODELROLE1d, D205 §6.4; FOUNDATION1 mod-iii): the roles' own verb, Ask Daoris's one-choice order,
/// the settings' port (<see cref="ChoiceVerb"/>) implemented beside the actors' judge (<see cref="ActorChoices"/>).
/// </summary>
public sealed class HelperVerb : ChoiceVerb
{
    public override string Word => "helper";

    public override ChoicePress Plan(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        var verb = Word;
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var agent = args.Skip(1).FirstOrDefault(token => !token.StartsWith("--", StringComparison.Ordinal))
            ?? throw new ChoiceWords("`driver helper` needs <adapter>|off — e.g. `daoris driver helper claude-code-acp`.");
        var off = agent == "off";
        // Ask Daoris's one-choice order at account default, or off, through SET_ACTOR (MODELROLE1d, D205 §6.4).
        return ActorPress.Change(verb, "ask-daoris", off ? [] : [new ActorChoice(agent)], before, world, said =>
        {
            Say(off
                ? "daoris: Ask Daoris runs no agent here — it offers its starters, each a door to the screen that fixes it."
                : $"daoris: Ask Daoris runs on `{agent}`. It reads, and proposes; every change is yours to apply (D89).");
            if (!off && !ActorPress.Managed(agent)) Say("  Daoris manages no toolchain for that name — `daoris agent list` shows the ones it does.");
            said();
        });
    }

    /// <summary>The lines this verb's key adds to <c>list</c>.</summary>
    public override void Listed(DriverConfig choices, DriverChoicesWorld world)
    {
        void Say(string line) => world.Instance.Out.WriteLine(line);
        Say(choices.HelperAdapter is { } helper
            ? $"  helper     {helper}  ({(choices.HelpMode == HelpModeNames.Full
                ? "Ask Daoris, full access: it acts on this machine itself, with no asking"
                : "Ask Daoris, restricted: it proposes, and you apply")} — `daoris-driver help mode` says and sets it)"
            : "  helper     off — Ask Daoris offers its starters; `daoris driver helper <adapter>` names an agent");
    }
}

/// <summary>
/// <c>daoris-driver driver intake</c> (MODELROLE1d, D205 §6.4; FOUNDATION1 mod-iii): the roles' own verb, Intake's one-choice order,
/// the settings' port (<see cref="ChoiceVerb"/>) implemented beside the actors' judge (<see cref="ActorChoices"/>).
/// </summary>
public sealed class IntakeVerb : ChoiceVerb
{
    public override string Word => "intake";

    public override ChoicePress Plan(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        var verb = Word;
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var named = args.Skip(1).FirstOrDefault(token => !token.StartsWith("--", StringComparison.Ordinal))
            ?? throw new ChoiceWords("`driver intake` needs <adapter>|off — e.g. `daoris driver intake claude-code-acp`, or `off` "
                + "to answer asks by declarations only.");
        var off = named == "off";
        // Intake's one-choice order at account default, or off, through SET_ACTOR (MODELROLE1d, D205 §6.4).
        return ActorPress.Change(verb, "intake", off ? [] : [new ActorChoice(named)], before, world, said =>
        {
            if (off)
            {
                Say("daoris: asks on this machine are answered by declarations only — proposed, never published unasked.");
                Say("  An intake already waiting on you still waits; answering its ask ends it.");
            }
            else
            {
                Say($"daoris: an ask the declarations do not settle opens an intake session via `{named}`.");
                Say("  It reads the workspace's declarations, publishes the quests itself, and asks you where they do not");
                Say("  settle it. 🔴 Every intake spends a real login on that harness's account.");
                if (!ActorPress.Managed(named)) Say("  Daoris manages no toolchain for that name — `daoris agent list` shows the ones it does.");
            }

            said();
        });
    }

    /// <summary>The lines this verb's key adds to <c>list</c>.</summary>
    public override void Listed(DriverConfig choices, DriverChoicesWorld world)
    {
        void Say(string line) => world.Instance.Out.WriteLine(line);
        Say(choices.IntakeAdapter is { } intake
            ? $"  intake     {intake}  (an ask the declarations do not settle opens a session — a login each)"
            : "  intake     off — asks are answered by declarations only; `daoris driver intake <adapter>` names a harness");
    }
}

/// <summary>
/// <c>daoris-driver driver actor</c> (MODELROLE1c, D205 §9.4; FOUNDATION1 mod-iii): the roles' own verb, an actor's order set from its quoted
/// entries or said, the settings' port (<see cref="ChoiceVerb"/>) implemented beside the actors' judge (<see cref="ActorChoices"/>).
/// </summary>
public sealed class ActorVerb : ChoiceVerb
{
    public override string Word => "actor";

    /// <summary>
    /// <c>actor &lt;actor&gt; …</c> (MODELROLE1c, D205 §9.4): an actor's order set from its quoted entries, <c>"agent [model|-] [effort|-]"</c>
    /// (<c>-</c> is account default), <c>off</c> for Intake and Ask Daoris, or <c>--clear</c> back to as before; judged by the one judge.
    /// </summary>
    public override ChoicePress Plan(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        var actor = args[1];
        if (!ActorChoices.Ids.Contains(actor, StringComparer.Ordinal))
        {
            throw new ChoiceWords($"`{actor}` is not an actor — one of {string.Join(", ", ActorChoices.Ids)}.");
        }

        var rest = args.Skip(2).ToList();
        IReadOnlyList<ActorChoice>? choices;
        if (rest is ["--clear"]) choices = null;
        else if (rest is ["off"]) choices = [];
        else
        {
            if (rest.Any(word => word.StartsWith("--", StringComparison.Ordinal)))
            {
                throw new ChoiceWords($"`driver actor {actor}` takes its choices, `off` or `--clear` alone — e.g. "
                    + $"`daoris driver actor {actor} \"claude-code opus high\" \"claude-code sonnet -\"`.");
            }

            choices = [.. rest.Select(Entry)];
        }

        return ActorPress.Change("actor", actor, choices, before, world, said =>
        {
            world.Instance.Out.WriteLine(choices is null ? $"daoris: {actor} is as before again: today's agent settings, at account default."
                : choices.Count == 0 ? $"daoris: {actor} is off."
                : $"daoris: {actor} runs the first of its choices that can run, in order.");
            said();
        });

        static ActorChoice Entry(string quoted)
        {
            var words = quoted.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (words.Length is 0 or > 3)
            {
                throw new ChoiceWords($"`{quoted}` is not a choice: an agent, then its model or `-`, then its effort or `-`, in one quoted word.");
            }

            string? Value(int at) => words.Length > at && words[at] != "-" ? words[at] : null;
            return new ActorChoice(words[0], Value(1), Value(2));
        }
    }

    /// <summary><c>actor</c> naming one actor or none: each order, where it came from, said.</summary>
    public override int? Said(IReadOnlyList<string> args, DriverConfig choices, DriverChoicesWorld world)
    {
        var said = world.Instance.Out;
        if (args.Count > 1 && !ActorChoices.Ids.Contains(args[1], StringComparer.Ordinal))
        {
            throw new ChoiceWords($"`{args[1]}` is not an actor — one of {string.Join(", ", ActorChoices.Ids)}.");
        }

        said.WriteLine($"daoris: {world.ConfigPath}");
        foreach (var view in ActorView.All(choices).Where(view => args.Count < 2 || view.Id == args[1]))
        {
            said.WriteLine($"  {view.Id,-11}{ActorPress.Said(view)}");
        }

        said.WriteLine("  Each runs the first of its choices that can run, in order. Set one: `daoris driver actor <actor> \"<agent> "
            + "[<model>|-] [<effort>|-]\" …`; `--clear` goes back to as before.");
        return 0;
    }
}

using System.Text.Json;
using System.Text.Json.Nodes;
using static Daoris.Driver.ChoiceArgs;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris settings risk [&lt;workspace&gt;|machine] [--repository &lt;name&gt;] [low|medium|high] [--mark &lt;marks&gt;] [--unmark &lt;marks&gt;]</c>:
/// the terminal twin of a place's risk and the owner's marks (<see cref="PlaceRiskBook"/>), and its read form. The owner's record: a press that
/// proves a session's key goes on the owner's card in every mode, a process in an agents' job is refused by the pipe, and with no Daoris
/// running only the owner's own terminal writes the file.
/// </summary>
public sealed class PlaceRiskVerb : ChoiceVerb
{
    private const string Usage = "usage: daoris settings risk [<workspace>|machine] [--repository <name>] [low|medium|high] [--mark <marks>] [--unmark <marks>] — "
        + "e.g. `daoris settings risk aurora low --mark release.production`; marks are comma-separated; with nothing to set it says what stands "
        + "and each act's class there; `daoris settings risk why <run> [<step>]` says the verdicts journaled for a run's holds.";

    /// <summary>The refusal of a file write from where an agent is: the record is the owner's, and an agent's press goes on the owner's card.</summary>
    internal const string Alone =
        "a place's risk and marks are the owner's: with Daoris running, an agent's press goes on the owner's card; with none running, only the owner's own terminal writes them. Nothing was written.";

    private static readonly string[] Valued = ["--repository", "--mark", "--unmark"];

    public override string Word => "risk";

    /// <summary>Whether the words set anything: a risk, a mark or an unmark.</summary>
    public static bool Changes(IReadOnlyList<string> args) =>
        args.Any(arg => arg is "--mark" or "--unmark") || Operands(args, Valued).Skip(1).Any(PlaceRisks.Is)
        || Operands(args, SetValued).Skip(1).FirstOrDefault() is Set or Apply;

    public override int? Said(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        if (Operands(args, Valued).Skip(1).FirstOrDefault() == Why) return SaidWhy(args, world);
        var edit = Read(args);
        var now = before.PlaceRisk.At(edit.Workspace, edit.Repository);
        world.Instance.Out.WriteLine($"daoris: {Named(edit)}: {now.Risk} ({now.Source}){Marks(now.Marks)}.");
        if (!before.PlaceRisk.Written) world.Instance.Out.WriteLine("  No record is written yet: Daoris writes it at its next start.");
        foreach (var line in Acts?.Invoke(before, DriverConfig.HomeOf(world.ConfigPath), world.Now(), edit.Workspace, edit.Repository) ?? []) world.Instance.Out.WriteLine(line);
        return 0;
    }

    /// <summary>The risk judge's table at a place and its journal's lines for a run: Workflows' readings, which Loop hands this verb.</summary>
    public Func<DriverConfig, string, DateTimeOffset, string?, string?, IReadOnlyList<string>>? Acts { get; init; }

    public Func<string, string, string?, IReadOnlyList<string>>? Verdicts { get; init; }

    private const string Why = "why";
    private const string Set = "set";
    private const string Apply = "apply";

    private const string SetUsage = "usage: daoris settings risk set <place> <act> none|role|owner|remove [--by <role>] [--mode full|restricted] [--conversation <session>], or <place> cast <role> <holder>|default, or <place> access full|clear|<pause>=<value>… — "
        + "<place> is machine, workspace/<name> or repository/<name>; <act> is a kind of the census (policy.change is fixed at the owner); the owner's own terminal sets it at once on the revision it read, an agent's set is a proposal that changes nothing; "
        + "daoris settings risk apply <proposal> <digest> applies a proposal on the owner's own turn naming both.";

    private static readonly string[] SetValued = ["--by", "--mode", "--conversation"];

    private int SaidWhy(IReadOnlyList<string> args, DriverChoicesWorld world)
    {
        var words = Operands(args, Valued).Skip(2).ToList();
        if (words.Count is 0 or > 2) throw new ChoiceWords($"`settings risk why` needs a run, and may name one step — {Usage}");
        if (Verdicts is not { } verdicts) throw new ChoiceWords("this program was handed no reading of the risk verdicts; nothing was read.");
        foreach (var line in verdicts(DriverConfig.HomeOf(world.ConfigPath), words[0], words.ElementAtOrDefault(1))) world.Instance.Out.WriteLine(line);
        return 0;
    }

    public override ChoicePress Plan(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        switch (Operands(args, SetValued).Skip(1).FirstOrDefault())
        {
            case Set: return Setting(args, before, world);
            case Apply: return Applying(args, world);
        }

        var path = world.ConfigPath;
        var edit = Read(args);
        var agent = world.Instance.Environment(DriverChoicesCommand.SessionVariable) is { Length: > 0 } || world.InAgentJob() == true;
        var payload = new JsonObject();
        if (edit.Workspace is not null) payload["workspace"] = edit.Workspace;
        if (edit.Repository is not null) payload["repository"] = edit.Repository;
        if (edit.Risk is not null) payload["risk"] = edit.Risk;
        if (edit.Mark.Count > 0) payload["mark"] = new JsonArray([.. edit.Mark.Select(mark => (JsonNode)mark)]);
        if (edit.Unmark.Count > 0) payload["unmark"] = new JsonArray([.. edit.Unmark.Select(mark => (JsonNode)mark)]);
        PlaceRiskBook after;
        try
        {
            after = Judged(() => before with { PlaceRisk = before.PlaceRisk.Apply(edit) }).PlaceRisk;
        }
        catch (ChoiceWords refused)
        {
            throw new ChoiceWords($"{refused.Message} — {Usage}", refused.Exit);
        }

        return new("risk", ChoiceRoutes.PlaceRisk, payload, _ =>
        {
            if (agent) throw new ChoiceWords(Alone, 1);
            StandingChoices.Write(path, config => config with { PlaceRisk = config.PlaceRisk.Apply(edit) });
            return Task.FromResult<object?>(null);
        }, _ => null, _ =>
        {
            var now = after.At(edit.Workspace, edit.Repository);
            world.Instance.Out.WriteLine($"daoris: {Named(edit)}: {now.Risk}{Marks(now.Marks)}.");
            world.Instance.Out.WriteLine($"  Written to {path}.");
            return 0;
        });
    }

    /// <summary><c>set</c>: an agent's is a proposal the owner confirms; the owner's own terminal writes at once on the revision it read.</summary>
    private static ChoicePress Setting(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        Flags(args, SetValued, [], "settings risk set", SetUsage);
        var edit = EditOf(args);
        var conversation = FlagValue(args, "--conversation") ?? world.Instance.Environment(DriverChoicesCommand.SessionVariable);
        var agent = conversation is { Length: > 0 } || world.InAgentJob() == true;
        var payload = new JsonObject { ["edits"] = new JsonArray(Wire(edit)) };
        if (agent)
        {
            if (conversation is { Length: > 0 }) payload["conversation"] = conversation;
            return Pressed("risk set", ChoiceRoutes.PolicyPropose, payload, world, _ => throw new ChoiceWords(AgentSet, 1));
        }

        var revision = RiskPolicy.Of(before).Revision;
        payload["revision"] = revision;
        var home = DriverConfig.HomeOf(world.ConfigPath);
        return Pressed("risk set", ChoiceRoutes.PolicySet, payload, world, _ =>
        {
            var outcome = new PolicyChanges(home, world.Now, new OwnerTurns(_ => [])).Set(PolicyChannel.OwnerPress(null), PolicyChangeLine.Cli, [edit], revision);
            return Task.FromResult<object?>(JsonSerializer.SerializeToElement(new { done = outcome.Done, code = outcome.Code, message = outcome.Said }));
        });
    }

    private const string AgentSet = "an agent's set of the risk policy is a proposal the owner confirms in Ask Daoris's conversation, so it needs Daoris running; nothing was changed.";

    private static PolicyEdit EditOf(IReadOnlyList<string> args)
    {
        var words = Operands(args, SetValued).Skip(2).ToList();
        if (words.Count > 0 && !PlaceKeyed(words[0])) throw new ChoiceWords($"`{words[0]}` is no place (machine, workspace/<name> or repository/<name>) — {SetUsage}");
        if (words.Count >= 3 && words[1] is "cast" or "access")
        {
            var cast = words[1] == "cast";
            if (cast ? words.Count != 4 : words.Count < 3) throw new ChoiceWords($"`settings risk set` takes a place, then cast <role> <holder> or access <setting> — {SetUsage}");
            return cast ? PolicyEdit.Cast(words[0], words[2], words[3] == "default" ? null : words[3]) : PolicyEdit.Access(words[0], string.Join(';', words.Skip(2)));
        }

        if (words.Count != 3) throw new ChoiceWords($"`settings risk set` takes a place, an act and a class — {SetUsage}");
        var riskClass = words[2] == "remove" ? null : words[2];
        if (riskClass is not null && !RiskClasses.Is(riskClass)) throw new ChoiceWords($"`{riskClass}` is no class (none, role, owner or remove) — {SetUsage}");
        var (place, act, by, mode) = (words[0], words[1], FlagValue(args, "--by"), FlagValue(args, "--mode"));
        try
        {
            if (RiskAct.Of(act).Kind == RiskActs.PolicyChange) throw new ChoiceWords($"policy.change is fixed at the owner: no row changes it — {SetUsage}");
        }
        catch (ArgumentException)
        {
            throw new ChoiceWords($"`{act}` names no act kind of the census — {SetUsage}");
        }

        if (by is not null && riskClass != RiskClasses.Role) throw new ChoiceWords($"--by names the role of a role row — {SetUsage}");
        if (mode is not (null or "full" or "restricted")) throw new ChoiceWords($"--mode is full or restricted — {SetUsage}");
        return new PolicyEdit(place, act, riskClass, by, mode);
    }

    private static bool PlaceKeyed(string place) => RiskPolicy.IsPlace(place);

    private static JsonObject Wire(PolicyEdit edit)
    {
        var wire = new JsonObject { ["place"] = edit.Place, ["act"] = edit.Act, ["class"] = edit.Class, ["by"] = edit.By, ["mode"] = edit.Mode };
        if (edit.Subject != PolicyEdit.RowSubject) wire["subject"] = edit.Subject;
        return wire;
    }

    private static ChoicePress Applying(IReadOnlyList<string> args, DriverChoicesWorld world)
    {
        var words = Operands(args, SetValued).Skip(2).ToList();
        if (words.Count != 2) throw new ChoiceWords($"`settings risk apply` takes a proposal and its digest — {SetUsage}");
        return Pressed("risk apply", ChoiceRoutes.PolicyApply, new JsonObject { ["proposal"] = words[0], ["digest"] = words[1] }, world,
            _ => throw new ChoiceWords("a proposed change to the risk policy is confirmed in the conversation that proposed it, so it needs Daoris running; nothing was changed.", 1));
    }

    private static ChoicePress Pressed(string verb, InstanceRoute route, JsonObject payload, DriverChoicesWorld world, Func<CancellationToken, Task<object?>> alone) => new(
        verb, route, payload, alone, answer => answer.Clone(),
        fields =>
        {
            var said = (JsonElement)fields!;
            var done = said.TryGetProperty("done", out var ok) && ok.ValueKind == JsonValueKind.True;
            var message = said.TryGetProperty("message", out var text) && text.ValueKind == JsonValueKind.String ? text.GetString() : "";
            world.Instance.Out.WriteLine(done ? message : $"daoris: {message}");
            return done ? 0 : 1;
        });

    public override void Listed(DriverConfig choices, DriverChoicesWorld world)
    {
        foreach (var (place, risk) in choices.PlaceRisk.Places.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            world.Instance.Out.WriteLine($"  risk       {place}  {risk.Risk}{Marks(risk.Marks)}");
        }
    }

    private static PlaceRiskEdit Read(IReadOnlyList<string> args)
    {
        try
        {
            Flags(args, Valued, [], "settings risk", Usage);
            var operands = Operands(args, Valued).Skip(1).ToList();
            var risk = operands.FirstOrDefault(PlaceRisks.Is);
            var place = operands.Where(each => !PlaceRisks.Is(each)).ToList();
            if (place.Count > 1) throw new ChoiceWords("`settings risk` takes one place and one risk.");
            var workspace = place.Count == 1 && place[0] != PlaceRiskBook.MachineKey ? place[0] : null;
            var repository = FlagValue(args, "--repository");
            if (repository is not null && workspace is null) throw new ChoiceWords("`settings risk --repository` needs the repository's workspace.");
            return new PlaceRiskEdit { Workspace = workspace, Repository = repository, Risk = risk, Mark = List(FlagValue(args, "--mark")), Unmark = List(FlagValue(args, "--unmark")) };
        }
        catch (ChoiceWords refused) when (!refused.Message.EndsWith(Usage, StringComparison.Ordinal))
        {
            throw new ChoiceWords($"{refused.Message} — {Usage}", refused.Exit);
        }
    }

    private static IReadOnlyList<string> List(string? value) =>
        value is null ? [] : [.. value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    private static string Named(PlaceRiskEdit edit) =>
        edit.Repository is { } repository ? $"`{repository}`" : edit.Workspace is { } workspace ? $"the workspace `{workspace}`" : "this machine";

    private static string Marks(IReadOnlyList<string> marks) => marks.Count == 0 ? "" : $", marked {string.Join(", ", marks.Select(mark => $"`{mark}`"))}";

    /// <summary>The payload as the route reads it, for a test's look at what the verb presses.</summary>
    internal static PlaceRiskEdit EditOf(JsonObject payload) => PlaceRiskBook.EditOf(JsonSerializer.SerializeToElement(payload));
}

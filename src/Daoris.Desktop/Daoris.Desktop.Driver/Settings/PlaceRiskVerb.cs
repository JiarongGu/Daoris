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
        || Operands(args, ProposeValued).Skip(1).FirstOrDefault() is Propose or Apply;

    public override int? Said(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        if (Operands(args, Valued).Skip(1).FirstOrDefault() == Why) return SaidWhy(args, world);
        var edit = Read(args);
        var now = before.PlaceRisk.At(edit.Workspace, edit.Repository);
        world.Instance.Out.WriteLine($"daoris: {Named(edit)}: {now.Risk} ({now.Source}){Marks(now.Marks)}.");
        if (!before.PlaceRisk.Written) world.Instance.Out.WriteLine("  No record is written yet: Daoris writes it at its next start.");
        foreach (var line in Acts?.Invoke(before, edit.Workspace, edit.Repository) ?? []) world.Instance.Out.WriteLine(line);
        return 0;
    }

    /// <summary>The risk judge's table at a place and its journal's lines for a run: Workflows' readings, which Loop hands this verb.</summary>
    public Func<DriverConfig, string?, string?, IReadOnlyList<string>>? Acts { get; init; }

    public Func<string, string, string?, IReadOnlyList<string>>? Verdicts { get; init; }

    private const string Why = "why";
    private const string Propose = "propose";
    private const string Apply = "apply";

    private const string ProposeUsage = "usage: daoris settings risk propose <place> <act> none|role|owner|remove [--by <role>] [--mode full|restricted] [--conversation <session>], or <place> cast <role> <holder>|default, or <place> access full|clear|<pause>=<value>… — "
        + "<place> is machine, workspace/<name> or repository/<name>; <act> is a kind of the census (policy.change is fixed at the owner); it proposes and changes nothing; "
        + "daoris settings risk apply <proposal> <digest> applies it on the owner's own turn naming both.";

    private static readonly string[] ProposeValued = ["--by", "--mode", "--conversation"];

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
        switch (Operands(args, ProposeValued).Skip(1).FirstOrDefault())
        {
            case Propose: return Proposing(args, world);
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

    private static ChoicePress Proposing(IReadOnlyList<string> args, DriverChoicesWorld world)
    {
        Flags(args, ProposeValued, [], "settings risk propose", ProposeUsage);
        var words = Operands(args, ProposeValued).Skip(2).ToList();
        if (words.Count >= 3 && words[1] is "cast" or "access")
        {
            var cast = words[1] == "cast";
            if (cast ? words.Count != 4 : words.Count < 3) throw new ChoiceWords($"`settings risk propose` takes a place, then cast <role> <holder> or access <setting> — {ProposeUsage}");
            var edit = cast
                ? new JsonObject { ["subject"] = "cast", ["place"] = words[0], ["act"] = words[2], ["by"] = words[3] == "default" ? null : words[3] }
                : new JsonObject { ["subject"] = "access", ["place"] = words[0], ["act"] = "access", ["class"] = string.Join(';', words.Skip(2)) };
            var body = new JsonObject { ["edits"] = new JsonArray(edit) };
            if ((FlagValue(args, "--conversation") ?? world.Instance.Environment(DriverChoicesCommand.SessionVariable)) is { Length: > 0 } named) body["conversation"] = named;
            return Pressed("risk propose", ChoiceRoutes.PolicyPropose, body, world);
        }

        if (words.Count != 3) throw new ChoiceWords($"`settings risk propose` takes a place, an act and a class — {ProposeUsage}");
        var riskClass = words[2] == "remove" ? null : words[2];
        if (riskClass is not null && !RiskClasses.Is(riskClass)) throw new ChoiceWords($"`{riskClass}` is no class (none, role, owner or remove) — {ProposeUsage}");
        var (place, act, by, mode) = (words[0], words[1], FlagValue(args, "--by"), FlagValue(args, "--mode"));
        if (place != PlaceRiskBook.MachineKey && !place.StartsWith("workspace/", StringComparison.Ordinal) && !place.StartsWith("repository/", StringComparison.Ordinal))
        {
            throw new ChoiceWords($"`{place}` is no place (machine, workspace/<name> or repository/<name>) — {ProposeUsage}");
        }

        try
        {
            if (RiskAct.Of(act).Kind == RiskActs.PolicyChange) throw new ChoiceWords($"policy.change is fixed at the owner: no row changes it — {ProposeUsage}");
        }
        catch (ArgumentException)
        {
            throw new ChoiceWords($"`{act}` names no act kind of the census — {ProposeUsage}");
        }

        if (by is not null && riskClass != RiskClasses.Role) throw new ChoiceWords($"--by names the role of a role row — {ProposeUsage}");
        if (mode is not (null or "full" or "restricted")) throw new ChoiceWords($"--mode is full or restricted — {ProposeUsage}");
        var payload = new JsonObject
        {
            ["edits"] = new JsonArray(new JsonObject { ["place"] = place, ["act"] = act, ["class"] = riskClass, ["by"] = by, ["mode"] = mode }),
        };
        // The conversation that proposes, where the press goes on the owner's card and its proof is not carried there: the terminal's own session.
        if ((FlagValue(args, "--conversation") ?? world.Instance.Environment(DriverChoicesCommand.SessionVariable)) is { Length: > 0 } conversation) payload["conversation"] = conversation;
        return Pressed("risk propose", ChoiceRoutes.PolicyPropose, payload, world);
    }

    private static ChoicePress Applying(IReadOnlyList<string> args, DriverChoicesWorld world)
    {
        var words = Operands(args, ProposeValued).Skip(2).ToList();
        if (words.Count != 2) throw new ChoiceWords($"`settings risk apply` takes a proposal and its digest — {ProposeUsage}");
        return Pressed("risk apply", ChoiceRoutes.PolicyApply, new JsonObject { ["proposal"] = words[0], ["digest"] = words[1] }, world);
    }

    private static ChoicePress Pressed(string verb, InstanceRoute route, JsonObject payload, DriverChoicesWorld world) => new(
        verb, route, payload,
        _ => throw new ChoiceWords("a change to the risk policy is proposed by Ask Daoris and confirmed in its conversation, so it needs Daoris running; nothing was changed.", 1),
        answer => answer.Clone(),
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

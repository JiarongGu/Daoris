using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris-driver driver role</c> (FOUNDATION1y-i, the foundation design §2.7): the terminal's door into the role catalogue, the window
/// twin of Settings → AI features → <i>Roles</i> over <c>SET_ROLE</c>. <c>role</c> says every role, <c>role &lt;id&gt;</c> one;
/// <c>role set</c> adds or changes a working role (its names, work, scope and choices), <c>role remove</c> takes one out and
/// <c>role default</c> gives a seeded one back its seed. Each change is judged by the role door's judge before anything is sent or
/// written, so a refusal is said in the judge's sentence.
/// </summary>
public sealed class RoleVerb : ChoiceVerb
{
    public override string Word => "role";

    private const string Grammar =
        "`driver role set <id> [--name \"…\"] [--zh \"…\"] [--takes <work,…>] [--scope machine|workspace:<id>|repository:<id>] "
        + "[\"<agent> [<model>|-] [<effort>|-]\" …]`, `driver role remove <id>` or `driver role default <id>`";

    public override ChoicePress Plan(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        if (args.Count < 3 || args[1] is not ("set" or "remove" or "default")) throw new ChoiceWords($"{Grammar}.");
        var staffing = RoleStaffing.Of(world.ConfigPath) ?? throw new ChoiceWords(Unkept(world));
        var catalogue = RoleCatalog.Read(staffing.Location);
        var id = args[2];
        var (change, choices) = args[1] switch
        {
            "remove" => (RoleChange.Remove(id), (IReadOnlyList<ActorChoice>?)null),
            "default" => (RoleChange.Default(id), null),
            _ => Set(id, [.. args.Skip(3)], catalogue),
        };
        if (args[1] != "set" && args.Count > 3) throw new ChoiceWords($"`driver role {args[1]}` takes the role's id alone.");
        if (RoleDoor.Judge(catalogue, change, choices) is { } refused) throw new ChoiceWords($"`driver role`: {refused.Said}");

        var payload = new JsonObject { ["change"] = args[1], ["id"] = id };
        if (change is RoleChange.SetRole { Role: var role })
        {
            payload["role"] = new JsonObject
            {
                ["name"] = new JsonObject { ["en"] = role.NameEn, ["zh"] = role.NameZh },
                ["takes"] = new JsonArray([.. role.Takes.Select(work => (JsonNode)JsonValue.Create(work))]),
                ["scope"] = new JsonObject { ["kind"] = role.Scope.Kind, ["id"] = role.Scope.Id },
            };
            payload["choices"] = choices is null ? null : new JsonArray([.. choices.Select(choice => (JsonNode)new JsonObject
            {
                ["agent"] = choice.Agent, ["model"] = choice.Model, ["effort"] = choice.Effort,
            })]);
        }

        return new(Word, ChoiceRoutes.Role, payload, _ =>
        {
            RoleDoor.Apply(staffing, change, choices);
            return Task.FromResult<object?>(null);
        }, _ => null, _ =>
        {
            var said = world.Instance.Out;
            said.WriteLine(change switch
            {
                RoleChange.SetRole { Role: var set } => $"daoris: role `{set.Id}` set: {Shape(set)}"
                    + (choices is null ? "." : $"; {(choices.Count == 0 ? "off" : Choices(choices))}."),
                RoleChange.RemoveRole => $"daoris: role `{id}` removed; a work no role takes holds its starts until it is given one. A running session goes on.",
                _ => $"daoris: role `{id}` is the seed's again.",
            });
            said.WriteLine("  The driver reads the catalogue at each start, so nothing restarts.");
            return 0;
        });
    }

    /// <summary><c>role</c> or <c>role &lt;id&gt;</c>: each role, its state, work, scope and choices; a work with no role said as holding.</summary>
    public override int? Said(IReadOnlyList<string> args, DriverConfig choices, DriverChoicesWorld world)
    {
        var said = world.Instance.Out;
        if (args.Count > 1 && args[1] is "set" or "remove" or "default") throw new ChoiceWords($"{Grammar}.");
        var staffing = RoleStaffing.Of(world.ConfigPath);
        var catalogue = staffing is null ? RoleCatalog.Of([]) : RoleCatalog.Read(staffing.Location);
        var rows = RoleDoor.Rows(catalogue, choices).Where(row => args.Count < 2 || row.Id == args[1]).ToList();
        if (args.Count > 1 && rows.Count == 0) throw new ChoiceWords($"no role `{args[1]}` is in the catalogue.");

        said.WriteLine("daoris: the roles that take this machine's work");
        foreach (var row in rows)
        {
            var shape = row.State switch
            {
                "removed" => "removed",
                "unreadable" => $"unreadable: {row.Error}",
                _ => $"{row.NameEn} · {Shape(row.Takes, row.ScopeKind, row.ScopeId)}"
                    + (row.Choices is { } staffed ? $" · {(staffed.Count == 0 ? "off" : Choices(staffed))}" : row.Seeded ? " · its actor's order" : "")
                    + (row.State == "seed" ? " (the seed)" : $" (revision {row.Revision})"),
            };
            said.WriteLine($"  {row.Id,-12}{shape}");
        }

        foreach (var work in RoleWork.Codes.Where(work => catalogue.RoleFor(work, new RolePlace()).Held is { } held && held == RoleCatalog.NoRole(work)))
        {
            said.WriteLine($"  `{work}` has no role on the machine, so its starts hold until one is given it.");
        }

        if (staffing is null) said.WriteLine($"  {Unkept(world)}");
        said.WriteLine($"  Change one: {Grammar}.");
        return 0;
    }

    private static (RoleChange, IReadOnlyList<ActorChoice>?) Set(string id, IReadOnlyList<string> rest, RoleCatalog catalogue)
    {
        var current = catalogue.Slots.FirstOrDefault(slot => slot.Id == id)?.Role;
        string? en = current?.NameEn, zh = current?.NameZh;
        var takes = current?.Takes ?? [];
        var scope = current?.Scope ?? RoleScope.Everywhere;
        var entries = new List<ActorChoice>();
        for (var at = 0; at < rest.Count; at++)
        {
            string Value() => at + 1 < rest.Count ? rest[++at] : throw new ChoiceWords($"`{rest[at]}` needs its value: {Grammar}.");
            switch (rest[at])
            {
                case "--name": en = Value(); break;
                case "--zh": zh = Value(); break;
                case "--takes": takes = [.. Value().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)]; break;
                case "--scope": scope = Scope(Value()); break;
                case var flag when flag.StartsWith("--", StringComparison.Ordinal): throw new ChoiceWords($"`{flag}` is not a flag of `driver role set`: {Grammar}.");
                default: entries.Add(Entry(rest[at])); break;
            }
        }

        if (takes.Count == 0) throw new ChoiceWords($"a new role names the work it takes (`--takes`): {Grammar}.");
        if (en is null) throw new ChoiceWords($"a new role names itself (`--name`): {Grammar}.");
        return (RoleChange.Set(new RoleRecord(id, en, zh ?? en, RoleRecord.Always, takes, scope)), entries.Count > 0 ? entries : null);
    }

    private static RoleScope Scope(string word)
    {
        if (word == RoleScope.Machine) return RoleScope.Everywhere;
        var parts = word.Split(':', 2);
        return parts.Length == 2 && parts[0] is RoleScope.Workspace or RoleScope.Repository && parts[1].Length > 0
            ? new RoleScope(parts[0], parts[1])
            : throw new ChoiceWords($"`{word}` is not a scope: `machine`, `workspace:<id>` or `repository:<id>`.");
    }

    private static ActorChoice Entry(string quoted)
    {
        var words = quoted.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length is 0 or > 3) throw new ChoiceWords($"`{quoted}` is not a choice: an agent, then its model or `-`, then its effort or `-`, in one quoted word.");
        string? Value(int at) => words.Length > at && words[at] != "-" ? words[at] : null;
        return new ActorChoice(words[0], Value(1), Value(2));
    }

    private static string Shape(RoleRecord role) => $"{role.NameEn} · {Shape(role.Takes, role.Scope.Kind, role.Scope.Id)}";

    private static string Shape(IReadOnlyList<string> takes, string? kind, string? scope) =>
        $"takes {string.Join(", ", takes)} " + (kind is null or RoleScope.Machine ? "on the machine" : $"in {kind} `{scope}`");

    private static string Choices(IReadOnlyList<ActorChoice> choices) =>
        string.Join(", ", choices.Select((choice, i) => $"{i + 1} {choice.Agent} · {choice.Model ?? "account default"} · {choice.Effort ?? "account default"}"));

    private static string Unkept(DriverChoicesWorld world) =>
        $"{world.ConfigPath}'s actors are the file's own here (not this home's store), so its roles are not set here.";
}

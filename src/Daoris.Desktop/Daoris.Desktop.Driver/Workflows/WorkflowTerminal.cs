using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Daoris.Driver.DriverWorkflowsCommand;

namespace Daoris.Driver;

internal sealed class WorkflowTerminal(IReadOnlyList<string> argv, DriverConfig choices, DriverChoicesWorld world, CancellationToken ct)
{
    internal IReadOnlyList<string> Argv => argv;
    internal DriverConfig Choices => choices;
    internal DriverChoicesWorld World => world;
    internal CancellationToken Ct => ct;
    internal readonly string Home = DriverConfig.HomeOf(world.ConfigPath);
    internal string Folder => WorkflowStore.FolderOf(Home);
    internal bool Plan => argv.Contains("--plan");

    internal readonly List<string> _held = [];
    internal bool _hold;
    internal WorkflowDeferred? Write;

    /// <summary>A session's terminal that writes says nothing until its write is known to be refused or sent.</summary>
    internal bool Session() => world.Instance.Environment(DriverChoicesCommand.SessionVariable) is { Length: > 0 };

    internal void Say(string line)
    {
        if (_hold) _held.Add(line);
        else world.Instance.Out.WriteLine(line);
    }


    public async Task<int> Go()
    {
        _hold = Changes([.. argv.Skip(1)]) && Session();
        try
        {
            var exit = await Dispatch().ConfigureAwait(false);
            return Write is { } write && exit == 0 ? await PressAsync(write).ConfigureAwait(false) : exit;
        }
        finally
        {
            foreach (var line in _held) world.Instance.Out.WriteLine(line);
        }
    }

    internal async Task<int> PressAsync(WorkflowDeferred write)
    {
        var written = false;
        async Task<int> Alone()
        {
            if (written) return 0;
            if (Session())
            {
                _held.Clear();
                await world.Instance.Error.WriteLineAsync($"{DriverChoicesCommand.Prefix}: {DriverChoicesCommand.SessionsTerminal}").ConfigureAwait(false);
                return 1;
            }

            write.Write();
            written = true;
            foreach (var line in write.Lines) Say(line);
            return 0;
        }

        if (write.Cannot is { } cannot && world.Instance is InstanceWorld door
            && (await InstanceDoor.FindAsync(door, ct).ConfigureAwait(false)).Found == InstanceFound.Answers)
        {
            await world.Instance.Error.WriteLineAsync($"{DriverChoicesCommand.Prefix}: {cannot} Nothing was written.").ConfigureAwait(false);
            return 1;
        }

        for (var at = 0; at < write.Payloads.Count; at++)
        {
            var lines = at == write.Payloads.Count - 1 ? write.Lines : [];
            var exit = await world.Instance.PressAsync(write.Route, write.Payloads[at], Alone, answer => Answered(answer, lines), ct).ConfigureAwait(false);
            if (exit != 0 || written) return exit;
        }

        return 0;
    }

    /// <summary>The route's answer in the verb's words: a refusal it gives as data is said and exit 1, else the lines the write would say.</summary>
    internal int Answered(JsonElement data, IReadOnlyList<string> lines)
    {
        if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("refused", out var refused) && refused.ValueKind == JsonValueKind.String)
        {
            var said = data.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String ? message.GetString() : refused.GetString();
            world.Instance.Error.WriteLine($"{DriverChoicesCommand.Prefix}: {said}");
            return 1;
        }

        foreach (var line in lines) Say(line);
        return 0;
    }

    internal Task<int> Dispatch()
    {
        var verb = argv.ElementAtOrDefault(1);
        switch (verb)
        {
            case "list":
                Flags("list", 2, [], []);
                return Task.FromResult(new WorkflowListing(this).List());
            case "defaults":
                return Task.FromResult(new WorkflowListing(this).Defaults());
            case "show":
                return argv.Contains("--repository") || argv.Contains("--workspace")
                    ? new WorkflowListing(this).ShowChosenAsync() : Task.FromResult(new WorkflowListing(this).Show());
            case "use":
                return new WorkflowChoosing(this).UseAsync();
            case "kind":
                return Task.FromResult(new WorkflowChoosing(this).Kind());
            case "new":
                return new WorkflowWriting(this).CreateAsync();
            case "edit":
                return Task.FromResult(new WorkflowWriting(this).Edit());
            case "apply":
                return Task.FromResult(new WorkflowWriting(this).Apply());
            case "export":
                return Task.FromResult(new WorkflowWriting(this).Export());
            case "import":
                return Task.FromResult(new WorkflowWriting(this).Import());
            default:
                throw new ChoiceWords(Usage);
        }
    }

    /// <summary>Refuses a flag the verb does not take, and an operand past the ones it names.</summary>
    internal void Flags(string name, int operands, string[] valued, string[] switches)
    {
        var seen = 0;
        for (var at = 2; at < argv.Count; at++)
        {
            var token = argv[at];
            if (valued.Contains(token)) at++;
            else if (switches.Contains(token)) continue;
            else if (token.StartsWith("--", StringComparison.Ordinal)) throw new ChoiceWords($"`{token}` is not a flag `workflow{name}` takes — {Usage}");
            else if (++seen > operands - 2) throw new ChoiceWords(Usage);
        }

        if (seen < operands - 2) throw new ChoiceWords(Usage);
    }

    internal WorkflowFound? Load(string id)
    {
        // An id is the file's name, so one that is no id would name a path outside the folder.
        if (!WorkflowNamed.IsId(id)) throw new ChoiceWords(WorkflowNamed.IdProblem(id)!, 1);
        var found = WorkflowPorts.Store(Home).Load(id);
        return found is null ? null : new WorkflowFound(id, WorkflowStore.PathOf(Home, id), found.File, found.Read);
    }

    internal WorkflowFound Required(string id) =>
        Load(id) ?? throw new ChoiceWords($"no workflow `{id}` here — `daoris workflow list` names them.");

    internal Task<ChoicesRegistry> Registry(CancellationToken token) => world.Registry(token);

    /// <summary>The new version's line, then its change as the card draws it, under the header.</summary>
    internal void Said(IReadOnlyList<NamedStep> steps, IReadOnlyList<string> lines)
    {
        Say($"  {WorkflowPresets.Line(steps)}");
        foreach (var line in lines) Say($"  {line}");
    }

    /// <summary>Written, or not with <c>--plan</c>, and said either way, with what reads it: the version a run binds.</summary>
    internal int Saved(string id, JsonNode file, IReadOnlyList<JsonObject> payloads, string? cannot = null)
    {
        if (Plan)
        {
            Say("  --plan: nothing was written.");
            Say(WorkflowWords.WhatChooses);
            return 0;
        }

        // Every value as read (FABRIC-GRAPH1f): a graph version's numbers by their own text and its keys in their order.
        Write = new WorkflowDeferred(ChoiceRoutes.WorkflowSave, payloads, () => WorkflowPorts.Store(Home).Write(id, file),
            [$"  Written to {WorkflowStore.PathOf(Home, id)}.", WorkflowWords.WhatChooses]) { Cannot = cannot };
        return 0;
    }

    /// <summary>The places that choose a workflow, as <c>WORKFLOW_SAVE</c> asks them authorized to append a version to it.</summary>
    internal JsonArray Authorize(string id) => new(WorkflowOperations.ScopesOf(choices, id).Select(each => (JsonNode)JsonValue.Create(each.Key)!).ToArray());

    /// <summary>A file the terminal names: absolute as given, else from the folder it was run in.</summary>
    internal string FileOperand(string? token)
    {
        if (token is null || token.StartsWith("--", StringComparison.Ordinal)) throw new ChoiceWords(Usage);
        return System.IO.Path.IsPathFullyQualified(token) ? token : System.IO.Path.GetFullPath(System.IO.Path.Combine(world.Folder, token));
    }

    /// <summary>A file's JSON, every value as written, or the refusal naming it.</summary>
    internal static JsonNode? Parsed(string path)
    {
        if (!File.Exists(path)) throw new ChoiceWords($"there is no file `{path}`.");
        try
        {
            return JsonNode.Parse(File.ReadAllText(path)); // read-shares: the person's own file, named on the command line
        }
        catch (JsonException)
        {
            throw new ChoiceWords($"`{path}` is not readable JSON.", 1);
        }
    }

    internal string? FlagValue(string flag)
    {
        var at = -1;
        for (var i = 0; i < argv.Count; i++)
        {
            if (argv[i] == flag)
            {
                at = i;
                break;
            }
        }

        if (at == -1) return null;
        var value = argv.ElementAtOrDefault(at + 1);
        if (string.IsNullOrEmpty(value) || value.StartsWith("--", StringComparison.Ordinal)) throw new ChoiceWords($"{flag} needs a name — e.g. `{flag} aurora`");
        return value;
    }

    internal string Moment() => world.Now().UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}

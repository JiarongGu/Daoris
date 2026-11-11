using System.Globalization;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris repository create &lt;name&gt; --purpose "…"</c> as the card shows it (<c>REGISTRY_CREATE</c>): the plan the route's own code reads
/// (<c>REGISTRY_CREATE_PLAN</c>, the press's own ground, which the Confirm reads again) said as what the press means and the command it is,
/// then every value: the name, workspace, the full folder, the line, the identity, that no remote is made, each step in its order and the first
/// lines of each quest it publishes. A plan its own rules refuse is refused at the ask in the plan's words, so the person never sees a card.
/// </summary>
public sealed class RegistryCreateView : IPressView
{
    public string Module => DriverModule.Name;

    public string Route => "REGISTRY_CREATE";

    public async Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        var ask = new CreateAsk(PressPayload.Text(payload, "name"), PressPayload.Text(payload, "purpose"))
        {
            Kind = Said(payload, "kind") ?? PlaceKind.Git,
            Workspace = Said(payload, "workspace"),
            Under = Said(payload, "under"),
            Line = Said(payload, "line"),
            Work = Said(payload, "work"),
        };
        var plan = await world.ReadAsync(Module, "REGISTRY_CREATE_PLAN", new
        {
            name = ask.Name, purpose = ask.Purpose, kind = ask.Kind, workspace = ask.Workspace, under = ask.Under, line = ask.Line, work = ask.Work,
        }, cancellationToken).ConfigureAwait(false);
        var refusals = PressPayload.Rows(plan, "refusals").Select(row => PressPayload.Field(row, "sentence")).Where(sentence => sentence.Length > 0).ToList();
        if (refusals.Count > 0) return PressViewed.Refused(string.Join(' ', refusals));

        var git = ask.Kind == PlaceKind.Git;
        var workspace = PressPayload.Field(plan, "workspace");
        var folder = PressPayload.Field(plan, "folder");
        var line = PressPayload.Field(plan, "line");
        var author = plan.TryGetProperty("author", out var who) && who.ValueKind == JsonValueKind.Object
            ? $"{PressPayload.Field(who, "name")} <{PressPayload.Field(who, "email")}>"
            : "";
        var remote = PressPayload.Field(plan, "remoteStep");
        var facts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["name"] = ask.Name, ["workspace"] = workspace, ["folder"] = folder, ["purpose"] = ask.Purpose,
        };
        if (git)
        {
            facts["line"] = line;
            facts["author"] = author;
        }

        List<PressSaid> also = [];
        if (git) also.Add(new PressSaid("repository-create-no-remote"));
        also.AddRange(Steps(plan, ask, workspace, folder, line, author, remote));
        if (PressPayload.Field(plan, "resumes") is { Length: > 0 } resumes) also.Add(new PressSaid("repository-create-resumes", ("plan", resumes[..Math.Min(12, resumes.Length)])));
        also.AddRange(PressPayload.Rows(plan, "quests").Select(quest => new PressSaid(
            PressPayload.Field(quest, "role") == CreateQuests.Work ? "repository-create-quest-work" : "repository-create-quest-setup",
            ("title", PressPayload.Field(quest, "title")), ("head", PressPayload.Field(quest, "head")))));

        var command = string.Join(' ', ["daoris", "repository", "create", .. RepositoryCreateCommand.ArgsOf(ask)
            .Select((word, at) => at == 0 ? ShellWord.Of(word, ShellWord.Name) : Spelled(word))]);
        return PressViewed.Shown(new PressView(git ? "repository-create" : "repository-create-folder", facts, command, also));
    }

    /// <summary>Each step the plan makes, worded by its id with the values it names, numbered in the order it is made.</summary>
    private static IEnumerable<PressSaid> Steps(JsonElement plan, CreateAsk ask, string workspace, string folder, string line, string author, string remote)
    {
        var at = 0;
        foreach (var step in PressPayload.Rows(plan, "steps"))
        {
            var n = (++at).ToString(CultureInfo.InvariantCulture);
            yield return PressPayload.Field(step, "id") switch
            {
                CreateOperations.Folder => new PressSaid("repository-create-step-folder", ("n", n), ("folder", folder)),
                CreateOperations.History => new PressSaid("repository-create-step-history", ("n", n), ("line", line), ("author", author)),
                CreateOperations.Registration => new PressSaid(
                    ask.Kind == PlaceKind.Git ? "repository-create-step-registration" : "repository-create-step-registration-folder",
                    ("n", n), ("name", ask.Name), ("workspace", workspace)),
                CreateOperations.Driving when remote.Length > 0 => new PressSaid("repository-create-step-driving-off", ("n", n), ("name", ask.Name), ("step", remote)),
                CreateOperations.Driving => new PressSaid("repository-create-step-driving", ("n", n), ("workspace", workspace)),
                _ => new PressSaid("repository-create-step-setup", ("n", n), ("name", ask.Name)),
            };
        }
    }

    /// <summary>A flag stays as written; a value is spelled for any shell, or named where no spelling holds.</summary>
    private static string Spelled(string word) => word.StartsWith("--", StringComparison.Ordinal) ? word : ShellWord.Of(word, "<value>");

    private static string? Said(JsonElement? payload, string name) => PressPayload.Text(payload, name) is { Length: > 0 } text ? text : null;
}

using System.Globalization;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris ask [--workspace &lt;name&gt;] [--to &lt;repo&gt;] … "…"</c> as the card shows it (<c>ASK_NEW</c>): the workspace it is made at, its
/// words, the receiver it is published to at once, and what it carries. A workspace or a receiver this machine does not have, and what
/// the verb's own judges refuse (<see cref="AskNewCommand.Unfit"/>), are refused at the ask in their words, so the person never sees it.
/// </summary>
public sealed class AskNewView : IPressView
{
    /// <summary>A value no spelling holds in every shell, by what it is.</summary>
    private const string WordsPlaceholder = "\"<words>\"";

    public string Module => DriverModule.Name;

    public string Route => "ASK_NEW";

    public async Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        var asked = new AskNew(Named(payload, "workspace") ?? "default", PressPayload.Text(payload, "sentence"), Named(payload, "to"),
            List(payload, "links"), List(payload, "files"), Named(payload, "review"), Named(payload, "reviewWords"), Named(payload, "kind"),
            Named(payload, "workflow"), Named(payload, "workflowWords"), Named(payload, "short"));
        if (asked.Sentence.Trim().Length == 0) return PressViewed.Refused("an ask needs its words.");

        var machine = await world.MachineAsync(cancellationToken).ConfigureAwait(false);
        var registry = (await world.RegistryAsync(cancellationToken).ConfigureAwait(false)).Select(row => (row.Repository, row.Workspace)).ToList();
        var held = registry.Select(row => string.IsNullOrWhiteSpace(row.Workspace) ? "default" : row.Workspace.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToList();
        if (held.FirstOrDefault(name => string.Equals(name, asked.Workspace, StringComparison.OrdinalIgnoreCase)) is not { } workspace)
        {
            var known = string.Join(", ", held.Select(name => $"`{name}`"));
            return PressViewed.Refused($"there is no workspace `{asked.Workspace}` on this machine — one of {known}.");
        }

        var ask = asked with { Workspace = workspace };
        if (ask.To is { } to && await world.RegistrationAsync(to, cancellationToken).ConfigureAwait(false) is null)
        {
            return PressViewed.Refused($"`{to}` is not registered on this machine — name a receiver as Repositories lists it.");
        }

        if (AskNewCommand.Unfit(ask, machine.Config, machine.Facts.Home ?? "", registry, out _) is { } unfit) return PressViewed.Refused(unfit);

        foreach (var path in ask.Files ?? [])
        {
            if (!File.Exists(path)) return PressViewed.Refused($"`{path}` is not a file on this machine. Nothing was asked.");
        }

        var facts = new Dictionary<string, string>(StringComparer.Ordinal) { ["workspace"] = ask.Workspace, ["sentence"] = ask.Sentence.Trim() };
        if (ask.To is not null) facts["to"] = ask.To;
        return PressViewed.Shown(new PressView(ask.To is null ? "ask-new" : "ask-new-to", facts, Command(ask), Carried(ask) is { Count: > 0 } also ? also : null));
    }

    /// <summary>What the ask carries beside its words, a sentence each, in the command's order.</summary>
    private static List<PressSaid> Carried(AskNew ask)
    {
        var also = new List<PressSaid>();
        if (ask.Short is { } title) also.Add(new PressSaid("ask-new-short", ("title", title)));
        if (ask.Review is { } review && review != AskReviewCommand.Rule)
        {
            also.Add(review switch
            {
                "on" => new PressSaid("ask-new-review-on"),
                "off" => new PressSaid("ask-new-review-off"),
                _ => new PressSaid("ask-new-review-environment", ("environment", review)),
            });
        }

        if (ask.ReviewWords is { } why) also.Add(new PressSaid("ask-new-review-words", ("words", why)));
        if (ask.Kind is { } kind) also.Add(new PressSaid("ask-new-kind", ("kind", kind)));
        if (ask.Workflow is { } workflow) also.Add(new PressSaid("ask-new-workflow", ("workflow", workflow)));
        if (ask.Links is { Count: > 0 } links) also.Add(new PressSaid("ask-new-links", ("count", Count(links.Count)), ("links", string.Join(", ", links))));
        if (ask.Files is { Count: > 0 } files)
        {
            also.Add(new PressSaid("ask-new-files", ("count", Count(files.Count)), ("files", string.Join(", ", files.Select(Path.GetFileName)))));
        }

        return also;
    }

    /// <summary>The command the press is, each value spelled for any shell.</summary>
    private static string Command(AskNew ask)
    {
        var flags = new List<string> { "daoris ask", $"--workspace {ShellWord.Of(ask.Workspace, ShellWord.Workspace)}" };
        void Flag(string flag, string? value, string placeholder)
        {
            if (value is not null) flags.Add($"{flag} {ShellWord.Of(value, placeholder)}");
        }

        Flag("--to", ask.To, ShellWord.Name);
        Flag("--short", ask.Short, "\"<title>\"");
        Flag("--review", ask.Review, ShellWord.Name);
        Flag("--review-words", ask.ReviewWords, WordsPlaceholder);
        Flag("--kind", ask.Kind, ShellWord.Name);
        Flag("--workflow", ask.Workflow, ShellWord.Name);
        Flag("--workflow-words", ask.WorkflowWords, WordsPlaceholder);
        foreach (var link in ask.Links ?? []) Flag("--url", link, "<address>");
        foreach (var file in ask.Files ?? []) Flag("--file", file, "<path>");
        flags.Add(ShellWord.Of(ask.Sentence.Trim(), WordsPlaceholder));
        return string.Join(' ', flags);
    }

    private static string? Named(JsonElement? payload, string name) => PressPayload.Text(payload, name) is { Length: > 0 } text ? text.Trim() : null;

    private static IReadOnlyList<string>? List(JsonElement? payload, string name) =>
        payload is { ValueKind: JsonValueKind.Object } body && body.TryGetProperty(name, out var list) && list.ValueKind == JsonValueKind.Array
            ? [.. list.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.String).Select(each => each.GetString()!)]
            : null;

    private static string Count(int count) => count.ToString(CultureInfo.InvariantCulture);
}

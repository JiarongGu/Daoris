using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// A steward's act that is a person's route of the driver module in one exact form of its own: within the acts the
/// person granted, its handler runs that route inside the role's door, so the owner's marks pass it by, and records it as the steward's;
/// outside them it asks the person as that same press. The route's own judges stand: one it refuses is refused with its code, nothing
/// recorded. Only a route that writes no one's words is such an act.
/// </summary>
/// <param name="target">The field of the form that names what the act acts on.</param>
/// <param name="kind">What that names: a repository, a quest, or a session (resolved to its quest).</param>
/// <param name="gate">The act's own condition on its target, judged under the gates; a repository's is that the registry names it.</param>
public sealed class StewardRouteAct(
    string name, string route, IReadOnlyList<string> fields, string target = "repository", string kind = OwnerKinds.Repository,
    Func<RoleActRun, string, CancellationToken, Task<string?>>? gate = null) : IRoleAct
{
    /// <summary>A place's workflow choice: <c>WORKFLOW_USE { repository, workflow }</c>, a saved workflow's id or <c>current</c>.</summary>
    public static StewardRouteAct Workflow { get; } = new(StewardActNames.Workflow, "WORKFLOW_USE", ["repository", "workflow"]);

    /// <summary>A quest closed as done: <c>QUEST_DONE { quest }</c>, the service's door for the person's own done.</summary>
    public static StewardRouteAct Done { get; } = new(StewardActNames.Done, "QUEST_DONE", ["quest"], "quest", OwnerKinds.Quest, StewardTargets.QuestExistsAsync);

    /// <summary>A quest deleted: <c>QUEST_DELETE { quest }</c>, which the service refuses for one anything stands on.</summary>
    public static StewardRouteAct Delete { get; } = new(StewardActNames.Delete, "QUEST_DELETE", ["quest"], "quest", OwnerKinds.Quest, StewardTargets.QuestExistsAsync);

    /// <summary>A session's tree cleaned: <c>DISCARD_SESSION_TREE { id }</c> unforced, so a tree holding work nobody landed stays.</summary>
    public static StewardRouteAct Clean { get; } = new(StewardActNames.Clean, "DISCARD_SESSION_TREE", ["id"], "id", OwnerKinds.Session, StewardTargets.OwnTreeAsync);

    public string Name => name;

    public string? PersonRoute => route;

    public string TargetKind => kind;

    public bool IsForm(JsonElement form) => StewardActRows.Exact(
        form, fields.ToDictionary(field => field, _ => (Func<JsonElement, bool>)StewardActRows.Named, StringComparer.OrdinalIgnoreCase));

    public string Target(JsonElement form) => StewardActRows.Text(form, target).TrimStart('#');

    public async Task<string?> UnderGatesAsync(RoleActRun run, CancellationToken ct)
    {
        if (gate is not null) return await gate(run, Target(run.Form), ct).ConfigureAwait(false);
        return run.World is { } world && !await world.RegisteredAsync(Target(run.Form), ct).ConfigureAwait(false) ? StewardWhy.Unregistered : null;
    }

    public async Task<object?> RunAsync(RoleActRun run, CancellationToken ct)
    {
        if (run.Route is not { } dispatch) throw new RoleActRefused(StewardWhy.NoRoute);
        var answered = await dispatch(route, run.Form, ct).ConfigureAwait(false);
        if (!answered.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True)
        {
            throw new RoleActRefused(answered.TryGetProperty("error", out var error) ? Text(error, "code") ?? StewardWhy.Refused : StewardWhy.Refused);
        }

        var data = answered.TryGetProperty("data", out var held) ? held : default;
        if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("done", out var done) && done.ValueKind == JsonValueKind.False)
        {
            throw new RoleActRefused(Text(data, "refused") ?? StewardWhy.Refused);
        }

        var named = Target(run.Form);
        if (kind == OwnerKinds.Repository)
        {
            run.Judge.StewardDid(run.Id, name, repository: named);
            return new { Act = name, Repository = named };
        }

        if (kind == OwnerKinds.Quest)
        {
            run.Judge.StewardDid(run.Id, name, quest: named);
            return new { Act = name, Quest = named };
        }

        var record = run.World is { } world ? await world.SessionAsync(named, ct).ConfigureAwait(false) : null;
        run.Judge.StewardDid(run.Id, name, record?.Repository, record?.Quest, named);
        return new { Act = name, Session = named };
    }

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>The route run on the pipe's dispatcher, inside the door the act runs in, its whole answer as the pipe would write it.</summary>
    internal static async Task<JsonElement> DispatchAsync(InstancePipe pipe, string act, string route, JsonElement form, CancellationToken ct)
    {
        var request = new IpcRequest { Id = $"{act}-{route}", Module = DriverModule.Name, Type = route, Payload = form };
        var response = await pipe.Dispatcher.DispatchAsync(request, ct).ConfigureAwait(false);
        return JsonDocument.Parse(IpcJson.Serialize(response)).RootElement.Clone();
    }
}

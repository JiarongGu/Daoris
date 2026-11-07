using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// A steward's act that is a person's route of the driver module in one exact form of its own (STEWARDROLE1d): within the acts the
/// person granted, its handler runs that route inside the role's door, so the owner's marks pass it by, and records it as the steward's;
/// outside them it asks the person as that same press. The route's own judges stand: one it refuses is refused with its code, nothing
/// recorded. Only a route that writes no one's words is such an act.
/// </summary>
public sealed class StewardRouteAct(string name, string route, IReadOnlyList<string> fields) : IRoleAct
{
    /// <summary>A place's workflow choice: <c>WORKFLOW_USE { repository, workflow }</c>, a saved workflow's id or <c>current</c>.</summary>
    public static StewardRouteAct Workflow { get; } = new(StewardActNames.Workflow, "WORKFLOW_USE", ["repository", "workflow"]);

    public string Name => name;

    public string? PersonRoute => route;

    public bool IsForm(JsonElement form) => StewardActRows.Exact(
        form, fields.ToDictionary(field => field, _ => (Func<JsonElement, bool>)StewardActRows.Named, StringComparer.OrdinalIgnoreCase));

    public string Target(JsonElement form) => StewardActRows.Text(form, "repository");

    public async Task<string?> UnderGatesAsync(RoleActRun run, CancellationToken ct) =>
        run.World is { } world && !await world.RegisteredAsync(Target(run.Form), ct).ConfigureAwait(false) ? StewardWhy.Unregistered : null;

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

        var repository = Target(run.Form);
        run.Judge.StewardDid(run.Id, name, repository: repository);
        return new { Act = name, Repository = repository };
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

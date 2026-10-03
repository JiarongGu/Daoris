using System.Text.Json;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// The screen's door to an install's update (UPDATE1, D139 §3, D50): the banner's state, and its words — *Update when
/// idle*, *Update now*, *Not now* — written to the request `daoris-driver update` writes.
/// </summary>
/// <remarks>
/// <para><b><c>STATE</c></b> answers <see cref="InstallUpdater.State"/>; <b><c>SET</c></b> takes <c>{ mode }</c>
/// (<c>when-idle</c>, <c>now</c>, <c>not-now</c>) and answers the state after it, *Update now* already applying;
/// <b><c>DISMISS</c></b> puts away an outcome already said, while <c>last</c>, the swap's journal told or not, stays on every
/// state for Settings' row (UPDATE1d). Each change also reaches the page as <c>UPDATE_STATE</c>.</para>
///
/// <para>Nothing it answers names a path: the staged build by its id, version and commit, a refusal by its code.</para>
/// </remarks>
public sealed class UpdateModule(InstallUpdater updater) : ModuleBase
{
    public override string ModuleName => "DAORIS.UPDATE";

    protected override Task<object?> RouteMessageAsync(IpcRequest request, IModuleContext context, CancellationToken cancellationToken) =>
        request.Type switch
        {
            "STATE" => Task.FromResult<object?>(updater.State),
            "SET" => Task.FromResult<object?>(updater.Say(Mode(request.Payload), "screen")),
            "DISMISS" => Task.FromResult<object?>(updater.Dismiss()),
            _ => throw UnknownType(request),
        };

    private static string? Mode(JsonElement? payload) =>
        payload is { ValueKind: JsonValueKind.Object } given
        && given.TryGetProperty("mode", out var mode) && mode.ValueKind == JsonValueKind.String
            ? mode.GetString()
            : null;
}

using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// How this Daoris is wired, the page's `bridge/system.ts` (WIRING1b, the this-Daoris design §6): Settings → This Daoris's
/// read, through the driver library's <see cref="SystemWiring"/>, the reader `daoris-driver wiring` calls too (D50). A machine
/// domain: the desktop's alone, answering this machine's paths and never a key (D47 §4, design §5).
/// </summary>
public sealed class SystemRoutes(DriverContext context)
{
    private readonly DriverContext _context = context;

    // WIRING1b (design §6): the wiring, every fact with its source and a fact not read null with its why. A read: it declares
    // nothing. `{ measure: true }` starts the folders' sizes in the background, one walk at a time, and the answer says
    // `measuring`; the open asks no network, starts no program and walks no folder (the second opinion's point 3). Off the
    // window's thread, since the read looks at files (UIFREEZE1). The reader's own JSON is answered whole, so a null stays a
    // field: the bridge leaves a null out of an object it serializes.
    [DriverRoute("SYSTEM_WIRING")]
    private async Task<object?> WiringAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var measure = request.Payload is { ValueKind: JsonValueKind.Object } payload
            && payload.TryGetProperty("measure", out var asked) && asked.ValueKind == JsonValueKind.True;
        var world = await WorldWithOriginsAsync(cancellationToken).ConfigureAwait(false);
        return await Task.Run(() =>
        {
            if (measure && world.Home is { } home)
            {
                world.Sizes.Start(home, SystemWiring.RootOf(home), WiringBudget.Default, ToolHomeLeftovers.List(world).Folders);
            }

            using var answer = JsonDocument.Parse(SystemWiring.Json(SystemWiring.Read(world)));
            return (object?)answer.RootElement.Clone();
        }, cancellationToken).ConfigureAwait(false);
    }

    // OWNSPACE2 (D175 point 3, D88): the person's press after the list of what Daoris's sessions left in their own tool home. It
    // moves aside, never deletes, exactly the `folders` sent (each a `name` and the `id` the list showed), each judged again
    // inside the driver library: still provably Daoris's, the same folder, no session that may use it, nothing read short. It
    // answers what moved, where, and what stayed with why; any other folder of the person's is never touched. A person's delete
    // (the pipe census), so the page asks first. It declares nothing: what it moves is outside Daoris's state, and the page
    // reads the wiring again itself.
    [DriverRoute("SYSTEM_TOOL_CLEAR")]
    private async Task<object?> ToolClearAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var folders = request.Payload is { ValueKind: JsonValueKind.Object } payload
            && payload.TryGetProperty("folders", out var listed) && listed.ValueKind == JsonValueKind.Array
                ? listed.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.Object).Select(each => new ToolAsk(
                    Text(each, "name") ?? "", Text(each, "id") ?? "")).ToList()
                : throw new DriverException("a clear sends the folders its list held: each folder's `name` and `id`.");
        var world = await WorldWithOriginsAsync(cancellationToken).ConfigureAwait(false);
        return await Task.Run(() =>
        {
            var outcome = ToolHomeLeftovers.Clear(world, folders, DateTimeOffset.UtcNow);
            return (object?)new
            {
                cleared = outcome.Cleared.Select(done => new { name = done.Name, bytes = done.Bytes, to = done.To }).ToList(),
                kept = outcome.Kept.Select(stays => new { name = stays.Name, why = stays.Why }).ToList(),
            };
        }, cancellationToken).ConfigureAwait(false);
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>
    /// This window's world with the working directories Daoris's own records hold (OWNSPACE2): the service's session records read
    /// now, each folder's owner and what keeps it. A service that does not answer is records unread, which keeps every folder.
    /// A test's world that already names its origins is left as it is.
    /// </summary>
    private async Task<WiringWorld> WorldWithOriginsAsync(CancellationToken cancellationToken)
    {
        var world = _context.WiringWorldOf();
        if (world.ToolOrigins is not null || world.Home is not { } home) return world;
        string? json = null;
        if (_context.Loop.Service is { } service)
        {
            try
            {
                json = await service.SessionRecordsJsonAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception error) when (error is DriverException or HttpRequestException or TaskCanceledException or JsonException)
            {
                json = null;
            }
        }

        return world with { ToolOrigins = await Task.Run(() => ToolHomeLeftovers.Origins(home, json), cancellationToken).ConfigureAwait(false) };
    }
}

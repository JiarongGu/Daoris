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
        return await Task.Run(() =>
        {
            var world = _context.WiringWorldOf();
            if (measure && world.Home is { } home) world.Sizes.Start(home, SystemWiring.RootOf(home), WiringBudget.Default);
            using var answer = JsonDocument.Parse(SystemWiring.Json(SystemWiring.Read(world)));
            return (object?)answer.RootElement.Clone();
        }, cancellationToken).ConfigureAwait(false);
    }
}

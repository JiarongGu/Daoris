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
    //
    // OWNSPACE2 (D175 point 3): its `toolHome` lists what Daoris's sessions left in the person's own tool home, read-only. There
    // is no press: removing them is OWNSPACE2e's, hardened, and until then a person removes a folder by hand.
    [DriverRoute("SYSTEM_WIRING")]
    private async Task<object?> WiringAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var measure = request.Payload is { ValueKind: JsonValueKind.Object } payload
            && payload.TryGetProperty("measure", out var asked) && asked.ValueKind == JsonValueKind.True;
        var world = await WorldWithOriginsAsync(cancellationToken).ConfigureAwait(false);
        return await Task.Run(() =>
        {
            if (measure && world.Home is { } home
                && world.Sizes.Start(home, SystemWiring.RootOf(home), WiringBudget.Default, ToolHomeLeftovers.List(world).Folders))
            {
                // FABRIC-EVENT5b: the windows are told a walk is under way, and again once its sizes have landed, in place of the
                // page reading this every 2 s. The walk keeps one reading, as it ends (`WiringSizes.MeasureNow`), and leaves the home's
                // walks after it, which `Finished` waits for, so the read this tells is no longer measuring.
                StatePublisher.Publish(_context.Events, StateScopes.Wiring);
                var sizes = world.Sizes;
                _ = sizes.Finished(home).ContinueWith(
                    _ => StatePublisher.Publish(_context.Events, StateScopes.Wiring), TaskScheduler.Default);
            }

            using var answer = JsonDocument.Parse(SystemWiring.Json(SystemWiring.Read(world)));
            return (object?)answer.RootElement.Clone();
        }, cancellationToken).ConfigureAwait(false);
    }

    // CACHECLEAR1: the Downloads row's list, the agents' and tools' downloaded versions with what keeps each, sized as they are
    // listed (a walk of each version's folder, off the window's thread). A read: it declares nothing and removes nothing.
    [DriverRoute("SYSTEM_DOWNLOADS")]
    private Task<object?> DownloadsAsync(IpcRequest request, CancellationToken cancellationToken) =>
        Task.Run(() => (object?)DownloadsAnswer(DownloadClearing.List(_context.Loop.Home, Uses(), DateTimeOffset.UtcNow, cancellationToken)), cancellationToken);

    // CACHECLEAR1 (D88's shape): the press after the list. `versions` names the rows the page showed, `{ kind, name, version, unfinished }`;
    // each is judged again from the files as they stand right before it goes, and one kept, held by a program or gone since is said in
    // the answer. What it moves: This Daoris's sizes, and the Tools' and agents' pages that say which versions are downloaded.
    [DriverRoute("SYSTEM_DOWNLOADS_CLEAR")]
    [Publishes("SYSTEM_DOWNLOADS_CLEAR", StateScopes.Harnesses | StateScopes.Tools | StateScopes.Wiring)]
    private Task<object?> ClearDownloadsAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var asked = DownloadsAsked(request.Payload);
        return Task.Run(() =>
        {
            var cleared = DownloadClearing.Clear(
                _context.Loop.Home, asked, Uses, () => DateTimeOffset.UtcNow, _context.Loop.Log, cancellationToken: cancellationToken);
            return (object?)new
            {
                Cleared = cleared.Select(each => new
                {
                    each.Asked.Kind, each.Asked.Name, each.Asked.Version, each.Asked.Unfinished, each.Outcome,
                    Bytes = each.Version?.Bytes, Keeps = each.Outcome == "kept" ? each.Version?.Keeps : null,
                    By = each.Outcome == "kept" ? each.Version?.By : null,
                }).ToArray(),
            };
        }, cancellationToken);
    }

    /// <summary>The files that say which downloads are used, as they stand now: the agents' wiring as the roster reads it, this home's tools and choices.</summary>
    private DownloadUses Uses() => DownloadUses.Read(_context.Loop.Home, _context.Loop.Harnesses.Settings, _context.Loop.ConfigPath);

    private static object DownloadsAnswer(DownloadListing listing) => new
    {
        listing.Why,
        Versions = listing.Versions.Select(row => new { row.Kind, row.Name, row.Version, row.Path, row.Bytes, row.Partial, row.Keeps, row.By, row.Unfinished }).ToArray(),
    };

    /// <summary>The rows a press names; a call naming none, or a row without its kind, name and version, is malformed.</summary>
    public static IReadOnlyList<DownloadAsked> DownloadsAsked(JsonElement? payload)
    {
        if (payload is not { ValueKind: JsonValueKind.Object } body || !body.TryGetProperty("versions", out var versions) || versions.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException("`versions` must name the downloaded versions to remove.");
        }

        var asked = new List<DownloadAsked>();
        foreach (var each in versions.EnumerateArray())
        {
            string? Text(string name) =>
                each.ValueKind == JsonValueKind.Object && each.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            if (Text("kind") is not ("agent" or "tool") || Text("name") is not { } name || Text("version") is not { } version)
            {
                throw new ArgumentException("each of `versions` must be `{ kind: agent|tool, name, version }`.");
            }

            asked.Add(new DownloadAsked(Text("kind")!, name, version,
                each.TryGetProperty("unfinished", out var unfinished) && unfinished.ValueKind == JsonValueKind.True));
        }

        return asked;
    }

    /// <summary>
    /// This window's world with the working directories Daoris's session records show a session ran in (OWNSPACE2): the
    /// service's records read now. A service that does not answer is records unread, and with no record of use nothing is
    /// claimed. A test's world that already names its origins is left as it is.
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

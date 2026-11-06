using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// How this Daoris is wired, the page's `bridge/system.ts` (WIRING1b, the this-Daoris design §6): Settings → This Daoris's
/// read, through the driver library's <see cref="SystemWiring"/>, the reader `daoris wiring` calls too (D50). A machine
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
    [DriverRoute("SYSTEM_WIRING", PipeClass.Open)]
    private async Task<object?> WiringAsync(WiringRead press, CancellationToken cancellationToken)
    {
        var measure = press.Measure;
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

    // WIRING1e (the this-Daoris design §4): *Open* on a folder or file row. The page sends the id the read gave it and this resolves
    // the folder (`WiringFolderPaths`), so no path comes from the page; the launcher runs through the start gate, off the window's
    // thread. A window's act: refused on the pipe. It changes nothing, so it declares nothing; an id with no place, or a folder the
    // file manager could not open, is answered as data, `{ done: false, why, message }`.
    [DriverRoute("SYSTEM_OPEN_FOLDER", PipeClass.Window)]
    [Owner("SYSTEM_OPEN_FOLDER", None = OwnerNone.ActsOnNothing)]
    private async Task<object?> OpenFolderAsync(OpenFolderPress press, CancellationToken cancellationToken)
    {
        var (folder, why) = WiringFolderPaths.Resolve(_context.WiringWorldOf(), press.Folder);
        if (folder is null) return Said(SetupResult.Refused(why ?? "FOLDER_UNKNOWN", "This Daoris keeps no folder by that name."));
        if (_context.OpenFolder is not { } open) return Said(SetupResult.Refused("FOLDER_NOT_OPENED", "This window has no file manager to open it in."));
        try
        {
            if (!await Task.Run(() => Directory.Exists(folder), cancellationToken).ConfigureAwait(false))
            {
                return Said(SetupResult.Refused("FOLDER_NOT_OPENED", "That folder is not there yet."));
            }

            await open.OpenAsync(folder, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return Said(SetupResult.Refused("FOLDER_NOT_OPENED", "The file manager could not open it."));
        }

        return Said(SetupResult.Did(changed: false));
    }

    // WIRING1e: *Put on my PATH* / *Take off my PATH* (`AccountPath`): the install's launchers' folder added to, or removed from,
    // the account's PATH, never the machine's; running programs are told the environment changed. The person's press, said first on the
    // page and undone by the next; a window's act, refused on the pipe. A refusal is data and changed nothing.
    [DriverRoute("SYSTEM_PATH", PipeClass.Window)]
    [Owner("SYSTEM_PATH", None = OwnerNone.MachineSetting)]
    [Publishes("SYSTEM_PATH", StateScopes.Wiring)]
    private Task<object?> PathAsync(PathPress press, CancellationToken cancellationToken)
    {
        var on = press.On ?? throw new ArgumentException("`on` must be true or false.");
        return Task.Run(() =>
        {
            var world = _context.WiringWorldOf();
            var install = SystemWiring.InstallOf(world.Home) ?? DaorisHome.InstallOf(world.BaseDirectory);
            return Said(AccountPath.Set(install, _context.Account, on));
        }, cancellationToken);
    }

    // WIRING1e: *Change the service's address* (`AccountServiceAddress`): the account's DAORIS_SERVICE_URL set to an address of this
    // machine, or cleared for the default (`url` null or absent). It takes effect when Daoris starts again, and a terminal reads it too.
    [DriverRoute("SYSTEM_SERVICE_ADDRESS", PipeClass.Window)]
    [Owner("SYSTEM_SERVICE_ADDRESS", None = OwnerNone.MachineSetting)]
    [Publishes("SYSTEM_SERVICE_ADDRESS", StateScopes.Wiring)]
    private Task<object?> ServiceAddressAsync(ServiceAddressPress press, CancellationToken cancellationToken) =>
        Task.Run(() => Said(AccountServiceAddress.Set(_context.Account, press.Url)), cancellationToken);

    private static object? Said(SetupResult result) => new { result.Done, result.Changed, result.Why, result.Message };

    // KEEP1c (D195 point 5, the cleanup design §4.4): the Disk space card's read through the driver library's `SystemSpace`, the reader
    // `daoris space` calls too: the free space, and each kind's size with when it was counted, its rule, its next and last clean,
    // and the run lock's holder. A read: it declares nothing and removes nothing. `{ measure: true }` counts every kind, `{ measure: [ids] }`
    // those, each a bounded walk in the background, one kind after another; each kind's end tells `wiring`, so the card fills in kind by
    // kind (WIRING-SIZES-LIVE1's need). The open walks nothing. Off the window's thread; the reader's own JSON whole, so a null stays a field.
    [DriverRoute("SYSTEM_SPACE", PipeClass.Open)]
    private Task<object?> SpaceAsync(SpaceRead read, CancellationToken cancellationToken)
    {
        var asked = SpaceAsked(read.Measure);
        return Task.Run(() =>
        {
            var loop = _context.Loop;
            var home = loop.Home;
            var sizes = SpaceSizes.Shared;
            if (asked.Count > 0)
            {
                sizes.Tell(home, _ => StatePublisher.Publish(_context.Events, StateScopes.Wiring));
                if (sizes.Start(home, asked, WiringBudget.Default)) StatePublisher.Publish(_context.Events, StateScopes.Wiring);
            }

            var world = new SpaceWorld(home) { Sizes = sizes, Uses = () => Judge().Uses() };
            using var answer = JsonDocument.Parse(SystemSpace.Json(SystemSpace.Read(world)));
            return (object?)answer.RootElement.Clone();
        }, cancellationToken);
    }

    // KEEP1e (D195 point 4, the cleanup design §3.3, §4.4): *Free up space…*, listed first, then pressed, through the driver library's
    // `SpaceFree`, which `daoris space free` calls too. `{ plan: true }` answers the list and changes nothing (open on the pipe, a
    // `PipeRule`): per kind what its rule takes now, what could go early in groups, and the doors of the kinds with presses of their own.
    // `{ units, early }` starts a run removing exactly those, each judged again right before it goes, under the home's run lock, and answers
    // at once `{ run, joined }`; a run under way here is joined, and one held elsewhere is refused as data, `{ why: held, held, message }`.
    // The run's progress is `SYSTEM_SPACE`'s `run`; each kind's end and the whole's tell `wiring`. The removal is the person's delete.
    [DriverRoute("SYSTEM_SPACE_FREE", PipeClass.Person, Drain = PipeDrain.Waits)]
    [ClassedBy("SYSTEM_SPACE_FREE", "plan", PipeClass.Open, "true")]
    [Owner("SYSTEM_SPACE_FREE", None = OwnerNone.MachineSetting)]
    [GroundRead("SYSTEM_SPACE_FREE", nameof(PipeReads.SpacePlan))]
    [Publishes("SYSTEM_SPACE_FREE", StateScopes.Harnesses | StateScopes.Tools | StateScopes.Wiring)]
    private Task<object?> FreeSpaceAsync(SpaceFreePress press, CancellationToken cancellationToken)
    {
        var world = SpaceFreeWorldOf();
        if (press.Plan)
        {
            return Task.Run(() =>
            {
                using var answer = JsonDocument.Parse(SpaceFree.Json(SpaceFree.Plan(world, cancellationToken)).ToJsonString());
                return (object?)answer.RootElement.Clone();
            }, cancellationToken);
        }

        var units = SpaceFree.Asked(press.Units, press.Early);
        var start = SpaceRuns.Shared.Start(world, KeepingRun.Press, units, _ => StatePublisher.Publish(_context.Events, StateScopes.Wiring));
        if (start.Run is not { } run)
        {
            var held = start.Held;
            return Task.FromResult<object?>(new
            {
                Run = (string?)null, Joined = false, Why = "held",
                Held = held is null ? null : new { held.Pid, Since = Moments.Write(held.Since), held.Door, held.Gone },
                Message = $"{held?.Said ?? "another run"} holds Daoris's keeping, so nothing was removed; press again once it ends",
            });
        }

        return Task.FromResult<object?>(new { Run = run.Id, start.Joined });
    }

    /// <summary>The press's world here: this home's sizes and downloads' judge, the shell's browser host and the loop's log; a test's changed.</summary>
    private SpaceFreeWorld SpaceFreeWorldOf()
    {
        var loop = _context.Loop;
        var world = new SpaceFreeWorld(loop.Home) { Judge = Judge, Caches = loop.BrowserCaches, Log = loop.Log };
        // KEEPSIZE1b (the keep-size design §5): the press's run resumes the finished trees' steps and posts a record due before it removes.
        if (loop.Service is { } service)
        {
            world = world.WithService(service);
        }

        return _context.SpaceFreeWorlds?.Invoke(world) ?? world;
    }

    /// <summary>The kinds a read asks counted: every kind for <c>measure: true</c>, the named ones for a list, none otherwise.</summary>
    public static IReadOnlyList<string> SpaceAsked(JsonElement? sent)
    {
        if (sent is not { } measure) return [];
        return measure.ValueKind switch
        {
            JsonValueKind.True => [.. SpaceKinds.Table.Select(kind => kind.Id)],
            JsonValueKind.Array => [.. measure.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.String).Select(each => each.GetString()!)
                .Where(id => SpaceKinds.Named(id) is not null)],
            _ => [],
        };
    }

    // CACHECLEAR1: the Downloads row's list, the agents' and tools' downloaded versions with what keeps each, sized as they are
    // listed (a walk of each version's folder, off the window's thread). A read: it declares nothing and removes nothing.
    // KEEP1b: with the rule's days (`days`, null under `never`), and for each version nothing uses when its current unused stretch began
    // (`unusedSince`) and when the rule takes it (`goes`), from `keeping.json`; both absent where no pass has found it unused yet.
    [DriverRoute("SYSTEM_DOWNLOADS", PipeClass.Open)]
    private Task<object?> DownloadsAsync(NoPayload _, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            var home = _context.Loop.Home;
            return (object?)DownloadsAnswer(
                DownloadClearing.List(home, Judge().Uses(), DateTimeOffset.UtcNow, cancellationToken), KeepingRecord.Read(home), KeepPolicy.Of(home).Downloads);
        }, cancellationToken);

    // CACHECLEAR1 (D88's shape): the press after the list. `versions` names the rows the page showed, `{ kind, name, version, unfinished }`;
    // each is judged again from the files as they stand right before it goes, and one kept, held by a program or gone since is said in
    // the answer. What it moves: This Daoris's sizes, and the Tools' and agents' pages that say which versions are downloaded.
    // KEEP1b (D195 point 6): the downloads' one act, each version judged under the use files' locks, held to its rename, and kept while a
    // running session was handed it.
    [DriverRoute("SYSTEM_DOWNLOADS_CLEAR", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SYSTEM_DOWNLOADS_CLEAR", None = OwnerNone.MachineSetting)]
    [GroundRead("SYSTEM_DOWNLOADS_CLEAR", nameof(PipeReads.Downloads))]
    [Publishes("SYSTEM_DOWNLOADS_CLEAR", StateScopes.Harnesses | StateScopes.Tools | StateScopes.Wiring)]
    private Task<object?> ClearDownloadsAsync(DownloadsClearPress press, CancellationToken cancellationToken)
    {
        var asked = DownloadsAsked(press.Versions);
        return Task.Run(async () =>
        {
            var home = _context.Loop.Home;
            var record = KeepingRecord.Read(home);
            var judge = Judge() with { FirstUnused = key => record.Unused.TryGetValue(key, out var since) ? since : null };
            var cleared = await DownloadClearing.ClearAsync(
                home, asked, judge, _context.Loop.Log, cancellationToken: cancellationToken, door: KeepingRun.Press).ConfigureAwait(false);
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

    /// <summary>
    /// How the downloads are judged here (KEEP1b): the agents' wiring as the roster reads it, this home's tools and the loop's choices, each
    /// read as it stands and locked at a version's judgement, and the home's running sessions.
    /// </summary>
    private DownloadJudge Judge()
    {
        var loop = _context.Loop;
        var markers = DaorisHome.Entry(loop.Home, HomeEntry.SessionPids);
        return DownloadJudge.Of(loop.Home, loop.Harnesses.SettingsPath, loop.ConfigPath, token => LiveSessions.ReadAsync(loop.Service, markers, token));
    }

    private static object DownloadsAnswer(DownloadListing listing, KeepingFile record, DownloadsPolicy policy) => new
    {
        listing.Why,
        Days = policy.Days is { } days ? (int?)days.TotalDays : null,
        Versions = listing.Versions.Select(row => new
        {
            row.Kind, row.Name, row.Version, row.Path, row.Bytes, row.Partial, row.Keeps, row.By, row.Unfinished,
            UnusedSince = row.Keeps is null ? DownloadKeeping.UnusedSince(row, record) : null,
            Goes = DownloadKeeping.Goes(row, record, policy),
        }).ToArray(),
    };

    /// <summary>The rows a press names; a call naming none, or a row without its kind, name and version, is malformed.</summary>
    public static IReadOnlyList<DownloadAsked> DownloadsAsked(IReadOnlyList<DownloadVersionAsked>? versions)
    {
        if (versions is null) throw new ArgumentException("`versions` must name the downloaded versions to remove.");
        return [.. versions.Select(each => each is { Kind: "agent" or "tool", Name: { } name, Version: { } version }
            ? new DownloadAsked(each.Kind, name, version, each.Unfinished)
            : throw new ArgumentException("each of `versions` must be `{ kind: agent|tool, name, version }`."))];
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

using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// The programs Daoris runs beside its agents, the page's `bridge/tools.ts` (TOOLS7, D121 §4.1): how each is run — the
/// system's, a managed version, or a file the person names — what is downloaded and what the lists offer for this
/// machine, a download started as an action the page follows and the person stops, a delete, the resource locations
/// listed, added, removed and looked at, and what a switch of git changes before it applies. The same
/// `$DAORIS_HOME/tools.json` the terminal's `daoris tool` edits (D50), which every start reads (TOOLS5).
/// </summary>
/// <remarks>
/// <para>🔴 <b>A download is followed, never waited on</b> (§3.6, WSR7 a): <c>TOOLS_DOWNLOAD</c> and a managed
/// <c>TOOLS_USE</c> answer once the action has started, its lines are the console's under <c>tools:&lt;tool&gt;</c>, and
/// its end is the <c>TOOLS_ENDED</c> news. The bridge gives up after thirty seconds, and a download outlives that.</para>
/// <para>The SSH command Daoris's git carries (§2.5) is TOOLS6's, and joins <c>TOOLS_GIT</c> with it.</para>
/// </remarks>
public sealed partial class DriverModule
{
    // One download, use or update at a time for each tool (§3.6), shared by every route that starts or stops one, so a
    // second press is refused by the driver's own claim and a stop reaches the action the first press started.
    private ToolActions? _toolActions;

    // Each file's answer to its version question, kept while the file is unchanged: asking starts the program, and the
    // page asks for the list after every change.
    private readonly ConcurrentDictionary<string, (DateTime Written, long Length, string? Version, string? Problem)> _versions =
        new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    private ToolActions ToolRuns => LazyInitializer.EnsureInitialized(
        ref _toolActions, () => new ToolActions(_loop.Home, AppContext.BaseDirectory, log: _loop.Log));

    /// <summary>The console a tool's action writes to, as a session's is keyed by its id.</summary>
    private static string ToolConsole(string tool) => $"tools:{tool}";

    // Every tool, as it is run and what could run it: the way and its file, the versions downloaded and offered, the
    // locations and the list built in. Reads files only; `ask` asks each resolved file its version, which starts it.
    [DriverRoute("TOOLS_LIST")]
    private async Task<object?> ToolsListAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var home = _loop.Home;
        var read = Tools.Read(home);
        var lists = ToolResources.ReadLists(home, AppContext.BaseDirectory, read);
        var platform = ToolResources.Current;
        var merged = ToolResources.Merge(lists, platform);
        var ask = Flag(request, "ask");

        var rows = await Task.WhenAll(Tools.Declared.Select(async declared =>
        {
            var id = declared.Id;
            var entry = read.Entries[id];
            var resolution = Tools.Resolve(read, home, id);
            var offers = merged.Tools.First(tool => tool.Tool == id);
            var downloaded = ToolInstall.Downloaded(home, id);
            var running = ToolRuns.Running(id);
            var asked = ask && resolution.File is { } file ? await VersionOfAsync(file, declared, cancellationToken) : default;
            return new
            {
                Tool = id,
                declared.Name,
                Way = WayWord(entry.Way),
                entry.Version,
                entry.File,
                Resolved = resolution.File,
                resolution.Refused,
                resolution.Problem,
                // What the system's way would run: the screen names it before *Use the system's* is pressed.
                System = CommandPresence.Resolve(declared.Answers[0], null, startable: true),
                Asked = asked.Version,
                AskedProblem = asked.Problem,
                Downloaded = downloaded
                    .Select(version => new
                    {
                        Version = version,
                        Size = RecordSize(home, id, version),
                        InUse = entry.Way == ToolWay.Managed && entry.Version == version,
                    })
                    .ToArray(),
                Offered = offers.Versions
                    .Select(offered => new
                    {
                        offered.Version,
                        offered.Size,
                        offered.Archive,
                        offered.Lists,
                        Hosts = offered.Urls.Select(HostOf).Distinct(StringComparer.Ordinal).ToArray(),
                        Downloaded = downloaded.Contains(offered.Version, StringComparer.Ordinal),
                    })
                    .ToArray(),
                RefusedVersions = offers.Refused.Select(refused => new { refused.Version, refused.Field, refused.Lists, refused.Problem }).ToArray(),
                offers.Newest,
                offers.Source,
                Licence = offers.Licence is { } licence ? new { licence.Id, licence.Url } : null,
                offers.Lists,
                Running = running is null ? null : new { running.Action, running.Version },
            };
        }));

        return new
        {
            File = read.Path,
            read.Exists,
            read.Problem,
            read.Notes,
            Platform = platform,
            Tools = rows,
            // The person's locations in order, then the list built in, always last (§3.3).
            Locations = read.Locations.Select((_, at) => ListRow(lists[at], platform)).ToArray(),
            BuiltIn = ListRow(lists[^1], platform),
        };
    }

    // A way chosen on the screen, written to the file the terminal's `daoris tool use` writes. The system's and a named
    // file answer at once; managed starts the driver's use, which downloads a version that is not here first.
    [DriverRoute("TOOLS_USE")]
    private async Task<object?> ToolsUseAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var declared = DeclaredTool(PayloadHelper.GetRequiredValue<string>(request.Payload, "tool"));
        var action = PayloadHelper.GetRequiredValue<string>(request.Payload, "action");
        var home = _loop.Home;

        switch (action)
        {
            case "system":
                Tools.UseSystem(home, declared.Id);
                Used(declared.Id, "system", null);
                return new { Tool = declared.Id, Way = "system" };

            case "file":
            {
                // Checked before it is written (§4.1): a file that is there, by its whole path, that starts and answers a
                // version. A program the person names runs as them on every call, so a wrong one is said now.
                var file = Optional(request, "file") ?? "";
                if (!Tools.IsWholePath(file) || !File.Exists(file))
                {
                    throw Refusals.Because(
                        Refusals.ToolFileMissing,
                        $"{declared.Name} is not set to `{file}`: name a file that is there by its whole path.",
                        ("tool", declared.Name), ("file", file));
                }

                var (version, problem) = await VersionOfAsync(file, declared, cancellationToken);
                if (version is null)
                {
                    throw Refusals.Because(
                        Refusals.ToolFileNoVersion,
                        $"{file} did not answer its version, so {declared.Name} was not set to it: {problem}",
                        ("tool", declared.Name), ("file", file), ("reason", problem ?? ""));
                }

                Tools.UseFile(home, declared.Id, file);
                Used(declared.Id, "file", null);
                return new { Tool = declared.Id, Way = "file", File = file, Version = version };
            }

            case "managed":
                return StartTool(declared, "use", Optional(request, "version"));

            default:
                throw new DriverException($"`{action}` is not a way a tool is run — one of: system, managed, file.");
        }
    }

    // Fetch and verify one version, nothing switching (§4.1 *Download*): answered once it has started.
    [DriverRoute("TOOLS_DOWNLOAD")]
    private object? ToolsDownload(IpcRequest request) =>
        StartTool(DeclaredTool(PayloadHelper.GetRequiredValue<string>(request.Payload, "tool")), "download", Optional(request, "version"));

    // The person's stop. `stopped: false` is an answer: nothing ran to stop, and a stop that went nowhere must not look
    // delivered. The action's end is the news either way.
    [DriverRoute("TOOLS_STOP")]
    private object? ToolsStop(IpcRequest request)
    {
        var tool = DeclaredTool(PayloadHelper.GetRequiredValue<string>(request.Payload, "tool")).Id;
        return new { Tool = tool, Stopped = ToolRuns.Cancel(tool) };
    }

    // A downloaded version nothing uses, removed (§3.6): the one in use refuses, and so does one the system holds.
    [DriverRoute("TOOLS_DELETE")]
    private object? ToolsDelete(IpcRequest request)
    {
        var declared = DeclaredTool(PayloadHelper.GetRequiredValue<string>(request.Payload, "tool"));
        var version = PayloadHelper.GetRequiredValue<string>(request.Payload, "version");
        try
        {
            var folder = ToolInstall.Delete(_loop.Home, declared.Id, version);
            return new { Tool = declared.Id, Version = version, Folder = folder };
        }
        catch (ToolRefusal refusal) when (refusal.Check is "missing" or "in-use" or "held")
        {
            throw refusal.Check switch
            {
                "missing" => Refusals.Because(
                    Refusals.ToolNotDownloaded, refusal.Message, ("tool", declared.Name), ("version", version)),
                "in-use" => Refusals.Because(
                    Refusals.ToolInUse, refusal.Message, ("tool", declared.Name), ("version", version)),
                _ => Refusals.Because(
                    Refusals.ToolHeld, refusal.Message, ("tool", declared.Name), ("version", version), ("reason", refusal.Message)),
            };
        }
    }

    // *Look for updates* (§3.7): each location fetched on the person's press, each bounded, its copy kept only when it
    // reads. The page waits by `hostBounds`, as long as the look may take.
    [DriverRoute("TOOLS_LOOK")]
    private async Task<object?> ToolsLookAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var looks = await ToolInstall.LookAsync(_loop.Home, ToolResources.Current, cancellationToken, log: _loop.Log);
        return new
        {
            Looks = looks.Select(look => new { look.Address, look.Outcome, look.Sentence, look.Added, look.Dropped }).ToArray(),
        };
    }

    // A resource location added at the end of the person's, or removed (§3.3): the file the terminal's
    // `daoris tool locations add|remove` edits. Removing one leaves every version already downloaded.
    [DriverRoute("TOOLS_LOCATION")]
    private object? ToolsLocation(IpcRequest request)
    {
        var action = PayloadHelper.GetRequiredValue<string>(request.Payload, "action");
        var address = PayloadHelper.GetRequiredValue<string>(request.Payload, "address").Trim();
        switch (action)
        {
            case "add":
                if (Tools.LocationProblem(address) is { } problem)
                {
                    throw Refusals.Because(Refusals.ToolLocationRefused, problem, ("address", address));
                }

                return new { Address = address, Added = Tools.AddLocation(_loop.Home, address) };

            case "remove":
                return new { Address = address, Removed = Tools.RemoveLocation(_loop.Home, address) };

            default:
                throw new DriverException($"`{action}` is not a change to the resource locations — one of: add, remove.");
        }
    }

    // What a switch of git changes, said before it applies (§4.1): the checkout keys the git that runs now and the git
    // the switch would run read differently, from each one's own `config --system --list`. Asked of both programs, so
    // a target that cannot run is refused before anything is asked.
    [DriverRoute("TOOLS_GIT")]
    private async Task<object?> ToolsGitAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var way = PayloadHelper.GetRequiredValue<string>(request.Payload, "way");
        var home = _loop.Home;
        var git = DeclaredTool("git");
        var now = Tools.Resolve(home, git.Id);

        string? then;
        string? thenProblem = null;
        switch (way)
        {
            case "system":
                then = CommandPresence.Resolve(git.Answers[0], null, startable: true);
                if (then is null) thenProblem = "`git` is not on this machine's PATH";
                break;

            case "managed":
            {
                var version = Optional(request, "version") ?? throw new DriverException("a managed git is compared at a version.");
                var (file, problem) = Tools.ManagedExecutable(home, git.Id, version);
                then = file ?? throw Refusals.Because(Refusals.ToolNotDownloaded, problem!, ("tool", git.Name), ("version", version));
                break;
            }

            case "file":
            {
                var named = Optional(request, "file") ?? "";
                if (!Tools.IsWholePath(named) || !File.Exists(named))
                {
                    throw Refusals.Because(
                        Refusals.ToolFileMissing,
                        $"{git.Name} is not set to `{named}`: name a file that is there by its whole path.",
                        ("tool", git.Name), ("file", named));
                }

                then = named;
                break;
            }

            default:
                throw new DriverException($"`{way}` is not a way a tool is run — one of: system, managed, file.");
        }

        if (now.File is not null && then is not null && SamePath(now.File, then))
        {
            return new { Now = new { now.File, Problem = (string?)null }, Then = new { File = then, Problem = (string?)null }, Keys = Array.Empty<object>(), Same = true };
        }

        var (nowKeys, nowRead) = now.File is null ? (null, now.Problem) : await SystemConfigAsync(now.File, cancellationToken);
        var (thenKeys, thenRead) = then is null ? (null, thenProblem) : await SystemConfigAsync(then, cancellationToken);

        // Compared only where both answered: a side that could not say would read as every key differing.
        var keys = nowKeys is not null && thenKeys is not null
            ? ToolQuestions.Differences(nowKeys, thenKeys).Select(pair => (object)new { pair.Key, pair.Now, pair.Then }).ToArray()
            : [];
        return new
        {
            Now = new { now.File, Problem = nowKeys is null ? nowRead : null },
            Then = new { File = then, Problem = thenKeys is null ? thenRead : null },
            Keys = keys,
            Same = false,
        };
    }

    // *Browse…* (§4.1): the system's file picker, which the application hands in. Null is the person cancelling.
    [DriverRoute("TOOLS_PICK")]
    private object? ToolsPick(IpcRequest request) =>
        _pickFile is null
            ? new { Can = false, File = (string?)null }
            : new { Can = true, File = _pickFile(Optional(request, "title")) };

    /// <summary>
    /// A download or a use, started through the driver's actions (§3.6) and answered once it has started; a version
    /// already here switches in the answer itself. A plan's refusal is a code the page translates.
    /// </summary>
    private object StartTool(ToolDeclaration declared, string action, string? version)
    {
        var console = ToolConsole(declared.Id);
        ToolRun run;
        try
        {
            run = ToolRuns.Start(declared.Id, action, version, line => _loop.Output.Append(console, line));
        }
        catch (ToolRefusal refusal)
        {
            // The file's own refusals (`file`, `machine`) travel as the driver's words, which name the file to fix.
            Exception mapped = refusal.Check switch
            {
                "busy" => Refusals.Because(Refusals.ToolBusy, refusal.Message, ("tool", declared.Name)),
                "unknown" or "version" or "platform" => Refusals.Because(
                    Refusals.ToolVersionUnknown, refusal.Message, ("tool", declared.Name), ("version", version ?? "")),
                "conflict" => Refusals.Because(
                    Refusals.ToolDownloadRefused, refusal.Message,
                    ("tool", declared.Name), ("version", version ?? ""), ("check", refusal.Check), ("message", refusal.Message)),
                _ => refusal,
            };
            throw mapped;
        }

        if (run.Ended.IsCompleted)
        {
            var end = run.Ended.Result;
            if (end.ExitCode == 0 && action != "download" && end.Version is not null) Used(declared.Id, "managed", end.Version);
            return new { Tool = declared.Id, Action = action, Started = false, run.Version, Ended = EndOf(declared, end) };
        }

        _ = FollowToolAsync(declared, action, run, console);
        return new { Tool = declared.Id, Action = action, Started = true, run.Version };
    }

    /// <summary>A tool's action to its end, and the end announced, after the request that started it was answered.</summary>
    private async Task FollowToolAsync(ToolDeclaration declared, string action, ToolRun run, string console)
    {
        var end = await run.Ended.ConfigureAwait(false);
        _loop.Output.Close(console);
        if (end.ExitCode == 0 && action != "download" && end.Version is not null) Used(declared.Id, "managed", end.Version);
        try
        {
            await _events.EmitAsync("DAORIS", "TOOLS_ENDED", EndOf(declared, end)).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Nobody awaits this: the next list says what is downloaded and what runs.
        }
    }

    private static object EndOf(ToolDeclaration declared, ToolEnd end) => new
    {
        end.Tool,
        declared.Name,
        end.Action,
        end.Version,
        end.ExitCode,
        end.Check,
        end.Problem,
        end.Stopped,
        end.Nothing,
    };

    /// <summary>A tool's way changed, in the machine log (§3.6): the tool and the way, never a path the person named.</summary>
    private void Used(string tool, string way, string? version) =>
        _loop.Log?.Info("tool.used", ("tool", tool), ("way", way), ("version", version));

    /// <summary>A declared tool by its id, or the driver's own refusal naming the ones there are.</summary>
    private static ToolDeclaration DeclaredTool(string id) =>
        Tools.Find(id) ?? throw new DriverException(
            $"`{id}` is not a tool this build runs — one of: {string.Join(", ", Tools.Declared.Select(tool => tool.Id))}.");

    private static string? WayWord(ToolWay? way) => way switch
    {
        ToolWay.System => "system",
        ToolWay.Managed => "managed",
        ToolWay.File => "file",
        _ => null,
    };

    /// <summary>
    /// The version a file answers to its tool's version question, kept while the file is unchanged. An answer that did
    /// not come in time is not kept: a slow first start is not the program's answer.
    /// </summary>
    private async Task<(string? Version, string? Problem)> VersionOfAsync(string file, ToolDeclaration tool, CancellationToken ct)
    {
        var info = new FileInfo(file);
        if (!info.Exists) return (null, $"there is no file at {file}");
        if (_versions.TryGetValue(file, out var held) && held.Written == info.LastWriteTimeUtc && held.Length == info.Length)
        {
            return (held.Version, held.Problem);
        }

        var answer = await ToolQuestions.AskAsync(file, tool.Version, ct);
        var version = answer.Started ? ToolQuestions.VersionIn(answer.Output) : null;
        var problem = answer.Problem
            ?? (version is null ? $"{file} answered no version to `{string.Join(' ', tool.Version)}`" : null);
        if (!answer.Started || answer.ExitCode is not null) _versions[file] = (info.LastWriteTimeUtc, info.Length, version, problem);
        return (version, problem);
    }

    /// <summary>
    /// A git's own system configuration, or why it could not say. A git with no system file answers that it cannot read
    /// one, which is a configuration of nothing, not a failure.
    /// </summary>
    private static async Task<(IReadOnlyDictionary<string, string>? Keys, string? Problem)> SystemConfigAsync(string git, CancellationToken ct)
    {
        var answer = await ToolQuestions.AskAsync(git, ["config", "--system", "--list"], ct);
        if (answer.Problem is not null) return (null, answer.Problem);
        if (answer.ExitCode is not 0)
        {
            return answer.Output.Contains("unable to read config file", StringComparison.OrdinalIgnoreCase)
                ? (new Dictionary<string, string>(), null)
                : (null, $"{git} answered {answer.ExitCode}: {answer.Output.Trim().Split('\n')[0].Trim()}");
        }

        return (ToolQuestions.ConfigIn(answer.Output), null);
    }

    /// <summary>A downloaded version's size, as its record says it was downloaded.</summary>
    private static long? RecordSize(string home, string tool, string version)
    {
        try
        {
            var record = JsonNode.Parse(File.ReadAllText(Path.Combine(ToolInstall.VersionFolder(home, tool, version), Tools.Record)));
            return record?["size"] is JsonValue size && size.TryGetValue<long>(out var bytes) ? bytes : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>One list as the screen shows it (§3.3): what vouches for it, when it was fetched, what it names here.</summary>
    private static object ListRow(ResourceList list, string? platform)
    {
        var builtIn = list.Origin == ToolResources.BuiltIn;
        return new
        {
            Address = builtIn ? null : list.Origin,
            list.Integrity,
            list.Exists,
            Fetched = !builtIn && list.Exists && list.Path is { } path ? File.GetLastWriteTimeUtc(path) : (DateTime?)null,
            list.Sha256,
            Names = Names(list, platform),
            list.Unknown,
            list.Problem,
            list.Notes,
        };
    }

    /// <summary>What a list names for this machine, <c>&lt;tool&gt; &lt;version&gt;</c>, in the declared order and newest first.</summary>
    private static string[] Names(ResourceList list, string? platform)
    {
        if (platform is null) return [];
        var newestFirst = Comparer<string>.Create((a, b) => ToolResources.CompareVersions(b, a));
        return
        [
            .. Tools.Declared.SelectMany(tool => list.Tools.TryGetValue(tool.Id, out var named)
                ? named.Versions.Where(pair => pair.Value.ContainsKey(platform)).Select(pair => pair.Key).Order(newestFirst)
                    .Select(version => $"{tool.Id} {version}")
                : []),
        ];
    }

    private static string HostOf(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : url;

    private static bool SamePath(string a, string b) => string.Equals(
        Path.GetFullPath(a), Path.GetFullPath(b), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}

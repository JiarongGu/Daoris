using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Shenora.Core.Events;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

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
public sealed class ToolsRoutes(DriverContext context)
{
    private readonly DriverLoop _loop = context.Loop;
    private readonly IEventBus _events = context.Events;
    private readonly PickFile? _pickFile = context.PickFile;

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
    // locations and the list built in. Reads files only; `ask` asks each resolved file its version, which starts it. Told by
    // event (NOBLOCK1): its files and the system's path held the window up to 1.4 s, so an ask is answered from what is held.
    [DriverRoute("TOOLS_LIST", PipeClass.Open)]
    [ByEvent("TOOLS_LIST", Untold = "the system's path and an action's end, which TOOLS_ENDED tells")]
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
            // The settings Daoris's git carries (TOOLS6): the page's git card and the press's grounds read them here.
            Git = read.Git,
            // Settings → Git (GITAUTH1e): the identities and what points at them, from the files alone.
            Identities = GitIdentityPage.Read(home),
            Tools = rows,
            // The person's locations in order, then the list built in, always last (§3.3).
            Locations = read.Locations.Select((_, at) => ListRow(lists[at], platform)).ToArray(),
            BuiltIn = ListRow(lists[^1], platform),
        };
    }

    // A way chosen on the screen, written to the file the terminal's `daoris tool use` writes. The system's and a named
    // file answer at once; managed starts the driver's use, which downloads a version that is not here first. Each press that
    // writes what the list reads declares the tools (STATEHUB1h, D169); an action's end is `TOOLS_ENDED`, as before.
    [DriverRoute("TOOLS_USE", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("TOOLS_USE", None = OwnerNone.MachineSetting)]
    [GroundRead("TOOLS_USE", nameof(PipeReads.ToolChoice))]
    [NotAlready("TOOLS_USE", "checks a file's version or downloads a managed one")]
    [Publishes("TOOLS_USE", StateScopes.Tools)]
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
                // version. The library's operation (PUSH1f2c), which `daoris tool use … file` calls alone; its coded
                // refusals are the module's one mapping's (ToolCodes).
                var file = Optional(request, "file") ?? "";
                var version = await ToolChoices.UseFileAsync(home, declared.Id, file, VersionOfAsync, cancellationToken);
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
    [DriverRoute("TOOLS_DOWNLOAD", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("TOOLS_DOWNLOAD", None = OwnerNone.MachineSetting)]
    [GroundRead("TOOLS_DOWNLOAD", nameof(PipeReads.ToolVersions))]
    [Publishes("TOOLS_DOWNLOAD", StateScopes.Tools)]
    private object? ToolsDownload(IpcRequest request) =>
        StartTool(DeclaredTool(PayloadHelper.GetRequiredValue<string>(request.Payload, "tool")), "download", Optional(request, "version"));

    // The person's stop. `stopped: false` is an answer: nothing ran to stop, and a stop that went nowhere must not look
    // delivered. The action's end is the news either way, so the stop declares nothing.
    [DriverRoute("TOOLS_STOP", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("TOOLS_STOP", None = OwnerNone.MachineSetting)]
    [GroundRead("TOOLS_STOP", nameof(PipeReads.ToolRunning))]
    private object? ToolsStop(IpcRequest request)
    {
        var tool = DeclaredTool(PayloadHelper.GetRequiredValue<string>(request.Payload, "tool")).Id;
        return new { Tool = tool, Stopped = ToolRuns.Cancel(tool) };
    }

    // A downloaded version nothing uses, removed (§3.6): the one in use refuses, and so does one the system holds. Its coded refusals
    // (missing, in use, held) are the module's one mapping's (ToolCodes, PUSH1f2c), the codes `daoris tool delete` says alone.
    // KEEP1b (D195 point 6): through the downloads' one act, judged under the use files' locks against the running sessions, moved aside
    // first; off the window's thread, since it asks the service and walks the version.
    [DriverRoute("TOOLS_DELETE", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("TOOLS_DELETE", None = OwnerNone.MachineSetting)]
    [GroundRead("TOOLS_DELETE", nameof(PipeReads.ToolVersions))]
    [Publishes("TOOLS_DELETE", StateScopes.Tools)]
    private async Task<object?> ToolsDeleteAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var declared = DeclaredTool(PayloadHelper.GetRequiredValue<string>(request.Payload, "tool"));
        var version = PayloadHelper.GetRequiredValue<string>(request.Payload, "version");
        var home = _loop.Home;
        var folder = await Task.Run(() => ToolInstall.DeleteAsync(
            home, declared.Id, version, token => LiveSessions.ReadAsync(_loop.Service, DaorisHome.Entry(home, HomeEntry.SessionPids), token),
            _loop.Harnesses.SettingsPath, _loop.ConfigPath, _loop.Log, ct: cancellationToken), cancellationToken).ConfigureAwait(false);
        return new { Tool = declared.Id, Version = version, Folder = folder };
    }

    // *Look for updates* (§3.7): each location fetched on the person's press, each bounded, its copy kept only when it
    // reads. The page waits by `hostBounds`, as long as the look may take. Unlike a look that only fetches, the copy it keeps is
    // what the list offers, so it declares the tools.
    [DriverRoute("TOOLS_LOOK", PipeClass.Open)]
    [Publishes("TOOLS_LOOK", StateScopes.Tools)]
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
    [DriverRoute("TOOLS_LOCATION", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("TOOLS_LOCATION", None = OwnerNone.MachineSetting)]
    [GroundRead("TOOLS_LOCATION", nameof(PipeReads.ToolLocations))]
    [NotAlready("TOOLS_LOCATION", "edits the list of locations in place")]
    [Publishes("TOOLS_LOCATION", StateScopes.Tools)]
    private object? ToolsLocation(IpcRequest request)
    {
        var action = PayloadHelper.GetRequiredValue<string>(request.Payload, "action");
        var address = PayloadHelper.GetRequiredValue<string>(request.Payload, "address").Trim();
        switch (action)
        {
            case "add":
                // An address that is no resource location is the operation's refusal (PUSH1f2c), said in its code by the module.
                return new { Address = address, Added = ToolChoices.AddLocation(_loop.Home, address) };

            case "remove":
                return new { Address = address, Removed = Tools.RemoveLocation(_loop.Home, address) };

            default:
                throw new DriverException($"`{action}` is not a change to the resource locations — one of: add, remove.");
        }
    }

    // A setting Daoris's git carries, set or cleared (TOOLS6, D121 §2.5; GITAUTH1e): the key on the allow-list, and a value only on a git
    // that reads it, else `TOOL_GIT_TOO_OLD` naming both versions. No value clears it. The file it travels in is written at the next start.
    [DriverRoute("TOOLS_GIT_SET", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("TOOLS_GIT_SET", None = OwnerNone.MachineSetting)]
    [GroundRead("TOOLS_GIT_SET", nameof(PipeReads.GitSetting))]
    [Publishes("TOOLS_GIT_SET", StateScopes.Tools)]
    private async Task<object?> ToolsGitSetAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var key = PayloadHelper.GetRequiredValue<string>(request.Payload, "key");
        var value = Optional(request, "value");
        var version = await ToolChoices.SetGitAsync(_loop.Home, key, string.IsNullOrWhiteSpace(value) ? null : value.Trim(),
            GitVersions.AskAsync, cancellationToken);
        return new { Key = key, Value = string.IsNullOrWhiteSpace(value) ? null : value.Trim(), Version = version?.ToString() };
    }

    // Settings → Git (GITAUTH1e, the git-auth design §7.1-§7.2): one act on an identity — add (ssh or https), test, rename, commits-as,
    // read-only, remove, default — the screen's half of `daoris git identity` and `git default`, over the same typed operations.
    // The page is the person's own press, so a widening is confirmed in place before it is sent. A change lifts every hold, so each
    // remote is tried once with what now serves it (§5.2).
    [DriverRoute("GIT_IDENTITY", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("GIT_IDENTITY", None = OwnerNone.MachineSetting)]
    [GroundRead("GIT_IDENTITY", nameof(PipeReads.GitIdentity))]
    [NotAlready("GIT_IDENTITY", "adds, tests, changes or removes one identity of the machine's set, which holds the secret apart")]
    [Publishes("GIT_IDENTITY", StateScopes.Tools)]
    private async Task<object?> GitIdentityAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var home = _loop.Home;
        var action = PayloadHelper.GetRequiredValue<string>(request.Payload, "action");
        var name = Optional(request, "identity");

        switch (action)
        {
            case "add-ssh":
            {
                var identity = await GitIdentityActs.AddSshAsync(home, Needed(request, "host"), Needed(request, "label"), Optional(request, "name"),
                    Optional(request, "email"), Optional(request, "import"), Flag(request, "rsa"), cancellationToken).ConfigureAwait(false);
                GitHolds.LiftAll(home);
                return GitAdded(home, identity);
            }

            case "add-https":
            {
                var identity = await GitIdentityHttps.AddHttpsAsync(home, Needed(request, "host"), Needed(request, "label"), Optional(request, "user"),
                    Optional(request, "name"), Optional(request, "email"), Optional(request, "token"), "https", cancellationToken).ConfigureAwait(false);
                GitHolds.LiftAll(home);
                return GitAdded(home, identity);
            }

            case "test":
            {
                var target = GitIdentityActs.Find(home, name ?? throw new DriverException("name the git identity."));
                var answer = target.Way == GitIdentity.Https
                    ? await GitIdentityHttps.TestHttpsAsync(home, name, Needed(request, "repository"), cancellationToken).ConfigureAwait(false)
                    : await GitIdentityActs.TestAsync(home, name, Optional(request, "trust"), cancellationToken).ConfigureAwait(false);
                if (answer.Result == "reached") GitHolds.LiftAll(home, "test");
                return new
                {
                    answer.Result, answer.User, answer.Words,
                    HostKeys = answer.HostKeys.Select(key => new { Algorithm = key.Key.Split(' ')[0], key.Fingerprint, key.Published }).ToArray(),
                };
            }

            case "rename":
                return GitShown(GitIdentityActs.Rename(home, name ?? throw new DriverException("name the git identity."), Needed(request, "label")));

            case "commits-as":
                return GitShown(Flag(request, "clear")
                    ? GitIdentityActs.CommitsAs(home, name ?? throw new DriverException("name the git identity."), null, null)
                    : GitIdentityActs.CommitsAs(home, name ?? throw new DriverException("name the git identity."), Optional(request, "name"), Optional(request, "email")));

            case "read-only":
                return GitShown(GitIdentityActs.ReadOnly(home, name ?? throw new DriverException("name the git identity."), Flag(request, "on")));

            case "remove":
            {
                var pointing = await GitIdentityHttps.RemoveAsync(home, name ?? throw new DriverException("name the git identity."), cancellationToken).ConfigureAwait(false);
                GitHolds.LiftAll(home);
                return new { Pointing = pointing };
            }

            case "default":
            {
                var chosen = GitIdentityActs.Default(home, Needed(request, "host"), name);
                GitHolds.LiftAll(home);
                return new { Identity = chosen?.Id };
            }

            default:
                throw new DriverException($"`{action}` is not an act on a git identity.");
        }
    }

    // A repository's remote, a workspace's host or this machine's host pointed at an identity, at the person's own sign-ins, or cleared
    // (`daoris git use`). Two remotes at one address must point at one identity: the operation refuses otherwise.
    [DriverRoute("GIT_USE", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("GIT_USE", Repository = "repository", Workspace = "workspace")]
    [GroundRead("GIT_USE", nameof(PipeReads.GitPointers))]
    [NotAlready("GIT_USE", "writes one pointer into the choices of a repository's remote, a workspace's host or the machine's host")]
    [Publishes("GIT_USE", StateScopes.Tools | StateScopes.Driver)]
    private async Task<object?> GitUseAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var scope = PayloadHelper.GetRequiredValue<string>(request.Payload, "scope");
        await UseGitAsync(_loop.Home, _loop.Service, scope, Optional(request, "repository"), Optional(request, "workspace"), Optional(request, "remote"),
            Optional(request, "host"), Optional(request, "value"), cancellationToken).ConfigureAwait(false);
        return new { Scope = scope };
    }

    /// <summary>
    /// The choice <c>GIT_USE</c> makes, shared with Ask Daoris's <c>git-use</c> door (GITAUTH1e): a repository's remote, a workspace's
    /// host or this machine's host pointed at an identity, at the person's own sign-ins or cleared.
    /// </summary>
    internal static async Task UseGitAsync(
        string home, ServiceClient? service, string scope, string? repository, string? workspace, string? remote, string? host, string? value,
        CancellationToken cancellationToken)
    {
        switch (scope)
        {
            case "machine":
                if (value is not (null or GitIdentityUse.Own)) throw new DriverException("this machine chooses the person's own sign-ins or nothing for a host; a host's identity is its default.");
                GitIdentityActs.UseMachine(home, host ?? throw new DriverException("`host` is needed."), value is not null);
                break;

            case "workspace":
                GitIdentityActs.UseWorkspace(home, workspace ?? throw new DriverException("`workspace` is needed."), host ?? throw new DriverException("`host` is needed."), value);
                break;

            case "repository":
            {
                if (repository is null) throw new DriverException("`repository` is needed.");
                if (service is null) throw new DriverException("Daoris is not ready to read its repositories yet.");
                var registry = await service.RegistryAsync(cancellationToken).ConfigureAwait(false);
                var rows = LinesRoutes.Checkouts(registry, repository, workspace);
                var root = rows.Select(row => row.Root).FirstOrDefault(each => !string.IsNullOrEmpty(each) && Directory.Exists(each))
                    ?? throw new DriverException($"{repository} has no checkout here.");
                var addresses = await GitRemoteRows.AddressesAsync(root, cancellationToken).ConfigureAwait(false);
                // Every checkout, as the verb remembers them: the file is written whole, so a lone repository's would drop the rest.
                GitFolders.Remember(home, LinesRoutes.Checkouts(registry, null, null).Select(row => (row.Root!, row.Repository, row.Workspace ?? "")));
                GitIdentityActs.UseRepository(home, repository, remote ?? "origin", value, addresses);
                break;
            }

            default:
                throw new DriverException($"`{scope}` is not a scope a git identity is chosen for.");
        }

        GitHolds.LiftAll(home, "pointer");
    }

    // A git go-ahead answered (GITAUTH1f, the git-auth design §6.6): its row's Allow or Refuse in What needs you and on its session's head,
    // and `daoris git answer` through the window. A door that waits takes the answer; for one that has gone an Allow is kept as a grant for
    // the identical retry. `{ ask, answer, waiting }`, waiting whether the door was still there. The row goes as the slice hears the file.
    [DriverRoute("GIT_ANSWER", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("GIT_ANSWER", None = OwnerNone.ActsOnNothing)]
    [GroundWait("GIT_ANSWER", "git-ask", "ask")]
    [Publishes("GIT_ANSWER", StateScopes.Tools)]
    private Task<object?> GitAnswerAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var ask = Needed(request, "ask");
        var answer = Needed(request, "answer");
        if (answer is not ("allow" or "refuse")) throw new DriverException("a git go-ahead is answered allow or refuse.");
        var waiting = GitAsks.Answer(_loop.Home, ask, answer == "allow", DateTimeOffset.UtcNow);
        return Task.FromResult<object?>(new { Ask = ask, Answer = answer, Waiting = waiting });
    }

    private static string Needed(IpcRequest request, string name) =>
        Optional(request, name) ?? throw new DriverException($"`{name}` is needed.");

    private static object GitAdded(string home, GitIdentity identity) => new
    {
        Identity = identity.Id, identity.Label, identity.Host, identity.Way,
        PublicKey = identity.Way == GitIdentity.Ssh ? GitKeys.PublicKey(home, identity.Id) : null,
    };

    private static object GitShown(GitIdentity identity) => new { Identity = identity.Id, identity.Label };

    // What a switch of git changes, said before it applies (§4.1): the checkout keys the git that runs now and the git
    // the switch would run read differently, from each one's own `config --system --list`. Asked of both programs, so
    // a target that cannot run is refused before anything is asked.
    [DriverRoute("TOOLS_GIT", PipeClass.Person, Drain = PipeDrain.Passes)]
    [ClassedBy("TOOLS_GIT", "way", PipeClass.Open, "\"system\"", "\"managed\"")]
    [Owner("TOOLS_GIT", None = OwnerNone.MachineSetting)]
    [GroundFiles("TOOLS_GIT", "file", "file", Quick = true)]
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
    [DriverRoute("TOOLS_PICK", PipeClass.Window)]
    [Owner("TOOLS_PICK", None = OwnerNone.NamesNothing)]
    private object? ToolsPick(IpcRequest request) =>
        _pickFile is null
            ? new { Can = false, File = (string?)null }
            : new { Can = true, File = _pickFile(Optional(request, "title"), Optional(request, "window")) };

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
        // The words ToolQuestions.VersionOfAsync says for a file that answered none, which `daoris tool use … file` asks alone.
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

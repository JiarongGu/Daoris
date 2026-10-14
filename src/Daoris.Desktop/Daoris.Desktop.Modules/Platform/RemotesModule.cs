using Daoris.Driver;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// The machine's wiring, as a surface (D50, workspace design §2b): which deployment serves each
/// workspace on this machine. The page asks, this edits the home's `remotes.json` — the same file
/// `daoris remote` edits and the same file the sync loop reads.
/// </summary>
/// <remarks>
/// <para><b>The file is the API; this is an editor.</b> Exactly the shape `driver.json` already
/// proved: hand-editing keeps working, a CLI verb and a checkbox are two doors onto one truth, and
/// nothing here is the only way to say anything.</para>
///
/// <para><b>A key goes IN and never comes OUT.</b> The person may type one here — this is the desktop,
/// on their own machine, and a native dialog would be a worse way to paste a long token — but `STATE`
/// answers only the audit prefix a deployment's own `keys list` shows. A page that could read the key
/// back would put it in a render tree, a devtools panel, and eventually a screenshot.</para>
///
/// <para><b>It lives in the shell rather than behind an HTTP route</b> for the same reason the folder
/// picker does: this is machine-local state, and the service deliberately has no door onto it — a
/// browser over a keyed remote must never be able to read, or re-point, where a machine syncs.</para>
/// </remarks>
public sealed class RemotesModule(IEventBus events, DriverLoop loop) : ModuleBase(events: events)
{
    public override string ModuleName => "DAORIS.REMOTES";

    /// <summary>
    /// Where the map is. Resolved once per call rather than cached: the person may edit the file by
    /// hand between calls, and a surface that answered from a cache would report its own last write.
    /// </summary>
    private static string Path =>
        // With no home there is no map to edit, and the home's own sentence says what to set (D63).
        RemoteTarget.ResolvePath() ?? DaorisHome.Require("remotes.json");

    protected override Task<object?> RouteMessageAsync(
        IpcRequest request, IModuleContext context, CancellationToken cancellationToken)
    {
        switch (request.Type)
        {
            case "STATE":
                return Task.FromResult<object?>(State());

            case "SET":
            {
                var workspace = RemoteTarget.Workspace(
                    PayloadHelper.GetRequiredValue<string>(request.Payload, "workspace"));
                var url = PayloadHelper.GetRequiredValue<string>(request.Payload, "url").TrimEnd('/');
                var key = PayloadHelper.GetRequiredValue<string>(request.Payload, "key");
                if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key))
                {
                    // Half a pair is no remote in every loader (D48 §5) — refused here rather than
                    // written, so the file never holds an entry that silently does nothing.
                    throw Refusals.Because(
                        Refusals.RemoteHalfDeclared,
                        $"`{workspace}` needs both an address and a key: a half-declared remote is no remote, "
                        + "and one written into the map would simply be skipped.",
                        ("workspace", workspace));
                }

                var remotes = new Dictionary<string, RemoteTarget>(
                    RemoteTarget.LoadFile(Path), StringComparer.OrdinalIgnoreCase)
                {
                    [workspace] = new(url, key),
                };
                RemoteTarget.Save(Path, remotes);

                // The loop re-reads the map on its next pass; nudging means the person sees the first
                // sync of a workspace they just wired, rather than waiting out a poll interval.
                loop.Nudge();
                return Task.FromResult<object?>(State());
            }

            case "REMOVE":
            {
                var workspace = RemoteTarget.Workspace(
                    PayloadHelper.GetRequiredValue<string>(request.Payload, "workspace"));
                var remotes = new Dictionary<string, RemoteTarget>(
                    RemoteTarget.LoadFile(Path), StringComparer.OrdinalIgnoreCase);

                // False is an answer, not an error: the end state is the one that was asked for.
                if (remotes.Remove(workspace)) RemoteTarget.Save(Path, remotes);
                loop.Nudge();
                return Task.FromResult<object?>(State());
            }

            default:
                throw UnknownType(request);
        }
    }

    /// <summary>
    /// What is wired, and whether the FILE is what decides. With the environment pair set, the file is
    /// not read by any loader — so a surface that showed the file's rows would be showing wiring that
    /// is not in effect, which is worse than showing none.
    /// </summary>
    private object State()
    {
        var path = Path;
        var live = RemoteTarget.Load();
        var fromEnvironment =
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(RemoteTarget.UrlVariable))
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(RemoteTarget.KeyVariable));

        return new
        {
            Path = path,
            FromEnvironment = fromEnvironment,
            Remotes = live
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => new
                {
                    Workspace = entry.Key,
                    entry.Value.Url,
                    Key = RemoteTarget.Redact(entry.Value.Key),
                })
                .ToArray(),
        };
    }
}

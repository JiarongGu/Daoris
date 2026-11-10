using System.Text.Json.Nodes;
using Daoris.Contracts.Registry;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>
/// The registry and the index over it: a repository's description of itself, its retirement and re-wiring, the bootstrap scan, the code
/// map and the rescan on both hosts, and the retires a machine's checkouts owe a circle on a local host.
/// </summary>
public sealed class RegistryRoutes(HostWiring host) : IRouteModule
{
    private ComposedService Service => host.Service;

    public void Map(RouteMap routes)
    {
        routes.Map<RegisterRepository, RegisteredResponse>(RegisterAsync);
        routes.Map<RetireRepository, RetiredResponse>(RetireAsync);
        routes.Map<WireRepository, RegisteredResponse>(WireAsync);
        routes.Map<ImportRepositories, ImportedResponse>(ImportAsync);
        routes.Map<ListRegistry, RegistrationResponse[]>(ListAsync);
        routes.Map<ReadCodeMap, JsonObject>(CodeMapAsync);
        routes.Map<RefreshIndex, RefreshResponse>(RefreshAsync);

        if (host.Mode != ServiceMode.Local) return;

        // The retires this machine's checkouts owe a circle: what the driver's pass carries to that circle's deployment, then
        // clears. The store writes them as the row leaves, so no door that retires, re-wires or re-registers has to remember to.
        routes.Map<ReadRetiredPending, RetiredPendingResponse>(async (request, _, ct) =>
        {
            var circle = Workspaces.Normalize(request.Workspace);
            return new RetiredPendingResponse(circle, await Service.Service.RetiredAsync(circle, ct));
        });
        routes.Map<ClearRetired, RetiredResponse>(async (request, _, ct) =>
        {
            var circle = Workspaces.Normalize(request.Workspace);
            var cleared = await Service.Service.ClearRetiredAsync(request.Repository, circle, ct);
            return new RetiredResponse(
                request.Repository, cleared,
                cleared
                    ? $"`{circle}` no longer owes a retire of `{request.Repository}`."
                    : $"`{circle}` owed no retire of `{request.Repository}`, so there was nothing to clear.");
        });
    }

    private async Task<Answer<RegisteredResponse>> RegisterAsync(RegisterRepository body, HttpContext http, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Repository)) return Refusal.BadRequest("repository is required");

        // One shared deployment serves one workspace. A row declaring another is a message delivered to the wrong building, so
        // it says so naming both sides. A conflict, not a bad request: the payload is well-formed and the deployment is wrong.
        if (Access.RefuseForeignWorkspace(host.Workspace, body.Repository, body.Workspace) is { } foreign) return Refusal.Conflict(foreign);

        // Registered is addressable; adopted is disciplined. Only a door that looked says false: the desktop's add of a folder with no
        // manifest. Silence is `connect`, which is adoption; a shared deployment holds no roots and so no unadopted row.
        var adopted = host.Mode == ServiceMode.Shared || (body.Adopted ?? true);
        var declared = new Registration(
            body.Repository,
            Adopted: adopted,
            body.Domain?.Summary,
            body.Domain?.Owns ?? [],
            body.Domain?.Accepts ?? [],
            body.Packs ?? [],
            Entries: 0,
            // A shared deployment never stores a machine path, even one a buggy client sent: the feed has no field for it.
            Root: host.Mode == ServiceMode.Shared || string.IsNullOrWhiteSpace(body.Root) ? null : body.Root,
            // A join is a manifest's declaration, so a repository without one joins nothing.
            Joined: adopted && (body.Join ?? false),
            // Knowledge feeds only from a joined repository, narrowed here as well as in the CLI.
            SharesKnowledge: adopted && (body.Join ?? false) && (body.ShareKnowledge ?? false),
            // A shared host puts every row in its own circle. On a local host silence preserves the row: an ordinary
            // re-registration runs on every sync tick and says nothing about the wiring, so a null must not re-point it to `default`.
            Workspace: host.Workspace ?? body.Workspace,
            // The canonical line, as the checkout that registered knows it; unstated preserves.
            DefaultBranch: body.DefaultBranch,
            // What it says it uses, part of the declaration and replaced with it.
            Uses: Declared.Uses(body.Domain?.Uses, body.Repository),
            // Its lanes' words; unstated preserves what the row holds.
            Lanes: body.Lanes is null
                ? null
                : Declared.Lanes(body.Lanes.Select(lane => lane is null
                    ? null
                    : new DeclaredLane(lane.Id ?? "", lane.Title ?? "", lane.Summary ?? "", lane.Steward ?? false))),
            Kind: PlaceKinds.Of(body.Kind));

        // A shared deployment holds many machines' copies of one declaration, so it orders them by the commit each carries, as it
        // orders their knowledge. A local host's registration is this machine's own: nothing to order.
        Registration registered;
        if (host.Mode == ServiceMode.Shared)
        {
            var (outcome, taken) = await Service.Service.RegisterFedAsync(
                declared, FeedAnswers.FedFrom(body.Commit, body.CommittedAt, body.Branch, body.Base, http), DateTimeOffset.UtcNow, ct);
            if (taken is null) return FeedAnswers.Of<RegisteredResponse>(outcome);
            registered = taken;
        }
        else
        {
            registered = await Service.Service.RegisterAsync(declared, DateTimeOffset.UtcNow, ct);
        }

        // Answered with the workspace that took, not the one asked for: the client learns which circle it is actually wired to.
        return new RegisteredResponse(body.Repository, DateTimeOffset.UtcNow, registered.InWorkspace);
    }

    // Neither retiring nor re-wiring touches a file. The machine-path half of adding stays with the shell: a browser is never told, and
    // never tells, a filesystem path.
    private async Task<Answer<RetiredResponse>> RetireAsync(RetireRepository request, HttpContext http, CancellationToken ct)
    {
        // Read before it goes: a joined checkout's retire is owed to its circle, and a person removing it should hear that the
        // team's deployment will too, not only this machine.
        var leaving = (await Service.Service.RegistryAsync(ct: ct)).Named(request.Repository);
        var retired = await Service.Service.RetireAsync(request.Repository, ct);
        var circle = retired && host.Mode == ServiceMode.Local && leaving is { Joined: true, Root: not null }
            && Service.Remotes?.For(leaving.InWorkspace) is not null
                ? leaving.InWorkspace
                : null;

        return new RetiredResponse(
            request.Repository, retired,
            retired
                ? $"`{request.Repository}` is no longer registered here. Nothing was deleted: its files, its history "
                  + "and its doctrine are its own — it has simply stopped being addressable and indexed on "
                  + "this machine, and its entries have left the index."
                  + (circle is null ? "" : $" It leaves the `{circle}` workspace's deployment too, on the next sync.")
                : $"`{request.Repository}` was not registered here, so there was nothing to retire.");
    }

    private async Task<Answer<RegisteredResponse>> WireAsync(WireRepository body, HttpContext http, CancellationToken ct)
    {
        // Re-wiring across a shared deployment's own boundary is the refusal the registration door gives. A re-wire must
        // name where to: silence would become `default`, out of a shared host's circle.
        if (Access.RefuseRewire(host.Workspace, body.Repository, body.Workspace) is { } refused) return Refusal.Conflict(refused);

        var existing = (await Service.Service.RegistryAsync(ct: ct)).Named(body.Repository);
        if (existing is null)
        {
            return Refusal.NotFound(
                $"`{body.Repository}` is not registered here — `daoris repository connect` from inside it, or add it from Repositories.");
        }

        var wired = await Service.Service.RegisterAsync(
            existing with { Workspace = Workspaces.Normalize(body.Workspace) }, DateTimeOffset.UtcNow, ct);
        return new RegisteredResponse(wired.Repository, DateTimeOffset.UtcNow, wired.InWorkspace);
    }

    // Local only: a shared deployment is fed, and has no disk of its own to read.
    private async Task<Answer<ImportedResponse>> ImportAsync(ImportRepositories body, HttpContext http, CancellationToken ct)
    {
        if (host.Mode == ServiceMode.Shared)
        {
            return Refusal.Conflict("a shared deployment is fed, not scanned — it has no checkouts of its own to import");
        }

        var folder = string.IsNullOrWhiteSpace(body.Folder) ? host.RepositoryRoot : body.Folder;
        if (!Directory.Exists(folder)) return Refusal.BadRequest($"no such folder: '{folder}'");

        // A named workspace is the person setting the folder up as one: every row lands there.
        var stated = string.IsNullOrWhiteSpace(body.Workspace) ? null : body.Workspace.Trim();
        var imported = await Service.Service.ImportAsync(folder, DateTimeOffset.UtcNow, stated, ct);
        var names = imported.Select(r => r.Repository).ToList();

        return new ImportedResponse(
            folder, names.Count, names,
            names.Count == 0
                ? $"Nothing under '{folder}' — an import registers a folder's immediate subdirectories."
                : stated is null
                    ? $"Registered {names.Count} from '{folder}': {string.Join(", ", names)}. Existing rows kept "
                      + "their workspace: an import states none, and unstated wiring is preserved."
                    : $"Registered {names.Count} from '{folder}' into workspace `{stated}`: {string.Join(", ", names)}.")
        {
            Workspace = stated,
        };
    }

    private async Task<Answer<RegistrationResponse[]>> ListAsync(ListRegistry request, HttpContext http, CancellationToken ct) =>
        (await Service.Service.RegistryAsync(request.Workspace, ct)).Select(r => new RegistrationResponse(
            r.Repository, r.Adopted, r.Registered, r.Summary, r.Owns, r.Accepts, r.Packs, r.Entries,
            // Machine-local by design: a filesystem path is answered only to a caller on this machine.
            Root: host.MachineLocal(http) ? r.Root : null,
            r.Joined, r.SharesKnowledge, r.InWorkspace, r.DefaultBranch, r.Addressable, r.DependsOn,
            // What an asker may address there: each lane's words, never its paths.
            r.DeclaredLanes.Select(lane => new LaneWire(lane.Id, lane.Title, lane.Summary, lane.Steward)).ToList())
        {
            Kind = r.PlaceKind,
        }).ToArray();

    // Read from the repository's own committed file where it has a checkout here, else what is fed or brought down from the
    // circle's remote. Nothing machine-local rides this door: the provenance names a commit and a key, never a path.
    private async Task<Answer<JsonObject>> CodeMapAsync(ReadCodeMap request, HttpContext http, CancellationToken ct) =>
        await Service.Service.CodeMapAsync(request.Repository, ct) is { } read
            ? WireObject.Of(CodeMapWire.Answer(request.Repository, read))
            : Refusal.NotFound($"`{request.Repository}` is not a repository this service has registered.");

    private async Task<Answer<RefreshResponse>> RefreshAsync(RefreshIndex request, HttpContext http, CancellationToken ct)
    {
        // A shared deployment is fed, not scanned: a filesystem scan here would ghost-sweep every fed entry.
        if (host.Mode == ServiceMode.Shared)
        {
            return Refusal.Conflict("a shared deployment is fed, not scanned — entries arrive with each desktop's sync");
        }

        var report = await Service.Service.RefreshAsync(ct);
        return new RefreshResponse(
            report.Entries, report.Repositories, report.Withheld, report.SemanticError, report.Absent,
            report.Embedded is { } embedded
                ? new EmbeddedResponse(embedded.Entries, embedded.Pieces, embedded.Split, embedded.Window)
                : null,
            report.Read.Select(r => new ReadingResponse(r.Repository, r.ReadAt, r.Commit)).ToList());
    }
}

using System.Text.Json;
using Daoris.Contracts;
using Daoris.Contracts.Registry;
using static Daoris.Driver.RefusalWords;

namespace Daoris.Driver;

/// <summary>
/// The service's registry doors (the service's registry routes): the repositories a host holds, their registration, retirement and import,
/// and the refresh that reads the index again. A class over the client, reached as its <see cref="ServiceClient.Registry"/> and by the
/// client's own doors of the same names.
/// </summary>
public sealed class RegistryDoors
{
    private readonly ServiceClient _service;

    internal RegistryDoors(ServiceClient service) => _service = service;

    /// <summary>A repository as the planner reads it: its machine's wiring (D48 §2) and the lanes it declares, by id (D115 §2.2); absent is none.</summary>
    internal static RepoView ViewOf(RegistrationResponse repo) =>
        new(repo.Repository ?? "", repo.Adopted, repo.Root, RemoteTarget.Workspace(repo.Workspace))
        {
            Lanes = [.. (repo.Lanes ?? []).Select(lane => lane.Id).OfType<string>().Where(id => id.Length > 0)],
        };

    /// <summary>Every repository this host holds, in every circle — what the page's scope is read from (FG4).</summary>
    public async Task<IReadOnlyList<RepoView>> ListAsync(CancellationToken ct = default) =>
        [.. (await _service.SendAsync(new ListRegistry(), ct).ConfigureAwait(false)).Answered().Select(ViewOf)];

    /// <summary>
    /// Every repository this host holds, with the declaration each row holds (WSSETUP5): what following a line compares against, so a
    /// registration is sent only where the row holds something else.
    /// </summary>
    public async Task<IReadOnlyList<RegistrationRow>> RegistrationsAsync(CancellationToken ct = default)
    {
        var rows = (await _service.SendAsync(new ListRegistry(), ct).ConfigureAwait(false)).Answered();
        return
        [
            .. rows.Select(repo => new RegistrationRow(
                repo.Repository,
                RemoteTarget.Workspace(repo.Workspace),
                repo.Root,
                repo.Adopted,
                repo.Summary,
                repo.Owns,
                repo.Accepts,
                repo.Uses,
                repo.Packs,
                repo.Joined,
                repo.SharesKnowledge,
                // Absent is none: a host from before lanes answers without them.
                [.. (repo.Lanes ?? []).Where(lane => lane.Id is { Length: > 0 })
                    .Select(lane => new LaneView(lane.Id!, lane.Title ?? "", lane.Summary ?? "", lane.Steward ?? false))])
            {
                // The service's own word (WSSETUP6): a workspace plan's *set up*, never recomputed on this side.
                Registered = repo.Registered,
            }),
        ];
    }

    /// <summary>
    /// A circle's declarations — what each repository owns and accepts, and where it is (D34). The intake's room is written from this, so it
    /// decides from the registry as it stands now.
    /// </summary>
    public async Task<IReadOnlyList<DeclarationView>> DeclarationsAsync(string workspace, CancellationToken ct = default)
    {
        var rows = (await _service.SendAsync(new ListRegistry(workspace), ct).ConfigureAwait(false)).Answered();
        return
        [
            .. rows.Select(repo => new DeclarationView(
                repo.Repository ?? "", repo.Adopted, repo.Registered, repo.Summary, repo.Owns ?? [], repo.Accepts ?? [], repo.Root)
            {
                // Its lanes' words (D115 §2.2). Absent is none: a host from before lanes answers without them.
                Lanes = [.. (repo.Lanes ?? []).Where(lane => lane.Id is { Length: > 0 })
                    .Select(lane => new LaneView(lane.Id!, lane.Title ?? "", lane.Summary ?? "", lane.Steward ?? false))],
            }),
        ];
    }

    /// <summary>
    /// Register a repository through the registry door, with the body <c>connect</c> would send (WSSETUP5, D124 §3.3): taken, or refused in
    /// the service's own words.
    /// </summary>
    public async Task<(bool Ok, string Message)> RegisterAsync(System.Text.Json.Nodes.JsonObject body, CancellationToken ct = default)
    {
        var sent = await _service.SendAsync(RegistrationOf(body), ct).ConfigureAwait(false);
        if (sent.Reply.Ok) return (true, "");
        return (false, ErrorWords(sent.Reply.Payload) ?? $"the registry door answered {sent.Reply.Status}: {sent.Reply.Payload}");
    }

    private static RegisterRepository RegistrationOf(System.Text.Json.Nodes.JsonObject body) =>
        body.Deserialize((System.Text.Json.Serialization.Metadata.JsonTypeInfo<RegisterRepository>)ContractJson.Options.GetTypeInfo(typeof(RegisterRepository)))!;

    /// <summary>
    /// Move a registered repository to a workspace on this machine (ENTRY1d1, D48 §7): the request the Manage drawer's <i>Move to workspace</i>
    /// sends, which edits one field of the row. The workspace the host took is said as the drawer says it; a refusal comes back in the
    /// service's words, and a host older than the door is said to be.
    /// </summary>
    public async Task<(bool Ok, string Message)> WireRepositoryAsync(string repository, string workspace, CancellationToken ct = default)
    {
        var sent = await _service.SendAsync(new WireRepository(repository, workspace), ct).ConfigureAwait(false);
        if (AnswerOf(sent.Reply.Payload) is null)
        {
            return (false, $"the service at {_service.BaseUrl} has no workspace door for a repository ({sent.Reply.Status}) — is it older than this driver?");
        }

        return sent.Answer is { } wired
            ? (true, $"`{wired.Repository}` is now in workspace `{wired.Workspace}`.")
            : (false, ErrorWords(sent.Reply.Payload) ?? sent.Reply.Payload);
    }

    /// <summary>
    /// A repository's first registration from its working tree at <paramref name="root"/> (ONEWAY1u, D124 §3.3), at this client's service,
    /// which may be any address: what <see cref="RegistryConnect.Plan"/> composes, taken or refused in the service's own words. The workspace
    /// the service says it took is read back, since silence preserves whatever the row held.
    /// </summary>
    /// <exception cref="RegistryRefusal">The plan's refusal, or the service's; nothing was registered.</exception>
    public async Task<ConnectDone> ConnectAsync(string root, string? workspace, CancellationToken ct = default)
    {
        var plan = RegistryConnect.Plan(root, workspace, _service.BaseUrl);
        var sent = await _service.SendAsync(RegistrationOf(plan.Body), ct).ConfigureAwait(false);
        if (!sent.Reply.Ok) throw Refused($"the service refused the registration: {ServiceSaid(sent.Reply)}", 2);
        return new ConnectDone(plan, sent.Answer?.Workspace);
    }

    /// <summary>
    /// Take a repository off the registry (ONEWAY1u): the service's own sentence, which says what retiring does not do. One that was never
    /// registered is an answer, not a failure.
    /// </summary>
    /// <exception cref="RegistryRefusal">The service refused; nothing changed.</exception>
    public async Task<string> RetireAsync(string repository, CancellationToken ct = default)
    {
        var sent = await _service.SendAsync(new RetireRepository(repository), ct).ConfigureAwait(false);
        if (!sent.Reply.Ok) throw Refused(ServiceSaid(sent.Reply), 1);
        return sent.Answer?.Message ?? $"{repository} is no longer registered.";
    }

    /// <summary>
    /// Register everything a folder's subdirectories propose (ONEWAY1u, D48 §3). A folder is a machine path and goes only to a service on this
    /// machine; none lets the service use its own knowledge root.
    /// </summary>
    /// <param name="folder">An absolute folder, or null.</param>
    /// <exception cref="RegistryRefusal">A folder for a service elsewhere, or the service refused; nothing was sent or changed.</exception>
    public async Task<string> ImportAsync(string? folder, string? workspace, CancellationToken ct = default)
    {
        if (folder is not null && !RegistryConnect.IsLocal(_service.BaseUrl))
        {
            throw new RegistryRefusal(
                RegistryRefusal.FolderNotLocal,
                $"{_service.BaseUrl} is not on this machine, and a folder here means nothing there — import a "
                + "folder into this machine's own service; a deployment is fed by the machines that join it.",
                2);
        }

        var sent = await _service.SendAsync(
            new ImportRepositories(string.IsNullOrEmpty(folder) ? null : folder, string.IsNullOrEmpty(workspace) ? null : workspace), ct).ConfigureAwait(false);
        if (!sent.Reply.Ok) throw Refused(ServiceSaid(sent.Reply), 1);
        return sent.Answer?.Message ?? "imported.";
    }

    /// <summary>
    /// Ask the host to read the index again (the refresh every door has): registering does not re-index (D124 §3.3). Null when it did; else
    /// why not, in the service's words.
    /// </summary>
    public async Task<string?> RefreshAsync(CancellationToken ct = default)
    {
        var sent = await _service.SendAsync(new RefreshIndex(), ct).ConfigureAwait(false);
        return sent.Reply.Ok ? null : ErrorWords(sent.Reply.Payload) ?? $"the refresh door answered {sent.Reply.Status}: {sent.Reply.Payload}";
    }

    private static RegistryRefusal Refused(string message, int exit) => new(RegistryRefusal.ServiceRefused, message, exit);

    private static JsonElement? AnswerOf(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

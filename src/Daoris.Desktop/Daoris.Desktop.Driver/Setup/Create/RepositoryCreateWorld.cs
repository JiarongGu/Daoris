using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The world a creation reads and writes on this machine: the service's registry and registration door, each repository's line and commit
/// author as its checkout and the home's git identities say, and the remote step of the workflow the home's saved workflows give.
/// </summary>
public sealed class RepositoryCreateWorld(ServiceClient service, string home) : ICreateWorld
{
    public Task<IReadOnlyList<RepoView>> RegistryAsync(CancellationToken ct) => service.RegistryAsync(ct);

    public bool LocalService => RegistryConnect.IsLocal(service.BaseUrl);

    public DateTime CreatedAt(string root)
    {
        try
        {
            return Directory.GetCreationTimeUtc(root);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return DateTime.MinValue;
        }
    }

    public async Task<string?> LineOfAsync(RepoView row, DriverConfig config, CancellationToken ct) =>
        (await CanonicalLine.ResolveAsync(row.Root!, row.Repository, row.Workspace, config, ct).ConfigureAwait(false)).Branch;

    public async Task<CreateAuthor?> AuthorOfAsync(RepoView row, CancellationToken ct) =>
        await GitAuthor.OfAsync(home, row.Root!, ct).ConfigureAwait(false) is { } who ? new CreateAuthor(who.Name, who.Email) : null;

    public async Task<CreateAuthor?> GitOwnAuthorAsync(string directory, CancellationToken ct)
    {
        var (nameCode, name, _) = await WorkingTree.GitAsync(directory, ["config", "user.name"], ct).ConfigureAwait(false);
        var (emailCode, email, _) = await WorkingTree.GitAsync(directory, ["config", "user.email"], ct).ConfigureAwait(false);
        return nameCode == 0 && emailCode == 0 && name.Trim().Length > 0 && email.Trim().Length > 0 ? new CreateAuthor(name.Trim(), email.Trim()) : null;
    }

    public string? RemoteStepOf(DriverConfig config, string repository, string workspace) => CreateRemoteSteps.Of(home, config, repository, workspace);

    public bool WorksApart(DriverConfig config, string repository, string workspace) => PlaceIsolates.Of(home, config, repository, workspace) is not null;

    public Task<(bool Ok, string Message)> RegisterAsync(JsonObject body, CancellationToken ct) => service.RegisterAsync(body, ct);
}

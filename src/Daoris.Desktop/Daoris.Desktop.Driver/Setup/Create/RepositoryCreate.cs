using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// What <c>daoris repository create</c> asks: the department's name and the one line of why it is opened (the set-up quest's body and the
/// empty commit's message), its kind, the workspace it joins, where its folder goes, the line its history lands on, and the person's work
/// that follows the set-up. Everything else is derived from the registry on this machine.
/// </summary>
public sealed record CreateAsk(string Name, string Purpose)
{
    /// <summary>A repository (<see cref="PlaceKind.Git"/>) or a folder department (<see cref="PlaceKind.Folder"/>).</summary>
    public string Kind { get; init; } = PlaceKind.Git;

    /// <summary>The workspace it joins; none said is the default one.</summary>
    public string? Workspace { get; init; }

    /// <summary>The folder its folder is made in, where the workspace's registered roots say none.</summary>
    public string? Under { get; init; }

    /// <summary>The line its history starts on, where the workspace's repositories say another.</summary>
    public string? Line { get; init; }

    /// <summary>The person's request, which follows the set-up as the set-up quest's one chained step (<see cref="RepositoryCreateWork"/>); a git repository's only.</summary>
    public string? Work { get; init; }
}

/// <summary>The name and email the empty commit carries: what the workspace's other repositories commit as.</summary>
public sealed record CreateAuthor(string Name, string Email);

/// <summary>A refusal: a code a screen keys on and the sentence that names what to do.</summary>
public sealed record CreateRefusal(string Code, string Sentence);

/// <summary>One step of the press's own hand, in its order, said without the values the plan holds.</summary>
public sealed record CreateStep(string Id, string Said);

/// <summary>The codes a plan refuses by, each with nothing written.</summary>
public static class CreateRefusals
{
    public const string NameUnusable = "name-unusable";
    public const string NameRegistered = "name-registered";
    public const string NoPurpose = "no-purpose";
    public const string Kind = "kind";
    public const string NoWorkspace = "no-workspace";
    public const string NoFolder = "no-folder";
    public const string UnderMissing = "under-missing";
    public const string FolderExists = "folder-exists";
    public const string LineUnusable = "line-unusable";
    public const string LineFolder = "line-folder";
    public const string WorkNeedsSetUp = "work-needs-set-up";
    public const string NoIdentity = "no-identity";
    public const string NoGit = "no-git";
    public const string ServiceNotLocal = "service-not-local";
    public const string GrowthOff = "growth-off";
}

/// <summary>The operations a creation's receipt keeps, in the order the press makes them.</summary>
public static class CreateOperations
{
    public const string Folder = "folder";
    public const string History = "history";
    public const string Registration = "registration";
    public const string Driving = "driving";
    public const string SetUp = "setup";
}

/// <summary>
/// A creation's plan: the ask as it resolves on this machine, the steps the press would make, and every refusal at once. It reads the registry,
/// the identities and the folder, and writes nothing. A plan that resumes an unfinished receipt carries that receipt's digest.
/// </summary>
public sealed record CreatePlanned(
    CreateAsk Asked, string Workspace, string Folder, string? Line, CreateAuthor? Author, IReadOnlyList<CreateStep> Steps, IReadOnlyList<CreateRefusal> Refusals)
{
    /// <summary>The step of the workspace's workflow, inherited, that needs a remote the new place has none of; the drive stays off for it.</summary>
    public string? RemoteStep { get; init; }

    /// <summary>The digest of the unfinished receipt this plan goes on from, where it does.</summary>
    public string? Resumes { get; init; }

    /// <summary>The growth switch the plan read for the workspace the department opens in (<see cref="GrowthKey"/>).</summary>
    public string? Growth { get; init; }

    public bool Applies => Refusals.Count == 0;

    public string Digest => Resumes ?? CreateDigests.Of(this);
}

/// <summary>A creation plan's digest: what the person read, said once, so an apply can tell that it no longer holds.</summary>
public static class CreateDigests
{
    /// <summary>
    /// The SHA-256 of the ask as it resolved: name (any case is one name), kind, workspace, folder, line, author, purpose, work and the remote
    /// step. The sentences a reader sees beside them are read from these, so they are no part of it.
    /// </summary>
    public static string Of(CreatePlanned plan)
    {
        var ask = plan.Asked;
        string[] parts =
        [
            "create", ask.Name.ToLowerInvariant(), ask.Kind, plan.Workspace, plan.Folder, plan.Line ?? "", plan.Author?.Name ?? "", plan.Author?.Email ?? "",
            ask.Purpose, ask.Work ?? "", plan.RemoteStep ?? "",
        ];
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(parts))));
    }
}

/// <summary>What an apply came to: whether every operation is done, a code where it is refused, and each operation as the receipt keeps it.</summary>
public sealed record CreateResult(bool Done, string? Refused, string Message, string Digest, IReadOnlyList<PlaceOperation> Operations)
{
    /// <summary>The department's name, as it is registered.</summary>
    public string? Name { get; init; }

    /// <summary>The workspace it joined.</summary>
    public string? Workspace { get; init; }

    /// <summary>The line its history starts on; none for a folder department.</summary>
    public string? Line { get; init; }

    /// <summary>The folder the plan names.</summary>
    public string? Folder { get; init; }

    /// <summary>The empty commit's id, once it is made.</summary>
    public string? Commit { get; init; }
}

/// <summary>
/// What a creation reads and writes beyond the home: the registry, the identities and lines of the workspace's repositories, the registration
/// door, and the remote step the inherited workflow holds. An interface so the plan and the apply are held without a service;
/// <see cref="RepositoryCreateWorld"/> is the real one. Git and the folder are real in every test.
/// </summary>
public interface ICreateWorld
{
    /// <summary>Every repository this machine's host holds, in every workspace.</summary>
    Task<IReadOnlyList<RepoView>> RegistryAsync(CancellationToken ct);

    /// <summary>Whether the service is on this machine: a folder made here means nothing to another.</summary>
    bool LocalService { get; }

    /// <summary>When the folder at <paramref name="root"/> came to be, which names the newest of the roots a tied parent is chosen by.</summary>
    DateTime CreatedAt(string root);

    /// <summary>The line the checkout lands on, or null where none is set and git names none.</summary>
    Task<string?> LineOfAsync(RepoView row, DriverConfig config, CancellationToken ct);

    /// <summary>What the repository's commits carry as author (the identity <c>git use</c> chose for it), or null where none is.</summary>
    Task<CreateAuthor?> AuthorOfAsync(RepoView row, CancellationToken ct);

    Task<CreateAuthor?> GitOwnAuthorAsync(string directory, CancellationToken ct);

    /// <summary>The step of the workflow <paramref name="repository"/> would follow that needs a remote, or null.</summary>
    string? RemoteStepOf(DriverConfig config, string repository, string workspace);

    bool WorksApart(DriverConfig config, string repository, string workspace) => true;

    /// <summary>Register through the registry door with the body <c>connect</c> would send: taken, or refused in the service's words.</summary>
    Task<(bool Ok, string Message)> RegisterAsync(JsonObject body, CancellationToken ct);
}

/// <summary>What a creation reads and writes: its world, the set-up press's world, the home, and the one edit of <c>driver.json</c>.</summary>
public sealed record CreateDoors(
    ICreateWorld World, ISetupWorld Setup, string Home, bool Unadopted, Func<DriverConfig> Config, Action<Func<DriverConfig, DriverConfig>> Change, DateOnly Day);

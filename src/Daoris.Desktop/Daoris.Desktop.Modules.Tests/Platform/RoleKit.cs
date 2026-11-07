using System.Text.Json;
using Daoris.Driver;
using CatalogueScope = Daoris.Driver.RoleScope;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// What the role door's tests share (STEWARDROLE1a-i, D233 point 2): a stand-in act for the steward's table; the supervisor and the
/// steward as the catalogue records them, staffed in the judge's reading of the catalogue; a role's terminal started; and a
/// <c>ROLE_ACT</c> payload proved as the holder's verb proves it.
/// </summary>
internal static class RoleKit
{
    public const string Driver = DriverModule.Name;

    /// <summary>
    /// A stand-in act: <c>{ repository }</c> exactly, or the names <see cref="Form"/> lists with <c>repository</c> first, its target that
    /// repository; it records each run and runs <see cref="Inside"/> first.
    /// </summary>
    internal sealed class Probe : IRoleAct
    {
        public const string Named = "probe";

        public string Name => Named;

        public int Ran { get; private set; }

        /// <summary>The person's route it asks as outside the role's acts, or none.</summary>
        public string? PersonRoute { get; init; }

        /// <summary>The form's names in order, the first the repository's.</summary>
        public IReadOnlyList<string> Form { get; init; } = ["repository"];

        /// <summary>Run inside the handler, before it answers: a test's role change or pending operation.</summary>
        public Func<RoleActRun, Task>? Inside { get; set; }

        public bool IsForm(JsonElement form)
        {
            var names = form.EnumerateObject().Select(each => each.Name).ToList();
            return names.Count == Form.Count && names.Zip(Form).All(each => string.Equals(each.First, each.Second, StringComparison.OrdinalIgnoreCase))
                   && form.EnumerateObject().First().Value.ValueKind == JsonValueKind.String;
        }

        public string Target(JsonElement form) => form.EnumerateObject().First().Value.GetString()!;

        public async Task<object?> RunAsync(RoleActRun run, CancellationToken ct)
        {
            Ran++;
            if (Inside is { } inside) await inside(run);
            return new { Ran = true };
        }
    }

    /// <summary>The steward's record, held by <paramref name="holder"/> with <paramref name="acts"/> its authority.</summary>
    public static RoleRecord Steward(IReadOnlyList<string> acts, string? holder = RoleHolders.Terminal) =>
        new(AppointedRoles.Steward, AuthoredText.Of("Steward"), RoleRecord.Appointed, [], CatalogueScope.Everywhere, new RoleHold(AppointedRoles.Steward, acts, holder));

    /// <summary>A supervisor's record at <paramref name="scope"/> (the machine where none), held by <paramref name="holder"/>, its window in minutes.</summary>
    public static RoleRecord Supervisor(
        string id = AppointedRoles.Supervisor, CatalogueScope? scope = null, string? holder = RoleHolders.Terminal, int window = RoleHold.DefaultWindow) =>
        new(id, AuthoredText.Of("Supervisor"), RoleRecord.Appointed, [], scope ?? CatalogueScope.Everywhere,
            new RoleHold(AppointedRoles.Supervisor, AppointedRoles.SupervisorActs, holder, window));

    /// <summary>A repository's scope in the catalogue.</summary>
    public static CatalogueScope Repository(string name) => new(CatalogueScope.Repository, name);

    /// <summary>A workspace's scope in the catalogue.</summary>
    public static CatalogueScope Workspace(string name) => new(CatalogueScope.Workspace, name);

    /// <summary>
    /// The catalogue's appointed roles as the judge reads them: <paramref name="roles"/> in place of the ones of their ids, the seed's
    /// supervisor (your rules) and steward (no one) for the rest, as a home with no store reads.
    /// </summary>
    public static StandingConfirmation Staff(this StandingConfirmation standing, params RoleRecord[] roles)
    {
        var now = standing.Appointed().Where(each => roles.All(role => role.Id != each.Id)).Concat(roles).ToList();
        standing.Appointed = () => now;
        return standing;
    }

    /// <summary>
    /// The steward held by no one, as the role door's change leaves it, and the holds the judge's step ended, as the door's sweep reads
    /// them: what turning the steward's terminal off is.
    /// </summary>
    public static IReadOnlyList<string> Unstaffed(this StandingConfirmation standing)
    {
        var steward = standing.Appointed().First(each => each.Id == AppointedRoles.Steward);
        standing.Staff(steward with { Hold = steward.Hold! with { Holder = null } });
        return standing.RolesChanged();
    }

    /// <summary>The steward's table with the probe in it, and the steward staffed by the terminal holding it.</summary>
    public static StandingConfirmation Probing(this StandingConfirmation standing, Probe probe)
    {
        standing.TableOf = role => role == AppointedRoles.Steward
            ? new Dictionary<string, IRoleAct>(StringComparer.Ordinal) { [Probe.Named] = probe }
            : new Dictionary<string, IRoleAct>(StringComparer.Ordinal);
        return standing.Staff(Steward([Probe.Named]));
    }

    /// <summary>The start of the steward's terminal, <paramref name="share"/> the choice while a holder lives.</summary>
    public static HoldRequest Stewarding(string? share = "own") => new(AppointedRoles.Steward, share);

    /// <summary>A role's terminal started, the tab a stand-in that keeps the key it was handed; null where none was handed.</summary>
    public static (HoldChange Change, string? Key) Start(StandingConfirmation standing, HoldRequest request)
    {
        string? handed = null;
        var change = standing.Start(request, open =>
        {
            handed = open.Key;
            return StandingConfirmationTests.Opened(open);
        });
        return (change, handed);
    }

    /// <summary>A live hold's generation of <paramref name="kind"/>, which the holder's verb reads from its listing.</summary>
    public static string GenerationOf(StandingConfirmation standing, string kind) =>
        standing.LiveGeneration(kind) ?? throw new InvalidOperationException($"no {kind}'s terminal is live");

    /// <summary>A <c>ROLE_ACT</c> payload proved by <paramref name="key"/> over <paramref name="nonce"/>, as the verb builds it.</summary>
    public static JsonElement Act(
        string key, string? nonce, string generation, string role = AppointedRoles.Steward, string act = Probe.Named, string form = """{"repository":"engine"}""")
    {
        var element = JsonDocument.Parse(form).RootElement.Clone();
        var proof = nonce is null ? new string('0', 64) : SupervisorProof.Prove(key, nonce, RoleActForm.Bytes(role, act, generation, element))!;
        return JsonDocument.Parse(JsonSerializer.Serialize(new RoleActForm(role, act, generation, element, proof).Payload())).RootElement.Clone();
    }

    /// <summary>The judge's role method for <c>ROLE_ACT</c>.</summary>
    public static RoleJudgement JudgeAct(StandingConfirmation standing, JsonElement payload, string? nonce, PipeClientProcess? client = null) =>
        standing.JudgeRole(Driver, RoleRoutes.Act, payload, client, nonce);
}

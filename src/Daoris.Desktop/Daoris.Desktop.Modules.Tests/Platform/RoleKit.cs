using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// What the role door's tests share (STEWARDROLE1a-i): a stand-in act for the steward's table, which this build leaves empty; a
/// <c>ROLE_ACT</c> payload proved as the holder's verb proves it; the steward's appointment; and an appointment's generation, read from
/// the record as the holder's verb learns it from its listing.
/// </summary>
internal static class RoleKit
{
    public const string Driver = DriverModule.Name;

    /// <summary>A stand-in act: <c>{ repository }</c> exactly, its target that repository; it records each run and runs <see cref="Inside"/> first.</summary>
    internal sealed class Probe : IRoleAct
    {
        public const string Named = "probe";

        public string Name => Named;

        public int Ran { get; private set; }

        /// <summary>Run inside the handler, before it answers: a test's revoke or pending operation.</summary>
        public Func<RoleActRun, Task>? Inside { get; set; }

        public bool IsForm(JsonElement form)
        {
            var names = form.EnumerateObject().Select(each => each.Name).ToList();
            return names.Count == 1 && string.Equals(names[0], "repository", StringComparison.OrdinalIgnoreCase)
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

    /// <summary>The steward's table and offered set with the probe in them.</summary>
    public static StandingConfirmation Probing(this StandingConfirmation standing, Probe probe)
    {
        standing.TableOf = role => role == Roles.Steward
            ? new Dictionary<string, IRoleAct>(StringComparer.Ordinal) { [Probe.Named] = probe }
            : new Dictionary<string, IRoleAct>(StringComparer.Ordinal);
        standing.OffersActs = role => role == Roles.Steward ? [Probe.Named] : [];
        return standing;
    }

    /// <summary>The steward's appointment for this machine, <paramref name="share"/> the later Appoint's choice.</summary>
    public static AppointRequest Stewarding(bool on = true, string? share = "own", bool setup = false) =>
        new(Roles.Steward, RoleScopes.Machine, RoleHolders.Terminal, on, share, setup);

    /// <summary>The steward appointed, the tab a stand-in that keeps the key it was handed; null where none was handed.</summary>
    public static (AppointmentChange Change, string? Key) Appoint(StandingConfirmation standing, AppointRequest request)
    {
        string? handed = null;
        var change = standing.Appoint(request, open =>
        {
            handed = open.Key;
            return StandingConfirmationTests.Opened(open);
        });
        return (change, handed);
    }

    /// <summary>An appointment's generation as the record names it, which the holder's verb reads from its listing.</summary>
    public static string GenerationOf(StandingConfirmation standing, string role) =>
        StandingConfirmation.Read(File.ReadAllText(standing.RecordPath), StandingConfirmation.ThisMachine()).Appointments
            .Single(each => each.Role == role).Generation;

    /// <summary>A <c>ROLE_ACT</c> payload proved by <paramref name="key"/> over <paramref name="nonce"/>, as the verb builds it.</summary>
    public static JsonElement Act(
        string key, string? nonce, string generation, string role = Roles.Steward, string act = Probe.Named, string form = """{"repository":"engine"}""")
    {
        var element = JsonDocument.Parse(form).RootElement.Clone();
        var proof = nonce is null ? new string('0', 64) : SupervisorProof.Prove(key, nonce, RoleActForm.Bytes(role, act, generation, element))!;
        return JsonDocument.Parse(JsonSerializer.Serialize(new RoleActForm(role, act, generation, element, proof).Payload())).RootElement.Clone();
    }

    /// <summary>The judge's role method for <c>ROLE_ACT</c>.</summary>
    public static RoleJudgement JudgeAct(StandingConfirmation standing, JsonElement payload, string? nonce, PipeClientProcess? client = null) =>
        standing.JudgeRole(Driver, RoleRoutes.Act, payload, client, nonce);
}

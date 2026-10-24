using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris-driver steward &lt;act&gt; …</c> (STEWARDROLE1b, D209 §3.3): the steward's holder's verbs, each the grammar of the verb it
/// mirrors, sent only over the pipe as <c>ROLE_ACT</c> with a proof over the connection's nonce; and <c>steward acts</c>, the run's acts,
/// an open read. No alone path: with no Daoris running, the role does not exist.
/// </summary>
/// <remarks>
/// The key is read from what the host's entry took (<see cref="HolderKey.Taken"/>), never an argument; only an HMAC proof is sent, and
/// only to a pipe whose server is the install's application (<see cref="InstanceDoor.InstallApplication"/>). The appointment's generation
/// is read from the instance on the same connection (<c>STEWARD_ACTS</c>), as the supervisor's listing names its own.
/// </remarks>
public static class StewardCommand
{
    /// <summary>The steward's acts as the pipe's census classes them: one route for every act.</summary>
    public static InstanceRoute Act { get; } = new(TreesRoutes.DriverModule, RoleRoutes.Act, RouteClass.Role);

    /// <summary>The run's acts and the live appointment's generation: an open read naming no key.</summary>
    public static InstanceRoute Acts { get; } = new(TreesRoutes.DriverModule, StewardRoutes.Acts, RouteClass.Open);

    /// <summary>The verb's refusal where no Daoris runs: it has no alone path.</summary>
    public const string NoInstance = "no-instance";

    /// <summary>What the verb's usage says.</summary>
    public const string Usage =
        "usage: daoris-driver steward acts | drive <repo> | undrive <repo> | trees <repo> on | hold <repo> | resume <repo> | stop <id> | "
        + "finish <id> | retry <quest>";

    /// <summary>What the words ask: the act and its exact form, or <c>acts</c> with no form; null where they are not one of the verbs.</summary>
    public static (string Act, object? Form)? Read(IReadOnlyList<string> args) => args switch
    {
        ["acts"] => ("acts", null),
        ["drive", var repo] when Named(repo) => ("drive", new { repository = repo, drivable = true }),
        ["undrive", var repo] when Named(repo) => ("undrive", new { repository = repo, drivable = false }),
        ["trees", var repo, "on"] when Named(repo) => ("trees", new { repository = repo, ownTree = true }),
        ["hold", var repo] when Named(repo) => ("hold", new { repository = repo, held = true }),
        ["resume", var repo] when Named(repo) => ("resume", new { repository = repo, held = false }),
        ["stop", var id] when Named(id) => ("stop", new { id, said = true }),
        ["finish", var id] when Named(id) => ("finish", new { id, state = "completed", said = true }),
        ["retry", var quest] when Named(quest) => ("retry", new { quest = quest.TrimStart('#') }),
        _ => null,
    };

    private static bool Named(string value) => value.Trim().Length > 0 && value.Trim() == value && !value.StartsWith("--", StringComparison.Ordinal);

    /// <summary>The verb: its words read, the instance found behind the install's application, the generation read, the act proved and sent.</summary>
    public static async Task<int> RunAsync(IReadOnlyList<string> args, InstanceWorld instance, Func<string?> key, TextWriter output, CancellationToken ct = default)
    {
        if (Read(args) is not { } asked)
        {
            await output.WriteLineAsync(Usage).ConfigureAwait(false);
            return 2;
        }

        if (asked.Form is not null && HolderKey.Read(key()) is null)
        {
            await output.WriteLineAsync(
                $"steward: this terminal holds no role's key ({HolderKey.Variable}): act from the terminal Daoris started when the owner "
                + "appointed the steward.").ConfigureAwait(false);
            return 2;
        }

        var checkedWorld = new InstanceWorld
        {
            Home = instance.Home, Environment = instance.Environment, BaseDirectory = instance.BaseDirectory, Status = instance.Status,
            Out = instance.Out, Error = instance.Error, In = instance.In, Verb = instance.Verb, Answer = instance.Answer,
            Wait = instance.Wait, Owners = instance.Owners,
            ServedBy = instance.ServedBy ?? (server => InstanceDoor.InstallApplication(server, instance.BaseDirectory)),
        };
        var look = await InstanceDoor.FindAsync(checkedWorld, ct).ConfigureAwait(false);
        switch (look.Found)
        {
            case InstanceFound.None or InstanceFound.Headless:
                await output.WriteLineAsync($"steward: Daoris is not running here, and the steward acts only through it ({NoInstance}).")
                    .ConfigureAwait(false);
                return 2;
            case not InstanceFound.Answers:
                await output.WriteLineAsync($"steward: {look.Why}").ConfigureAwait(false);
                return 2;
        }

        await using var client = look.Client!;
        var listed = await client.RequestAsync(
            Guid.NewGuid().ToString(), Acts.Module, Acts.Route, new { }, Guid.NewGuid().ToString("N"), told: null, instance.Within, ct)
            .ConfigureAwait(false);
        if (listed is not { } read || !read.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            await output.WriteLineAsync("steward: Daoris did not answer what the steward did; nothing was sent.").ConfigureAwait(false);
            return 2;
        }

        if (asked.Form is null) return await ListAsync(data, output).ConfigureAwait(false);
        if (InstanceClient.Text(data, "generation") is not { Length: > 0 } generation)
        {
            await output.WriteLineAsync("steward: no live appointment of the steward's holds this terminal (not-appointed); nothing was sent.")
                .ConfigureAwait(false);
            return 1;
        }

        if (look.Nonce is not { } nonce)
        {
            await output.WriteLineAsync("steward: this Daoris draws no nonce for a proof; nothing was sent.").ConfigureAwait(false);
            return 2;
        }

        var form = JsonSerializer.SerializeToElement(asked.Form);
        var proof = SupervisorProof.Prove(key()!, nonce, RoleActForm.Bytes(StewardRoutes.Role, asked.Act, generation, form));
        if (proof is null)
        {
            await output.WriteLineAsync("steward: Daoris's nonce is not one this verb reads; nothing was sent.").ConfigureAwait(false);
            return 2;
        }

        var answer = await client.RequestAsync(
            Guid.NewGuid().ToString(), Act.Module, Act.Route, new RoleActForm(StewardRoutes.Role, asked.Act, generation, form, proof).Payload(),
            Guid.NewGuid().ToString("N"), told: null, instance.Within, ct).ConfigureAwait(false);
        if (answer is not { } envelope)
        {
            await output.WriteLineAsync("steward: Daoris did not answer; read `steward acts` before acting again.").ConfigureAwait(false);
            return 2;
        }

        if (!envelope.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True)
        {
            var error = envelope.TryGetProperty("error", out var held) ? held : default;
            await output.WriteLineAsync($"steward: {InstanceClient.Text(error, "message")} ({InstanceClient.Text(error, "code")})").ConfigureAwait(false);
            return 1;
        }

        var acted = envelope.GetProperty("data");
        var done = acted.TryGetProperty("done", out var was) && was.ValueKind == JsonValueKind.True;
        await output.WriteLineAsync(done
            ? $"steward: {asked.Act} {string.Join(' ', args.Skip(1))}: done, recorded as the steward's."
            : $"steward: {asked.Act} {string.Join(' ', args.Skip(1))}: refused ({InstanceClient.Text(acted, "why")}); nothing was done.").ConfigureAwait(false);
        if (acted.TryGetProperty("roles", out var roles) && roles.ValueKind == JsonValueKind.Array)
        {
            var named = roles.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.String).Select(each => each.GetString()!).ToList();
            await output.WriteLineAsync($"steward: {RolesBrief.Said(named)}").ConfigureAwait(false);
        }

        return done ? 0 : 1;
    }

    /// <summary>The run's acts, newest first, one line each: when, the act, what it acted on, and whether the owner has acted on it since.</summary>
    private static async Task<int> ListAsync(JsonElement data, TextWriter output)
    {
        var acts = data.TryGetProperty("acts", out var listed) && listed.ValueKind == JsonValueKind.Array ? [.. listed.EnumerateArray()] : new List<JsonElement>();
        if (acts.Count == 0)
        {
            await output.WriteLineAsync("steward: no act of the steward's since Daoris started.").ConfigureAwait(false);
            return 0;
        }

        foreach (var each in acts)
        {
            var target = InstanceClient.Text(each, "session") ?? InstanceClient.Text(each, "quest") ?? InstanceClient.Text(each, "repository");
            var standing = each.TryGetProperty("standing", out var stands) && stands.ValueKind == JsonValueKind.True;
            await output.WriteLineAsync(
                $"{InstanceClient.Text(each, "at")}  {InstanceClient.Text(each, "act")}  {target}{(standing ? "" : "  (the owner has acted on it since)")}")
                .ConfigureAwait(false);
        }

        return 0;
    }
}

/// <summary>The steward's routes beside <c>ROLE_ACT</c> (STEWARDROLE1b, D209 §3.3).</summary>
public static class StewardRoutes
{
    /// <summary>The role's name as <c>ROLE_ACT</c> carries it.</summary>
    public const string Role = "steward";

    /// <summary>The run's acts and the live appointment's generation: <c>Open</c> in the pipe's census, naming no key.</summary>
    public const string Acts = "STEWARD_ACTS";
}

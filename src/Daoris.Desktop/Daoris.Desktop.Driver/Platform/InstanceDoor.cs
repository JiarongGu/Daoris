using System.Text.Json;

namespace Daoris.Driver;

/// <summary>What a verb finds when it looks for the running Daoris (RPC1b, the terminal RPC design §7.2's rows).</summary>
public enum InstanceFound
{
    /// <summary>No instance on this home, or a workspace build, which reads no address file: the verb acts alone.</summary>
    None,

    /// <summary>A headless <c>drive</c> loop holds the home: alone under the lease's rule, until a headless loop serves the pipe.</summary>
    Headless,

    /// <summary>The instance answered its hello on this home: the verb is its client.</summary>
    Answers,

    /// <summary>The instance lives and its pipe did not answer, or it names none: a press is refused, never done alone.</summary>
    Silent,

    /// <summary>The hello was refused (<c>protocol</c>, <c>home</c>, <c>stopping</c>): a press is refused, saying which.</summary>
    Refused,
}

/// <summary>What a verb found, with the sentence that says it and the connection where the instance answered.</summary>
public sealed record InstanceLook(InstanceFound Found, string Why, InstanceClient? Client = null);

/// <summary>Where a verb looks for the running Daoris, and what it says while it waits: the process's own, or a test's.</summary>
public sealed class InstanceWorld
{
    /// <summary>The home the verb acts on, or null where it has none.</summary>
    public required string? Home { get; init; }

    /// <summary>The variables, read by name.</summary>
    public Func<string, string?> Environment { get; init; } = System.Environment.GetEnvironmentVariable;

    /// <summary>Where the program runs from, which says whether an install carries it.</summary>
    public string BaseDirectory { get; init; } = AppContext.BaseDirectory;

    /// <summary>What an address's status door answers as a Daoris host (<see cref="RunningService.Status"/>), or a test's.</summary>
    public Func<string, ServiceStatus?> Status { get; init; } = url => RunningService.Status(url);

    /// <summary>Where the answer and the progress are said.</summary>
    public TextWriter Out { get; init; } = Console.Out;

    /// <summary>Where a refusal is said.</summary>
    public TextWriter Error { get; init; } = Console.Error;

    /// <summary>The verb, as its sentences begin (<c>trees</c>).</summary>
    public string Verb { get; init; } = "daoris-driver";

    /// <summary>How long the pipe is given to connect and answer the hello (§7.2: five seconds).</summary>
    public TimeSpan Answer { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>How long a press is waited on before its answer is asked for by <c>RESULT</c>.</summary>
    public TimeSpan Wait { get; init; } = TimeSpan.FromMinutes(30);
}

/// <summary>
/// A verb's door to the running Daoris (RPC1b, D171 point 3, the terminal RPC design §7): it finds the instance as §7.4 says and
/// switches as §7.2's table does, sending a press's route while the instance answers, acting alone where none runs, and never
/// acting alone beside one that does not answer, nor after a request was sent.
/// </summary>
public static class InstanceDoor
{
    /// <summary>The pipe the dev loop and a rehearsal name, as they name the service (§7.4).</summary>
    public const string Variable = "DAORIS_INSTANCE";

    /// <summary>
    /// What the verb finds on its home (§7.4): the instance whose address file's process lives with its recorded start, whose
    /// status and hello answer this home's id, and whose pipe that process serves; or the pipe <see cref="Variable"/> names,
    /// whose hello answers this home's id.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>A workspace build reads no address file</b> (D171's FINDDAORIS1 note): on a machine with an install the account's
    /// <c>DAORIS_HOME</c> names that install's live home, and a gate or a test that met a running Daoris would act on it. The dev
    /// loop and a rehearsal that mean to reach an instance name its pipe in <see cref="Variable"/>.
    /// </remarks>
    public static async Task<InstanceLook> FindAsync(InstanceWorld world, CancellationToken ct = default)
    {
        var mine = world.Home is { } home ? HomeIdentity.Read(home) : null;
        if (world.Environment(Variable) is { Length: > 0 } named)
        {
            var silent = $"the Daoris {Variable} names ({named}) did not answer; nothing was done.";
            return await HelloAsync(named, mine, world, silent, server: null, ct).ConfigureAwait(false);
        }

        if (DaorisHome.InstallOf(world.BaseDirectory) is null || world.Home is null)
        {
            return new(InstanceFound.None, "a workspace build reads no address file.");
        }

        if (HomeLease.HeldBy(world.Home) is { Kind: LeaseKind.Headless } headless)
        {
            return new(InstanceFound.Headless, HomeLease.Refusal(world.Home, headless));
        }

        if (RunningAddress.Read(world.Home) is not { } entry || !RunningService.Lives(entry.Pid, entry.Started))
        {
            return new(InstanceFound.None, "Daoris is not running on this home.");
        }

        // HOMEID1: only this home's instance. A file naming another home's id, or a status answering as another's, is not it.
        if (mine is null || (entry.Home is { } its && its != mine) || world.Status(entry.Url) is { Home: { } answered } && answered != mine)
        {
            return new(InstanceFound.None, "the Daoris running here answers as another home's.");
        }

        var running = $"Daoris is running on this home (`Daoris.exe`, pid {entry.Pid}) and did not answer; nothing was done.";
        if (entry.Pipe is not { } pipe)
        {
            return new(InstanceFound.Silent,
                $"{running} It names no pipe for a terminal to send this to: a Daoris from before the terminal's pipe, or one that "
                + "could not serve it, which its machine log says.");
        }

        return await HelloAsync(pipe, mine, world, running, server: entry.Pid, ct).ConfigureAwait(false);
    }

    /// <summary>The pipe connected and its hello said, or why not.</summary>
    private static async Task<InstanceLook> HelloAsync(
        string pipe, string? mine, InstanceWorld world, string silent, int? server, CancellationToken ct)
    {
        var within = $"{world.Answer.TotalSeconds:0.#} seconds";
        var client = await InstanceClient.ConnectAsync(pipe, world.Answer, ct).ConfigureAwait(false);
        if (client is null) return new(InstanceFound.Silent, $"{silent} Its pipe did not answer within {within}.");
        if (server is { } expected && client.Server is { } serving && serving != expected)
        {
            await client.DisposeAsync().ConfigureAwait(false);
            return new(InstanceFound.Silent, $"{silent} Its pipe is served by another process (pid {serving}), so nothing was sent to it.");
        }

        var hello = await client.HelloAsync(mine, world.Answer, ct).ConfigureAwait(false);
        if (hello is not { } answer)
        {
            await client.DisposeAsync().ConfigureAwait(false);
            return new(InstanceFound.Silent, $"{silent} Its pipe did not answer the hello within {within}.");
        }

        if (Succeeded(answer) && answer.TryGetProperty("data", out var data) && InstanceClient.Text(data, "home") is { } home && home == mine)
        {
            return new(InstanceFound.Answers, "", client);
        }

        await client.DisposeAsync().ConfigureAwait(false);
        var (code, message, why) = Refusal(answer);
        return new(InstanceFound.Refused, code switch
        {
            "stopping" when why == "update" => "Daoris is updating; run this again once it is back.",
            "stopping" => "Daoris is closing; run this again once it is back.",
            "protocol" => $"Daoris on this home speaks another version of the terminal's pipe ({message}); nothing was done: run this "
                          + "from the same build as the desktop.",
            _ => $"the Daoris at {pipe} runs on another home ({code ?? "home"}: {message}); nothing was done.",
        });
    }

    /// <summary>
    /// A press of <paramref name="route"/> as §7.2's switch takes it: alone, sent with its answer said by
    /// <paramref name="words"/>, or refused. The exit code is the verb's.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>A timeout never means "do it here"</b> (§7.2): once a request was sent, a lost answer is asked for by <c>RESULT</c>
    /// with the request's key on a new connection, and what it says is said; the press is never sent again nor done alone.
    /// <para>A route the census does not open is not sent, and the verb looks for no instance: until RPC1c confirms a person's press
    /// in the window, a <c>person</c> verb acts alone as it does today, so no row takes a door from the terminal.</para>
    /// </remarks>
    /// <param name="payload">The route's payload, as a window's press sends it.</param>
    /// <param name="alone">The verb acting from its own process, as it does where no instance runs.</param>
    /// <param name="words">The route's answer in the verb's own words, returning its exit code (§7.3).</param>
    public static async Task<int> PressAsync(
        InstanceRoute route, object? payload, Func<Task<int>> alone, Func<JsonElement, int> words, InstanceWorld world,
        CancellationToken ct = default)
    {
        if (route.Class != RouteClass.Open) return await alone().ConfigureAwait(false);

        var look = await FindAsync(world, ct).ConfigureAwait(false);
        switch (look.Found)
        {
            case InstanceFound.None or InstanceFound.Headless:
                return await alone().ConfigureAwait(false);
            case InstanceFound.Silent or InstanceFound.Refused:
                await world.Error.WriteLineAsync($"{world.Verb}: {look.Why}").ConfigureAwait(false);
                return 2;
        }

        var id = Guid.NewGuid().ToString();
        var key = Guid.NewGuid().ToString("N");
        JsonElement? answer;
        await using (var client = look.Client!)
        {
            answer = await client.RequestAsync(id, route.Module, route.Route, payload, key, told => Told(told, id, world), world.Wait, ct)
                .ConfigureAwait(false);
        }

        // The answer was lost (the connection, or the verb's wait over): asked for, never sent again, never done here (§6.1).
        if (answer is null)
        {
            var result = await ResultAsync(client: look, id, key, world, ct).ConfigureAwait(false);
            var state = result is { } said && said.TryGetProperty("data", out var data) ? InstanceClient.Text(data, "state") : null;
            if (state == "answered" && result!.Value.GetProperty("data").TryGetProperty("answer", out var ended))
            {
                answer = ended;
            }
            else
            {
                await world.Error.WriteLineAsync(state == "unknown"
                    ? $"{world.Verb}: Daoris does not know how it ended (request {id}): it stopped while this was under way, and nothing was done again here."
                    : $"{world.Verb}: Daoris was still working on it (request {id}), and its answer did not reach this terminal; nothing was done again here.")
                    .ConfigureAwait(false);
                return 2;
            }
        }

        if (Succeeded(answer.Value))
        {
            return words(answer.Value.TryGetProperty("data", out var data) ? data : default);
        }

        var (code, message, _) = Refusal(answer.Value);
        await world.Error.WriteLineAsync($"{world.Verb}: {message} ({code})").ConfigureAwait(false);
        return 2;
    }

    /// <summary><c>RESULT</c> for a press whose answer was lost, asked on a new connection to the pipe the verb found.</summary>
    private static async Task<JsonElement?> ResultAsync(InstanceLook client, string id, string key, InstanceWorld world, CancellationToken ct)
    {
        await using var again = await InstanceClient.ConnectAsync(client.Client!.Pipe, world.Answer, ct).ConfigureAwait(false);
        if (again is null) return null;
        var mine = world.Home is { } home ? HomeIdentity.Read(home) : null;
        if (await again.HelloAsync(mine, world.Answer, ct).ConfigureAwait(false) is not { } hello || !Succeeded(hello)) return null;
        return await again.ResultAsync(id, key, world.Answer, ct).ConfigureAwait(false);
    }

    /// <summary>A route's progress, said as a line where it names this press and says something (§6).</summary>
    private static void Told(JsonElement told, string id, InstanceWorld world)
    {
        if (!told.TryGetProperty("payload", out var batch) || batch.ValueKind != JsonValueKind.Array) return;
        foreach (var each in batch.EnumerateArray())
        {
            if (!each.TryGetProperty("payload", out var status) || InstanceClient.Text(status, "id") != id) continue;
            if (status.TryGetProperty("detail", out var detail) && InstanceClient.Text(detail, "text") is { Length: > 0 } text)
            {
                world.Out.WriteLine($"  {text}");
            }
        }
    }

    private static bool Succeeded(JsonElement envelope) =>
        envelope.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.True;

    /// <summary>A refusal's code, sentence and <c>why</c>, as the kit's error envelope carries them.</summary>
    private static (string? Code, string Message, string? Why) Refusal(JsonElement envelope)
    {
        if (!envelope.TryGetProperty("error", out var error)) return (null, "Daoris answered no answer it could read", null);
        var why = error.TryGetProperty("parameters", out var parameters) ? InstanceClient.Text(parameters, "why") : null;
        return (InstanceClient.Text(error, "code"), InstanceClient.Text(error, "message") ?? "it said no more", why);
    }
}

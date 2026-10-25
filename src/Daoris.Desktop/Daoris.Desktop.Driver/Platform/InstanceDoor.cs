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

    /// <summary>
    /// The hello was refused (<c>protocol</c>, <c>home</c>, <c>stopping</c>), or the pipe refused this terminal as another account's
    /// or elevation's (PIPEOWNER1): a press is refused, saying which.
    /// </summary>
    Refused,
}

/// <summary>What a verb found, with the sentence that says it and the connection where the instance answered.</summary>
/// <param name="Nonce">The hello's nonce for this connection (SUPERVISOR1a-ii), which a holder's proof is bound to; null from an older Daoris.</param>
public sealed record InstanceLook(InstanceFound Found, string Why, InstanceClient? Client = null, string? Nonce = null);

/// <summary>
/// Where a verb looks for the running Daoris, and what it says while it waits: the process's own, or a test's. It is the terminal the
/// settings' command is said on and pressed from (<see cref="IChoiceTerminal"/>, FOUNDATION1 mod-iii), pressing through the door.
/// </summary>
public sealed class InstanceWorld : IChoiceTerminal
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

    /// <summary>What the person types or pipes in: <c>agent key</c>'s key, never an argument (PUSH1f2e5).</summary>
    public TextReader In { get; init; } = Console.In;

    /// <summary>The verb, as its sentences begin (<c>trees</c>).</summary>
    public string Verb { get; init; } = "daoris-driver";

    /// <summary>How long the pipe is given to connect and answer the hello (§7.2: five seconds).</summary>
    public TimeSpan Answer { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long a press is waited on before its answer is asked for by <c>RESULT</c>: <c>--wait &lt;minutes&gt;</c>, which the host's
    /// entry takes (CONFIRMWAIT1b, the confirm-wait design §4.1). Zero is <c>--no-wait</c>: the verb returns once the instance says the
    /// press waits for the window (<c>CONFIRMING</c>), and a press that asks no window, or a read, is waited on as long as by default.
    /// </summary>
    public TimeSpan Wait { get; init; } = InstanceDoor.LongestWait;

    /// <summary>The bound a request is waited on within: <see cref="Wait"/>, or the default where it is <c>--no-wait</c>'s zero.</summary>
    public TimeSpan Within => Wait > TimeSpan.Zero ? Wait : InstanceDoor.LongestWait;

    /// <summary>
    /// The SIDs whose pipe this terminal takes as its user's: <see cref="InstanceClient.ThisUser"/>'s where null (PIPEOWNER1), or a
    /// test's, which needs no second elevation to meet a pipe it does not take.
    /// </summary>
    public IReadOnlySet<string>? Owners { get; init; }

    /// <summary>
    /// A check of the process serving the pipe, read from the pipe itself, made before anything is sent (SUPERVISOR1a-ii, D208 §2.2):
    /// false refuses it (<see cref="InstanceDoor.NotInstall"/>). Null checks nothing, as every verb but the supervisor's answer.
    /// </summary>
    public Func<int?, bool>? ServedBy { get; init; }

    /// <summary>
    /// Whether this verb runs in a role's terminal (STEWARDROLE1a-i, D209 §3.3): the entry's marker, <see cref="HolderKey.RoleTerminal"/>,
    /// unless a test names its own. Such a verb sends no person's press and acts nothing alone (<see cref="RoleTerminal"/>).
    /// </summary>
    public Func<bool> RoleTerminal { get; init; } = () => HolderKey.RoleTerminal;

    /// <summary>A person's press sent through <see cref="InstanceDoor.PressAsync"/> from this terminal.</summary>
    public Task<int> PressAsync(InstanceRoute route, object? payload, Func<Task<int>> alone, Func<JsonElement, int> words, CancellationToken ct) =>
        InstanceDoor.PressAsync(route, payload, alone, words, this, ct);
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
            var turnedAway = $"the Daoris {Variable} names ({named}) refused this terminal; nothing was done.";
            return await HelloAsync(named, mine, world, silent, turnedAway, server: null, ct).ConfigureAwait(false);
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

        var refused = $"Daoris is running on this home (`Daoris.exe`, pid {entry.Pid}) and refused this terminal; nothing was done.";
        return await HelloAsync(pipe, mine, world, running, refused, server: entry.Pid, ct).ConfigureAwait(false);
    }

    /// <summary>What a pipe that refused this terminal is said as (PIPEOWNER1): never a pipe that did not answer.</summary>
    internal const string NotThisUsers = "Its pipe belongs to another Windows account or elevation.";

    /// <summary>The supervisor's verb's refusal of a pipe not served by the install's own application (SUPERVISOR1a-ii, D208 §2.2).</summary>
    public const string NotInstall = "not-install";

    /// <summary>
    /// Whether <paramref name="server"/> runs the install's own application, <c>app/Daoris.Desktop.exe</c> under the root that carries
    /// the verb (<paramref name="baseDirectory"/>), never the root's <c>Daoris.exe</c> launcher (SUPERVISOR1a-ii, D208 §2.2). Its limit,
    /// said in §4.6: it binds a path, not the owner's instance. False for a workspace build, which no install carries.
    /// </summary>
    public static bool InstallApplication(int? server, string baseDirectory, Func<int, string?>? image = null)
    {
        if (server is not { } pid || DaorisHome.InstallOf(baseDirectory) is not { } install) return false;
        var expected = Path.GetFullPath(Path.Combine(install, "app", "Daoris.Desktop.exe"));
        return (image ?? ImageOf)(pid) is { } path && string.Equals(Path.GetFullPath(path), expected, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The executable a process runs, as the system names it; null where it cannot be read.</summary>
    public static string? ImageOf(int pid)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(pid);
            return process.MainModule?.FileName;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception
                                          or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>The pipe connected and its hello said, or why not.</summary>
    private static async Task<InstanceLook> HelloAsync(
        string pipe, string? mine, InstanceWorld world, string silent, string refused, int? server, CancellationToken ct)
    {
        var within = $"{world.Answer.TotalSeconds:0.#} seconds";
        var connect = await InstanceClient.ConnectAsync(pipe, world.Answer, world.Owners, ct).ConfigureAwait(false);
        if (connect.Refused) return new(InstanceFound.Refused, $"{refused} {NotThisUsers}");
        if (connect.Client is not { } client) return new(InstanceFound.Silent, $"{silent} Its pipe did not answer within {within}.");
        if (server is { } expected && client.Server is { } serving && serving != expected)
        {
            await client.DisposeAsync().ConfigureAwait(false);
            return new(InstanceFound.Silent, $"{silent} Its pipe is served by another process (pid {serving}), so nothing was sent to it.");
        }

        // SUPERVISOR1a-ii (D208 §2.2): a verb that checks the pipe's server checks it here, before even the hello is sent.
        if (world.ServedBy is { } check && !check(client.Server))
        {
            await client.DisposeAsync().ConfigureAwait(false);
            return new(InstanceFound.Refused,
                $"the pipe at {pipe} is not served by this install's `app/Daoris.Desktop.exe`, so nothing was sent to it ({NotInstall}).");
        }

        var hello = await client.HelloAsync(mine, world.Answer, ct).ConfigureAwait(false);
        if (hello is not { } answer)
        {
            await client.DisposeAsync().ConfigureAwait(false);
            return new(InstanceFound.Silent, $"{silent} Its pipe did not answer the hello within {within}.");
        }

        if (Succeeded(answer) && answer.TryGetProperty("data", out var data) && InstanceClient.Text(data, "home") is { } home && home == mine)
        {
            return new(InstanceFound.Answers, "", client, InstanceClient.Text(data, "nonce"));
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
    /// <para><b>A person's press is confirmed in the window</b> (RPC1c, D156 §4.2): the instance shows it there naming this terminal,
    /// and the verb says it waits as the instance tells it so (<c>CONFIRMING</c>), naming the request's id and the verbs that read and
    /// take it back at once (CONFIRMWAIT1b); the confirmed press's answer is said in the verb's words, and the person's refusal or the
    /// two minutes' end is the instance's sentence, exit 1, nothing done here. The verb's own wait over with the press still
    /// <c>waiting</c> is exit 1 too, and an instance from before CONFIRMWAIT1a-i, which answers <c>running</c> then, is said as before,
    /// exit 2; so is the close's answer <c>waiting</c> (CONFIRMWAIT1a-ii), the press kept across the restart. A window's route is never
    /// sent: no terminal verb has one.</para>
    /// </remarks>
    /// <param name="payload">The route's payload, as a window's press sends it.</param>
    /// <param name="alone">The verb acting from its own process, as it does where no instance runs.</param>
    /// <param name="words">The route's answer in the verb's own words, returning its exit code (§7.3).</param>
    public static async Task<int> PressAsync(
        InstanceRoute route, object? payload, Func<Task<int>> alone, Func<JsonElement, int> words, InstanceWorld world,
        CancellationToken ct = default)
    {
        // STEWARDROLE1a-i (D209 §3.3): a role's terminal never presses as the owner nor acts alone, refused before it connects.
        if (world.RoleTerminal()) return await RoleRefusedAsync(world).ConfigureAwait(false);
        if (route.Class == RouteClass.Window) return await alone().ConfigureAwait(false);

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
        // RPC1j: kept before it is sent, so `result <id>` can ask for an answer that never reached this verb.
        if (world.Home is { } kept) new SentPresses(kept).Keep(new SentPress(id, key, $"{route.Module}.{route.Route}", DateTimeOffset.UtcNow));
        JsonElement? answer;
        // CONFIRMWAIT1b (§4.1): `--no-wait` leaves the line once the instance says the press waits, the id already said.
        using var leave = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await using (var client = look.Client!)
        {
            answer = await client.RequestAsync(id, route.Module, route.Route, payload, key, told =>
            {
                if (Told(told, id, world) && world.Wait <= TimeSpan.Zero) leave.Cancel();
            }, world.Within, leave.Token).ConfigureAwait(false);
        }

        // The answer was lost (the connection, or the verb's wait over): asked for, never sent again, never done here (§6.1). Which
        // end it is, RESULT's state says (CONFIRMWAIT1b): a press still waiting for the person is not one under way.
        if (answer is null)
        {
            var result = await ResultAsync(client: look, id, key, world, ct).ConfigureAwait(false);
            var state = result is { } said && said.TryGetProperty("data", out var data) ? InstanceClient.Text(data, "state") : null;
            if (state == "answered" && result!.Value.GetProperty("data").TryGetProperty("answer", out var ended))
            {
                answer = ended;
            }
            else if (state == Waiting)
            {
                if (world.Home is { } home) new SentPresses(home).Waiting(id, DateTimeOffset.UtcNow);
                await world.Error.WriteLineAsync(StillWaiting(world.Verb, id)).ConfigureAwait(false);
                return 1;
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

        // CONFIRMWAIT1 (§15.2 item 6): its answer is its end, so its key keeps a day from now; a close's `waiting` is no end, marked below.
        if (world.Home is { } sent && (Succeeded(answer.Value) || Refusal(answer.Value).Code != Waiting))
        {
            new SentPresses(sent).Ended(id, DateTimeOffset.UtcNow);
        }

        if (Succeeded(answer.Value))
        {
            return words(answer.Value.TryGetProperty("data", out var data) ? data : default);
        }

        var (code, message, _) = Refusal(answer.Value);
        // CONFIRMWAIT1a-ii (§4.1, §7.2): the close answers a line waiting on a press `waiting`, the press kept in its record across the
        // restart; said as this verb's own wait's end.
        if (code == Waiting)
        {
            if (world.Home is { } home) new SentPresses(home).Waiting(id, DateTimeOffset.UtcNow);
            await world.Error.WriteLineAsync(StillWaiting(world.Verb, id)).ConfigureAwait(false);
            return 1;
        }

        await world.Error.WriteLineAsync($"{world.Verb}: {message} ({code})").ConfigureAwait(false);
        return RefusalExit(code);
    }

    /// <summary>
    /// A press's refusal as the verb's exit code: <c>already</c> 0, the world being as asked (CONFIRMWAIT1b, the confirm-wait design
    /// §5.1); the person's or the asker's word on it (<see cref="PersonEnds"/>) 1, a policy answer (RPC1c); any other 2.
    /// </summary>
    private static int RefusalExit(string? code) => code == Already ? 0 : code is not null && PersonEnds.Contains(code) ? 1 : 2;

    /// <summary>The instance's refusal of a person's press the person refused in its window (RPC1c, <c>InstancePipe.Refused</c>).</summary>
    public const string Refused = "refused";

    /// <summary>The instance's refusal of a person's press nobody confirmed within its two minutes (RPC1c, <c>InstancePipe.Expired</c>).</summary>
    public const string Expired = "expired";

    /// <summary>
    /// A press's endings that are the person's or its asker's word, exit 1 at the press and at <c>result</c> alike (the confirm-wait
    /// design §2, §4.2): refused, a ground that changed while it waited, withdrawn, lost at a restart, and an instance from before
    /// CONFIRMWAIT1a-i's two minutes.
    /// </summary>
    public static readonly IReadOnlySet<string> PersonEnds = new HashSet<string>(StringComparer.Ordinal)
    {
        Refused, Expired, "changed", "withdrawn", "lost",
    };

    /// <summary>A press whose world is already as it asks, answered at the ask with no card (the confirm-wait design §5.1): exit 0.</summary>
    public const string Already = "already";

    /// <summary><c>RESULT</c>'s state for a press that waits for the person's Confirm (CONFIRMWAIT1a-i, the confirm-wait design §4.2).</summary>
    public const string Waiting = "waiting";

    /// <summary>The longest a verb waits on a press, and the default: <c>--wait</c>'s bound (the confirm-wait design §4.1).</summary>
    public static readonly TimeSpan LongestWait = TimeSpan.FromMinutes(30);

    /// <summary>What a family's usage says of the wait (CONFIRMWAIT1b), held in each by its usage test.</summary>
    public const string WaitUsage =
        "Each change takes --wait <minutes> (0 to 30, the default 30) or --no-wait: how long to wait on its Confirm in Daoris's window.";

    /// <summary>
    /// What a family says at the person's Ctrl+C (RPC1h, CONFIRMWAIT1b): it withdraws nothing (the confirm-wait design §4.1, §11), so the
    /// two verbs that read and take back what was sent are named.
    /// </summary>
    public const string StoppedAtCtrlC =
        "daoris: stopped at your Ctrl+C. A change already sent to Daoris's window may still be confirmed there: `daoris-driver result <id>` "
        + "asks how it ended, and `daoris-driver withdraw <id>` takes it back; nothing more was done here.";

    /// <summary>
    /// A press still waiting for the person's Confirm when the verb's wait ended (the confirm-wait design §4.1): not lost and not an
    /// error, said with its id and the two verbs, exit 1.
    /// </summary>
    public static string StillWaiting(string verb, string id) =>
        $"{verb}: still waiting for your confirmation in Daoris's window (request {id}); `daoris-driver result {id}` says how it ended, "
        + $"and `daoris-driver withdraw {id}` takes it back (waiting)";

    /// <summary>
    /// Takes <c>--wait &lt;minutes&gt;</c> (0 to 30) and <c>--no-wait</c>, which is <c>--wait 0</c>, from the host's words before any
    /// verb reads them (CONFIRMWAIT1b, the confirm-wait design §4.1): the rest are the verb's, and the wait is set on the world the
    /// verb is given, null where none was said. False, with the problem, for a wait out of bounds, without its minutes, or said twice.
    /// </summary>
    public static bool TakeWait(IReadOnlyList<string> args, out string[] rest, out TimeSpan? wait, out string? problem)
    {
        var left = new List<string>(args.Count);
        wait = null;
        problem = null;
        for (var at = 0; at < args.Count; at++)
        {
            if (args[at] is not ("--wait" or "--no-wait"))
            {
                left.Add(args[at]);
                continue;
            }

            if (wait is not null)
            {
                problem = "--wait or --no-wait is said once.";
                break;
            }

            if (args[at] == "--no-wait")
            {
                wait = TimeSpan.Zero;
                continue;
            }

            if (at + 1 >= args.Count || !int.TryParse(args[at + 1], System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out var minutes) || minutes > LongestWait.TotalMinutes)
            {
                problem = $"--wait takes the minutes to wait on a press's Confirm, 0 to {LongestWait.TotalMinutes:0}.";
                break;
            }

            wait = TimeSpan.FromMinutes(minutes);
            at++;
        }

        rest = [.. left];
        if (problem is null) return true;
        wait = null;
        return false;
    }

    /// <summary>The notification that tells this terminal its press waits for the person in the window (<c>InstancePipe.Confirming</c>).</summary>
    public const string Confirming = "CONFIRMING";

    /// <summary>
    /// The notification that tells this terminal its press runs under the person's standing confirmation, with no card
    /// (<c>InstancePipe.Standing</c>, TERMTRUST1a, D201 point 7).
    /// </summary>
    public const string Standing = "STANDING";

    /// <summary>What the terminal says on its error stream as its press runs under the standing confirmation (the terminal-trust design §6.2).</summary>
    public const string RanStanding = "ran under your standing confirmation; nothing was asked in Daoris's window";

    /// <summary>
    /// Why a press the person's standing confirmation names still waits for the window (the terminal-trust design §5 step 5), by the code
    /// <c>CONFIRMING</c>'s <c>standing</c> carries; null for a code this build does not know, which says the plain wait.
    /// </summary>
    public static string? StandingWhy(string? code) => code switch
    {
        "in-agent-job" => "its standing confirmation does not cover a press from inside an agent Daoris runs",
        "untold" => "Daoris could not tell this terminal is outside every agent it runs, so its standing confirmation does not cover it",
        "form" => "its standing confirmation covers only the verb's own form of this press",
        "not-held-here" => "its standing confirmation resumes only a hold a terminal made under it since Daoris started",
        "running-here" => "it runs here now, and its standing confirmation finishes only a session with nothing of it running",
        _ => null,
    };

    /// <summary>
    /// Which of §7.2's rows a press that goes several routes in one run is in, asked once before the first: the connection it made is
    /// closed, since each press of <see cref="PressAsync"/> finds its own. A press then refused (<see cref="InstanceFound.Silent"/> or
    /// <see cref="InstanceFound.Refused"/>) is said by <see cref="RefusedAsync(InstanceLook, InstanceWorld)"/> and is exit 2.
    /// </summary>
    public static async Task<InstanceLook> PeekAsync(InstanceWorld world, CancellationToken ct = default)
    {
        var look = await FindAsync(world, ct).ConfigureAwait(false);
        if (look.Client is { } client) await client.DisposeAsync().ConfigureAwait(false);
        return look with { Client = null };
    }

    /// <summary>A press refused before it was sent, said as <see cref="PressAsync"/> says it: exit 2.</summary>
    public static async Task<int> RefusedAsync(InstanceLook look, InstanceWorld world)
    {
        await world.Error.WriteLineAsync($"{world.Verb}: {look.Why}").ConfigureAwait(false);
        return 2;
    }

    /// <summary>
    /// A read of <paramref name="route"/> as §7.2's read column takes it (RPC1b2, RPC1e): sent where the instance answers, and
    /// <paramref name="alone"/> wherever it does not (no instance, a pipe that is silent or refuses its hello, an answer that is lost
    /// or refused), since a read changes nothing and acting alone beside an instance is harmless. Said nothing of why.
    /// </summary>
    /// <remarks>
    /// The route's answer is the page's shape, so the <paramref name="words"/> read what it adds for a terminal (<c>said</c>); an
    /// answer that carries none is read alone, as one from an instance that predates the field would be.
    /// </remarks>
    public static async Task<int> ReadAsync(
        InstanceRoute route, object? payload, Func<Task<int>> alone, Func<JsonElement, int?> words, InstanceWorld world,
        CancellationToken ct = default) =>
        await TryReadAsync(route, payload, words, world, ct).ConfigureAwait(false) ?? await alone().ConfigureAwait(false);

    /// <summary>
    /// <see cref="ReadAsync"/> without the alone path: the exit code where the instance answered the read and its words took the
    /// answer, else null, and the verb goes on alone from the next line. For a verb whose alone path is a long run of statements that a
    /// lambda would only move.
    /// </summary>
    public static async Task<int?> TryReadAsync(
        InstanceRoute route, object? payload, Func<JsonElement, int?> words, InstanceWorld world, CancellationToken ct = default)
    {
        // STEWARDROLE1a-i (D209 §3.3): in a role's terminal the read is the instance's or nothing; its alone path is refused, exit 2.
        var role = world.RoleTerminal();
        if (route.Class != RouteClass.Open) return role ? await RoleRefusedAsync(world).ConfigureAwait(false) : null;
        var look = await FindAsync(world, ct).ConfigureAwait(false);
        if (look.Found != InstanceFound.Answers) return role ? await RoleRefusedAsync(world).ConfigureAwait(false) : null;

        JsonElement? answer;
        await using (var client = look.Client!)
        {
            answer = await client.RequestAsync(Guid.NewGuid().ToString(), route.Module, route.Route, payload, Guid.NewGuid().ToString("N"),
                told: null, world.Within, ct).ConfigureAwait(false);
        }

        var exit = answer is { } said && Succeeded(said) ? words(said.TryGetProperty("data", out var data) ? data : default) : null;
        return exit is null && role ? await RoleRefusedAsync(world).ConfigureAwait(false) : exit;
    }

    /// <summary>A role's terminal's verb refused (STEWARDROLE1a-i, D209 §3.3), before it connects or instead of acting alone: exit 2.</summary>
    public static async Task<int> RoleRefusedAsync(InstanceWorld world)
    {
        await world.Error.WriteLineAsync(RoleTerminal.Refusal(world.Verb)).ConfigureAwait(false);
        return 2;
    }

    /// <summary>
    /// A host verb's route as the running Daoris's client (RPC1d, RPC1e, §7.2 and §7.3): where the verb was handed an instance to look
    /// for, an open route is read through <see cref="ReadAsync"/> and any other pressed through <see cref="PressAsync"/>, its answer
    /// said in the verb's own lines (<see cref="TreesSaid"/>, which the route answers asked by <see cref="Said"/>); handed none, the
    /// verb acts alone.
    /// </summary>
    public static Task<int> ThroughAsync(
        InstanceWorld? instance, InstanceRoute route, object payload, Func<Task<int>> alone, TextWriter output, CancellationToken ct = default) =>
        instance is null
            ? alone()
            : route.Class == RouteClass.Open
                ? ReadAsync(route, payload, alone, TreesSaid.ReadWords(output, instance.Error), instance, ct)
                : PressAsync(route, payload, alone, TreesSaid.Words(output, instance.Error, instance.Verb), instance, ct);

    /// <summary>
    /// A verb's payload: the route's own fields, as a window's press sends them, beside <c>said: true</c>, which asks the route for the
    /// verb's lines and exit code (RPC1d). An absent value is left out.
    /// </summary>
    public static Dictionary<string, object?> Said(params (string Name, object? Value)[] fields)
    {
        var body = new Dictionary<string, object?>();
        foreach (var (name, value) in fields)
        {
            if (value is not null) body[name] = value;
        }

        body["said"] = true;
        return body;
    }

    /// <summary>
    /// <c>daoris-driver result &lt;id&gt;</c> (RPC1j, §6.1): how a press this terminal sent ended, asked of the instance with the key it
    /// was sent with. An answered press's words are its route's (<c>said</c>), else its answer as the instance gave it; a press still
    /// under way, one the instance does not know and one it holds for another key are said, exit 2. CONFIRMWAIT1b (the confirm-wait
    /// design §4.2): a press still <c>waiting</c> for the person is said as the verb's own wait's end says it, exit 1, and kept past the
    /// day; an ending the person or the asker made is exit 1 as at the press, <c>already</c> 0.
    /// </summary>
    public static async Task<int> ResultAsync(string id, InstanceWorld world, CancellationToken ct = default)
    {
        var (kept, presses, look) = await KeptAsync(id, world, ct).ConfigureAwait(false);
        if (look is null) return 2;

        JsonElement? result;
        await using (var client = look.Client!)
        {
            result = await client.ResultAsync(id, kept!.Key, world.Answer, ct).ConfigureAwait(false);
        }

        if (result is not { } envelope)
        {
            await world.Error.WriteLineAsync($"{world.Verb}: Daoris did not answer within {world.Answer.TotalSeconds:0.#} seconds; ask again.").ConfigureAwait(false);
            return 2;
        }

        if (!Succeeded(envelope)) return await RefusedAsync(envelope, world).ConfigureAwait(false);
        var data = envelope.TryGetProperty("data", out var held) ? held : default;
        switch (InstanceClient.Text(data, "state"))
        {
            case "answered" when data.TryGetProperty("answer", out var answer):
                presses!.Ended(id, DateTimeOffset.UtcNow);
                if (!Succeeded(answer)) return await RefusedAsync(answer, world).ConfigureAwait(false);
                var inside = answer.TryGetProperty("data", out var body) ? body : default;
                if (TreesSaid.Read(inside) is { } said)
                {
                    said.Print(world.Out, world.Error);
                    return said.Code;
                }

                await world.Out.WriteLineAsync(inside.ValueKind == JsonValueKind.Undefined ? "{}" : inside.GetRawText()).ConfigureAwait(false);
                return 0;
            case Waiting:
                presses!.Waiting(id, DateTimeOffset.UtcNow);
                await world.Error.WriteLineAsync(StillWaiting(world.Verb, id)).ConfigureAwait(false);
                return 1;
            case "running":
                await world.Error.WriteLineAsync($"{world.Verb}: Daoris is still working on it (request {id}); ask again later.").ConfigureAwait(false);
                return 2;
            default:
                presses!.Ended(id, DateTimeOffset.UtcNow);
                await world.Error.WriteLineAsync(
                    $"{world.Verb}: Daoris has no ending for request {id}: it keeps an ending a day, and a press it never began is not in it.")
                    .ConfigureAwait(false);
                return 2;
        }
    }

    /// <summary>
    /// <c>daoris-driver withdraw &lt;id&gt;</c> (CONFIRMWAIT1b, the confirm-wait design §4.3): takes back a press this terminal sent that
    /// still waits for the person's Confirm, by the pipe's own <c>WITHDRAW { id, key }</c> with the key it was sent with, on a new
    /// connection; never the kit's <c>CANCEL</c>, which reaches a confirmed press and aborts it. Taken back is exit 0; confirmed meanwhile
    /// (nothing is cancelled) or already ended, said with its ending, exit 1; anything else exit 2, an instance from before the route
    /// among them, which says so.
    /// </summary>
    public static async Task<int> WithdrawAsync(string id, InstanceWorld world, CancellationToken ct = default)
    {
        var (kept, presses, look) = await KeptAsync(id, world, ct).ConfigureAwait(false);
        if (look is null) return 2;

        JsonElement? withdrawn;
        await using (var client = look.Client!)
        {
            withdrawn = await client.WithdrawAsync(id, kept!.Key, world.Answer, ct).ConfigureAwait(false);
        }

        if (withdrawn is not { } envelope)
        {
            await world.Error.WriteLineAsync($"{world.Verb}: Daoris did not answer within {world.Answer.TotalSeconds:0.#} seconds; ask again.").ConfigureAwait(false);
            return 2;
        }

        if (!Succeeded(envelope))
        {
            if (Refusal(envelope).Code != NoRoute) return await RefusedAsync(envelope, world).ConfigureAwait(false);
            await world.Error.WriteLineAsync(
                $"{world.Verb}: the Daoris running here cannot take a press back (request {id}): it is from before `withdraw`, and a press "
                + "it holds ends by its own time if nobody answers it; nothing was taken back.").ConfigureAwait(false);
            return 2;
        }

        var data = envelope.TryGetProperty("data", out var held) ? held : default;
        switch (InstanceClient.Text(data, "state"))
        {
            case "withdrawn":
                presses!.Ended(id, DateTimeOffset.UtcNow);
                await world.Out.WriteLineAsync(
                    $"{world.Verb}: took back request {id}: it was not done, and its card is gone from Daoris's window.").ConfigureAwait(false);
                return 0;
            case "running":
                await world.Error.WriteLineAsync(
                    $"{world.Verb}: request {id} was confirmed meanwhile and is under way, so nothing was taken back; "
                    + $"`daoris-driver result {id}` says how it ended.").ConfigureAwait(false);
                return 1;
            case "ended":
                presses!.Ended(id, DateTimeOffset.UtcNow);
                string ending;
                if (!data.TryGetProperty("answer", out var answer)) ending = "Daoris did not say how.";
                else if (Succeeded(answer)) ending = $"it was done; `daoris-driver result {id}` says its answer.";
                else
                {
                    var (code, message, _) = Refusal(answer);
                    ending = $"{message} ({code})";
                }

                await world.Error.WriteLineAsync($"{world.Verb}: request {id} had already ended: {ending}").ConfigureAwait(false);
                return 1;
            default:
                await world.Error.WriteLineAsync(
                    $"{world.Verb}: Daoris answered the withdrawal of request {id} in words this terminal does not read; "
                    + $"`daoris-driver result {id}` says how it stands.").ConfigureAwait(false);
                return 2;
        }
    }

    /// <summary>The kit's refusal of a route it does not have: an instance from before the pipe's <c>WITHDRAW</c>.</summary>
    private const string NoRoute = "NO_ROUTE";

    /// <summary>
    /// The press this terminal kept under <paramref name="id"/> and the instance's connection to ask about it, or, said, why there is
    /// none: no press kept, or no instance that answers (null look).
    /// </summary>
    private static async Task<(SentPress? Kept, SentPresses? Presses, InstanceLook? Look)> KeptAsync(
        string id, InstanceWorld world, CancellationToken ct)
    {
        var presses = world.Home is { } home ? new SentPresses(home) : null;
        if (presses?.Find(id, DateTimeOffset.UtcNow) is not { } kept)
        {
            await world.Error.WriteLineAsync(
                $"{world.Verb}: this terminal sent no request {id} within the day Daoris keeps one's answer, or the thirty days it keeps "
                + "one that waits; nothing is known of it.").ConfigureAwait(false);
            return (null, null, null);
        }

        var look = await FindAsync(world, ct).ConfigureAwait(false);
        if (look.Found == InstanceFound.Answers) return (kept, presses, look);
        await world.Error.WriteLineAsync($"{world.Verb}: {look.Why}").ConfigureAwait(false);
        return (kept, presses, null);
    }

    /// <summary>A refusal said, exit as <see cref="PressAsync"/> gives the same refusal (CONFIRMWAIT1b, the confirm-wait design §4.2).</summary>
    private static async Task<int> RefusedAsync(JsonElement envelope, InstanceWorld world)
    {
        var (code, message, _) = Refusal(envelope);
        await world.Error.WriteLineAsync($"{world.Verb}: {message} ({code})").ConfigureAwait(false);
        return RefusalExit(code);
    }

    /// <summary><c>RESULT</c> for a press whose answer was lost, asked on a new connection to the pipe the verb found.</summary>
    private static async Task<JsonElement?> ResultAsync(InstanceLook client, string id, string key, InstanceWorld world, CancellationToken ct)
    {
        var connect = await InstanceClient.ConnectAsync(client.Client!.Pipe, world.Answer, world.Owners, ct).ConfigureAwait(false);
        await using var again = connect.Client;
        if (again is null) return null;
        var mine = world.Home is { } home ? HomeIdentity.Read(home) : null;
        if (await again.HelloAsync(mine, world.Answer, ct).ConfigureAwait(false) is not { } hello || !Succeeded(hello)) return null;
        return await again.ResultAsync(id, key, world.Answer, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// A route's progress, said as a line where it names this press and says something (§6); and that the press waits for the person
    /// in the window (RPC1c), said once on the error, so the output stays the answer's: with the request's id and the verbs that read
    /// and take it back, at once and flushed, before any wait (CONFIRMWAIT1b, the confirm-wait design §4.1). True where it said so.
    /// </summary>
    private static bool Told(JsonElement told, string id, InstanceWorld world)
    {
        if (!told.TryGetProperty("payload", out var batch) || batch.ValueKind != JsonValueKind.Array) return false;
        var confirming = false;
        foreach (var each in batch.EnumerateArray())
        {
            if (!each.TryGetProperty("payload", out var status) || InstanceClient.Text(status, "id") != id) continue;
            if (InstanceClient.Text(each, "module") == InstanceClient.PipeModule && InstanceClient.Text(each, "type") == Confirming)
            {
                // TERMTRUST1a: where the person's standing confirmation names this press and did not cover it, the wait says why.
                var why = StandingWhy(InstanceClient.Text(status, "standing")) is { } standing ? $"; {standing}" : "";
                world.Error.WriteLine(
                    $"{world.Verb}: waiting for you to confirm this in Daoris's window (request {id}{why}); `daoris-driver result {id}` says "
                    + $"how it ended, and `daoris-driver withdraw {id}` takes it back…");
                world.Error.Flush();
                // CONFIRMWAIT1 (the confirm-wait design §15.2 item 6): its record is marked waiting as the id is said, before any RESULT, so a
                // verb killed now leaves its key kept as long as the card may live.
                if (world.Home is { } home) new SentPresses(home).Waiting(id, DateTimeOffset.UtcNow);
                confirming = true;
                continue;
            }

            if (InstanceClient.Text(each, "module") == InstanceClient.PipeModule && InstanceClient.Text(each, "type") == Standing)
            {
                world.Error.WriteLine($"{world.Verb}: {RanStanding}.");
                continue;
            }

            if (status.TryGetProperty("detail", out var detail) && InstanceClient.Text(detail, "text") is { Length: > 0 } text)
            {
                world.Out.WriteLine($"  {text}");
            }
        }

        return confirming;
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

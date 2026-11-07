using System.Text.Json;

namespace Daoris.Driver;

/// <summary>What <see cref="HeldDoor.OpenAsync"/> came to: the exit, the connection where the request is answered, and its answer's data.</summary>
/// <param name="Exit">0 where the instance answered; else the verb's exit, its sentence already said.</param>
/// <param name="Held">The connection, still open, where the instance answered; the caller disposes it where it holds nothing.</param>
/// <param name="Data">The answer's data, or default.</param>
public sealed record HeldOpened(int Exit, HeldConversation? Held, JsonElement Data);

/// <summary>
/// A verb's door to the running Daoris for a conversation: the
/// request that holds what it opens, or <c>HOLD</c> of one already open, sent through the one connection the verb then keeps. It acts
/// alone nowhere: where no Daoris answers on the home it says so, exit 2, and nothing is run.
/// </summary>
public static class HeldDoor
{
    /// <summary>What a verb says where no Daoris runs on the home.</summary>
    public static string NotRunning(string verb) => $"{verb}: Daoris is not running on this home; open it, then run this again.";

    /// <summary>What a verb says where only a headless <c>drive</c> loop holds the home, which serves no pipe and runs no conversation.</summary>
    public static string Headless(string verb) =>
        $"{verb}: only a headless `drive` loop runs on this home, which serves no pipe and runs no conversation; open Daoris, then run this again.";

    /// <summary>What a verb says of the confirmation a hold waits on, beside the wait itself.</summary>
    public const string HoldSaid = "Confirm also lets this terminal speak for you in this conversation until it lets go.";

    /// <summary>
    /// Sends <paramref name="module"/> <paramref name="type"/> with <paramref name="payload"/> as the person's press, <paramref name="hold"/>
    /// asking that it holds what it opens, and waits for its answer and the person's Confirm in the window. <paramref name="told"/> hears
    /// every notification from the connection's first, the forwarded lines of a conversation held among them.
    /// </summary>
    public static async Task<HeldOpened> OpenAsync(
        InstanceWorld world, string module, string type, object payload, bool hold, Action<HeldNotice> told, CancellationToken ct = default)
    {
        // A role's terminal presses nothing as the owner, and acts nowhere alone.
        if (world.RoleTerminal()) return new(await InstanceDoor.RoleRefusedAsync(world).ConfigureAwait(false), null, default);

        var look = await InstanceDoor.FindAsync(world, ct).ConfigureAwait(false);
        switch (look.Found)
        {
            case InstanceFound.None:
                await world.Error.WriteLineAsync(NotRunning(world.Verb)).ConfigureAwait(false);
                return new(2, null, default);
            case InstanceFound.Headless:
                await world.Error.WriteLineAsync(Headless(world.Verb)).ConfigureAwait(false);
                return new(2, null, default);
            case InstanceFound.Silent or InstanceFound.Refused:
                await world.Error.WriteLineAsync($"{world.Verb}: {look.Why}").ConfigureAwait(false);
                return new(2, null, default);
        }

        var id = Guid.NewGuid().ToString();
        var key = Guid.NewGuid().ToString("N");
        var held = new HeldConversation(look.Client!, told);
        held.Told = notice =>
        {
            if (notice is { Module: InstanceClient.PipeModule, Type: InstanceDoor.Confirming } && InstanceClient.Text(notice.Payload, "id") == id)
            {
                world.Error.WriteLine(
                    $"{world.Verb}: waiting for you to confirm this in Daoris's window (request {id}); {HoldSaid} "
                    + $"`daoris press withdraw {id}` takes it back…");
                world.Error.Flush();
                return;
            }

            told(notice);
        };

        if (world.Home is { } home) new SentPresses(home).Keep(new SentPress(id, key, $"{module}.{type}", DateTimeOffset.UtcNow));
        var answer = await held.RequestAsync(id, module, type, payload, key, hold, world.Within, ct).ConfigureAwait(false);
        if (answer is not { } said)
        {
            await held.DisposeAsync().ConfigureAwait(false);
            await world.Error.WriteLineAsync(
                $"{world.Verb}: Daoris did not answer (request {id}); `daoris press result {id}` says how it ended. Nothing is held here.").ConfigureAwait(false);
            return new(2, null, default);
        }

        if (world.Home is { } ended) new SentPresses(ended).Ended(id, DateTimeOffset.UtcNow);
        if (!InstanceDoor.Succeeded(said))
        {
            await held.DisposeAsync().ConfigureAwait(false);
            var (code, message, _) = InstanceDoor.Refusal(said);
            await world.Error.WriteLineAsync($"{world.Verb}: {message} ({code})").ConfigureAwait(false);
            return new(InstanceDoor.RefusalExit(code), null, default);
        }

        return new(0, held, said.TryGetProperty("data", out var data) ? data.Clone() : default);
    }
}

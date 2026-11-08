using System.Collections;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>What a terminal hands the running Daoris with a heavy run: the folder it runs from, its environment and its standard handles.</summary>
public sealed record HeavyTerminal(string Directory, IReadOnlyDictionary<string, string> Environment, StandardHandles Handles)
{
    /// <summary>This process's.</summary>
    public static HeavyTerminal OfThisProcess()
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in System.Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string name && entry.Value is string value) variables[name] = value;
        }

        return new(System.Environment.CurrentDirectory, variables, StandardHandles.OfThisProcess());
    }
}

/// <summary>
/// <c>daoris gate run</c> as the running Daoris's client: the verb asks the instance on its home to run its words
/// (<see cref="HeavyTerminalRun"/>), so the gates are the application's children, in its job and under its admission, writing to
/// this terminal's own output; the verb only waits on the run and exits with its code. Where no Daoris takes the run (none runs on
/// the home, a workspace build, a pipe that does not answer or refuses, a Daoris that cannot take this terminal's handles), it says
/// so in one line and the terminal runs it itself, as it always could: nothing has run yet. A run Daoris took is never run again here.
/// </summary>
/// <remarks>
/// Ctrl+C asks the instance to stop the run (<c>RUN_STOP</c>), which ends what it started and answers. The run is held to this
/// connection: a verb that is killed, or whose session is ended, closes it, and the instance stops the run then too.
/// </remarks>
public static class HeavyAppRun
{
    /// <summary>
    /// Ask the running Daoris to run <paramref name="words"/> for <paramref name="terminal"/>.
    /// </summary>
    /// <param name="interrupts">Hooks the person's Ctrl+C to the action it is given, until disposed.</param>
    /// <returns>The run's exit code where Daoris ran it; null where this terminal runs it itself, the reason said.</returns>
    public static async Task<int?> AskAsync(
        IReadOnlyList<string> words, HeavyTerminal terminal, InstanceWorld world, Func<Action, IDisposable> interrupts, CancellationToken ct = default)
    {
        var look = await InstanceDoor.FindAsync(world, ct).ConfigureAwait(false);
        if (look.Found != InstanceFound.Answers || look.Client is not { } client)
        {
            await Alone(world, look.Why).ConfigureAwait(false);
            return null;
        }

        await using (client)
        {
            var id = Guid.NewGuid().ToString();
            var key = Guid.NewGuid().ToString("N");
            var payload = new
            {
                words,
                directory = terminal.Directory,
                environment = terminal.Environment,
                handles = new { input = terminal.Handles.Input, output = terminal.Handles.Output, error = terminal.Handles.Error },
            };
            try
            {
                await client.WriteLineAsync(
                    JsonSerializer.Serialize(new { id, module = InstanceClient.PipeModule, type = HeavyTerminalRun.Type, payload, key }), ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is IOException or ObjectDisposedException or InvalidOperationException)
            {
                await Alone(world, "Daoris's pipe closed before the run was asked of it.").ConfigureAwait(false);
                return null;
            }

            var stop = JsonSerializer.Serialize(new
            {
                id = Guid.NewGuid().ToString(), module = InstanceClient.PipeModule, type = HeavyTerminalRun.StopType, payload = new { id }, key,
            });
            using var hooked = interrupts(() => _ = StopAsync(client, stop));
            while (await client.ReadAsync(ct).ConfigureAwait(false) is { } envelope)
            {
                if (InstanceClient.Text(envelope, "category") == "notification" || InstanceClient.Text(envelope, "id") != id) continue;
                if (InstanceDoor.Succeeded(envelope))
                {
                    return envelope.TryGetProperty("data", out var data) && data.TryGetProperty("exit", out var exit) && exit.TryGetInt32(out var code)
                        ? code
                        : 2;
                }

                var (_, message, _) = InstanceDoor.Refusal(envelope);
                await Alone(world, $"Daoris did not take the run: {message}").ConfigureAwait(false);
                return null;
            }

            await world.Error.WriteLineAsync(
                "run-heavy: Daoris stopped answering while it ran this; what it started ends with the run, and it was not run again here.")
                .ConfigureAwait(false);
            return 2;
        }
    }

    /// <summary>What a run this terminal runs itself is said with: why Daoris did not, and that its processes are this terminal's.</summary>
    private static Task Alone(InstanceWorld world, string why) =>
        world.Out.WriteLineAsync($"run-heavy: {why.Trim()} This terminal runs it itself, and what it starts is its own.");

    private static async Task StopAsync(InstanceClient client, string stop)
    {
        try
        {
            await client.WriteLineAsync(stop, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException or InvalidOperationException)
        {
            // The connection is gone, which stops the run as surely.
        }
    }
}

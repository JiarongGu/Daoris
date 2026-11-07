using Daoris.Driver;

namespace Daoris.Driver.Host;

/// <summary>
/// The terminal's three doors to a conversation: `daoris chat`, `daoris session start-from` and `daoris session hand-over`.
/// </summary>
/// <remarks>
/// <para>Each hands to <see cref="HeldChatConsole"/>, which holds the running Daoris's conversation. No door runs a
/// conversation in this process: with no Daoris running on the home, nothing runs.</para>
///
/// <para>Exit codes keep the family contract: 0 the conversation ran, 1 it refused, 2 the driver could not run at all.</para>
/// </remarks>
internal static class ChatConsole
{
    /// <summary>
    /// The door, with its sibling consoles' contract: a driver that could not run at all — no home,
    /// no service, a service that did not answer, a file it could not read — says so in one line and exits 2.
    /// </summary>
    public static Task<int> RunAsync(string[] args) => DoorAsync("chat", () => HeldChatConsole.ChatAsync(args));

    /// <summary>`daoris session start-from <id>`: a conversation held here with the words a session could not go on with.</summary>
    public static Task<int> StartFromAsync(string session) => DoorAsync("sessions", () => HeldChatConsole.StartFromAsync(session));

    /// <summary>`daoris session hand-over <id> --agent <adapter> [--account <name>]`: the conversation goes on with another agent, held here.</summary>
    public static Task<int> HandOverAsync(string session, string agent, string? account) =>
        DoorAsync("sessions", () => HeldChatConsole.HandOverAsync(session, agent, account));

    private static async Task<int> DoorAsync(string door, Func<Task<int>> run)
    {
        try
        {
            return await run().ConfigureAwait(false);
        }
        catch (DriverException error)
        {
            Console.Error.WriteLine($"{door}: {error.Message}");
            return 2;
        }
        catch (HttpRequestException error)
        {
            Console.Error.WriteLine($"{door}: could not reach the service — {error.Message}");
            return 2;
        }
    }
}

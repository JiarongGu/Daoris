using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// A stand-in's spawn (AGENTPROVIDER1 row b): the command the machine's configuration names, run in the repository root with the
/// target in the environment, as <see cref="StubAdapter"/> and <see cref="AcpStubAdapter"/> run it. The protocol door keeps stdin open
/// for a driven session, because the driver writes frames into it (D53); a conversation's is open on both.
/// </summary>
internal sealed class StandInSpawn(string id, string example, bool protocol) : ISpawn
{
    public ProcessStartInfo Start(SessionTarget target, IReadOnlyList<string>? command)
    {
        var run = Command(command);
        return Spawning.InRoot(target, run[0], run.Skip(1), redirectInput: protocol);
    }

    public ProcessStartInfo Chat(ChatTarget target, IReadOnlyList<string>? command)
    {
        var run = Command(command);
        return Spawning.ChatInRoot(target, run[0], run.Skip(1));
    }

    private IReadOnlyList<string> Command(IReadOnlyList<string>? command) =>
        command is { Count: > 0 }
            ? command
            : throw new DriverException(
                $"the {id} adapter needs a command — name one in driver.json: "
                + "{ \"commands\": { \"" + id + "\": [\"node\", \"path/to/" + example + "\"] } }");
}

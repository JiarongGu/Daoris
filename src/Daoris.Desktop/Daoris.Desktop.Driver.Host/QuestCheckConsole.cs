namespace Daoris.Driver.Host;

/// <summary>
/// A done's evidence read again from a terminal (EVID1b, D144 §5, D50): <c>daoris-driver quest check &lt;id&gt; [--commit
/// &lt;sha&gt;]</c>. The words are the library's (<see cref="QuestCheckCommand"/>); this wires the real world: the service, the
/// git Daoris runs, and this host's machine log, where the read's <c>evidence.checked</c> line goes.
/// </summary>
internal static class QuestCheckConsole
{
    public static async Task<int> RunAsync(string[] args, MachineLog log)
    {
        // The usage before the service is asked for: a mistyped line needs no service to be told so.
        if (QuestCheckCommand.Read(args, out var problem) is not { } ask)
        {
            Console.Error.WriteLine($"daoris-driver: {problem}");
            Console.Error.WriteLine(QuestCheckCommand.Usage);
            return 2;
        }

        using var service = ServiceClient.FromEnvironment();
        service.EvidenceLined += line => SessionLog.WriteEvidence(log, line);
        return await QuestCheckCommand.RunAsync(ask, new QuestCheckWorld(service), Console.Out).ConfigureAwait(false);
    }
}

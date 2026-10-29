namespace Daoris.Driver;

/// <summary>What <c>daoris-driver sync forward</c> was asked: the quest, and the machine and sequence of the side brought forward.</summary>
public sealed record SyncForwardAsk(string Quest, string Machine, long Sequence);

/// <summary>
/// The terminal's door to the person's bring-forward of one side of two machines' work on a quest (SYNCMERGE1c3; D178 points 2, 7, 8):
/// <c>daoris-driver sync forward &lt;quest&gt; &lt;machine&gt; &lt;sequence&gt;</c>, through the service's person door
/// <c>POST /api/quests/{id}/conflicts/forward</c>, printing the service's sentence.
/// </summary>
/// <remarks>
/// <para><b>Exit codes</b>: 0 brought forward, 1 refused or lost again (a 409 or a 404 is the service's answer, not a tool error), 2 the
/// usage. The parser lives here, not in the Host's <c>SyncConsole</c>, so it is tested (the shape of <see cref="QuestReopenCommand"/>).</para>
/// </remarks>
public static class SyncForwardCommand
{
    public const string Usage =
        "usage: daoris-driver sync forward <quest> <machine> <sequence>\n"
        + "       bring forward one side of two machines' work on a quest: the machine and sequence its conflict names.";

    /// <summary>Whether a <c>sync</c> line asks for a bring-forward.</summary>
    public static bool Asks(IReadOnlyList<string> args) => args is ["forward", ..];

    /// <summary>The line read, or null with why not.</summary>
    public static SyncForwardAsk? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        if (args is ["forward", var id, var machine, var sequence]
            && Word(id.TrimStart('#')) && Word(machine) && long.TryParse(sequence, out var number) && number >= 0)
        {
            return new SyncForwardAsk(id.TrimStart('#'), machine, number);
        }

        problem = "`sync forward` takes a quest's id, the machine, and the sequence (a whole number) of the side to bring forward.";
        return null;
    }

    /// <summary>Post the bring-forward and say the service's sentence: 0 brought forward, 1 refused.</summary>
    public static async Task<int> RunAsync(SyncForwardAsk ask, ServiceClient service, TextWriter output, CancellationToken ct = default)
    {
        var (ok, message) = await service.ForwardConflictAsync(ask.Quest, ask.Machine, ask.Sequence, ct).ConfigureAwait(false);
        output.WriteLine($"sync: {message}");
        return ok ? 0 : 1;
    }

    private static bool Word(string word) => word.Length > 0 && !word.StartsWith('-');
}

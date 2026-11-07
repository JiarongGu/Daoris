namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's rooms written again as the facts they said were not read yet land: only a room that said
/// something not read yet, one writing at a time, and the landings meanwhile written once after. Each workspace's room is written
/// from its own workspace's facts (<see cref="HelpMachine.Within"/>), and a room that said nothing is not read yet is
/// never written, whichever workspace's facts landed.
/// </summary>
/// <param name="describe">The whole machine from the facts as they stand now; null where they cannot say it yet. Reads no git.</param>
public sealed class HelpRoomCatchUp(string home, Func<HelpMachine?> describe, MachineLog? log = null)
{
    private int _writing;
    private int _again;

    /// <summary>Whether a writing is under way, which a test waits out.</summary>
    internal bool Writing => Volatile.Read(ref _writing) == 1;

    /// <summary>A fact landed: each room written last saying something not read yet is written again, off the caller's thread.</summary>
    public void Landed()
    {
        if (!KnowledgeHelp.Current.SaidNotReadYet(home)) return;
        Interlocked.Exchange(ref _again, 1);
        if (Interlocked.CompareExchange(ref _writing, 1, 0) != 0) return;
        _ = Task.Run(Write);
    }

    private void Write()
    {
        try
        {
            while (Interlocked.Exchange(ref _again, 0) == 1 && KnowledgeHelp.Current.Unread(home) is { Count: > 0 } rooms)
            {
                if (describe() is not { } machine) continue;
                foreach (var workspace in rooms)
                {
                    try
                    {
                        KnowledgeHelp.Current.Prepare(home, machine.Within(workspace));
                    }
                    catch (Exception error)
                    {
                        // That room as last written stands, and the next landing or its start writes it again; another's is still written.
                        log?.Failed("Ask Daoris's room written again", error);
                    }
                }
            }
        }
        catch (Exception error)
        {
            // The rooms as last written stand, and the next landing or start writes them again.
            log?.Failed("Ask Daoris's room written again", error);
        }
        finally
        {
            Volatile.Write(ref _writing, 0);
        }

        if (Volatile.Read(ref _again) == 1) Landed();
    }
}

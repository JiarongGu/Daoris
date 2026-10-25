namespace Daoris.Driver;

/// <summary>
/// Whether a start's confirmation is settled, read from its install's swap journal (FOUNDATION1 z-i; the design §13.5 and §19.7, D139):
/// the store holds every import and every stamp-raising migration until it is, so a build rolled back before it confirmed meets every file
/// as it was and a store it can still write.
/// </summary>
/// <remarks>
/// <para><b>Settled</b> is a journal at no phase the launcher can still roll back from: no journal, no install (a workspace build), or
/// <c>confirmed</c>, <c>installed</c> (confirmed or not, the silent swap among them: it is never rolled back after), <c>rolled-back</c> or
/// <c>refused</c>. <c>swapping</c> and <c>started</c> are not, a <c>started</c> with a reason being a roll-back stopped part way, which the
/// launcher finishes as one. A phase this build does not know is not settled either: it cannot say the launcher is done.</para>
/// <para><b>A journal that is there and does not read is never settled</b> (round 6, C5-1.7): <see cref="StagedBuild.ReadJournal"/> answers
/// null for no journal and for one that does not read alike, so the file's presence is read apart, and an unreadable one holds everything,
/// saying so, until it reads. No build identity is compared and none is needed: the journal is the install's, and the launcher waits on the
/// one window it started.</para>
/// <para>It is read at each come-up and look until settled, which in practice is the first read; the read stays because a confirmation's
/// write can fail. It reads a file and writes none.</para>
/// </remarks>
public static class StartConfirmation
{
    /// <summary>The reading for the install this build runs from: the folder above its <c>app/</c> holding <c>INSTALLED.md</c>, none for a workspace build.</summary>
    public static StoreSettled Read() => Read(DaorisHome.InstallOf(AppContext.BaseDirectory));

    /// <summary>The reading for <paramref name="install"/>, null for a build that runs from no install.</summary>
    public static StoreSettled Read(string? install)
    {
        if (install is null || !File.Exists(StagedBuild.JournalOf(install))) return StoreSettled.Yes;
        var journal = StagedBuild.ReadJournal(install);
        if (journal is null)
        {
            // Gone between the presence read and the journal's read: there is none now, which is settled.
            return File.Exists(StagedBuild.JournalOf(install))
                ? StoreSettled.Not("the install's swap journal is there and does not read, so nothing is imported and no stamp raised until it reads")
                : StoreSettled.Yes;
        }

        return journal.Phase switch
        {
            SwapPhase.Confirmed or SwapPhase.Installed or SwapPhase.RolledBack or SwapPhase.Refused => StoreSettled.Yes,
            SwapPhase.Started when journal.Reason is not null =>
                StoreSettled.Not($"the install's swap journal is at `{SwapPhase.Started}` with a roll-back stopped part way ({journal.Reason}), which the launcher finishes"),
            SwapPhase.Swapping or SwapPhase.Started =>
                StoreSettled.Not($"the install's swap journal is at `{journal.Phase}`, which the launcher can still roll back from"),
            _ => StoreSettled.Not($"the install's swap journal is at `{journal.Phase}`, a phase this build does not know"),
        };
    }
}

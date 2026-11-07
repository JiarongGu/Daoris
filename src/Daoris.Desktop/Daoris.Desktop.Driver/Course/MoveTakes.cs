using static Daoris.Driver.MoveRows;

namespace Daoris.Driver;

/// <summary>A move taken or received from a run that goes no further with it, by one guarded update that raises the generation.</summary>
internal sealed class MoveTakes(MoveStore owner)
{
    /// <summary>
    /// Take up a move whose claim's run is dead by the lease's test, in one guarded update that writes this store's run and
    /// raises the generation: refused, nothing written, where the move is gone or ended, where its claim is this run's own, where the generation
    /// moved past <paramref name="generation"/>, or where its run lives or cannot be told dead.
    /// </summary>
    public MoveTaken Take(long move, long generation)
    {
        try
        {
            using var store = owner.Open();
            return store.Write(MoveStore.Family, write =>
            {
                var found = ReadIn(write, move) ?? throw Refused(null, MoveTakeRefusals.Gone);
                if (!found.Stands) throw Refused(found, MoveTakeRefusals.Ended);
                if (string.Equals(found.ClaimRun, owner.Run, StringComparison.Ordinal)) throw Refused(found, MoveTakeRefusals.Own);
                if (found.ClaimGeneration != generation) throw Refused(found, MoveTakeRefusals.Moved);

                // A run the store holds no row of cannot be told dead, so it holds, as a start that cannot be read does.
                if (RunIn(write, found.ClaimRun) is not { } holder || owner.Alive(holder)) throw Refused(found, MoveTakeRefusals.Live);
                var taken = write.Execute(
                    "UPDATE moves SET claim_run = $mine, claim_generation = claim_generation + 1 WHERE id = $id AND claim_run = $run AND claim_generation = $generation",
                    ("$mine", owner.Run), ("$id", move), ("$run", found.ClaimRun), ("$generation", generation));
                if (taken != 1) throw Refused(found, MoveTakeRefusals.Moved);
                var now = ReadIn(write, move)!;
                return new MoveTaken(now, new MoveClaim(move, owner.Run, now.ClaimGeneration), null);
            });
        }
        catch (MoveStore.Unwritten unwritten)
        {
            return (MoveTaken)unwritten.Answer!;
        }

        static MoveStore.Unwritten Refused(MoveRecord? found, string why) => new(new MoveTaken(found, null, why));
    }

    /// <summary>
    /// Receive a hand-over still waiting: decided or quiescing and naming its agent, from a run that
    /// decided it and goes no further with it, a headless host's look or a terminal's verb, which a long-lived host would otherwise hold for good
    /// and a quitting terminal's run only a look after it is dead. One guarded update that raises the generation, as a take does, so the run that
    /// decided it writes nothing more under its old claim. Never from a live shell, whose chat runner takes its own (a dead shell's is taken up);
    /// refused, nothing written, where the move is gone, ended, this run's own, past its wait or naming no agent, where the generation moved
    /// past <paramref name="generation"/>, or where its run is a live shell's or cannot be told.
    /// </summary>
    public MoveTaken Receive(long move, long generation)
    {
        try
        {
            using var store = owner.Open();
            return store.Write(MoveStore.Family, write =>
            {
                var found = ReadIn(write, move) ?? throw Refused(null, MoveTakeRefusals.Gone);
                if (!found.Stands) throw Refused(found, MoveTakeRefusals.Ended);
                if (string.Equals(found.ClaimRun, owner.Run, StringComparison.Ordinal)) throw Refused(found, MoveTakeRefusals.Own);
                if (found is not { Agent: not null, Phase: MoveRecordPhase.Decided or MoveRecordPhase.Quiescing }) throw Refused(found, MoveTakeRefusals.NotWaiting);
                if (found.ClaimGeneration != generation) throw Refused(found, MoveTakeRefusals.Moved);

                // A run the store holds no row of cannot be told, so it holds; a shell's lives or not by the lease's test, any other kind goes no further.
                if (RunIn(write, found.ClaimRun) is not { } holder
                    || (string.Equals(holder.Kind, StoreProcess.Shell, StringComparison.Ordinal) && owner.Alive(holder)))
                {
                    throw Refused(found, MoveTakeRefusals.Live);
                }

                var taken = write.Execute(
                    "UPDATE moves SET claim_run = $mine, claim_generation = claim_generation + 1 WHERE id = $id AND claim_run = $run AND claim_generation = $generation",
                    ("$mine", owner.Run), ("$id", move), ("$run", found.ClaimRun), ("$generation", generation));
                if (taken != 1) throw Refused(found, MoveTakeRefusals.Moved);
                var now = ReadIn(write, move)!;
                return new MoveTaken(now, new MoveClaim(move, owner.Run, now.ClaimGeneration), null);
            });
        }
        catch (MoveStore.Unwritten unwritten)
        {
            return (MoveTaken)unwritten.Answer!;
        }

        static MoveStore.Unwritten Refused(MoveRecord? found, string why) => new(new MoveTaken(found, null, why));
    }
}

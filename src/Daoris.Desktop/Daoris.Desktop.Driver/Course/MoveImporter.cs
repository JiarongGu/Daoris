using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

using static Daoris.Driver.MoveRows;

/// <summary>A box's move written as the store's own row, with the import's receipt, in one transaction.</summary>
internal sealed class MoveImporter(MoveStore owner)
{
    /// <summary>
    /// Write the move an older build's box kept as this store's own row, claimed by this store's run at generation 1, with the import's receipt
    /// in the same transaction (§13.5): held, nothing written, until the start's confirmation is settled; and refused, nothing written, where
    /// the record left already has a standing move, which it then waits behind. Its moments are the box's where it says one, else the import's.
    /// </summary>
    /// <exception cref="ArgumentException">The box is at a phase no box keeps, or at <c>opened</c> with no destination, or does not read as a decision.</exception>
    public MoveImported Import(StoreImport receipt, MoveFromBox box)
    {
        var decision = box.Decision;
        MoveStore.Required(decision.Left, "the record it leaves");
        MoveStore.Known(decision.Executor, MoveExecutors.All, "executor");
        MoveStore.Known(decision.How, MoveHows.All, "how");
        MoveStore.Required(decision.Why, "its why");
        MoveStore.Required(decision.By, "who asked for it");
        if (box.Phase is not (MoveRecordPhase.Decided or MoveRecordPhase.OldEnded or MoveRecordPhase.Opened))
        {
            throw new ArgumentException($"no box keeps a move at `{MovePhases.Word(box.Phase)}`", nameof(box));
        }

        if (box.Phase == MoveRecordPhase.Opened) MoveStore.Required(box.Destination, "the record its open made");
        if (owner.Settled() is { Is: false } waiting) return new MoveImported(null, null, waiting.Said ?? "the start's confirmation is not settled");
        try
        {
            using var store = owner.Open();
            MoveRecord? made = null;
            var outcome = store.Import(receipt, write =>
            {
                if (StandingToIn(write, decision.Left) is { } to) throw new MoveStore.Unwritten(new MoveImported(null, to, null));
                var at = owner.Now();
                try
                {
                    owner.Insert(write, decision, box.Phase, box.DecidedAt ?? at, at, box.Phase == MoveRecordPhase.Opened ? box.Destination : null);
                }
                catch (SqliteException error) when (error.SqliteExtendedErrorCode == MoveStore.UniqueRefused)
                {
                    var standing = StandingIn(write, decision.Left);
                    if (standing is null) throw;
                    throw new MoveStore.Unwritten(new MoveImported(null, standing, null));
                }

                var id = write.Scalar<long>("SELECT last_insert_rowid()");
                foreach (var word in box.Words)
                {
                    write.Execute(
                        "INSERT INTO move_words (move, transfer, word, producer, position) VALUES ($move, $transfer, $word, $producer, $position)",
                        ("$move", id), ("$transfer", MoveStore.Required(word.Transfer, "a word's transfer")), ("$word", MoveStore.Required(word.Word, "a word's id")),
                        ("$producer", word.Producer), ("$position", word.Position));
                }

                made = ReadIn(write, id);
            });
            return outcome.Done ? new MoveImported(made, null, null) : new MoveImported(null, null, outcome.Said ?? "the start's confirmation is not settled");
        }
        catch (MoveStore.Unwritten unwritten)
        {
            return (MoveImported)unwritten.Answer!;
        }
    }
}

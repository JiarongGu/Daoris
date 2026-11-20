namespace Daoris.Driver;

/// <summary>
/// What the driver keeps of each opinion it delivers: <c>&lt;home&gt;/opinions/&lt;id&gt;/delivered.json</c>, beside the
/// pass's diff: whether its findings went to the person and why, the working session's answers as read when its turn
/// ended, and the recheck asked or why none is. This machine's own, as the opinion is.
/// </summary>
/// <remarks>
/// <para>A file that does not read is nothing done: the next look reads the host and the record again, never the words lost.</para>
/// <para>What is kept once and what the latest replaces is this use case's; the file and its format are
/// <see cref="OpinionDeliveryFiles"/>'.</para>
/// </remarks>
public sealed class OpinionDeliveries(string home)
{
    public const string FileName = "delivered.json";

    /// <summary>Where an opinion's delivery is kept; null for an id that is no plain name.</summary>
    public string? PathOf(string opinion)
    {
        try
        {
            return Path.Combine(OpinionPackets.Folder(home, opinion), FileName);
        }
        catch (DriverException)
        {
            return null;
        }
    }

    /// <summary>What is kept for <paramref name="opinion"/>; <see cref="OpinionDelivered.None"/> where nothing reads.</summary>
    public OpinionDelivered Read(string opinion) => PathOf(opinion) is { } path ? OpinionDeliveryFiles.Read(path) : OpinionDelivered.None;

    /// <summary>Its findings went to the person, and why: kept once, the first reason standing.</summary>
    public OpinionDelivered ToPerson(string opinion, string why, DateTimeOffset at) =>
        Keep(opinion, kept => kept.Person is not null ? kept : kept with { Person = why, PersonAt = at });

    /// <summary>The answers as read when the working session's turn ended: the latest reading stands.</summary>
    public OpinionDelivered Answered(string opinion, OpinionReading reading) => Keep(opinion, kept => kept with { Answered = reading });

    /// <summary>The opinion's consultation record, <c>consultation.json</c> beside its delivery, in the brainstorm's shape; a write that fails is lost.</summary>
    public void Consulted(OpinionView view, OpinionReading? answered)
    {
        if (PathOf(view.Id) is not { } path) return;
        try
        {
            AtomicFile.WriteBytes(Path.Combine(Path.GetDirectoryName(path)!, ConsultationFile), Consultations.Json(OpinionConsultations.Of(view, answered)));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Lost: the next answer writes it again.
        }
    }

    public const string ConsultationFile = "consultation.json";

    /// <summary>The recheck asked, by its opinion's id, or why none is: kept once.</summary>
    public OpinionDelivered Rechecked(string opinion, string? recheck, string? why) =>
        Keep(opinion, kept => kept.Recheck is not null || kept.RecheckWhy is not null ? kept : kept with { Recheck = recheck, RecheckWhy = why });

    /// <summary>Answers read again past the reading kept: a recheck the older reading found no fix for is judged again; any other stays.</summary>
    public OpinionDelivered Reread(string opinion) =>
        Keep(opinion, kept => kept.Recheck is null && kept.RecheckWhy is OpinionRechecks.NoFix or OpinionRechecks.NotAnswered
            ? kept with { RecheckWhy = null }
            : kept);

    /// <summary>The recheck a cap ended, replaced by the one asked of the next holder, only while it is still the one kept.</summary>
    public OpinionDelivered HandedOn(string opinion, string capped, string recheck) =>
        Keep(opinion, kept => string.Equals(kept.Recheck, capped, StringComparison.Ordinal) ? kept with { Recheck = recheck } : kept);

    private OpinionDelivered Keep(string opinion, Func<OpinionDelivered, OpinionDelivered> change)
    {
        var next = change(Read(opinion));
        if (PathOf(opinion) is { } path) OpinionDeliveryFiles.Write(path, next);
        return next;
    }
}

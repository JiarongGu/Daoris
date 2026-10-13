namespace Daoris.Driver;

/// <summary>
/// What the driver keeps of each opinion it delivers (XAGENT1e): <c>&lt;home&gt;/opinions/&lt;id&gt;/delivered.json</c>, beside the
/// pass's diff (§4): whether its findings went to the person and why, the working session's answers as read when its turn
/// ended, and the recheck asked or why none was. This machine's own, as the opinion is (D47 §4).
/// </summary>
/// <remarks>
/// <para>A file that does not read is nothing done: the next look reads the host and the record again, never the words lost.</para>
/// <para>What is kept once and what the latest replaces is this use case's; the file and its format are Review's Infrastructure's
/// (<see cref="OpinionDeliveryFiles"/>; STRUCT3c, D166's note).</para>
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

    /// <summary>What was kept for <paramref name="opinion"/>; <see cref="OpinionDelivered.None"/> where nothing reads.</summary>
    public OpinionDelivered Read(string opinion) => PathOf(opinion) is { } path ? OpinionDeliveryFiles.Read(path) : OpinionDelivered.None;

    /// <summary>Its findings went to the person, and why: kept once, the first reason standing.</summary>
    public OpinionDelivered ToPerson(string opinion, string why, DateTimeOffset at) =>
        Keep(opinion, kept => kept.Person is not null ? kept : kept with { Person = why, PersonAt = at });

    /// <summary>The answers as read when the working session's turn ended: the latest reading stands.</summary>
    public OpinionDelivered Answered(string opinion, OpinionReading reading) => Keep(opinion, kept => kept with { Answered = reading });

    /// <summary>The recheck asked, by its opinion's id, or why none was: kept once.</summary>
    public OpinionDelivered Rechecked(string opinion, string? recheck, string? why) =>
        Keep(opinion, kept => kept.Recheck is not null || kept.RecheckWhy is not null ? kept : kept with { Recheck = recheck, RecheckWhy = why });

    private OpinionDelivered Keep(string opinion, Func<OpinionDelivered, OpinionDelivered> change)
    {
        var next = change(Read(opinion));
        if (PathOf(opinion) is { } path) OpinionDeliveryFiles.Write(path, next);
        return next;
    }
}

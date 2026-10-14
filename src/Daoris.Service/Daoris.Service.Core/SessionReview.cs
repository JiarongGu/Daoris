using System.Globalization;
using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// What a set-up step's session asked Daoris to serve to its tab (REVIEWENV1b, design §2.6): a folder in its tree, and the
/// address it is served at. Kept until the session asks again, and named by each set-up it says after.
/// </summary>
/// <param name="Folder">The folder, as a path in the session's tree.</param>
/// <param name="Address">Where it is served: the environment's address, as the session named it.</param>
/// <param name="At">When it asked.</param>
public sealed record ReviewServing(string Folder, string Address, DateTimeOffset At);

/// <summary>
/// A set-up a session said through its connector (REVIEWENV1b, design §2.6), waiting for its driver to read the commit its
/// tree holds and post it on the quest. The words are the session's; the commit never is.
/// </summary>
/// <param name="Number">Its number on the session's record, from 1, in the order said.</param>
/// <param name="Look">The address the step's tab is at.</param>
/// <param name="Shows">What it showed and what to look at.</param>
/// <param name="Again">How to show it again by hand.</param>
/// <param name="At">When it was said.</param>
public sealed record ReviewSaid(int Number, string Look, string Shows, string Again, DateTimeOffset At)
{
    /// <summary>A review run's command, quoted from the procedure; null where it named none.</summary>
    public string? Run { get; init; }

    /// <summary>The folder the session last asked Daoris to serve before saying it, where it asked for one.</summary>
    public string? Served { get; init; }

    /// <summary>Whether its driver posted it on the quest, with the commit it read.</summary>
    public bool Posted { get; init; }
}

/// <summary>What a session's record keeps of its review (REVIEWENV1b): its serving, and every set-up it said, oldest first.</summary>
public sealed record SessionReview(ReviewServing? Serving, IReadOnlyList<ReviewSaid> Said)
{
    /// <summary>A record that asked nothing and said nothing.</summary>
    public static readonly SessionReview None = new(null, []);
}

/// <summary>
/// A session's review as its record stores it (REVIEWENV1b): the store reads and writes it blind, like every write here, and the
/// ledger and the exchange judge. Its stored shape is kept as it was; only where it is spelled moved (PART6).
/// </summary>
internal static class SessionReviewJson
{
    /// <summary>
    /// A review as stored. A part this build cannot read, or a set-up missing its words or its moment, is passed over: a later
    /// build's field, or a malformed one, is never a failed read of the record.
    /// </summary>
    internal static SessionReview ReviewOf(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return SessionReview.None;

            ReviewServing? serving = null;
            if (root.TryGetProperty("serving", out var served) && served.ValueKind == JsonValueKind.Object
                && JsonFields.Text(served, "folder") is { } folder && JsonFields.Text(served, "address") is { } address
                && Moments.Read(served, "at") is { } servedAt)
            {
                serving = new ReviewServing(folder, address, servedAt);
            }

            var said = new List<ReviewSaid>();
            foreach (var each in JsonFields.Items(root, "said"))
            {
                if (JsonFields.Number(each, "number") is not { } number || number is < 1 or > int.MaxValue
                    || JsonFields.Text(each, "look") is not { } look || JsonFields.Text(each, "shows") is not { } shows
                    || JsonFields.Text(each, "again") is not { } again || Moments.Read(each, "at") is not { } at)
                {
                    continue;
                }

                said.Add(new ReviewSaid((int)number, look, shows, again, at)
                {
                    Run = JsonFields.Text(each, "run"),
                    Served = JsonFields.Text(each, "served"),
                    Posted = each.TryGetProperty("posted", out var posted) && posted.ValueKind == JsonValueKind.True,
                });
            }

            return new SessionReview(serving, said);
        }
        catch (JsonException)
        {
            return SessionReview.None;
        }
    }

    // Hand-rolled for the reason every store's lists are: nothing here may stop working under AOT.
    internal static string ReviewJson(SessionReview review) => JsonFields.Written(writer =>
    {
        writer.WriteStartObject();
        if (review.Serving is { } serving)
        {
            writer.WriteStartObject("serving");
            writer.WriteString("folder", serving.Folder);
            writer.WriteString("address", serving.Address);
            writer.WriteString("at", Moments.Write(serving.At));
            writer.WriteEndObject();
        }

        writer.WriteStartArray("said");
        foreach (var said in review.Said)
        {
            writer.WriteStartObject();
            writer.WriteNumber("number", said.Number);
            writer.WriteString("look", said.Look);
            writer.WriteString("shows", said.Shows);
            writer.WriteString("again", said.Again);
            if (said.Run is not null) writer.WriteString("run", said.Run);
            if (said.Served is not null) writer.WriteString("served", said.Served);
            writer.WriteString("at", Moments.Write(said.At));
            if (said.Posted) writer.WriteBoolean("posted", true);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    });

}

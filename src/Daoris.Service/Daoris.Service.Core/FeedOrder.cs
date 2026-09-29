using System.Security.Cryptography;
using System.Text;

namespace Daoris.Knowledge;

/// <summary>What a feed's arrival means against what this deployment holds (SYNC5a).</summary>
public enum FeedVerdict
{
    /// <summary>Replace what is held: nothing was, the feed fast-forwards it, or it is newer by commit time.</summary>
    Take,

    /// <summary>The same commit, read the same way — nothing to do, and the first feeder stays credited.</summary>
    AlreadyHeld,

    /// <summary>The same commit, read differently. The first reading stands (SYNC0c).</summary>
    ContentDiffers,

    /// <summary>The feed was checked against a commit that another machine has since replaced.</summary>
    Moved,

    /// <summary>The feed could not be ordered by ancestry and is older by commit time.</summary>
    Stale,
}

/// <summary>Which commit a repository's knowledge, code map and declaration stand on at this deployment, each on its own.</summary>
public sealed record FeedHeld(string? Knowledge, string? CodeMap, string? Registration = null);

/// <summary>
/// The ordering rule for anything fed from a checkout (sync design §8): the machine with the checkout
/// asks git, and the deployment checks that the commit it answered against is still the one held.
/// </summary>
/// <remarks>
/// <para>The deployment cannot run git, so ancestry is asked where the checkout is and carried here as
/// <see cref="FeedProvenance.Base"/>. That makes the take a compare-and-swap: a feed based on what is
/// held fast-forwards it, and one based on anything else has lost a race it did not see.</para>
///
/// <para>Commit time is the fallback, not the rule. It orders a diverged history and an older client,
/// which cannot do better; a rebase keeps author dates and a clock can be wrong, so ancestry wins
/// wherever it was asked.</para>
/// </remarks>
public static class FeedOrder
{
    /// <param name="held">What this deployment holds, or null where nothing has fed.</param>
    /// <param name="arriving">The feed, its <see cref="FeedProvenance.Digest"/> already computed here.</param>
    public static FeedVerdict Judge(FeedProvenance? held, FeedProvenance arriving)
    {
        if (held is null) return FeedVerdict.Take;

        if (string.Equals(held.Commit, arriving.Commit, StringComparison.OrdinalIgnoreCase))
        {
            // A row from before digests has nothing to compare, so the commit is read once more and
            // its digest recorded. Refusing it would freeze the repository at that deployment.
            if (held.Digest is null) return FeedVerdict.Take;

            return string.Equals(held.Digest, arriving.Digest, StringComparison.Ordinal)
                ? FeedVerdict.AlreadyHeld
                : FeedVerdict.ContentDiffers;
        }

        if (arriving.Base is { Length: > 0 } checkedAgainst)
        {
            return string.Equals(checkedAgainst, held.Commit, StringComparison.OrdinalIgnoreCase)
                ? FeedVerdict.Take
                : FeedVerdict.Moved;
        }

        // Equal times with different commits cannot be ordered, so the arriving one takes: a tie is
        // not evidence of staleness.
        return arriving.CommittedAt < held.CommittedAt ? FeedVerdict.Stale : FeedVerdict.Take;
    }
}

/// <summary>
/// What a feed SAYS, as one hash — so that the same commit fed twice can be told apart from the same
/// commit read two ways (SYNC0c).
/// </summary>
/// <remarks>
/// Computed at the deployment over what it would store, never trusted from the wire: a digest the
/// feeder supplied would let it claim sameness while sending something else. Every field is
/// length-prefixed, so two fields meeting at a different boundary cannot hash alike.
/// </remarks>
public static class FeedDigest
{
    /// <summary>Knowledge entries, in identity order — arrival order is not content.</summary>
    public static string Of(IEnumerable<KnowledgeEntry> entries)
    {
        var text = new StringBuilder();
        foreach (var entry in entries.OrderBy(e => e.Id, StringComparer.Ordinal))
        {
            Field(text, entry.Kind.ToString());
            Field(text, entry.Title);
            Field(text, entry.Body);
            Field(text, entry.RelativePath);
            Field(text, entry.Anchor);
        }

        return Of(text.ToString());
    }

    /// <summary>
    /// A repository's declaration (SYNC5b): what it says about itself, and nothing the deployment
    /// decides — the root it never keeps, the entry count it computes, the workspace its own wiring sets.
    /// </summary>
    public static string Of(Registration registration)
    {
        var text = new StringBuilder();
        Field(text, registration.Summary);
        List(text, registration.Owns);
        List(text, registration.Accepts);
        List(text, registration.Packs);
        Field(text, registration.Joined ? "joined" : "local");
        Field(text, registration.SharesKnowledge ? "shares" : "keeps");
        Field(text, registration.DefaultBranch);
        // What it says it uses (D91), only when it says something: a declaration of none hashes as it
        // did before the field existed, so every row a deployment already holds keeps its digest.
        if (registration.DependsOn.Count > 0) List(text, registration.DependsOn);
        return Of(text.ToString());
    }

    /// <summary>A body stored whole, such as a code map in its canonical form.</summary>
    public static string Of(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static void Field(StringBuilder text, string? value)
    {
        // Null and empty are different statements (an anchor absent is not an anchor named ""), so a
        // null carries a length no string can have.
        text.Append(value is null ? "-1" : value.Length.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Append(':')
            .Append(value)
            .Append('|');
    }

    /// <summary>A list is its length, then its items: order is part of what a declaration says.</summary>
    private static void List(StringBuilder text, IReadOnlyList<string> items)
    {
        Field(text, items.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        foreach (var item in items) Field(text, item);
    }
}

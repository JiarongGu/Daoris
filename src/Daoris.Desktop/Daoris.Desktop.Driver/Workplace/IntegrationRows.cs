using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>The integration family's rows read into its records, and the text a record's proof and files are kept as.</summary>
internal static class IntegrationRows
{
    internal static string? Text(SqliteDataReader row, string column)
    {
        var at = row.GetOrdinal(column);
        return row.IsDBNull(at) ? null : row.GetString(at);
    }

    internal static long? Number(SqliteDataReader row, string column)
    {
        var at = row.GetOrdinal(column);
        return row.IsDBNull(at) ? null : row.GetInt64(at);
    }

    internal static DateTimeOffset Moment(SqliteDataReader row, string column) =>
        Moments.Read(Text(row, column)) ?? throw new DesktopStoreException($"the integration's `{column}` does not read as a moment");

    internal static IntegrationRequest MapRequest(SqliteDataReader row)
    {
        var generation = Number(row, "generation");
        var requester = new IntegrationRequester(Text(row, "requester")!, Text(row, "door")!) { Act = Text(row, "act"), Generation = generation };
        var policy = new LandingIntegrate(
            Text(row, "policy_request"), Text(row, "policy_commit"), (int?)Number(row, "policy_batch"), Text(row, "policy_order"), Text(row, "policy_gates"),
            Text(row, "policy_unreached"), Text(row, "policy_reached"), Text(row, "policy_conflicts"), Text(row, "policy_overlap"), Text(row, "policy_line"));
        return new IntegrationRequest(
            Text(row, "id")!, Text(row, "repository")!, Text(row, "workspace")!, Text(row, "branch")!, Text(row, "tip")!, Text(row, "route")!,
            Text(row, "line")!, requester, policy, Moment(row, "at"))
        {
            RuleSource = Text(row, "rule_source"),
            Unit = Text(row, "unit"),
            Session = Text(row, "session"),
            Quest = Text(row, "quest"),
            LandedAt = Moments.Read(Text(row, "landed_at")),
            Proof = ProofOf(Text(row, "proof")),
            ClosedAt = Moments.Read(Text(row, "closed_at")),
            ClosedWhy = Text(row, "closed_why"),
            Order = Has(row, "queue_order") ? Number(row, "queue_order") : null,
            Epoch = Has(row, "epoch") ? Text(row, "epoch") : null,
        };
    }

    internal static IntegrationCandidate MapCandidate(SqliteDataReader row) =>
        new(Text(row, "id")!, Text(row, "repository")!, Text(row, "workspace")!, Text(row, "line")!, Text(row, "base")!, Text(row, "job")!,
            Text(row, "claim_run")!, Number(row, "claim_generation")!.Value, Moment(row, "formed_at"))
        {
            Tip = Text(row, "tip"),
            PromotedAt = Moments.Read(Text(row, "promoted_at")),
            EndedAt = Moments.Read(Text(row, "ended_at")),
            EndedWhy = Text(row, "ended_why"),
        };

    internal static IntegrationMember MapMember(SqliteDataReader row) =>
        new(Text(row, "candidate")!, Text(row, "request")!, (int)Number(row, "position")!.Value, Text(row, "state")!, Moment(row, "joined_at"))
        {
            Replayed = Text(row, "replayed"),
            ContainedBy = Text(row, "contained_by"),
            Said = Text(row, "said"),
            EndedAt = Moments.Read(Text(row, "ended_at")),
        };

    internal static IntegrationStep MapStep(SqliteDataReader row) =>
        new(Number(row, "id")!.Value, Text(row, "candidate")!, Text(row, "step")!, (int)Number(row, "attempt")!.Value, Text(row, "claim_run")!,
            Number(row, "claim_generation")!.Value, Moment(row, "began_at"))
        {
            Request = Text(row, "request"),
            LineCommit = Text(row, "line_commit"),
            EndedAt = Moments.Read(Text(row, "ended_at")),
            Outcome = Text(row, "outcome"),
            Said = Text(row, "said"),
        };

    /// <summary>
    /// The gate proof in the landing record's own shape (<c>acceptedBy</c>, <c>review</c>, <c>opinion</c>, <c>workflow</c>), written by that
    /// record's own writers so a request keeps it as its landing kept it; null for a records branch's, which carries none.
    /// </summary>
    internal static string? ProofText(IntegrationProof? proof)
    {
        if (proof is null) return null;
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("acceptedBy", proof.AcceptedBy);
            if (proof.Review is { } review) LandedBranchJson.WriteReview(writer, review);
            if (proof.Opinion is { } opinion) LandedBranchJson.WriteOpinion(writer, opinion);
            if (proof.Workflow is { } workflow) LandedBranchJson.WriteWorkflow(writer, workflow);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>The gate proof as <see cref="ProofText"/> writes it, read by the landing record's own readers; null where none is kept or it does not read.</summary>
    internal static IntegrationProof? ProofOf(string? text)
    {
        if (text is null) return null;
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("acceptedBy", out var by) || by.ValueKind != JsonValueKind.String) return null;
            return new IntegrationProof(by.GetString()!)
            {
                Review = LandedBranchJson.ReviewOf(root),
                Opinion = LandedBranchJson.OpinionOf(root),
                Workflow = LandedBranchJson.WorkflowOf(root),
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static IntegrationDisposition MapDisposition(SqliteDataReader row) =>
        new(Text(row, "request")!, Text(row, "commit_id")!, Text(row, "reference")!, Text(row, "by_commit")!, FilesOf(Text(row, "files")), Text(row, "subject")!)
        {
            Candidate = Text(row, "candidate")!,
            At = Moment(row, "at"),
            AnsweredAt = Moments.Read(Text(row, "answered_at")),
            Answer = Has(row, "answer") ? Text(row, "answer") : null,
        };

    /// <summary>Whether the row's query named <paramref name="column"/>: a later step's column is named only of a store that holds it.</summary>
    internal static bool Has(SqliteDataReader row, string column)
    {
        for (var at = 0; at < row.FieldCount; at++)
        {
            if (string.Equals(row.GetName(at), column, StringComparison.Ordinal)) return true;
        }

        return false;
    }

    internal static string FilesText(IReadOnlyList<string> files) => JsonSerializer.Serialize(files);

    internal static IReadOnlyList<string> FilesOf(string? text)
    {
        try
        {
            return text is null ? [] : JsonSerializer.Deserialize<string[]>(text) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

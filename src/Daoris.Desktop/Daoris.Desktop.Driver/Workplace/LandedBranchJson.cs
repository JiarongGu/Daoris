using System.Globalization;
using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>The landings file written and read by hand, as the driver's other files are, for the AOT reason <see cref="DriverConfig"/> gives.</summary>
internal static class LandedBranchJson
{
    /// <summary>Written by hand, as the driver's other files are, for the AOT reason <see cref="DriverConfig"/> gives.</summary>
    internal static string ToJson(IReadOnlyList<LandedBranch> entries) => JsonFile.Serialize(writer =>
    {
        writer.WriteStartObject();
        writer.WriteStartArray("branches");
        foreach (var entry in entries)
        {
            writer.WriteStartObject();
            writer.WriteString("repository", entry.Repository);
            writer.WriteString("workspace", entry.Workspace);
            writer.WriteString("branch", entry.Branch);
            if (entry.Line is not null) writer.WriteString("line", entry.Line);
            writer.WriteString("tip", entry.Tip);
            // Written only where the landing squashed (PROCESS1b-ii): absent is the session's own commits on the branch.
            if (entry.Squashed is not null) writer.WriteString("squashed", entry.Squashed);
            writer.WriteString("session", entry.Session);
            if (entry.Quest is not null) writer.WriteString("quest", entry.Quest);
            if (entry.Title is not null) writer.WriteString("title", entry.Title);
            writer.WriteString("landedAt", Moments.Write(entry.LandedAt));
            if (entry.Plugin is not null) writer.WriteString("plugin", entry.Plugin);
            if (entry.Pushed) writer.WriteBoolean("pushed", true);
            if (entry.PullRequest is not null) writer.WriteString("pullRequest", entry.PullRequest);
            if (entry.PushedTip is not null) writer.WriteString("pushedTip", entry.PushedTip);
            if (entry.From is not null) writer.WriteString("from", entry.From);
            if (entry.GoneAt is { } gone) writer.WriteString("goneAt", Moments.Write(gone));
            if (entry.RemovedAs is not null) writer.WriteString("removedAs", entry.RemovedAs);
            if (entry.RemovedOn is not null) writer.WriteString("removedOn", entry.RemovedOn);
            if (entry.GoneAs is not null) writer.WriteString("goneAs", entry.GoneAs);
            if (entry.MovedTo is not null) writer.WriteString("movedTo", entry.MovedTo);
            if (entry.MovedToAt is not null) writer.WriteString("movedToAt", entry.MovedToAt);
            if (entry.KeptOn is not null) writer.WriteString("keptOn", entry.KeptOn);
            // LANDRETIRE1: only where a withdrawal said why, so a trace from before reads as it did.
            if (entry.GoneWhy is not null) writer.WriteString("goneWhy", entry.GoneWhy);
            if (entry.AcceptedBy is not null) writer.WriteString("acceptedBy", entry.AcceptedBy);
            if (entry.Rule is { } rule)
            {
                writer.WriteStartObject("rule");
                if (rule.Plugin is not null) writer.WriteString("plugin", rule.Plugin);
                writer.WriteBoolean("autoAccept", rule.AutoAccept);
                writer.WriteString("source", rule.Source);
                // AUTONOMY1c2: added beside the rest, only where the access setup turned the switch on; a record from before reads none.
                if (rule.Access is not null) writer.WriteString("access", rule.Access);
                writer.WriteEndObject();
            }

            if (entry.Integrate is { } integrate) WriteIntegrate(writer, integrate);
            if (entry.Review is { } review) WriteReview(writer, review);
            if (entry.Opinion is { } opinion) WriteOpinion(writer, opinion);
            if (entry.Workflow is { } workflow) WriteWorkflow(writer, workflow);

            if (entry.Advances.Count > 0)
            {
                writer.WriteStartArray("advances");
                foreach (var advance in entry.Advances)
                {
                    writer.WriteStartObject();
                    writer.WriteString("from", advance.From);
                    writer.WriteString("to", advance.To);
                    writer.WriteString("at", Moments.Write(advance.At));
                    writer.WriteString("session", advance.Session);
                    if (advance.AcceptedBy is not null) writer.WriteString("acceptedBy", advance.AcceptedBy);
                    if (advance.Integrate is { } stood) WriteIntegrate(writer, stood);
                    if (advance.Review is { } advanced) WriteReview(writer, advanced);
                    if (advance.Opinion is { } read) WriteOpinion(writer, read);
                    if (advance.Workflow is { } went) WriteWorkflow(writer, went);
                    if (advance.Squashed is not null) writer.WriteString("squashed", advance.Squashed);
                    // FABRIC-UNITLINK1, FABRIC-CLOSE1h13: each only where it is known, so an advance from before reads as it did.
                    if (advance.Quest is not null) writer.WriteString("quest", advance.Quest);
                    if (advance.Unit is not null) writer.WriteString("unit", advance.Unit);
                    if (advance.Submission is { } made) WriteSubmission(writer, made);
                    WriteBrought(writer, advance.Brought);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            if (entry.PullRequestState is { } state) WriteState(writer, state);
            if (entry.PullRequestAskFailed is { } failed)
            {
                writer.WriteStartObject("pullRequestAskFailed");
                writer.WriteString("code", failed.Code);
                writer.WriteString("plugin", failed.Plugin);
                writer.WriteString("at", Moments.Write(failed.At));
                writer.WriteEndObject();
            }

            if (entry.Carried.Count > 0)
            {
                writer.WriteStartArray("carried");
                foreach (var carried in entry.Carried)
                {
                    writer.WriteStartObject();
                    writer.WriteString("branch", carried.Branch);
                    writer.WriteString("tip", carried.Tip);
                    writer.WriteString("at", Moments.Write(carried.At));
                    writer.WriteString("by", carried.By);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            // FABRIC-END2: each only where it is known, so an entry from before reads as it did.
            if (entry.Held is { } held) HeldFact.Write(writer, held);
            if (entry.Units.Count > 0)
            {
                writer.WriteStartArray("units");
                foreach (var unit in entry.Units) writer.WriteStringValue(unit);
                writer.WriteEndArray();
            }

            // FABRIC-CLOSE1h11, FABRIC-CLOSE1h13: each only where it is known, so a landing from before reads as it did.
            if (entry.Served is not null) writer.WriteString("served", entry.Served);
            if (entry.Submission is { } submission) WriteSubmission(writer, submission);
            WriteBrought(writer, entry.Brought);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    });

    /// <summary>The submission a landing or an advance was made on (FABRIC-CLOSE1h13): its machine and sequence.</summary>
    internal static void WriteSubmission(Utf8JsonWriter writer, QuestSubmissionRef submission)
    {
        writer.WriteStartObject("submission");
        writer.WriteString("machine", submission.Machine);
        writer.WriteNumber("sequence", submission.Sequence);
        writer.WriteEndObject();
    }

    /// <summary>
    /// The submission a landing or an advance kept, or null: absent, which is every record from before FABRIC-CLOSE1h13 and every done's,
    /// or named only in half.
    /// </summary>
    internal static QuestSubmissionRef? SubmissionOf(JsonElement element) =>
        element.TryGetProperty("submission", out var submission) && submission.ValueKind == JsonValueKind.Object
        && Text(submission, "machine") is { Length: > 0 } machine
        && submission.TryGetProperty("sequence", out var sequence) && sequence.ValueKind == JsonValueKind.Number
        && sequence.TryGetInt64(out var number) && number > 0
            ? new QuestSubmissionRef(machine, number)
            : null;

    /// <summary>
    /// The acceptance's provenance (INTEGRATE1e, design §17.4): written only where <c>integrate</c> stood, so a landing or an advance without
    /// it, and every one from before, writes nothing and reads as it did.
    /// </summary>
    internal static void WriteIntegrate(Utf8JsonWriter writer, LandedIntegrate integrate)
    {
        writer.WriteStartObject("integrate");
        writer.WriteString("request", integrate.Request);
        writer.WriteString("source", integrate.Source);
        writer.WriteEndObject();
    }

    /// <summary>
    /// The provenance a landing or an advance kept, or null: a record that says neither, which is one from before INTEGRATE1e, one an older
    /// build rewrote, one made where no <c>integrate</c> stood, and one whose shape does not read whole. Its values are kept as written.
    /// </summary>
    internal static LandedIntegrate? IntegrateOf(JsonElement element) =>
        element.TryGetProperty("integrate", out var integrate) && integrate.ValueKind == JsonValueKind.Object
        && Text(integrate, "request") is { Length: > 0 } request && Text(integrate, "source") is { Length: > 0 } source
            ? new LandedIntegrate(request, source)
            : null;

    /// <summary>
    /// The review that let a landing go (REVIEWENV1c), each field only where it is known. Internal with its reader and the opinion's and
    /// workflow's, since an integration request keeps its gate proof in the record's own shape (INTEGRATE1e part e2).
    /// </summary>
    internal static void WriteReview(Utf8JsonWriter writer, LandingReview review)
    {
        writer.WriteStartObject("review");
        writer.WriteString("said", review.Said);
        if (review.Environment is not null) writer.WriteString("environment", review.Environment);
        if (review.Quest is not null) writer.WriteString("quest", review.Quest);
        if (review.Commit is not null) writer.WriteString("commit", review.Commit);
        if (review.At is { } at) writer.WriteString("at", Moments.Write(at));
        if (review.Words is not null) writer.WriteString("words", review.Words);
        writer.WriteEndObject();
    }

    /// <summary>The review a landing or an advance kept, or null: absent, which is every landing from before REVIEWENV1c, or not whole.</summary>
    internal static LandingReview? ReviewOf(JsonElement element) =>
        element.TryGetProperty("review", out var review) && review.ValueKind == JsonValueKind.Object && Text(review, "said") is { } said
            ? new LandingReview(said, Text(review, "environment"), Text(review, "quest"))
            {
                Commit = Text(review, "commit"),
                At = Moments.Read(Text(review, "at")),
                Words = Text(review, "words"),
            }
            : null;

    /// <summary>The second opinion that let a landing go (XAGENT1f, design §8.6), each field only where it is known.</summary>
    internal static void WriteOpinion(Utf8JsonWriter writer, LandingOpinion opinion)
    {
        writer.WriteStartObject("opinion");
        writer.WriteString("said", opinion.Said);
        if (opinion.Opinion is not null) writer.WriteString("opinion", opinion.Opinion);
        if (opinion.Reviewer is not null) writer.WriteString("reviewer", opinion.Reviewer);
        if (opinion.Label is not null) writer.WriteString("label", opinion.Label);
        if (opinion.Base is not null) writer.WriteString("base", opinion.Base);
        if (opinion.Tip is not null) writer.WriteString("tip", opinion.Tip);
        writer.WriteNumber("passes", opinion.Passes);
        Counts(writer, "weights", opinion.Weights);
        Counts(writer, "answers", opinion.Answers);
        writer.WriteNumber("disputes", opinion.Disputes);
        writer.WriteNumber("unread", opinion.Unread);
        if (opinion.Person is not null) writer.WriteString("person", opinion.Person);
        if (opinion.At is { } at) writer.WriteString("at", Moments.Write(at));
        if (opinion.Words is not null) writer.WriteString("words", opinion.Words);
        if (opinion.Code is not null) writer.WriteString("code", opinion.Code);
        writer.WriteEndObject();

        static void Counts(Utf8JsonWriter writer, string name, IReadOnlyDictionary<string, int> counts)
        {
            writer.WriteStartObject(name);
            foreach (var (key, count) in counts.OrderBy(pair => pair.Key, StringComparer.Ordinal)) writer.WriteNumber(key, count);
            writer.WriteEndObject();
        }
    }

    /// <summary>The second opinion a landing or an advance kept, or null: absent, which is every landing from before XAGENT1f.</summary>
    internal static LandingOpinion? OpinionOf(JsonElement element)
    {
        if (!element.TryGetProperty("opinion", out var opinion) || opinion.ValueKind != JsonValueKind.Object || Text(opinion, "said") is not { } said)
        {
            return null;
        }

        static int Number(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n) ? n : 0;
        static IReadOnlyDictionary<string, int> Counts(JsonElement element, string name)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            if (!element.TryGetProperty(name, out var kept) || kept.ValueKind != JsonValueKind.Object) return counts;
            foreach (var each in kept.EnumerateObject())
            {
                if (each.Value.ValueKind == JsonValueKind.Number && each.Value.TryGetInt32(out var count)) counts[each.Name] = count;
            }

            return counts;
        }

        return new LandingOpinion(said)
        {
            Opinion = Text(opinion, "opinion"),
            Reviewer = Text(opinion, "reviewer"),
            Label = Text(opinion, "label"),
            Base = Text(opinion, "base"),
            Tip = Text(opinion, "tip"),
            Passes = Number(opinion, "passes"),
            Weights = Counts(opinion, "weights"),
            Answers = Counts(opinion, "answers"),
            Disputes = Number(opinion, "disputes"),
            Unread = Number(opinion, "unread"),
            Person = Text(opinion, "person"),
            At = Moments.Read(Text(opinion, "at")),
            Words = Text(opinion, "words"),
            Code = Text(opinion, "code"),
        };
    }

    /// <summary>The named workflow a landing went by (WORKFLOW1f, design §5.5), each field only where it is known.</summary>
    internal static void WriteWorkflow(Utf8JsonWriter writer, LandingWorkflow workflow)
    {
        writer.WriteStartObject("workflow");
        writer.WriteString("workflow", workflow.Workflow);
        writer.WriteNumber("version", workflow.Version);
        writer.WriteString("level", workflow.Level);
        if (workflow.Kind is not null) writer.WriteString("kind", workflow.Kind);
        writer.WriteEndObject();
    }

    /// <summary>The named workflow a landing or an advance kept, or null: absent, which is every landing under Current and from before WORKFLOW1f.</summary>
    internal static LandingWorkflow? WorkflowOf(JsonElement element) =>
        element.TryGetProperty("workflow", out var workflow) && workflow.ValueKind == JsonValueKind.Object
        && Text(workflow, "workflow") is { } id && Text(workflow, "level") is { } level
        && workflow.TryGetProperty("version", out var version) && version.ValueKind == JsonValueKind.Number && version.TryGetInt32(out var number)
            ? new LandingWorkflow(id, number, level) { Kind = Text(workflow, "kind") }
            : null;

    /// <summary>The kept answer, in the answer's own field names, with who answered and when it was asked (PLUGHOOK1a).</summary>
    internal static void WriteState(Utf8JsonWriter writer, PullRequestState state)
    {
        writer.WriteStartObject("pullRequestState");
        writer.WriteString("state", state.State);
        if (state.PullRequest is not null) writer.WriteString("pullRequest", state.PullRequest);
        if (state.MergeCommit is not null) writer.WriteString("mergeCommit", state.MergeCommit);
        if (state.SourceCommit is not null) writer.WriteString("sourceCommit", state.SourceCommit);
        if (state.Target is not null) writer.WriteString("target", state.Target);
        if (state.How is not null) writer.WriteString("how", state.How);
        if (state.At is { } at) writer.WriteString("at", Moments.Write(at));
        if (state.Message is not null) writer.WriteString("message", state.Message);
        if (state.Plugin is not null) writer.WriteString("plugin", state.Plugin);
        writer.WriteString("askedAt", Moments.Write(state.AskedAt));
        writer.WriteEndObject();
    }

    /// <summary>
    /// The kept answer, read by the wire's own rules (<see cref="PullRequestAnswers.Read"/>), or null: one that would not have
    /// been an answer is none, since a removal chooses by it.
    /// </summary>
    internal static PullRequestState? StateOf(JsonElement element)
    {
        if (!element.TryGetProperty("pullRequestState", out var kept) || kept.ValueKind != JsonValueKind.Object) return null;
        var plugin = Text(kept, "plugin");
        return PullRequestAnswers.Read(kept, plugin ?? "") is { } state
            ? state with
            {
                Plugin = plugin,
                AskedAt = Moments.Read(Text(kept, "askedAt")) ?? DateTimeOffset.MinValue,
            }
            : null;
    }

    /// <summary>The latest failed ask, whole or none.</summary>
    internal static PullRequestAskFailed? AskFailedOf(JsonElement element) =>
        element.TryGetProperty("pullRequestAskFailed", out var failed) && failed.ValueKind == JsonValueKind.Object
        && Text(failed, "code") is { } code && Text(failed, "plugin") is { } plugin
        && Moments.Read(Text(failed, "at")) is { } at
            ? new PullRequestAskFailed(code, plugin, at)
            : null;

    /// <summary>The session branches removed on its answer, each whole or left out. Absent is none.</summary>
    internal static IReadOnlyList<CarriedBranch> CarriedOf(JsonElement element)
    {
        if (!element.TryGetProperty("carried", out var list) || list.ValueKind != JsonValueKind.Array) return [];
        var carried = new List<CarriedBranch>();
        foreach (var each in list.EnumerateArray())
        {
            if (each.ValueKind != JsonValueKind.Object || Text(each, "branch") is not { } branch || Text(each, "tip") is not { } tip
                || Text(each, "by") is not { } by
                || Moments.Read(Text(each, "at")) is not { } at)
            {
                continue;
            }

            carried.Add(new CarriedBranch(branch, tip, at, by));
        }

        // The empty case is the property's own default, so an entry from before PLUGHOOK1a still equals itself read twice.
        if (carried.Count == 0) return [];
        return carried;
    }

    /// <summary>One entry, or null where it lacks what makes it one: a repository, a branch, the commit it was made at, a session.</summary>
    internal static LandedBranch? Read(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        var repository = Text(element, "repository");
        var branch = Text(element, "branch");
        var tip = Text(element, "tip");
        var session = Text(element, "session");
        if (repository is null || branch is null || tip is null || session is null) return null;
        var at = Moments.Read(Text(element, "landedAt")) ?? DateTimeOffset.MinValue;
        return new LandedBranch(repository, Text(element, "workspace") ?? "default", branch, Text(element, "line"), tip, session,
            Text(element, "quest"), Text(element, "title"), at)
        {
            Plugin = Text(element, "plugin"),
            Pushed = element.TryGetProperty("pushed", out var pushed) && pushed.ValueKind == JsonValueKind.True,
            PullRequest = Text(element, "pullRequest"),
            PushedTip = Text(element, "pushedTip"),
            From = Text(element, "from"),
            GoneAt = Moments.Read(Text(element, "goneAt")),
            RemovedAs = Text(element, "removedAs"),
            RemovedOn = Text(element, "removedOn"),
            GoneAs = Text(element, "goneAs"),
            MovedTo = Text(element, "movedTo"),
            MovedToAt = Text(element, "movedToAt"),
            KeptOn = Text(element, "keptOn"),
            GoneWhy = Text(element, "goneWhy"),
            AcceptedBy = Text(element, "acceptedBy"),
            Rule = element.TryGetProperty("rule", out var rule) && rule.ValueKind == JsonValueKind.Object
                ? new LandedRule(
                    Text(rule, "plugin"),
                    rule.TryGetProperty("autoAccept", out var auto) && auto.ValueKind == JsonValueKind.True,
                    Text(rule, "source") ?? LandingSource.Default)
                { Access = Text(rule, "access") }
                : null,
            Integrate = IntegrateOf(element),
            Review = ReviewOf(element),
            Opinion = OpinionOf(element),
            Workflow = WorkflowOf(element),
            Advances = AdvancesOf(element),
            PullRequestState = StateOf(element),
            PullRequestAskFailed = AskFailedOf(element),
            Carried = CarriedOf(element),
            Squashed = Text(element, "squashed"),
            Held = HeldFact.Read(element),
            Units = UnitsOf(element),
            Served = Text(element, "served"),
            Submission = SubmissionOf(element),
            Brought = BroughtOf(element),
        };
    }

    /// <summary>The sessions a combined Accept brought into a landing or an advance (LINESTOGETHER1d); none in a record from before.</summary>
    internal static IReadOnlyList<LandedWork> BroughtOf(JsonElement element)
    {
        if (!element.TryGetProperty("brought", out var brought) || brought.ValueKind != JsonValueKind.Array) return [];
        return [.. brought.EnumerateArray()
            .Where(each => each.ValueKind == JsonValueKind.Object && Text(each, "session") is { Length: > 0 } && Text(each, "tip") is { Length: > 0 })
            .Select(each => new LandedWork(Text(each, "session")!, Text(each, "tip")!)
            {
                Quest = Text(each, "quest"), Unit = Text(each, "unit"), Submission = SubmissionOf(each),
            })];
    }

    internal static void WriteBrought(Utf8JsonWriter writer, IReadOnlyList<LandedWork> brought)
    {
        if (brought.Count == 0) return;
        writer.WriteStartArray("brought");
        foreach (var work in brought)
        {
            writer.WriteStartObject();
            writer.WriteString("session", work.Session);
            writer.WriteString("tip", work.Tip);
            if (work.Quest is not null) writer.WriteString("quest", work.Quest);
            if (work.Unit is not null) writer.WriteString("unit", work.Unit);
            if (work.Submission is { } made) WriteSubmission(writer, made);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    /// <summary>The units an entry carries (FABRIC-END2), each a non-empty string. Absent is none, which is every landing from before.</summary>
    internal static IReadOnlyList<string> UnitsOf(JsonElement element)
    {
        if (!element.TryGetProperty("units", out var list) || list.ValueKind != JsonValueKind.Array) return [];
        var units = list.EnumerateArray()
            .Where(each => each.ValueKind == JsonValueKind.String && each.GetString() is { Length: > 0 })
            .Select(each => each.GetString()!)
            .ToList();
        // The empty case is the property's own default, so an entry from before still equals itself read twice.
        if (units.Count == 0) return [];
        return units;
    }

    /// <summary>
    /// The advances an entry keeps (LAND2c), each whole or left out: one without the commits it moved between, when, or the
    /// session is no fact at all. Absent is none, which is every landing from before LAND2c.
    /// </summary>
    internal static IReadOnlyList<LandedAdvance> AdvancesOf(JsonElement element)
    {
        if (!element.TryGetProperty("advances", out var list) || list.ValueKind != JsonValueKind.Array) return [];
        var advances = new List<LandedAdvance>();
        foreach (var each in list.EnumerateArray())
        {
            if (each.ValueKind != JsonValueKind.Object || Text(each, "from") is not { } from || Text(each, "to") is not { } to
                || Text(each, "session") is not { } session
                || Moments.Read(Text(each, "at")) is not { } at)
            {
                continue;
            }

            advances.Add(new LandedAdvance(from, to, at, session)
            {
                AcceptedBy = Text(each, "acceptedBy"), Integrate = IntegrateOf(each), Review = ReviewOf(each), Opinion = OpinionOf(each), Workflow = WorkflowOf(each),
                Squashed = Text(each, "squashed"), Quest = Text(each, "quest"), Unit = Text(each, "unit"), Submission = SubmissionOf(each),
                Brought = BroughtOf(each),
            });
        }

        // The empty case is the property's own default, so an entry from before LAND2c still equals itself read twice.
        if (advances.Count == 0) return [];
        return advances;
    }
}

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The owner's change of the policy through Daoris's assistant. The assistant's session proposes edits of the policy's
/// rows (<see cref="Propose"/>): a proposal with an id and a digest, its before and after and the waits it opens or releases, which changes nothing.
/// It is applied (<see cref="Apply"/>) only where a turn of the owner's own in that same conversation, noted by <see cref="OwnerTurns"/>
/// from the person's door alone, comes after the proposal and names both its id and its digest, and the policy has not moved since. An agent's
/// words, a quote an agent relays, another conversation's turn, a process that proves no session, a commit or a file never apply it. Each applied
/// change is appended to the home's change journal, naming the turn.
/// </summary>
public sealed class PolicyChanges(string home, Func<DateTimeOffset> clock, OwnerTurns turns)
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);
    private static readonly object Gate = new();

    private const string AgentSentence = "the policy is the owner's: a working session's press neither proposes nor applies a change to it, so nothing was changed";

    private string ProposalsPath => Path.Combine(DaorisHome.Entry(home, HomeEntry.Workflows, RiskJournal.Folder), "proposals.json");

    /// <summary>The proposals this home holds, pending and applied, oldest first.</summary>
    public IReadOnlyList<PolicyProposal> Proposals()
    {
        lock (Gate) return Read();
    }

    /// <summary>The assistant's proposal of <paramref name="edits"/>: kept pending, with the waits it would open or release, and nothing changed.</summary>
    public PolicyOutcome Propose(PolicyChannel channel, IReadOnlyList<PolicyEdit> edits)
    {
        if (channel.Worker) return Refuse(PolicyRefusals.Agent, AgentSentence);
        if (channel.Session is not { } session) return Refuse(PolicyRefusals.Keyless, "a proposal comes from a conversation whose session key the press proves; this process proves none, so nothing was proposed");
        if (edits.Count == 0) return Refuse(PolicyRefusals.Shape, "a proposal names at least one edit");
        foreach (var edit in edits)
        {
            if (edit.Act.StartsWith(RiskActs.PolicyChange, StringComparison.Ordinal)
                && (edit.Act.Length == RiskActs.PolicyChange.Length || edit.Act[RiskActs.PolicyChange.Length] == '/'))
            {
                return Refuse(PolicyRefusals.Fixed, PolicyRefusals.FixedSentence);
            }

            if (edit.Fault() is { } fault) return Refuse(PolicyRefusals.Shape, fault);
        }

        lock (Gate)
        {
            var config = DriverConfig.Load(ConfigPath);
            var before = RiskPolicy.Of(config);
            if (before.Fault is { } unread) return Refuse(PolicyRefusals.Shape, $"the risk policy does not read ({unread}): it is mended first, so nothing was proposed");
            var after = After(config, edits);
            var at = clock();
            var proposal = new PolicyProposal(
                $"pc-{Guid.NewGuid():N}"[..11], PolicyProposal.DigestOf(session, before.Revision, edits), session, at, before.Revision, edits, Shifts(config, before, after, edits));
            var all = Read();
            all.Add(proposal);
            Write(all);
            return new(true, null, proposal.Said(), proposal);
        }
    }

    /// <summary>The proposal applied: the rows written under the file's lock, journaled naming the owner's turn that confirmed it.</summary>
    public PolicyOutcome Apply(PolicyChannel channel, string proposalId, string digest)
    {
        if (channel.Worker) return Refuse(PolicyRefusals.Agent, AgentSentence);
        if (channel.Session is null && !channel.Pressed) return Refuse(PolicyRefusals.Keyless, "a change to the policy is applied from the conversation its owner's turn is in, by a press that proves its session; this process proves none, so nothing was changed");
        lock (Gate)
        {
            var all = Read();
            var index = all.FindIndex(each => string.Equals(each.Id, proposalId, StringComparison.Ordinal));
            if (index < 0) return Refuse(PolicyRefusals.Unknown, $"no proposal `{proposalId}` is held here, so nothing was changed");
            var proposal = all[index];
            if (proposal.State == PolicyProposal.Applied) return Refuse(PolicyRefusals.Taken, $"`{proposal.Id}` was applied already, so nothing was changed again");
            if (!string.Equals(proposal.Digest, digest, StringComparison.OrdinalIgnoreCase)) return Refuse(PolicyRefusals.Digest, $"`{digest}` is not the digest of `{proposal.Id}`, so nothing was changed");
            var turn = channel.Pressed ? new OwnerTurn(PolicyChangeLine.Pressed, proposal.Session, clock(), "") : Confirmed(channel.Session!, proposal);
            if (turn is null)
            {
                return Refuse(
                    string.Equals(proposal.Session, channel.Session, StringComparison.OrdinalIgnoreCase) ? PolicyRefusals.NoOwnerTurn : PolicyRefusals.Conversation,
                    string.Equals(proposal.Session, channel.Session, StringComparison.OrdinalIgnoreCase)
                        ? $"no turn of the owner's own in this conversation, after the proposal, names `{proposal.Id}` and `{proposal.Digest}`: an agent's words, a quote it relays, a commit or a file never apply a change to the policy. Nothing was changed."
                        : $"`{proposal.Id}` is the owner's to confirm in the conversation that proposed it; a press from another session changes nothing");
            }

            var config = DriverConfig.Load(ConfigPath);
            var before = RiskPolicy.Of(config);
            if (before.Revision != proposal.BaseRevision)
            {
                return Refuse(PolicyRefusals.Stale, $"the policy moved from revision {proposal.BaseRevision} to {before.Revision} since `{proposal.Id}` was proposed; it is proposed again on the policy as it stands. Nothing was changed.");
            }

            DriverConfigWrites.Change(ConfigPath, text =>
            {
                // The lock is held: the revision is read again from the file as it stands, so a writer that came between is not written over.
                var current = text ?? "{}";
                if (RiskPolicy.Read(JsonDocument.Parse(current).RootElement).Revision != proposal.BaseRevision)
                {
                    throw new DriverException($"policy: the policy moved since `{proposal.Id}` was proposed, so nothing was changed ({PolicyRefusals.Stale})");
                }

                var root = JsonNode.Parse(current) as JsonObject ?? throw new DriverException($"policy: {ConfigPath} is not an object, so nothing was changed");
                PolicyRows.Apply(root, proposal.Edits);
                return JsonFile.Serialize(root);
            });
            // Read back as written: the revision is of the file's own text, which the writer lays out.
            var revisionAfter = RiskPolicy.Of(DriverConfig.Load(ConfigPath)).Revision;
            PolicyChangeJournal.Append(home, new PolicyChangeLine(
                clock(), channel.Pressed ? PolicyChangeLine.Pressed : PolicyChangeLine.Assistant, proposal.Id, proposal.Digest, proposal.Session, turn.Id, turn.At, turn.Text, proposal.Edits, proposal.Shifts,
                before.Revision, revisionAfter));
            all[index] = proposal with { State = PolicyProposal.Applied };
            Write(all);
            return new(true, null, $"daoris: `{proposal.Id}` applied on the owner's turn {turn.Id}; the policy is at revision {revisionAfter}.", proposal);
        }
    }

    private OwnerTurn? Confirmed(string session, PolicyProposal proposal) =>
        string.Equals(proposal.Session, session, StringComparison.OrdinalIgnoreCase) ? turns.Binding(proposal.Session, proposal.At, proposal.Id, proposal.Digest) : null;

    private string ConfigPath => DaorisHome.Entry(home, HomeEntry.DriverConfig);

    private static PolicyOutcome Refuse(string code, string said) => new(false, code, said);

    private static RiskPolicy After(DriverConfig config, IReadOnlyList<PolicyEdit> edits)
    {
        var root = JsonNode.Parse(config.Source ?? config.ToJson()) as JsonObject ?? new JsonObject();
        PolicyRows.Apply(root, edits);
        return RiskPolicy.Read(JsonDocument.Parse(root.ToJsonString()).RootElement);
    }

    /// <summary>
    /// The waits each edit would open or release or move to another answerer, at its place, under each access mode: the act judged before and after.
    /// A place's workspace is not known from a repository's key, so a repository place is read at its own access.
    /// </summary>
    private static IReadOnlyList<PolicyShift> Shifts(DriverConfig config, RiskPolicy before, RiskPolicy after, IReadOnlyList<PolicyEdit> edits)
    {
        var shifts = new List<PolicyShift>();
        foreach (var edit in edits)
        {
            var place = PlaceOf(config, edit.Place);
            var act = RiskAct.Of(edit.Act);
            foreach (var full in new[] { true, false })
            {
                var mode = full ? RiskPlace.FullMode : RiskPlace.RestrictedMode;
                if (edit.Mode is { } only && only != mode) continue;
                var at = place with { Full = full };
                var was = RiskGate.Judge(before, act, "", at, "");
                var now = RiskGate.Judge(after, act, "", at, "");
                if (was.Class == now.Class && was.By == now.By) continue;
                var effect = Strictness(now.Class) > Strictness(was.Class) ? PolicyShift.Opens : Strictness(now.Class) < Strictness(was.Class) ? PolicyShift.Releases : PolicyShift.Moves;
                shifts.Add(new(edit.Place, edit.Act, mode, was.Class, now.Class, effect));
            }
        }

        return [.. shifts.DistinctBy(shift => (shift.Place, shift.Act, shift.Mode))];
    }

    private static int Strictness(string riskClass) => riskClass switch { RiskClasses.None => 0, RiskClasses.Role => 1, _ => 2 };

    private static RiskPlace PlaceOf(DriverConfig config, string key) =>
        key.StartsWith("repository/", StringComparison.Ordinal) ? RiskPlace.Of(config, null, key["repository/".Length..])
        : key.StartsWith("workspace/", StringComparison.Ordinal) ? RiskPlace.Of(config, key["workspace/".Length..], null)
        : RiskPlace.Of(config, null, null);

    private List<PolicyProposal> Read()
    {
        if (!File.Exists(ProposalsPath)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<PolicyProposal>>(AtomicFile.ReadText(ProposalsPath), Wire) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private void Write(List<PolicyProposal> all)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ProposalsPath)!);
        AtomicFile.WriteText(ProposalsPath, JsonSerializer.Serialize(all, Wire));
    }
}

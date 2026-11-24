using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The owner's change of the policy through Daoris's assistant. The assistant's session proposes edits of the policy's
/// rows, a place's cast of a role or its access mode (<see cref="Propose"/>): a proposal with an id and a digest, its before and after and the waits it opens or releases, which changes nothing.
/// It is applied (<see cref="Apply"/>) only where a turn of the owner's own in that same conversation, noted by <see cref="OwnerTurns"/>
/// from the person's door alone, comes after the proposal in the form its card says, naming both its id and its digest, as the owner's latest word on it, and the policy has not moved since. An agent's
/// words, a quote an agent relays, another conversation's turn, a process that proves no session, a commit or a file never apply it. Each applied
/// change is appended to the home's change journal, naming the turn.
/// </summary>
public sealed class PolicyChanges(string home, Func<DateTimeOffset> clock, OwnerTurns turns, PlaceCastFiles? casts = null)
{
    private PlaceCastFiles Casts => casts ?? new PlaceCastFiles(home);

    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);
    private static readonly object Gate = new();

    private const string AgentSentence = "the policy is the owner's: a working session's press neither proposes nor applies a change to it, so nothing was changed";

    private string ProposalsPath => Path.Combine(DaorisHome.Entry(home, HomeEntry.Workflows, RiskJournal.Folder), "proposals.json");

    /// <summary>The proposals this home holds, pending and applied, oldest first.</summary>
    public IReadOnlyList<PolicyProposal> Proposals()
    {
        lock (Gate) return Read();
    }

    /// <summary>Whether <paramref name="text"/> names a proposal still waiting, by its id or its digest: words only the owner's own door may carry.</summary>
    public bool Mentions(string text)
    {
        lock (Gate)
        {
            return Read().Any(each => each.State == PolicyProposal.Pending
                && (text.Contains(each.Id, StringComparison.OrdinalIgnoreCase) || text.Contains(each.Digest, StringComparison.OrdinalIgnoreCase)));
        }
    }

    /// <summary>The assistant's proposal of <paramref name="edits"/>: kept pending, with the waits it would open or release, and nothing changed.</summary>
    public PolicyOutcome Propose(PolicyChannel channel, IReadOnlyList<PolicyEdit> edits, Func<string, string?>? workspaceOf = null)
    {
        if (channel.Worker) return Refuse(PolicyRefusals.Agent, AgentSentence);
        if (channel.Session is not { } session) return Refuse(PolicyRefusals.Keyless, "a proposal comes from a conversation whose session key the press proves; this process proves none, so nothing was proposed");
        if (edits.Count == 0) return Refuse(PolicyRefusals.Shape, "a proposal names at least one edit");
        foreach (var edit in edits)
        {
            if (edit.Subject == PolicyEdit.RowSubject && edit.Act.StartsWith(RiskActs.PolicyChange, StringComparison.Ordinal)
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
                $"pc-{Guid.NewGuid():N}"[..11], PolicyProposal.DigestOf(session, before.Revision, edits), session, at, before.Revision, edits, Shifts(config, AfterConfig(config, edits), before, after, edits, workspaceOf));
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

            if (Make(proposal.Edits, proposal.BaseRevision, proposal.Id, $"the owner's change `{proposal.Id}`, confirmed on turn {turn.Id}") is { } failed) return failed;

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

    /// <summary>The owner's own press (window or terminal, named by <paramref name="door"/>) writing at once on the revision it read; an agent's set is a proposal.</summary>
    public PolicyOutcome Set(PolicyChannel channel, string door, IReadOnlyList<PolicyEdit> edits, string? revision, Func<string, string?>? workspaceOf = null)
    {
        if (channel.Worker) return Refuse(PolicyRefusals.Agent, AgentSentence);
        if (!channel.Pressed) return Refuse(PolicyRefusals.Keyless, "a change to the policy is the owner's own press in the window or their terminal; this press is neither, so nothing was changed (an agent proposes it)");
        if (edits.Count == 0) return Refuse(PolicyRefusals.Shape, "a set names at least one edit");
        foreach (var edit in edits)
        {
            if (edit.Subject == PolicyEdit.RowSubject && edit.Act.StartsWith(RiskActs.PolicyChange, StringComparison.Ordinal)
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
            if (before.Fault is { } unread) return Refuse(PolicyRefusals.Shape, $"the risk policy does not read ({unread}): it is mended first, so nothing was changed");
            if (revision is not null && before.Revision != revision)
            {
                return Refuse(PolicyRefusals.Stale, $"the policy moved from revision {revision} to {before.Revision} since it was read; read it again. Nothing was changed.");
            }

            var shifts = Shifts(config, AfterConfig(config, edits), before, After(config, edits), edits, workspaceOf);
            var id = $"set-{Guid.NewGuid():N}"[..12];
            if (Make(edits, before.Revision, id, $"the owner's set `{id}` through the {door}") is { } failed) return failed;
            var revisionAfter = RiskPolicy.Of(DriverConfig.Load(ConfigPath)).Revision;
            PolicyChangeJournal.Append(home, new PolicyChangeLine(
                clock(), door, id, PolicyProposal.DigestOf(door, before.Revision, edits), "", door, clock(), "", edits, shifts, before.Revision, revisionAfter));
            return new(true, null, $"daoris: the policy is at revision {revisionAfter}: {string.Join("; ", edits.Select(edit => edit.Said()))}.");
        }
    }

    /// <summary>The owner's own terminal setting one edit as <c>daoris settings risk set</c> sends it: the answer as the route says it.</summary>
    public static JsonElement SetByOwnerTerminal(string home, DateTimeOffset now, JsonObject wire, string revision)
    {
        string? Text(string name) => wire[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
        var edit = new PolicyEdit(Text("place") ?? "", Text("act") ?? "", Text("class"), Text("by"), Text("mode")) { Subject = Text("subject") ?? PolicyEdit.RowSubject };
        var outcome = new PolicyChanges(home, () => now, new OwnerTurns(_ => [])).Set(PolicyChannel.OwnerPress(null), PolicyChangeLine.Cli, [edit], revision);
        return JsonSerializer.SerializeToElement(new { done = outcome.Done, code = outcome.Code, message = outcome.Said });
    }

    /// <summary>The edits written under the caller's gate, each through the door that validates it; null where they were made.</summary>
    private PolicyOutcome? Make(IReadOnlyList<PolicyEdit> edits, string baseRevision, string id, string why)
    {
        try
        {
            if (edits.Any(each => each.Subject == PolicyEdit.RowSubject))
            {
                DriverConfigWrites.Change(ConfigPath, text =>
                {
                    // Read again under the lock, so a writer that came between is not written over.
                    var current = text ?? "{}";
                    if (RiskPolicy.Read(JsonDocument.Parse(current).RootElement).Revision != baseRevision)
                    {
                        throw new DriverException($"policy: the policy moved since `{id}` was read, so nothing was changed ({PolicyRefusals.Stale})");
                    }

                    var root = JsonNode.Parse(current) as JsonObject ?? throw new DriverException($"policy: {ConfigPath} is not an object, so nothing was changed");
                    PolicyRows.Apply(root, edits);
                    return JsonFile.Serialize(root);
                });
            }

            foreach (var edit in edits.Where(each => each.Subject == PolicyEdit.AccessSubject))
            {
                StandingChoices.Write(ConfigPath, standing => AccessRules.Apply(standing, edit.AccessEdit()));
            }

            foreach (var edit in edits.Where(each => each.Subject == PolicyEdit.CastSubject))
            {
                if (AgentSettles.CastAt(Casts, edit.Place, edit.Act, edit.By, why, clock()) is { } refused)
                {
                    return Refuse(PolicyRefusals.Shape, $"the cast of `{edit.Act}` was refused: {refused}; the edits before it were made");
                }
            }
        }
        catch (DriverException refused)
        {
            return Refuse(refused.Message.Contains(PolicyRefusals.Stale, StringComparison.Ordinal) ? PolicyRefusals.Stale : PolicyRefusals.Shape, refused.Message);
        }

        return null;
    }

    private OwnerTurn? Confirmed(string session, PolicyProposal proposal) =>
        string.Equals(proposal.Session, session, StringComparison.OrdinalIgnoreCase) ? turns.Binding(proposal.Session, proposal.At, proposal.Id, proposal.Digest) : null;

    private string ConfigPath => DaorisHome.Entry(home, HomeEntry.DriverConfig);

    private static PolicyOutcome Refuse(string code, string said) => new(false, code, said);

    /// <summary>The config with the edits' access modes made, as the access rules make them.</summary>
    internal static DriverConfig AfterConfig(DriverConfig config, IReadOnlyList<PolicyEdit> edits) =>
        edits.Where(each => each.Subject == PolicyEdit.AccessSubject).Aggregate(config, (standing, edit) => AccessRules.Apply(standing, edit.AccessEdit()));

    internal static RiskPolicy After(DriverConfig config, IReadOnlyList<PolicyEdit> edits)
    {
        var root = JsonNode.Parse(config.Source ?? config.ToJson()) as JsonObject ?? new JsonObject();
        PolicyRows.Apply(root, edits);
        return RiskPolicy.Read(JsonDocument.Parse(root.ToJsonString()).RootElement);
    }

    /// <summary>
    /// The waits each edit would open or release or move to another answerer, at its place, under each access mode: the act judged before and after.
    /// A repository place is read with the workspace the registry names it in, so the rows it inherits from the workspace are judged.
    /// </summary>
    internal static IReadOnlyList<PolicyShift> Shifts(
        DriverConfig config, DriverConfig changed, RiskPolicy before, RiskPolicy after, IReadOnlyList<PolicyEdit> edits, Func<string, string?>? workspaceOf)
    {
        var shifts = new List<PolicyShift>();
        foreach (var edit in edits.Where(each => each.Subject != PolicyEdit.CastSubject))
        {
            if (edit.Subject == PolicyEdit.AccessSubject)
            {
                // The mode moves what the seed and the rows keyed by it say: every kind judged at the place as it stands and as it would.
                var was = PlaceOf(config, edit.Place, workspaceOf);
                var now = PlaceOf(changed, edit.Place, workspaceOf);
                foreach (var kind in RiskActs.All.Where(kind => kind != RiskActs.PolicyChange))
                {
                    var from = RiskGate.Judge(before, new RiskAct(kind, null), "", was, "");
                    var to = RiskGate.Judge(after, new RiskAct(kind, null), "", now, "");
                    if (from.Class == to.Class && from.By == to.By) continue;
                    shifts.Add(new(edit.Place, kind, now.Mode, from.Class, to.Class,
                        Strictness(to.Class) > Strictness(from.Class) ? PolicyShift.Opens : Strictness(to.Class) < Strictness(from.Class) ? PolicyShift.Releases : PolicyShift.Moves));
                }

                continue;
            }

            var place = PlaceOf(config, edit.Place, workspaceOf);
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

    private static RiskPlace PlaceOf(DriverConfig config, string key, Func<string, string?>? workspaceOf) =>
        key.StartsWith("repository/", StringComparison.Ordinal) ? RiskPlace.Of(config, workspaceOf?.Invoke(key["repository/".Length..]), key["repository/".Length..])
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

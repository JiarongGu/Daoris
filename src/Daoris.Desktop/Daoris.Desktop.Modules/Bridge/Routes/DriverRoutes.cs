using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// The driver's own state and standing choices, the page's `bridge/driver.ts`. Each standing choice is an edit to `driver.json`
/// and a nudge, the file the terminal's `daoris driver` edits, and declares the driver's state; a nudge declares nothing.
/// </summary>
public sealed class DriverRoutes(DriverContext context)
{
    private readonly DriverContext _context = context;
    private readonly DriverLoop _loop = context.Loop;

    [DriverRoute("STATE", PipeClass.Open)]
    private object? GetState(NoPayload _) => State(_loop);

    /// <summary>What is under way, numbered as the last <c>UNDER_WAY</c> telling, for a window opened mid-press.</summary>
    [DriverRoute("UNDER_WAY", PipeClass.Open)]
    private object? GetUnderWay(NoPayload _) => _loop.UnderWay.Now();

    /// <summary>
    /// A slice's snapshot, read from memory as last judged, never judged for the asking, so a second window judges nothing.
    /// </summary>
    [DriverRoute("STATE_SLICE", PipeClass.Open)]
    private object? GetStateSlice(StateSliceRead press) =>
        _loop.Slices.Find(press.Slice)?.Snapshot();

    [DriverRoute("SET_DRIVABLE", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SET_DRIVABLE", Repository = "repository")]
    [GroundRepository("SET_DRIVABLE", "repository")]
    [GroundListed("SET_DRIVABLE", "drivable", "drivable")]
    [Says("SET_DRIVABLE", "repository:repository")]
    [Publishes("SET_DRIVABLE", StateScopes.Driver)]
    private object? SetDrivable(SetDrivable press)
    {
        _context.Change(config => config.WithDrivable(press.Repository, press.Drivable));
        return State(_loop);
    }

    [DriverRoute("SET_HOLD", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SET_HOLD", Repository = "repository")]
    [GroundRepository("SET_HOLD", "repository")]
    [GroundListed("SET_HOLD", "holds", "held")]
    [Says("SET_HOLD", "repository:repository")]
    [Publishes("SET_HOLD", StateScopes.Driver)]
    private object? SetHold(SetHold press)
    {
        // A hold changed by anything but the standing confirmation is no longer one a covered resume may release.
        if (!PipeDoor.Standing) _loop.Pipe?.StandingConfirmation.Forget(press.Repository);
        _context.Change(config => config.WithHold(press.Repository, press.Held));
        return State(_loop);
    }

    [DriverRoute("SET_TREES", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SET_TREES", Repository = "repository")]
    [GroundRepository("SET_TREES", "repository")]
    [GroundListed("SET_TREES", "trees", "ownTree")]
    [Says("SET_TREES", "repository:repository")]
    [Publishes("SET_TREES", StateScopes.Driver)]
    private object? SetTrees(SetTrees press)
    {
        _context.Change(config => config.WithTrees(press.Repository, press.OwnTree));
        return State(_loop);
    }

    // *Daoris may change this checkout's files*, off by default: it writes nothing to a repository, it lets the Changes tab's acts be offered.
    [DriverRoute("SET_CHECKOUT_WRITES", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SET_CHECKOUT_WRITES", Repository = "repository")]
    [GroundRepository("SET_CHECKOUT_WRITES", "repository")]
    [GroundListed("SET_CHECKOUT_WRITES", "checkoutWrites", "allow")]
    [Says("SET_CHECKOUT_WRITES", "repository:repository")]
    [Publishes("SET_CHECKOUT_WRITES", StateScopes.Driver)]
    private object? SetCheckoutWrites(SetCheckoutWrites press)
    {
        _context.Change(config => config.WithCheckoutWrites(press.Repository, press.Allow));
        return State(_loop);
    }

    // A repository's standing answer, the person's words handed to every session there; no words clears it.
    [DriverRoute("SET_STANDING", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SET_STANDING", Repository = "repository")]
    [GroundRepository("SET_STANDING", "repository")]
    [GroundRead("SET_STANDING", nameof(PipeReads.StandingAnswer))]
    [Says("SET_STANDING", "repository:repository")]
    [Publishes("SET_STANDING", StateScopes.Driver)]
    private object? SetStanding(SetStanding press)
    {
        _context.Change(config => config.WithStanding(press.Repository, press.Says, DateTimeOffset.UtcNow));
        return State(_loop);
    }

    /// <summary>
    /// This machine's heavy gates for a repository whose own <c>daoris.gates.json</c> marks none: the whole list in run order, judged
    /// by <see cref="HeavyMapping.Apply"/> before anything is written. One gate at a time is not offered, since the order is the point.
    /// </summary>
    [DriverRoute("SET_HEAVY_GATES", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SET_HEAVY_GATES", Repository = "repository")]
    [GroundRepository("SET_HEAVY_GATES", "repository")]
    [GroundRead("SET_HEAVY_GATES", nameof(PipeReads.HeavyGates))]
    [Says("SET_HEAVY_GATES", "repository:repository")]
    [Publishes("SET_HEAVY_GATES", StateScopes.Driver)]
    private object? SetHeavyGates(SetHeavyGatesPress press)
    {
        var repository = press.Repository ?? "";
        var gates = HeavyGatesOf(press.Gates);
        // The refusals come first, before anything is written.
        HeavyMapping.Apply(DriverConfig.Load(_loop.ConfigPath), repository, gates);
        _context.Change(config => config.WithHeavyGates(repository, gates));
        return State(_loop);
    }

    /// <summary>
    /// Which checks a repository's sessions run, <c>light</c> or <c>heavy-at-end</c>, judged by <see cref="SessionChecks.Apply"/> before
    /// anything is written. The person's alone: light lowers their part, so no proposal names it.
    /// </summary>
    [DriverRoute("SET_SESSION_CHECKS", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SET_SESSION_CHECKS", Repository = "repository")]
    [GroundRepository("SET_SESSION_CHECKS", "repository")]
    [GroundRead("SET_SESSION_CHECKS", nameof(PipeReads.SessionChecks))]
    [Says("SET_SESSION_CHECKS", "repository:repository")]
    [Publishes("SET_SESSION_CHECKS", StateScopes.Driver)]
    private object? SetSessionChecks(SetSessionChecks press)
    {
        var repository = press.Repository ?? "";
        // The refusals come first, before anything is written.
        SessionChecks.Apply(DriverConfig.Load(_loop.ConfigPath), repository, press.Checks);
        _context.Change(config => config.WithSessionChecks(repository, press.Checks));
        return State(_loop);
    }

    /// <summary>
    /// Whether a repository's own <c>daoris.gates.json</c> wins over this machine's heavy gates: a read of one file in its checkout,
    /// with no git, null with <c>refusal</c> where it cannot be told.
    /// </summary>
    [DriverRoute("HEAVY_MARKS", PipeClass.Open)]
    private async Task<object?> HeavyMarksAsync(HeavyRead press, CancellationToken cancellationToken)
    {
        var repository = (press.Repository ?? "").Trim();
        var service = _loop.Service ?? throw NotReady();
        var root = LinesRoutes.Checkouts(await service.RegistryAsync(cancellationToken), repository, null)
            .Select(row => row.Root).FirstOrDefault(path => Directory.Exists(path));
        if (root is null)
        {
            return new
            {
                repository, marks = (bool?)null, names = Array.Empty<string>(), light = Array.Empty<string>(),
                refusal = $"`{repository}` has no checkout on this machine to read.",
            };
        }

        // FABRIC-PIPELINE1b: and its light gates, those it declares and does not mark heavy, which Setup's sessions' checks name.
        var marks = HeavyGates.Marks(root);
        return new { repository, marks = marks.Marks, names = marks.Names, light = marks.Light, refusal = marks.Refusal };
    }

    /// <summary>
    /// Who holds a repository's heavy-run slot, at which gate, and who waits, read from the records the runs keep beside the slot,
    /// each only where its process still runs. Told by event, since it held the window up to 2.9 s.
    /// </summary>
    [DriverRoute("HEAVY_SLOT", PipeClass.Open)]
    [ByEvent("HEAVY_SLOT", Untold = "a run's records beside the slot and whether its process lives")]
    private async Task<object?> HeavySlotAsync(HeavyRead press, CancellationToken cancellationToken)
    {
        var repository = (press.Repository ?? "").Trim();
        var service = _loop.Service ?? throw NotReady();
        var registry = await service.RegistryAsync(cancellationToken);
        // Files and processes, off the window's thread (UIFREEZE1).
        return await Task.Run(() => HeavySlotOf(_loop.Home, repository, LinesRoutes.Checkouts(registry, repository, null).Select(row => row.Root)), cancellationToken);
    }

    /// <summary>The slot of the first of <paramref name="roots"/> on this disk, as <c>HEAVY_SLOT</c> answers it.</summary>
    internal static object HeavySlotOf(string home, string repository, IEnumerable<string> roots, Func<int, long, bool>? alive = null)
    {
        var root = roots.FirstOrDefault(Directory.Exists);
        if (root is null)
        {
            return new { repository, waiters = Array.Empty<object>(), refusal = $"`{repository}` has no checkout on this machine to read." };
        }

        if (HeavySlot.KeyOf(root) is not { } key)
        {
            return new { repository, waiters = Array.Empty<object>(), refusal = $"git's files in `{repository}`'s checkout do not say which repository it is." };
        }

        var look = HeavySlot.Look(home, key, alive);
        var holder = look.Holder is { } held
            ? new
            {
                session = held.Session,
                // FABRIC-PIPELINE1d: the verification run holding it, where the heavy executor does.
                run = held.Run,
                gates = held.Gates,
                since = held.Since,
                now = held.Now is { } now ? new { gate = now.Gate, at = now.At, since = now.Since } : null,
                failed = held.Failed is { } failed ? new { gate = failed.Gate, exit = failed.Exit } : null,
            }
            : null;
        return new
        {
            repository,
            holder,
            waiters = look.Waiters.Select(waiter => new { session = waiter.Session, since = waiter.Since }).ToArray(),
            admission = AdmissionOf(home),
        };
    }

    /// <summary>
    /// The machine's heavy-run admission beside the repository's slot (HEAVYQUEUE1): <c>{ capacity, holds, waits }</c>, each run
    /// <c>{ kind, session?, what, since }</c>, the longest asking first, read from its file and never from a run's output. A session waiting
    /// for a place in it is told so on its page; the merge queue's heavy gates are in it too, as <c>queue</c>.
    /// </summary>
    private static object AdmissionOf(string home)
    {
        var look = MachineAdmission.Look(home);
        object Run(AdmissionEntry entry) => new { kind = entry.Kind, session = entry.Session, what = entry.What, since = entry.Since };
        return new { capacity = look.Capacity, holds = look.Holds.Select(Run).ToArray(), waits = look.Waits.Select(Run).ToArray() };
    }

    /// <summary>
    /// The list as the page sends it: null where it sends none or null; each gate's name, command and folder as written, a field that is
    /// no text read as none, so the mapping's own rule says what is missing.
    /// </summary>
    private static List<HeavyGate>? HeavyGatesOf(JsonElement? sent)
    {
        if (sent is not { } gates || gates.ValueKind == JsonValueKind.Null) return null;
        if (gates.ValueKind != JsonValueKind.Array)
        {
            throw new DriverException("a repository's heavy gates are a list of gates, each its name, its command and its folder, in the order they run.");
        }

        static string? Text(JsonElement gate, string name) =>
            gate.ValueKind == JsonValueKind.Object && gate.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        return [.. gates.EnumerateArray().Select(gate => new HeavyGate(Text(gate, "name") ?? "", Text(gate, "run") ?? "", Text(gate, "cwd"), Heavy: true))];
    }

    // Whether this machine interrupts the person at all (SURF5b). The same file
    // `daoris settings notify on|off` edits — one truth, two doors (D50).
    [DriverRoute("SET_NOTIFY", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SET_NOTIFY", None = OwnerNone.MachineSetting)]
    [GroundRead("SET_NOTIFY", nameof(PipeReads.Notify))]
    [Publishes("SET_NOTIFY", StateScopes.Driver)]
    private object? SetNotify(SetNotify press)
    {
        _context.Change(config => config.WithNotify(press.Notify));
        return State(_loop);
    }

    // How long an account cools when its agent names no time: whole minutes, at least one, the same
    // `cooloff` `daoris settings cooloff <minutes>` edits — one truth, two doors (D50). Less than one is the driver's refusal.
    [DriverRoute("SET_COOLOFF", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("SET_COOLOFF", None = OwnerNone.MachineSetting)]
    [GroundRead("SET_COOLOFF", nameof(PipeReads.CoolOff))]
    [Publishes("SET_COOLOFF", StateScopes.Driver)]
    private object? SetCoolOff(SetCoolOff press)
    {
        var minutes = press.Minutes ?? throw new DriverException(DriverConfig.CoolOffRefusal);
        _context.Change(config => config.WithCoolOff((int)Math.Clamp(minutes, int.MinValue, int.MaxValue)));
        return State(_loop);
    }

    // One kind's keep rule (KEEP1d, D195 point 2; the cleanup design §2.2, §4.4): the set-aside folders' days, `whole` or not, the
    // downloads' days or `never`, the logs' days, or a kind's default again; the same `keep` `daoris settings keep` edits, judged by the
    // one judgement (`KeepRules.Apply`) before anything is written, a value out of its bounds refused as KEEP_OUT_OF_RANGE. What the
    // Disk space card shows moves with it, so it declares the wiring with the state.
    [DriverRoute("SET_KEEP", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("SET_KEEP", None = OwnerNone.MachineSetting)]
    [GroundRead("SET_KEEP", nameof(PipeReads.KeepRule))]
    [NotAlready("SET_KEEP", "merges into a kind's keeping rule")]
    [Publishes("SET_KEEP", StateScopes.Driver | StateScopes.Wiring)]
    private object? SetKeep(SetKeep press)
    {
        static int? Clamped(long? value) => value is { } number ? (int)Math.Clamp(number, int.MinValue, int.MaxValue) : null;
        // A floor's size in GB, its share, or `off`: a size is judged against half the Daoris folder's volume as read here.
        var set = new KeepSet(press.Kind, Clamped(press.Days), press.Never, press.Whole, press.Default, Clamped(press.Gb), Clamped(press.Percent), press.Off);
        if ((KeepRules.IsFloor(set.Kind) || KeepRules.IsAllowance(set.Kind)) && set.Gb is not null) set = set with { Volume = SpaceVolumes.Read(DaorisHome.FolderOf(_loop.Home).Folder)?.Total };
        _context.Change(config => KeepRules.Apply(config, set));
        return State(_loop);
    }

    // How many sessions Daoris starts at once on this machine (CAPSET1, D130's CAPCOUNT1 note): the person's dial, the same
    // `cap` `daoris settings cap <n>` edits — one truth, two doors (D50). Less than one is the driver's refusal.
    [DriverRoute("SET_CAP", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("SET_CAP", None = OwnerNone.MachineSetting)]
    [GroundRead("SET_CAP", nameof(PipeReads.Cap))]
    [Publishes("SET_CAP", StateScopes.Driver)]
    private object? SetCap(SetCap press)
    {
        var cap = press.Cap ?? throw new DriverException(DriverConfig.CapRefusal);
        _context.Change(config => config.WithCap((int)Math.Clamp(cap, int.MinValue, int.MaxValue)));
        return State(_loop);
    }

    // The role catalogue (FOUNDATION1y-i, the foundation design §2.7): every role, the seed's and the person's, with its work, scope and
    // stored choices, and the work codes a role may take; a read, the same `daoris role` says. Beside them, the roles Daoris
    // suggests adding (the Designer while no role takes design), each the person's press alone.
    [DriverRoute("ROLES", PipeClass.Open)]
    [ByEvent("ROLES", Untold = "the store's roles, role policies, rules and starts, driver.json's staffing, and the machine log's starts that Use sums")]
    private Task<object?> RolesAsync(NoPayload _, CancellationToken cancellationToken)
    {
        var catalogue = RoleCatalog.ReadHome(_loop.Home);
        // Each role's last seven days: names and counts from the machine log's actor.chose lines; a role with no start is not listed.
        var use = RoleUse.Of(_loop.Home, DateTimeOffset.UtcNow);
        var config = DriverConfig.Load(_loop.ConfigPath);
        return Task.FromResult<object?>(new
        {
            Use = new { use.Days, use.Roles },
            Roles = RoleRowsRunning.Of(_loop.Home, catalogue, config),
            // Each role's policy (default, set or stale), its record and its next new start; a probe that reserves nothing and
            // asks no account. The fields this build enforces, and the presets, are what the doors take.
            Policies = RolePolicyRow.Of(new StaffLedger(_loop.Home), catalogue, config),
            PolicyFields = StaffCapabilities.Fields,
            PolicyPresets = StaffPresets.Names,
            Supervisor = SupervisorRuleRows.Of(_loop.Home),
            Works = RoleWork.Codes,
            Kept = RoleStaffing.Of(_loop.ConfigPath) is not null,
            Suggested = RoleSuggestion.For(catalogue).Select(each => new
            {
                each.Role.Id, each.Role.Name, each.Role.Takes, ScopeKind = each.Role.Scope.Kind, each.Choices,
            }),
            // The choices suggested for the seeded roles, whole; the page leaves out an agent this machine has not set up.
            SuggestedChoices = ActorSuggestion.Table.Select(each => new { each.Role, each.Choices }),
            SuggestedChoicesDated = ActorSuggestion.Dated,
        });
    }

    // The roles' structure (COMPANYVIEW1c, the company-view design §4): for each place asked (none is this machine) and each work asked
    // (none is every work, one this build does not know left out), the role a start finds there, the work's rule with where it was set,
    // and each choice's state by the last readings alone. A read: it walks no account and asks no account or binary, so a view opening
    // probes nothing (D150 point 5); names only.
    [DriverRoute("ROLE_STRUCTURE", PipeClass.Open)]
    [ByEvent("ROLE_STRUCTURE", Untold = "the store's roles, driver.json, and the accounts' readings, cool-offs and offers under the home")]
    private Task<object?> RoleStructureAsync(RoleStructureRead read, CancellationToken cancellationToken)
    {
        var places = new List<RolePlace>();
        var works = new List<string>();
        foreach (var place in read.Places ?? [])
        {
            var each = new RolePlace(Trimmed(place.Workspace), Trimmed(place.Repository));
            if (!places.Contains(each)) places.Add(each);
        }

        // AGENTSPLACE1a: the chosen place's chain alone (the place, its workspace, this machine), where the page reads one place.
        if (read.Chain is { ValueKind: JsonValueKind.Object } chosen)
        {
            foreach (var each in PlaceProjection.ChainOf(new RolePlace(Plain(chosen, "workspace"), Plain(chosen, "repository"))).Where(each => !places.Contains(each)))
            {
                places.Add(each);
            }
        }

        works.AddRange((read.Works ?? []).Where(work => RoleWork.Codes.Contains(work, StringComparer.Ordinal)).Distinct(StringComparer.Ordinal));

        if (places.Count == 0) places.Add(new RolePlace());
        if (works.Count == 0) works.AddRange(RoleWork.Codes);
        return Task.Run<object?>(() =>
        {
            var config = DriverConfig.Load(_loop.ConfigPath);
            var structure = RoleStructure.Of(() => RoleCatalog.ReadHome(_loop.Home), config, _loop.Harnesses, places, works);
            return new
            {
                Works = RoleWork.Codes,
                Roles = structure.Catalogue is { } catalogue ? RoleDoor.Rows(catalogue, config) : [],
                Catalogue = structure.CatalogueFailed is { } why ? new { Why = why } : null,
                Places = structure.Places.Select(place => new
                {
                    place.Place.Workspace,
                    place.Place.Repository,
                    Works = place.Works.Select(work => new
                    {
                        work.Work,
                        Role = work.Role is { } role
                            ? new { role.Id, role.Name, Scope = new { role.Scope.Kind, role.Scope.Id }, work.Revision }
                            : null,
                        work.Held,
                        Choices = work.Choices.Select(each => new
                        {
                            each.Choice.Agent, each.Choice.Model, each.Choice.Effort, each.Choice.From,
                            State = new { each.State.Code, each.State.At, each.State.Account, each.State.Until },
                        }),
                        work.ChoicesFailed,
                        Policy = work.Policy is { } policy
                            ? new { policy.Consent, policy.Agents, policy.Concurrency, policy.Minutes, policy.Source, Requirement = Requirement(policy.Requirement) }
                            : null,
                        work.PolicyFailed,
                    }),
                }),
                Agents = structure.Agents.Select(agent => new { agent.Agent, agent.Known, agent.Installed, agent.Version }),
            };
        }, cancellationToken);
    }

    private static string? Plain(JsonElement place, string name) =>
        place.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? Trimmed(value.GetString()) : null;

    /// <summary>The text with its edges trimmed, or null where nothing is left.</summary>
    private static string? Trimmed(string? text) => text?.Trim() is { Length: > 0 } trimmed ? trimmed : null;

    // The places (AGENTSPLACE1a, the Agents-place design §2 and §3): this machine, each workspace and a page of the driven repositories,
    // each with its driving, its works' roles and rules, the values of its head's line with where each was set, and why it needs a look,
    // by the readers a start uses. The not-driven repositories are counts, and a page of one workspace's when its fold is asked. A read of
    // driver.json, the role store and the service's registry and quests (a set-up quest still open is an adoption running): it asks no git,
    // process or account, so opening a place probes nothing (D150 point 5). `detail` answers one place whole, driven or not, and `members` a
    // workspace's whole list (SETUPUX1d).
    [DriverRoute("PLACES", PipeClass.Open)]
    [ByEvent("PLACES", Untold = "the service's registry and quests, driver.json and the role store")]
    private async Task<object?> PlacesAsync(PlacesPageRead read, CancellationToken cancellationToken)
    {
        var service = _loop.Service ?? throw NotReady();
        var registry = await service.RegistryAsync(cancellationToken).ConfigureAwait(false);
        PlaceTarget? target = read.Detail is { ValueKind: JsonValueKind.Object } detail
            ? new PlaceTarget(Plain(detail, "workspace"), Plain(detail, "repository"))
            : null;
        var ask = new PlacesAsk(
            read.Limit is >= int.MinValue and <= int.MaxValue ? (int)read.Limit : PlacesAsk.DefaultLimit, Trimmed(read.After), Trimmed(read.Search),
            read.NeedsLook, Trimmed(read.Fold), Trimmed(read.FoldAfter), Trimmed(read.FoldSearch), target, Trimmed(read.Members));

        // Only a repository not adopted can have an adoption running; an unreachable ledger reads as none running.
        IReadOnlyList<QuestView>? quests = null;
        if (registry.Any(known => !known.Adopted))
        {
            try
            {
                quests = await service.EveryQuestAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                quests = null;
            }
        }

        return await Task.Run<object?>(
            () => PlaceProjection.Of(DriverConfig.Load(_loop.ConfigPath), registry, () => RoleCatalog.ReadHome(_loop.Home), ask, _loop.Home, quests),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A work's typed requirement on the wire, its kind beside its fields; none where the work has none.</summary>
    private static object? Requirement(WorkRequirement? requirement) => requirement switch
    {
        HelpRequirement help => new { Kind = "help", help.SetUp },
        ReviewRequirement review => new { Kind = "review", review.Required, review.Verify, review.Recheck, review.MakerPasses },
        PartnerRequirement partner => new { Kind = "partner", partner.Measured },
        BrainstormRequirement brainstorm => new { Kind = "brainstorm", brainstorm.FanOut, brainstorm.ModeRead },
        HandOverRequirement handOver => new { Kind = "hand-over", handOver.Consent, handOver.Source },
        _ => null,
    };

    // A role added, changed, removed or given back its seed, the supervisor and the steward among them: the person's press, as `daoris
    // driver role` and `role_propose` make it, through one door, `RoleDoor.Apply`, whose judge refuses with nothing written. A removed
    // role's work holds its starts, said; a running session goes on; a role's terminal it no longer staffs ends.
    [DriverRoute("SET_ROLE", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("SET_ROLE", None = OwnerNone.MachineSetting)]
    [GroundRead("SET_ROLE", nameof(PipeReads.RoleRow))]
    [NotAlready("SET_ROLE", "edits a role's row of the catalogue in place, its revision moving")]
    [Publishes("SET_ROLE", StateScopes.Driver | StateScopes.Harnesses)]
    private object? SetRole(SetRolePress sent)
    {
        var press = RoleDoor.ReadPress(sent.Press.Json);
        RoleDoor.Apply(Staffing(), press.Change, press.Choices, press.Read, press.Policy);
        return RolesChanged();
    }

    // Several role changes made as one: the suggested choices, or a role's policy with a place's rules. Each is a `SET_ROLE` press in
    // `changes`, judged in order by `RoleDoor.ApplyAll` before anything is written; the first refusal refuses the batch, nothing written.
    [DriverRoute("SET_ROLES", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("SET_ROLES", None = OwnerNone.MachineSetting)]
    [GroundRead("SET_ROLES", nameof(PipeReads.RoleRows))]
    [NotAlready("SET_ROLES", "edits several roles' rows of the catalogue in place, each revision moving, in one transaction")]
    [Publishes("SET_ROLES", StateScopes.Driver | StateScopes.Harnesses)]
    private object? SetRoles(SetRolesPress sent)
    {
        RoleDoor.ApplyAll(Staffing(), RoleDoor.ReadPresses(sent.Press.Json));
        return RolesChanged();
    }

    private RoleStaffing Staffing() => RoleStaffing.Of(_loop.ConfigPath)
        ?? throw new DriverException("this home's staffing is the file's own (not this home's store), so its roles are not set here.");

    private object? RolesChanged()
    {
        _ = SupervisorDoor.RolesChangedAsync(_loop, CancellationToken.None);
        _loop.Nudge();
        return State(_loop);
    }

    // Ask Daoris's access mode: `restricted` or `full`, the person's press alone, over the same key
    // `daoris ask-daoris mode` sets: one truth, two doors (D50). Each start reads it, and a conversation running under another ends
    // at its turn's end, its next words resuming it under the mode chosen (ACCESSSWAP1): nothing it runs with changes while it runs.
    // A mode it does not know, or none, is the driver's refusal, with nothing written.
    [DriverRoute("SET_HELP_MODE", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SET_HELP_MODE", None = OwnerNone.MachineSetting)]
    [GroundRead("SET_HELP_MODE", nameof(PipeReads.HelpMode))]
    [Publishes("SET_HELP_MODE", StateScopes.Driver)]
    private object? SetHelpMode(SetHelpMode press)
    {
        var mode = press.Mode
            ?? throw new DriverException($"Ask Daoris's mode is `{HelpModes.Restricted}` or `{HelpModes.Full}`; the press named none.");
        _context.Change(config => config.WithHelpMode(mode));
        _loop.Chat?.HelpAccessChosen(HelpModes.Named(mode)!);
        return State(_loop);
    }

    // Restricted mode's configured access (HELPMODE1c2b, D183's note): the person's choice of one housekeeping operation, or since
    // HELPMODE1c3 one chosen setting, or since HELPMODE1c4 one checkout turned off or on by its repository, set on Ask Daoris's screen,
    // written to the home's own file. Each start reads it onto its allow-list, receipt and room, and each tool call
    // is judged by it then, so a revoke refuses the next call; a conversation running with another choice ends at its turn's end, its
    // next words resuming it with this one (ACCESSSWAP1). No proposal names it: a helper never widens its own access. A grant or an
    // operation this build does not act on is the driver's refusal, with nothing written.
    [DriverRoute("SET_HELP_GRANT", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SET_HELP_GRANT", None = OwnerNone.MachineSetting)]
    [GroundRead("SET_HELP_GRANT", nameof(PipeReads.HelpGrant))]
    [Publishes("SET_HELP_GRANT", StateScopes.Driver)]
    private object? SetHelpGrant(SetHelpGrant press)
    {
        var operation = press.Operation
            ?? throw new DriverException("A grant's press names the operation it chooses; this one named none.");
        ConfiguredAccess.Choose(_loop.Home, press.Grant ?? ConfiguredAccess.HousekeepingGrant, operation, press.Chosen);
        _loop.Chat?.HelpAccessChosen(DriverConfig.Load(_loop.ConfigPath).HelpMode);
        return State(_loop);
    }

    // The person's grant of a folder the driver is holding for the harness's trust (D73) — the
    // screen's half of `daoris agent trust`. Only a pair this machine's last tick held is
    // granted, in the file that tick read; the terminal is the door that names any folder. The holds themselves are the tick's
    // to tell again, as it looks at once.
    [DriverRoute("TRUST_FOLDER", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("TRUST_FOLDER", None = OwnerNone.MachineSetting)]
    [GroundWait("TRUST_FOLDER", "trust", "folder", "trustFile")]
    [Publishes("TRUST_FOLDER", StateScopes.Driver)]
    private object? TrustFolder(TrustFolderPress press)
    {
        var (folder, trustFile) = (press.Folder, press.TrustFile);
        var hold = _loop.Trust.Holding(folder, trustFile)
            ?? throw new DriverException(
                $"the driver is not holding `{folder}` for this agent's trust, so there is nothing "
                + "here to confirm — only a folder it is holding can be trusted from this screen. "
                + "`daoris agent trust claude-code <folder> --yes` names any folder.");

        // 🔴 Never the own sign-in's file (POOL1a3, D197 point 3; Fable's F3): its hold offers no press, and one sent anyway is
        // refused before anything is written. The person trusts the folder with the tool itself, at their terminal.
        if (hold.Own)
        {
            throw Refusals.Because(
                Refusals.TrustOwnHome,
                "That folder is held for your own sign-in, outside Daoris's accounts, so Daoris writes nothing in its file: trust it "
                + "with the tool itself, at your terminal.",
                ("folder", hold.Folder));
        }

        var grant = ClaudeTrust.Grant(hold.TrustFile, hold.Folder);
        // The hold is off the slice the windows read now, not a look from now (FOUNDATION1s): the grant is what the person just did,
        // and a look that still held it would put it back and push it again.
        if (grant.Verified)
        {
            _loop.Trust.Record([.. _loop.Trust.Latest.Where(held => held != hold)]);
            _loop.Slices.Find(ConsiderationsStateSlice.Name)?.Wake();
        }

        _loop.Nudge();
        return new
        {
            hold.Folder,
            grant.Key,
            grant.Changed,
            grant.Verified,
            Message = grant.Verified
                ? $"Trusted `{hold.Folder}` for this agent: its own `permissions.allow` applies there "
                  + "now, and the driver looks again."
                : $"Wrote the grant for `{hold.Folder}`, but reading `{hold.TrustFile}` back does not "
                  + "show it — a running Claude Code may have saved over it. If so, the driver will "
                  + "hold the same folder again.",
        };
    }

    /// <summary>
    /// *Try again*: one act that does whichever applies to the quest, read from the planner's verdict as Sessions' groups read it,
    /// so the press and the list cannot disagree. Anything else is refused, and nothing written.
    /// </summary>
    [DriverRoute("RETRY_QUEST", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("RETRY_QUEST", Quest = "quest")]
    [GroundQuest("RETRY_QUEST", "quest")]
    [Says("RETRY_QUEST", "quest:quest")]
    [Publishes("RETRY_QUEST", StateScopes.Sessions | StateScopes.Driver)]
    private async Task<object?> RetryQuestAsync(QuestPress press, CancellationToken cancellationToken)
    {
        var quest = press.Quest.Trim().TrimStart('#');
        var config = DriverConfig.Load(_loop.ConfigPath);
        var considered = _loop.Look.Latest
            ?? await SessionGroups.VerdictsAsync(_loop.Service ?? throw NotReady(), config, _context.Door(config), lastLook: null, cancellationToken, _loop.Home)
                .ConfigureAwait(false);
        // PUSH1f2a: the verdict's switch is the library's operation, which `daoris quest retry` acting alone calls too; a
        // pause or a quest neither parked nor held is its coded refusal (ChoiceCodes), raised here by the module's one mapping.
        var decision = QuestRetry.Decide(quest, considered);
        _context.Change(decision.Change);
        // STAFFPOLICY1e: the yes to a role policy's ask or bound is one approval beside the line's moves, which the next look reads.
        if (decision.Approve is { } role && !new StaffMoves(_loop.Home).Approve(quest, role))
        {
            throw new DriverException("this machine's store would not take your yes, so nothing was approved; try again.");
        }

        if (decision.Approve is not null) _loop.Nudge();
        return State(_loop, new { Quest = quest, decision.Did, decision.Session });
    }

    /// <summary>
    /// *Show it again* on the strip's review chip: the set-up step's newest set-up served to its tab again, with no session and no
    /// model. Refused wherever it would show anything but that build at the rule's address. The verdict is never a route here.
    /// </summary>
    [DriverRoute("SHOW_REVIEW_AGAIN", PipeClass.Window)]
    [Owner("SHOW_REVIEW_AGAIN", Quest = "quest")]
    [Publishes("SHOW_REVIEW_AGAIN", StateScopes.Driver)]
    private async Task<object?> ShowReviewAgainAsync(QuestPress press, CancellationToken cancellationToken)
    {
        var quest = press.Quest.Trim().TrimStart('#');
        var desk = _loop.Reviews
                   ?? throw new DriverException("this window carries no browser to show a review in, so nothing was shown again.");
        var service = _loop.Service ?? throw NotReady();
        var (ok, message) = await desk.ShowAgainAsync(service, DriverConfig.Load(_loop.ConfigPath), quest, cancellationToken);
        if (!ok) throw new DriverException(message);
        return State(_loop, shownAgain: new { Quest = quest, Message = message });
    }

    // "Look now": a person who just published a quest should not watch a poll countdown.
    [DriverRoute("NUDGE", PipeClass.Open)]
    private async Task<object?> NudgeAsync(NoPayload _, CancellationToken cancellationToken)
    {
        _loop.Nudge();
        // The one verb with nothing to answer — and `await` keeps this method honestly async.
        await Task.CompletedTask;
        return null;
    }

    // *Sync now* (SYNC6b): the tick's own pass for one circle, through the loop's own set —
    // `daoris remote sync` is the other door to the same pass (D50). What it says comes back
    // in the driver's words; a circle with no remote is its refusal, mapped above. A pass can move where each circle stands,
    // the quests and sessions it fed, and the registry, and a wall it hit is answered as its report (STATEHUB1d, D169).
    [DriverRoute("SYNC_NOW", PipeClass.Open)]
    [Publishes("SYNC_NOW", StateScopes.Sessions | StateScopes.Quests | StateScopes.Sync | StateScopes.Registry)]
    private async Task<object?> SyncNowAsync(SyncNowPress press, CancellationToken cancellationToken)
    {
        var workspace = RemoteTarget.Workspace(press.Workspace);
        var pass = _loop.SyncNowAsync(workspace, cancellationToken)
            ?? throw NotReady();
        var report = await pass;
        return new { Workspace = workspace, report.Problem, report.Notes };
    }

    /// <param name="retried">What *Try again* did (SESSUX1b): the quest, <c>marked</c> or <c>released</c>, and the stop's
    /// session for a release. Null for every other answer, which the bridge leaves out.</param>
    /// <param name="reviewed">What a review's procedure look found (REVIEWENV1a), on the answer to <c>SET_REVIEW</c> alone.</param>
    /// <param name="shownAgain">What *Show it again* said (REVIEWENV1d), on the answer to <c>SHOW_REVIEW_AGAIN</c> alone.</param>
    /// <remarks>
    /// A read of the loop alone, so the domains whose standing choices answer with it (Across, Lines) reach it by name over
    /// their loop, holding no other domain's class (PART10d).
    /// </remarks>
    internal static object State(DriverLoop loop, object? retried = null, object? reviewed = null, object? shownAgain = null)
    {
        var config = DriverConfig.Load(loop.ConfigPath);
        var access = ConfiguredAccess.Read(loop.Home);
        // HELPMODE1c3: the browser's grant is its plugin's approval, drawn here read only from that record.
        var browser = HelpGrants.Read(loop.Home, PluginCatalog.Load(loop.Home, AdapterSet.Built().Names)).Browser;
        // HELPMODE1c4: the checkouts D107 lets agents outside read, from the machine's facts as last judged, never a read of its own;
        // none before the facts are judged, and the loop's next look pushes them.
        string[] readable = loop.Facts.Current.Value<HelpMachineWhole>(HelpFacts.Machine, HelpFacts.Whole) is { } whole
            ? [.. AcrossRules.Readable(config, whole.Snapshot.Repositories).Select(checkout => checkout.Repository)]
            : [];
        return new
        {
            Retried = retried,
            Reviewed = reviewed,
            ShownAgain = shownAgain,
            // Whether the loop's service is up (LOOK2a): until it is, every route that reads it refuses *still coming up*,
            // so the status bar says starting rather than ready, which this file alone would claim.
            Ready = loop.Service is not null,
            loop.ConfigPath,
            // Where this machine's Daoris lives (D63), and what establishing it did this start — a
            // machine-local path, answered only over this bridge, like every path here.
            loop.Home,
            config.Drivable,
            config.Holds,
            // TERMTRUST1a (D201, the terminal-trust design §6.1): the person's standing confirmation as This Daoris's row and the line in
            // Notifications read it: offered, on, the presses granted and built, since, a record not honoured, its count this run, and
            // the holds a terminal made under it. None where this shell serves no pipe.
            StandingConfirmation = loop.Pipe?.StandingConfirmation.Now(),
            config.Trees,
            config.Cap,
            config.Adapter,
            config.PollSeconds,
            config.Notify,
            config.Strikes,
            // The cool-off a limit naming no time takes, in minutes (TOOL4g): the file's, or the hour when it sets none.
            CoolOff = (int)config.CoolOff.TotalMinutes,
            config.Forgiven,
            // The stops the person released (SESSUX1b), the quest against the session, as the terminal lists them.
            config.Released,
            // 🔴 Reported so the page can show it, and modelled on the record so no toggle deletes it.
            // Off is "" on the wire, never null: the bridge leaves a null out, and the page tells a
            // shell older than the intake by this field's absence (AGT6, seen on the window).
            IntakeAdapter = config.IntakeAdapter ?? "",
            // Off is "" on the wire for the intake's reason: a shell older than Ask Daoris sends no field.
            HelperAdapter = config.HelperAdapter ?? "",
            // Ask Daoris's access mode as the person chose it (HELPMODE1a2): `restricted` or `full`, what its next start reads. A
            // conversation's own, the mode its start got, is its record's (`HELP_CONVERSATIONS`' `mode`).
            config.HelpMode,
            // Restricted mode's access as the person chose it: each grant by its id, with the operations chosen and those it acts on,
            // so the page offers no other, and whether a running conversation was handed another choice its next turn takes. The
            // browser's is read only from its plugin's approval, with no `built`, so the page offers nothing to choose.
            HelpGrants = new[]
            {
                Grant(loop, ConfiguredAccess.HousekeepingGrant, access, ConfiguredAccess.HousekeepingBuilt),
                Grant(loop, ConfiguredAccess.SettingsGrant, access, ConfiguredAccess.SettingsBuilt),
                new HelpGrantRow(
                    ConfiguredAccess.CheckoutsGrant, [.. readable.Where(access.Reads)], readable,
                    loop.Chat?.GrantPending(ConfiguredAccess.CheckoutsGrant, access) == true, false, null),
                new HelpGrantRow(
                    "browser",
                    browser is { State: HelpGrants.Approved } ? ["reads", .. browser.Accepted] : [],
                    null, false, browser?.Changed == true, browser?.Plugin),
            },
            // The lines as set (WSR2), as rows rather than an object's keys: a key policy on the
            // bridge would respell a repository's name.
            Lines = config.Lines.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, Branch = p.Value }).ToArray(),
            WorkspaceLines = config.WorkspaceLines.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Workspace = p.Key, Branch = p.Value }).ToArray(),
            // Reading and writing across as set (D107), as rows for the same reason.
            ReadAcross = config.ReadAcross.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, Read = p.Value }).ToArray(),
            WorkspaceReadAcross = config.WorkspaceReadAcross.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Workspace = p.Key, Read = p.Value }).ToArray(),
            WriteAcross = config.WriteAcross.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, To = p.Value }).ToArray(),
            // GITAPP2b: the repositories whose checkout the person let Daoris change the files of (off where absent), as names.
            CheckoutWrites = config.CheckoutWrites.Where(p => p.Value).Select(p => p.Key).Order(StringComparer.Ordinal).ToArray(),
            // Each repository's standing answer (KNOWUSE1b), as rows for the same reason; `at` in UTC, or null where unknown.
            Standing = config.Standing.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, p.Value.Says, p.Value.At }).ToArray(),
            // This machine's heavy gates for each repository that marks none (HEAVYRUN1b2b), as rows for the same reason, each list in
            // the order its gates run; a gate's folder left out where it runs in the checkout itself.
            HeavyGates = config.HeavyGates.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new
                {
                    Repository = p.Key,
                    Gates = p.Value.Select(gate => new { gate.Name, gate.Run, Cwd = gate.WorkingDirectory }).ToArray(),
                }).ToArray(),
            // Which checks each repository's sessions run under Current (FABRIC-PIPELINE1b), as rows for the same reason.
            SessionChecks = config.SessionChecks.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, Checks = p.Value }).ToArray(),
            // The session languages as set (LANG1c), as rows for the same reason, and the closed table each screen names its
            // options from, so adding a language is a row in the driver's table and needs no window catalogue.
            Languages = config.Languages.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, Language = p.Value }).ToArray(),
            WorkspaceLanguages = config.WorkspaceLanguages.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Workspace = p.Key, Language = p.Value }).ToArray(),
            LanguageTable = SessionLanguages.Table.Select(row => new { row.Code, row.Name }).ToArray(),
            // The review rules as set (REVIEWENV1a), as rows for the same reason: a repository's may be none here.
            Reviews = config.Reviews.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, Rule = LinesRoutes.ReviewWire(p.Value) }).ToArray(),
            WorkspaceReviews = config.WorkspaceReviews.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Workspace = p.Key, Rule = LinesRoutes.ReviewWire(p.Value) }).ToArray(),
            // The second-opinion rules as set (XAGENT1a), as rows for the same reason, each naming the working agent's family
            // over the loop's agents, a plugin's among them (XAGENT1b3).
            Opinions = config.Opinions.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, Rule = LinesRoutes.OpinionWire(p.Value, config.Adapter, loop.Harnesses.Adapters) }).ToArray(),
            WorkspaceOpinions = config.WorkspaceOpinions.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Workspace = p.Key, Rule = LinesRoutes.OpinionWire(p.Value, config.Adapter, loop.Harnesses.Adapters) }).ToArray(),
            // The helpers and commit rules as set (DOORS1b), as rows for the same reason: a workspace's Defaults reads its own here.
            Helpers = config.Helpers.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, Rule = LinesRoutes.HelperWire(p.Value) }).ToArray(),
            WorkspaceHelpers = config.WorkspaceHelpers.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Workspace = p.Key, Rule = LinesRoutes.HelperWire(p.Value) }).ToArray(),
            // The hand-over rules as set (MIXAGENT1c), as rows for the same reason: a workspace's Defaults reads its own here.
            HandOver = config.HandOver.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, Rule = new { p.Value.Mode } }).ToArray(),
            WorkspaceHandOver = config.WorkspaceHandOver.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Workspace = p.Key, Rule = new { p.Value.Mode } }).ToArray(),
            Commits = config.Commits.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, Rule = LinesRoutes.CommitWire(p.Value) }).ToArray(),
            WorkspaceCommits = config.WorkspaceCommits.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Workspace = p.Key, Rule = LinesRoutes.CommitWire(p.Value) }).ToArray(),
            // The access setups as set (AUTONOMY1a), as rows for the same reason, and this machine's, none set being no pause: a
            // workspace's Defaults and Settings read their own here, a repository's setup reads what it resolves to off LINES.
            Access = config.Access.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, Setting = LinesRoutes.AccessWire(p.Value) }).ToArray(),
            WorkspaceAccess = config.WorkspaceAccess.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Workspace = p.Key, Setting = LinesRoutes.AccessWire(p.Value) }).ToArray(),
            MachineAccess = LinesRoutes.AccessWire(config.MachineAccess),
            // Git access as set (GITAUTH1f), level by level: a workspace's Defaults and Settings read their own here.
            GitAccess = LinesRoutes.GitLevelsOf(config).Repository.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, Classes = LinesRoutes.GitClassesWire(p.Value) }).ToArray(),
            WorkspaceGitAccess = LinesRoutes.GitLevelsOf(config).Workspace.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Workspace = p.Key, Classes = LinesRoutes.GitClassesWire(p.Value) }).ToArray(),
            MachineGitAccess = LinesRoutes.GitClassesWire(LinesRoutes.GitLevelsOf(config).Machine),
            Running = loop.Processes.Running,
            // Who is driving Daoris's browser (BRW8): the running sessions handed a server that drives it.
            DrivingBrowser = loop.Processes.DrivingBrowser,
            // The Ask Daoris conversations running under another access than the one chosen (ACCESSSWAP1): each ends at its turn's end,
            // and the page keeps it in view so its next words resume it under the new access.
            HelpSwapping = loop.Chat?.HelpSwapping(config.HelpMode) ?? [],
            // The set-ups waiting for the person here (REVIEWENV1d, design §3.3), each with whether Daoris serves its tab now: the
            // strip's chip beside the browser's door. Left out where the loop carries no browser, which shows nothing.
            InReview = loop.Reviews?.Waiting
                .Select(row => new { row.Quest, row.Title, row.Environment, row.Look, row.Shows, row.Served })
                .ToArray(),
        };
    }

    /// <summary>A grant kept in the home's record, as <c>helpGrants</c> answers it: chosen, built, and pending a running conversation's next turn.</summary>
    private static HelpGrantRow Grant(DriverLoop loop, string grant, ConfiguredAccess access, IReadOnlyList<string> built) =>
        new(grant, access.Of(grant), built, loop.Chat?.GrantPending(grant, access) == true, false, null);

    /// <summary>
    /// One row of <c>helpGrants</c> (HELPMODE1c2b, HELPMODE1c3): the grant, what is chosen, the operations the driver acts on (none for one
    /// set elsewhere, the browser's), whether a running conversation has yet to take it, whether its plugin's update changed it since
    /// approval, and the plugin whose tab sets it.
    /// </summary>
    private sealed record HelpGrantRow(string Id, IReadOnlyList<string> Chosen, IReadOnlyList<string>? Built, bool Pending, bool Changed, string? Plugin);
}

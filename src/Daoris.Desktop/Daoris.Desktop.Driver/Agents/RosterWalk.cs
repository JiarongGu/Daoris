using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// The walk over an agent's accounts for a start: the steps taken in order and what each leaves unknown, held or chosen.
/// </summary>
internal sealed class RosterWalk(HarnessRoster roster)
{
    /// <summary>The walk behind <see cref="SelectAsync"/> and a view's read of it (<see cref="WiringAsync"/>).</summary>
    /// <param name="asks">
    /// False for a view's read (ROSTER1, D150 point 5: nothing is probed as a view opens): the walk composes from the last readings, the
    /// account's, the cool-offs', the offer judgement and the agent's report, and asks no account's status nor its binary again. An
    /// account read signed out stays signed out for it however old the reading, and an agent the report says is absent is refused as such;
    /// the next start, which asks, or a press, is what reads them again.
    /// </param>
    internal async Task<HarnessSelection> WalkAsync(
        string adapter, DriverConfig config, string? workspace, string? chosen, StartKind kind, bool counts, bool ownHome,
        CancellationToken ct, OfferAsk? offer = null, bool asks = true)
    {
        offer ??= OfferAsk.None;
        var resolved = roster.Adapters.Resolve(adapter);

        // An adapter that declares no toolchain has nothing to check: it spawns exactly as it did
        // before this existed. Purely additive, which is what lets a new adapter arrive without
        // answering questions about installers it may not have.
        if (resolved.Toolchain is not { } toolchain) return new HarnessSelection(Refusal: null);

        var settings = roster.Settings;
        // 🔴 A door runs as its OWNER's accounts (AGT7): the owner's default, the owner's directory,
        // the owner's login question and key. The pin below stays the door's own — a door is a
        // different package at a different version (ACP2).
        var owner = toolchain.Owner(resolved.Name);
        // The agent's one set, whatever the workspace (POOL1a2, the account-pool design §2.4): no start reads a workspace's scope.
        var named = settings.Set(owner);
        var picked = string.IsNullOrWhiteSpace(chosen) ? null : chosen.Trim();
        // Where the person named no account, the agent's Daoris accounts are the start's (OWNSPACE1a), and none signed in holds it.
        var ours = picked is null && !ownHome && named.List.Count == 0 && named.Begins is null && !toolchain.StartsInOwnHome;
        var scope = ours ? roster.Walked(named, owner, toolchain) : named;

        // Where the agent's door carries none of its windows, its own server is asked them first, for each account of the list
        // the start could take that a set point owes a question (CODEXUSE1, USAGEPROBE1): never read, unread for the interval,
        // projected near its curve, or a limit or a credit said of it. A start, never a look or a panel, which count nothing.
        // Each under its own account's lock and under no gate of the walk's (PARALLEL1c), so another start never waits on them.
        // Only where there is a choice for a reading to change (USAGEONE1): a set of one, and a resume held to its own account,
        // run where they run whatever they would say, so the hour's backstop and a person's look read them instead.
        if (counts && picked is null && !ownHome && scope.List.Count > 1)
        {
            await roster.FreshenAsync(resolved.Name, owner, scope.List, config, ct, scope.Use).ConfigureAwait(false);
        }

        // The first look, before any question: read again under the gate once the questions are asked (PARALLEL1c).
        var now = roster.Clock();
        var (order, known) = OrderOf(owner, scope, kind, picked, ownHome, now, offer.Except);

        // What a hold says names each account by the person's name, read now, as it is said (ACCT2b); the selection's facts
        // and the record's opening line keep the ids.
        var names = AccountNames.Of(roster.Home, owner);

        // 🔴 No Daoris account to run on (OWNSPACE1a, D175 point 2): the start waits for the person's sign-in, before anything is
        // probed or spawned, and never runs in the person's own tool home, which a scope naming nothing once meant (D125 point 5).
        if (ours && order is [null]) return HarnessRoster.NoAccount(owner, [], names);
        var states = order.Select(account => roster.Before(owner, account, now, names, HarnessRoster.FactOf(known, account))).ToList();

        // 🔴 A cooling account is held FIRST, by a file read, before any probe (TOOL4d, D125 §3.3, §4): a start on a
        // spent account is refused at once and spends nothing, and three of them parked a quest on 1 October whose only
        // fault was its account. An account its provider already refused is not spent again (AGT3b). So when no account
        // the start may use is ready on either count, nothing is spawned or probed: the start waits.
        if (!states.Any(state => state.IsReady))
        {
            // A pick is the person's (§3.3): refused, never rotated, naming what they could pick instead.
            if (picked is null || states[0].Cooling is not { } pick) return Unready(owner, states, picked is null ? scope : null, kind, now, names);

            var refusal = RotationWords.PickedOf(
                pick, await roster.ReadyAsync(resolved.Name, toolchain, config, pick.Account, now, ct).ConfigureAwait(false), roster.Zone, names);
            return new HarnessSelection(refusal.Note) { Cooling = pick, RefusalNote = refusal };
        }

        // Which binary this spawn runs (TOOL2/D57): the explicit command, then the managed pin, then
        // PATH. An explicit `commands` entry is the person naming exactly what to run and has the
        // last word — a pin quietly replacing it would be a standing choice overruling a specific one.
        string? managed = null;
        string? pinnedVersion = null;
        if (config.Commands.GetValueOrDefault(resolved.Name) is not { Count: > 0 }
            && settings.ResolveVersion(resolved.Name, workspace, chosen: null) is { } pinned)
        {
            pinnedVersion = pinned;
            managed = HarnessSettings.ManagedBinary(roster.Home, resolved.Name, pinned, toolchain.Binary);
            if (managed is null)
            {
                // 🔴 Refused, never a silent fall back to PATH. Running a different tool than the one
                // that was pinned — and recording its version as though it were the pinned one —
                // is the failure this whole shape exists to prevent.
                var pinRefusal = $"`{resolved.Name}` is pinned to {pinned} on this machine, and nothing is "
                    + $"installed at that version — `daoris agent pin {resolved.Name} {pinned}` "
                    + $"installs it, and `daoris agent unpin {resolved.Name}` goes back to PATH. "
                    + "Daoris will not quietly run a different version than the one you asked for.";
                return new HarnessSelection(pinRefusal)
                {
                    Absent = AgentAbsence.PinNotInstalled,
                    RefusalNote = Noted.Of(HeldNoteCodes.HeldPinNotInstalled, pinRefusal, ("agent", resolved.Name), ("version", pinned)),
                };
            }
        }

        var report = await roster.ReportAsync(resolved.Name, config, refresh: false, ct).ConfigureAwait(false);
        // 🔴 A pin this start resolved counts for this start alone (WSPINONLY1): the roster's binary look reads the machine's
        // install and pin, never a workspace's, so an agent only a workspace pins was refused as absent. The managed install
        // the pin names is its presence, found above; another workspace's start resolves no such pin and still asks the machine.
        var pinPresent = managed is not null;
        if (asks && !pinPresent && report is { Present: false })
        {
            // Asked again before refusing: the cached answer may predate the install the last refusal
            // asked for. Its binary alone, since an install is no question of an account (ROSTER1). A view's read refuses on the
            // cached answer, as it asks nothing.
            await roster.BinaryChangedAsync(resolved.Name, config, ct).ConfigureAwait(false);
            report = await roster.ReportAsync(resolved.Name, config, refresh: false, ct).ConfigureAwait(false);
        }

        // The pin's version is the version this start runs, where the machine's look found none to name.
        var version = pinPresent && report is { Present: false } ? pinnedVersion : report?.Version;
        if (!pinPresent && report is { Present: false })
        {
            var installs = toolchain.Install is { Count: > 0 };
            var own = string.Join(' ', toolchain.Command(config.Commands.GetValueOrDefault(resolved.Name)));
            var install = installs
                ? $"`daoris agent install {resolved.Name}` installs it"
                : $"install it with its own tooling ({own})";

            const string never = "Daoris never installs an agent unasked: a tool that changed under a running loop is a moving target.";
            var absent = $"`{resolved.Name}` is not installed on this machine, so there is nothing to spawn — {install}. {never}";
            return new HarnessSelection(absent)
            {
                Absent = AgentAbsence.NotInstalled,
                // An agent with its own tooling names that command, which may hold a path: it is the program's words between the
                // lead and the line that says Daoris never installs, never a value (ROSTERREFUSE2).
                RefusalNote = installs
                    ? Noted.Of(HeldNoteCodes.HeldNotInstalled, absent, ("agent", resolved.Name))
                    : Noted.Of(HeldNoteCodes.HeldNotInstalledOwn,
                            $"`{resolved.Name}` is not installed on this machine, so there is nothing to spawn — install it with its own tooling (",
                            ("agent", resolved.Name))
                        .Then("", Noted.Said(own, NoteBy.Program)).Then("). ", Noted.Of(HeldNoteCodes.HeldNeverInstalls, never)),
            };
        }

        // The ACP adapter's own seam (ACP2): it runs the Agent SDK, which needs to be told which
        // `claude` to use. Read from the PIPE door's pin, because that is where the CLI lives.
        var claude = resolved.Wire == SessionWire.Acp
            ? HarnessSettings.ManagedBinary(
                roster.Home, "claude-code", settings.ResolveVersion("claude-code", workspace, null), ["claude"])
            : null;

        if (order is [null])
        {
            return new HarnessSelection(null, null, null, version, managed, claude);
        }

        // The account's state is its owner's answer when this build carries the owner — a door
        // declares no login question of its own, which is what `accountOf` says.
        var asker = roster.AccountAgent(resolved.Name, toolchain);

        // 🔴 The walk (TOOL4f, D125 §3.3), with no question asked under its gate (PARALLEL1c, D181 point 3). A pass reads again
        // the order, each account's state and what the agent last said of each, and chooses the first still ready, counting it
        // where the start counts, under the gate alone, so starts made together each see the others' counts. A question the walk
        // owes an account, a wait for the chosen account's lock, and every other question are outside it: a question that does
        // not answer on one account holds no start on another.
        var walk = new WalkMemory();
        // Each account's offer reading, judged for the model this start asks (D205 §3.3, MODELROLE1c2): an account passed already, or
        // whose reading of this version, under a day old, lacks or refused it, is passed for this start, never cooled.
        var readings = offer.Model is null ? [] : AgentOffers.Read(roster.Home);
        string? Offers(string account) =>
            offer.Passed?.Contains(account, StringComparer.OrdinalIgnoreCase) == true
                ? AgentOffers.NotOffered
                : AgentOffers.Judge(readings, resolved.Name, version, account, offer.Model, roster.Clock());
        WalkPass pass;
        while (true)
        {
            if (counts)
            {
                lock (_walking) pass = Pass(owner, scope, kind, picked, ours, counts, names, walk, Offers, offer.Except, asks);
            }
            else
            {
                pass = Pass(owner, scope, kind, picked, ours, counts, names, walk, Offers, offer.Except, asks);
            }

            if (pass.Held is { } hold) return hold;
            if (pass.Ask is { } asking)
            {
                // Asked again, as the install above is, since the person may have signed in since (TOOL6g): that account
                // alone, and only once a sign-in marked it or the word is `SignedOutAskedAgain` old, or once after a refusal
                // its last reading said signed in (TOOL6h). Before, a start held on a signed-out account asked every account's
                // status at every look. An account never read is unknown, and a start runs on it as on any unknown (SES3): a
                // look asks nothing (ROSTER1). One whose lock another question kept past its patience is unknown for this walk
                // alone, and passed (PARALLEL1c).
                HarnessProbe.LoginAnswer answer;
                try
                {
                    answer = await roster.AskOneAsync(asker.Name, asker.Toolchain, owner, asking, config, ct).ConfigureAwait(false);
                }
                finally
                {
                    _asking.TryRemove(HarnessRoster.AccountKey(owner, asking), out _);
                }

                if (answer.Locked) walk.Unknown.Add(asking);
                else walk.Said[asking] = answer.Login;
                if (pass.Owed && AccountReads.Of(roster.Home, owner).Accounts.GetValueOrDefault(asking) is { Login: LoginState.In } fresh)
                {
                    roster._fresh[HarnessRoster.AccountKey(owner, asking)] = fresh.At;
                }

                continue;
            }

            // Counted already, and busy from then on (TOOL6g): a status question another caller is asking the account now is
            // waited out, and no question begun after asks it while the session's own process may refresh its token. Where its
            // lock stays held past its patience, the count is taken back and the account passed for this walk (PARALLEL1c).
            if (counts && !await WaitedOutAsync(owner, pass.States[pass.At].Account!, pass.Number, ct).ConfigureAwait(false))
            {
                walk.Unknown.Add(pass.States[pass.At].Account!);
                continue;
            }

            break;
        }

        // What the pass that chose it read: the start's line says the walk as it was when the account was counted.
        (states, order, now) = (pass.States, pass.Order, pass.Now);
        var (at, facts) = (pass.At, pass.Facts);
        var runs = states[at].Account!;
        var home = HarnessSettings.ProfileHome(roster.Home, owner, runs);

        // An account that is a key is handed its key through the tool's own variable (AGT3) — the
        // owner's variable, for a door, since the door runs the owner's tool.
        var key = asker.Toolchain.KeyVariable is { Length: > 0 } variable
            && HarnessKeys.Of(roster.Home, owner, runs) is { } held
                ? new Dictionary<string, string> { [variable] = held }
                : null;

        var selection = new HarnessSelection(null, runs, home, version, managed, claude, key) { Number = pass.Number };
        if (picked is not null || scope.List.Count == 0 || scope.Begins is not { } begins) return selection;

        // Said by whoever opens the record (TOOL4f, §3.6; TOOL6b, §16.4): its first line and `account.rotated`, with what each
        // account of the list last said about its windows (TOOL6c).
        var choice = AccountRotation.Chose(scope, kind, states, at, facts, now);
        var passed = states.FirstOrDefault(state => string.Equals(state.Account, begins, StringComparison.OrdinalIgnoreCase));
        // Each step by code beside its English (UX7d-1): the choice's clause joins them, and a rotation's is the first alone.
        var steps = RotationWords.Steps(choice, owner, runs, scope, null, passed, facts, kind, now, roster.Zone);
        // Over its curve is the log's `near`, the word for an account passed (SWITCH1b-ii).
        string? Standing(string account) => AccountReadings.Standing(facts.GetValueOrDefault(account)?.Said, scope.Use, now);
        var walked = scope.List.Where(account => order.Contains(account, StringComparer.OrdinalIgnoreCase)).ToList();
        return selection with
        {
            Choice = scope.Use.Use == "goal" && order.Count > 1 ? new AccountChoice(choice.Step, RotationWords.Text(steps)) : null,
            Steps = steps,
            // A start's line says what was projected (USAGEPROBE1); a panel, which a person waits on, reads no history for it.
            AccountsSaid = RotationWords.AccountsSaid(walked, counts ? roster.Projected(owner, walked, facts, now) : facts, scope.Use, now),
            Rotated = string.Equals(runs, begins, StringComparison.OrdinalIgnoreCase)
                ? null
                : new RotatedStart(begins, passed?.Cooling, steps[0].Text)
                {
                    Step = choice.Step,
                    // The set's, for every start now (the account-pool design §2.4); a line written before keeps the workspace it named.
                    Scope = null,
                    FromSaid = Standing(begins),
                    ToSaid = Standing(runs),
                },
        };
    }

    /// <summary>
    /// The accounts a start may use, in the order it tries them (TOOL6b, D130 §16.3; D125 §3.3 under `order`), and what the walk
    /// knows of each. A pick, a scope that names one account, and the tool's own sign-in are the one account, and nothing more
    /// is read.
    /// </summary>
    internal (List<string?> Order, IReadOnlyDictionary<string, AccountFacts> Facts) OrderOf(
        string owner, RotationScope scope, StartKind kind, string? picked, bool ownHome, DateTimeOffset now, long except = 0)
    {
        if (picked is not null) return ([picked], HarnessRoster.NoFacts);
        if (ownHome || scope.List.Count == 0) return ([ownHome ? null : scope.Begins], HarnessRoster.NoFacts);

        var facts = roster.Facts(owner, scope.List, now, except);
        return ([.. AccountRotation.Order(scope, kind, HarnessSettings.Profiles(roster.Home, owner), facts, now)], facts);
    }

    /// <summary>
    /// The hold of a start whose every ready account's reading does not offer its model (D205 §3.3, MODELROLE1c2): one sentence naming
    /// each account by the person's name, and the soonest cool-off of the rest beside it, so the actor's walk passes the choice
    /// <c>not-offered</c> and a pick is refused with its reason (§3.5(d)).
    /// </summary>
    private static HarnessSelection NotOfferedHold(
        string owner, IReadOnlyList<string> unoffered, IReadOnlyList<AccountState> states, IReadOnlyDictionary<string, string> names)
    {
        var said = string.Join(", ", unoffered.Select(account => $"`{AccountNames.Said(names, account)}`"));
        var soonest = states.Select(state => state.Cooling).OfType<CoolingEntry>().OrderBy(cooling => cooling.Until).FirstOrDefault();
        var verb = unoffered.Count == 1 ? "account {0} does" : "accounts {0} do";
        return new HarnessSelection($"the `{owner}` {string.Format(System.Globalization.CultureInfo.InvariantCulture, verb, said)} not offer the model asked.")
        {
            NotOffered = [.. unoffered],
            Cooling = soonest,
        };
    }

    /// <summary>
    /// One pass of a start's walk (TOOL4f, D125 §3.3), asking nothing (PARALLEL1c): the order and each account's state read again,
    /// with what this walk's own questions answered and every other account's last reading (ROSTER1), and the first account still
    /// ready chosen, and counted where the start counts. A counted start's pass runs under the walk's gate, so it reads and counts
    /// as one; it reads files and asks no agent.
    /// </summary>
    /// <remarks>
    /// An account read signed out that is owed a question (TOOL6g's mark or hour, TOOL6h's fresh reading) is handed back to be
    /// asked outside the gate, claimed so no other walk asks it beside this one; one another walk is asking now is passed as its
    /// last reading says, never waited on. An account unknown for this walk is passed; where nothing else is chosen, the start is
    /// held on it, for the next start to try again.
    /// </remarks>
    private WalkPass Pass(
        string owner, RotationScope scope, StartKind kind, string? picked, bool ours, bool counts,
        IReadOnlyDictionary<string, string> names, WalkMemory walk, Func<string, string?>? offers = null, long except = 0, bool asks = true)
    {
        var now = roster.Clock();
        var (order, facts) = OrderOf(owner, scope, kind, picked, ownHome: false, now, except);
        var states = order.Select(account => roster.Before(owner, account, now, names, HarnessRoster.FactOf(facts, account))).ToList();
        // What the agent last said of each account (ROSTER1), as a report says it: an account not on this machine said nothing.
        var present = HarnessSettings.Profiles(roster.Home, owner);
        var reads = AccountReads.Of(roster.Home, owner);

        var at = -1;
        List<string> passed = [];
        List<string> unoffered = [];
        for (var i = 0; i < states.Count && at < 0; i++)
        {
            if (!states[i].IsReady) continue;
            var account = states[i].Account!;
            // D205 §3.3 (MODELROLE1c2): an account whose reading does not offer the model asked is passed for this start, before any
            // question is asked of it: not a cool-off, never counted.
            if (offers?.Invoke(account) is not null)
            {
                unoffered.Add(account);
                continue;
            }

            if (walk.Unknown.Contains(account))
            {
                passed.Add(account);
                states[i] = states[i] with { Locked = true };
                continue;
            }

            var read = present.FirstOrDefault(name => string.Equals(name, account, StringComparison.OrdinalIgnoreCase)) is { } listed
                ? reads.Accounts.GetValueOrDefault(listed)
                : null;
            var asked = walk.Said.TryGetValue(account, out var answered);
            var login = asked ? answered : read?.Login ?? LoginState.Unknown;
            var key = HarnessRoster.AccountKey(owner, account);
            // A view's read asks nothing (`asks` false): the reading stands, and the one fresh reading a refusal owes (TOOL6h) stays
            // owed to the next start that asks.
            if (asks && login == LoginState.Out && !asked && _asking.TryAdd(key, true))
            {
                // The one fresh reading a refusal of a signed-in reading owes it (TOOL6h), spent here whatever it answers.
                var owed = roster._owed.TryRemove(key, out _);
                if (owed || roster.AskAgain(owner, account, read?.At)) return new WalkPass(Ask: account, Owed: owed);
                _asking.TryRemove(key, out _);
            }

            if (login == LoginState.Out)
            {
                var profile = ShellWord.Of(account, ShellWord.Account);
                var refusal = $"the `{owner}` account `{AccountNames.Said(names, account)}` is not signed in, so a session would have "
                    + $"nothing to run as — `daoris agent login {owner} --account {profile}` runs "
                    + "the agent's own sign-in into it. Daoris manages the directory and the name; the "
                    + "credential stays in the agent's own store.";
                states[i] = states[i] with
                {
                    Readiness = AccountReadiness.SignedOut,
                    Refusal = refusal,
                    RefusalNote = Noted.Of(HeldNoteCodes.HeldSignedOut, refusal, ("owner", owner), ("account", account), ("profile", profile)),
                };
                continue;
            }

            at = i;
        }

        if (at < 0)
        {
            // An account passed for its lock comes free by itself: the start is held for the next to try, never waiting for a time.
            if (passed.Count > 0) return new WalkPass(Held: Locked(owner, passed, names));

            // Every account still ready refuses the model (D205 §3.3, §3.5(d)): the start holds with the reason, the soonest cool-off of
            // the rest beside it, for the actor's walk to take its next choice.
            if (unoffered.Count > 0 && !states.Any(state => state.IsReady && state.Readiness != AccountReadiness.SignedOut
                    && !unoffered.Contains(state.Account!, StringComparer.OrdinalIgnoreCase)))
            {
                return new WalkPass(Held: NotOfferedHold(owner, unoffered, states, names));
            }

            // Every Daoris account the person never named said signed out: none is signed in, so the same sentence (OWNSPACE1a).
            return new WalkPass(Held: ours && states.All(state => state.Readiness == AccountReadiness.SignedOut)
                ? HarnessRoster.NoAccount(owner, RotationWords.SignedOut(states), names)
                : Unready(owner, states, picked is null ? scope : null, kind, now, names));
        }

        return new WalkPass
        {
            States = states, Order = order, Facts = facts, Now = now, At = at,
            // Counted from here on as one of this account's sessions, so the next start chosen before the next look spreads.
            Number = counts ? roster.Chose(owner, states[at].Account!) : 0,
        };
    }

    /// <summary>
    /// A counted start waits out a question in flight on the account it chose (TOOL6g), counted already, so no question begun
    /// since asks it. False, the count taken back, where the account's lock stays held past <see cref="LockPatience"/>
    /// (PARALLEL1c): a start beside a question in flight could spend its single-use refresh token twice, which signs it out.
    /// </summary>
    private async Task<bool> WaitedOutAsync(string owner, string account, long number, CancellationToken ct)
    {
        IAsyncDisposable? held;
        try
        {
            held = await ProbeLock.TakeAsync(ProbeLock.PathOf(roster.Home, owner, account), roster.LockPatience, ct).ConfigureAwait(false);
        }
        catch
        {
            roster.Unchose(number);
            throw;
        }

        if (held is null)
        {
            roster.Unchose(number);
            return false;
        }

        await held.DisposeAsync().ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// The hold of a start whose every account it could run on stayed locked by another question past the walk's patience
    /// (PARALLEL1c): nothing counted, nothing run beside the question, and no wait for a time, so the next start tries again. Its
    /// words alone, a refusal no held note carries by code (<see cref="HarnessSelection.RefusalNote"/>).
    /// </summary>
    private static HarnessSelection Locked(string owner, IReadOnlyList<string> accounts, IReadOnlyDictionary<string, string> names)
    {
        var one = accounts.Count == 1;
        var said = string.Join(", ", accounts.Select(account => $"`{AccountNames.Said(names, account)}`"));
        var words = $"the `{owner}` account{(one ? "" : "s")} {said} {(one ? "is" : "are")} being asked about by another question that held "
            + $"{(one ? "it" : "them")} longer than a start waits, so nothing starts on {(one ? "it" : "them")} now: a start beside that "
            + "question could sign the account out. The next start tries again.";
        return new HarnessSelection(words)
        {
            RefusalNote = Noted.Of(HeldNoteCodes.HeldLocked, words, ("owner", owner), ("accounts", (IReadOnlyList<string>)[.. accounts])),
        };
    }

    /// <summary>
    /// No account the start may use is ready (TOOL4f, D125 §4). One account is held as it always was: its own sentence,
    /// and its cool-off where that is why. Over an order, the start waits for the first reset when one is cooling, said
    /// with every account and why; with none cooling, nothing will come ready by itself, so the default's own refusal,
    /// which names the fix, is the answer.
    /// </summary>
    /// <remarks>
    /// A wait for a time also says what the person could do (TOOL6b, D130 §3.3, §4.6), where it applies: a driven start
    /// whose kept account is ready says it is kept for conversations. No account is outside the set (POOL1a2, D197 point 1), so
    /// none is named as one a list could add. Read from the cool-offs and the refusals alone, as a waiting look starts no process.
    /// </remarks>
    /// <param name="scope">The start's scope, or null for a pick, which says nothing more.</param>
    /// <param name="names">The owner's names by id, which the sentence says each account by (ACCT2b), and its facts carry.</param>
    private HarnessSelection Unready(
        string owner, IReadOnlyList<AccountState> states, RotationScope? scope, StartKind kind, DateTimeOffset now,
        IReadOnlyDictionary<string, string> names)
    {
        var first = LineAccount.Earliest(ResetSet.Walk, states);
        // TOOL6g: the accounts passed as not signed in are facts beside the sentence, and over a list with nothing cooling
        // each is named with its sign-in, where the first account's refusal alone named only its own.
        var signedOut = RotationWords.SignedOut(states) is { Count: > 0 } passed ? SignedOutAccounts.Of(owner, passed, names) : null;
        var held = states.Count == 1 || (first is null && signedOut is null)
            ? new HarnessSelection(states[0].Refusal)
            {
                Cooling = states[0].Cooling, NotReady = states[0].Readiness, SignedOut = signedOut, RefusalNote = states[0].RefusalNote,
            }
            : first is null
                ? new HarnessSelection(RotationWords.NoneReady(owner, states, names))
                {
                    SignedOut = signedOut, RefusalNote = RotationWords.NoneReadyOf(owner, states, names),
                }
                : new HarnessSelection(RotationWords.Wait(owner, states, roster.Zone, names))
                {
                    Cooling = first, SignedOut = signedOut, RefusalNote = RotationWords.WaitOf(owner, states, roster.Zone, names),
                };
        if (held.Cooling is null || scope?.Begins is null) return held;

        // What is said after the refusal is parts of their own after its code (ROSTERREFUSE2); where the refusal has none, the
        // sentence keeps its words.
        var sentence = held.Refusal!;
        var note = held.RefusalNote;
        if (kind == StartKind.Driven
            && scope.Use.Keep is { } kept
            && scope.List.Contains(kept, StringComparer.OrdinalIgnoreCase)
            && !states.Any(state => string.Equals(state.Account, kept, StringComparison.OrdinalIgnoreCase))
            && roster.Before(owner, kept, now).IsReady)
        {
            var aside = RotationWords.KeptAsideOf(kept, names);
            sentence += " " + aside.Note;
            note = note?.Then(" ", aside);
        }

        // No account outside the set to name (POOL1a2, D197 point 1): every account of Daoris's is in the order the start walked.
        return held with { Refusal = sentence, RefusalNote = note };
    }

    // ——— What the goal's walk counts (TOOL6b, D130 §4.2, §16.2): Daoris's sessions as of the driver's last look, and the
    // starts chosen since. One roster serves the driver and the conversations alike, so both count.

    // One choice and its count at a time, so starts begun together see each other's: a pass of a counted start's walk, which
    // reads files and counts, and is held across no question, binary or lock wait (PARALLEL1c, D181 point 3).
    private readonly object _walking = new();

    // The accounts a walk is asking its status now (PARALLEL1c), by owner and account: another walk passes such an account as its
    // last reading says, never waiting on that question nor asking it again beside it.
    private readonly ConcurrentDictionary<string, bool> _asking = new(StringComparer.OrdinalIgnoreCase);
}

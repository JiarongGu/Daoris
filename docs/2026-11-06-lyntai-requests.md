# What Daoris's agent providers still need of Lyntai's agent layer (one quest's text, 2026-11-06)

**Standing:** the text of one quest from Daoris to Lyntai, written at AGENTLYNTAI1a (D248) from the agent-lyntai design's §8
(`docs/2026-10-30-agent-lyntai-design.md`). **Not published:** the steward publishes it after reading, and records its receipt on the
rows that wait. Nothing here is written into Lyntai, and nothing here proposes a change: each item states a need and its evidence.

---

**To Lyntai, from Daoris.** Daoris now takes the agent-provider layer from a pack of Lyntai's main line, as your D235 describes: a
host's own pack of a named commit, in a folder it keeps untracked. Daoris asks for no release; your own process makes your published
versions work. Read at `master` `8aacfb8f` (2026-11-06) against the archive's Parts 381 to 445.

**Already answered, with thanks.** The usage reading's version policy (AG24, your D236), the allowance as stated (AG23, D237), and the
exchange's order with the server's folder and client name (AG21, AG22, D238) answer everything Daoris's account-window move waits on.
AG10 to AG19 are built; Daoris reads AG12's refused model, AG15's extension seam and AG19's approver when its moves reach them.

**What remains.** Twelve needs, numbered by Daoris's design. Your records already use LQ16 to LQ20 for Daoris's memory requests (D239 to
D241), so these are **A-LQ** numbers. Each says what Daoris's moves wait on it: a move takes a door onto the layer only when a release, or
here a pack, carries everything Daoris does on that door today, shown case for case; until then the door stays on Daoris's own transport,
so nothing breaks while a need is open. Daoris paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver/`; Lyntai paths are under
`src/`. The design's §8 table holds each citation at the line it was read.

## For Claude Code's driven sessions (Daoris's move 1d waits on all of these)

**A-LQ4. A permission mode carried as the agent's own.** An application that lets a person choose how freely an agent acts needs the door
to carry the agent's own permission mode in place of the read-only or write intent, and to refuse, before a spawn, a mode it cannot
carry. *Evidence:* Daoris's three postures and the driven run's modes (`default`, `acceptEdits`, `bypassPermissions`), `Agents/Contract/
ProviderParts.cs` and Claude Code's adapter; the door's `AgentToolPolicy` has two intents, write is `acceptEdits` and skip-all replaces any
mode (`Lyntai.Providers.Basic/ClaudeCli/ClaudeAgentArgs.cs`); the protocol door sets no mode. A second half may follow: the door always
denies `AskUserQuestion`, `ExitPlanMode` and `EnterPlanMode` (`ClaudeAgentArgs.cs`, `AlwaysDenied`), which Daoris's driven start does not;
Daoris is measuring whether its driven start should keep them, and will ask then for the host to state the set a headless turn denies.

**A-LQ5. Effort carried, or refused.** An application that sets how hard an agent thinks needs the door to carry the effort a turn asks,
or to refuse it. *Evidence:* Daoris passes `--effort` from a role's choice (`Roles/Domain/ActorRun.cs`); `AgentChoice.Effort` exists in
the pool's models (`Lyntai.Core/Agents/AgentPoolModels.cs`) and no door carries it (`AgentSessionOptions` has no effort).

**A-LQ6. Plugins handed, or refused.** An application that composes plugins for a CLI agent's session needs the door to hand them, or to
refuse. *Evidence:* Daoris hands `--plugin-dir` per plugin (`Agents/Contract/Ports.cs`'s `HandPlugins`,
`Agents/Providers/ClaudeCode/ClaudeCodeSpawn.cs`); `ClaudeAgentOptions` carries a settings file, a
server file, allowed tools, skip-permissions, setting sources and strict servers, and no plugins.

**A-LQ7 (the rest). A child's and a task's life, without re-reading vendor frames.** A consumer keeping a transcript and a lineage needs a
child's identity, words, start and end; a task's life and the turn it wakes; a replay told from live; and an unread frame kept as a
bounded diagnostic. *Answered already:* AG10's `ParentCallId` on a subagent's calls and results, and AG18's `AgentTaskStarted` and
`AgentTaskEnded`. *Open:* a subagent's text and thinking are dropped (AG10's own note), a child's start and end are not events, the turn a
task's notification wakes is not told, a replayed frame reads as a live one, and a frame the reader does not know leaves no trace.
*Evidence:* Daoris's native readings of these (`Agents/ClaudeStreams.cs`, `Agents/AcpSubagent.cs`, `Agents/AcpReplay.cs`); the closed
event set in `Lyntai.Core/Agents/AgentStreamEvent.cs`.

**A-LQ11 (its access half first). Why a hold needs a person, and what kind of access failed.** An application that tells a person what
to do about a hold needs the owner action's reason (refill, sign in, grant a permission, change a configuration); and, for an access
failure, whether the account must sign in again or its credential was refused, with whether a field the door defines stated it or its
words implied it, the two kept apart and never one winner. *Evidence:* `AgentFailureKind.Access` and `RecoveryKind.OwnerAction` carry no
reason (`Lyntai.Core/Agents/AgentFailure.cs`); Daoris chooses a code before sign-in words (`Agents/AgentErrors.cs`), while its conclusion's
and conversation's sign-out readers act on the words alone, and its refused open tells the two apart (`Agents/RefusedAccounts.cs`,
`Agents/SignedOutAccounts.cs`). The whole of it also serves Daoris's isolated-home stage (below).

**A-LQ16. A limit's window, what was hit, the reset as stated, and the allowance reports as they come.** A host that keeps an account's
allowance by window needs, from the agent's own stream: the window a limit names (the vendor's name for it, where the door states one)
and what the limit said was hit; the allowance reports the stream makes while a turn runs, allowed as well as rejected, each window's value
with its precision, its reset, the report's standing, and the overage and credit state it names; and a reset as it was stated (an absolute
moment, a date and time, a weekday and time, a time of day, a relative wait), its zone as named or assumed, read against a clock the host
hands, and an unresolved zone returned with its stated parts, never dropped whole. A host's naming and its policy for an omitted date stay
the host's. *Evidence:* `AgentRestriction` names no window and nothing hit, and `RestrictionRecovery` is a kind and an instant alone
(`AgentFailure.cs`); the Claude reader keeps a `rate_limit_info` frame only when rejected (`ClaudeCli/StreamJsonAgentReader.cs`) and is
built with no clock (`ClaudeCli/ClaudeAgentSession.cs`); an unresolved zone returns null (`Lyntai.Core/Inference/StatedRecovery.cs`).
Daoris reads every window report into its walk and keeps the weekly windows (`Agents/AccountReadings.cs`, `Agents/HarnessCapture.cs`),
and reads a time of day a minute behind as tomorrow's, an unresolved zone in the machine's zone marked assumed, and grace on a dated reset
alone (`Agents/AccountLimits.cs`), where the library reads the same time as today's.

**A-LQ17. A turn's own report, and the session's start as the agent announced it.** A consumer that keeps a transcript and an account's
usage needs each tool call the agent's harness denied, with the tool and the reason; the context window the agent names, and on a protocol
door the context figures its updates report; and the turn's tokens by the model that spent them, a subagent's and the harness's own kept
apart from the session's model. A host that resumes a conversation needs the session's start as the agent's own initialization announced
it, told from a later frame that merely carries the id, the first id so announced kept. *Evidence:* Daoris reads `permission_denials`,
`modelUsage` and `contextWindow` from the native stream (`Agents/StructuredOutput.cs`) and `usage_update`'s figures from the protocol door
(`Agents/AcpMeasure.cs`); the layer's events carry none of them and `UsageFinal` holds one remembered model
(`Lyntai.Core/Agents/AgentStreamEvent.cs`); the Claude reader yields `SessionStarted` for any `system` frame with a new id, by design
keyed on the id (`ClaudeCli/StreamJsonAgentReader.cs`), where Daoris names a conversation by `system/init` alone (`Agents/NativeInit.cs`).

## For the protocol doors (Daoris's moves 1e-i to 1e-iv wait on these, and on A-LQ4, A-LQ7, A-LQ16 and A-LQ17's context half)

**A-LQ3. A run kept across turns, each prompt's own delivery, and an opening the host may repeat.** An application that converses with an
agent needs a run kept across turns: a word taken while a turn runs, a stop that keeps the conversation, a model re-asked on the same
session, background work read until it ends, and a permission request that waits for a person as long as the host holds it, the wait never
read as the agent's inactivity. For each prompt it needs what its own turn showed after it was written: whether the write completed, and
which of its turn's events came after it, by kind, the session's start never counted toward it. And it needs an opening operation the host
may repeat alone after a transient refusal, its handshake not run again and no prompt sent, the library never repeating it itself.
*Evidence:* one `StreamAsync` call is one turn (`Lyntai.Core/Agents/IAgentSession.cs`); `AgentDelivery.Taken` counts a session start
(`AgentFailure.cs`); AG19's approver is refused at half the turn's inactivity window (`Acp/AcpAgentReader.cs`), where Daoris holds a
request for the person until they answer (`Agents/AcpPermission.cs`, `Agents/AcpWaits.cs`); Daoris's protocol queueing and held native
stdin are built (`Agents/AcpSteering.cs`, `Agents/DoorInput.cs`); its open retry repeats the raw operation (`Agents/AcpTry.cs`,
`Agents/ProtocolOpen.cs`), and a conversation's open is never repeated. The native half (below) waits on the same need.

**A-LQ8 (the rest). What the open offered, accepted and refused.** A consumer offering models needs the open's offered models and options,
what it accepted and what it refused, as a reading apart from reported usage. *Answered already:* AG11 carries a model by the agent's
model option or ends the turn, the agent's answer deciding. *Open:* what was offered reaches the host only in a diagnostic's words.
*Evidence:* Daoris's negotiated offers (`Agents/AgentOffers.cs`, `Agents/DoorOffers.cs`), read from the open and shown to the person.

## For Claude Code's conversations and the native stand-ins (moves 1f-i and 1f-ii)

A-LQ3's native half (above), and:

**A-LQ13. The CLI turn's rules over a line reader the host supplies.** A host whose agent's lines the library does not read needs the
CLI turn's rules (exactly one terminal, a fault made the terminal, the delivery stamped from what the turn showed) over a line reader it
supplies, for a program it starts as the library starts its own, the reader told when the stream ends and with which exit code so it
can yield its terminal there. *Evidence:* the loop every CLI door shares is internal (`Lyntai.Providers.Basic/CliAgentLoop.cs`), and a
stream ending with no terminal reads fatal whatever the exit; Daoris's stand-in programs write plain lines with no terminal, their success
their exit (`Agents/Providers/Stub/`, `Agents/Providers/AltStub/`).

## For Daoris's isolated account homes (its infrastructure design's stage B)

**A-LQ11** (whole, above), and:

**A-LQ12. The restriction rules without the walk.** An application whose own planner selects, and which runs several processes on one
store, needs the restriction rules as operations pure over a snapshot the host holds and stores: a failure observed as a hold, restated at
a higher revision when its attempt fails; admission over a seat's covering scopes, a door's allowance among them, the reservation committed
against the complete applicable state; a probe lease with its token, a report by that token resolving the lease only while it still owns
it; a clear by id and revision; and an evidence observation with no attempt (a status report, a credit or budget check, a bounded status
probe) naming the hold's identity, revision and subject key, discharging only the recovery kinds that evidence answers, a stale,
wrong-scope or other-subject observation discharging nothing. A hold's identity must never be reused after its removal. *Evidence:* the
reducer is pure and internal (`Lyntai.Core/Agents/AgentPoolReducer.cs`); the pool's store is optional and its last writer wins
(`AgentAccountPool.cs`); a hold id built from scope and cause restarts at revision 1 after removal, so a stale clear matches a recreated
hold, read from the source and not run. Daoris's walk and cool-off are its own (`Agents/RosterWalk.cs`, `Agents/AccountCooling.cs`).

## Nothing waits on these

**A-LQ9 (the rest). A turn's own variables.** An application that starts each turn with variables of its own needs a turn's environment,
applied as a registration's is, a null removing one. *Answered in part:* AG17's `AgentAccount.Environment`, limited to the variables the
provider declares. Daoris's own variables per start (`Agents/Spawning.cs`) are outside that set; meanwhile Daoris registers once per start.

**A-LQ10. A built-in door constructed without a container.** An application that composes without a container needs each built-in door's
provider, its claims and usage source included, constructible directly. *Evidence:* `ClaudeAgentDoor`, `CodexAgentDoor` and `AcpAgentDoor`
are internal, reached through a registration; meanwhile Daoris builds a private container per start inside its layer folder.

---

**What Daoris asks back.** For each need: whether Lyntai takes it, declines it, or answers it another way, and, where it builds one, the
row. A declined need keeps Daoris's working path on that door and ends the move that waited on it; Daoris never builds the mechanism on
its side.

# One usage model: windows, spend and the rule that spends what lapses first (USAGEMODEL1)

Status: proposed, 2026-11-14. Source: brainstorm `2f044396` on the installed Daoris, both makers answering (Anthropic `g1`,
OpenAI `g2`, synthesis `s1`); nine groups, seven named by both. This document is the shape to build; the decisions it asks are
at its end.

## Why

On 2026-11-14 an account four hours before its weekly reset had 81% used, and the rest lapsed while three sessions ran on other
accounts. `AccountRotation.Rank` is a ladder: room and fewest sessions came before the lapsing week, and a ladder cannot trade one
against the other. The person set a manual order and curve to save it and had to remember to clear it. SPENDLAPSE1 patched one
rung (the last day of a weekly window allows everything). Readings were minutes old, and one signed-in account had never reported.

The model under the ladder is narrower than the providers: `UsageCurves.Length` knows two windows by name, spend is one ratio per
account with Claude's token weights for every agent, a ledger turn has no model or effort, and Codex's turns are not weighed.

## The model

1. **Windows are declared, not coded.** Each provider declares its windows as data: an id, a length, rolling or fixed reset, a
   scope (the whole plan or one model family), whether unused units lapse, and the plan's size where the provider says it. A turn
   draws on every window that applies to it at once. `UsageCurves.Length` and `AccountWindows.Week` read the declaration; the
   session/weekly switch and the lengths held in three places go. Paid credits are a separate balance with a price and no reset.
2. **One cost per account, one rule for starts and moves.** Among the ready accounts with room on every window the asked model
   draws on, the rule spends first the units forecast to lapse (valued by the useful work otherwise lost, so a lapsing unit costs
   nothing), then keeps strong-model capacity, then takes the least cost and the fewest moves. A running session moves only at a
   turn's end and past a margin. The same rule answers a start and a move, and the walk keeps saying its reason as a step.
3. **Strong-model headroom is computed, not a curve's knee.** Each start carries its model class; each account holds back its
   forecast strong-model demand until its nearest binding reset, and cheap-model starts spend only above it, unless those units
   would lapse. This replaces the smart curve's fixed shape and reads the model-scoped windows the ledger now drops.
4. **One evidence stream.** Provider snapshots and per-turn deltas go into one reading record with its time, source, coverage and
   confidence. A provider total anchors a window; only deltas it does not cover are added on top. Readings are taken at sign-in,
   start-up and reset, and more often near a lapse. A reader that never answered, or a stale one, is a state shown on the account,
   never room.
5. **Spend rates are learned, and rank only once proved.** Every ledger turn records its model, effort, door and token kinds, and
   a running total becomes a delta only inside a verified counter. Rates are fitted per plan, model and effort inside one reset.
   Each provider read is first predicted and the error kept; a forecast may order the walk only while its error is under a bound,
   else the last reading stands.
6. **Overrules are dated constraints.** A pin, an avoid, a cap, a hold-back or an order is a record with a scope, an author and an
   end (a reset, a time, one run, or a clear), applied before cost and said as a step. Nothing the person sets is left standing to
   be remembered. A role states needs (a model, may wait, must not move), never an account.

## The migration, one way per job

Each step before the flip changes no choice, so it reverts cleanly.

1. The ledger records model, effort, door and the per-turn delta, and each reset's lapsed units.
2. Providers declare their windows; `UsageCurves` and `AccountWindows` read the declarations.
3. `UsageProjection` grows into the forecast, with its error kept against each provider read.
4. The new rule runs in shadow beside `Rank`, its choice logged at every walk; it is replayed on 2026-11-14's history.
5. The flip: `Rank` is replaced once the shadow spends what lapsed that day and strands nothing.
6. The old rungs go: `Lapsing`, `LastDay`, the smart curve's knee and SPENDLAPSE1's rule. The order and the curve become kinds of
   overrule.

What stays: `AccountWindows`, `UsageLedger`, the provider readers, and the walk's steps and words (`Sequence`, `Keep`, `Spent`,
`Chose`, `Next`).

## Later, and only on their own evidence

- **Moves as transactions** (reserve the target, keep the source until it is ready): rests on checkpoint and resume abilities no
  door has shown yet; measure them first.
- **Several homes on one account**: provider reads already bound what another home spent within the read interval; a shared
  ledger and reservation authority waits until two homes actually share an account.

## Read before building

The brainstorm's packet cut `AccountRotation.Rank`, the switch decision, the provider readers and decisions D130, D165, D166 and
D248. The builder reads those first, and what each provider reports of plan size, model-scoped windows and credit balance.

## Decisions for the person

Each has a recommended default in the person's direction: Daoris uses the accounts fully and never limits an agent.

| Choice | Recommended default |
|---|---|
| Paid credits | **Off**: never drawn to save a lapse; a policy per scope (off, finish running work, up to a weekly budget) can turn it on. |
| Strong-model reserve | **Computed** as above, with no fixed share. |
| Overrule lifetime | **Until the next reset** of the window it was set for, unless the person names a time or one run. |
| Uncertainty | **Readings win** until a forecast proves itself; an account with no reading is shown, and never counted as room. |
| Shared homes | **Not built** until two homes share an account. |

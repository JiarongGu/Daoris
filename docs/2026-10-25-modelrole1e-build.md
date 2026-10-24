# MODELROLE1e build record

Quest `#1d2f0395f34a`, D205 row e (*second opinions, helpers, partners and brainstorms take their models*), on main after MODELROLE1b,
1c, 1c2, 1c3 and 1d. The first session on it was cut off at its account's limit with one file edited; this record says what was built
and how it was proved.

## What is built

1. **Second opinions over entries** (D205 §4.0, §4.4). `ReviewerChoice.ChooseAsync` expands each reviewer the rule or the ask names into
   the Second opinion actor's entries for that agent, in their order, else one entry at account default (`rule-default`); the passes
   (another maker first, then an undeclared maker, then the same agent) keep the rule's order. A model never makes a reviewer
   independent: the same agent on a stronger model is labelled `same-agent` and its sentence says *fresh: opus*. A model an account does
   not offer (the agent's reading, `AgentOffers`) passes that entry on that account, `not-offered`, and the agent's next entry is walked;
   an agent held as a whole (not installed, cooling, signed out, its pin) holds each of its entries alike. A rule of `false` stays off,
   and a stored actor that does not read chooses nothing, saying so. The recheck takes its first pass's record's asked choice, not the
   actor as it stands (§4.2).
2. **Helper and partner over entries.** `HelperChoice.Order`: the ask's agent or the person's list decides who, each taking the
   Helper actor's entries; with no such list and the actor *set*, its entries replace every agent of another maker, the asker's own
   family among them if listed, and another maker than the asker's still comes first. `PartnerChoice` walks the agent the ask names,
   its other accounts and then its other entries, never another agent.
3. **The pass carries the entry.** Each one-turn pass puts the entry's `Asked` and `Choice` on its target, so the record keeps what was
   asked and the opening note's actor line and the machine log's `actor.chose` say a later entry (`OneTurnReask.Say`).
4. **Each one-turn owner's refusal at the open, handled in its own call** (§3.5(f)). `OneTurnReask`: a refusal of the entry's model at
   the open is kept as the account's reading, the owner's own walk is asked again with the refused (entry, account) passed (an
   agent under its own sign-in passes the entry whole), a later entry of the same agent on the same account is set on the open session,
   and anything else ends the refused record `choice-not-offered`, gives its count back (`Release`) and opens the walk's answer in the
   same call: the second opinion's gate is told the replacement (`Replaced`), the helper's `started` state is written again naming the
   replacement session (`Opened` called twice), and the partner's state names the replacement's account. With none left each owner
   ends as it always has (no reviewer could run, `not-started`, refused) with the pass reasons.
5. **The brainstorm's frozen voices** (§4.4). `Generators` plans the voices once: the request's and the policy's agents decide who, each
   taking the actor's entry for it, else account default; with none named, each entry of the actor is one voice. A voice's turn asks its
   model and effort, its pass record keeps the choice and what was asked, and the synthesis takes its generator's asked choice. A voice
   whose agent no account of offers the model is dropped with `not-offered`, and a refusal at the open is its one bounded restart on the
   agent's next account with the refusing account passed; never replaced by another entry or agent.
6. **The hand-over's order is the actor's** (§3.5(e), round 4's Opus N1). `HandOverOrder.Steps` takes the actor's effective order, Build's
   for a driven line and Chat's for a conversation: the entries whose agent is a present door on another tool, each with its model and
   number. The roster's order (`Others`) stands only where the actor is *as before*. An agent's ask under `on` takes the first such
   step that can take the session (`DriverHandOvers.NextAgentAsync`); a quest handed on starts on `Effective(..., handed:)`'s model. The
   page's `nextChoices` is its twin, and `fixtures/hand-over-order.json` gained `actorRows`, read by `HandOverOrderTests` and
   `handOverOrder.test.ts`. The page's offer names the choice (*Build's next choice is 2: Codex · gpt-5.*) and the hand-over's ask lists
   the actor's order first. Both stay the person's press: nothing is handed over by itself under `ask` or `on`.

## Proof

- `ReviewerChoiceTests` (another maker first on the actor's model; the same agent on a stronger model labelled fresh and not
  independent; a reviewer with no entry at `rule-default`; `false` still off; a model not offered passing to the agent's next entry; a
  cooling agent asked once; an owner's passed (entry, account) left out; a named agent never walking another).
- `PartnerConfigurationTests`: two choices of the partner actor, one admission digest.
- `BrainstormLookTests`: voices from the actor's entries with their models, a voice that cannot run dropped with nothing in its place,
  a refusal at the open restarted once on the next account, and the request's agents deciding who.
- `HandOverAtTurnEndTests`: an ask under `on` going to Build's next entry on another agent, not the roster's; a cooling Build handing
  nothing over by itself under `ask` and `on` alike, and the person's press kept under either.
- `HandOverOrderTests`, `handOverOrder.test.ts`, `handOverOffer.test.ts`, `HandOverOfferLine.test.tsx` and the head's stories.
- `OneTurnReaskTests` (the seam the three owners share), and, with real processes on a stand-in ACP agent refusing `opus` on
  `account-1`: `HelperProcessTests` (the ask answered once, by the replacement on `account-2`, its state naming it) and
  `ReviewTreeTests` (the second opinion read once, by the replacement; the gate told it).

## Not built here

- **The partner's refusal at the open has no real-process proof.** It is the same seam (`OneTurnReask`, `PartnerPassAsync`'s loop around
  `PartnerAttemptAsync`) and is proved at the seam and by `PartnerChoice`'s walk; a Process case needs the built launcher and a strict
  job, which `PartnerProcessTests` lays out only on Windows.
- **No editor for the four fenced actors' models yet.** `daoris driver actor opinion|helper|partner|brainstorm` sets them; the page's
  *Actors* rows show them but edit only Build, Intake, Ask Daoris and Chat (`EDITABLE` in `Actors.tsx`). Row h's Ask Daoris proposals and
  the suggestion (row g) are the later doors.
- **A conversation's hand-over still opens at account default.** The new part's model on a hand-over to another agent is MODELROLE1d2's
  briefed-move seam; the offer and the order name the choice, and a driven line's start takes the model.
- **The installed proof**: a second opinion's *Details* naming Codex's reported model, which no branch can give.

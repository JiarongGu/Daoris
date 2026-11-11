# One way to ask the person: the press, its card a typed view of the route (ASKFULL1g, D253 point 5)

**Status:** proposed design, settled in D261; its first row (g-i) is built with it. **Area:** roles-and-supervision, rules 55-61.

## 1. The defect

Ask Daoris in restricted mode can ask the person for one change in two ways, and the room tells it to choose (*press it with the
command or propose it with the tool for its kind*, `HelpRoomMayDo`):

| | The proposal (16 kinds) | The asked press (ASKFULL1a) |
|---|---|---|
| Door | a `<kind>_propose` tool of the service, written as a file in the home's box (`HelpProposalBox`, `Help<Kind>ProposalWriter`) | a `daoris` verb over the pipe, proved by the delegate key (`PipeDelegate`) |
| Judged | by the kind's class (`Help/Proposals/`), the route's code against the machine now, before the person sees it; a refusal never reaches the person | by the route's grounds (`PipeGrounds`), read at the ask and again at the Confirm; the route itself judges when it runs |
| Shown | in the conversation's panel, a typed card per kind (before and after, the terminal twin), *Apply* and *Not now* | on the window's Confirm card, every value by its JSON pointer, a sentence by role (`PipeWording`), naming Ask Daoris |
| Applied | through the screen's door (`IHelpDoors`, `HelpScreenDoors`) | through the route itself, the one a terminal and every session presses |
| Outcome | `Daoris: Applied …` said to the conversation | the verb's line answers; `daoris press result` reads one no line waits on |
| Grants | none: a proposal always waits | a family of acts the person granted runs with no card (`ActFamilies`, rule 57) |

Every kind but `go` has a verb today (`setting` the `place`, `settings`, `quest` and `git` verbs; `ask`; `agent`; `delete` the
`quest`/`ask delete`; `account` the `agent account|settings` verbs; `plugin`; `browser`; `sync` `tree sync`; `repository`
`repository wire`; `session`; `pause`; `clear` `history clear`; `workflow`; `setup` `place`; `role`). So the same change has two
doors, two judges, two cards, two outcomes and two grant models: D228's defect.

A third finding (option C below): configured access's *housekeeping* and *chosen settings* are tools of their own
(`housekeeping_clean_up`, `housekeeping_discard_branch`, `settings_notify|language|cap|cooloff|quiet`) that act with no card, beside the
families of acts that let the same presses (`daoris settings notify …`) run with no card. A grant has two shapes.

## 2. The options

**(A) The press is the one way.** Ask Daoris presses verbs in every tier; the Confirm card is the one place the person says yes. A
proposal kind becomes the card's typed view of its route: judged at the ask by the route's own code (the kind's judge, re-keyed by
route), a refusal answered to the terminal at once and never shown, the view's facts worded by the page's catalogue, and the command
the press is. The `*_propose` tools, the box, the kinds, the screen doors and the panel's card go.

**(B) The proposal is the one way.** A delegate press in restricted mode becomes a proposal card. Rejected:
- Kinds exist only where one is wanted (rule 60); a press of any other verb would need a generic proposal, which is the Confirm card
  under another name, so two cards stay.
- A working session's asked press (D253 point 6) and a terminal's press keep the Confirm card, so one job (a press waiting on the
  person's yes) has two cards by who asks.
- Full access presses verbs; restricted would propose. The modes would differ in more than who says yes, against D253 point 5.
- The pipe would translate each route and payload into a kind's file: a second spelling of every act, kept in step by hand.
- Families of acts grant presses; proposals have no grant, so configured access would need a second grant model.

**(C1) Keep both doors, one card.** A `*_propose` tool writes a press onto the pipe. Rejected: two doors to one act are two ways
(D228 counts doors), and an MCP tool spelling every verb again must track the command tree by hand.

**(C2) A's grant half.** The housekeeping and chosen-settings tools become families of acts on the press, so a grant is one shape: a
family moves its presses from asked to allowed (D253 point 5). Taken as part of A.

## 3. Settled: A, with C2

Why the press:
- It is already the one door of every other asker (a terminal, a working session, a supervisor's allow, full access), so the tiers
  differ only in who says yes (D253 point 5): full access Ask Daoris's yes, a granted family the person's standing yes, restricted
  the person's on the card.
- Its card already re-reads what the press rests on at the Confirm, survives a restart in its record, and is bounded and attributed
  (D156); a proposal file has none of that.
- The command tree is declared once and held equal across programs (D228's ONEWAY1 note); a verb gains a flag once for every asker.
- What the proposal had that the card lacks is a view, not a door: the judge before the person sees it, the typed before and after,
  the terminal twin, and the outcome said to the conversation. Each moves onto the press (§4).

### What is removed (nothing kept twice)

| Where | Goes |
|---|---|
| Service | each `Help<Kind>Tool` (`*_propose`) and `Help<Kind>ProposalWriter`; then `HelpProposalBox`, `HelpProposalFile`, their tests, the call table's rows; the `housekeeping_*` and `settings_*` tools (C2) |
| Driver | each kind under `Help/Proposals/` (its judge moves to its route's view), `HelpProposalKinds`, `HelpProposals`, `HelpAppliedAgain`, the room's *What you may propose* section; the brainstorm's list of the tools |
| Modules | `HelpScreenDoors` (`IHelpDoors`), `HelpProposalFacts`, the bridge's proposal list, Apply and Not now routes |
| Web | `help/ProposalCard.tsx`, `help/proposal/*` (`RulesChange`, `SupervisorChange` move to the Confirm card's views), the panel's proposal list, the proposal words in both catalogues |
| Records | the home's proposals folder, cleared by its store's remover once the last kind goes (no proposal is read after) |

`go` stays: it asks no yes and changes nothing, and no verb moves the window. It leaves the proposal box in the last row for a link
of its own in the conversation (§5, g-ix).

## 4. The shape

**A route's view.** A route the person presses may declare a view: a unit of its own (`IPressView`, in `Modules/Platform/Views/`,
registered in `PressViews.All` by one line, keyed by module and route) that reads what it needs through the grounds' world and
answers either a refusal sentence or a `PressView` (a kind, the facts its sentence names, and the command the press is, spelled by
`ShellWord`). It runs at the ask, after the grounds and before the record: a refusal ends the press as `not-allowed` with its
words, so the terminal (Ask Daoris) hears why and the person sees nothing, as a proposal's judge did. A view rides
`PipeConfirmation.View`; a press restored after a restart shows its values alone, since its record keeps what it rests on, not
its sentence.

**The card.** The page's Confirm card draws a view first: its sentence from `person.view.<kind>` in both catalogues, the facts as
values, and the command in a code span; every value the press carries stays below it (D156: the person still sees exactly what
runs). A route with no view is drawn as today. The kinds' own cards (`RulesChange`, `SupervisorChange`, the place set-up's
changes, the sync's rows, the plugin's commands) become the views of their routes.

**The outcome.** The verb's line answers Ask Daoris. A press of Ask Daoris's that its line no longer waits on is said in its
conversation once it settles, as Daoris's word (*Daoris: confirmed and done …* / *refused*), from the confirmations' `Settled` sink,
as an Apply's was; an ask or quest its answer names is kept with the conversation (`HelpPublished`), as an Apply's was.

**The grant.** A family of acts is the one grant (`ActFamilies`). Housekeeping becomes the families `clean-up` and `discard`, the
chosen settings the family `settings` over `settings notify|language|cap|cooloff|quiet`; the record's old lists convert through the
home's conversion door on the person's press (D228), and the tools go.

## 5. Rows

Each row moves its kinds' judges to their routes' views and removes those kinds whole (tool, writer, kind, door, card, room line,
call table) in the same change; the room names a kind's tool only while its row is open.

| Row | What |
|---|---|
| **g-i** | The seam: `IPressView`, `PressViews`, `PipeConfirmation.View` judged at the ask (a refusal never shown), the Confirm card's view with its command; the first view `REGISTRY_REWIRE`, and `repository_propose` removed whole. *Built.* |
| g-ii | The outcome: Ask Daoris's settled press said in its conversation where no line waits; a published ask or quest kept with it. Before g-vi. |
| g-iii | `session`, `delete`, `pause`: views of `SESSION_ARCHIVE|UNARCHIVE|DELETE`, the quest and ask deletes, the pause and resume routes; the three kinds go. |
| g-iv | `setting`, `browser`, `account`, `agent`: views of their verbs' routes (a setting's before and after, an account's order, an update's version); the four kinds go. |
| g-v | `role`, `plugin`: `RulesChange` and `SupervisorChange` as the role routes' views, the plugin's commands and grant as its routes'; both kinds go. |
| g-vi | `ask`, `clear`, `sync`, `setup`: the ask's review choice on `daoris ask` where the verb lacks it; the plan doors the card already has become those routes' views; the four kinds go. |
| g-vii | `workflow`: a press naming a draft the room writes, viewed as the design's card; after APPROVE1's finisher leaves `Workflows/`. |
| g-viii | C2: housekeeping and chosen settings as families of acts, the record converted on the person's press, the `housekeeping_*` and `settings_*` tools gone. |
| g-ix | The box goes: `HelpProposalBox`, `HelpProposals`, `HelpProposalKinds`, `HelpScreenDoors`, `ProposalCard`, the proposals folder cleared; `go` a link of its own; the room's *What you may do* names verbs alone; rule 60's doors table says *asks on the card*. *Built.* |

## 6. Not changed

The working sessions' own proposing tools (`permission_propose`, `handover_propose`, `memory_propose`) ask a role or a record, not
the person's yes to a press of Daoris's verb: other jobs, out of this design. Full access's presses run as before (rule 58).

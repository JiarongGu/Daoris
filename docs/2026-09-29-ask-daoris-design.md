# Ask Daoris — a conversation about Daoris itself (HELP1)

**Carried by:** HELP1 in `TASKS.md`. **Status:** design, written before the build, with the owner's
two calls made (§8, D89). The requirement is `docs/2026-09-28-after-the-first-workspace.md` §4. The owner,
setting the first real workspace's landing rule (2026-09-28): *"I think we will need some chat agent to
support configure for workspace"*.

## 1. What it is

A conversation with a session whose subject is Daoris on this machine: how to set up a workspace,
what a screen means, why a start was refused, how to drive a repository, write a rule, or start a
task. It is **a harness session like any other** (D24: Daoris calls no model), in a room of its own,
and it **changes nothing by itself**. Every change it wants is a proposal the person confirms, made
through the same door the screen uses, so what it can do is exactly what the person can do (D50).

## 2. The doors

- **An app-strip button** beside the palette, **the palette's *Ask Daoris***, and **F1**.
- It opens as **a panel at the application's right edge**, beside whatever view is in front of the
  person, and it stays open across views. *As built (HELP1d):* not a tab of Sessions' right dock, as
  first written, since that dock exists only inside Sessions and a panel there would close with a
  change of view. Whether it is open is this viewer's, remembered like the view.
- One conversation per machine, carried on across opens. *New conversation* starts again.

## 3. The room

`<home>/help/`, re-rendered at every open (the intake's rule, D65 §1b), never a repository and never
a `SessionTree`:
- **`AGENTS.md`**: what Daoris is, the doors table (§5), and the machine as it stands, rendered from
  the host's own answers: the workspaces and their repositories, what is drivable and held, each
  repository's line and landing rule, the agents and accounts with their sign-in state, and what
  waits on the person. Names and states, never a key.
- **`CLAUDE.md`** carrying `@AGENTS.md`, and **`.claude/settings.json`** allowing the family's read
  tools, the connector's `setting_propose` and `ask_propose`, and nothing else. Over the protocol door
  a request for anything unlisted is refused by construction (D52), which is the point: it reads, and
  it proposes.

## 4. Where the person is

The page hands the conversation what is on the screen, as a short preface to the person's message
whenever it has changed since the last one: the view, the workspace in scope, the attended session or
repository, and a sentence the screen is showing (a refusal, a parked card's question). It is chrome
the page already renders, so nothing new leaves the machine (D47 §4).

## 5. What it may propose

**A setting.** `setting_propose {door, target, value, why}`, where `door` is one of the driver
module's own setting routes, named as a person would name them:

| Door | Route | Terminal twin |
|---|---|---|
| drive · hold · own tree | `SET_DRIVABLE` · `SET_HOLD` · `SET_TREES` | `daoris driver drive|hold|trees` |
| line · landing | `SET_LINE` · `SET_LANDING` | `daoris driver line|landing` |
| intake · strikes · timeout · notify | `SET_INTAKE` · `SET_STRIKES` · … | `daoris driver intake|strikes|timeout|notify` |
| a rule | `RULE_ACTION` | `daoris agent rules …` |

The driver checks a proposal against the route's own validation before the person sees it, so a
proposal the route would refuse is refused to the agent, in the route's words, and never shown.

**Starting something.** `ask_propose {sentence, workspace}`: an ask, which the ordinary loop takes
(D65). The helper never runs a session's work, and never publishes a quest itself.

**Never**: a push, a merge, a discard, a sign-in, a key. Those stay the person's own presses where
they already are.

## 6. The confirmation

A proposal appears in the conversation as a card: what it changes, in the page's words, the terminal
command that does the same, the agent's `why`, and **Apply** or **Not now**. Apply calls the same
route the screen would, and the result, done or refused in the route's words, goes back into the
conversation as the person's next message, so the agent knows. A proposal is a file under the home
(`<home>/help/proposals/<id>.json`, PERM2's shape), so an unanswered one survives a restart.

## 7. With no agent named

D24: the useful part without a model. Ask Daoris opens with **starters from what the machine
lacks**, each a door to the screen that fixes it and its terminal command: no workspace, nothing
drivable, an agent with no account signed in, a session waiting on the person, a repository with no
line git can name. With an agent named, a starter is also a first message.

## 8. The owner's calls — made (2026-09-29, D89)

1. **Which agent and account it runs on**: *its own choice in Settings*. A third job under Settings →
   *Daoris's own AI*, with its own agent choice and the account each circle's default names, off
   until named, as the intake is.
2. **Whether a confirmation may be remembered**: *always confirm*. Every change shows its card, and
   nothing applies on its own.

## 9. Build order

1. **HELP1a**: the doors, the dock tab, the room, and the conversation as a chat in the room (the
   intake's plumbing, as a conversation rather than one turn). *Plan (2026-09-29):* the ledger opens
   a help session as the intake's is opened, a chat serving no quest in "repository" `daoris:help`
   (a colon no folder name holds), one running per room; `START_HELP` renders the room from the
   machine and starts the chat there on the helper's agent, or hands back the one already running;
   the panel shows the newest help session's conversation with a composer, and a message with none
   running starts one. *Built 2026-09-29, as planned*, with what building it settled:
   - **The room says what the machine holds from the driver's own answers** (`HelpRoom.Describe`):
     its file, the registry, each repository's line and landing rule, the roster. Names and states;
     no root, no profile's home, no key. Every terminal command in its doors table is spelled as
     the CLI's help spells it, and the timeout, which has no screen, says so.
   - **Its rules are the room's allows and the person's denies**, never the person's allows, which
     are for work in a repository; the connector is handed on both doors, and no plugin's server
     (a browser brought up for a tool it may not use would be a window for nothing).
   - **Its conversations are read across every workspace**: it belongs to none, and the scope a
     person works in must not hide the conversation beside it.
   - **A running conversation is carried on only where this process holds it**: after a restart,
     the one that ran is shown ended, and a message opens the next.
   - 🔴 **Its session asks for the agent's own asking mode** (`default`), never D81's `auto`. The
     first real conversation, on the scratch window, ran `grep` and `sed` over a checkout under
     `auto` to research its answer: the allow-list is not a gate in a mode that judges its own
     actions, and a helper that can run a command can run `daoris driver …`, a change nobody
     confirmed. Asked again in `default`, every read outside the room was refused, and it answered
     from the room. Its room now also says what a landing pattern may say, which it had to guess.
   - **Found on the same look:** the strip's door was a bare glyph, and the owner found no easy way
     to open the chat. It is a named button now, the name giving way at a narrow window.
2. **HELP1b**: where the person is, as the preface. *Built 2026-09-29:* one line, agent-facing and
   so untranslated (`help/where.ts`): the view or the Settings domain, the workspace in scope, and on
   Sessions the attended session with its state and up to 280 characters of what a parked one asks.
   The page sends it with a message only when it changed since the last one that conversation was
   handed. The driver hands the agent the preface, a blank line, then the words, on every door, and
   the record keeps it as a note, *told where the person is*, never as the person's words. A refusal on
   the screen is not carried yet: nothing holds one in a shape the page can hand over.
3. **HELP1c**: `setting_propose` and `ask_propose`, the validation, the card, Apply. *Plan
   (2026-09-29):* the connector's two tools write a file each under the home the driver names on the
   connector (`<home>/help/proposals/<id>.json`, PERM2's shape), shape-checked there and nothing more:
   the route's own validation is the driver's, which the service cannot run. So the driver checks a
   pending proposal with the route's code when the panel asks for them; one the route would refuse is
   never shown, and its refusal goes back into the conversation in the route's words. *Apply* makes
   the same edit the screen's route makes, and *Not now* dismisses; either result goes back into the
   conversation as the person's next message.
4. **HELP1d**: the starters, and the no-agent tier. *Built 2026-09-29:* waiting sessions, nothing
   registered or driven, a tool no account is signed in to, a repository with no line, and Ask Daoris
   with no agent, each with its door and its terminal command; the strip's button, the palette's
   *Ask Daoris* and F1 open the panel. Its agent choice (D89) is Settings → *Daoris's own AI*.

## 10. Not chosen

- **Running `daoris` commands in the room.** The installed application does not carry the CLI, and
  a helper that ran commands would be a second door with its own approval problem (PERM2's answer:
  it proposes).
- **A model call of Daoris's own** for help. D24: the deployment chooses a model, and a session is how
  Daoris uses one.

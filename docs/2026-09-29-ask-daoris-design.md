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
- It opens as a tab of the right dock (*Ask Daoris*, beside *Timeline* and *Review*), so it sits
  beside whatever the person is looking at, and it survives a change of view.
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
   intake's plumbing, as a conversation rather than one turn).
2. **HELP1b**: where the person is, as the preface.
3. **HELP1c**: `setting_propose` and `ask_propose`, the validation, the card, Apply.
4. **HELP1d**: the starters, and the no-agent tier.

## 10. Not chosen

- **Running `daoris` commands in the room.** The installed application does not carry the CLI, and
  a helper that ran commands would be a second door with its own approval problem (PERM2's answer:
  it proposes).
- **A model call of Daoris's own** for help. D24: the deployment chooses a model, and a session is how
  Daoris uses one.

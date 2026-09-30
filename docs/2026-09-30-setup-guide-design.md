# The setup guide: what a first start walks through (SETUP1)

**Status: the contract for SETUP1a–b, recorded as D97 before any code.** The owner, 2026-09-30:
*"we also will need a setup guide for first use daoris, so setup agent and agent for "daoris" (the
main agent for daoris system) and other rules"*, and the same day: *"we need to have a good ui/ux or
easy access for everything use ask daoris"*.

## 1. What is there

A fresh install opens on the Overview with nothing registered, and the pieces a machine needs are
spread over four places: Settings → Agents & accounts (a tool and an account), Settings → Daoris's
own AI (the agent Ask Daoris and the intake run on), the Workspace menu and Projects (repositories),
and Settings → Workspace and Driver (what is driven, how work lands, what agents may do). Ask Daoris
opens with starters from what the machine lacks (HELP1d, `help/starters.ts`), which is the right
knowledge in the wrong shape for a first start: it lists what is missing, not what to do in order.

## 2. The guide

**Get started** is the first domain in Settings, and a fresh machine opens on it. It is a list of
steps in the order a setup goes, each with its state read off the machine, the screen that does it,
and the terminal command that does the same (D50):

| Step | Done when | Its screen | At a terminal |
|---|---|---|---|
| 1. An agent | a tool is present and an account of it is signed in | Settings → Agents & accounts | `daoris agent install <agent>`, `daoris agent login <agent>` |
| 2. Daoris's own agent | Ask Daoris's agent is named (and the intake's, which is offered beside it) | Settings → Daoris's own AI | `daoris driver helper <agent>`, `daoris driver intake <agent>` |
| 3. A workspace and its repositories | at least one repository is registered | Workspace → Add repository / Import a folder | `daoris connect`, `daoris import <folder>` |
| 4. What is driven | at least one repository is drivable | Projects | `daoris driver drive <repository>` |
| 5. How work lands | each workspace with a drivable repository has a landing rule, and every drivable repository a line | Settings → Workspace | `daoris driver landing …`, `daoris driver line …` |
| 6. What agents may do | the rules have been looked at (optional, never blocking) | Settings → Permissions | `daoris agent rules` |

**The facts are the starters' facts.** One pure function reads the machine for both, so a step and
a starter never disagree; the guide adds the order, the done state and the optional last step.

**Ask Daoris walks it with you.** The guide's head offers *Set up with Ask Daoris*, which opens Ask
Daoris on a first message asking to be walked through the steps not yet done. Its proposals are the
same confirmed changes as ever (HELP1c): nothing applies without the person's press. With no agent
named yet, step 2 says so and the guide stands on its own (D24's no-model tier).

**When it opens.** A machine with steps 1–3 not done opens on it at start; once they are done, or the
person closes it with *Don't open at start*, it does not. It stays one click away: the Daoris menu,
the palette (*Set up Daoris*) and Ask Daoris's starters all lead to it. The status bar carries a small
*setup: 2 of 5* until the required steps are done.

## 3. Build order

1. **SETUP1a**: the facts shared with the starters, the Get started domain, its steps with their
   doors and commands, the menu and palette entries, both catalogues; looked at on the window with a
   fresh home (the scratch machine emptied), in both themes. *Built 2026-09-30*, with what building it
   settled:
   - **One reading, two consumers.** `help/machine.ts`'s `readMachine` turns the queries' answers into
     the machine; `starters` (unchanged in behaviour) and `help/setup.ts`'s `setupSteps` both read it,
     and `help/useMachine.ts` is the one organism that asks. A table test holds a step and a starter to
     the same answer on every machine it names.
   - **What each state reads.** Step 1 counts a sign-in as the roster does (SES3): only a definite
     `out` with no account signed in is *to do*, so an `unknown` never opens the guide on a tool that
     works. Step 5 is *done* only once something is driven and every driven repository has a line and a
     landing rule of its own or its workspace's; its door opens the lines card when a line is missing,
     the landing card otherwise (a new `landing` anchor). **Step 6 is optional and never done**: nothing
     on the machine records the rules being looked at.
   - **Nothing is drawn until the machine is read**: a step drawn before the roster answered would say
     *to do* of a machine that has it done, so the guide says it is reading until every answer is in.
   - **In a browser** only step 3 is listed, with its commands and no door (Add and Import are the
     desktop's), and a sentence says the rest is the desktop's; there is no count there.
   - **The doors**: the Daoris menu's first item, *Set up Daoris*, and the palette's *Set up Daoris*,
     both a browser's too; and Ask Daoris's starters end with *set up Daoris step by step: n of 5 done*
     while the required steps are not all done.
2. **SETUP1b**: opening at start, *Don't open at start*, the status bar's count, and *Set up with Ask
   Daoris*. *Built 2026-09-30*, with what building it settled:
   - **Opening at start is decided once, when the machine has been read** (`useSetupAtStart`): before
     the roster answers, a machine with an agent reads as one without. Whatever was decided, nothing
     reopens it that start, and a view the person chose before the reading settled is kept. The view
     it watches is the one chosen, not the one shown, since a remembered Sessions stands in as Overview
     until the shell answers. A browser never opens it: its steps are the desktop's.
   - ***Don't open at start*** is a checkbox at the guide's foot, kept in this browser's storage
     (`daoris.setup.atStart`), read and written through the guarded `stored`/`store`; refused storage
     reads as not chosen, so the guide still opens.
   - **The status bar's *setup: n of 5*** sits after the workspace and the remote, its mark in open's
     hue, leading to Get started; it waits for the reading, and goes once the five are done.
   - ***Set up with Ask Daoris*** opens the side bar on a first message naming the steps not yet done,
     numbered, the optional one marked, in the reader's language (they are the person's words). With
     no agent named, the head says step 2 comes first and offers no hand-off that could not run.
   - 🔴 **A question handed to Ask Daoris is let go once it is sent.** The side bar's Ask Daoris is
     drawn again with its tab, and Quick Ask's with its box, so an opening still held was asked again
     by every new drawing — true of the palette's question too since DOCK1d. The conversation says it
     sent one (`onOpened`, from its effect), the application clears it, and ids come from one counter
     so a cleared one is never reused.
   - **The room names the guide** (`Help.cs`'s doors table): the preface says *Settings → Get started*,
     and a helper that did not know the domain would guess at it, as it once guessed at a menu.

## 4. Not chosen

- **A wizard that steps through modal screens.** Every step is already a screen with its own
  controls; a wizard would be a second copy of each, and the two would drift.
- **Doing the setup for the person.** Signing in, choosing an agent and choosing what is driven are
  the person's own choices, and the steps lead to where they are made.

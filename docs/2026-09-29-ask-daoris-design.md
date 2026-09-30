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

**Where it lives, looked at again (the owner, 2026-09-29: *"the ask daoris need to be a better
location please refer to a better ui/ux design"*).** On the installed window the panel was a second
right column beside Sessions' own dock, at a fixed width, so the centre was squeezed between two side
bars. The references agree on one right region:
- **VS Code** opens the Chat view in the **Secondary Side Bar**, the one right region, next to the
  editor; reached from a Chat menu beside the Command Center or `Ctrl+Alt+I`; Quick Chat is a light
  box at the top, like the palette; the region toggles, resizes, and can move to the panel.
- **Cursor**'s AI pane is the same right side bar (`Ctrl+L`), movable to the panel.
- **JetBrains** AI Chat is a tool window on the **right toolbar**.

So: **one right region.** On Sessions, Ask Daoris is a tab of the right dock beside *timeline* and
*review*, sharing its resize, its close and its full; `F1` and `Ctrl+Alt+I` open the dock on it. It has
no button of its own on the strip (the owner: *"no ask daoris at top border bar"*, DOCK1c): the right
side bar's toggle is its door. Quick Ask (a palette-like box for one question) and moving the region
are DOCK1's: both built (`2026-09-29-dock-design.md` §4), Quick Ask on `Ctrl+Shift+Alt+L` and the
palette's last row. *Since DOCK1a* the same side bar is on every view, so Ask Daoris has one host: the
region on its own away from Sessions, with its own width, is gone.

## 3. The room

`<home>/help/`, re-rendered at every open (the intake's rule, D65 §1b), never a repository and never
a `SessionTree`:
- **`AGENTS.md`**: what Daoris is, the doors table (§5), and the machine as it stands, rendered from
  the host's own answers: the workspaces and their repositories, what is drivable and held, each
  repository's line and landing rule, the agents and accounts with their sign-in state, and what
  waits on the person. Names and states, never a key.
- **The window** (HELP2, 2026-09-29): how it is laid out, how a view moves between the side bar and
  the panel, the layout keys, and that the helper cannot see the window and asks rather than guesses.
  Added after it guessed at a menu that does not exist.
- **`CLAUDE.md`** carrying `@AGENTS.md`, and **`.claude/settings.json`** allowing the family's read
  tools, the connector's `setting_propose` and `ask_propose` (and since HELP6 its four further
  `*_propose` tools, §9.6, and since PLUG9 `plugin_propose`, §9.7), and nothing else. Over the protocol door
  a request for anything unlisted is refused by construction (D52), which is the point: it reads, and
  it proposes.

## 4. Where the person is

The page hands the conversation what is on the screen, as a short preface to the person's message
whenever it has changed since the last one: the view, the workspace in scope, the attended session or
repository, and a sentence the screen is showing (a refusal, a parked card's question). It is chrome
the page already renders, so nothing new leaves the machine (D47 §4). On Sessions it also says which
views each region holds and whether it is showing (HELP2).

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

**Every door built since** (HELP6, §9.6): an agent's update or pin (`agent_propose`), an account's
model and effort (`agent_settings_propose`), the delete of a quest or an ask made by mistake
(`delete_propose`), and a screen to open (`go_propose`), each judged by the rules of the screen that
makes the same change and applied through that screen's own route.

**A plugin** (PLUG9, §9.7): one that has landed, added from its folder in the checkout of the
repository that holds it (`plugin_propose`), or one installed here switched on or off. Making one is
never a proposal of this kind: it is an ask at that repository's workspace, since a plugin runs as the
person and is made with tests and reviewed like any other work.

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
   conversation as the person's next message. *Built 2026-09-29, as planned*: the doors are the
   twelve `daoris driver` verbs the room's table names (drive, undrive, hold, resume, trees, line,
   landing, intake, helper, strikes, timeout, notify) and an ask. The driver's check is the config's
   own edits, which throw the route's refusal, plus the names the routes do not check and a helper
   can invent: a registered repository, a workspace, an agent. **Not the rules**: the room is never
   handed `permission_propose`, since PERM2 applies a narrowing with nobody's press. On the scratch
   window, asked to make a repository land on feature branches, the helper proposed exactly that
   with its reason; *apply* wrote the file the screen writes, and it acknowledged the result.
4. **HELP1d**: the starters, and the no-agent tier. *Built 2026-09-29:* waiting sessions, nothing
   registered or driven, a tool no account is signed in to, a repository with no line, and Ask Daoris
   with no agent, each with its door and its terminal command; the strip's button, the palette's
   *Ask Daoris* and F1 open the panel. Its agent choice (D89) is Settings → *Daoris's own AI*.
5. **HELP5**: answering sooner (2026-09-30). Measured on the owner's install, a first word came 40 s
   after the conversation was made, over five model round trips, three of them Daoris's to remove:
   - **Its tools load with the first request.** Claude Code defers every MCP tool behind a search step
     unless `ENABLE_TOOL_SEARCH` reads false, so the helper paid a round trip to find its own tools. The
     switch is the adapter's to name (`ISessionAdapter.ToolsUpFront`, empty by default; both Claude Code
     doors name it), and only the room's conversation asks for it: a repository's keeps the harness's
     default.
   - **The room says it has no web either**, in the sentence that says it has no shell: its fourth move
     was a fetch of a ticket's URL, refused before it ran.
   - **The conversation opens as the panel shows**, so the spawn and `session/new` (3.2 s measured) are
     done before the person types: once per showing, again after *New conversation* once the one it
     finished has gone, never while one runs. One that is refused says nothing and is not asked again;
     the send asks for itself and says why, as before. **Nobody has spoken in it, so it is not shown**:
     the starters, or the conversation that ended, stay in front until the first words go to it, and
     words sent while it is still opening wait for it (HELP4's holding). Which conversation that is lives
     in the page's query cache, so the side bar and Quick Ask agree on it and a tab drawn again still
     hides it. *Not settled:* a conversation opened ahead is a record whether or not anyone speaks in it,
     so after a restart the newest help record can be an empty one, shown ended in place of the last real
     conversation.
6. **HELP6**: Ask Daoris reaches everything (2026-09-30). The owner: *"we need to have a good ui/ux or
   easy access for everything use ask daoris"*. Doors built since HELP1c were not reachable from it.
   *Plan:* four more kinds, each a connector tool writing the same file (§6), judged by the driver with
   the rules of the route that serves the same screen, applied through that route's own code:
   - **`agent_propose {action, agent, version?}`**: *update* where the roster's `updates` is `pin` or
     `tool`, *pin* to one exact release where the door is pinnable; applied as `HARNESS_ACTION`'s
     own start, one at a time, its end said into the conversation.
   - **`delete_propose {quest | ask}`**: only where the service's `deletable` says the record may go
     (D95); the card says what goes. Applied through the local host's `DELETE /api/quests/{id}` and
     `DELETE /api/asks/{id}`, the drawer's routes. Not D95's rejected *delete over MCP*: the tool only
     proposes, and the delete is the person's press.
   - **`agent_settings_propose {agent, account, model?, effort?}`**: an account whose tool's settings
     Daoris knows (D98), the values the tool's own, `max` refused; applied as `SET_AGENT_SETTINGS`.
   - **`go_propose {view, domain?, part?}`**: a *go* card, applied by navigating the page as the
     starters' doors do, nothing else changed; the places are a twin of the page's views, Settings
     domains, their parts and the setup guide's steps.

   *Built 2026-09-30, as planned*, with what building it settled:
   - **The apply is the driver library's, and each door is the module's code for the screen's route**
     (`HelpProposals.ApplyAsync` over `IHelpDoors`; `DriverModule.HelpDoors`). `HARNESS_ACTION`'s process
     start and `SET_AGENT_SETTINGS`'s write became one method each, which the route and the door both
     call, so the two cannot drift; a delete is `ServiceClient`'s call to the drawer's own route. Tests
     hold each door with a stand-in that records the call, in both test projects.
   - **Each kind is judged by its screen's rules, in that route's words.** An update: the agent is
     installed and the roster's `updates` names one. A pin: the door declares a package or a channel,
     and the version is one exact release (the channel's own `RefuseVersion`; for a package, an exact
     semver, since npm would take a pointer). A delete: the service's own `deletable` on every quest
     and ask, closed ones included, read only when a delete is pending; a refusal says what to do
     instead, as D95's does. An account: a tool whose settings Daoris knows, one of its owner's accounts,
     and the values through `AgentSettings.JudgeModel`/`JudgeEffort`, so `max` is refused in its words.
     A go: `HelpPlaces`, the twin of the page's `help/places.ts`.
   - **An agent action's end is said into the conversation after what the Apply did**, however soon it
     comes: a pin already installed ends before its start is answered. The page's Agents screen follows
     the started action (`HarnessRun.follow`), so its console and its end show there too. A busy slot is
     the route's refusal and leaves the card for another press.
   - **The room lists the asks by id** (the quests are the family's `quest_list`), says the rule each
     kind is judged by, and lists every place a go may name from the driver's own table.
   - **Left out:** a per-model effort (`--for <model>`), which the screen sets only where the file
     already holds one; *unpin*, *install*, a sign-in and an API key, which stay the person's presses;
     and a go to a place inside a view other than Projects' two drawers, since nothing else there has
     a door that opens it.
   - **What the gates do not cover:** a real helper choosing these tools, and the cards on the window
     in both themes; both are the owner's look on the installed window.
7. **PLUG9 (a) and (b)**: Ask Daoris makes a plugin by asking for it, and installs one by a press
   (2026-09-30). The owner asked that Daoris make plugins itself, through Ask Daoris; the reading in the
   backlog is that a plugin is code running as the person, so making one is work and installing it is
   the person's press. It fits HELP6's shape, so it needed no decision of its own. *Built:*
   - **The room says how a plugin is made** (*Making a plugin*): as an ask (`ask_propose`) at the
     workspace of the repository that holds plugins, addressed to it, saying what the plugin does and
     the point it speaks on, each point named from `HookPoints` with what it is for; the session there
     makes it with its tests. The helper finds that repository by what the registry says it owns, and
     with none the person decides where plugins live. It never writes one and never proposes adding one
     that has not landed. The room lists the plugins installed here by id and state.
   - **`plugin_propose {action, repository?, folder?, id?}`**: `add` names the folder from a registered
     repository's checkout root (a whole path only where the person gave one), `enable` or `disable` an
     installed id. The service checks the shape, `..` and a whole path within a repository included.
   - **Judged by the catalogue's own reader** (`PluginCatalog.ReadAsWritten`, `RefusedByThisBuild`,
     through `PluginInstall.Read`): a folder with no sound manifest, a harness this build carries, a
     folder inside or holding the Daoris home, one outside the checkout it names, a repository with no
     checkout here, and an id already installed are refused; a switch needs an installed id not already
     where it would put it.
   - **The card shows what will run before Apply**: the id, the command it starts with `${plugin}` as
     written, its points, the agents it declares and the servers it hands every session, and for an add
     that the folder is copied in and nothing starts at the press. The terminal twin is `daoris plugin
     add <folder>` or `daoris plugin enable|disable <id>`.
   - **Applied through the door the screen or the terminal uses**: an add is `PluginInstall.Add`, the
     driver's twin of `plugin add`'s copy, which never replaces an installed plugin; a switch is
     `PLUGIN_ACTION`'s own row, one method the route and the door share. Neither starts anything.
   - **Left for PLUG9 (c) and (d)**: remembering where an added plugin came from, to update it, and the
     install's example plugins offered as cards.
   - **What the gates do not cover:** whether the folder's contents have landed on the repository's line
     (the checkout is copied as it stands), a real helper choosing the tool, and the card on the window in
     both themes.

## 10. Not chosen

- **Running `daoris` commands in the room.** The installed application does not carry the CLI, and
  a helper that ran commands would be a second door with its own approval problem (PERM2's answer:
  it proposes).
- **A model call of Daoris's own** for help. D24: the deployment chooses a model, and a session is how
  Daoris uses one.

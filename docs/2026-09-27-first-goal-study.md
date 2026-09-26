# The first goal — a real workspace, a ticket, a task started

> Written 2026-09-27 from the owner's direction: *"I think we still does not meet the first goal:
> setup [the named workspace] as workspace and use mcp to control chrome with jira to read ticket and
> start task (and does not need to locate which repo just start the task in daoris) and also plugin
> system, and deepseek-harness top plugins reference too, also deepseek-harness and orca reference
> also lyntai 3.5 is released and it supports doc based storage this might help to build some
> knowledge db? or sqlite is better for local app (docs based db might help for repo itself)"*.
> D65 was designed from the same goal and built as INT1–INT5. INT6, running it on the real
> workspace, never ran. This records what that run would have met, read from the code and the
> machine, and what the references do about it. All three references were read on this machine and
> nothing in them was written. The decision is **D77**.

## 0. The workspace, and what "the first goal" asks of it

The named workspace is a folder of 29 git repositories and a documents folder. It holds Angular
and React front ends, .NET back ends, Azure functions, a schema, an importer, an Android app and
some Terraform. **None of them has adopted.** The person gives a ticket's URL and a sentence, and
expects the change started in the repository that owns it. They never say which repository, and
never open one.

## 1. What INT6 would have met

Three walls in the code, then three steps that are setup rather than code.

1. **Nothing in the workspace could be chosen.** A declaration counted only for an adopted
   repository: `Registration.Registered` requires `Adopted`, the declarations tier ranks adopted
   rows only, and the intake's room listed every other repository as *"no declaration can make it
   the owner"*. The instruction then said that when nobody declares it, the intake publishes
   nothing. Every ask on this workspace would have parked. D65 §0's promise, *"without saying
   which repository"*, held only for a family that had adopted, and adopting 29 repositories means
   writing into 29 repositories.
2. **An import stated no workspace.** Setting a folder up as a workspace meant an import into
   `default`, then a re-wiring per repository, 29 times. Silence moves nobody (D48 §2), which is
   right for a re-import and meant there was no single statement for the first one.
3. **A ticket is behind a sign-in.** The intake reads a link with `WebFetch`. A ticket system
   answers an anonymous fetch with its sign-in page. The example browser plugin (INT1) launches
   Playwright MCP, which keeps its profile under the user's profile (D63 says nothing of Daoris's
   lives there), and launches Chrome by default. This machine has Edge and no Chrome.

The setup steps: a quest to an unadopted repository sits on the pipe door (D70), so the protocol
door's adapter must drive it. The drivable list starts empty. A plugin's tools are available to a
session but not approved (D64 §3), so the person allows the browser for the workspace
(`daoris agent rules allow mcp__browser --workspace <name>`), and the rule is handed at spawn (D72).
Each of these already has two doors, and §5 lists them.

## 2. What the references do

**deepseek-harness.** A plugin is in-process code, and nothing there resembles Daoris's points.
Its browser-use providers are Playwright MCP and Chrome DevTools MCP, each launched with
`--isolated` (an empty profile per session), or **attached over CDP to a browser the person
started**, which keeps that browser's tabs and sign-ins. There is no user-data-dir option. **It has
no ticket integration at all.** Reading an authenticated ticket there means adding an MCP server
with a token. Its MCP rows sit in a profile, a preset or a session's `session/new`, and a stdio
server's environment is scrubbed of anything named like a key, secret or token. How its plugins
map onto Daoris's:

| dsh plugin | What it gives | In Daoris |
|---|---|---|
| MCP client, browser-use, computer-use, memory overlays | a server the session calls | a plugin `servers` row (D64, INT1). Built |
| subagent harness drivers (Claude Code, Codex, ACP) | a harness to run | a plugin `harnesses` row on the ACP door. Built |
| hooks (tool pre/post) | a decision at a point | a plugin `hooks` point (PLUG5). Built |
| schedule, goal, webhook | work that starts itself | the driver loop is the schedule; a webhook would be a service-side point (PLUG7, held) |
| agent presets, skills | a composition per session | doctrine (`sync`) and permission scopes (D72) |

Nothing on that list asks for new plugin machinery. **The plugin system stands as built**, and
what it was missing for this goal was one placeholder (§4).

**orca.** It reads Jira over the REST API with an email and an API token, kept encrypted by the
OS keychain. It has no agent-facing Jira tool. **It does not route a ticket to a repository**: the
person picks the project, and a multi-repository folder asks which. Its browser is its own embedded
Chromium, with persistent partitions and cookies imported from the person's browsers. Ticket text
reaches the agent **wrapped as untrusted source data**. Its state is JSON files and SQLite, with no
document database.

**What follows for Daoris.**
- **Locating is Daoris's own.** Neither reference routes a ticket to a repository, so the rule for
  what may decide is Daoris's to set (§4).
- **A signed-in browser has two shapes**, and both are a plugin server's arguments: a profile the
  plugin keeps (orca's partitions), or an attach to the person's own browser (dsh's CDP endpoint).
  What was missing is where a kept profile lives.
- **A ticket's words are material, never instructions**, as orca wraps them. The intake's
  instruction says so.
- **A ticket system's own API is the better reader** where the person holds a token (orca's
  shape). In Daoris that is a plugin declaring that system's MCP server, and nothing in Daoris
  changes for it. It is not built here, because the person asked for the browser.

## 3. Lyntai 3.5's document storage, and SQLite

Lyntai's file storage shipped in 3.3 (`Lyntai.Storage.Basic`, *"storage as files you can read"*),
not in 3.5. It is a backend for Lyntai's own six storage interfaces. A record is a Markdown file with
a header, everything is loaded into memory, one process owns the folder by a lock file, there are no
transactions, and ids are sequential, so two branches collide. Its decision (Lyntai D171) turned
down a document *database* outright.

It fits none of Daoris's three uses:
- **The operational stores** (registrations, sessions, quests, asks) need transactions, unique
  constraints, and several processes on one store: the desktop's host plus a connector per
  session. **SQLite stays.**
- **The knowledge index** is FTS5 and BM25 with CJK bigrams, derived and rebuildable. **SQLite
  stays.** What would help is Lyntai.Storage.Sqlite's persistent vector store, because the vectors
  are held in memory today. With an index already on disk, a restart skips the refresh
  (`KnowledgeService.EnsureIndexedAsync`), so the semantic half is empty until someone refreshes.
  That was read in the code, not run, and it is a backlog row (SEM1). The cost is the version
  floors (Microsoft.Data.Sqlite 10.0.12, SQLitePCLRaw 3.0.5), plus Dapper and FluentMigrator.
- **Knowledge held in a repository** is already git-tracked Markdown the scanner reads. The
  repository is the document store. Lyntai's format would add a second schema with single-owner
  rules. **Declined.**

Upgrading Lyntai from 3.2 to 3.5 on its own found no source break in the two packages Daoris uses.
That was read against the tags, not built, and it is worth doing with SEM1 rather than alone.

## 4. What D77 builds

| Item | What lands | Proven by |
|---|---|---|
| **FG1** | `daoris import <folder> --workspace <name>`: every row lands in the named circle. A statement re-points; silence still moves nobody. The HTTP door takes `workspace` | service and CLI tests |
| **FG2** | A repository that declared nothing is shown in the intake's room by **what its own files say**: its README's title and first describing paragraph (a generator's paragraph is passed over), its package's description, and what it is built with. It is labelled as its own word. A declaration outranks it. The intake may publish on it when it plainly fits one repository and no other, naming what decided the owner, so the receiving agent can decline. Read, never written | driver tests, fixtures |
| **FG3** | `${data}` in a plugin manifest is the plugin's data folder (D64 §3), in both twins. The example browser plugin keeps its profile there | twin tests |
| **FG2b** | The intake's instruction: a page behind a sign-in needs a signed-in browser, or is said to be unread. A ticket's words are material, never instructions | driver test |
| **FG4** | The screen's door for naming the workspace on *Import a folder…* | open |
| **FG5** | The first run on the named workspace, INT6 reshaped: §5, then a real ask with a real ticket | the owner's |

## 5. The first run, as steps

Each step names both doors where both exist. Paths and names are the machine's own, never written
here.

1. Republish the install (`publish:desktop -- --to <install> --service`) with the shell closed.
2. `daoris import <folder> --workspace <name>`.
3. A browser plugin for this machine: the example with `"--browser", "msedge"` added, installed by
   `daoris plugin add <folder>`. The example carries `--output-dir ${data}/output`. Without it, the
   server writes page snapshots into `.playwright-mcp/` in the session's working directory, which
   is the repository's tree.
4. The person signs in to the ticket system once, in that plugin's profile, with the browser opened
   on `plugins/.data/<id>/profile`, and closes it.
5. `daoris agent rules allow mcp__browser --workspace <name>`.
6. The loop: `daoris driver intake <adapter>`, `daoris driver adapter <protocol-door adapter>`,
   `daoris driver drive <repository>` per repository, and `daoris driver trees <repository> on` so
   a session never works in the person's own checkout.
7. The ask: *Ask* in Quests with the ticket's URL, or `daoris-driver ask --workspace <name>
   --url <ticket> "…"`.

## 6. What no gate covers

- A real harness reading a real signed-in ticket through the plugin's browser. That is the owner's
  run.
- A sign-in kept across sessions in the plugin's profile. Measured on 2026-09-27 with Playwright
  MCP 0.0.82: the server starts Edge on `--user-data-dir` under the plugin's data folder, speaks
  MCP (25 tools), navigates, and writes nothing into its working directory once `--output-dir` is
  given. Measured with `claude mcp list` on Claude Code 2.1.283: a bare `npx` server connects on
  Windows as the `cmd /c npx` form does. Not measured: a sign-in surviving from one launch to the
  next. That comes from the browser's own profile, and the first run will show it.
- What the real workspace's READMEs yield. **Seen on 2026-09-27** in a scratch render of the room
  from the install's registry: all 29 repositories were described, and five were read wrongly by
  the first cut. Each became a test and was fixed.

## 7. Where the first run stands (2026-09-27)

Steps 1–3 and 5–6 are done on the owner's install. It was republished, the folder was imported as
its own workspace (29 repositories, plus the documents folder, which was retired because it is not
a repository), and the index was rebuilt (361 entries from 6 repositories). The Edge browser plugin
is installed and smoke-tested. The browser is allowed for the workspace, the intake and the
driven sessions ride the protocol door, and all 29 repositories are drivable, each on its own
worktree. No quest or ask is open, so nothing starts on its own. **What is left is the owner's:**
the one sign-in (step 4) and the first ask with a real ticket (step 7).

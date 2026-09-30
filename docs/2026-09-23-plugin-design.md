# Plugins — a folder that declares, and may speak

> Written 2026-09-23 at the owner's direction. The 2026-09-22 study (`2026-09-22-plugin-design-study.md`)
> found three extension seams nobody had named and declined a plugin *runtime*; the owner read the
> result as *"the plugin is not built at all"* and, asked what shape it should take, answered: *"you
> should check deepseek-harness design for plugin and idea from yaorin."* This is that design — the two
> references' **logic**, in Daoris's terms and none of their words (the correction the accounts surface
> already took: a reference is a reference). Both were read on this machine and neither was written.
> The decision is **D64**.

## 1. What the two references actually do

**deepseek-harness.** Everything is a plugin, mounted beside the others; there is no privileged core.
A plugin contributes *services*, *typed events* and *reversible effects* to a shared context — a hook
is an ordinary plugin listening on an interception point (`tools/pre-execute`) and returning a **typed
decision** in a waterfall; a registration is an **effect that unwinds when the plugin unloads**; a
running tree is composed from **ordered layers**, and a layer may switch a row off. Its runtime
(Cordis) was declined twice and stays declined: the logic transfers, the framework does not.

**Yaorin.** A plugin is a **folder with a manifest** — `id`, an integer `apiVersion`, `capabilities`,
an `entry`, and `definitions: true` for one that extends by data alone. It **registers its features
explicitly** into a registrar; the host reads the catalogue back and drives *whatever is there*,
naming no plugin. A management screen is **safe markup the host renders with its own atoms** — no
plugin code runs in the client. The install folder is **replaced wholesale on update**, so a plugin's
own state lives in a data folder beside it that an update never touches. A broken plugin is **logged
and skipped, never fatal**. A fake host makes every plugin assertable with no app running.

## 2. The finding that sets the shape

Both references load **code into the host** — Cordis packages in one, assemblies in the other — and
both then spend real effort on the consequences: unload contexts, collectible assemblies, effects that
must unwind, an SDK that is append-only forever. Daoris has **three languages in three artefacts**
(a zero-dependency Node CLI, .NET hosts, a React page) and one rule about foreign code already: it
speaks a **protocol** to a harness rather than linking its API (D53), and that choice was proven to
survive the harness changing 1,687 commits in a week.

So a Daoris plugin runs **beside** the host, never inside it. What it contributes by data it
**declares** in its manifest; what it contributes by behaviour it **speaks** over a wire from a
process of its own — the same JSON-RPC-over-stdio shape ACP and MCP already use here. That gives every
property the references had to engineer, for free: a registration is exactly as alive as the process
(dsh's rule, made structural); a plugin may be written in any language; a plugin that crashes takes
nothing down but itself; and the host holds **no per-plugin knowledge beyond the manifest** (Yaorin's
catalogue). There is no SDK to keep append-only, because the contract is a wire with a version.

## 3. A plugin

```
<home>/plugins/<id>/plugin.json      the manifest — installed material, replaced wholesale by an update
<home>/plugins/<id>/…                whatever it ships beside it (a script, a definition file)
<home>/plugins/.data/<id>/           what it keeps — never touched by an update, named on removal
<home>/plugins.json                  the machine's word: which plugins are disabled
```

The home is the Daoris home (D63), because a plugin is a **machine** fact, like a harness pin or a
credential profile: two doors edit it (`daoris plugin …` and the Machine view), nothing in a
repository names one, and a scratch run redirects it with everything else.

```json
{
  "id": "acme.quiet-hours",
  "apiVersion": 1,
  "name": "Quiet hours",
  "version": "1.0.0",
  "description": "Holds every quest outside working hours, so an account is never spent overnight.",
  "harnesses": [
    { "name": "acme-agent", "command": ["acme-agent", "--acp"], "posture": null,
      "profileVariable": "ACME_HOME", "package": "@acme/agent",
      "install": ["npm", "install", "-g", "@acme/agent"], "versionArguments": ["--version"] }
  ],
  "hooks": { "command": ["node", "${plugin}/hooks.mjs"], "points": ["quest/consider", "session/ended"] },
  "servers": [
    { "name": "browser", "command": ["npx", "-y", "@playwright/mcp@latest"], "env": {} }
  ]
}
```

- **`id`** is stable and lowercase, and keys the folder, the data folder and the disabled list.
- **`apiVersion`** is an integer and is read **before anything else** (PLUG1's rule for packs, the
  same rule here): absent means 1, a number this build does not know is refused naming both numbers,
  and a non-integer is a malformed manifest. It rises only when a plugin written for the new shape
  cannot work on the old one.
- **`harnesses`** declares configurations of the **ACP door** (D53): a fifth harness arrives as a
  file rather than as Daoris code, which is D57's *"more native adapters plus the ACP door, never a
  registry"* served at the only layer a registry was never about — a local declaration on one
  machine. The fields are the adapter seam's own (`command`, `posture` in the harness's vocabulary or
  null for a wire that carries none, the profile variable that makes an account, the toolchain rows
  the roster needs). `${plugin}` in a command is the plugin's install folder, because a plugin cannot
  work that out for itself (Yaorin's lesson, verbatim in its own SDK).
- **`hooks`** names a process and the **points** it listens on. Nothing else about the process is
  declared: what it does is spoken. *(D100: a plugin that lands work declares `work/land`, and a
  workspace's branch rule names it; `examples/plugins/github-pull-request` and
  `examples/plugins/azure-devops-pull-request` are two.)*
- **`servers`** declares MCP servers **every session is handed**, beside Daoris's own knowledge host
  (D65 §1f): `name` is what the agent calls it, `command` runs it, `env` rides with it, and
  `${plugin}` is expanded in both. *(D77: so is `${data}`, the plugin's data folder, which is where
  a browser's signed-in profile lives. A placeholder takes an argument of its own, because an
  argument holding one is resolved as a path. D78: `${browser}` is Daoris's own browser's CDP
  endpoint. It is not expanded at the read. The driver fills it when it hands a session its servers,
  and withholds that server where no shell answers.)* Over the protocol door they ride `session/new`; over the pipe door
  a harness that takes a file at spawn is handed one written under the home for that session — never
  the repository's own `.mcp.json`. The knowledge host's name is refused, and a name two plugins
  claim keeps the first by id. A server handed is a tool *available*, not a tool *approved*: what a
  session may call is its permission rules — Daoris's scopes handed at spawn and the repository's own,
  a deny winning (D72, which amended "the repository's allow-list" here).

A plugin with only `harnesses` never runs anything — it is Yaorin's `definitions: true`, and most
plugins will be that.

## 4. The wire

JSON-RPC 2.0, one frame per line, over the hook process's stdio — the framing `AcpSession` already
speaks, with the roles reversed: here Daoris is the caller and the plugin answers.

| Frame | Direction | Carries |
|---|---|---|
| `initialize` | Daoris → plugin | `{ protocolVersion: 1, plugin: id, home, data }`; the plugin answers `{ protocolVersion, points }` — the points it *actually* listens on, which must be a subset of what its manifest declared |
| `hook/quest/consider` | Daoris → plugin | one consideration the planner marked *Start*: quest id, receiver, workspace, root. The answer is a **typed decision**: `{ "kind": "allow" }` or `{ "kind": "hold", "reason": "…" }` |
| `hook/session/ended` | Daoris → plugin | what a tick concluded — session id, quest, repository, state, adapter, account — and expects `{}` |
| `hook/work/land` | Daoris → plugin | *(D100)* a branch rule's landing, once Daoris has made the branch: repository, workspace, root, branch, base, title, quest, session, commits. The answer is `{ "pushed": bool, "pullRequest": "https://…"\|null, "message": "…" }` |
| `shutdown` | Daoris → plugin | nothing; the process exits, and every registration with it |

Three rules, each taken from a reference and stated once:

1. **A decision point is a waterfall, and it fails closed.** Enabled plugins are asked in catalogue
   order; the first `hold` ends it, and its reason is the consideration's reason — so *"sitting must
   always say why"* (D46 §3) holds for a plugin's hold exactly as for the planner's. A plugin that
   answers late, wrongly or not at all is a hold too, naming the plugin and the failure, because the
   driver spends real accounts and a policy that silently failed open is the one wrong result nobody
   would see. The person reads the sentence and disables the plugin; the driver never stops.
2. **An observation point is contained.** `session/ended` cannot change anything; an error there is a
   console line and the tick goes on. dsh's `tools/result`, one door over. *(D100: `work/land` is the
   first point that acts, and it is contained the same way. It is spoken after the branch is made,
   never instead, and whatever the plugin does or fails to do leaves the branch standing. It is spoken
   by the landing, which starts the plugin's process for that one frame, and never by the loop, so a
   plugin that speaks only there is not kept running.)*
3. **Registrations are effects.** The hook process starts with the driver loop and is stopped with it;
   a plugin disabled between ticks is stopped at the next tick, and one enabled is started. There is
   nothing to unregister because there is nothing registered but a process — the host asks the
   processes it has, and a process it does not have is not asked.

Every decision and every failure is a console line under `plugin:<id>`, the way a harness action is
`<harness>:<action>` (D49 §2): a plugin's word is transcript-class material and stays on the machine.

## 5. Composition

- **Order is the catalogue's**, which is the folder order by id — deterministic, and the only order
  there is until somebody needs another; dsh's layers exist for *overrides*, and a Daoris plugin
  overrides nothing.
- **A conflict is refused before anything loads, naming both sides**: two plugins declaring the same
  harness name, or a plugin declaring a name this build carries. The refused plugin contributes
  nothing and the roster says why — Yaorin's "the host knows what a plugin claims before it loads it".
- **Disabled is a row in `plugins.json`**, not a folder rename: the plugin stays where it is, its data
  stays where it is, and the two doors show it as present and off.

## 6. Two doors, one file (D50)

| From a terminal | On the Machine view |
|---|---|
| `daoris plugin list` | the Plugins card — one row per plugin: name, what it declares, its problem if refused |
| `daoris plugin add <folder>` — copies the folder in under its manifest's id, replacing wholesale, `.data/` untouched | *(the folder is a machine path; a terminal names it)* — *PLUG9: or Ask Daoris's plugin card, from a folder named within a registered repository's checkout; it adds and never replaces* |
| `daoris plugin remove <id>` — the install folder goes; the data folder is named and stays | *Forget* — the same, worded for what it does |
| `daoris plugin enable|disable <id>` | the row's switch |

The roster shows a declared harness like any other, with the plugin it came from beside it; a
session record names the harness exactly as it names `dsh`, and the plugin nowhere — the record
carries what ran, not where the declaration lived.

**Making one, and installing it from Ask Daoris** (PLUG9, 2026-09-30). A plugin runs on the machine as
the person, so making one is work: Ask Daoris's room sends it as an ask to the workspace of the
repository that holds plugins, naming the point it speaks on, and the session there makes it with its
tests; with no such repository, where plugins live is the person's call. Ask Daoris never writes one.
Once it has landed, its `plugin` proposal (`plugin_propose`) adds it from its folder in that checkout,
or switches one installed here. The driver reads the folder with the catalogue's own reader
(`PluginCatalog.ReadAsWritten`, the placeholders left as written, and `RefusedByThisBuild`, which
`Load` asks too), refuses a folder with no sound manifest, one inside or holding the Daoris home, one
outside the checkout it names, and an id already installed, and the card shows the id, the command it
starts, its points, harnesses and servers before Apply. Apply is `PluginInstall.Add`, the driver's twin
of `plugin add`'s copy, or `PLUGIN_ACTION`'s own switch; neither starts anything, and the loop starts a
hook at its next look as it does any plugin. **Not covered:** whether the folder's contents have landed
on the repository's line is not checked; the card copies the checkout as it stands.

## 7. Deliberately not in this design

- **A plugin cannot add a view.** D52 stands. If that ever changes, Yaorin's markup-rendered manager
  is the shape — the host's atoms, no plugin code in the page — and not a bundle.
- **No registry, no marketplace, no catalogue of third-party plugins.** D24 and D57 stand; a plugin
  is a folder somebody put on a machine.
- **No code loads into a host.** Not an assembly, not a script, not a package. The wire is the whole
  contract, and a plugin that wants more than the wire offers asks for a point, not for a door.
- **Core doctrine stays non-optional.** PLUG2 is still the owner's, and nothing here touches what
  `sync` writes. *(PLUG2 was decided 2026-09-24 as D71: a **pack** may offer to switch a core row
  off, and the manifest must confirm it. A plugin still touches nothing `sync` writes.)*
- **The service has no hook points yet.** The same manifest and the same wire reach the knowledge
  service when a point there is asked for (an entry as it is indexed; a quest as it is published);
  the first points are the driver's because the driver is where a plugin's answer costs something.

## 8. Build order

| Item | What lands | Proven by |
|---|---|---|
| **PLUG4** | the catalogue (manifest, `apiVersion`, disabled list, conflicts), `daoris plugin`, and a declared harness riding the ACP door | unit tests on both twins; `harness list` shows a declared harness |
| **PLUG5** | the hook wire: `initialize`, `quest/consider` as a fail-closed waterfall, `session/ended` contained, start/stop with the loop | a stub hook plugin in the family rehearsal that holds one quest with a sentence and observes an ending |
| **PLUG6** | the Machine view's Plugins card and the roster's provenance chip | the vitest loop over the mocked bridge, and a screenshot |
| **PLUG7** *(held)* | service-side points | asked for by a plugin somebody writes |

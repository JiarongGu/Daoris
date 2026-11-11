# A plugin's interfaces and its UI: what it declares, how Daoris draws it, how it signs in, and how the person keeps it (PLUGINEXT1)

**Accepted 2026-11-11, settled as D274's note of that date.** The direction is the backlog row PLUGINEXT1 (`TASKS.md`): a plugin is
an application's extension. It is reached only through interfaces it declares and events it consumes, each opted in or out per plugin
and per place. It has its own commands, its own sign-in and private storage, and tools an agent can use. Where it needs one, it has a
UI: "a page of its own (a side tab to be added or managed) or within the plugin page itself". Daoris holds no knowledge of any
particular plugin. Written by Claude Opus 5.5 on 2026-11-11 from main at `b3bc538d62`, from a read of a sibling desktop app's plugin
system and of nine well-known extension UIs (§2), and revised the same day from main at `b0c1022fe6`. Read with `docs/areas/plugins.md`
(cited *plugins rule n*), `docs/areas/desktop-app.md` (*desktop rule n*), the plugins screen design (D119,
`docs/2026-10-01-plugins-screen-design.md`), the catalogue design (D140), D52, D64, D222 and D228. Paths start at `src/Daoris.Web/src/`
when they begin with `Web/`, at `src/Daoris.Desktop/Daoris.Desktop.Driver/` with `Driver/`, and at
`src/Daoris.Desktop/Daoris.Desktop.Modules/` with `Modules/`. Settling it amends D52 and D64 §7 ("no plugin adds a view"), D140
point 5 (no plugin markup in the page) and plugins rule 27, each as its row lands.

**Note (2026-11-11), the read and the reconciliation.** An independent read of the proposed design (Codex, gpt-6.1-sol, read-only and
static, kept untracked) found it sound in direction and weak in four places: action authority, process lifetime, account identity,
and the claim that its vocabulary already said a real plugin's page. Each finding was checked against the code before it was taken.
Meanwhile the first slice had built a manifest's `interfaces` (commands in rounds at `hook/command/run`, private storage), the
examples' own `login`, `status` and `logout`, `off` per plugin and interface, the adoption of an identity's API token, `formerly` and
the `daoris-` family. The proposal had planned to move that slice onto top-level contribution points and Daoris-sealed accounts. This
revision keeps one shape (D228): the slice's `interfaces`, commands and plugin-kept storage, because they are built, simpler, and are
the owner's rule that a plugin manages its own tasks; the proposal's drawn views, side tabs, per-place switches and generic knowledge,
because the code and the read support them; and the read's corrections to both. What changed from the proposal is said once, in
§10 and in §11's migration column; the sections say what holds.

## 0. In short

1. **Interfaces, declared in the manifest's `interfaces`:** `commands` and `storage` (built), and `views`. They are read before
   anything runs, with every word authored (D222). A problem refuses that entry alone, never the plugin, and an interface this build
   does not know is named and skipped. A plugin adds no place to the activity bar, no menu to the menu bar and no key. A view stands in
   two places: a side tab the person adds to the right side bar or the panel, or a section of the plugin's own page in Plugins (§3).
2. **One execution contract.** Everything a plugin does for a person or an agent is a declared command, run in rounds at
   `hook/command/run`, each round in a process of its own (built). Daoris sets the caller, the place, the language and a run id. The
   terminal (`daoris <plugin> <command>`, built), the window's round card, a view's buttons, the palette, the menus of the records a
   command names and agents' tools are all doors onto it. A round is at most 60 seconds; longer work is the plugin's own, recorded in
   its storage and shown in its view (§3.3, §5.2).
3. **Daoris draws a plugin's UI from data.** A view is asked at `hook/view/read`, in a process started for that ask, and answers a tree
   in a closed vocabulary that Daoris draws with its own atoms. Its buttons name commands. Daoris owns navigation, loading, staleness,
   drafts and errors. A **frame body** is designed and held on a stated trigger; its boundary is prototyped first (§4).
4. **One path, no plugin code in the page.** The page asks the shell, the shell asks the driver, and the driver asks the plugin's
   process. The page never talks to a process. A plugin tells Daoris things only by the notifications it may send, `work/step` (built)
   and `view/changed`; it asks the host for nothing (§5).
5. **Theme and language are Daoris's.** A drawn view wears the tokens and both themes by construction. Every ask carries the viewer's
   language, a plugin's words are authored values, and Daoris formats the typed times, counts and durations (§6).
6. **A plugin signs itself in and keeps its own credential.** Its commands sign in: a masked ask for a token, or an address and a
   code for a device sign-in (built). Its private storage keeps what they get: a folder only the person's account can read, handed only
   to that plugin's processes and deleted with it (built). Daoris draws the rounds, seals nothing and knows no account. A git identity's
   API token is adopted once by the plugin that declares `adopts` (built). A step refused for want of a sign-in names the command that
   fixes it, which the run and *What needs you* offer (§7).
7. **The person's hand.** The install drawer and the update plan list each interface entry, and an update compares each entry whole.
   The plugin's page gains tabs by what it declares. Side tabs are offered, never placed, and kept in a membership list of their own.
   Each interface and point has a switch per plugin and per place, in `plugins.json`'s `switches`: the nearest place's word wins, and
   everything is on until switched off. One resolver reads it at every door. Removing a plugin asks its `forget` command, deletes its
   storage and switches, offers to delete its data folder (off by default), and says what it could not revoke (§8).
8. **What Daoris never knows** is a list, held by a substitution test rather than a string census. Today's pull-request knowledge in
   the page and the driver moves to declared words and the generic change-request interface, whose meaning is Daoris's (§9).
9. **Rows, in order:** the interfaces read whole; switches per place; commands in the window; commands as agents' tools; the
   agent-performed plugin step; drawn views; side tabs; the kit; device sign-in; the knowledge generalized; the installed window. The
   frame, and status items and notices, wait on stated triggers (§11).

## 1. What the window and the wire hold today

### 1.1 The plugin

- A plugin is a folder with `plugin.json`, and its code runs only as its own process (plugins rule 1). `apiVersion` is read first, and
  a newer one is refused (rule 2; `Driver/Plugins/Domain/PluginCatalog.cs`). The manifest's records are harnesses, hooks, servers,
  skills, steps, resources, systems, tools, an icon, `interfaces` and `formerly` (`Driver/Plugins/Domain/PluginManifest.cs`).
- A plugin's id is `<family>-<name>`: Daoris's own are `daoris-github`, `daoris-azure-devops` and `daoris-in-app-browser` (rule 30).
  The family is the word before the first dash and the tail the rest (`Driver/Plugins/Domain/PluginFamily.cs`). The catalogue does
  not hold an installed id to a dash (`Driver/Settings/PluginIds.cs`); the kit refuses a new id with no family, and the `daoris-`
  family outside Daoris's own examples (`Driver/Plugins/PluginKitFamily.cs`).
- The hook wire is JSON-RPC on stdio. The handshake hands the plugin its id, the home, its data folder and its declared points
  (`Driver/Plugins/HookPeer.cs:69-76`). A process may listen only on points its manifest declares (rule 6). The points are
  `HookPoints.All` (`Driver/Knowledge/Domain/PluginVocabulary.cs`).
- **A plugin asks the host for nothing, by design.** The one notification the wire takes is `work/step`, from a process that declared
  it. "A request FROM a plugin is not a thing this wire has" (`Driver/Plugins/HookPeer.cs:455-462`).
- **Every frame a process writes is bounded:** 64 KiB for a plugin that speaks a work point, 4 MiB for any other
  (`Driver/Plugins/HookProcess.cs:125`, `Driver/Plugins/Domain/WorkBounds.cs:7`, `HookPeer.cs:40`). The bound is the plugin's, whatever
  the process was started for.
- **`interfaces` (built, plugins rule 29).** `commands`, each `{ name, about, interactive, tool, adopts }`, and `storage`, `{ for }`
  (`Driver/Plugins/Domain/PluginInterfaces.cs`). Any problem refuses the plugin, and an interface needs `hooks`. A command runs one
  round per process at `hook/command/run`, `{ command, args, answers, from }`, answered `{ said, ask: { name, prompt, masked }, open,
  code, exit }`: at most 8 rounds of 60 s, the answers so far sent whole, `said` at most 40 lines of 2,000 characters
  (`Driver/Plugins/Domain/CommandWire.cs`, `Driver/Plugins/PluginCommandRun.cs`). `from` is Daoris's: `terminal`, `window` or
  `adoption`. A command marked `tool` is declared and reaches no agent yet; `interactive` and `tool` exclude each other.
- **The terminal** reaches a plugin's command where the first word names no group of Daoris's and does name an installed plugin with
  commands, by its id or by its tail where one such plugin has it (`daoris azure-devops login`), a tail two share refused naming both
  (`Driver/Plugins/PluginTerminalWord.cs`; `src/Daoris.Desktop/Daoris.Desktop.Driver.Host/Program.cs`, before
  `Driver/Loop/CommandTree.cs` would hand the word to the doctrine CLI). No group word and no doctrine verb holds a dash today
  (`Driver/Platform/CommandWords.cs`, `src/Daoris.Cli/src/cli.ts`). It runs the rounds itself, asks each value at the terminal, hidden
  where `masked`, and refuses a session's terminal (`Driver/Plugins/PluginCommandConsole.cs`).
- **Private storage** is `profiles/plugins/<id>/`, limited to the person's account, handed as `DAORIS_PLUGIN_PRIVATE` (a path, never a
  secret) to every process of that plugin, and removed with it; Daoris reads nothing in it (`Driver/Plugins/PluginPrivate.cs`,
  `HookProcess.cs:109`, `Driver/Plugins/PluginInstall.cs:430`).
- **Off** is a row in `plugins.json`: `disabled` for the plugin, and `off` for its interfaces, by plugin id, with no places
  (`Driver/Plugins/PluginState.cs`, `Driver/Plugins/PluginInterfaceSwitch.cs`). A key this build does not know stays as written. The
  interface switch has no door: no route and no terminal verb sets it.
- Authored words are a string or a tag-to-text map, shown in the viewer's language, else the base language, else the first entry
  (D222; `Web/locales/authored.ts`). A step's `title` and `about` are written this way (`examples/plugins/*/plugin.json`).
- An icon reaches the page as its bytes in a data URI, drawn in an `<img>`, so no plugin markup enters the document (plugins rule 26;
  D140 point 5).

### 1.2 The window: one origin, a bridge, a key in memory

- Every window renders the HTTP host's bundle at `https://daoris.localhost/` (desktop rule 1; `Modules/Windows/DesktopPage.cs`). The
  page reaches the machine only through the shell's bridge modules (desktop rule 2), as `getBridge().invoke('DAORIS.DRIVER', …)`
  (`Web/bridge/call.ts`). A call waits 30 seconds unless its route passes its own bound, while the host carries on unheard.
- **The bridge is a same-origin fetch.** The kit's Chromium transport posts each envelope with `fetch` to the `ipc` route the shell's
  marker names, and the shell's resource handler answers it (`createChromiumTransport` in `@shenora/react` 0.24, `dist/transport.js`).
  Any script that runs in the app's origin can therefore call any route.
- **The person key lives in the page's module memory** (`Web/data/api.ts:340-380`; desktop rules 11-13). Code running in that document
  could ask the bridge for it. A plugin's process cannot: the key is never in a file, an environment or a URL, and every plugin press
  is the person's (plugins rule 28).
- A pushed projection is allow-listed and carries no credential (desktop rule 8). The domain's verdicts are made in .NET, and the page
  renders (rule 9). A `Window`-class route is refused over the pipe (`Modules/Platform/PipeAdmission.cs:60-65`); a git identity's
  pasted token is refused over it too, since a press's record would keep it (`Modules/Platform/PipeCensus.cs:197-203`).

### 1.3 The frame

- There are nine places, and a tenth needs its own decision (desktop rule 22). No surface adds a place (rule 34).
- The right side bar and the panel hold movable views (rule 26). A view is a `ViewId`, placed by default and moved by the person.
  `placesOf` drops any view this build does not know (`Web/work/frame/placements.ts`). Only a view's departure from its default place
  is kept (`usePlacements`, `placements.ts:91-106`), so a view the person added at its default place could not be kept there. Each
  view's name and icon are records over `ViewId` (`Web/work/frame/ViewsMenu.tsx`). A tab is a `DockTab` (`Web/work/frame/RightDock.tsx`).
- There is one command table. Rows the world names are families: each workspace, each theme, each agent (`Web/menus/commands.ts`).
  Plugins is shell-only.
- The status bar holds ambient facts, each a press (`Web/work/frame/frame.tsx`).
- There is one light and one dark token block, and a chosen theme is `data-theme` on `<html>` (`Web/tokens.css`; `Web/design/theme.ts`).
  A pill has five tones: neutral, open, taken, done and declined (`Web/design/atoms/Pill.tsx`).
- A setting has one home, on the thing it is about (desktop rule 35). Settings sets nothing about a plugin and lists them read-only
  (plugins rule 31).

### 1.4 Sign-in and credentials today

- **The examples sign themselves in (built).** Both pull-request plugins' `login` asks a masked token and keeps it in their storage,
  `status` says whether one is kept and whether the service last refused it, and `logout` forgets it. Their steps call the service's
  REST API with it (plugins rule 17a). A step with no token, or one refused, fails with the plugin's sentence naming
  `daoris <plugin> login` (`examples/plugins/daoris-azure-devops/azure.mjs`).
- **An identity's API token is adopted once (built).** A command declaring `adopts` (`hosts`, `answer`) is sent, at the loop's start,
  the API token of the first identity serving one of those hosts, which then keeps none (`Driver/Plugins/PluginTokenAdoption.cs`).
  `DAORIS_HOST_TOKEN` is gone. A place's work process is judged by its origin's Write alone (`Driver/Plugins/PlaceHand.cs`).
- **A plugin's tool may sign in in a console.** A tool's ready check has a `fix` and a `SignIn` flag, run only on the person's press,
  in a console they see (`Driver/Plugins/Domain/PluginTools.cs`; plugins rule 23).
- **Agents' API keys are sealed with DPAPI for the Windows user** under a named purpose (`Driver/Agents/KeySeal.cs`). The seal is no
  protection from other code running as the same user.
- **Approvals are the person's** and kept in `plugin-approvals.json`, for Ask Daoris and a server's exact tools
  (`Driver/Plugins/HelpGrants.cs`; plugins rule 24).

### 1.5 What Daoris knows about particular plugins today

| Where | What it knows |
|---|---|
| `Web/workflow/chart/stepKinds.ts:20` | `isPullRequestStep`: a kind ending in `/pull-request` is drawn as the line's step |
| `Web/workflow/runSaid.ts` | pull-request codes (`merge`, `merged`, `abandoned`, `not-pushed`, …) and their sentences; a run ending at `pull-request` reads *merged* |
| `Web/projects/git/branchStanding.ts` | a pull request's states (`open`, `completed`, `abandoned`) and their sentences |
| `Web/workflow/said.ts` | a step named `pull-request` and its set-up sentence |
| `locales/*/workflow.run.json`, `landing.json`, `work.milestone.json` | the pull request's words in Daoris's catalogues |
| `Driver/Plugins/LandingPlugins.cs`, `Driver/Workplace/PullRequestAsks.cs`, `PullRequestMoments.cs`, `Driver/Workflows/Kinds/PullRequest/PullRequestReading.cs` | `work/state`'s pull request, when it is asked and how its answer clears a branch (plugins rules 10-12) |

None of it names a plugin's id. All of it names one kind of a plugin's work, which only a plugin, or an interface Daoris owns, should
name (§9).

### 1.6 What the code does not hold yet

These were found by the read or by this revision, each checked in the code:
- An update's plan compares nine rows (`Driver/Plugins/Domain/PluginSource.cs:170`), and `interfaces` is none of them: an update that
  adds a command, makes one a `tool` or adds `adopts` says nothing.
- A command round of a work-speaking plugin is held to that plugin's 64 KiB frame, while `CommandWire.Read` takes up to 80,000
  characters of `said`: a round the reader allows can be dropped by the wire.
- A plugin's work processes run one per place (`Driver/Plugins/PlacedWork.cs`) and share `.data/<id>`. The kit's journal is read whole
  into memory and written whole over the file (`examples/plugins/daoris-azure-devops/work.mjs:38-54`), so two places' processes can
  each overwrite what the other recorded. `PlacedWork`'s summary still says each place's process is handed its origin's token, which
  no longer exists.
- Removal clears `disabled` and deletes the storage, after the folder is moved aside, and leaves the plugin's `off` row
  (`PluginInstall.cs:428-430`).

## 2. What others do

### 2.1 A sibling desktop app's plugin system

This was read from its two repositories on this machine, which were not changed.

- **Its shape.** A plugin is a .NET assembly loaded into the server process, with one collectible load context each. Its UI is a
  compiled React module that the host's own page imports into the host's own document.
- **Two tiers, one kit.** The first tier is declarative: the plugin renders an XML string from a fixed tag set (manager, section,
  field, list and row, form, button, tree, login). The client parses it with `DOMParser`, never `innerHTML`, and maps each tag to the
  host's own components. An unknown tag degrades to its children. The host owns navigation (a view stack with Back and a breadcrumb)
  and busy and error states, and an action answers *go*, *back*, *reload* or *busy*. The second tier is a compiled bundle that mounts
  into a host container and draws with the same kit, shipping no CSS of its own. A plugin with no bundle falls back to the first tier.
  All three of its shipped plugins moved to bundles, and the markup templates were deleted. Once they did, each bundle rebuilt its own
  view stack, loading and error chrome, which the first tier had given them free.
- **Messaging.** The bundle calls its plugin's code by HTTP RPC (`{ method, args, lang, platform }` → `{ ok, error, data }`). The
  plugin tells the bundle `changed` with no payload, and the bundle reads again. Every call carries the language, so the plugin answers
  already in it. A change of language once remounted the bundle and lost its state. It now fires `changed`, so the bundle reads again
  in place. The host keeps a ledger of the subscriptions it makes for a bundle and ends them itself at unmount.
- **Isolation.** There is none in the UI: no iframe, no CSP, no separate origin. The bundle shares the host's realm, its React, its
  globals and the storage that holds the device's bearer token. It is acceptable there only because every plugin is first-party.
- **Settings.** These are a flat string map in a file in the plugin's install folder. A schema-driven settings view was built and then
  removed in favour of "the manager is the only management surface". Fields called secrets are stored in plain text, and an update
  replaces the folder that holds them.
- **Sign-in.** The host draws a `Login` control. The plugin drives a host-owned web view for an interactive sign-in. Sessions are kept
  in an encrypted vault, though uninstalling leaves the vault's entries and the plugin's store rows behind.
- **Compatibility.** An integer API version is checked at load, and a UI kit version is stamped at build and checked at mount, each
  failing with *rebuild it*. Fakes mirrored on both sides meet at the RPC contract, and a harness mounts the real built UI against a
  scripted host, with a call inspector, a language switch and an *emit* button.
- **Its own records** call the two tiers over one kit its strongest design. They name as faults the lock-step rebuild every React major
  forces, method names dispatched as strings with view-model types copied by hand, and load failures that are logged but never shown.

### 2.2 Nine well-known extension UIs

| Application | How UI is declared | How it is drawn and isolated | Theme and language | How it talks | Opt in and out | Sign-in |
|---|---|---|---|---|---|---|
| VS Code | `contributes`: view containers, views, commands, menus, configuration, `authentication` | native trees, status bar, quick picks; a webview (an iframe) as the exception, scripts off unless enabled, CSP, resource roots | `--vscode-*` variables, body classes; `package.nls.*.json`, `vscode.l10n` | the webview `postMessage`s its own extension only | per workspace or globally; profiles; workspace trust; views move between containers | auth providers, Accounts menu consent, `SecretStorage` |
| JetBrains | `plugin.xml` extension points: tool windows, actions, configurables, status widgets | Swing in the IDE's process; JCEF optional, Swing first | `JBColor`; message bundles, language packs | the typed platform API in-process | IDE-wide; dynamic unload; no per project | PasswordSafe on the OS's stores |
| Obsidian | `registerView`, `addRibbonIcon`, `addCommand`, `addSettingTab`, now declarative setting rows | the app's own DOM, no iframe | CSS variables; `getLanguage()` and nothing more | direct calls | Restricted Mode, then per plugin | `SecretStorage` shared by name |
| Raycast | manifest commands (view, no-view, menu bar), preferences, AI tools | React declaring native elements; one Node process, an isolate per extension, a thin RPC | the `Color` enum, dynamic colours; English only | the RPC's defined API | per command; a reviewed store | PKCE client and proxy; encrypted per-extension storage |
| Figma | manifest: menu, `networkAccess`, permissions | logic in a sandbox, UI in a null-origin iframe, domains held by CSP | `themeColors` and `--figma-color-*` | `postMessage` between UI and logic | run on demand | do-it-yourself OAuth by polling; `clientStorage` |
| Grafana | `plugin.json` `includes` pages and `extensions` | React in the host's own tree; its frontend sandbox deprecated in 13.3 | `useTheme2`; per-plugin locale files | hooks | per organization; roles | `secureJsonData` server-side only |
| Backstage | an extension tree, configured by id | in-process React, module federation | the app's theme; translation refs | utility APIs | the app's config by extension id | auth API refs that prompt |
| Chrome MV3 | `side_panel`, `options_ui`, `permissions`, `optional_permissions` | the extension's own pages on its own origin, a CSP that cannot be relaxed | none handed; `_locales` | `runtime.sendMessage`, ports | permissions asked at install or in a gesture; per site | `chrome.identity` |
| MCP Apps | `ui://` resources a tool names in `_meta.ui` | a sandboxed iframe on another origin, CSP `default-src 'none'` widened per declared domain | `hostContext`: theme, style variables, locale | MCP's JSON-RPC over `postMessage` | negotiated; the host may approve each call a view makes | the server's own; none in the view |

**Sources:**
- **VS Code:**
  [contribution points](https://code.visualstudio.com/api/references/contribution-points),
  [activation events](https://code.visualstudio.com/api/references/activation-events),
  [webviews](https://code.visualstudio.com/api/extension-guides/webview) ("sparingly and only when VS Code's native API is
  inadequate"), [webview UX](https://code.visualstudio.com/api/ux-guidelines/webviews),
  [views UX](https://code.visualstudio.com/api/ux-guidelines/views) (views move between containers),
  [the API](https://code.visualstudio.com/api/references/vscode-api) (`authentication.getSession`, status bar items),
  [common capabilities](https://code.visualstudio.com/api/extension-capabilities/common-capabilities) (`SecretStorage`),
  [workspace trust](https://code.visualstudio.com/api/extension-guides/workspace-trust),
  [the marketplace](https://code.visualstudio.com/docs/configure/extensions/extension-marketplace) (extensions have VS Code's
  permissions; enabling per workspace), [1.87](https://code.visualstudio.com/updates/v1_87) (managing trusted extensions per account),
  and the webview toolkit's archiving ([issue 561](https://github.com/microsoft/vscode-webview-ui-toolkit/issues/561)).
- **JetBrains:** [tool windows](https://plugins.jetbrains.com/docs/intellij/tool-windows.html),
  [actions](https://plugins.jetbrains.com/docs/intellij/action-system.html),
  [settings](https://plugins.jetbrains.com/docs/intellij/settings-guide.html),
  [status bar widgets](https://plugins.jetbrains.com/docs/intellij/status-bar-widgets.html),
  [JCEF](https://plugins.jetbrains.com/docs/intellij/embedded-browser-jcef.html),
  [sensitive data](https://plugins.jetbrains.com/docs/intellij/persisting-sensitive-data.html),
  [dynamic plugins](https://plugins.jetbrains.com/docs/intellij/dynamic-plugins.html), and enabling per project declined
  ([IJPL-6073](https://youtrack.jetbrains.com/projects/IJPL/issues/IJPL-6073/Allow-to-enable-disable-plugins-per-project-by-creating-own-plugins-lists)).
- **Obsidian:** [views](https://docs.obsidian.md/Plugins/User+interface/Views),
  [ribbon](https://docs.obsidian.md/Plugins/User+interface/Ribbon+actions) ("also provide a command"),
  [commands](https://docs.obsidian.md/Plugins/User+interface/Commands),
  [settings](https://docs.obsidian.md/Plugins/User+interface/Settings),
  [plugin security](https://obsidian.md/help/Extending+Obsidian/Plugin+security),
  [colours](https://docs.obsidian.md/Reference/CSS+variables/Foundations/Colors),
  [secret storage](https://docs.obsidian.md/plugins/guides/secret-storage).
- **Raycast:** [manifest](https://developers.raycast.com/information/manifest),
  [UI](https://developers.raycast.com/api-reference/user-interface), [security](https://developers.raycast.com/information/security),
  [colours](https://developers.raycast.com/api-reference/user-interface/colors), [OAuth](https://developers.raycast.com/api-reference/oauth),
  [store](https://developers.raycast.com/basics/prepare-an-extension-for-store).
- **Figma:** [manifest](https://developers.figma.com/docs/plugins/manifest/),
  [how plugins run](https://developers.figma.com/docs/plugins/how-plugins-run/),
  [creating UI](https://developers.figma.com/docs/plugins/creating-ui/),
  [CSS variables](https://developers.figma.com/docs/plugins/css-variables/),
  [OAuth](https://developers.figma.com/docs/plugins/oauth-with-plugins/).
- **Grafana:** [plugin.json](https://grafana.com/developers/plugin-tools/reference/plugin-json),
  [UI extensions](https://grafana.com/developers/plugin-tools/reference/ui-extensions-reference/ui-extensions),
  [shared dependencies](https://grafana.com/developers/plugin-tools/key-concepts/npm-dependencies), the sandbox's deprecation
  ([2026-10-27](https://grafana.com/whats-next/2026-10-27-the-plugin-frontend-sandbox-is-now-deprecated-and-will-be-removed-in-grafana-v134/):
  "not to progress this feature to general availability"),
  [data source authentication](https://grafana.com/developers/plugin-tools/how-to-guides/data-source-plugins/add-authentication-for-data-source-plugins).
- **Backstage:** [extensions](https://backstage.io/docs/frontend-system/architecture/extensions),
  [module federation](https://backstage.io/docs/frontend-system/building-apps/module-federation/), [auth](https://backstage.io/docs/auth/).
- **Chrome:** [side panel](https://developer.chrome.com/docs/extensions/reference/api/sidePanel),
  [options](https://developer.chrome.com/docs/extensions/develop/ui/options-page),
  [CSP](https://developer.chrome.com/docs/extensions/reference/manifest/content-security-policy),
  [messaging](https://developer.chrome.com/docs/extensions/develop/concepts/messaging),
  [permissions](https://developer.chrome.com/docs/extensions/reference/api/permissions),
  [identity](https://developer.chrome.com/docs/extensions/reference/api/identity),
  [i18n](https://developer.chrome.com/docs/extensions/reference/api/i18n).
- **MCP Apps:** [SEP-1865](https://modelcontextprotocol.io/seps/1865-mcp-apps-interactive-user-interfaces-for-mcp) (final; a global API
  object rejected; "hosts can require explicit approval for UI-initiated tool calls"),
  [the specification](https://github.com/modelcontextprotocol/ext-apps/blob/main/specification/draft/apps.mdx) (`hostContext`,
  `_meta.ui.csp`, visibility), [the SDK](https://github.com/modelcontextprotocol/ext-apps), and the
  [OpenAI Apps SDK](https://developers.openai.com/apps-sdk/build/chatgpt-ui) on the same wire.
- **Purely declarative, for contrast:** [Slack Block Kit](https://docs.slack.dev/block-kit/),
  [Adaptive Cards](https://adaptivecards.microsoft.com/) (drawn natively through the host's `HostConfig`), and Zed, whose extensions add
  no panel ([extensions](https://zed.dev/docs/extensions/developing-extensions)).

### 2.3 What the survey settles

1. **Mature hosts converge on a hybrid with a strong default.** The manifest declares the structure (commands, settings, views, status)
   and the host draws it. An iframe is the exception, discouraged unless needed (VS Code's webview guide, Figma). The sibling app's two
   tiers say the same, and its history adds a warning: a declarative tier too thin to hold a real plugin's page pushes every author to
   the escape hatch, which then has to rebuild the host's chrome.
2. **Isolation cannot be added later.** In-process UI (Obsidian, Grafana, Backstage, JetBrains, the sibling app) has no boundary.
   Grafana built a frontend sandbox afterwards and deprecated it in 13.3. Where an iframe exists, it is on its own origin, under a
   deny-by-default CSP, talking only by messages (Chrome, Figma, MCP Apps).
3. **A component kit for webviews is a long commitment.** VS Code's webview toolkit was archived when its base library was. A host that
   wants plugins to look like it draws them itself, or hands over tokens and lets authors draw.
4. **Words.** Hosts either let plugins declare words per language (VS Code, Chrome, JetBrains, Grafana) or ask nothing (Raycast is
   English only). The host never translates a plugin. Handing the locale to the plugin is the only way an iframe gets one.
5. **Sign-in is the host's surface and the extension's protocol.** VS Code's providers and Accounts menu, Raycast's PKCE client and
   Chrome's `identity` all keep the consent and the store with the host, and the provider logic with the extension. Grafana's
   `secureJsonData` is never returned to the browser. Figma, with no host sign-in, makes every author build OAuth by polling. Daoris
   takes the protocol half and leaves the store with the plugin (§7.1): its host draws the consent, the plugin keeps what it got.

## 3. The interfaces

### 3.1 Where they are declared

| Option | For | Against |
|---|---|---|
| A. Top-level manifest fields beside `steps`, `servers` and `systems` | one level fewer | a second move of a shape already built; the fields Daoris composes into its own features (steps, servers, systems) and the ones it reaches the plugin through for the person would sit mixed |
| **B. One `interfaces` object** (built) | built and named by the owner's own word; the switch is keyed by it; read before anything runs and listed in the install and update plans with no process started | one level deeper |
| C. Registered at run time by the process (Obsidian, the sibling app) | flexible | nothing is known before the process starts; the install plan cannot say what a plugin adds; a crash loses the registrations |

**Chosen: B.** Top-level fields stay what a plugin supplies to Daoris's own features: harnesses, servers, skills, steps, resources,
systems, tools and an icon. `interfaces` is what Daoris reaches the plugin through for the person and for agents, and what it keeps
for the plugin. What a plugin adds is known from its manifest, and what it shows is asked of its process. This is the sibling app's
lesson turned round: its manifest declared nothing, so the host knew a plugin's surfaces only after loading its code.

### 3.2 The interfaces

| Interface | What it is | Doors | Switched as |
|---|---|---|---|
| `commands` (built) | what the plugin does, run in rounds in its own process | the terminal; on its page; the palette; the menus of the records it names (`on`); a view's buttons | `commands`, per place |
| the same, marked `tool` | a command an agent may call | agents' tools server (§3.4) | `tools`, per place |
| `storage` (built) | a private folder for its credentials and state | handed to its processes as `DAORIS_PLUGIN_PRIVATE` | `storage`, at the machine only |
| `views` | what it shows, as a tree Daoris draws | a section of its page (`where: page`); a side tab the person adds (`where: side`) | `views`, per place |

Every word is authored (a string or a tag map, D222). Every name and id is lowercase letters, digits and dashes, at most 40
characters, once per interface. A problem in an entry refuses that entry alone: it is said on the plugin's page and by
`daoris plugin list`, and the plugin's points, steps and other entries run. Whatever names a refused or missing entry (a view's button
naming a command, `storage.forget` naming one) is drawn disabled with the reason. An interface this build does not know is named once
on the page and skipped, as a point this build lacks is (rule 6). `apiVersion` stays 1. An interface needs `hooks`. Its methods are
not points: `hook/command/run` and `hook/view/read` are never claimed at the handshake, and a process that answers one with
method-not-found draws *It does not do this*.

**Not offered:**
- A place in the activity bar (desktop rule 22).
- An entry in the menu bar's seven menus, which are Daoris's verbs (rule 28).
- Keys: no plugin command has a key, as no act that ends or removes something has one (rule 29). Collisions would make one plugin's
  install change another's keys.
- A toast: a refusal is said where its act started (rule 44).

### 3.3 Each declaration

**`commands`**
```json
{ "name": "start-stage", "title": { "en": "Start a release stage", "zh": "启动发布阶段" },
  "about": { "en": "Starts a stage that no trigger started; approvals still decide.", "zh": "启动没有触发器启动的阶段；仍由审批决定。" },
  "args": [{ "name": "stage", "type": "text", "title": { "en": "Stage", "zh": "阶段" }, "required": true }],
  "on": ["session"], "writes": true, "confirm": { "en": "Start this stage now?", "zh": "现在启动此阶段？" }, "tool": true }
```
- `name`, `about`, `interactive`, `tool` and `adopts` are the slice's, as built. `about` may now be authored, and `title` (authored,
  the name where unwritten) is what the window shows.
- `interactive` says the command asks the person (built). It is never a tool, never run for an agent, and in the window always runs in
  the round card.
- `args` are typed `text`, `number`, `boolean` or `choice` (with `choices`), each optionally `required`. The window opens a drawer
  Daoris draws (desktop rule 24). The terminal takes `--<name> <value>`, or the values in declared order. An agent's tool takes them as
  its input schema. A missing required value is asked at the window and the terminal, and refuses an agent's call.
- `on` names the records whose ⋯ and right-click menus offer the command, under a group headed by the plugin's name. The record's id is
  passed as the argument of that record's kind. This is VS Code's `menus`, folded into the command so that it is one declaration.
- `writes: true` says it changes something outside the plugin. The install plan says *changes things*, an agent's tool is annotated as
  not read-only, and the window asks once with `confirm` where one is written.
- There is no `timeoutSeconds`: a round is 60 seconds (built). Work that lasts longer is the plugin's own. It records the work in its
  storage under the run's id, answers, and shows the progress in its view; a later round or read picks it up.

**`storage`**
```json
{ "for": { "en": "The personal access token login keeps, and when Azure DevOps last refused it.", "zh": "…" }, "forget": "logout" }
```
- `for` is built. `forget` names a declared command that is not interactive, run once with `from: removal` before a removal deletes
  the folder (§8). It is bounded to 10 seconds, and its exit says whether the plugin confirmed it signed out at its service.
- Daoris reads nothing in the folder and shows only its `for` and its size.

**`views`**
```json
{ "id": "pull-requests", "title": { "en": "Pull requests", "zh": "拉取请求" }, "icon": "icons/pull-request.svg",
  "where": ["side", "page"], "places": ["repository", "workspace"], "context": ["session"] }
```
- `where` is `side`, `page` or both; `page` when unwritten.
- `places` names the kinds of place the view is about: `machine` (the default), `workspace` or `repository`. The view is asked with the
  place in force: the chosen record's, else the scope's. Where none fits, Daoris draws *Choose a repository* and asks nothing.
- `context` is what the view is told of the window beyond its place: a closed list (`session`, `quest`) of ids and names, never
  content. The page says it on the view's row: *It is told which session you are looking at*.
- `icon` follows plugins rule 26's checks. Without one, the tab wears the plugin's icon or monogram.
- `body` is `drawn` (the default) or, once §4.2 is built, `frame` with an `entry` path inside the folder.

### 3.4 Commands as agents' tools

- **One server per session.** A session in a place is handed one MCP server, `daoris-plugins`, which the driver serves for that
  session (`daoris plugin tools --session <id>`), as a plugin's declared server is handed today (rule 24). It lists each `tool`
  command of each plugin that is installed, on and sound, with `commands` and `tools` on at the session's place, named
  `<plugin>_<command>` (built: the model APIs' tool names take no dot, and no id or command name holds an underscore). It is handed
  only where it lists one.
- **Each tool.** Its description is the command's `about` in the session's language, its input schema comes from `args`, and its
  annotations say `writes`. A call is one round, `from: agent`, with the session's id and place. `said` is the text content, `data` the
  structured content, and an exit other than 0 an error result with `said`.
- **The person's hand.** In a session there is no per-call approval: the switch `tools` per place is the opt-in, and every call is a
  `plugin.command` line and a row of the session's record. Ask Daoris reaches a plugin's tools only as the person approved them, as a
  server's tools are approved (rule 24, `HelpGrants`), since its room has its own grant.
- **The agent-performed step.** A workflow's plugin step may be performed by an agent session that holds the plugin's tools and is
  given the step's goal, instead of Daoris calling `work/perform` alone (D274 point 1). Its evidence is the session's record and its
  outcome the agent's answer. It is designed with the workflows area in its own row (§11 e), after the tools reach a session.

## 4. How a plugin's UI is drawn

| | A. Drawn by Daoris from data | B. A frame the plugin serves | C. Both, drawn by default, frame on a trigger |
|---|---|---|---|
| Plugin code in the window | none | in an iframe on another origin | none until a frame is built and granted |
| Look, both themes, zoom, density | Daoris's by construction (rules 41-44) | only as far as the author uses the handed tokens | drawn: Daoris's; frame: the author's |
| Both languages | authored words and Daoris's formatting | the plugin's own translation, told the language | drawn: by construction |
| Accessibility, focus, list keys | Daoris's atoms (`ListPane`'s keys, `aria-*`) | the author's | drawn: Daoris's |
| Ceiling | the vocabulary | none | none |
| What Daoris maintains | a versioned vocabulary, append-only like the wire (D64) | an origin, a CSP, a relay, a token list | both, the frame later |
| An agent or Ask Daoris reading it | the tree is data, so a later row could read it | opaque | drawn: data |
| Evidence | Raycast, Block Kit, Adaptive Cards, VS Code's trees, the sibling app's first tier | VS Code's webviews, Figma, Chrome, MCP Apps | VS Code, Figma, the sibling app (§2.3, lessons 1-3) |

**Chosen: C, built in order.**
- **The drawn body** is the default body of every view, and built first. Its version 1 is frozen only once the Azure DevOps plugin's
  pull-request and release views, and its sign-in view, are written in it whole, interactions included (row f). Anything they need
  is added before the freeze.
- **The frame body** is designed now (§4.2) so the protocol has room for it, and is built on its trigger.

This is two bodies for one interface, which D228 weighs. They do different jobs. The drawn body is Daoris drawing the plugin's data in
Daoris's language. The frame is the plugin drawing in its own document. Each is a declared `body`, with one way to draw each. To keep
authors from fleeing to the frame, the vocabulary carries what the sibling app's thin first tier lacked: a view stack, forms, tables
with sorting and paging, filters, nested phases and progress.

### 4.1 The drawn body

**The ask.** `hook/view/read { view, inner?, place, context, language, width, query?, cursor?, revision }`, asked in a process of its
own (§5.2). `inner` is a view-stack entry `{ view, args }`, `width` is `narrow` (under 480 px) or `wide`, `query` is the view's filter
text, `cursor` is the token a `more` gave, and `revision` is the asking instance's count. The answer is
`{ ui: 1, title?, body: [node…] }`.

**The vocabulary, version 1.** Each node kind maps to an existing atom, so a drawn plugin view is Daoris's page:

| Node | Fields | Drawn as |
|---|---|---|
| `section` | `title`, `children` | `PageSection` (`Web/work/frame/ViewMain.tsx`) |
| `text` | `text` (plain; `` `code` `` inline), `tone`: `soft` or `faint` | a paragraph through `Inline` (`Web/design/atoms/Card.tsx`); no HTML, no Markdown beyond backticks |
| `fields` | `rows: [{ label, value }]` | one label track (rule 44) |
| `list` | `items: [{ id, title, line?, meta?, icon?, pill?, open?, actions? }]`, `filter?` (its placeholder), `sort?` (choices), `more?` (a cursor) | a list pane's rows; `open` pushes an inner view; the filter's text is sent as `query`; `more` asks for the next page |
| `table` | `columns: [{ id, title, align?, sort? }]`, `rows: [{ id, cells, actions? }]` (at most 200), `more?` | a table at the pane's width; below 480 px one block per row; a cell is text, a typed value, a pill or a link |
| `phases` | `steps: [{ id, title, state, at?, took?, steps?, actions? }]`, state `waiting`, `working`, `held`, `done`, `failed` or `skipped` | `PhaseList` (`Web/design/phases.tsx`); `held` waits on an approval or a person; a step's own `steps` (one level) are parallel jobs, drawn as a group |
| `progress` | `value`, `max?`, `text` | a bar with its words |
| `pill` | `text`, `tone`: `neutral`, `open`, `taken`, `done` or `declined` | `Pill` (`Web/design/atoms/Pill.tsx`); never a tone without words |
| `actions` | `[{ id, title, variant: primary, ghost or danger, command, args?, confirm? }]`, or a local `{ id, title, open \| reload \| back }` | `Button`s; `command` names a declared command, `args` its values; `confirm` asks once in place (`InlineConfirm`, `Web/work/InlineConfirm.tsx`) |
| `form` | `id`, `fields: [{ name, type, title, value, required, choices?, min?, max?, problem? }]`, `submit: { command, title }` | `FormGrid` (`Web/design/atoms/SettingForm.tsx`); the fields' values are the command's arguments by name |
| `mono` | `text` (at most 16 KiB), `follow?` | `MonoWell` (`Web/design/atoms/Marks.tsx`); `follow` keeps its end in view across reads, for a log that grows |
| `notice` | `tone`: `warn` or `neutral`, `text`, `actions?` | the lead on a warn rail (`Lead`, `Web/plugins/PluginPage.tsx`) |
| `empty` | `headline`, `body`, `actions?` | `EmptyState` (`Web/design/ui.tsx`) |
| `image` | `src` (a PNG or SVG data URI, at most 64 KiB), `alt` | an `<img>`, as an icon is (rule 26) |
| `link` | `url` (`https` or `http`), `text` | opened in the person's own browser by the shell; the host's name in its tip |
| values | `{ time }`, `{ count }`, `{ duration }`, `{ bytes }` wherever a value is written | formatted by Daoris in the viewer's language (`Web/locales/format`) |

**Acting is a command, and navigation is Daoris's.**
- A button, a row's action and a form's submit run a declared command, `from: window`, with the view's place, through the same rounds
  as every other door. An interactive command opens the round card (§7.2). There is no separate action channel.
- The command's last round may answer `then`: `stay`, `reload` (the default), `back`, or `{ go: { view, args } }`. A `said` line is
  shown in place, and an exit other than 0 is shown where the act started (rule 44).
- `open` and `go` push an inner view. Daoris draws the back control and the trail, and keeps the stack per view instance. While a
  command runs, its button is busy and the rest of the view stands.

**Each shown view is an instance in its window** (a side tab or a page section), holding its stack, filter, sort, cursor, unsent form
values and revision. Each ask carries the revision, and an answer for an older one is dropped. A newer ask stops the older ask's
process. A reread keeps a field's unsent value while the form's `id` and the field's `name` stay in the tree: the person's value wins
until it is sent or the field leaves. Two windows showing one view are two instances.

**States are Daoris's:**
- Loading: skeleton rows, never a flash of empty.
- Stale: the last good tree stays drawn and dimmed, its commands disabled, with *It did not answer; Ask again*. The stack and the
  unsent values are kept. This is what a failed, late or exited read draws.
- A tree that does not read: *This part could not be drawn*, with the reader's sentence, on the plugin's page.
- *Switched off here*, *gone* (the plugin or the view no longer declared), and *It does not do this* (the process does not answer
  `hook/view/read`).

They follow D119 §3.3's table.

**The tree is read in .NET** (a reader `PluginViews` beside `PluginCatalog`), since the verdict is the domain's (desktop rule 9):
- **Its bounds:** 256 KiB, 2,000 nodes and depth 8. Text is at most 4 KiB a node, except a `mono` node's 16 KiB. The process asked
  reads frames to the view's bound (§5.2).
- **What it allows:** only the kinds above, URLs checked to `http(s)`, images only as data URIs, and a `command` only where the plugin
  declares it (else the button is drawn disabled with the reason).
- **What it does with the rest:** an unknown kind is kept as `{ kind: "unknown" }` and drawn *This part needs a newer Daoris*.

The page receives the allow-listed tree (desktop rule 8) and draws it in `Web/plugins/view/`.

**When it is asked:**
- When the view is shown, and when its place, its context, its width class or the language changes.
- After a command run from it, unless that command answered `then: stay`, and on *Ask again*.
- On the plugin's `view/changed`, at most once in 2 seconds and only while shown.

It is never asked on a timer: a hidden view is asked nothing. At most four reads run at once across a window, the newest first. The
ask starts a process only because the person showed the view, so no process is started to find a fact out (desktop rule 32).

### 4.2 The frame body: designed now, built on its trigger

**The trigger:** the first plugin view the vocabulary says only by awkward encodings, or only with a host change made for one plugin,
named with the view (a chart, a canvas and an editor are the expected ones). Until then, a manifest declaring `body: frame` refuses
that view (*this build draws no frame*). The boundary's prototype (row l1) comes first, and may run beside row f.

**Daoris speaks a stated subset of MCP Apps' wire, not a dialect of its own.** Daoris acts as the host:
- **It hands the frame** `ui/initialize`'s `hostContext`, with the theme, Daoris's tokens as style variables, the locale and the
  container's size, and sends `host-context-changed` on any change and a teardown when the view goes.
- **The frame may call** `tools/call`, which names one of its own plugin's declared commands and runs it as a drawn button does
  (`from: window`, the round card for an interactive one), `ui/open-link` (the person's browser, after the host's name is shown), and
  its size.
- **It never gets** model messages or sampling: a plugin view is not a conversation. Every other request is answered method-not-found.
- **What portability is claimed:** a view keeping to this subset runs in another MCP Apps host only as far as that host serves the
  same tools. Daoris claims no more.

**Its isolation:**
- **The frame's origin** is a host of its own per plugin under `.localhost`. It is never a subdomain of `daoris.localhost`, so it is
  another site, and Chromium's site isolation can give it a process of its own. Whether it does is measured and reported (row l1), and
  is never a gate.
- **What is served there:** the files under the view's `entry` folder in the plugin's install folder, by the shell's resource handler.
- **The CSP:** `default-src 'none'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self';
  connect-src 'none'; frame-ancestors https://daoris.localhost`. The plugin's network is its process's, never its frame's. A later
  capability (declared domains to connect to) widens it only as the manifest declares and the install plan shows.
- **The iframe:** `sandbox="allow-scripts allow-same-origin allow-forms"`, so it is same-origin with itself only. It has no
  `allow-top-navigation` and no `allow-popups`, and an empty `allow` list.
- **The shell's `ipc` route** answers only the app's own top document. The shell checks the requesting frame through the embedding
  API (the request's frame is the main frame and its origin the app's), with `Sec-Fetch-Site: same-origin` as added evidence.
  `Sec-Fetch-Dest` alone proves nothing here, since a `fetch` made inside a frame is `empty` like the page's own.
- **Messages:** the page takes a message only where `event.source` is that view's `contentWindow` and `event.origin` is that
  plugin's origin. A message is at most 256 KiB, and at most 20 are taken a second. Daoris relays only the subset above.
- **The grant** is the install or update plan's press, whose list says *draws its own view* (§8). It is not asked a second time.

## 5. Isolation and the messaging protocol

### 5.1 One path

```
page (https://daoris.localhost, holds the person key)
  │  DAORIS.DRIVER route (Window or Person class), payload allow-listed
shell (Modules) ── driver (Driver/Plugins) ── stdio JSON-RPC ── the plugin's own process
  ▲                                                          │
  └─ STATE_CHANGED / a pushed slice  ◄── notifications of the kinds the wire takes only
```
- The page never addresses a process, and a process never reaches the page. Everything a plugin shows passes the driver's reader
  first.
- Nothing of a plugin runs in the page's document. The drawn body is data, and the frame (later) is another origin without the bridge.
- **Why the window must be the strict part:** a plugin's process already runs as the person's OS user with everything that user can
  reach (rule 1 is a boundary of crashes and languages, not of rights). What it lacks is the person key and the person's doors
  (desktop rules 11-14; plugins rule 28). A plugin's UI in the window would be the one place its code sat beside that key. So the rule
  is: **a plugin's UI is never a new way to reach the person key or the bridge.**
- **What the design does not claim:** a hostile program the person installed, running as them, can read what they can, including a
  plugin's storage and DPAPI's seal under a known purpose. Installing a plugin is trusting its code, as installing an editor extension
  is (VS Code: "the same permissions as VS Code itself"). The install and update plans say what it adds, so the trust is given
  knowingly (§8).

### 5.2 The wire

**The host asks, and the plugin answers**, each ask in a process started for it:

| Method | Params | Answer | Bound |
|---|---|---|---|
| `hook/command/run` (built) | `command`, `run`, `args`, `answers`, `from`, `session?`, `place`, `language` | `{ said, ask, open, code, then, data, exit }` | 60 s a round, 8 rounds; frames to 128 KiB |
| `hook/view/read` | `view`, `inner?`, `place`, `context`, `language`, `width`, `query?`, `cursor?`, `revision` | `{ ui, title?, body }` | 10 s; frames to 256 KiB |

- **What the built frame gains.** `run` is a 32-hex id, one per run, the same across its rounds. `args` becomes an object by name (the
  built frame sends the terminal's words as a list; the shipped commands take none). `place` is `{ kind, id }`, or `machine`. `language`
  is the viewer's, and `en` at the terminal. `session` is set where `from` is `agent`. `from` adds `agent` and `removal` to the
  built `terminal`, `window` and `adoption`, and is always Daoris's, never the caller's.
- **What the built answer gains.** `then` is honoured where the command ran from a view. `data` is an object of at most 32 KiB, for an
  agent, never shown as words. `said` keeps its 40 lines but is at most 8 KiB in all, so a round always fits its frame.
- **A lost round** (no answer in 60 s, or the process exited) is said as *It did not answer; it may have acted*, never as nothing done.
  A command that changes something records its `run` in its storage before it acts, and answers a repeated `run` from that record, as
  the work kit records an attempt before acting (`work.mjs`). *Ask again* sends the same `run`, and *Run again* a new one.

**Which process answers.** Every command round and every view read starts a process of its own, initialized as any, asked once and
stopped, as the slice's rounds are (`PluginCommandRun.RoundAsync`). A loop or work process is never asked an interface's method and
keeps its own life, and nothing a window shows keeps a process alive. Such a process has no generation to fence: an answer comes from
the process started for its ask, or not at all, and the page drops an answer for an older revision. Its frames are read to its
method's bound, whatever points the plugin speaks; a loop or work process keeps today's (`HookProcess.FrameLimit`). The terminal runs
its own rounds whether or not Daoris runs (built), since a round writes nothing of Daoris's but the machine log.

**Shared folders.** Several processes of one plugin run at once (a work process per place, a round, a read), sharing `.data/<id>` and
its storage. The kit's helpers write one record at a time under a lock: take the lock file, read the file again, change that record,
write beside, rename over, and release. The kit's work journal moves to them, so two places' processes no longer overwrite each
other's attempts (§1.6, row h).

**The plugin tells, as notifications only.** Each is taken only from a process of a plugin whose manifest declares what it is about:
- `work/step` (built).
- `view/changed { view?, place? }`, from any process of a plugin declaring `views`. It marks those views changed, and a shown one is
  read again (§4.1). An old mark costs one read, so it needs no fence.

Anything else is noise, as it is today. A plugin still asks the host for nothing (`HookPeer.cs:455-462`). Where Daoris tells a plugin
something, it is an event the plugin consumes on its points and the `work/event` relay (D274 point 3).

### 5.3 The routes

These are added to `DAORIS.DRIVER` in `Modules/Bridge/Routes/PluginsRoutes.cs`, marked as the area's other plugin routes are:

| Route | Class | Why that class |
|---|---|---|
| `PLUGIN_COMMAND` (one round) | `Window` | the window's round card and a view's buttons. A masked answer never crosses the pipe and is never in a press's record, a push or a log. The terminal runs its own rounds, and an agent calls through its tools server |
| `PLUGIN_VIEW` | `Window` | a plugin's view is the window's; a terminal or a session has no reason to draw one |
| `PLUGIN_SWITCH` | `Person` | the person's word on the machine's record; the terminal's `daoris plugin switch <id> <what> on\|off [--place <place>]` takes it, carded as any Person press |

- Each `PLUGIN_COMMAND` call is one round, and the page passes the round's bound (65 seconds) as its `timeoutMs`. `PLUGIN_VIEW` passes
  15 seconds.
- Views' change marks ride one pushed slice, `pluginViews` (plugin, view, place, seq), judged in .NET (desktop rule 6). A view's body
  stays on demand (rule 8).
- Every press declares its scopes with `[Publishes]` (rule 4).

## 6. Theme, zoom and both languages

**Drawn**
- The theme, the zoom (ZOOM1) and the density are Daoris's by construction. A plugin chooses only a node's kind, a pill's tone from
  the five (`Pill.tsx`), a text's `soft` or `faint`, and an action's variant. It has no colour, size or font of its own. Its images are
  data URIs, as its icon is, and there is no dark-theme variant (D140 rejected one).
- Every word a plugin sends is authored: a string, or a tag map shown by `authoredText` (`Web/locales/authored.ts`). Every ask that
  can return words carries the viewer's `language`, command rounds included, so a plugin may answer already in it, as the sibling
  app's plugins do. No plugin need write both shipped languages (D222).
- A structured time, count, duration or size is a typed value, which Daoris words in the viewer's language. Words a plugin writes (a
  log, its service's message) stay as written, dates in them included.
- A change of language reads every shown view again in place, keeping its stack and its unsent values (§4.1). The sibling app lost
  both until it learned this.
- Daoris's chrome around a plugin's content goes through its catalogues: the tab list's rows, *Add a plugin's view*, *Ask again*, the
  round card's buttons, *Remove…*, the states. A plugin's names are content, not glossary names (desktop rule 52 holds Daoris's own).
- `plugins try` notes, without failing, which of the shipped catalogues' languages a manifest's words lack (D222: nothing requires
  one).
- The terminal shows a command's words in English, or as written (rule 53).

**Frame (later)**
- `hostContext.theme` carries `light` or `dark`. A closed list of Daoris's tokens is handed as style variables, read from the computed
  `:root` of the theme in force: page, sunken, raised, line, the inks, the accent and its ink, the five status hues, and the font
  stack and type steps.
- The shell also serves `/.daoris/base.css` at the plugin's origin: body colours, the font and the focus ring, for an author who wants
  Daoris's look without copying it.
- `locale` is the viewer's language, and the plugin translates.

## 7. Sign-in and credentials

### 7.1 Who keeps the credential

| Option | For | Against |
|---|---|---|
| **A. The plugin, in its private storage, signing in through its own commands** (built) | the plugin manages its own task, as the owner's rule says; Daoris holds no account, sign-in way, renewal, revision or place selection; it serves tokens, device codes, a CLI's own login, certificates and browser profiles alike | a file in a folder only the account can read, not sealed; removal deletes it but revokes it at the service only where `forget` does |
| B. Daoris keeps an opaque secret per plugin and account, sealed, handed at `initialize` (the proposal) | one store; sealed | before it served one plugin, Daoris would need account ids, sign-in attempts with cancellation and expiry, renewals fenced by revision and process, the account per place, and a refusal for each secret-bearing route; refresh-capable secrets handed to several processes race; the seal stops no code running as the same user (`KeySeal`) |
| C. The OS's credential manager | the OS's own UI | a backend per OS, with no gain over A or B for a same-user threat |

**Chosen: A.** It is what is built, it is simpler, and every account question the read raised becomes the plugin's own, where the
knowledge of its service already is. A sealed store can be added later beneath the same folder contract, without a change to any
plugin. The contract below is what Daoris gives a plugin so that it can sign itself in.

### 7.2 What Daoris gives a plugin

- **Private storage** (built): the folder, limited to the person's account, handed only to that plugin's processes, read by no one
  else, and deleted at removal after `forget` (§8).
- **Rounds that ask and show** (built): a hidden value (`ask` with `masked`), an address to open (`open`) and a code (`code`). The
  terminal draws them (built). The window draws them in one **round card** (row c): the plugin's lines; the code large in the mono face
  with *Copy*; *Open the page*, in the person's own browser with the address's host shown; and a field per ask, a password field where
  masked, with *Go on* and *Cancel*. A masked value is passed to the round and never echoed, logged, pushed or kept.
- **A device sign-in is rounds.** The first round starts it and answers `open`, `code` and an ask to go on once the person has signed
  in, keeping the device code in its storage. The next round asks the service and either keeps the token or asks again. A token
  sign-in is one masked ask. A browser sign-in through a loopback redirect is left to a later round of the kit, since a round lasts
  60 seconds.
- **The adoption** (built): a command declaring `adopts` takes a git identity's API token once.
- **A refusal that names its fix.** A step's refusal on `work/perform` may carry `needs: "<command>"`, one of its plugin's declared
  interactive commands. The step fails as today. Its row on the run offers that command's *{title}*, which opens the round card, and
  *What needs you* lists it, judged in .NET from the run's record and the declaration (desktop rule 32). Once the command ends 0, the
  step offers *Try again*. Daoris names no account and no service.

The sign-in never opens in Daoris's own browser: sessions drive that browser (desktop rule 55), and an agent must never see a sign-in.

### 7.3 What the person sees

1. **The plugin's *Set up* tab** lists its commands, each with *Run*, and its storage's `for` and size. Whether it is signed in is the
   plugin's own sentence: its view, or its `status` command's lines (the shipped plugins' `status`).
2. **The round card**, from *Set up*, a view's button, the palette or a refused step.
3. **The terminal** (built): `daoris daoris-azure-devops login` prints the lines, the address and the code, and asks hidden.
4. **Agents** never sign in: an interactive command is never a tool. An agent acts through the plugin's process, which holds the
   credential, so the agent never holds a token.

**Consent** is the person's run of the sign-in command. Nothing signs in unpressed, and rule 23 keeps its spirit: Daoris starts no
sign-in; the person does, and the plugin speaks to its service.

**One way (D228).** `DAORIS_HOST_TOKEN` is gone (built). An identity's `api` token has one reader left, the adoption, so its setters
go now (row i), and the adoption ends when no identity holds one. A tool's own sign-in (a CLI that keeps its own login, the `fix` of
`PluginTools.cs`) stays a tool's check, since Daoris holds nothing of it.

## 8. The person's hand: add, arrange, opt in or out, remove

**Installing**
- The install drawer's second step (D119 §3.4) lists, beside the command, points, agents and servers, each interface entry: each
  command (with *changes things* for `writes`, *for agents* for `tool`, *takes a git identity's token* for `adopts`), the storage's
  `for` and `forget`, and each view with where it may stand and what `context` it is told.
- Then *Install*. Nothing is placed in the side bar. The page's last step offers *Add its view to the side bar* for each `side` view.

**Updating**
- The update plan (rule 15) gains an `interfaces` row per entry, compared whole by name or id: added, removed or changed
  (`PluginChange.Between` today compares nine rows and none of these).
- An entry that asks more is marked *asks more of you*: a command gaining `tool`, `writes`, `adopts` or an argument, a view gaining
  `context`, storage newly declared, and a frame body. The update still applies only on the press, which is Chrome's new-permission rule.
- A view or command no longer declared keeps its switches and its added tab unshown until the plugin is removed.

**The plugin's page** gains tabs by what it declares, and draws none empty:
1. ***Its page*** (它的页面), where it declares a `page` view: those views, a section each, in declared order.
2. ***Set up*** (设置), where it declares an interface or a point: *Commands*, *Storage*, and *Where it is on*, a row per interface and
   point with a switch per place.
3. ***About*** (关于): today's sections, Points through Source (`Web/plugins/PluginPage.tsx`), and its last 20 command runs (command,
   from, place, exit, when).

Where only *About* would show, the page is today's, unchanged. The words in both languages are proposals for the glossary (D116) and
are settled with the row that builds them.

**Arranging a side tab**
- **Adding.** A `side` view is added from its row on the plugin's page, from the tab list's menu (`ViewsMenu`, *Add a plugin's view ▸*,
  each by its plugin and title), or from the palette (*Show view: Azure DevOps: Pull requests*, a family like `go.agent`).
- **Membership is kept apart from places.** The added views are an ordered list, `daoris.pluginTabs`, of `plugin:<id>/<view>`. Each
  one's place is kept in `daoris.viewPlaces` as every movable view's is, so it moves between the side bar and the panel by the same menu
  and drag. *Reset view locations* moves it back to the right and never removes it. Each window reads both at its start and on the
  storage event, so two windows agree.
- **Removing.** A tab's right-click offers *Remove from the side bar*, which a built-in view never has. It leaves the list.
- **Dropped views.** A view of a plugin no longer installed, or no longer declared, leaves the list at the next read, as `placesOf`
  drops any unknown view. A switched-off one stays, drawn as *Switched off here*.
- **The tab's own menu** offers *Open its plugin's page*, *Ask again* and *Remove*. Every view is also a verb in the command table
  (rule 34; Obsidian's "also provide a command").

**Opting in and out, per plugin and per place**
- `plugins.json` gains `switches`, beside `disabled`:
  `{ "switches": { "<plugin>": { "tools": { "machine": false, "repository:engine": true }, "work/event": { "workspace:home": false } } } }`.
  Each key is an interface (`commands`, `tools`, `views`, `storage`) or a point the manifest names, and each place is `machine`,
  `workspace:<name>` or `repository:<id>`, the setup tree's keys (desktop rule 58).
- **Resolution.** For a place, the repository's word, else its workspace's, else the machine's, else on. `storage` reads the machine's
  word alone. `disabled` stays the whole plugin's switch, over all of these.
- **One resolver** (`PluginSwitches`) is read at every door: a command's run, the tools server's list, a view's read, the storage
  handed, and the points. A point switched off at a place is not told there (an observation), not asked there (`quest/consider`, which
  then holds nothing), and not given that place's work units (a step it performs is then one no plugin performs there, which the run
  says). Hiding the UI alone is never the switch.
- **The slice's `off` moves once.** At the loop's start a migration step writes each `off` row into `switches` at `machine` and removes
  `off`. The step ends when no home on a supported update path holds `off`.
- **An older build** reads `disabled` alone and ignores `switches`, so a plugin switched off in one place runs there under it. The
  changelog says so.
- A place's own page (Agents → Setup) lists the plugins on in it, read-only, with a door to each plugin's *Set up* (rule 35).
- Defaults are on. A side tab is never placed by default.

**Switching a plugin off** hides all it adds: its tabs (kept, not drawn), its commands, its tools and its `on` menu rows.

**Removing it** asks once (D119 §3.2). The sentence adds: *It is asked to sign out, and its own storage and its switches are deleted.
What it kept in its data folder stays, unless you tick Also delete its data folder. A sign-in it held at its service is revoked only
where it says it signed out.* The order:
1. Its processes are stopped: the loop's set lets it go, and a round or a read under way is stopped.
2. Its `forget` command runs with `from: removal`, best effort within 10 seconds. Its exit is said: *signed out at its service*, or
   *not confirmed*.
3. Its folder is moved aside and deleted (built), and its storage is deleted (built).
4. Its `disabled` row and its `switches` rows are cleared.
5. `.data/<id>` is kept (rule 5), unless the person ticked its deletion, which is off by default, since it may hold journals, browser
   profiles and credentials.

**In a plain browser** none of it shows: Plugins is shell-only, and so is everything a plugin adds.

**Many plugins.** The palette groups plugin commands under each plugin's name and searches them. Reads are bounded at four at once in
a window (§4.1). Nothing a plugin adds stands in the status bar (§11 m).

## 9. What Daoris never knows about a particular plugin

Daoris knows the interfaces, the vocabulary, the wire, the person's records, and the interfaces it owns (the change-request interface
below). It never knows:
1. **Any plugin's id, step id, step kind, schema name, outcome code, command, view or sign-in** as behaviour, in the driver, the
   modules, the service or the page. The offers list in `tools/desktop-publish/lay.mjs` is a list of folders to lay out, data rather
   than behaviour, and stays.
2. **Any plugin's words** in Daoris's catalogues. A step's outcome words come from its manifest: a step declares
   `said: { "<code>": authored }` beside `outcomes`, and the page draws `authoredText`. Daoris's catalogues hold only the generic frame:
   *{plugin} says*, *waiting on {plugin}*, *{plugin} did not answer*.
3. **Any service's address, authority, scopes or token format.**
4. **The meaning** of what a plugin keeps or a view's content. Daoris checks shapes and bounds only.
5. **A layout per plugin.** There is one drawer of trees and one round card.

**Today's knowledge (§1.5) leaves:**
- The page's `isPullRequestStep` and the pull-request codes and sentences become the step's declared interface and its `said` words.
- The driver's pull-request reading becomes the generic **change-request interface** the row names, which a step declares it
  implements. Its states are a closed grammar Daoris owns (`open`, `completed`, `abandoned`), and knowing what they prove is Daoris's:
  a branch is cleared by git's confirmation exactly as plugins rule 12 says, never by the plugin's word alone. Its words are the
  plugin's.

**A run's words outlive the plugin.** A run keeps its outcome codes as written. They are shown in the installed plugin's words for
that code, else as the code under *{plugin} said `{code}`*, so an update or a removal never makes a run unreadable. A plugin keeps a
code's meaning across its versions, which the kit's README says.

**The proof is a substitution, not a string census.** A scan for the examples' ids and step words would hit Daoris's own words
(`release`, `perform`, `browser`) and miss knowledge spelled otherwise. `PluginSubstitutionTests` installs the example plugins again
under other ids, step ids, command names and words, against a stand-in service, keeping what they declare, and requires the host's
behaviour unchanged: the same runs, landings, pages and terminal lines, the names aside. Daoris's own protocol words (the points, the
interfaces, the vocabulary's kinds and the change-request interface's states) are permitted by name.

## 10. Not chosen

From the proposal, after the read and the slice:
- **Top-level contribution fields** (`views`, `commands`, `settings`, `accounts`, `status`, `notices`). The slice's `interfaces` is
  built, is the owner's word, and is the switch's key (§3.1).
- **Daoris-sealed account secrets handed at `initialize`,** with `account/begin`, `account/finish`, `account/end`, `accounts/changed`,
  `account/renewed`, `account/expired`, `plugin-secrets/` and `PLUGIN_ACCOUNT_*`. See §7.1.
- **Daoris-kept plugin settings** (`settings`, `plugin-settings.json`, `settings/changed`). They would be a second store and resolver,
  beside the switches, for what a plugin keeps in its own storage, sets by its commands and shows in a `form` Daoris draws. The read's
  questions about inheritance, removal, validation and concurrent edits would all be Daoris's.
- **`view/act`, an action channel beside commands.** It would be two ways to act, with different confirmation and visibility (the
  read's first finding).
- **A namespaced terminal door** (`daoris plugin run <id> <command>`). It would be a second way to one command, beside the id and its
  tail (D274's notes). No word of Daoris's holds a dash (row c's test keeps it so), so a family's id never meets one. Where a tail is a
  group's word, the group wins and the whole id still reaches the plugin; an installed id with no dash that is a group's word is
  reached from the window and as tools alone, which its page says.
- **One long-lived UI process per plugin,** with subscriptions, generations and an idle stop. Per-ask processes need none.
- **Transport envelopes per message family on one process.** A per-ask process takes its method's bound.
- **A command's `timeoutSeconds` up to 600, or operation handles.** A round is 60 seconds, and longer work is the plugin's own.
- **A second grant for a frame,** beyond the install or update plan's press.
- **A string census** of the examples' words (§9).
- **A host-coded *Use this identity's token for Azure DevOps*.** It would name a plugin; `adopts` is the plugin's own declaration.
- **Status items and notices now.** They wait on §11 m.

Kept from the proposal:
- **Plugin React in the page's document.** This is the sibling app's second tier, Grafana's and Backstage's way. The document holds the
  person key and the bridge (§1.2), and Grafana's later sandbox did not reach general availability.
- **Module federation or an import map of a shared React.** Every major version would force every plugin to be rebuilt in lock-step,
  as the sibling app's records say.
- **A Daoris component kit for frames.** VS Code's toolkit was archived. A frame gets tokens and an optional base stylesheet.
- **A place in the activity bar per plugin** (VS Code's view containers). It conflicts with desktop rule 22, and a side tab and a page
  section hold what the owner asked for.
- **Keys for plugin commands,** and **menu-bar entries.** See §3.2.
- **A request from the plugin.** The wire has none by design (`HookPeer.cs:455-462`): what a plugin needs is in its storage or its
  frame.
- **A secret in the process's environment.** Every child the plugin starts would inherit it. The storage's path travels there, never a
  secret.
- **The sign-in in Daoris's own browser.** Sessions drive that browser.
- **Driving a sign-in page from the plugin** (the sibling app's host-owned web view with script execution and cookie reads). It reads
  the person's session, which no plugin needs when a device code or a token will do.
- **A settings view in the plugin's own markup.** A plugin's settings are a `form` in its drawn view.
- **Polling a view on a timer.** A hidden view is asked nothing, and a shown one is asked again when the plugin says it changed.
- **Toasts from plugins.**

## 11. The rows, in order, with their proofs

Every row's proof includes the Azure DevOps plugin where it applies, which is brought into rows c, d and f rather than proved at the
end. None is proved by a story or a mocked bridge alone (desktop *Where a change goes*). *From the slice* says what each row changes
of what is built.

| Row | What lands | From the slice | Proved by |
|---|---|---|---|
| **a** Interfaces read whole | `views` read; commands' `title`, typed `args`, `on`, `writes`, `confirm`; storage's `forget`; each problem refusing its entry alone; an unknown interface named and skipped; anything naming a refused entry disabled with its reason; `PLUGINS` answering the interfaces; the install drawer's list; the update plan's rows per entry with *asks more of you* | `PluginInterfaces.Read` stops refusing the plugin; `PluginChange.Between` gains the interfaces | `PluginInterfacesTests` per field and per problem; `PluginChange.Between` cases; `DriverModulePluginPageTests`; `PluginPage` tests; stories in both themes and languages |
| **b** Switches per place | `switches` and the resolver `PluginSwitches`, read at every door (commands, tools, views, storage, points, the work processes, the relay's `work/event`); `PLUGIN_SWITCH` and `daoris plugin switch`; *Where it is on*; removal clearing the rows | `PluginInterfaceSwitch` and `off` replaced; `off` moved once by a migration step | the resolver's table (nearest place, defaults, storage at the machine, `disabled` over all); the migration's test; `PluginStateTests` keeping `disabled`, `switches` and an unknown key; the pipe census's row; a process test of a point not asked at a place switched off |
| **c** Commands in the window, whole | the frame's `run`, `args` by name, `place`, `language`; the answer's `then` and `data`; `said` bounded in all; per-ask frame bounds; `PLUGIN_COMMAND` and the round card; *Set up*'s commands; the palette family; `on` in records' menus; `confirm`; `plugin.command` lines; a refusal's `needs` on the run and in *What needs you*; `forget` at removal; the Azure DevOps plugin's `login`, `status` and `logout` from the window | `CommandWire` and `PluginCommandRun` extended; the terminal's `args` mapped by name | `CommandWire` cases; a process test with a stand-in plugin answering a round from the window and from the terminal, a session's terminal refused, a masked answer absent from the log, the push and the pipe; the round card's web tests; the needs row's .NET judgement; a test that no group word or doctrine verb holds a dash |
| **d** Commands as agents' tools | the `daoris-plugins` server per session; its tools from `tool` commands where `commands` and `tools` are on at the session's place; `from: agent`; `data` as structured content; the session's record; Ask Daoris by approval | `tool` reaches an agent | a process test of a stand-in session's MCP client calling the Azure DevOps plugin's `status` against its stand-in; the list following a switch per place; Ask Daoris refused an unapproved tool |
| **e** The agent-performed plugin step | a step performed by an agent session holding its plugin's tools and the step's goal, its evidence the session's record, settled with the workflows area | none | its design's proofs, with the workflows area's run and review tests |
| **f** Drawn views on the plugin's page | `hook/view/read`; the reader `PluginViews` with its bounds; `Web/plugins/view/` drawing each node; buttons as commands; the view stack; instances, revisions and kept unsent values; the states, stale included; `view/changed` and `pluginViews`; four reads at once; *Its page*; `plugins try` reading each view's sample; the Azure DevOps plugin's pull-request view (filter, sort, paging, row commands), release view (parallel stages, an approval held, a growing log, start and retry) and sign-in view, written whole before version 1 freezes | none | the reader's table (a tree that reads, one too large, an unknown kind, a `javascript:` URL, a non-data image, a button naming no command); a web test per node; interaction tests of the three views (a reread keeping a draft, an older revision dropped, a process exiting mid-read drawn stale, a language change in place); stories in both themes and languages |
| **g** Side tabs | `plugin:<id>/<view>` as a `ViewId`; `daoris.pluginTabs` kept apart from `viewPlaces`; *Add a plugin's view*; the palette's *Show view*; `context` told; removal; two windows; a switched-off view against a removed one; a manifest change while shown | none | `placements.test.ts` cases (an added tab at its default place kept, reset never removing); `RightDock` and the frame tests moving a plugin's view to the panel and back; `help/where.ts` naming it |
| **h** The kit | `plugins new` writing commands, storage and a view; the storage helper writing one record under a lock; the work journal moved to it; both examples moved, with their versions and `changes` lines | the shared-journal race fixed | the kit's tests; a test of two processes' attempts both kept; each example's `node --test` |
| **i** Device sign-in and the last token setter | both examples' `login` signing in by device code where their service offers one, the token kept as the other way; the identity's `api` setters removed | the adoption stays the last reader | each example's `node --test` against its stand-in (start, go on before signing in, finish, refused); the setters' tests removed with them |
| **j** Daoris's plugin knowledge generalized | §9's moves in the page and the driver; the change-request interface; `said` words; a run's codes outliving its plugin | none | `PluginSubstitutionTests`; the landing's tests unchanged against the renamed examples |
| **k** The installed window | everything above seen on the install, in both languages and themes, at 280 px of the side bar; a plugin crashing while shown, a slow read, a refused token offered its fix, an update while shown, two windows in two languages, ten plugins installed | none | `tools/desktop.mjs` shots and checks; the owner's look |
| **l** *(on its trigger)* The frame body | l1: the boundary's prototype on the kit's CEF (the `ipc` route refusing a frame by the embedding API, the CSP holding, the marker absent, the renderer process measured); l2: the shell serving a view's `entry` at its origin; l3: MCP Apps' subset, `tools/call` mapped to commands; l4: the install plan's *draws its own view* | none | l1's evidence document; process tests per refusal; a frame view drawn on the install |
| **m** *(on its trigger)* Status items and notices | a status item the person adds as a side tab is added, or one aggregated plugin entry; notices with a place, a language and an aggregate bound | none | written with the first plugin that needs one |

## 12. What only the window and a real plugin prove, and what was the owner's

- **Only the installed window proves:** whether a drawn plugin view reads as Daoris's own at 280 px and full width, in both themes
  and languages; whether the round card is clear enough to finish a device sign-in without help; and whether the side bar holds a
  plugin's tab beside Timeline, Review and Workflow without crowding.
- **The proposal's three questions, settled with D274's note:**
  - *A status item on by default, or added?* Added by the person, like a side tab, or one aggregated entry, when row m is built.
  - *The frame's trigger?* Broadened to the first view the vocabulary says only awkwardly, with the boundary prototyped first and a
    separate renderer process measured, never a gate.
  - *Deleting `.data/<id>` at removal?* Offered as its own tick, off by default.

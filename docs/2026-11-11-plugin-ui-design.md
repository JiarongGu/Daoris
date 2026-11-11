# A plugin's own UI: what it contributes, how Daoris draws it, and how the person keeps it (PLUGINEXT1)

**Proposed, for the owner's yes, then Codex's read.** The direction is the backlog row PLUGINEXT1 (`TASKS.md`): a plugin is an
application's extension. It is reached only through interfaces it declares and events it consumes, each opted in or out per plugin
and per place. It has its own commands, its own sign-in and private storage, and tools an agent can use. Where it needs one, it has a
UI: "a page of its own (a side tab to be added or managed) or within the plugin page itself". Daoris holds no knowledge of any
particular plugin. Written by Claude Opus 5.5 on 2026-11-11 from main at `b3bc538d62`, from a read of a sibling desktop app's plugin
system and of nine well-known extension UIs (§2). Read with `docs/areas/plugins.md` (cited *plugins rule n*),
`docs/areas/desktop-app.md` (*desktop rule n*), the plugins screen design (D119, `docs/2026-10-01-plugins-screen-design.md`), the
catalogue design (D140), D52, D64, D222 and D228. Paths start at `src/Daoris.Web/src/` when they begin with `Web/`, at
`src/Daoris.Desktop/Daoris.Desktop.Driver/` with `Driver/`, and at `src/Daoris.Desktop/Daoris.Desktop.Modules/` with `Modules/`.
Nothing was built or run. Settling it amends D52 and D64 §7 ("no plugin adds a view"), D140 point 5 (no plugin markup in the page)
and plugins rule 27. It is to be settled as D274, the number the row reserves for its first slice.

## 0. In short

1. **Six contribution points, declared in the manifest beside today's fields:** `views`, `commands`, `settings`, `accounts`,
   `status` and `notices`. They are read before anything runs, with every word authored (D222). A problem refuses that contribution
   alone, never the plugin. A plugin adds no place to the activity bar, no menu to the menu bar and no key. A view stands in two
   places: a side tab the person adds to the right side bar or the panel, or a section of the plugin's own page in Plugins. A command
   is offered in the palette, on the plugin's page, in the menus of the records it names (`on`), in the terminal as
   `daoris <plugin> <command>`, and as an agent's tool (§3).
2. **Daoris draws a plugin's UI from data.** The plugin's process answers a view with a tree in a closed vocabulary (sections, rows,
   lists, tables, phases, forms, actions). Daoris draws it with its own atoms and owns its navigation, busy, empty and error states.
   That is the one way for every contribution, and the default body of every view. A **frame body** is designed now and built only
   when a plugin needs what the vocabulary cannot say: the plugin's own document, in a sandboxed iframe on an origin of its own,
   speaking MCP Apps' wire (§4).
3. **One path, no plugin code in the page.** The page asks the shell, the shell asks the driver, and the driver asks the plugin's
   process over the hook wire's new points. The page never talks to a process, and nothing of a plugin runs in the page's document,
   which holds the person key. A plugin tells Daoris things only by notifications of the kinds it declared, as it tells `work/step`
   today. The wire keeps its rule that a plugin asks the host for nothing (§5).
4. **Theme and language are Daoris's.** A drawn view wears the tokens and both themes by construction. Its tones come from the
   pill's five, and Daoris formats its times, counts and durations. Every ask carries the viewer's language, a plugin's words are
   authored values shown in the viewer's language, and a change of language reads the view again in place. A frame is handed the
   tokens and the language and translates itself (§6).
5. **Sign-in is declared, drawn by Daoris, spoken by the plugin and kept sealed.** An account declares its ways: a device code, a
   browser sign-in or a pasted token. Daoris draws the card, opens the address in the person's own browser and keeps what the plugin
   answers as an opaque secret, sealed per plugin and per account. That secret is handed only to that plugin's process, never to the
   page, a session or the log. A place is served by an account the person chooses. A missing or expired account is a *What needs you*
   row. The token taken from a git identity (`DAORIS_HOST_TOKEN`) retires once both pull-request plugins sign in (§7).
6. **The person's hand.** The install drawer and the update plan list what a plugin adds. A new frame, a new account or a newly told
   context waits for the person's press. The plugin's page gains three tabs: *Its page*, *Set up* and *About*. Side tabs are offered,
   never placed: the person adds one from the plugin's page, the tab list or the palette, then moves it like any view. Each
   contribution has a switch per place in `plugins.json`, beside `disabled`. Turning a plugin off hides what it adds. Removing it
   signs it out and forgets its secrets, its settings and its switches, and keeps `.data/<id>` (§8).
7. **What Daoris never knows** is a list with a census to hold it. No plugin's id, step, outcome code, words, service, endpoint,
   secret's meaning or layout appears in Daoris's code or catalogues. Today's pull-request knowledge in the page and the driver
   (§1.5) moves to contributed words and a generic interface (§9).
8. **Rows, in order:** the contributions read and listed; commands; accounts and sealed secrets (with the first two, the first slice
   the row names); settings; drawn views on the plugin's page; side tabs; status and notices; the Azure DevOps plugin as first user;
   the knowledge census; the installed window. The frame waits on a stated trigger (§11).

## 1. What the window and the wire hold today

### 1.1 The plugin

- A plugin is a folder with `plugin.json`, and its code runs only as its own process (plugins rule 1, lines 50-51). `apiVersion` is
  read first, and a newer one is refused (rule 2; `Driver/Plugins/Domain/PluginCatalog.cs:63`, `:361-374`). The manifest's records
  are harnesses, hooks, servers, skills, steps, resources, systems, tools and an icon (`Driver/Plugins/Domain/PluginManifest.cs:111-149`).
- The hook wire is JSON-RPC on stdio. The handshake hands the plugin its id, the home, its data folder and its declared points
  (`Driver/Plugins/HookPeer.cs:73-77`). A process may listen only on points its manifest declares (rule 6). The points are
  `HookPoints.All` (`Driver/Knowledge/Domain/PluginVocabulary.cs:43`).
- **A plugin asks the host for nothing, by design.** The one notification the wire takes is `work/step`, from a process that declared
  it. "A request FROM a plugin is not a thing this wire has" (`Driver/Plugins/HookPeer.cs:450-460`).
- Authored words are a string or a tag-to-text map. They are shown in the viewer's language, else the base language, else as written
  (D222; `Web/locales/authored.ts:1-26`). A step's `title` and `about` are already written this way (`examples/plugins/*/plugin.json`).
- An icon reaches the page as its bytes in a data URI, drawn in an `<img>`, so no plugin markup enters the document (plugins rule 26,
  lines 149-150; D140 point 5).
- Off is a row in `plugins.json` (`disabled`), never a rename (rule 5; `Driver/Plugins/PluginState.cs:9-11`). The file is saved over
  what it holds, so a key this build does not know stays as written (`PluginState.cs:73-79`).

### 1.2 The window: one origin, a bridge, a key in memory

- Every window renders the HTTP host's bundle at `https://daoris.localhost/` (desktop rule 1, lines 60-61;
  `Modules/Windows/DesktopPage.cs:16-34`). The page reaches the machine only through the shell's bridge modules (desktop rule 2), as
  `getBridge().invoke('DAORIS.DRIVER', …)` (`Web/bridge/call.ts:29-35`).
- **The bridge is a same-origin fetch.** The kit's Chromium transport posts each envelope with `fetch` to the `ipc` route the shell's
  marker names, and the shell's resource handler answers it (`createChromiumTransport` in `@shenora/react` 0.24, `dist/transport.js`).
  Any script that runs in the app's origin can therefore call any route.
- **The person key lives in the page's module memory** (`Web/data/api.ts:340-380`; desktop rules 11-13, lines 89-102). Code running in
  that document could read it. A plugin's process cannot: the key is never in a file, an environment or a URL, and every plugin press
  is the person's (plugins rule 28).
- A pushed projection is allow-listed and carries no credential (desktop rule 8). The domain's verdicts are made in .NET, and the page
  renders (rule 9).

### 1.3 The frame

- There are nine places, and a tenth needs its own decision (desktop rule 22, lines 160-161). No surface adds a place (rule 34).
- The right side bar and the panel hold movable views (rule 26). A view is a `ViewId`, placed by default and moved by the person.
  `placesOf` drops any view this build does not know (`Web/work/frame/placements.ts:9-33`, `:39-60`). Each view's name and icon are
  records over `ViewId` (`Web/work/frame/ViewsMenu.tsx:9-16`). A tab is a `DockTab` (`Web/work/frame/RightDock.tsx:20-22`).
- There is one command table. Rows the world names are families: each workspace, each theme, each agent
  (`Web/menus/commands.ts:235-263`, `:559-638`). Plugins is shell-only (`commands.ts:27-47`).
- The status bar holds ambient facts, each a press (`Web/work/frame/frame.tsx:345-380`).
- There is one light and one dark token block, and a chosen theme is `data-theme` on `<html>` (`Web/tokens.css:8-61`, `:98-164`;
  `Web/design/theme.ts:4-8`, `:34-38`). A pill has five tones: neutral, open, taken, done and declined (`Web/design/atoms/Pill.tsx:9-17`).
- A setting has one home, on the thing it is about (desktop rule 35). Settings sets nothing about a plugin and lists them read-only
  (plugins rule 29; desktop rule 57's Driver domain).

### 1.4 Sign-in and credentials today

- **A plugin's tool may sign in in a console.** A tool's ready check has a `fix` and a `SignIn` flag. Daoris runs the fix only on the
  person's press, in a console they see, and never signs in itself (`Driver/Plugins/Domain/PluginTools.cs:17-26`; plugins rule 23).
- **The pull-request plugins borrow a git identity's API token.** It is `DAORIS_HOST_TOKEN`, set around one start
  (`Driver/Plugins/HookProcess.cs:108-118`; `Driver/Plugins/PluginHostToken.cs:4-42`; plugins rule 17a, lines 88-104). Without a
  token the plugin is not started (`HandRefusals`).
- **Agents' API keys are sealed with DPAPI for the Windows user,** under a named purpose (`Driver/Agents/KeySeal.cs:6-48`, the purpose
  at `:20`). This is the precedent for a plugin's sealed store.
- **Approvals are the person's** and kept in `plugin-approvals.json` (`Driver/Plugins/HelpGrants.cs:24-30`; plugins rule 24).

### 1.5 What Daoris knows about particular plugins today

| Where | What it knows |
|---|---|
| `Web/workflow/chart/stepKinds.ts:16-27` | `isPullRequestStep`: a kind named `pull-request` is drawn as the line's step |
| `Web/workflow/runSaid.ts:27`, `:167-170`, `:303`, `:349` | pull-request codes (`merge`, `merged`, `abandoned`, `not-pushed`, …) and their sentences; a run ending at `pull-request` reads *merged* |
| `Web/projects/git/branchStanding.ts:33-55` | a pull request's states (`open`, `completed`, `abandoned`) and their sentences |
| `Web/workflow/said.ts:39`, `:87-90` | a step named `pull-request` and its set-up sentence |
| `locales/*/workflow.run.json`, `landing.json`, `work.milestone.json` | the pull request's words in Daoris's catalogues |
| `Driver/Plugins/LandingPlugins.cs`, `Driver/Workplace/PullRequestAsks.cs`, `PullRequestMoments.cs`, `Driver/Workflows/Kinds/PullRequest/PullRequestReading.cs:8-24` | `work/state`'s pull request, when it is asked and how its answer clears a branch (plugins rules 10-12) |
| `Driver/Plugins/PluginHostToken.cs`, `PlaceHostTokens.cs`, `HandRefusals.cs` | that a plugin needs a git host's API token, and how it gets one |

None of it names a plugin's id. All of it names one plugin's kind of work, which only a plugin should name (§9).

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
   `secureJsonData` is never returned to the browser. Figma, with no host sign-in, makes every author build OAuth by polling.

## 3. The contribution points

### 3.1 Where they are declared

| Option | For | Against |
|---|---|---|
| **A. Top-level manifest fields** beside `steps`, `servers` and `systems` | one way to declare (D228), read before anything runs, listed in the install and update plans with no process started | a plugin cannot add a view while it runs (none needs to; a view's content is dynamic) |
| B. A `contributes` object, as VS Code's | groups the UI | a second shape beside today's top-level declarations |
| C. Registered at run time by the process (Obsidian, the sibling app) | flexible | nothing is known before the process starts; the install plan cannot say what a plugin adds; a crash loses the registrations |

**Recommended: A.** What a plugin adds is known from its manifest, and what a contribution shows is asked of its process. This is the
sibling app's lesson turned round: its manifest declared nothing, so the host knew a plugin's surfaces only after loading its code.

### 3.2 The six points

| Point | Shows | Drawn by | Default | Per place |
|---|---|---|---|---|
| `views` | a side tab (`where: side`) and/or a section of its page (`where: page`) | Daoris, from the plugin's tree (§4.1); a frame later (§4.2) | a page section shows; a side tab is offered and added by the person | yes, by `places` |
| `commands` | the palette, its page's commands, the ⋯ and right-click menus of the records it names (`on`), the terminal, an agent's tools | Daoris (a row; an arguments drawer) | on | yes |
| `settings` | its page's *Set up* tab, by place; read-only on the place's own page | Daoris (`SettingList`, `FormGrid`) | on | the values are per place |
| `accounts` | its page's *Set up* tab; the sign-in card; *What needs you* where one is missing | Daoris | on | the account serving each place |
| `status` | one item at the status bar's end | Daoris (text, tone, tip, a command) | on, hideable | yes |
| `notices` | *Notifications*, in its messages | Daoris | on; announced on the desktop only once the person turns it on | yes |

Every word is authored (a string or a tag map, D222). Every id is lowercase letters, digits and dashes, at most 40 characters, once
per kind. A problem in a contribution refuses that contribution alone, says so on the plugin's page and by `daoris plugin list`, and
never refuses the plugin. This puts contributions with the icon and the tools under plugins rule 4, since none of them is what the
plugin's steps do. `apiVersion` stays 1: an older build ignores the new fields and draws nothing of them, as it does an unknown point
(rule 6). The manifest names the UI points in `hooks.points`, so a process that does not listen on one shows *does not draw this*
rather than failing.

**Not offered:**
- A place in the activity bar (desktop rule 22).
- An entry in the menu bar's seven menus, which are Daoris's verbs (rule 28).
- Keys: no plugin command has a key, as no act that ends or removes something has one (rule 29). Collisions would make one plugin's
  install change another's keys.
- A toast: a refusal is said where its act started (rule 44).

### 3.3 Each point's declaration

**`views`**
```json
{ "id": "pull-requests", "title": { "en": "Pull requests", "zh": "拉取请求" }, "icon": "icons/pull-request.svg",
  "where": ["side", "page"], "places": ["repository", "workspace"], "context": ["repository"] }
```
- `where` is `side`, `page` or both; `page` when unwritten.
- `places` names the kinds of place the view is about: `machine` (the default), `workspace` or `repository`. The view is asked with the
  place in force: the chosen record's, else the scope's. Where none fits, Daoris draws *Choose a repository* and asks nothing.
- `context` is what the view is told of the window beyond its place: a closed list (`session`, `quest`) of ids and names, never
  content. The page says it on the view's row: *It is told which session you are looking at*.
- `icon` follows plugins rule 26's checks. Without one, the tab wears the plugin's icon or monogram.
- `body` is `drawn` (the default) or, once §4.2 is built, `frame` with an `entry` path inside the folder.

**`commands`**
```json
{ "id": "start-stage", "title": { "en": "Start a release stage", "zh": "启动发布阶段" },
  "about": { "en": "Starts a stage that no trigger started; approvals still decide.", "zh": "启动没有触发器启动的阶段；仍由审批决定。" },
  "args": [{ "name": "stage", "type": "text", "title": { "en": "Stage", "zh": "阶段" }, "required": true }],
  "on": ["session"], "places": ["repository"], "writes": true,
  "confirm": { "en": "Start this stage now?", "zh": "现在启动此阶段？" }, "tool": true, "timeoutSeconds": 120 }
```
- `args` are typed `text`, `number`, `boolean` or `choice`. With any, the window opens a drawer that Daoris draws (desktop rule 24); the
  terminal takes `--<name> <value>`.
- `on` names the records whose ⋯ and right-click menus offer the command, under a group headed by the plugin's name. The record's id is
  passed as `record`. This is VS Code's `menus`, folded into the command so that it is one declaration.
- `writes: true` says that it changes something outside the plugin. The window asks once with `confirm`. An agent's call is a write,
  approved as a server's writes are (rule 24).
- `tool: true` offers the command to agents as a tool through the server Daoris serves for the plugin. That is the row's agent-run
  step, which uses this declaration and is built after the end-to-end work.
- **The terminal** is `daoris <plugin-id> <command> [--arg value…]`. A plugin id that is a verb of `daoris` is refused at the install,
  as a harness name this build carries is (rule 3). The terminal prints English (desktop rule 53).

**`settings`**
```json
{ "id": "watch-seconds", "type": "number", "min": 30, "max": 600, "default": 60,
  "title": { "en": "Read a release every", "zh": "跟踪发布的间隔" }, "unit": "seconds", "places": ["machine", "workspace"] }
```
- The types are `text`, `number`, `boolean` and `choice`, with no secret type: a secret is an account's (§7).
- The values are kept by Daoris in `<home>/plugin-settings.json`, keyed by plugin, then place (`machine`, `workspace:<name>`,
  `repository:<id>`), and written whole beside the file and renamed over it.
- The process is handed every place's values in `initialize` and each change in `settings/changed`. Its answer may name a problem per
  value, which is shown under the row.
- A step's graph options stay the workflow's (plugins rule 17b). A setting is what holds for the plugin wherever it runs.

**`accounts`**
```json
{ "id": "azure-devops", "title": "Azure DevOps",
  "for": { "en": "Opens pull requests, sets auto-complete and watches releases as you.", "zh": "以你的身份创建拉取请求、设置自动完成并跟踪发布。" },
  "ways": ["device-code", "token"], "places": ["workspace", "repository"],
  "tokenHelp": { "en": "A personal access token with Code (read and write) and Release (read, write and execute).", "zh": "…" } }
```
- `ways` is one or more of `device-code`, `browser` and `token`.
- The flow is §7.

**`status`**
```json
{ "id": "release", "title": { "en": "Release", "zh": "发布" }, "places": ["workspace"] }
```
- There is at most one item per plugin.
- Its text (at most 32 characters), tone, tip and command come from `status/read` and change on `status/changed`.

**`notices`**
```json
{ "id": "release-ended", "title": { "en": "A release ended", "zh": "发布已结束" } }
```
- A notice kind is declared so that the person can switch each off, and announce each on the desktop, by its name (JetBrains'
  notification groups). A notice of an undeclared kind is dropped and counted.

## 4. How a plugin's UI is drawn

| | A. Drawn by Daoris from data | B. A frame the plugin serves | C. Both, drawn by default, frame on a trigger |
|---|---|---|---|
| Plugin code in the window | none | in an iframe on another origin | none until a frame is built and granted |
| Look, both themes, zoom, density | Daoris's by construction (rules 41-44) | only as far as the author uses the handed tokens | drawn: Daoris's; frame: the author's |
| Both languages | authored words and Daoris's formatting | the plugin's own translation, told the language | drawn: by construction |
| Accessibility, focus, list keys | Daoris's atoms (`ListPane`'s keys, `aria-*`) | the author's | drawn: Daoris's |
| Ceiling | the vocabulary | none | none |
| What Daoris maintains | a versioned vocabulary, append-only like the wire (D64) | an origin, a CSP, a relay, a grant, a token list | both, the frame later |
| An agent or Ask Daoris reading it | the tree is data, so a later row could read it | opaque | drawn: data |
| Evidence | Raycast, Block Kit, Adaptive Cards, VS Code's trees, the sibling app's first tier | VS Code's webviews, Figma, Chrome, MCP Apps | VS Code, Figma, the sibling app (§2.3, lessons 1-3) |

**Recommended: C, built in order.**
- **The drawn body** is the one way for every contribution, and the default body of every view. It is built first, and the Azure
  DevOps plugin's page (its pull requests, release stages, account and commands) is wholly sayable in it.
- **The frame body** is designed now (§4.2) so the protocol has room for it. It is built when a plugin needs what the vocabulary cannot
  say: a chart, a canvas or an editor, named with the view that needs it.

This is two bodies for one contribution kind, which D228 weighs. They do different jobs. The drawn body is Daoris drawing the plugin's
data in Daoris's language. The frame is the plugin drawing in its own document. Each is a declared `body`, with one way to draw each.
To keep authors from fleeing to the frame, the vocabulary carries what the sibling app's thin first tier lacked: a view stack,
forms, tables, phases and progress.

### 4.1 The drawn body

**The ask.** `view/read { view, place, context, inner?, language, width }`, where `inner` is a view-stack entry
`{ view, args }` and `width` is `narrow` (under 480 px) or `wide`. The answer is
`{ ui: 1, title?, body: [node…], actions?: [action…] }`.

**The vocabulary, version 1.** Each node kind maps to an existing atom, so a drawn plugin view is Daoris's page:

| Node | Fields | Drawn as |
|---|---|---|
| `section` | `title`, `children` | `PageSection` (`Web/work/frame/ViewMain.tsx:238`) |
| `text` | `text` (plain; `` `code` `` inline), `tone`: `soft` or `faint` | a paragraph through `Inline` (`Web/design/atoms/Card.tsx:131`); no HTML, no Markdown beyond backticks |
| `fields` | `rows: [{ label, value }]` | one label track (rule 44) |
| `list` | `items: [{ id, title, line?, meta?, icon?, pill?, open?, actions? }]`, `more?` | a list pane's rows; `open` pushes an inner view; `more` asks for the next page |
| `table` | `columns: [{ id, title, align? }]`, `rows` (at most 200) | a table at the pane's width; below 480 px one block per row |
| `phases` | `steps: [{ title, state, at?, took? }]` with state `waiting`, `working`, `done`, `failed` or `skipped` | `PhaseList` (`Web/design/phases.tsx:157`) |
| `progress` | `value`, `max?`, `text` | a bar with its words |
| `pill` | `text`, `tone`: `neutral`, `open`, `taken`, `done` or `declined` | `Pill` (`Web/design/atoms/Pill.tsx:210`); never a tone without words |
| `actions` | `[{ id, title, variant: primary, ghost or danger, confirm?, args? }]` | `Button`s; `confirm` asks once in place (`InlineConfirm`, `Web/work/InlineConfirm.tsx:181`) |
| `form` | `id`, `fields: [{ name, type, title, value, required, problem? }]`, `submit` | `FormGrid` (`Web/design/atoms/SettingForm.tsx:142`) |
| `mono` | `text` (at most 16 KiB) | `MonoWell` (`Web/design/atoms/Marks.tsx:139`) |
| `notice` | `tone`: `warn` or `neutral`, `text`, `actions?` | the lead on a warn rail (`Lead`, `Web/plugins/PluginPage.tsx:24`) |
| `empty` | `headline`, `body`, `actions?` | `EmptyState` (`Web/design/ui.tsx:26`) |
| `image` | `src` (a PNG or SVG data URI, at most 64 KiB), `alt` | an `<img>`, as an icon is (rule 26) |
| `link` | `url` (`https` or `http`), `text` | opened in the person's own browser by the shell; the host's name in its tip |
| values | `{ time }`, `{ count }`, `{ duration }`, `{ bytes }` wherever a value is written | formatted by Daoris in the viewer's language (`Web/locales/format`) |

**Navigation is Daoris's.**
- `open` and an action's answer `{ go: { view, args } }` push an inner view. Daoris draws the back control and the trail and keeps the
  stack per tab.
- An action answers `{ then: stay | reload | back | { go } | { open: url } }`, an optional `said` line, or a `problem { code, said }`
  shown where the act started (rule 44).
- While an action runs, its button is busy and the rest of the view stands.

**States are Daoris's:** loading (skeleton rows, never a flash of empty), not answered in time (*It did not answer; Ask again*), a
tree that does not read (*This part could not be drawn* with the reader's sentence, on the plugin's page), switched off and gone. They
follow D119 §3.3's table.

**The tree is read in .NET** (a reader `PluginViews` beside `PluginCatalog`), since the verdict is the domain's (desktop rule 9):
- **Its bounds:** 256 KiB, 2,000 nodes, depth 8, text of at most 4 KiB a node.
- **What it allows:** only the kinds above, URLs checked to `http(s)`, and images only as data URIs.
- **What it does with the rest:** an unknown kind is kept as `{ kind: "unknown" }` and drawn *This part needs a newer Daoris*.

The page receives the allow-listed tree (desktop rule 8) and draws it in `Web/plugins/view/`.

**When it is asked:** when the view is shown, when an action answers `reload`, when the language changes, and on the plugin's
`view/changed`, at most once in 2 seconds and only while shown. It is never asked on a timer: a hidden view is asked nothing. The ask
starts the plugin's process if needed. The process is the person's act of showing it, so it starts no process to find a fact out
(desktop rule 32).

### 4.2 The frame body: designed now, built on its trigger

**The trigger:** the first plugin whose view the vocabulary cannot say, named with the view. Until then, a manifest declaring
`body: frame` is refused for that view (*this build draws no frame*).

**Its protocol is MCP Apps', not Daoris's own.** Daoris's plugins already hand sessions MCP servers. A view written to MCP Apps' wire
runs in Daoris and in every other host that speaks it (Claude, VS Code, ChatGPT among them, per the SDK's list), so Daoris invents no
dialect. Daoris acts as the host:
- **What it hands the frame:** `ui/initialize`'s `hostContext` with the theme, Daoris's tokens as style variables, the locale and the
  container's size, and `host-context-changed` on any change.
- **What the frame may call:** its own plugin's commands by `tools/call`, approved as §3.3 says, `ui/open-link` (the person's browser,
  after the host's name is shown) and its size.
- **What it never gets:** model messages and sampling. A plugin view is not a conversation.

**Its isolation:**
- **The frame's origin** is a host of its own per plugin under `.localhost`. It is never a subdomain of `daoris.localhost`, so it is
  another site, and Chromium's site isolation can give it a process of its own.
- **What is served there:** the files under the view's `entry` folder in the plugin's install folder, by the shell's resource handler.
- **The CSP:** `default-src 'none'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self';
  connect-src 'none'; frame-ancestors https://daoris.localhost`. The plugin's network is its process's, never its frame's.
- **The iframe:** `sandbox="allow-scripts allow-same-origin allow-forms"`, so it is same-origin with itself only. It has no
  `allow-top-navigation` and no `allow-popups`, and an empty `allow` list.
- **The shell's `ipc` route** answers only the app's own origin's top frame (`Sec-Fetch-Site: same-origin`, `Sec-Fetch-Dest` not
  `iframe`).
- **Messages:** the page takes a message only where `event.source` is that tab's `contentWindow` and `event.origin` is that plugin's
  origin. A message is at most 1 MiB, and at most 20 are taken a second. Daoris relays it to its own process (`ui/call`) through a
  `Window` route.
- **The grant:** a frame view waits for the person's *Let it draw its own view*, kept beside the approvals (`HelpGrants`), and is asked
  again where an update adds one.

Whether CEF 154 keeps all of this (a separate renderer process, the IPC refusal, the CSP) is the trigger's first measurement (§11,
row k1). It is never assumed.

## 5. Isolation and the messaging protocol

### 5.1 One path

```
page (https://daoris.localhost, holds the person key)
  │  DAORIS.DRIVER route (Window or Person class), payload allow-listed
shell (Modules) ── driver (Driver/Plugins) ── stdio JSON-RPC ── the plugin's own process
  ▲                                                          │
  └─ STATE_CHANGED / a pushed slice  ◄── notifications of declared kinds only
```
- The page never addresses a process, and a process never reaches the page. Everything a plugin shows passes the driver's reader
  first.
- Nothing of a plugin runs in the page's document. The drawn body is data, and the frame (later) is another origin without the bridge.
- **Why the window must be the strict part:** a plugin's process already runs as the person's OS user with everything that user can
  reach (rule 1 is a boundary of crashes and languages, not of rights). What it lacks is the person key and the person's doors
  (desktop rules 11-14; plugins rule 28). A plugin's UI in the window would be the one place its code sat beside that key. So the rule
  is: **a plugin's UI is never a new way to reach the person key or the bridge.**
- **What the design does not claim:** a hostile program the person installed, running as them, can read what they can, including
  DPAPI's seal under a known purpose. Installing a plugin is trusting its code, as installing an editor extension is (VS Code: "the
  same permissions as VS Code itself"). The install and update plans say what it adds, so the trust is given knowingly (§8).

### 5.2 The wire's new points

**The host asks, and the plugin answers.** Each point is declared in `hooks.points` and has a kit entry (plugins rule 6; *Where a
change goes* in the area):

| Point | Params | Answer | Bound |
|---|---|---|---|
| `view/read` | `view`, `place`, `context`, `inner?`, `language`, `width` | `{ ui, title?, body, actions? }` | 5 s |
| `view/act` | `view`, `action`, `args?`, `values?`, `place`, `language` | `{ then, said?, problem? }` | 30 s, busy while it runs |
| `command/run` | `command`, `args`, `record?`, `place`, `by` (`window`, `terminal` or `agent`), `language` | `{ said?, open?, problem? }` | the command's `timeoutSeconds`, at most 600 |
| `settings/changed` | `settings` (every place's values) | `{ problems?: [{ setting, place, said }] }` | 10 s |
| `status/read` | `status`, `place`, `language` | `{ text, tone?, tip?, command? }` | 5 s |
| `account/begin` | `account`, `way`, `place?`, `language` | `{ code, url, expiresIn }`, `{ url }` or `{ fields }` | 30 s |
| `account/finish` | `account`, `way`, `fields?` | `{ label, secret, expiresAt? }` or `{ problem }` | until the code expires, at most 15 min |
| `account/end` | `account`, `label` | `{}` | 10 s |
| `ui/call` (frame, later) | `view`, `message` | the message's answer | 30 s |

**The host tells, unanswered:** `accounts/changed { accounts }` (§7).

**The plugin tells, as notifications only.** Each is taken only from a process whose manifest declares that contribution, as
`work/step` is taken today (`HookPeer.cs:450-457`):
- `view/changed { view? }`
- `status/changed { status }`
- `notice/post { notice, title, body?, command? }`: at most 10 an hour per plugin; the title at most 80 characters and the body at
  most 400.
- `account/renewed { account, label, secret, expiresAt? }` and `account/expired { account, label }`.

Anything else is noise, as it is today. A plugin still asks the host for nothing (`HookPeer.cs:458-460`): what it needs (its
settings, its accounts' secrets) is handed to it.

**Which process answers.** UI points go to one process per plugin in the shell's loop: the one it already runs for its loop points,
or one started at the first UI ask and stopped after five minutes with none. They never go to a place's work process (rule 17a's
`PlacedWork`), which keeps its place's own hand. The kit's README says that two processes of one plugin may run at once and share
`.data/<id>`.

### 5.3 The routes

These are added to `DAORIS.DRIVER` in `Modules/Bridge/Routes/PluginsRoutes.cs`, marked as the area's other plugin routes are (`:39`,
`:310`, `:433-450`):

| Route | Class | Why that class |
|---|---|---|
| `PLUGIN_VIEW`, `PLUGIN_VIEW_ACT` | `Window` | a plugin's view is the window's; a terminal or a session has no reason to draw or press one |
| `PLUGIN_COMMAND` | `Person` | the person's press. A terminal's is carded, or runs under the tier `act` (desktop rule 15). A session's terminal is refused (plugins rule 28): an agent uses the command as a tool |
| `PLUGIN_SETTING`, `PLUGIN_SWITCH`, `PLUGIN_ACCOUNT_SIGN_IN`, `PLUGIN_ACCOUNT_SIGN_OUT`, `PLUGIN_ACCOUNT_SERVES` | `Person` | the person's acts on the machine's records |

- Status items and the views' change marks ride one pushed slice (`pluginUi`), judged in .NET (desktop rule 6). A view's body stays on
  demand (rule 8).
- Every press declares its scopes with `[Publishes]` (rule 4).

## 6. Theme, zoom and both languages

**Drawn**
- The theme, the zoom (ZOOM1) and the density are Daoris's by construction. A plugin chooses only a node's kind, a pill's tone from
  the five (`Pill.tsx:9-17`), a text's `soft` or `faint`, and an action's variant. It has no colour, size or font of its own. Its
  images are data URIs, as its icon is, and there is no dark-theme variant (D140 rejected one).
- Every word a plugin sends is authored: a string, or a tag map shown by `authoredText` (`Web/locales/authored.ts:20-26`). Each ask
  carries the viewer's `language`, so a plugin may answer already in it, as the sibling app's plugins do.
- Times, counts, durations and sizes are typed values that Daoris words in the viewer's language. A plugin never formats a date.
- A change of language reads every shown view again in place, keeping its stack and its form's unsent values. The sibling app lost
  both until it learned this.
- Daoris's chrome around a plugin's content goes through its catalogues: the tab list's rows, *Add a plugin's view*, *Ask again*,
  *Remove…*, the states. A plugin's names are content, not glossary names (desktop rule 52 holds Daoris's own).
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

### 7.1 Who keeps the secret

| Option | For | Against |
|---|---|---|
| A. The plugin keeps its own, in `.data/<id>` | Daoris knows nothing | each plugin writes its own plain-text file (the sibling app's "secret" settings); the person cannot see or remove an account in one place; a removal leaves it behind |
| **B. Daoris keeps an opaque secret per plugin and account, sealed, and hands it to that plugin's process** | one place to see, sign out and remove; never in a log, a push or a session; Daoris knows its label, not its meaning | Daoris keeps a store (it already keeps agents' keys this way, `KeySeal`) |
| C. The OS's credential manager | the OS's own UI | a per-OS backend now, with no gain over DPAPI on Windows for a same-user threat |

**Recommended: B**, as VS Code's `SecretStorage`, Raycast's encrypted per-extension store and Grafana's server-only `secureJsonData`
do.

**The store.**
- It is `<home>/plugin-secrets/<id>.json`, a record per account and label: `{ account, label, secret, expiresAt?, signedIn, serves }`.
  The secret is sealed with DPAPI for the Windows user under the purpose `daoris/plugin/<id>/<account>/1`, so a value sealed for one
  plugin does not open under another's purpose.
- It is read only by the driver. It is handed in the plugin's `initialize` (`accounts: [{ account, label, secret, places }]`), and
  again in `accounts/changed` when the person signs in or out.
- `account/renewed` (a refreshed token) writes the new secret back, but only for a label already signed in.
- No secret is in a pushed slice, a route's answer, the machine log, a process's environment or a session. The census in row c holds
  this with a canary value.

### 7.2 What the person sees

1. **The plugin's *Set up* tab, *Accounts*.** There is a row per declared account: its title, what it is `for`, then each label signed
   in (`someone@contoso`), when, where it serves and *Sign out*. *Sign in* opens the card.
2. **The card**, by way:
   - **`device-code`.** Daoris asks `account/begin` and draws the code large in the mono face, with *Copy*, *Open the page* (the
     person's own browser, the address's host shown) and the time left. The driver then awaits `account/finish`.
   - **`browser`.** The plugin answers a URL; its own process listens on its loopback redirect. Daoris opens the URL in the person's
     browser and awaits `account/finish`.
   - **`token`.** Daoris draws a password field, with the `tokenHelp` and a link to where one is made, and sends the value to
     `account/finish` for the plugin to check. The value is never echoed, kept unsealed or logged.

   The sign-in never opens in Daoris's own browser: sessions drive that browser (desktop rule 55), and an agent must never see a
   sign-in.
3. **Places.** An account serves *this machine* by default. A row per workspace or repository the plugin is on may choose another
   label (`PLUGIN_ACCOUNT_SERVES`), as a git identity serves an origin today.
4. **What needs you.** A step that needs an account in a place none serves, or one the plugin said `account/expired`, is a row judged
   in .NET (desktop rule 32): *Sign in to Azure DevOps for engine*, with *Sign in* opening the card. A step waits on it and is never
   failed by it.
5. **The terminal.** `daoris <plugin> sign-in [--account <id>] [--way device-code]` prints the code and the address and waits, as the
   person's press (rule 28). `sign-out` likewise.
6. **Ask Daoris and agents** may open the card, never complete it. An agent using a plugin's commands as tools acts through the
   plugin's process, which holds the secret. The agent never holds a token. This is the gain over handing tokens to sessions.

**Consent** is the person's press of *Sign in*, after the card shows the account's `for`. Nothing signs in unpressed (rule 23 keeps
its spirit: Daoris starts no sign-in; the person does, and the plugin speaks to its service).

**One way (D228).** Once both pull-request plugins declare an account, `DAORIS_HOST_TOKEN` retires in the same chunk: `PluginHostToken`,
`PlaceHostTokens`, `HandRefusals`, `PluginTrialHand`'s `host-token` step and plugins rule 17a's token sentences. Removing it is offered
once per identity as *Use this identity's token for Azure DevOps*, the person's press. It seals that token as a `token`-way account and
leaves nothing behind. A tool's own sign-in (a CLI that keeps its own login, the `fix` of `PluginTools.cs:17-26`) stays a tool's
check, since Daoris holds nothing of it.

## 8. The person's hand: add, arrange, opt in or out, remove

**Installing**
- The install drawer's second step (D119 §3.4) lists, beside the command, points, agents and servers, what the plugin adds: each view
  and where it may stand, each command (with *changes things* for `writes`), settings, accounts, the status item and the notice kinds.
- Then *Install*. Nothing is placed in the side bar. The page's last step offers *Add its view to the side bar* for each `side` view.

**Updating**
- The update plan (rule 15; `PluginChange.Between`) gains these rows.
- A new account, a new `context`, a new `writes` command, a new `tool` or a new frame body is marked *asks more of you*. The update
  still applies only on the press, which is Chrome's new-permission rule.

**The plugin's page.** When the plugin adds any view or setting, the page gains three tabs. With none, it is today's page, unchanged:
1. ***Its page*** (它的页面): its `page` views, a section each, in declared order.
2. ***Set up*** (设置): *Accounts*, *Settings* (by place, the value in force and where it comes from) and *What it adds*. *What it adds*
   has a row per contribution: its kind, title and where it shows, with a switch and, for one with `places`, *In…*, the places where it
   is on.
3. ***About*** (关于): today's sections, Points through Source (`Web/plugins/PluginPage.tsx:356-483`).

The words in both languages are proposals for the glossary (D116) and are settled with the row that builds them.

**Arranging a side tab**
- **Adding.** A `side` view is added from its row on the plugin's page, from the tab list's menu (`ViewsMenu`, *Add a plugin's view ▸*,
  each by its plugin and title), or from the palette (*Show view: Azure DevOps: Pull requests*, a family like `go.agent`).
- **Placing and moving.** The view joins `ViewId` as `plugin:<id>/<view>`. It is kept in `daoris.viewPlaces` with its place, as every
  movable view is (`placements.ts:91-106`), and moves between the side bar and the panel by the same menu and drag. *Reset view
  locations* puts it on the right.
- **Removing.** A tab's right-click offers *Remove from the side bar*, which a built-in view never has.
- **Dropped views.** `placesOf` drops a view of a plugin no longer installed, as it drops any unknown view. It keeps a switched-off
  plugin's view and does not draw it.
- **The tab's own menu** offers *Open its plugin's page*, *Ask again* and *Remove*. Every view is also a verb in the command table
  (rule 34; Obsidian's "also provide a command").

**Opting in and out, per plugin and per place**
- `plugins.json` gains `off`, beside `disabled`:
  `{ "off": { "<plugin>": { "view:pull-requests": ["repository:engine"], "notice:release-ended": ["machine"] } } }`. Each contribution
  `<kind>:<id>` lists the places where it is off, and `machine` means everywhere. The place keys are the setup tree's (desktop
  rule 58).
- An older build keeps the key as written (`PluginState.cs:73-79`).
- The interfaces half of PLUGINEXT1 uses the same map for points and event subscriptions (`point:work/event`, `event:channel.posted`),
  so that every switch a plugin has is one file and one section.
- A place's own page (Agents → Setup) lists the plugins on in it, read-only, with a door to each plugin's *Set up* (rule 35).
- Defaults are on, except a notice's desktop announcement, which is off until the person turns it on. A side tab is never a default
  either.

**Switching a plugin off** hides all it adds: its tabs (kept, not drawn), its commands, its status item and its `on` menu rows. Notices
already posted stay readable, and accounts stay sealed and unused.

**Removing it** asks once (D119 §3.2). The sentence adds: *It is signed out of its accounts, and its settings and switches are
forgotten. What it kept stays at `<data folder>`.* Daoris asks `account/end` for each label, best effort within 10 seconds, deletes its
secrets file and its rows in `plugin-settings.json` and `plugins.json`, and leaves `.data/<id>` (rule 5). The sibling app's uninstall
left its credentials and store rows behind. This one leaves no credential.

**In a plain browser** none of it shows: Plugins is shell-only (`commands.ts:42-44`), and so is everything a plugin adds.

## 9. What Daoris never knows about a particular plugin

Daoris knows the contribution points, the vocabulary, the wire and the person's records. It never knows:
1. **Any plugin's id, step id, step kind, schema name, outcome code, account kind, view or command** as behaviour, in the driver, the
   modules, the service or the page. The offers list in `tools/desktop-publish/lay.mjs` is a list of folders to lay out, data rather
   than behaviour, and stays.
2. **Any plugin's words** in Daoris's catalogues. A step's outcome words come from its manifest: a step declares
   `said: { "<code>": authored }` beside `outcomes`, and the page draws `authoredText`. Daoris's catalogues hold only the generic frame:
   *{plugin} says*, *waiting on {plugin}*, *{plugin} did not answer*.
3. **Any service's address, authority, scopes or token format.**
4. **The meaning** of a secret, a setting's value or a view's content. Daoris checks shapes and bounds only.
5. **A layout per plugin.** There is one drawer of trees, one settings form and one sign-in card.

**Today's knowledge (§1.5) leaves:**
- The page's `isPullRequestStep` and the pull-request codes and sentences become a step's declared variant and `said` words.
- The driver's pull-request reading becomes the generic *change request* interface the row names ("later pull request"). Its states
  are a closed grammar (`open`, `completed`, `abandoned`), its words are the plugin's, and the branch is cleared by git's confirmation
  exactly as plugins rule 12 says.
- `DAORIS_HOST_TOKEN` leaves with §7.

The interface itself is the interfaces design's. This design names the UI half and the census.

**The census.** `PluginKnowledgeCensusTests` reads the driver's, the modules', the service's and the page's sources (tests, stories and
fixtures excepted) for every id, step id, kind and schema declared under `examples/plugins/`. Each hit fails, naming its file and
line, against an allow-list that starts as §1.5's rows and must be empty when row i lands.

## 10. Not chosen

- **Plugin React in the page's document.** This is the sibling app's second tier, Grafana's and Backstage's way. The document holds the
  person key and the bridge (§1.2), and Grafana's later sandbox did not reach general availability.
- **Module federation or an import map of a shared React.** Every major version would force every plugin to be rebuilt in lock-step,
  as the sibling app's records say.
- **A Daoris component kit for frames.** VS Code's toolkit was archived. A frame gets tokens and an optional base stylesheet.
- **A place in the activity bar per plugin** (VS Code's view containers). It conflicts with desktop rule 22, and a side tab and a page
  section hold what the owner asked for.
- **Keys for plugin commands,** and **menu-bar entries.** See §3.2.
- **A request from the plugin** (`secrets/read`). The wire has none by design (`HookPeer.cs:458-460`), and a plugin needs nothing
  handed to it on demand: its accounts and settings are handed at the start and on each change.
- **The secret in the process's environment,** as `DAORIS_HOST_TOKEN` is. Every child the plugin starts inherits it, and a sign-in
  after the start would need a restart.
- **The sign-in in Daoris's own browser.** Sessions drive that browser.
- **Driving a sign-in page from the plugin** (the sibling app's host-owned web view with script execution and cookie reads). It reads
  the person's session, which no plugin needs when a device code or a redirect will do.
- **A settings view the plugin draws.** Settings are a schema Daoris draws on the thing they are about. The sibling app deleted its
  schema view for its one manager modal, and its settings then had no home but the plugin's own drawing.
- **Polling a view on a timer.** A hidden view is asked nothing, and a shown one is asked again when the plugin says it changed.
- **Toasts from plugins.**
- **A notice raising a *What needs you* row.** .NET judges what needs the person.

## 11. The rows, in order, with their proofs

The first slice the row names (commands, private storage and sign-in, D274) is rows b and c, with a, and comes with the end-to-end
work. Every row's proof includes the Azure DevOps plugin where it applies, and none is proved by a story or a mocked bridge alone
(desktop *Where a change goes*).

| Row | What lands | Proved by |
|---|---|---|
| **a** Contributions read and listed | the six fields in `PluginCatalog` and `PluginManifest`, each problem refusing that contribution alone; `PLUGINS` answering them; the install drawer's list; the update plan's rows and *asks more of you*; *What it adds* with its switches; `plugins.json`'s `off` kept and edited | `PluginCatalogTests` cases per field and per problem; `PluginChange.Between` cases; `PluginStateTests` keeping `disabled`, `off` and an unknown key; `DriverModulePluginPageTests`; `PluginPage` tests; stories in both themes and languages |
| **b** Commands | `command/run` and its kit entry; `PLUGIN_COMMAND`; the palette family; the page's commands; `on` in records' menus; the arguments drawer; `daoris <plugin> <command>` with reserved ids | `HookTests` on the frame; `PluginKitTests` (a point with no kit entry fails); the pipe census's row; `commands.test.ts`' family case; a process test with a stand-in plugin answering a command from the window and from the terminal, and refused from a session's terminal |
| **c** Accounts and sealed secrets | `account/begin`, `finish` and `end`; `accounts/changed`, `account/renewed` and `expired`; `plugin-secrets/`; the card in three ways; *serves* per place; the *What needs you* row; removal signing out; the Azure DevOps plugin signing in by device code; `DAORIS_HOST_TOKEN` retired with both pull-request plugins | seal tests (one plugin's secret does not open under another's purpose); a stand-in identity provider in a process test, with no network (code, wait, finish, renew, expire); the pushed slices' allow-list holding a canary secret out; the machine log's census; `HandRefusals`' and `PluginHostToken`'s tests removed with them |
| **d** Settings | the declarations; `plugin-settings.json`; the form by place; the place's read-only list; `settings/changed` with its problems | reader cases per type and bound; a process test of a change reaching a running plugin; `SettingList` tests |
| **e** Drawn views on the plugin's page | `view/read`, `view/act` and `view/changed`; the reader `PluginViews` with its bounds; `Web/plugins/view/` drawing each node; the view stack; the states; *Its page*; `plugins try` reading each view's sample tree | the reader's table (a tree that reads, one too large, an unknown kind, a `javascript:` URL, a non-data image); a web test per node; stories of every node in both themes and languages; the kit's trial of a view |
| **f** Side tabs | `plugin:<id>/<view>` in `ViewId`; `placesOf` keeping and dropping; *Add a plugin's view*; the palette's *Show view*; `context` told; removal | `placements.test.ts` cases; `RightDock` and the frame tests moving a plugin's view to the panel and back; `help/where.ts` naming it |
| **g** Status items and notices | `status/read` and `status/changed`; the `pluginUi` slice; the bar's item; `notice/post` in *Notifications* with its bound; the announce switch | `StateSlicesTests` for the slice; `NotificationsView` tests; a process test of the notice bound and an undeclared kind dropped |
| **h** The Azure DevOps plugin as first user | its account, a *Pull requests* view (`side`, `page`), a release status item, the *Start a release stage* command, `said` words for its steps; version and `changes` line | its `node --test` against its `stand-in.mjs`; the installed window (row j) |
| **i** Daoris's plugin knowledge generalized | §9's moves in the page and the driver, with the interfaces design's change-request interface | `PluginKnowledgeCensusTests` with an empty allow-list |
| **j** The installed window | everything above seen on the install, in both languages and themes and at 280 px of the side bar | `tools/desktop.mjs` shots; the owner's look |
| **k** *(held on its trigger)* The frame body | k1: a measurement on the kit's CEF (the plugin origin in another renderer process, the `ipc` route refusing it, the CSP holding, the marker absent); k2: the shell serving a view's `entry` at its origin; k3: MCP Apps' host half, relayed to `ui/call`; k4: the grant | k1's evidence document; process tests per refusal; a frame view drawn on the install |

## 12. What only the window and a real plugin prove, and what is the owner's

- **Only the installed window proves:** whether a drawn plugin view reads as Daoris's own at 280 px and full width, in both themes
  and languages; whether the device-code card is clear enough to finish a sign-in without help; and whether the side bar holds a
  plugin's tab beside Timeline, Review and Workflow without crowding.
- **The owner's to decide:**
  - Whether a plugin's status item is on by default (proposed) or added like a side tab.
  - Whether the frame's trigger is right, or the frame should be built with the drawn body.
  - Whether removing a plugin should also offer to delete `.data/<id>` (today it never does, rule 5).

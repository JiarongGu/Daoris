# Plugins leave the repository: Daoris.Plugins, the workshop, and a package source (PLUGREPO2, PLUGDIST1)

**Carried by:** PLUGREPO2 and PLUGDIST1 in `TASKS.md`, and the owner's answer of 2026-10-01 on where a plugin
Daoris makes should live. **Status:** design, written before the build; **D120** records what it settles. It
extends `docs/2026-09-23-plugin-design.md` (D64, D100, D101, D103) and the Plugins view's design,
`docs/2026-10-01-plugins-screen-design.md` (D119). Its rows use DEV2's lane ids (`daoris.lanes.json`).

> *"this repo will for our default plugin development and later will be release to nuget so we can use nuget
> as plugin site and we need to find a way to filter on nuget for daoris plugin (or we can use npm, you can
> decide …)"* — the owner, 2026-10-01, having made an empty folder for it beside this repository.

The owner then answered where a plugin Daoris makes should live. In short:
- A plugin Daoris makes when a person asks for one, through Ask Daoris or the Plugins view, lives by default
  in **the Daoris home**, or in another place the person sets. It needs no repository.
- **Daoris.Plugins is where Daoris's own plugins are developed**, and it is what gets published to the
  searchable package source.
- Daoris may make a plugin **in whatever place is set up**, Daoris.Plugins among them, or **in the home and then
  hand it over** to Daoris.Plugins to be versioned and published.

That changes one premise of PLUG9 (Ask Daoris design §9.7, D103): *a plugin is made as an ask at the repository
that holds plugins*. §2 is the new rule.

## 0. What is true today

Read from the code at `d618cbb`.

- **Daoris's own plugins are tracked examples** in `examples/plugins/`:

  | Plugin | What it does | Who reads the tracked folder |
  |---|---|---|
  | `hold-by-title` | speaks at `quest/consider` and `session/ended` | the family rehearsal installs it with `daoris plugin add`; the deployment rehearsal copies it into a scratch home |
  | `browser` | hands sessions the Playwright MCP on a profile of its own | the family rehearsal installs it as *the plugin that declares a server* |
  | `github-pull-request`, `azure-devops-pull-request` | land work at `work/land` (D100) | the CLI's `landing-plugins.test.ts` (WSR4) drives them with a fake `gh` and `az`; `PluginOfferTests` checks them; the publish offers them |
  | `in-app-browser` | hands sessions Daoris's own browser (D78) | `PluginOfferTests`; the publish offers it |

- **The offers** are laid out by `tools/desktop-publish.mjs`: `OFFERED_PLUGINS` names the three, and
  `layOffers(join(repoRoot, 'examples', 'plugins'), to)` copies them into `app/plugin-offers/`. `tools/desktop.mjs`
  lays the same folders beside its scratch home. The layout is a twin of the CLI's `OFFERS_DIR` and the driver's
  `PluginOffers.Layout`.
- **The two landing plugins predate the kit** (D101). They have no `plugin.test.mjs`, and their tests live in
  Daoris's CLI suite, which imports a TypeScript fixture helper.
- **Making a plugin** (PLUG9): Ask Daoris proposes an ask at the repository that holds plugins, and with none,
  *where plugins live is the person's call*. No plugins repository exists (D101's stated gap).
- **Where an installed plugin came from** is `.daoris-source.json`: `{ "folder": … }` or `{ "offer": … }` (D103).
- **No package source.** D64 §7 says *no registry, no marketplace, no catalogue of third-party plugins*, and
  D119 §8 repeats it.
- **The folder `Daoris.Plugins`** sits beside this repository's folder, empty, not yet a git repository.

## 1. The survey: two sibling plugins repositories

Two repositories in the family keep their plugins in a repository of their own. Both were read and neither was
written. They are called here *the first* and *the second*.

| | The first sibling's plugins repository | The second sibling's plugins repository |
|---|---|---|
| **A plugin is** | a .NET assembly, one per plugin, loaded into the host | a folder with a `plugin.json` (`id`, integer `apiVersion`, `capabilities`, `entry`, `definitions`). It may hold an assembly, a data-only definitions bundle, a UI bundle, or all three |
| **Layout** | one folder per plugin, each a project; a solution; the host's SDK assemblies vendored in `lib/` and refreshed by the host repository's dev tool | one folder per plugin; a props file that finds the host repository's checkout beside it, for the SDK project; a tests project over a fake host |
| **The contract it builds against** | the vendored SDK assemblies, copied across repositories by hand | the host's SDK project, referenced across repositories by a relative path |
| **Its own gate** | a dev tool (`bump`, `pack`), a pre-commit hook that refuses a changed plugin without a new version | an offline gate (plugins against the fake host, UI unit tests, a lint of the definitions), and a slower gate that boots the real host from the sibling checkout |
| **Versions** | two tiers: the repository's release version, tagged, and each plugin's own version, bumped when it changes | each plugin's `version` in its manifest |
| **Packaging** | one zip per plugin; a large model fetched in CI from a URL and checked by SHA-256, never committed | one zip per plugin, `…-plugin-<id>-<version>.zip`; older zips of the same id dropped |
| **Distribution** | GitHub Releases, from a manually dispatched workflow. It publishes each zip and a public manifest asset, and carries an unchanged zip forward from the last release | none: a person imports a zip on the host's Plugins screen |
| **How the host finds and trusts it** | reads the latest release's manifest asset through the GitHub API; trusts an asset whose address starts with the releases prefix, with **no hash**. The addresses are layered: a shipped file, an operator's override, and code constants | the person picks the archive. The host extracts it with a guard against paths that escape, checks the id's shape, and loads it into its own collectible load context |
| **Updates** | staged in a pending folder and swapped at the next start, because a loaded assembly is locked | an import replaces the plugin and keeps its data; an uninstall removes the data too |

**What Daoris.Plugins takes from them.**
- **One folder per plugin with its own manifest and tests, and an offline gate at the root.** Both converged on
  it, and the kit already makes that folder (D101).
- **A version per plugin, independent of any release, and a change that must bump it.** A published package
  version never changes (§4), so the first sibling's guard becomes the pack's refusal (§5.2).
- **A guard on extraction.** The second sibling's zip-slip rule applies to a package (§5.7).
- **Layered addresses.** The first sibling's shipped-default-plus-override shape is how package sources work
  here (§5.3), and it is the shape TOOLS1 asks for its downloads.

**What Daoris.Plugins does not need, and why that matters.**
- **No SDK, and no sibling checkout.** Both siblings build against the host's code, vendored or by a path to
  the host's checkout, because their plugins load into the host. A Daoris plugin speaks a wire (D64), and the
  kit's wire test imports only Node (D101). Daoris.Plugins therefore never references this repository, and a
  clone of it builds and tests alone. That independence is a rule for its brief (§3.2).
- **No pending swap.** A Daoris plugin is a process, stopped before its folder is swapped (D103).
- **No trust by address prefix.** The first sibling trusts a URL. Daoris checks the package's hash against the
  source's own record and keeps it (§5.6).

## 2. Where a plugin is made: the workshop

### 2.1 The setting

A machine has one **plugin workshop**: where Daoris makes a plugin a person asks for. It has three kinds.

| Kind | Where a plugin is made | Who makes it |
|---|---|---|
| **The home** (the default) | `<home>/plugins/.workshop/<id>/` | a workshop session (§2.2) |
| **A folder you choose** | `<folder>/<id>/`, a folder outside any git checkout and outside the home | a workshop session, the same way |
| **A repository** | that registered repository's checkout, in its own session tree | its own driven session, from an ask addressed to it: PLUG9 as built |

- **Stored in its own file**, `<home>/plugin-sources.json`, beside `plugins.json`:
  `{ "workshop": { "in": "home" } }`, `{ "in": "folder", "folder": "<whole path>" }` or
  `{ "in": "repository", "repository": "<name>" }`. An absent file or field means the home. The same file holds
  the package sources (§5.3).
  - **Why not `driver.json`.** The driver's `DriverConfig.ToJson` writes fixed keys (the intake design's
    finding), so an older desktop build that toggles anything would drop a field it does not know.
  - **Why not `plugins.json`.** D103 declined it for the same reason: two writers and every older build
    rewrite it whole.
  - A new file has no older writer. Each twin preserves what it has no field for.
- **Two doors (D50), and Ask Daoris.**
  - The terminal: `daoris plugin workshop` prints it; `daoris plugin workshop home|folder <path>|repository <name>`
    sets it. This is an offline edit of one file under the home, like the rest of `daoris plugin`.
  - The screen: Settings → Driver, a row *Plugin workshop* under the *Plugins folder* row that D119 §5 put
    there, with *Choose…* for a folder and a picker of registered repositories. The Plugins view's ⋯ gains
    *Where plugins are made…*, a door to that row.
  - Ask Daoris: a setting door, `workshop`, judged against the same refusals (D110).
- **Refusals**, the same at both doors (twins `plugins.ts` and `PluginSources.cs`):
  - a folder inside the home, or holding it;
  - a folder inside a git working tree, asked with `rev-parse --show-toplevel` from the folder, the session
    trees' guard. Git walks up, and a workshop inside someone's checkout would make every plugin a stray
    folder in their repository;
  - a folder that holds anything but a workshop;
  - a repository that is not registered, or has no checkout here.
- **Changing it moves nothing.** Plugins made in the old workshop stay where they are, and the notice under
  the row names the old place once.

### 2.2 A workshop session

- **The workshop is a small git repository Daoris owns.** The folder is Daoris's own, like the session trees it
  keeps under the home, so writing it is not reaching into anyone's repository. It holds one folder per plugin.
  The driver `git init`s it on first use, and every session's work is commits there. That gives the session's
  review a real diff (D52), and a second ask about the same plugin grows from the first.
- **The room.** Like the intake's room (D65), the driver renders the workshop's `AGENTS.md` at each open, with
  `CLAUDE.md` importing it. It holds the kit's rules and points table (plugin design §9), how to test, and what a
  session there never does. Both files are listed in the workshop's `.git/info/exclude`, so the history holds
  only plugins.
- **The ask.** Making a plugin is an ask addressed to `workshop`, carrying the plugin's id and its points. It is
  machine-local: the service accepts that receiver in local mode only, and it never travels (D47 §4). The loop
  picks such asks up as it picks intake asks, in the slots the quests left. For a new id, the driver first runs
  the kit's `new` into the workshop, so the plugin starts with its wire test and README, and commits the
  scaffold.
- **One session at a time per workshop.** As the intake room's lock is its process, so is the workshop's. A
  machine's workshop is one working tree.
- **What it may do.** The spawn hands it rules allowing edits in the workshop, `node --test`, and git in the
  workshop, and nothing across (D72, D107). It needs no network: a plugin's tests fake its platform's tools
  (plugin design §9, rule 6).
- **The record** is a chat-kind record, as the intake's is: it serves no quest, and its repository reads
  `workshop`. Its tree is the workshop, and its base is the workshop's HEAD at start.
- **How it ends is observed, never self-reported** (D46 §4). The exit code comes first. Then the driver runs
  the plugin's own tests with PLUGUI1g's runner (a copy under `.trials/`, D119 §4.3) and the kit's trial, and
  the record carries both verdicts.
  - With a commit and passing tests, the session is `completed` and the ask is answered.
  - With failing tests, it parks `awaiting-person` with the output, as a parked intake does. The person gives
    it another turn, or closes the ask.

### 2.3 A plugin in the workshop: try, test, install

- **Nothing in the workshop runs on the loop.** `.workshop/` is a dot-folder the catalogue skips, as it skips
  `.data/`, `.trials/` and `.checks/`, and a folder-kind workshop is outside `plugins/`. A plugin in development
  is never installed by being there.
- **The Plugins view gains a group, *In the workshop***, after *Off* and before *Daoris's own plugins*. It holds
  one row per plugin being made, with its version and its last tests' verdict. Its page is D119's page with:
  - Points, Agents and Servers as written;
  - Tests, with *Try* (a folder's trial, D101) and *Run tests* (PLUGUI1g);
  - Source, reading *In the workshop*;
  - the acts *Install*, *Send to a repository…* and *Remove from the workshop*, the last a discard that asks once.
- **Install** is `PluginInstall.Add` from `<workshop>/<id>`, which adds and never replaces. It is refused while a
  workshop session works, so a half-made plugin is never copied. The card shows the last tests' verdict. The
  record is D103's folder record, `{ "folder": "<workshop>/<id>" }`, unchanged.
- **Update keeps D103's rule.** It re-reads the recorded folder and shows the five rows before the press. So a
  plugin installed from the workshop takes a later session's work by an update, like any folder source.
- **Ask Daoris** adds a workshop plugin by id (`plugin_propose` add with `workshop: <id>`), never by path.

### 2.4 Handing a plugin to a repository

A plugin made in the home that should be versioned and published moves to Daoris.Plugins, or to any plugins
repository. **The workshop never writes into that repository. The copy lands through the repository's own door.**

- **The door is a quest.** *Send to a repository…*, or `daoris-driver plugins send <id> --to <repository>`,
  publishes a quest to a registered repository asking it to take the plugin in.
  - The title is *Take in the plugin `<id>` <version>*.
  - The body says what the plugin does, its points and servers, its tests' and trial's last verdicts, and the
    steps: place it by the repository's own layout, run its gate, land by its rule.
  - The plugin folder travels as **one zip attachment**, since a quest carries at most ten files of at most
    20 MB each (`QuestExchange.MaxAttachments`, `MaxAttachmentBytes`). The workshop's history does not travel.
    The repository's history of the plugin starts at its own commit.
- **The receiving repository's session does the writing**, on its branch, landed by its workspace's rule (D87).
  That is `repository-owns-its-work` exactly.
- **A person may copy the folder by hand** into their own checkout, since it is theirs to do. Daoris offers no
  button that writes into a checkout.
- **Afterwards, the installed plugin can take its new source.** D103's update takes a plugin's recorded source.
  It gains **a source the person names**:
  - at a terminal, `daoris plugin update <id> --from <folder>` (a folder within a registered checkout) or
    `--from package` (§5.8);
  - on the page, *Update from…* in Source.
  The plan shows the new source beside the five rows before the press, and applying records the new source.
  Nothing changes source silently, and a plugin with no record still cannot be updated until the person names
  one.

### 2.5 What this changes in PLUG9 and D103

- **Making a plugin is an ask at the machine's workshop.** That is the home unless the person set otherwise.
  An ask goes to a repository only when the workshop names one, which is PLUG9's path as built. Ask Daoris's
  room section *Making a plugin* says which, from the setting. PLUG9's *with none, where plugins live is the
  person's call* becomes *with none set, the home*.
- **An ask to a repository carries the kit's scaffold** for its id and points, as one attachment made by
  `plugins new` into a scratch folder under the home. A session there has neither `daoris` nor `daoris-driver`
  on its PATH (D101), so it could not scaffold one itself.
- **D103's update gains a named source** (§2.4). Its refusals, its five rows and its swap are unchanged.

## 3. Daoris.Plugins

### 3.1 Its layout

```
Daoris.Plugins/
  AGENTS.md                 the brief, above the doctrine region Daoris writes
  CLAUDE.md                 the import (D59, D117)
  daoris.json, daoris.lock  "harness": "agents", "target": ".agents", its domain
  .agents/                  knowledge and skills, synced
  .claude/skills/           the mirror, synced (D117 §3.2)
  package.json              private; "test": "node --test"; engines node >= 22
  plugins/
    <id>/                   one folder per plugin, as the kit makes it (D101 §9)
      plugin.json
      plugin.mjs            or whatever its manifest's command starts
      plugin.test.mjs       the kit's wire test, and the plugin's own cases
      README.md             the wire, the rules, and `## What it needs`
  test/repository.test.mjs  the repository's own checks over every plugin folder
  pack/                     the pack project and its script (PLUGDIST1b)
  .github/workflows/release.yml   manual dispatch, dry run by default (PLUGDIST1b)
  dist/, local/             ignored: pack output, private notes
```

A plugin's folder is named by its **plugin id**, never by its package id (§5.1). `plugins/` keeps the plugins
apart from the repository's own files.

### 3.2 Its brief, gate and lanes

- **The brief** (its `AGENTS.md`, above the region) says what the repository is, in the canon's plain words,
  with no path of Daoris's:
  - Daoris's own plugins, each a folder the kit made. A plugin runs on a machine as its person, and is installed
    only by that person's press.
  - Each plugin's README carries the kit's rules.
  - The gate is `npm test`, which is `node --test`, and needs only Node 22 or later. Nothing of Daoris's is on a
    session's PATH, and nothing here references Daoris's source.
  - A change to a plugin's folder bumps that plugin's version, and a published version never changes.
  - Publishing and pushing are the owner's.
  - A test never reaches a network.
- **The gate**: each plugin's wire test and its own cases, plus `test/repository.test.mjs`, which checks every
  folder under `plugins/`:
  - the manifest's id is the folder's name, its `apiVersion` is an integer, and its version is SemVer;
  - a plugin that speaks carries a `plugin.test.mjs`;
  - a plugin with hooks or servers has a README with `## What it needs`.

  It declares that gate to Daoris's queue by D115's gate declaration once DEV5 lands; until then its brief
  names it.
- **No lanes at first.** A repository with no lanes file is one lane (D115). When two plugins are worked side by
  side, lanes of one plugin folder each come then.
- **Its doctrine** is the canon's core on D117's `agents` layout, with the `windows-machine` pack: its plugins run
  on Windows as their person, and a `.cmd` tool's quoting is one of that pack's traps.

### 3.3 How it is connected

It is a repository like any other, with an agent of its own:
- **Registered** in the owner's workspace, the one this repository is in, with a declared `domain`:
  - it owns Daoris's own plugins;
  - it accepts a plugin asked for with what it does and the point it speaks on, a change to one of its plugins
    with the case that needs it, and a plugin handed over from a workshop (§2.4).
- **Drivable, with trees on**, so its sessions work in their own trees (D51).
- **Its landing rule is `merge`** into its line while it has no remote. Once the owner gives it one, `branch`
  with `github-pull-request` lands its work as a pull request, made by one of its own plugins.
- **On the owner's machine the workshop names it** (`daoris plugin workshop repository Daoris.Plugins`), so the
  plugins Daoris makes for itself are made there. A fresh install's default stays the home.
- **Its sessions read Daoris's checkout** when a quest points there, since reading across is on by default in a
  workspace (READ1, D107). They never write it.

### 3.4 Setting it up: what the parent runs

The folder is empty and has no owner, so initializing it is set-up, not reaching in (`repository-owns-its-work`).
Set-up ends at the first commit, and every plugin after it is asked for (PLUGREPO2b onwards). **It waits for
LAYOUT3**, which gives `init` its `--harness agents` (D117). The CLI runs from this checkout's source, since
nothing is on npm yet (D105, D117 §6.6). Paths below are relative to this repository's root. `<workspace>` is the
owner's workspace, and `<host>` is the running install's local host address.

```
cd ../Daoris.Plugins
git init -b main
node ../Daoris/src/Daoris.Cli/bin/daoris.mjs init --harness agents
#   daoris.json: add "windows-machine" to packs; fill "domain" (§3.3)
#   AGENTS.md:   write the brief (§3.2) above the region; package.json, .gitignore (dist/, local/, node_modules/)
node ../Daoris/src/Daoris.Cli/bin/daoris.mjs sync
node ../Daoris/src/Daoris.Cli/bin/daoris.mjs check
git add -A
git commit -m "chore: set up Daoris.Plugins for its own agent" -m "Co-Authored-By: …"
DAORIS_SERVICE_URL=<host> node ../Daoris/src/Daoris.Cli/bin/daoris.mjs connect --workspace <workspace>
node ../Daoris/src/Daoris.Cli/bin/daoris.mjs driver drive Daoris.Plugins
node ../Daoris/src/Daoris.Cli/bin/daoris.mjs driver trees Daoris.Plugins on
node ../Daoris/src/Daoris.Cli/bin/daoris.mjs driver landing Daoris.Plugins merge
node ../Daoris/src/Daoris.Cli/bin/daoris.mjs plugin workshop repository Daoris.Plugins   # once WORKSHOP1a lands
```

- The last five lines edit this machine's registry and home. Repositories' *Add* and Settings are the same
  doors (D50). They are the parent's to run against the running install, when no session there is running.
- A remote for it, and a push, are the owner's.

### 3.5 What moves, and what stays

| Now | After | Why |
|---|---|---|
| `examples/plugins/github-pull-request`, `azure-devops-pull-request` | `plugins/` in Daoris.Plugins, each with a `plugin.test.mjs` that carries the kit's wire test and the WSR4 cases, rewritten in plain Node: a fake `gh` or `az` first on the PATH, and a bare repository as `origin` | they are products Daoris offers, not a contract its gates hold |
| `examples/plugins/in-app-browser` | `plugins/in-app-browser`, with a test that its server keeps `${browser}` and `${data}` as written and pins its MCP version | the same |
| `src/Daoris.Cli/test/landing-plugins.test.ts` | retired once its cases pass in Daoris.Plugins | a repository's plugins are tested where they live |
| `PluginOfferTests`' check of the tracked examples | a check over a fixture package (PLUGDIST1a), and the deployment rehearsal's check of the real offers | the offers are no longer in this tree |
| `examples/plugins/hold-by-title` | **stays** | the rehearsals' speaking plugin: the wire's contract at `quest/consider` and `session/ended`, driven by two gates |
| `examples/plugins/browser` | **stays** | the family rehearsal's *plugin that declares a server*; it is not offered (D103) |

- **The order matters.** The three leave `examples/` only after the offers can be laid out from packages
  (PLUGDIST1g), which waits for the first publish (PLUGDIST1f). Until then, `examples/` still feeds the
  publish, and its three copies are frozen: a change to them is made in Daoris.Plugins.
- A Daoris gate never reads a sibling checkout, so none can depend on Daoris.Plugins being beside it.

### 3.6 The offers, after the move

D103's offers stay: the install still carries Daoris's own plugins so a first start needs no network. They are
laid out **from packages**:
- `plugin-offers.json` (tools lane) pins each offer: `{ package, version, sha512 }`.
- The publish fetches each from the package source, checks the hash, and extracts its `plugin/` folder into
  `app/plugin-offers/<plugin id>/`.
- Fetched packages are cached in the workspace's ignored `_fixtures/`, so a republish works offline.
- `--offers-feed <folder>` takes a folder of packages instead, for a plugin not yet published.
- Each offer's folder keeps its package identity beside it, so an installed offer can be updated from the
  package source (§5.8).
- The dev loop lays out the offers from the same cache, and says so when it has none.

## 4. The package source: NuGet or npm

### 4.1 NuGet, as its maker documents it

- **A custom package type is allowed.** A package can declare one or more package types. A custom type is a
  string shaped like a package id, of 1 to 100 characters, with an optional `System.Version`, set in a nuspec's
  `<packageTypes><packageType name="…" version="…"/></packageTypes>` or with MSBuild's `PackageType`.
  [set-package-type], [msbuild-targets]
  - Visual Studio and nuget.exe refuse to install a package whose type they do not know. [set-package-type],
    and the NuGet issue it links, NuGet/Home#10468
  - nuget.org accepted a push of a custom type: `Cake.PackageType.Addin` carries `CakeAddin`, seen in its nuspec
    and its catalog leaf here. That package is unlisted, so the search below cannot return it.
- **The search filters by type.** `SearchQueryService/3.5.0` adds `packageType`. It filters *to only packages
  that have at least one package type matching the package type name*, and an invalid type gives an empty result.
  - Results carry `id`, `version`, `description`, `authors`, `owners`, `totalDownloads`, `verified`, `versions`
    (each with its `downloads`), `deprecation`, `vulnerabilities` and `packageTypes`.
  - An unlisted package never appears.
  - nuget.org caps `take` at 1,000 and `skip` at 3,000. [search]
  - The query syntax filters `owner:` with an exact, case-insensitive match. [finding-and-choosing]
- **Observed on 2026-10-01**, through the service index's search `@id`:
  - `packageType=McpServer` gave 274 hits, each typed `McpServer`, and `packageType=mcpserver` the same 274: the
    filter is not case-sensitive;
  - `packageType=DotnetPlatform` gave 2,661, each typed `DotnetPlatform`;
  - `packageType=ContosoExtension` gave 0.
  - A custom third-party type returning hits was not observed, since the one found is unlisted. The first
    publish proves it for `DaorisPlugin` (§10).
- **Downloads need no client.** `PackageBaseAddress/3.0.0`, the flat container, serves:
  - `{@id}/{lower_id}/index.json`, every version, listed or not;
  - `{@id}/{lower_id}/{lower_version}/{lower_id}.{lower_version}.nupkg`;
  - `…/{lower_id}.nuspec`.
  Each is plain `GET`, and a missing package is a 404. [package-base-address]
- **Hashes are published.** A catalog leaf carries `packageHash` (standard base64), `packageHashAlgorithm`
  (always `SHA512` on nuget.org), `packageSize` and `packageTypes` with their versions. It is reached from the
  registration's `catalogEntry.@id`, and `RegistrationsBaseUrl/3.6.0` is gzip-compressed and includes SemVer 2.0.0.
  [catalog], [registration]
  - **Checked here:** the SHA-512 of `cake.packagetype.addin 1.0.0-rc0002`, as the flat container served it
    (repository-signed, with `.signature.p7s` inside), equalled its catalog `packageHash`, and its size equalled
    `packageSize`.
- **Signing.** nuget.org's `RepositorySignatures/5.0.0` lists its signing certificates, with SHA-256
  fingerprints, and `allRepositorySigned`. [repository-signatures]
- **Ownership and verification.**
  - Owners are nuget.org accounts: nuget.org assigns ownership to whoever publishes, and does not use the
    nuspec's `authors`. [publish-a-package]
  - **ID prefix reservation**, applied for by mail to account@nuget.org, rejects a push under the prefix from
    anyone but its owners, and marks their packages *verified*, which search returns. [id-prefix-reservation]
- **Publishing.**
  - It needs a free nuget.org account and an API key scoped by glob, operation and expiry, or **trusted
    publishing**. There, a GitHub Actions or GitLab workflow exchanges an OIDC token for a one-hour, single-use
    key (`NuGet/login@v1`). [publish-a-package], [trusted-publishing]
  - Pushed packages are virus-checked and indexed, usually within 15 minutes.
  - The size limit is about 250 MB. [publish-a-package]

### 4.2 npm, as its maker documents it

- **The search** is `GET /-/v1/search?text=…&size=…&from=…`, with `size` at most 250. `text` takes the
  qualifiers `keywords:` (`,` for or, `+` for and, `,-` to exclude), `author:`, `maintainer:`, `scope:`,
  `not:unstable`, `is:insecure` and `boost-exact:false`. [registry-api]
  - **Observed on 2026-10-01**, each result carries `package` (name, version, description, keywords, publisher,
    maintainers, links), `score`, `searchScore`, `downloads` (weekly and monthly), `dependents`, `updated` and
    `flags`.
  - `keywords:daoris-plugin` gave 0, so the hyphenated keyword was not split into two words that would each
    match thousands.
  - `keywords:homebridge-plugin`, a keyword a real plugin ecosystem finds its plugins by, returned packages
    carrying it.
- **Tarballs and integrity.** A version's `dist` holds `tarball`, `shasum`, `integrity` (an SRI SHA-512),
  `signatures`, `attestations`, `fileCount` and `unpackedSize` (observed).
  - Registry signatures are ECDSA P-256 over `${name}@${version}:${integrity}`, with keys at
    `/-/npm/v1/keys`. [registry-signatures]
  - **Provenance** links a package to its source commit and build. It is generated on GitHub Actions or GitLab
    and logged in Sigstore's transparency log, and trusted publishing generates it without a flag.
    [provenance], [trusted-publishers]
- **Namespace.** A scope belongs to its user or organization, and only its owner publishes under it.
  [about-scopes]
- **Publishing.** `npm publish` from the folder needs only Node. Trusted publishing needs npm 11.5.1 and Node
  22.14.0 or later. [trusted-publishers]

### 4.3 Compared

| | NuGet, a `DaorisPlugin` package type | npm, a `daoris-plugin` keyword |
|---|---|---|
| **How exact the filter is** | exact on a field meaning *what this package is for*, applied by the server, not case-sensitive. The type's version can carry the wire's `apiVersion`, readable from the `.nuspec` before download | exact in the observations here: the row's premise that keywords filter less precisely did not survive checking. But a keyword is one tag among the author's own, with no version and no meaning beyond the word |
| **What a publisher must do** | an account; a package whose type is `DaorisPlugin`, with the plugin under `plugin/`, packed by `dotnet pack` (a .NET SDK) or nuget.exe; a push by key or trusted publishing | an account; a `package.json` with the keyword, and a scope for a namespace; `npm publish` with Node alone |
| **A plugin in Node** | fits: the package is the folder, and Daoris runs no install | fits in the tarball. But everything an npm author expects to happen at install (dependencies, `bin` links, lifecycle scripts) silently would not, since Daoris runs no `npm install` |
| **A plugin as a native executable** | fits, up to about 250 MB | fits |
| **What Daoris implements to search, install and update, with no client** | HTTP and JSON; the service index; search; the flat container; the registration and catalog leaf for the hash (two small requests); a zip (`System.IO.Compression`) | HTTP and JSON; search; the packument; a gzip tar (`System.Formats.Tar`) |
| **Integrity** | SHA-512 from the catalog, checked here against a served package; a repository signature on every package (CMS, `.signature.p7s`; checking it needs `System.Security.Cryptography.Pkcs` and NuGet's signed-content rules); no provenance a consumer can read | SRI SHA-512 inline; ECDSA registry signatures, checkable with the base library; provenance, whose check needs a Sigstore verifier Daoris does not have |
| **Open to other publishers** | anyone can publish the type. `owners` are real accounts, and `verified` is an identity-reviewed prefix owner, both in the search result | anyone can add the keyword. A scope is owned, free and immediate, with no identity review |
| **The owner's word** | *"we can use nuget as plugin site"* | *"or we can use npm, you can decide"* |

### 4.4 The recommendation: NuGet

1. **The filter names what the package is.** `packageType=DaorisPlugin` is a declaration of intended use, and its
   version carries the wire's `apiVersion`, so a plugin this build cannot run is refused from the `.nuspec`
   before its package is downloaded. A `DaorisPlugin` package is not one Visual Studio or nuget.exe will put
   into a project. A keyword on npm is a tag on an ordinary dependency.
2. **The source's rules match a plugin's.** A Daoris plugin package declares no dependencies (§5.1) and runs no
   installer. npm's model is exactly the machinery Daoris would not run, so an author's npm habits would not
   apply, and nothing would say so.
3. **Who published it is in the search result, and can be relied on.** `owners` are accounts, never the free
   text of `authors`. `verified` means a reserved prefix whose owner nuget.org reviewed.
4. **Integrity takes plain HTTPS and SHA-512.** The catalog's hash matched a served package here.
5. **The owner leans to it, and it fits the reader.** The desktop and the driver are .NET, and a package is a zip.

**What npm does better, and what D120 does about it.**
- **Inline integrity, registry signatures and provenance.** D120 pins the SHA-512 in the source record. The
  repository signature's check is a held row (PLUGDIST1h). Trusted publishing narrows who can push, though
  nothing gives NuGet's consumers provenance.
- **A free, immediate namespace.** Until the prefix is reserved, *Daoris's own* is decided by the owner account,
  which is an account and not free text. The reservation is the owner's application.
- **Publishing with Node alone.** Daoris.Plugins needs the .NET SDK only in its pack step, which its release
  workflow runs. Its gate stays `node --test` (D101).

**Rejected: npm**, for 1 to 3 above. **Rejected: both**: two source kinds would double the reader, the source
record, the trust rules and *Find plugins*, for no plugin that needs the second. **Rejected: a release asset
listing Daoris's own plugins**, the first sibling's way: it serves one publisher, has no search, and is what the
bundled offers already are. **Rejected: an index Daoris runs**: a registry to operate, which is what D24 and D57
declined.

## 5. A Daoris plugin package, and how Daoris reads a package source

### 5.1 The package

- **Its type**: `<packageType name="DaorisPlugin" version="<apiVersion>.0"/>`, and no other type. A package
  without it is not a plugin, whatever its name says.
- **Its content**: the plugin folder under `plugin/` (`plugin/plugin.json` and what sits beside it). The package
  root keeps NuGet's own parts (`<id>.nuspec`, `[Content_Types].xml`, `_rels/`, `package/`, `.signature.p7s`),
  which Daoris never extracts.
- **No dependencies.** A package whose nuspec declares any is refused. A plugin carries everything it runs,
  and Daoris installs nothing else.
- **Its version is its plugin's.** The nuspec's version must equal `plugin.json`'s, and a package whose
  plugin says another version is refused.
- **Its id is the publisher's; the plugin's id is the plugin's.** Package ids are global on nuget.org, and
  plugin ids key the machine's folders and the landing rules that name them. For Daoris's own:
  `Daoris.Plugins.<Name>`, such as `Daoris.Plugins.GitHubPullRequest`, under a reserved `Daoris.` prefix once
  the owner has it.
- **Its metadata**: a description, a license expression, the plugin's README as the package readme (nuget.org
  renders it, and the service index's `ReadmeUriTemplate/6.13.0` gives its address), the repository URL and
  commit, and the tag `daoris` for people browsing nuget.org. The filter is the type, never the tag.

### 5.2 Publishing, the owner's

- **The pack** lives in Daoris.Plugins: one small project packs one plugin folder
  (`dotnet pack pack/DaorisPlugin.proj -p:PluginFolder=plugins/<id>`), with:
  - `NoBuild`, `IncludeBuildOutput=false` and `SuppressDependenciesWhenPacking`;
  - `PackageType=DaorisPlugin, <api>.0`;
  - the folder as `None` items with `Pack=true` and `PackagePath=plugin/`;
  - `PackageReadmeFile` and `PackageLicenseExpression`. [msbuild-targets]
- **It refuses** a version the flat container already lists, since a published version never changes. It also
  refuses a nuspec version that differs from the plugin's.
- **The release workflow** is dispatched by hand, with `dry_run` true by default, as this repository's is. It
  packs every plugin whose version is not yet published, and pushes by trusted publishing. There is no
  long-lived key in the repository.
- **Every push is the owner's press** (CLAUDE.md: publish and release are the owner's). A Daoris session in
  Daoris.Plugins never runs the workflow.

### 5.3 Package sources

- **The shipped default** is nuget.org's service index, `https://api.nuget.org/v3/index.json`, a constant until
  TOOLS1's resource file carries it.
- **A person may add** another v3 source that answers without credentials, or **a folder of `.nupkg` files**,
  which serves offline installs, a team's share, and a plugin not yet published. They are listed in
  `<home>/plugin-sources.json`'s `packages`.
- **Both doors**: `daoris plugin sources [add|remove <url|folder>]` and Settings → Driver's *Package sources* row.
  Ask Daoris proposes adding one as a setting door, since a new source widens what can be installed (D74's
  widening waits for the person).
- **A source must offer `SearchQueryService/3.5.0`** to be searched. One without it cannot filter by type, and is
  refused with that reason. A source with no catalog cannot give a hash, so installing from it says that the
  package is checked against nothing, and the person presses *Install anyway*. A folder source is the person's
  own files.

### 5.4 Search

`GET {search}?q=<text>&packageType=DaorisPlugin&prerelease=<bool>&semVerLevel=2.0.0&take=20&skip=<n>`.
- **The reader checks each result's `packageTypes` again.** A server *may* implement the filter, and a result
  without the type is dropped.
- **Daoris's own** is a result owned by Daoris's publisher account, and `verified` once the prefix is reserved.
  Both are read from the result and never from the name.
- **An unreachable source is information, not failure** (D48 §6). The view says the source did not answer, and
  keeps the last results.

### 5.5 Versions and details

- **The versions** come from the search's `versions`, the listed ones with their downloads.
- **For an installed version**, the registration says whether it has since been unlisted, deprecated (with its
  reasons and alternative), or reported vulnerable.
- **The apiVersion** comes from the `.nuspec`'s package type version, before any download. One this build does not
  speak is refused naming both numbers, D64's rule.

### 5.6 Download and integrity

- **The hash comes first.** The registration's `catalogEntry.@id` leads to the catalog leaf, whose `packageHash`,
  `packageHashAlgorithm` and `packageSize` it reads.
- **The download** comes from the flat container, into `<home>/plugins/.downloads/`, a dot-folder the catalogue
  skips.
- **The check**: its SHA-512 and its size must equal the leaf's, or the file is deleted and the install refused,
  naming both hashes.
- **The kept hash.** The source record keeps it (§5.8). A later fetch of the same version that hashes otherwise
  is refused, since a version on nuget.org never changes.
- **The repository signature** is not checked in the first build. Held as PLUGDIST1h, it would check the signer
  against the source's `RepositorySignatures` fingerprints.

### 5.7 Install

1. **Read before extract.** The package must hold the type, no dependencies, and `plugin/plugin.json`.
2. **Extract `plugin/**` only**, into a staging folder under the home. An entry that is rooted, holds `..` or a
   drive, or would land outside the stage refuses the whole package: the second sibling's rule.
3. **Read the stage** with the catalogue's own reader (`PluginCatalog.ReadAsWritten`, `RefusedByThisBuild`). The
   manifest's version must equal the package's.
4. **Add through `PluginInstall.Add`**, which never replaces an installed id. A plugin id already here is
   refused, naming where the installed one came from.
5. **Record the source**: `{ "package": "<Id>", "version": "<v>", "sha512": "<base64>", "source": "<index url>" }`.
6. **Another publisher's plugin lands off.** A row in `plugins.json`'s disabled list is written in the same act.
   The person reads its page and turns it on. Daoris's own lands as an offer does. Installing runs nothing at
   the press either way (D103).

### 5.8 Update from where it came from

D103's rule, with a package as the source.
- **Its source** is the same package on the same source.
- **The plan** looks up the newest listed version, stable unless the installed one is a preview, or the version
  the person names. It fetches and checks that package, reads its manifest in a stage, and shows the five rows
  D103 compares. The press swaps the folder as today, with `.data/` untouched and the new hash recorded.
- **The list's *update available*** (D119 §2) for a package source comes from the search's listed versions, at
  most once an hour per plugin and when its page opens. The five rows come with the plan, since reading them
  means a download.
- **An installed offer whose folder carries its package identity** updates from the package source when it
  answers, and from the install's own offer when it does not.
- **The CLI does not reach a network.** Only `service.ts` may (the dogfood test), and a package source is not a
  knowledge service. The CLI's `daoris plugin update <id>` for a package source answers, with exit 2 in a moved
  verb's shape, that `daoris-driver plugins update <id>` does it. It still reads and lists the record, since the
  record is a twin of `PluginInstall.cs`.

### 5.9 Open to other publishers: what it means for trust

- **The type is a claim, not an endorsement.** Anyone can publish a `DaorisPlugin` package, and anyone can put
  any plugin id in its manifest.
- **What Daoris shows before a press**:
  - the publisher (the owners, linked to their profile by `OwnerDetailsUriTemplate/6.11.0`);
  - *verified* or not;
  - downloads, in total and for this version;
  - when it was published, its license, its source repository and commit;
  - any deprecation or vulnerability;
  - **what it would run**: the command, points, agents and servers, read from the fetched package's manifest,
    as the folder card shows them (PLUG9).
- **Daoris's own and everyone else's are apart** in the list, by the owner account and never by the name. The
  sentence over another publisher's *Install* says it was not reviewed by Daoris, and that a plugin runs on this
  machine as the person.
- **What stays true** for any source: a plugin is a process beside the host, never code in it (D64); it lands off
  unless it is Daoris's own; a hold fails closed (D64 §4); and removing it is one press (D119).
- **nuget.org's own checks** (a virus check at push, and reservation of a prefix) are the source's. Daoris
  names them for what they are and claims nothing past them.

### 5.10 What the machine log keeps

New D94 events, with names, counts and flags only:
- `plugin.searched`: `results`, `ms`, `source` (the kind: `nuget`, `feed` or `folder`). **Never the search
  text**, which is the person's words (D94 §5);
- `plugin.fetched`: `package`, `version`, `bytes`, `ms`, `verified` (the hash matched);
- `plugin.installed`: `plugin`, `from` (`offer`, `folder`, `workshop` or `package`), `on`.

## 6. *Find plugins* on the Plugins view

D119's view gains a way to look beyond this machine. Every name follows D116. The build measures each name's
budget in both languages with `names:check`.

### 6.1 The list: two modes

- **The list pane's header gains a choice**: *On this machine* (D119's groups, with *In the workshop*) or
  *Find*.
  - `＋` gains *Find plugins…* first, which opens *Find*.
  - The mode is remembered (`daoris.list.plugins.mode`).
  - D119 §8's *no search in the list* stands for the plugins on this machine. *Find* searches a package source,
    which is a different list.
- ***Find*** holds:
  - a search field, with the text kept while the view lives and never logged;
  - a choice of *Daoris's own* (the start) or *All publishers*;
  - *Include previews*;
  - the source, when there is more than one.
- **A result's row** holds its title, its publisher with the verified mark, its downloads, its newest version, and
  *Installed* or *Update to <v>* when this machine has it. Paging is *More*, 20 at a time.
- **States**:
  - a first search shows skeleton rows;
  - no results: *Nothing found*, with the query and the filter named;
  - a source that did not answer: the sentence in place, with the last results kept;
  - no source at all: the row in Settings → Driver as the action.

### 6.2 A package's page, in the main area

- **The header**:
  - the title, the version chooser (listed versions, newest first, each with its downloads and date; previews
    only when included; deprecated and vulnerable versions marked), and *Daoris's own* or the publisher;
  - the acts: *Install* (primary), *Update to <v>* for a plugin installed from this package, or *Installed* as a
    door to that plugin's page. *Open on nuget.org*, through `PackageDetailsUriTemplate/5.1.0`, opens in the
    browser the person chose (BRW7).
- **Sections**, in order:
  1. **Trust**: publisher, verified, downloads, published, license, source repository and commit, any
     deprecation or vulnerability, and for another publisher the sentence of §5.9.
  2. **What it runs**: Points, Agents and Servers as D119 §3.2 draws them. They are read from the fetched package,
     so the section loads after the download and its check, with the hash said.
  3. **What it needs**: the README's bullets, verbatim (D103).
  4. **Versions**: the full list, with downloads and dates.
- **An installed plugin's page** (D119) gains in Source: *From <source>, package `<Id>` <version>*, its hash
  shortened with the whole in a tip, *Update…* as today, and *Update from…* (§2.4).
- **Refusals**, verbatim in place of the section that needs them:
  - not a Daoris plugin;
  - a plugin API this build does not speak;
  - the hash differed;
  - the plugin is already installed from elsewhere;
  - the source did not answer.

### 6.3 Names

Proposed, each within its kind's budget by count. The build's `names:check` measures them.

| Key | Kind | English | 中文 |
|---|---|---|---|
| `plugin.list.find` | menu | Find plugins… | 查找插件… |
| `plugin.mode.here` | choice | On this machine | 本机 |
| `plugin.mode.find` | choice | Find | 查找 |
| `plugin.find.own` | choice | Daoris's own | Daoris 自带 |
| `plugin.find.all` | choice | All publishers | 所有发布者 |
| `plugin.find.previews` | choice | Include previews | 包括预览版 |
| `plugin.group.workshop` | section | In the workshop ({{count}}) | 工坊中（{{count}}） |
| `plugin.section.trust` | section | Trust | 可信度 |
| `plugin.field.publisher` | field | Publisher | 发布者 |
| `plugin.field.downloads` | field | Downloads | 下载量 |
| `plugin.verified` | status | verified | 已验证 |
| `plugin.package.install` | button | Install | 安装 |
| `plugin.package.update` | button | Update to {{version}} | 更新到 {{version}} |
| `plugin.package.open` | button | Open on nuget.org | 在 nuget.org 打开 |
| `plugin.workshop.send` | menu | Send to a repository… | 发送到仓库… |
| `plugin.source.from` | menu | Update from… | 从其他来源更新… |
| `settings.driver.workshop` | field | Plugin workshop | 插件工坊 |
| `settings.driver.packageSources` | field | Package sources | 包源 |

**The glossary gains three terms:**
- **workshop, 工坊**: where this machine makes a plugin a person asks for. It avoids *repository*, which is
  something else.
- **package source, 包源**: NuGet's own term, in its own Chinese. The code says `PackageSource`. It never says
  *catalogue*, which is the driver's word for the installed plugins (`PluginCatalog`), and would be one word for
  two things (D116 §2).
- **publisher, 发布者**: the account that published a package, never its free-text authors.

### 6.4 Two doors (D50)

| On the view | At a terminal | |
|---|---|---|
| *Find* | `daoris-driver plugins find <text> [--all] [--previews] [--json]` | new |
| a package's page | `daoris-driver plugins show --package <Id> [--version <v>] [--json]` | new |
| *Install* | `daoris-driver plugins install <Id> [--version <v>] [--yes]`; without `--yes` it prints the card | new |
| *Update to <v>* | `daoris-driver plugins update <id> [--version <v>] [--yes]` | new, for a package source |
| *Update from…* | `daoris plugin update <id> --from <folder>\|package` | new |
| *Send to a repository…* | `daoris-driver plugins send <id> --to <repository>` | new |
| *Plugin workshop* | `daoris plugin workshop …` | new |
| *Package sources* | `daoris plugin sources …` | new |
| a package file | `daoris-driver plugins install <file.nupkg>` | new |

**Why the network verbs are `daoris-driver`'s.** Only the CLI's `service.ts` may hold a network primitive, and a
package source is not a knowledge service. The CLI keeps the offline edits of files under the home.

### 6.5 Ask Daoris (D110)

| Control | Its answer |
|---|---|
| *Find*, the filters, the version chooser | exempt: they read and change nothing |
| *Install* from a package | door: `plugin_propose` add with `package`, `version` and `source`, judged by fetching and checking the package. The card is §5.9's, and never names a path |
| *Update to <v>* | door: `plugin_propose` update, as today, its card naming the version |
| *Update from…* | door: `plugin_propose` update with `from`, a registered checkout's folder or `package` |
| *Send to a repository…* | door: `ask_propose` to that repository, with the plugin as its attachment |
| *Plugin workshop*, *Package sources* | door: setting doors `workshop` and `sources` |
| *Open on nuget.org* | exempt: it opens a page and changes nothing |

**The room's *Making a plugin*** says where this machine makes plugins, from the setting (§2.5). **Its plugin
list** names each plugin's source kind.

## 7. The build

Every row is ready for `TASKS.md`. A row that runs in Daoris.Plugins is **an ask to that repository**, taken by
its own session with the kit: the owner's *let Daoris develop the plugins*. Its proof is that repository's gate,
and the parent's look at the landed diff. D120 decides all of it, so no row needs a decision number of its own,
unless building it finds something D120 did not decide.

**Order:**
1. PLUGREPO2a (after LAYOUT3), then PLUGREPO2b, then PLUGREPO2c ∥ PLUGREPO2d.
2. PLUGDIST1a ∥ WORKSHOP1a.
3. PLUGDIST1b after PLUGREPO2c, PLUGREPO2d and PLUGDIST1a. PLUGDIST1c after 1a, and 1d after 1c.
4. PLUGDIST1e after 1d and PLUGUI1f.
5. PLUGDIST1f, the owner's, after 1b. PLUGDIST1g after 1f, then PLUGREPO2e.
6. WORKSHOP1b after WORKSHOP1a and PLUGUI1g (the tests' runner), then WORKSHOP1c (and PLUGUI1f), then
   WORKSHOP1d.

### Daoris.Plugins

- [ ] **PLUGREPO2a — set Daoris.Plugins up** (the parent; set-up, §3.4; after LAYOUT3).
  - **What:** `git init`, `init --harness agents` with `windows-machine`, the domain, the brief, `package.json`,
    `.gitignore`, `sync`, `check`, the first commit. Then register it, drive it with trees on, and land by
    `merge`.
  - **Lanes:** none here: another folder, set up.
  - **Proof:** `check` clean there. It shows in Repositories as adopted on the `agents` layout (LAYOUT8's facts,
    once built). `daoris driver list` shows it drivable, with its rule.
- [ ] **PLUGREPO2b — its own gate** (an ask to Daoris.Plugins).
  - **What:** `test/repository.test.mjs` (§3.2), `npm test`, and the brief's conventions.
  - **Proof:** its `node --test`, landed by its rule.
- [ ] **PLUGREPO2c — the two landing plugins** (an ask; after 2b).
  - **What:** each with the kit's wire test and the WSR4 cases in plain Node, read across from this repository's
    `landing-plugins.test.ts` (READ1). A fake `gh` or `az` goes first on the PATH, a bare repository is `origin`,
    and nothing reaches a network. Each README keeps `## What it needs`.
  - **Proof:** its `node --test`; the parent's `daoris-driver plugins try <folder>` exits 0 on each.
- [ ] **PLUGREPO2d — the in-app browser's plugin** (an ask; after 2b).
  - **What:** the manifest and README, and a test that the server keeps `${browser}` and `${data}` as written
    and pins its MCP version.
  - **Proof:** its `node --test`. The parent installs it from the checkout on the dev loop's scratch machine, and
    a session there is handed `browser`.
- [ ] **PLUGREPO2e — this repository lets them go** (after PLUGDIST1g).
  - **What:**
    - the three leave `examples/plugins/`, and `landing-plugins.test.ts` retires;
    - `PluginOfferTests`' tracked-examples fact is rewritten over PLUGDIST1a's fixture package;
    - `examples/README.md` and plugin design §6 say where the offers come from;
    - the twins document's offers row follows.
    `hold-by-title` and `browser` stay.
  - **Lanes:** laneless (examples, docs, `.claude/knowledge/twins.md`); `cli` (the test); `driver` (the offer test).
  - **Proof:** `verify`, whose CLI count drops by the retired file's cases, said in the hand-back; the driver's
    fast half; both rehearsals at the parent's merge, the family's unchanged and the deployment's offers from
    packages.

### The workshop

- [ ] **WORKSHOP1a — where plugins are made, as a setting** (§2.1).
  - **What:** `<home>/plugin-sources.json`'s `workshop`, with its refusals, in `plugins.ts` and
    `PluginSources.cs`, and `daoris plugin workshop`; the modules' state and `SET_WORKSHOP`; Settings → Driver's
    row; the setting door `workshop`.
  - **Lanes:** `cli`; `driver`; `modules`; `web-settings`; `service` (the door's box).
  - **Proof:** the twins' tables (`plugin-sources.test.ts`, `PluginSourcesTests`: kinds, absence, each refusal);
    vitest over the mocked bridge in both catalogues; `HelpCoverageTests`. The look: the row in both themes and
    languages.
- [ ] **WORKSHOP1b — the workshop and its sessions** (§2.2; after WORKSHOP1a and PLUGUI1g).
  - **What:** the workshop's repository, the room rendered at each open, the kit's scaffold for a new id, and
    the ask to `workshop` (local only). Also the loop picking such asks up, one session per workshop, and the
    rules handed at spawn. The record is concluded from the exit, then the tests' and the trial's verdicts, with
    failing tests parking `awaiting-person`.
  - **Lanes:** `driver`; `service` (the receiver); `modules`.
  - **Proof:** driver tests in the fast half for the room, the scaffold, the lock and the ending's table. In
    the family rehearsal, at the parent's merge, an ask to the workshop is served by the stub agent, which
    writes a plugin, and the record carries a passing verdict. A second ask with a failing test parks.
- [ ] **WORKSHOP1c — the workshop on the view, and Ask Daoris makes plugins there** (§2.3, §2.5; after 1b and
  PLUGUI1f).
  - **What:** the *In the workshop* group and its page; *Install* from the workshop; `plugin_propose` add by
    `workshop` id; the room's *Making a plugin* from the setting; an ask to a repository carrying the scaffold.
  - **Lanes:** `web-shell`; `modules`; `driver` (the judge, the room, its goldens); `service` (the box's shape).
  - **Proof:** stories and vitest; `HelpPluginProposalsTests`, `HelpRoomGoldenTests`, `HelpCoverageTests`. The
    look: a plugin asked for, made, tried and installed on the dev loop's scratch machine.
- [ ] **WORKSHOP1d — handing a plugin to a repository, and a named source** (§2.4; after 1c).
  - **What:** `plugins send` and *Send to a repository…*, a quest with one zip within the attachment limits;
    and `update --from` at both twins, with *Update from…*.
  - **Lanes:** `driver`; `modules`; `cli`; `web-shell`.
  - **Proof:** `PluginSourceTests` and `plugin-sources.test.ts` hold the same new rows (a named source shown,
    then recorded; a folder outside a registered checkout refused). The family rehearsal sends a workshop plugin
    to `engine`, and the quest carries it.

### The package source

- [ ] **PLUGDIST1a — the package and its reader, offline** (§5.1, §5.7).
  - **What:** `PluginPackage` reads a `.nupkg`: its type, no dependencies, the `plugin/` guard, the versions and
    the apiVersion. It extracts through `PluginInstall.Add`. The package source record is read by both twins.
    `daoris-driver plugins install <file.nupkg>` and a folder source are included, with no network.
  - **Lanes:** `driver`; `cli` (the record's twin).
  - **Proof:** `PluginPackageTests` over packages built in the test with `System.IO.Compression`, one per
    refusal and one that escapes; the twins' tables.
- [ ] **PLUGDIST1b — the pack and the release workflow** (an ask to Daoris.Plugins; after 1a, 2c and 2d).
  - **What:** `pack/`, `npm run pack`, and the refusals of §5.2. The workflow: manual, dry run by default,
    trusted publishing.
  - **Proof:** its `node --test` reads each produced package's entries with a small zip reader in plain Node.
    The parent installs a produced package with `plugins install <file>` on a scratch home. The workflow's dry
    run is the owner's press.
- [ ] **PLUGDIST1c — a package source over HTTP** (§5.3–§5.6, §5.8; after 1a).
  - **What:** the service index, search with the type, versions and details, the hash, the download cache, the
    update check, `daoris plugin sources`, and `daoris-driver plugins find|show|install|update`.
  - **Lanes:** `driver`; `cli` (the sources' twin).
  - **Proof:** driver tests against an in-process fake source that serves nuget.org's own shapes. Those shapes
    are recorded once, from real answers, and kept as the tests' fixtures. No gate reaches a network.
- [ ] **PLUGDIST1d — the host answers** (after 1c).
  - **What:** `PLUGIN_FIND`, `PLUGIN_PACKAGE`, `PLUGIN_INSTALL` with a package, `PLUGIN_UPDATE` for a package
    source, and `PLUGIN_SOURCES`, each with MOD5's three things. The refusals are
    `PLUGIN_PACKAGE_NOT_A_PLUGIN`, `PLUGIN_PACKAGE_API`, `PLUGIN_PACKAGE_HASH`, `PLUGIN_SOURCE_NO_TYPE` and the
    information-class `PLUGIN_SOURCE_SILENT`.
  - **Lanes:** `modules`; `driver` only where a reader needs a public seam.
  - **Proof:** `DriverModulePluginsTests`, `DriverModuleRoutesTests`, and the refusal catalogue tests.
- [ ] **PLUGDIST1e — *Find plugins*** (§6; after 1d and PLUGUI1f).
  - **What:** the list's modes, the results, a package's page, the version chooser, install and update, the
    names, and Ask Daoris's doors.
  - **Lanes:** `web-shell`; `driver` (the judge, `HelpCoverageTests`' rows); `service` (`plugin_propose`'s
    package shape).
  - **Proof:** stories for each state (a first search, results, none, a silent source, Daoris's own, another
    publisher, a refused package, an update); vitest; `HelpPluginProposalsTests`; `HelpCoverageTests`. The look:
    both themes and languages, at 1280 and 680 px.
- [ ] **PLUGDIST1f — the first publish** (the owner's; after 1b).
  - **What:** the nuget.org account, the trusted publishing policy for Daoris.Plugins' workflow, the prefix
    application, and the workflow run not dry.
  - **Proof:** an evidence document. On the install, `plugins find` returns the three typed `DaorisPlugin`, with
    their owner. One is installed and updated through a real round trip, and the served hash equals the catalog's.
- [ ] **PLUGDIST1g — the offers from packages** (§3.6; after 1f).
  - **What:** `plugin-offers.json`; `layOffers` from packages, checked and cached in `_fixtures/`;
    `--offers-feed`; the dev loop's offers.
  - **Lanes:** `tools`; `cli` (`desktop-publish.test.ts`); `driver` (an offer's package identity).
  - **Proof:** `desktop-publish.test.ts` over a fixture folder source: the pins are held, a wrong hash is refused
    before anything is replaced, and the layout is D103's. The deployment rehearsal runs at the parent's merge.
- [ ] ⏸ **PLUGDIST1h — the repository signature checked** (§5.6; held).
  - **What:** the signer of `.signature.p7s` is checked against the source's `RepositorySignatures`
    fingerprints.
  - **Lanes:** `driver`.
  - **Trigger:** a source that serves no catalog hash, or the owner's call.

## 8. What does not change

- **D64: no code loads into a host.** A package is unpacked into a folder, and the plugin runs beside Daoris as
  a process on the wire. The wire and the points are unchanged (D101).
- **D103**: the offers stay, install and update never run anything at the press, and a plugin with no record is
  never given a guessed source.
- **D52 and D119 §7**: a plugin adds no view. Its page is drawn by the host's atoms from what the driver reads.
- **D47 §4**: the workshop, the package sources and *Find* are machine-local and shell-only.
- **D32 and `repository-owns-its-work`**: the workshop writes only its own folder, and a hand-over is a quest.
- **D24 and D57**: Daoris names no model and runs no registry. A package source is someone else's index, read
  over HTTP.

## 9. Not chosen

- **npm**, **both sources**, **a release asset**, and **an index Daoris runs** (§4.4).
- **A workshop inside a repository's checkout by folder.** That repository has its own door, and the
  `repository` kind uses it.
- **One git repository per workshop plugin.** One working tree per workshop is simpler, and one session at a
  time is what a machine's workshop needs. Per-plugin repositories wait for a person who works two plugins at
  once in the home.
- **The workshop's setting in `driver.json` or `plugins.json`**: older writers drop fields they do not know
  (§2.1).
- **The package source in the CLI**: it would make `service.ts`'s network rule a list (§5.8).
- **Installing another publisher's plugin on**: its code would run at the loop's next look, before the person
  had read its page.
- **Trusting a package by its name** (`Daoris.Plugins.*`) before the prefix is reserved: anyone may publish
  under an unreserved prefix. The owner account decides.
- **Moving `browser` too**: the family rehearsal's *declares a server* check would need a fixture of its own
  first. It can move when someone wants it published.
- **Checking the repository signature in the first build**: it needs a CMS reader and NuGet's signed-content
  rules. The catalog's hash covers what the first build needs (PLUGDIST1h).

## 10. What only a real run can prove

- **That nuget.org filters `DaorisPlugin` exactly.** The filter was observed on two Microsoft types and a custom
  type with no listed package. The first publish is the first listed custom type queried (PLUGDIST1f).
- **That the catalog's hash is the served package's** for a package Daoris.Plugins publishes. It was observed on
  one package here.
- **The time from push to search**, which nuget.org says is usually under 15 minutes.
- **A workshop session making a real plugin** with a real harness, the tests' verdict, and the time it takes.
  The gates use a stub agent.
- **The prefix reservation**, which is nuget.org's decision on the owner's application.
- **A hand-over's session** in Daoris.Plugins taking a zip in, and what it does with the layout.

## Sources

Read on 2026-10-01.

- [search]: https://learn.microsoft.com/en-us/nuget/api/search-query-service-resource
- [set-package-type]: https://learn.microsoft.com/en-us/nuget/create-packages/set-package-type
- [package-base-address]: https://learn.microsoft.com/en-us/nuget/api/package-base-address-resource
- [registration]: https://learn.microsoft.com/en-us/nuget/api/registration-base-url-resource
- [catalog]: https://learn.microsoft.com/en-us/nuget/api/catalog-resource
- [repository-signatures]: https://learn.microsoft.com/en-us/nuget/api/repository-signatures-resource
- [id-prefix-reservation]: https://learn.microsoft.com/en-us/nuget/nuget-org/id-prefix-reservation
- [finding-and-choosing]: https://learn.microsoft.com/en-us/nuget/consume-packages/finding-and-choosing-packages
- [msbuild-targets]: https://learn.microsoft.com/en-us/nuget/reference/msbuild-targets
- [publish-a-package]: https://learn.microsoft.com/en-us/nuget/nuget-org/publish-a-package
- [trusted-publishing]: https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing
- nuget.org's service index: https://api.nuget.org/v3/index.json
- [registry-api]: https://github.com/npm/registry/blob/main/docs/REGISTRY-API.md
- [registry-signatures]: https://docs.npmjs.com/about-registry-signatures
- [provenance]: https://docs.npmjs.com/generating-provenance-statements
- [trusted-publishers]: https://docs.npmjs.com/trusted-publishers
- [about-scopes]: https://docs.npmjs.com/about-scopes

The observations (search counts, a package's hash against its catalog leaf, npm's `keywords:` answers) were live
queries against nuget.org and registry.npmjs.org on the same day. They are a record of that moment.

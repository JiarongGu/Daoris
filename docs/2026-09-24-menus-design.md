# The menus are the setup domains — the design

**Status: the contract for FRAME1–FRAME5, the owner's choices of 2026-09-24 recorded as D75 before
any code.** The ask, made while looking at the installed application: *"I think we can use the topbar
menu to have more different domain of setup, this is closer to ide logic, and I dont see workspace
anymore?"* It amends the frame design (`docs/2026-09-21-desktop-frame-design.md` §3a) and D66 §2. It
does not touch D66's one navigation: the activity bar still goes to views, and a menu goes to setup.

## 1. What was there

Measured on the installed application with every registration retired:

| | What it did |
|---|---|
| The *Daoris* menu | Settings, Agent tools & accounts, Remotes, Refresh, Language, About. **All three setup items opened the same Settings page at its top.** |
| The *View* menu | The palette and the monitor window. |
| Settings | One page: Appearance, Daoris's own AI, then *This machine* (home, Driver, Wiring, Agent tools with usage, What a start runs on, What agents may do, Plugins) as one long scroll. |
| The workspace | The command center printed the scope as text. The status bar's switcher renders only with two or more workspaces (WSP5). With none, nothing on the screen said what a workspace was or that there was none. |
| The word | *workspace* 41 times and *circle* 26 in English, 工作区 27 and 圈子 24 in 中文, both in one tooltip. |

## 2. The menus

```
◆ Daoris  Workspace  Agents  View                 [ aurora · Ctrl K ]   ─ □ ✕
```

| Menu | Items | Opens |
|---|---|---|
| **Daoris** | Settings · Driver · Plugins · ─ · Refresh the index · Switch language · ─ · About | Settings at *Appearance*, *Driver*, *Plugins*; the rest act as today |
| **Workspace** | every workspace with its repository count, the current one marked · ─ · Add repository… · Import a folder… · Wire to a remote… · ─ · Workspace settings | choosing a workspace scopes the window; *Add* opens the add drawer; the rest open Settings at *Workspace* |
| **Agents** | Tools & accounts · What agents may do · Proposals (with the count waiting) · Usage · Daoris's own AI | Settings at *Agents & accounts*, *Permissions*, *Permissions*, *Agents & accounts*, *Daoris's own AI* |
| **View** | unchanged | |

**In a browser** (D47 §4) a menu holds only what a browser may know: *Daoris* keeps Settings (at
Appearance), Refresh, Language and About; *Workspace* keeps the list of workspaces; *Agents* keeps
*Daoris's own AI*. No item opens something that is not there.

## 3. Settings: one page, a list of domains

An IDE's settings page: the domains in a list at its left, one shown at a time, the chosen one
remembered for the viewer. Every domain is cards the page already held:

| Domain | Holds | Desktop only |
|---|---|---|
| **Appearance** | theme, language | |
| **Daoris's own AI** | search and convergence's tier; the intake's agent and account per workspace | the intake |
| **Workspace** | every workspace: its repositories, its remote; *Wiring* (the remotes map, *Wire a workspace*); *What a start runs on* | yes, bar the list |
| **Driver** | the Daoris home; notify; strikes | yes |
| **Agents & accounts** | each tool, its accounts, its ways in; usage | yes |
| **Permissions** | what agents may do; proposals | yes |
| **Plugins** | the plugins folder and each plugin | yes |

A browser's list is Appearance, Daoris's own AI and Workspace, and says nothing of the rest. The two
doors are unchanged (D50): every row is still the same file the CLI edits.

## 4. The workspace, always named

| The machine holds | Command center | Status bar |
|---|---|---|
| none | *no workspace yet* | *no workspace yet* |
| one | its name | its name, as text |
| several, none chosen | *every workspace · N* | the switcher |
| several, one chosen | its name | the switcher |

WSP5's rule holds for the **control**: a switcher with one option is noise. It no longer hides the
**fact**. The Workspace menu is the other door to the same scope, and the Workspace domain lists what
each one holds.

## 5. One word

*workspace* / 工作区 in every catalogue, and in the sentences the CLI, the driver and the service print.
*Circle* stays prose in the design documents, where it explains what a workspace is for. The sweep is
two items: the catalogues (FRAME1) and the other doors' sentences (FRAME5), because the second touches
refusals three artefacts assert verbatim.

## 6. Build order

1. **FRAME1 — one word in the interface.** Both catalogues; the tests that read the old words.
2. **FRAME2 — Settings by domain.** The list, one domain at a time, a domain reachable by name.
3. **FRAME3 — the menus by domain.** Each item opens its domain; *Workspace* scopes; the browser's
   menus hold only what it may know.
4. **FRAME4 — the workspace always named.** The command center, the status bar, and the Workspace
   domain's list of workspaces with their repositories.
5. **FRAME5 — one word at the other doors.** The CLI's, the driver's and the service's sentences.

Each is looked at on the installed application in both themes and both languages before it closes.

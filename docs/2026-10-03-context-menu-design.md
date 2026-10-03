# The right-click menu — design

> CTX1, from the owner, 2026-10-03: *"we do need to utilize the right click menu of daoris desktop"*. The decision is
> **D138**. Status: **designed and built** (CTX1). Read with the platform language (`2026-09-19-platform-ux.md` §4, §6),
> MENU1's one menu atom (`2026-09-21-working-surface-components.md` §4) and D126 §3.1, whose one rule says which acts a
> session is offered.

## 1. What was there

Read from the code at `19592227`:

- **A right-click showed the engine's own menu nearly everywhere**: Chromium's, in the operating system's language
  rather than the one the person chose (Shenora's `ChromiumEngineOptions.Locale` is the OS's UI language when unset),
  offering a web page's acts on an application.
- **Two places answered it**: a tab of the side bar and a tab of the panel open their views menu (DOCK1e), each by its
  own `onContextMenu`.
- **What Shenora 0.19 offers a page**: no menu the page fills, natively drawn. It has one route, `showSystemMenu`, which
  opens the window's own system menu at the pointer, as a right-click on a real caption does.

## 2. The page's own menu

**The menu is the page's own**: one `contextmenu` handler in `Daoris.Web`, drawing MENU1's menu at the pointer. A native
menu was the alternative, and the evidence is against it:

- **There is no native menu a page can fill.** It would be a new host module in the application and the modules, a
  payload of labels and ids across the bridge and a chosen id back, and a menu that looks like neither theme.
- **Every act a surface offers already has its one owner in the page** (`sessionActs.ts`, `workActs.ts`, each page's
  presses). A native menu would hand each press back across the bridge to the same owner.
- **The words are the catalogue's**, in the reader's language (D116), with the same names the buttons and the ⋯ say.

Three places keep a menu that is not the page's:

| Where | Which menu | Why |
|---|---|---|
| A text field (an input, a textarea, the terminal) | The engine's own: cut, copy, paste | Pasting from the page needs a clipboard permission the engine's own menu does not, and an edit menu is the one menu every field already has |
| The app strip's own space | The window's system menu, through `showSystemMenu` | It is the title bar, and a caption's right-click is the window's (D56) |
| Daoris's browser (`daoris-browser`, D99) | The engine's own | It shows other sites' pages, holds no Daoris page and no handler of the page's runs there; its menu is a browser's (open in a new tab, save, inspect) |
| The page in a person's own browser (no shell, D47 §4) | The browser's own, wherever no surface offers acts | It is the person's browser, with its own acts (a new tab, its translation, its extensions); the page's menu opens only for a surface's acts |

**On the desktop, where nothing is offered, the engine's menu is suppressed**: a web page's *Back*, *Reload* and
*Inspect* are not this application's acts.

## 3. What a press is on

The handler reads what was pressed, in this order:

1. **Already answered** by a surface of its own (the two tab strips): nothing more.
2. **Inside an open menu**: nothing, and nothing from the engine.
3. **A text field**: the engine's own menu.
4. Otherwise a menu of up to four groups, most specific first, a rule between them:
   - **selected text**, where the press is on the selection: *Copy*, *Search Daoris for it*, *Ask Daoris about it*;
   - **a link**: *Open*, *Open in Daoris's browser*, *Copy link*;
   - **a code span or a path**, where no text is selected: *Copy*;
   - **the nearest surface that offers acts**: its own (§4).
5. **None**: nothing, and the engine's menu suppressed; the strip's own space opens the system menu. In a browser, a
   press no surface offers acts for is the browser's, a link and selected words included.

*Open* on a link is the link's own click, so it opens where the person chose (BRW7). *Open in Daoris's browser* is
offered only on a shell, for a web address, and never for a link that must open in the system's browser (a sign-in, D78
§3.4). *Search Daoris for it* opens Search with the words in its box. *Ask Daoris about it* opens Quick Ask with the
words quoted in its box, sent when the person has asked their question; it is a shell's. Every copy says so in one
toast, since nothing on the screen shows it.

## 4. Per surface

A surface offers the acts it already has, from the owner it already has, so the menu and the ⋯ never disagree:

| Surface | Where a right-click offers it | Its acts, in order |
|---|---|---|
| A session's row | its row in Sessions' list | its ⋯'s acts (`offeredActs(…, 'row')`, D126 §3.1), carried out by `sessionActs.ts` |
| A session's page | its header and the page under it | its header's acts: the loud act, *Stop…*, then its ⋯'s, in §3.1's order |
| A quest's page | the page | its header's acts (*Take*, *Mark done*, *Pause…*, *Decline…*, *Abandon…*, *Delete…*), then its body's (*Resume*, *Try again*, *Trust this folder…*, *Open in Sessions*), then *Copy quest ID* |
| An ask's page | the page | its header's acts (*Resume*, *Pause…*, *Close ask*, *Abandon…*, *Delete…*), then *Copy ask ID* |
| A quest's or an ask's row | its row in Quests' list | *Open*, the row's own act (*Resume* a held repository), *Copy quest ID* or *Copy ask ID* |
| A repository's row | its row in Repositories' list | *Open*, *Copy repository name* |
| A repository's page | the page | *Open code map*, *Manage*, *Copy repository name* |
| A plugin's row | its row in Plugins' list | *Open*, *Install* on an offer's row, *Copy plugin ID* |
| A plugin's page | the page | its header's acts (*Turn on* or *Turn off*, *Try*, *Update…*, *Remove…*), then *Copy plugin ID* |
| A document (a hit, an entry) | a hit's row in Search; an entry's page | *Open* on a row, *Copy path* |

**A row offers what it does; its page offers what is done to it.** A row is for reading and choosing, and its record
opens in the main area, where the acting is (Quests', Repositories' and Plugins' lists say so). An act that asks under
its page's header (*Decline…*, *Remove…*) needs that page. The session's row is the exception D126 §3.1 already made:
its ⋯ holds the session's acts, and the frame's doors attend the session before an act that asks.

**A record's menu ends by copying its name**, as a session's ⋯ ends with *Copy session ID*.

## 5. Keys and access

- **The menu key and Shift+F10** open the same menu for the focused element, at its corner, with the first item
  focused. A field keeps the engine's.
- **The menu is a `menu`** with `menuitem`s, named by what it is for (*Actions for …*), the arrows moving through it,
  Enter choosing and **Esc** closing it. Focus goes back where it was, unless the act moved it (a reason's field opened,
  Quick Ask's box).
- **One menu at a time**: a right-click elsewhere closes it and opens the next.

## 6. The parts

- **`Menu.Acts`** (`ui.tsx`): a list of acts drawn as MENU1's rows. The ⋯ menus that already listed acts draw through
  it, so a surface's ⋯ and its right-click are one list.
- **`ContextMenu`** (`menus/ContextMenu.tsx`): the menu at a point, MENU1's surface and rows, its groups between rules.
  A molecule.
- **`contextOffer`**: what a surface spreads on its root to offer its acts. It holds no hook, so a molecule may offer.
- **`ContextMenus`**: the one handler, mounted once per window, handed the frame's doors (copy, search, ask, open in
  Daoris's browser). A detached session's window and the monitor have no Search and no Quick Ask, so their selection
  offers *Copy* alone.
- **Both languages**: an area of its own, `contextMenu`, with structural keys; an act already named on a button keeps
  its key, so it says the same words in both places.

## 7. Rejected

- **The shell's native menu**: §2.
- **A page's acts on its row** (*Take* from a quest's row): an act that asks under the header needs the page, and the
  lists say a row is for choosing. Re-opened if the owner wants it on the window.
- **Leaving the engine's menu where nothing is offered**: its acts are a web page's.
- **Our own cut, copy and paste in a field**: the engine's work without a clipboard permission, and say the same.

## 8. What the checks do not cover

The vitest loop drives the handler in jsdom, which has no engine: whether CEF fires `contextmenu` for the menu key on
its key-up, as Chromium does on Windows, is not seen there; the handler stops it on both the key's down and up, and a
menu already open ignores a second one. The menu's place at the pointer, its look in both themes and both languages,
and the system menu on the strip are the window's to show (`npm run desktop -- shot`), which this branch did not run.

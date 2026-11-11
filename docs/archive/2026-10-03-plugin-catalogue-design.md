# The plugins page as a catalogue, and a plugin's icon — design

> PLUGUI2, from the owner, 2026-10-03: *"plugin page design (and plugin icon)"*. The decision is **D140**. Status:
> **designed and built** (PLUGUI2), and the host's answer of the icon with it (PLUGUI2b, §3.2). Read with the plugins
> screen design (`2026-10-01-plugins-screen-design.md`, D119), which this amends, the plugin design (D64 §3) and the
> distribution design (D120 §4, §6).

## 1. What was there

Read from the code at `a1117db0`:

- **The list** groups the installed plugins by state (*Waiting on you*, *On*, *Off*), then *Daoris's own plugins*. A
  row is a name, a version, a state's word and one line of fragments, *2 points · 1 agent*: what the manifest
  declares, in the manifest's terms. What a plugin is for, its description, appears only on its page.
- **Nothing draws an icon.** The only mark a plugin has is its name's initial, on the list's strip.
- **`PLUGINS` answers whether an update waits** (`update`, PLUGUI1e), and the page never reads it.

## 2. The catalogue

**The list holds three sections, in this order**, each with its count; a section with none is absent:

1. **Installed** — every plugin on this machine: those waiting on you first (refused, and failing once PLUGUI1f
   reads health), then those on, then those off, each by name. The order is still what the person acts on (D119
   §3.1); the state is the row's word, not a heading.
2. **Daoris's own plugins** — the offers this install carries and this machine has not installed (D103), each with
   *Install* on its row.
3. **Available** — what a package source offers that this machine has not installed (D120 §6). PLUGDIST1e builds it
   once PLUGDIST1c's source answers, and until then it is absent. Its row says who published it, *Daoris's own* only by
   the owner account or a verified prefix (D120 §4), and its *Install* installs another publisher's plugin off.

**A row:**

| Part | What it shows |
|---|---|
| Icon | 32 px: the plugin's own, or its monogram (§3.3) |
| Name and version | The manifest's, as declared; the version in the mono meta face |
| State | Its word on a pill at the row's right, where it has one (D119 §2): neutral *running* and *off*, open's *refused* |
| One line | Its description, the author's words for what it gives, cut to the row and whole on its page; with none, what it adds as fragments |
| Meta line | Where it came from (*Daoris's own*, *from a folder*, *from a package*, *no source recorded*, *source unreadable*), then what it adds as fragments, then *update available* where its source declares something different (D119 §2's neutral pill) |

An offer's row has no state and no source, since its section says both, and its *Install* sits at its right beside
the row's door. **The strip** draws each installed plugin's icon, 20 px, in its initial's place, with the waiting mark
and the dimmed *off* as before.

**A plugin's page** keeps D119 §3.2, with four changes: its header leads with the icon at 48 px beside the title;
*update available* sits beside the state's pill when an update waits; its Source says, where its declared icon is not
drawn, why, in the reader's sentence; and **every block of the detail takes the pane's width**. **An offer's page**
wears its icon in its header, and takes the pane's width too.

**One width, the pane's.** The install showed the detail capping its description and its data folder's note at 65ch
(`max-w-prose`, per paragraph) while its source line ran the pane's width: at 1600 px one page wrapped at two edges,
and in 中文 a 65ch column held about 32 glyphs. The owner asked that a plugin's detail size with its pane. So no block
of the detail keeps a measure of its own: the header's line (`PageHead`'s `measure={false}`), the lead, the sections'
paragraphs and an offer's needs. **LAYOUT11 sets the measure**, the rule and any cap for a whole page, in one place
(D141).

**How Daoris's own plugins read**: not installed, an offer is in the second section with *Install*; installed, it is in
the first, its meta line saying *Daoris's own*, and its update re-reads the install's copy (D103). Its monogram is the
same in both, since the monogram is keyed by the id they share. **An update that waits** reads as *update available*
on the row's meta line and beside the page's pill, never as a state: it waits on nobody (D119 §2).

## 3. The icon

### 3.1 The manifest's field

```json
{ "id": "acme.quiet-hours", "icon": "assets/icon.svg" }
```

`icon` is a path inside the plugin's folder to an SVG or a PNG. Both readers check these rules in this order, and the
first problem is said:

1. Absent or `null`: no icon, so its monogram is drawn.
2. A value that is not text, or blank.
3. Not a path inside the folder: written from the folder, names joined by `/`, with none empty, `.` or `..`, and no `\`
   or `:` (a drive or an address).
4. Neither `.svg` nor `.png`, case aside.
5. Not a file there: absent, or a folder.
6. Larger than 32 KiB (32,768 bytes).
7. A `.png` that does not start with PNG's signature and its `IHDR` chunk, or is wider or taller than 512 px.
8. A `.svg` that is not UTF-8 text holding an `<svg` element, or that declares an XML entity (`<!ENTITY`).

Rules 1–4 are the manifest's, read by the catalogue. Rules 5–8 are the file's, read where the icon is drawn or listed.

- 🔴 **An icon's problem never refuses the plugin.** An icon is how a plugin is recognised, never what it does. The
  plugin stays sound, its monogram is drawn, and the sentence is said on its page's Source and by `daoris plugin list`.
  A refused plugin keeps its icon, since it is how the person finds the plugin the refusal is about.
- **The limits.** An icon is drawn at 48 px at most, and the list carries every plugin's, so 32 KiB is ample. 512 px
  covers 48 px at any screen's density, and bounds what a small PNG inflates to. An entity declaration is how a small
  XML file expands into a large one.
- **An SVG is drawn as an image, never inlined as markup.** The page puts it in an `<img>`, where none of its scripts
  run, nothing outside it loads, and none of its styles reach the page. So its content is checked only for being an
  SVG that does not expand. No plugin markup enters the page's document (D64 §7, D52).
- **No link is followed into an install.** The add doors copy no link (D3, `PluginInstall.CopyTree`), so an installed
  plugin's icon is a file it carries, and a link in a source folder reads as rule 5 once installed.

### 3.2 How the page reaches it

**The page is handed the icon's bytes, never its path.** `PLUGINS` answers each plugin and each offer with:

- `icon`: a `data:image/svg+xml;base64,…` or `data:image/png;base64,…` URI, which `PluginIcon.Read` builds from the
  bytes it judged, or null;
- `iconProblem`: the rule's sentence, or null.

The page draws only a URI with one of those two prefixes and base64 after it, and anything else as no icon. A browser
never meets it: the view is shell-only (D47 §4), and nothing in the answer says where the file lies.

**Why the list's answer and not a route per icon.** 32 KiB a plugin bounds the answer, and the list is asked when the
view comes to the front and after an act, never on a timer. A route per icon would ask the driver once per row for
files the catalogue has just read.

**The two fields on `PLUGINS`** (`DriverModule.Plugins.cs`, the modules' lane) came after the page, in PLUGUI2b, which
calls `PluginIcon.Read` for each plugin and offer; `DriverModulePluginPageTests` holds them, and that no string in the
answer names an icon's file.

### 3.3 The monogram

With no icon that draws, the page draws a monogram:

- **Its glyph** is the first character of the plugin's name as declared, or of its id where the name is blank, in
  capitals where the script has them. A 中文 name gives its first character.
- **Its hue** is one of six identity hues, picked by the 32-bit FNV-1a hash of the plugin's id, modulo six. An id is the
  folder's and unique, while a name may repeat and may change with an update. So a plugin keeps its hue across a
  rename, two plugins wearing one name tell apart, and an offer and the plugin it becomes look alike.
- **The hues** are `--ident-1` to `--ident-6` (moss, teal, slate, iris, plum, stone), each with a light value and a dark
  one. The glyph wears the hue, on a 15% field of it.
- **Computed, not eyeballed.** The glyph is at least 4.5:1 on its field over `--page` and `--raised` in both themes
  (light 4.6 and up, dark 6.3 and up). Every hue is at least ΔE 20 from each status hue and the accent, so a monogram
  never reads as a state. `plugins/icon.test.ts` computes both from `tokens.css`, and a pinned table holds the hash.
- **A monogram is identity, never status.** Its hue says which plugin; how it stands stays on the pill, with its word
  (platform language §3).

## 4. Two doors and two readers

- **The manifest is read by two artefacts that share no code** (twins): the CLI's `plugins.ts` (`readManifest`,
  `readIcon`) and the driver's `Plugins.cs` with `PluginIcon.cs`. One table holds them: `plugin-icons.test.ts`
  parses `PluginIconTests`' theory and holds its own rows to it, cell for cell.
- **`daoris plugin list`** prints, under a plugin or an offer whose declared icon is not drawn, `icon not drawn:` and
  the sentence.
- **Nothing sets an icon from a screen.** It is the plugin's own declaration (D64), changed where the plugin is made.

## 5. Words

Chrome translates and content does not. A plugin's name, description and version stay as declared; the monogram's
glyph is the name's own character; the icon's sentence is the reader's, passed through whole in both languages, as a
refused plugin's is. The words are new where named: *Installed* (已安装), *update available* (可更新, which D119 §3.8
named), the meta line's sources (*Daoris's own* 「Daoris 自带」, *from a folder* 来自文件夹, *from a package* 来自软件包,
*no source recorded* 无来源记录, *source unreadable* 来源记录无法读取), and *Its icon is not drawn* (图标未显示). The
groups *Waiting on you*, *On* and *Off* retire from the list.

## 6. Not chosen

- **A second icon for the dark theme.** An author draws one that reads on both, as an extension's store asks; the
  monogram is the platform's and has both.
- **An icon by address.** The page would fetch from wherever a plugin says, once per row. The bytes ride from the folder.
- **An SVG inlined for crispness or `currentColor`.** Its markup would be in the page's document.
- **JPEG, GIF, WebP or ICO.** Two formats cover a drawn icon and a picture.
- **A monogram's hue from the name.** A rename would change it, and two plugins named alike would wear one.
- **The status hues for monograms.** A red tile reads as declined (platform language §3).
- **Keeping D119's three state headings.** The owner asked for a catalogue; the order within *Installed* keeps what they
  were for.
- **Refusing a plugin for its icon.**

## 7. What only the window proves

- the rows at 280 px with their three lines, in both languages, and the strip at 680 px;
- a real SVG and a real PNG at 20, 32 and 48 px in both themes, once PLUGUI2b hands them;
- the monogram hues as the window draws them: the computed check holds the numbers, not the look.

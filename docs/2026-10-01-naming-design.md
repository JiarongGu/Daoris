# Names — a name is a UI element, designed in both languages (NAME1)

**Status: the contract for NAME1, written first (NAME1a, D116).** The renames it proposes are NAME1b's,
made after the owner reads them; this document, the glossary and the check are what they are made
against. The owner, 2026-10-01:

> *"also for naming (for example in settings) we do need to have properly named in both en/zh since this
> is not just translation this is part of the ui element (this also apply to other display too)"*

It extends two contracts and changes neither: `.claude/knowledge/translation-parity.md` (*chrome translates
and content does not; keys are structural*) and `docs/2026-09-19-platform-ux.md` §4 (*one word per thing,
in each language*). It measures against the frame's widths (`docs/2026-09-21-desktop-frame-design.md`,
D56; `docs/2026-09-29-dock-design.md`; the type tokens), and it follows UX5's method
(`docs/2026-09-26-ux5-screen-audit.md`): read each key where it renders, not only in the catalogue.

**Its three companions:**
- `src/Daoris.Web/src/locales/glossary.json`: the terms, the kinds with their budgets, and the doors (§5).
- `docs/2026-10-01-naming-audit.md`: every label key, its kind, today's names and the proposed ones (§7).
- `src/Daoris.Web/scripts/names-check.mjs`: the check, in report mode (§6).

## 1. What was found

The catalogues hold 1,699 keys in 66 areas per language. Read where each renders, 772 are names a person
reads as a label; the rest are sentences, tooltips, a row's meta fragments and screen-reader names. The
drift is of three kinds, and the owner's examples are one of each:

1. **A name translated instead of named.** Settings' own domains: *Daoris's own AI* is 「Daoris 自身的 AI」
   and *Agents & accounts* 「智能体与账户」, a possessive and a conjunction carried word for word into a
   list that should hold one noun each. A section's title against its button: the section 「同步到最新」
   (*Bring up to date*) opens on a button 「查看更新」 (*Look for updates*), so the heading and the press
   under it name two acts.
2. **One concept, two words.** Measured, not sampled:

   | Concept | English | 中文 |
   |---|---|---|
   | the view of what a repository holds | *Projects* on the bar, *repository* everywhere else | 项目, 仓库 |
   | convergence | *Convergence* | 同归 on the bar, 汇聚 in the palette |
   | the review | *Review* | 改动 on its tab, 审阅 in the head, 查看 in the menus |
   | adopt | *adopt* | 加入 ×9, 采用 ×4, 接入 ×2 |
   | register | *register* | 注册 ×15, 登记 ×15 |
   | doctrine | *doctrine* | 规范, 教义 (a religion's word) |
   | an agent (Claude Code, Codex) | *agent*, *tool*, *agent tool*, *harness* | 智能体, 工具, 智能体工具, 代理 |
   | a chat session | *chat* in the rail, *conversation* as its title | 对话 for both, and for the record |
   | sign in | *Log in*, *logged in*, *Sign in*, *sign-in* | 登录 |
   | the session list | *session list*, *rail* | 会话列表, 会话栏 |
   | the receiver of a quest | *receiver*, *addressed to*, *to* | 接收方, 受托方, 致 |
   | a quest's decline | *decline* | 谢绝, and 拒绝 on a session's relations |
   | a proposal's accept | *accept* | 接受, where a session's work is 采纳 |
   | bring up to date | *Bring up to date* | 同步到最新, which is sync's word (同步) |
   | park, and hold | *park*, *hold* | 暂停 for both |
   | a start that cannot run | *held* | 受阻, while *held* is also a repository the person paused |

3. **English drift, which reads as two voices on one card.** Buttons: of the 183 names of acts, 133
   start lower-case and 50 capitalised, so Agents & accounts shows *install · update · look again* beside
   *Log in · Make default · Remove*. Every other kind has settled, counted where a key renders as that
   element's text: menu items 32 of 33 capitalised, section titles 30 of 35, setting rows 18 of 18,
   empty-state headlines 16 of 17, pills 20 of 21 lower-case. And Chinese leaves
   concept words in English inside a sentence: 「本 quest」, 「自己的 agent」, 「以及 Ask Daoris」.

## 2. What a name is

**A name is part of the element it labels, designed in each language for that element.** It is not a
translation of the other language's name, and it is not a sentence. Three things decide it:

- **The concept it names**, which the glossary holds: one term per concept in each language, chosen as a
  name. Where the element names a concept, it uses the glossary's term, and never a word the glossary
  says the concept must not be called.
- **The kind of element**, which decides its form: its case, its punctuation, its grammar, and how long
  it may be (§3, §4).
- **The room the frame gives it**, which decides the budget: a name either fits its element at the
  frame's narrowest layout, or the element cuts it, wraps it, or turns it into an icon (§4).

**Chrome is named; content is not** (`translation-parity`). A repository's name, a quest's title, a
session's opening message, the driver's verbatim refusal: none is a name in this sense, and none is
renamed. A placeholder that carries such a value is part of the name around it, and the name is measured
with the value's typical width (§6).

**The key stays structural.** Renaming a name never renames its key: the key names the place, and a
name that moves under it is what NAME1b does.

## 3. The kinds, and their rules in each language

Fourteen kinds. **An element's accessible name and its tooltip take the kind of the control they name**:
an icon-only close button's name is a button's name. (The fourteenth, `command`, was found building the
check: a palette row is a name, a dash and a gloss in a 34rem dialog, and measuring it by a strip menu's
room reported every row.)

| Kind | What it is | Where it renders |
|---|---|---|
| `nav` | a place a person goes to | the activity bar (its tip, and the view's header), Settings' domain list, the strip's four menus |
| `title` | the name of what is open | a view's page header, a drawer's title, a window's title |
| `tab` | one of the views a region holds | the side bar's and the panel's tabs |
| `section` | the name of a part of a page | `SectionTitle`, `CardHeader`, a sub-head in a card, a menu's group label |
| `field` | the name of a value, or of a setting | `SettingRow`'s label, a form's label, a record's `dt`, a tile's label, a checkbox's label |
| `choice` | one of the values offered | a segmented option, a select's option |
| `button` | an act | `Button`, an icon-only control's name, a link that acts |
| `status` | a word for a state | `Pill`, a dot's word, a `Chip`, the status bar's nouns and values |
| `menu` | an item in a menu | the strip's menus, a tab list's menu, a row's menu, the map's menus |
| `command` | a row of the command palette | a command's name, then a dash and its gloss, which is a sentence |
| `headline` | an empty state's one line | `EmptyState`'s headline |
| `placeholder` | the hint inside an empty field | an input's or a select's placeholder |
| `toast` | an act's outcome, said once | `notify(…)` |
| `sentence` | the platform explaining itself | a hint, a tip, a body, a description, and a row's meta fragments (*filed 2d ago*) |

### 3a. English

- **Case.** Sentence case for `nav`, `title`, `tab`, `section`, `field`, `choice`, `button`, `menu`,
  `command` and `headline`: the first word capitalised, the rest lower-case but for a proper name (*Daoris*, *Ask
  Daoris*, *Quick Ask*, *Claude Code*, *Edge*, *Git*, *PATH*). Lower case for `status` and
  `placeholder`. A `toast` and a `sentence` are sentences. **The buttons are the drift**: the
  lower-case majority was never decided, and every other kind that names something on the page is
  sentence case, as the platform's own Windows is.
- **Grammar.** A `nav` is a noun, plural for a collection (*Sessions*, *Quests*), with no article, no
  conjunction and no possessive. A `title` is the nav's noun, or a noun phrase naming what is open (*New
  quest*, *Manage engine*). A `tab` is one noun. A `section` is a noun phrase naming what the section
  holds; the house's embedded question (*What needs you*, *How work lands*) stays where the noun would be
  jargon. A `field` is a noun phrase naming the value (*Theme*, *Daoris home*); a switch's label says what
  on does (*Notify me when a session parks*). A `button` is a verb first, in the imperative, with its object
  named as the glossary names it (*Publish quest*, *Discard tree*), and never a pronoun where the object
  can be named (*Register it*, *Stop it*, *Import them*). A `status` is a past participle for an outcome
  (*done*, *declined*), *-ing* for a state in progress (*working*), an adjective for a condition (*idle*,
  *ready*). A `menu` item is its destination's name or a button's verb phrase.
- **Register.** The platform's voice: plain words, the person addressed as *you*, *Never mind* for
  backing out. **No code word on the window**: a harness is an *agent*, a profile an *account*, a strike
  a *failed session*, a tick *the driver's next look*; the code's word stays in the code and in a
  backticked command.
- **Punctuation.** No label ends in a full stop or a colon. An ellipsis, one character (`…`), ends a
  `button` or `menu` item that asks for more before it acts; never `...`. An em dash, spaced, separates
  a palette row's name from its gloss, and a choice's value from its qualifier (*Off — starters only*).
- **Parallel structure.** Siblings share a shape: the items of one menu, the options of one choice, the
  sections of one domain, the moves of one footer, the states of one state machine.

### 3b. 中文

- **Grammar.** A `nav` is one noun of two to four characters, with no 的, no 与 or 和, and no possessive
  (「智能体」, not 「智能体与账户」). A `tab` is the same. A `section` is a noun phrase that names what the
  part holds (「落地方式」, 「启动配置」); **a Chinese title is never an English question carried over**
  (「各账户各自承担了多少」 is a translation of *What each account has carried*, and reads as one). A
  `field` is a noun phrase; a switch's label is a short clause of what on does.
- **A button is verb and object (动宾).** The verb alone where the button sits in its object's own
  drawer or card (「接下」 in a quest's drawer); the verb and the glossary's object otherwise
  (「发布委托」, 「丢弃工作树」). Two-character verbs where one exists (发布, 采纳, 退回, 丢弃, 清理,
  移除, 删除); a four-character phrase where it is the natural one (重新检查, 立即同步). **Never a
  pronoun standing for the object** (它, 它们): name the object or drop it; 这个 before the object's name
  (「信任这个文件夹」) names it. Never 了, 吧 or 呢. The confirming second
  press is 确认 and the verb (「确认删除」); the back-out is 「取消」, not 「算了」, which reads as a shrug
  where English's *Never mind* reads as courtesy. A door is 打开 and the place's name (「打开会话」),
  except in a menu, which is a door already.
- **Status words.** 已 and the verb for the outcome of an act (已完成, 已谢绝, 已停止); the verb and 中 for
  a state in progress (工作中, 启动中, 排队中); a bare word for a condition (空闲, 就绪, 失败, 未安装). One
  state has one word on every screen: a quest's *Declined* is 已谢绝 in the pill, the timeline, the chain
  and the record.
- **Punctuation.** Full-width inside Chinese (，：；？！（）); the dash is 「——」; the ellipsis on a label
  is the same one character as English's (「…」), because it is a UI mark saying *this asks for more*,
  not prose's 「……」. A UI element's name quoted in a sentence takes 「」.
- **Latin in Chinese.** Kept for a product or company name (Daoris, Claude Code, Edge, Git, PowerShell,
  Windows); for what the person types or reads in a file (a command, a file name, a variable, a plugin's
  id, a branch); and for an acronym with no settled Chinese name (AI, API, ID, URL, MCP, PATH, diff).
  **Never for a concept the glossary names**: 「本 quest」, 「自己的 agent」 and 「以及 Ask Daoris」 are
  three strings that did. Latin and numbers are set apart from Chinese by a space (`API 密钥`, `3 个会话`);
  Chinese beside Chinese is tight. ID is written in capitals as a word (「插件 ID」); `id` stays in
  backticks as a field.
- **道衍 and Daoris.** 道衍 is the wordmark, the application's menu and Ask Daoris's name (问道衍); running
  text says *Daoris* (D41 §4). 「关于 道衍」 (a space between two Chinese words) and 「打开道衍自身的 AI」
  (道衍 in running text) both break it.

### 3c. Across both

- **A door names its destination by the destination's own name**, in both languages: a menu item, a
  palette row, a Get started step's door, Ask Daoris's go card, a status bar item's tip, a button that
  opens a view. *Tools & accounts* opens *Agents & accounts*, *What agents may do* opens *Permissions*,
  *Set up Daoris* opens *Get started*, and 汇聚 opens 同归: four doors, eight names. The glossary lists
  the doors and the check holds them (§5, §6).
- **A heading and the press under it name one act.** A section titled for an act opens on that act's
  first step, in the same word: the section *Updates* (「更新」) opens on *Look for updates*
  (「检查更新」), and its second press is *Bring up to date* (「更新到最新」).
- **A name is not its value**, and a label never repeats its value's first word (D41 §4, as before).

## 4. The budgets

A budget is the longest a name of that kind may be, in characters for English and in units for Chinese
(a Chinese character or full-width mark is 1; a Latin letter, digit, space or ASCII mark is ½, since the
system's Latin face averages half an em). Each budget comes from the **room** the frame gives the kind at
its narrowest layout: a window of 888 CSS px (UX5's narrow instrument) with the side bar at its 300px
floor (`layout.ts` `DOCK.floor`), in both themes, measured at the kind's type token (13px `text-body`
averages 6.5px a Latin character and 13px a Chinese one; 12px `text-small` 6 and 12; 11px `text-meta` in
the pill's mono face 6.6 and 11).

| Kind | The room it must fit, at its token | English | 中文 |
|---|---|---|---|
| `nav` | Settings' domain list: 11rem less its padding and rail, 150px of `text-body`, one line; and one word on the bar's tip | 16 | 5 |
| `title` | a drawer's title: 32rem less its padding and close, about 430px of `text-title` | 40 | 16 |
| `tab` | the selected tab whole beside three icon tabs, the side bar at its floor: about 66px of `text-small` | 10 | 4 |
| `section` | one line in a card in Settings' narrowest column, about 260px of `text-small`, semibold | 32 | 12 |
| `field` | a settings row's label column floor, 16rem (256px) of `text-body` | 36 | 14 |
| `choice` | a segmented option, its whole group on one line in a row: about 90px of `text-small` | 16 | 6 |
| `button` | two buttons side by side in the side bar at its floor: about 110px of `text-body` each | 20 | 8 |
| `status` | a pill beside its row's title: about 100px of `text-meta` mono | 16 | 5 |
| `menu` | a strip menu at its 224px minimum, less the check, icon and shortcut columns: about 140px of `text-small` | 24 | 10 |
| `command` | the palette's 34rem less its padding and the row's icon, about 470px of `text-body`; the name before the dash is measured | 40 | 16 |
| `headline` | an empty state in the side bar at its floor: about 270px of `text-body`, semibold | 40 | 16 |
| `placeholder` | inside a field in a 309px row: about 250px of `text-body` | 40 | 16 |
| `toast` | the 26rem toast, clamped to two lines: about 720px of `text-body`; past it the sentence is cut | 110 | 55 |
| `sentence` | `Prose`, 65ch; it wraps, so no budget | — | — |

**Three are tighter than their room, on purpose.** A `nav` holds 23 English characters and 11 Chinese
in its 150px, and is budgeted at 16 and 5 because a place is one word, read at a glance down a list. A
`tab` is budgeted for four views in the side bar, since DOCK1b lets the console move in. A `button` is
budgeted for the side bar's floor, where the review's moves sit.

**How the parent verifies a budget on the window** (`npm run desktop -- shot`, UX5's sizing at 888 wide,
both themes, both languages): Settings with every domain in its list on one line; the side bar at its
floor on Sessions, with a session attended, its selected tab whole beside the others' icons; the review's
footer with its moves on one line; a quest's drawer with its title on one line; the strip's menus open,
each item on one line; a long toast on two lines, uncut.

## 5. How names are kept: the glossary

`src/Daoris.Web/src/locales/glossary.json` is the authority. It sits beside the language folders and in
neither, so it is never merged into a catalogue (`locales/index.ts` reads `./en/*.json` and `./zh/*.json`).
It holds three things:

- **`terms`**: one entry per concept. Its `en` and `zh` are the names, chosen as names; `means` is its
  one-line definition; `avoid` lists, per language, the words it must not be called; `match` is how its
  English is recognised in a key's value, so the check knows which keys name it. A code word that is
  never shown on the window (`harness`, `profile`, `strike`) is an entry whose `use` points at the term
  that is.
- **`kinds`**: the fourteen kinds of §3, each with its budget, its room, its case, and **the keys of that
  kind**. A kind lists keys and key prefixes (`nav.*`, `work.status.*`), and a key takes the most specific
  entry that holds it: its own key before any prefix, a longer prefix before a shorter one. A plural
  form (`_other`) takes its stem's kind. A key no kind names is a `sentence`. This is the simplest
  declaration that needs no key renamed: a naming convention on key names would have meant renaming
  hundreds of structural keys to carry a fact the map states in one line each.
- **`doors`**: each door and the key naming its destination (§3c).

**Adding a label key** is two lines in one change: the key in both catalogues, and the key (or a prefix
already covering it) in its kind. **Adding a concept** is a term: its names in both languages, the
reason each was chosen rather than translated, and what it must not be called.

## 6. How names are kept: the check

`npm --prefix src/Daoris.Web run names:check` runs `scripts/names-check.mjs`, beside `i18n-check.mjs`. It
reads the glossary and both catalogues and reports, per key:

1. **Glossary conformance.** A label whose English names a term (by `match`) says the term's Chinese
   name or one of its short forms, and neither language uses a word the term must not be called. With
   `--all` it reads every sentence, tooltip and toast too, for the forbidden words only: a sentence may
   say a thing its own way, and holding it to the term's name reported a verb (*Daoris owns this
   location*) as the declaration's *owns*.
2. **Budgets.** A label longer than its kind's budget, in either language. A placeholder counts as its
   typical value: a number two characters, anything else ten. A menu item and a palette row are measured
   by their name, before the dash.
3. **Form.** Case by kind; no full stop or colon closing a label; `...` or 「……」 where `…` belongs; a
   Chinese button that says 它; Latin punctuation inside Chinese; a Latin word or number not set apart
   from Chinese.
4. **Doors.** A door that does not say its destination's name.

**It reports and never fails, today.** Today's names would fail every rule above before NAME1b renames
them, and a gate that is red on its first day is a gate someone switches off. It exits 0 with its report,
and 2 only when the glossary itself is malformed (a kind naming a key the catalogues lack, a term
without a name in either language, two terms claiming one name), since a glossary that cannot be read
has stopped checking anything, which is a fact.

**What NAME1b turns on, by D54's line** (*a fact gates, a judgement reports*). Conformance, form and
doors are facts: a quest's label says 任务 or it does not. They gate once the renames land, by the
`--strict` flag the script already takes, added to the web's build beside the parity check. The budgets
stay a report: a character count estimates a width, and the window is where a width is a fact. The
parity gate itself is unchanged.

## 7. The audit, and the renames

The audit is `docs/2026-10-01-naming-audit.md`: every label key, grouped by where it renders, Settings'
domains and section titles first. Each row has the element's kind, today's English and 中文, the proposed
English and 中文, and a one-line reason where either changes. The glossary's kind map is the audit's
kind column.

**The proposals obey the rules they propose.** Applied to the catalogues in memory and checked, they leave
no glossary, form or door finding, and every placeholder where it was; what remains is the budgets'
report, which the audit's §1 describes.

**NAME1b is the renames**, after the owner reads the proposals: the catalogue values, the tests and
stories that pin a value, and the check turned to `--strict`. Some renames touch a component: a
section title that becomes a card's only heading, where U57 says the domain's list already names it; a
door whose destination moved. Those are named in the audit's rows, and they are NAME1b's.

## 8. Rejected

- **Translating the English names more carefully.** It is the defect, not the cure: a careful
  translation of 「Daoris 自身的 AI」 is still an English possessive in a Chinese list.
- **A kind declared by the key's name** (`….title`, `….button`). Keys are structural and many already
  end in a word that is not their kind (`settings.theme.label` is a field; `harness.pin` is an action
  word the driver says); renaming them would churn every call site to state what one line of a map
  states.
- **Measuring widths in the check by rendering.** A browser in the check would make it the slowest and
  least portable gate in the web for an estimate the window then confirms. Characters and units are
  within the rooms' slack, and the window is the arbiter.
- **Failing the build now.** See §6: a gate red before its renames is switched off, not obeyed.
- **Title Case for buttons and menus.** Windows, where the platform runs, writes sentence case, and so
  does every other kind on the page already.
- **Lower case for every button**, the majority today. It keeps 133 names and changes 50, and leaves a
  button's name in a different case from the section title above it and the menu item that does the same
  act.
- **One word for chat and conversation.** A chat is a kind of session, started by the person to talk;
  the conversation is the record every structured session keeps. Two concepts: *chat* (聊天, as VS Code's
  zh-CN names its Chat view) and *conversation* (对话).

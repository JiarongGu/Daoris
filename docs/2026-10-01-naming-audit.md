# NAME1a — the naming audit (2026-10-01)

**What this is.** Every catalogue key a person reads as a name, with its kind, today's English and 中文, and the
proposed ones, for the owner to read before NAME1b renames anything. The contract is
`docs/2026-10-01-naming-design.md` (D116): the kinds and their rules, the budgets, and the glossary
(`src/Daoris.Web/src/locales/glossary.json`) that every proposal here takes its terms from. Nothing in the
catalogues changes in NAME1a.

**Applied (NAME1b, 2026-10-01).** The owner approved the proposals, and made the two calls §4 left to them:
the view is *Repositories* (仓库) and an ask is 需求. NAME1b applied every row below with its plural forms, and
what §4 and §5 ask beyond the strings; the check now finds no glossary, form or door finding in any label,
and its `--strict` gates the web's build (design §6). The rows keep today's names as they stood, so the
record reads before and after.

**How it was found** (UX5's method: read each key where it renders, not only in the catalogue). A source scan
found every `t('…')` call, every template prefix and every key held as data in the web's components, with the
element or prop around it (`Button`, `SectionTitle`, `SettingRow`'s label, a `Pill`, a menu, a placeholder,
a toast); each key was then read by hand in its component and given its kind. Tooltips, screen-reader names,
a row's meta fragments and every sentence are sentences, and not rows here; §5 lists what the glossary finds in
them. Settings comes first, domain by domain in its list's order, because the owner named it.

**How to read a row.** A cell shows today's name, and `→` the proposed one in bold where it changes; a row
that changes in neither language has no reason. *Kind* is the element kind (design §3); the glossary's kind map
is this column. Plural forms (`_other`) move with their stem and are not rows.

## 1. The numbers

- **772 label keys**, of 1,699 keys in 66 areas.
  By kind: button 183, status 172, field 129, section 86, choice 46, menu 44, nav 22, placeholder 22, headline 21, title 20, command 19, tab 8.
- **406 change**: 342 in English, 227 of them only their first letter
  (sentence case), and 180 in 中文.
- **What the check finds today** (`npm --prefix src/Daoris.Web run names:check`): 519 findings —
  glossary 84, budget 131, form 288, door 16.
- **What it finds once the proposals land**, applied to the catalogues in memory and checked: glossary
  0, form 0, door 0. The proposals obey the rules they propose. **107 budget
  judgements remain**, which the renames leave as a report (D54): mostly a row's phrases and a chip that
  carries a name (*declared by plugin X*, *waiting for this turn to end*) measured against a pill's room,
  the plugin kit's three long choices, and English confirming presses a few characters over. The window
  decides each (design §4).

## 2. The ten that matter most

| # | Where | English, today → proposed | 中文, today → proposed | Why |
|---|---|---|---|---|
| 1 | `settings.domain.ai` (nav) | Daoris's own AI → **AI features** | Daoris 自身的 AI → **AI 功能** | The owner's example. A place is one noun; the possessive was carried into Chinese. |
| 2 | `settings.domain.agents` (nav) | Agents & accounts → **Agents** | 智能体与账户 → **智能体** | The owner's example. One noun; the domain's sections name the accounts. |
| 3 | `settings.sync.title` (section) | Bring up to date → **Updates** | 同步到最新 → **更新** | The owner's example: the heading and the press under it named two acts, and 同步 is sync's word. Its presses become *Look for updates* 「检查更新」 and *Bring up to date (3)* 「更新到最新（3）」. |
| 4 | `settings.domain.start` (nav) | Get started → **Setup** | 开始使用 → **配置** | Three names for one guide: *Get started* in the list, *Set up Daoris* in the menu, *setup* on the status bar. |
| 5 | `nav.projects` (nav) | Projects → **Repositories** | 项目 → **仓库** | One concept, two words: every row, button and sentence in the view says *repository*. The owner's call. |
| 6 | `asks.group` (section) | Asks ({{count}}) | 请求（{{count}}） → **需求（{{count}}）** | 请求 is every request's word, a pull request's and an agent's for more; 需求 names what a person wants made. The owner's call. |
| 7 | `work.review.tab` (tab) | Review | 改动 → **审阅** | The review was 改动 on its tab, 审阅 in the head and 查看 in the menus. |
| 8 | `sessionState.awaiting-person` (status) | awaiting person → **waiting on you** | 等待人工 → **等你处理** | One state, three words: *awaiting person*, *waiting on you*, *waiting for you*; 「人工」 is manual labour. |
| 9 | `overview.tiles.adopted.label` (field) | Adopted projects → **Adopted repositories** | 已加入的项目 → **已采用的仓库** | Adopt was 加入, 采用 and 接入; and the view the tile opens lists repositories. |
| 10 | `quests.detail.done` (button) | done → **Mark done** | 完成 → **标为完成** | Buttons are sentence case, verb first: 133 of the 183 were lower-case and 50 capitalised. A quest drawer reads *Take · Mark done · Decline…* 「接下 · 标为完成 · 谢绝…」. |

## 3. The rows

### Settings — The domain list

| Key | Kind | English | 中文 | Why |
|---|---|---|---|---|
| `settings.title` | title | Settings | 设置 |  |
| `settings.domain.start` | nav | Get started → **Setup** | 开始使用 → **配置** | A door names its destination: the Daoris menu says *Set up Daoris* and the status bar *setup: 2 of 5*, and the guide had a third name |
| `settings.domain.appearance` | nav | Appearance | 外观 |  |
| `settings.domain.ai` | nav | Daoris's own AI → **AI features** | Daoris 自身的 AI → **AI 功能** | A place is one noun: *Daoris's own AI* is a possessive, and 「Daoris 自身的 AI」 the possessive carried into Chinese; the domain holds the jobs Daoris gives a model or an agent |
| `settings.domain.workspace` | nav | Workspace | 工作区 |  |
| `settings.domain.driver` | nav | Driver | 驱动 |  |
| `settings.domain.agents` | nav | Agents & accounts → **Agents** | 智能体与账户 → **智能体** | A place is one noun: accounts are an agent's, and the domain's sections name them; 「智能体与账户」 is the conjunction translated |
| `settings.domain.permissions` | nav | Permissions | 权限 |  |
| `settings.domain.plugins` | nav | Plugins | 插件 |  |
| `settings.domain.browser` | nav | Browser | 浏览器 |  |
| `settings.domain.logs` | nav | Logs → **Machine log** | 日志 → **本机日志** | The domain holds one thing and its section calls it *Machine log*; the list names it so, and the section's own title goes (U57, NAME1b) |

### Settings — Get started (proposed: Setup)

| Key | Kind | English | 中文 | Why |
|---|---|---|---|---|
| `setup.state.done` | status | done | 已完成 |  |
| `setup.state.todo` | status | to do | 待完成 |  |
| `setup.state.optional` | status | optional | 可选 |  |
| `setup.step.agent.title` | section | An agent | 一个智能体 → **智能体** | A Chinese title is a noun; 「一个」 is English's article carried over |
| `setup.step.helper.title` | section | Daoris's own agent → **Ask Daoris's agent** | Daoris 自身的智能体 → **问道衍的智能体** | The step sets the agent Ask Daoris runs on; *own* was the old domain's possessive |
| `setup.step.repositories.title` | section | A workspace and its repositories | 工作区及其仓库 → **工作区与仓库** | Parallel noun phrases; 「及其」 is the English *and its* |
| `setup.step.driven.title` | section | What is driven | 驱动哪些仓库 → **驱动范围** | A Chinese title names what the step sets, never an English question carried over |
| `setup.step.landing.title` | section | How work lands | 工作如何落地 → **落地方式** | As the Workspace domain's section of the same name |
| `setup.step.rules.title` | section | What agents may do | 智能体可以做什么 → **智能体权限** | Its door is Permissions; the Chinese was the English question translated |
| `setup.door.agents` | button | open Agents & accounts → **Open Agents** | 打开智能体与账户 → **打开智能体** | A door names its destination: the domain is *Agents* |
| `setup.door.ai` | button | open Daoris's own AI → **Open AI features** | 打开 Daoris 自身的 AI → **打开 AI 功能** | A door names its destination: the domain is *AI features* |
| `setup.door.add` | button | Add repository… | 添加仓库… |  |
| `setup.door.import` | button | Import a folder… | 导入一个文件夹… → **导入文件夹…** | A button names its object; 「一个」 is an article |
| `setup.door.projects` | button | open Projects → **Open Repositories** | 打开项目 → **打开仓库** | A door names its destination: the view is *Repositories* (owner's call, nav.projects) |
| `setup.door.workspace` | button | open Workspace settings → **Open Workspace** | 打开工作区设置 → **打开工作区** | A door names its destination: the domain is *Workspace*, not *Workspace settings* |
| `setup.door.permissions` | button | open Permissions → **Open Permissions** | 打开权限 | Sentence case |
| `setup.copied` | status | copied | 已复制 |  |
| `setup.ask.button` | button | Set up with Ask Daoris | 和问道衍一起配置 | Over the button budget (22), kept: it sits in Get started's head, not the side bar |
| `setup.atStart` | field | Don't open at start | 启动时不打开 |  |

### Settings — Appearance

| Key | Kind | English | 中文 | Why |
|---|---|---|---|---|
| `settings.theme.label` | field | Theme | 主题 |  |
| `settings.theme.system` | choice | System | 跟随系统 |  |
| `settings.theme.light` | choice | Light | 浅色 |  |
| `settings.theme.dark` | choice | Dark | 深色 |  |
| `settings.language.label` | field | Language | 语言 |  |
| `language.en` | choice | English | English |  |
| `language.zh` | choice | 中文 | 中文 |  |

### Settings — Daoris's own AI (proposed: AI features)

| Key | Kind | English | 中文 | Why |
|---|---|---|---|---|
| `settings.ai.search.label` | field | Search and convergence | 搜索与同归 |  |
| `settings.ai.helper.label` | field | Ask Daoris | 问道衍 |  |
| `settings.ai.helper.off` | choice | Off — starters only | 关闭——仅入门提示 |  |
| `settings.ai.intake.label` | field | Intake | 受理 |  |
| `settings.ai.intake.off` | choice | Off — declarations only | 关闭——仅按声明 |  |
| `settings.ai.intake.runsAs` | field | runs as → **Runs as** | 运行账户 | Sentence case |
| `settings.ai.unknown` | status | not yet known | 尚不知道 |  |

### Settings — Workspace

| Key | Kind | English | 中文 | Why |
|---|---|---|---|---|
| `settings.workspaces.title` | section | {{count}} workspace | {{count}} 个工作区 |  |
| `settings.workspaces.inView` | status | in view | 当前查看 → **当前** | A status word is a bare word for a condition; 查看 is the review's old word |
| `settings.workspaces.repositories` | status | {{count}} repository | {{count}} 个仓库 |  |
| `wiring.title` | section | What a start runs on | 启动时用什么 → **启动配置** | A Chinese title names what the section holds; 「启动时用什么」 is the English question translated |
| `wiring.job.work` | status | sessions | 会话 |  |
| `wiring.job.intake` | status | intake | 受理 |  |
| `wiring.ready` | status | ready | 就绪 |  |
| `wiring.held` | status | held → **blocked** | 受阻 | *Held* is a repository you paused (hold); this is a start that cannot run, which the Chinese already says |
| `wiring.agent` | field | agent → **Agent** | 智能体 | Sentence case |
| `wiring.account` | field | account → **Account** | 账户 | Sentence case |
| `wiring.version` | field | version → **Version** | 版本 | Sentence case |
| `wiring.versionUnknown` | status | not known | 未知 |  |
| `settings.wiring.title` | section | Wiring | 接线 |  |
| `settings.wiring.label` | field | Remotes map | 远端映射 |  |
| `settings.wiring.cancel` | button | cancel → **Never mind** | 取消 | The back-out is *Never mind* everywhere else |
| `settings.wiring.addTitle` | section | Wire a workspace | 接入一个工作区 → **为工作区接线** | Wire is 接线; 接入 was adopt's stray word |
| `settings.wiring.workspace` | field | workspace → **Workspace** | 工作区 | Sentence case |
| `settings.wiring.workspacePlaceholder` | placeholder | default | default |  |
| `settings.wiring.url` | field | deployment → **Deployment** | 部署地址 | Sentence case |
| `settings.wiring.key` | field | key → **Key** | 密钥 | Sentence case |
| `settings.wiring.add` | button | wire it → **Wire** | 接上 → **接线** | A button in its object's own form is its verb alone, never *it*; wire is 接线 |
| `settings.wiring.remove` | button | unwire → **Unwire** | 断开 | Sentence case |
| `settings.landing.title` | section | How work lands | 工作如何落地 → **落地方式** | A Chinese title names what the section holds; 「工作如何落地」 is the English question translated |
| `settings.landing.pluginNone` | choice | you push it → **You push it** | 由你推送 | Sentence case |
| `settings.landing.pluginNamed` | choice | plugin {{plugin}} → **Plugin {{plugin}}** | 插件 {{plugin}} | Sentence case |
| `settings.landing.form.merge` | choice | merge → **Merge** | 合并 | Sentence case |
| `settings.landing.form.branch` | choice | branch → **Branch** | 分支 | Sentence case |
| `settings.landing.set` | button | Set → **Save** | 设置 → **保存** | 设置 is Settings' own name; the press saves a value, as the account's *Save* does |
| `settings.landing.tidy` | field | tidy once landed → **Clean up once landed** | 落地后清理 | Clean up is the name; *tidy* is its code word |
| `settings.landing.clear` | button | Clear | 清除 |  |
| `settings.lines.title` | section | Lines | 主线 |  |
| `settings.lines.workspaceField` | field | The line for {{workspace}} | {{workspace}} 的主线 |  |
| `settings.lines.repositoryField` | field | The line for {{repository}} | {{repository}} 的主线 |  |
| `settings.lines.eachCheckout` | placeholder | each checkout's own | 各检出自己的 |  |
| `settings.lines.unnamed` | placeholder | none named | 未指明 |  |
| `settings.lines.set` | button | Set → **Save** | 设置 → **保存** | As *How work lands*' Save |
| `settings.lines.clear` | button | Clear | 清除 |  |
| `settings.sweep.title` | section | Session branches | 会话分支 |  |
| `settings.sweep.goes` | status | goes | 移除 |  |
| `settings.sweep.kept` | status | kept | 保留 |  |
| `settings.sweep.clean` | button | Clean up {{count}} branch | 清理 {{count}} 个分支 |  |
| `settings.sweep.look` | button | Look again | 重新查看 → **重新检查** | One phrase for looking again, as Updates' and the agents' |
| `settings.sweep.landed.title` | section | Branches landings made → **Landed branches** | 落地产生的分支 → **落地分支** | A title is a noun phrase; *Branches landings made* reads as a sentence cut short |
| `settings.sync.title` | section | Bring up to date → **Updates** | 同步到最新 → **更新** | The owner's example: the section names what it holds, and its presses say look and bring in the same word; 同步 is sync's word |
| `settings.sync.look` | button | Look for updates | 查看更新 → **检查更新** | 检查更新 is the phrase for looking for updates; 查看 is the review's old word |
| `settings.sync.lookAgain` | button | Look again | 重新查看 → **重新检查** | As *Look for updates* |
| `settings.sync.lookingButton` | button | Looking… | 正在查看… → **正在检查…** | As *Look for updates* |
| `settings.sync.apply` | button | Bring up to date: {{count}} change → **Bring up to date ({{count}})** | 同步到最新：{{count}} 项更改 → **更新到最新（{{count}}）** | A button is the act, its count after it; 同步 is sync's word |
| `settings.sync.bringingButton` | button | Bringing up to date… | 正在同步到最新… → **正在更新…** | 同步 is sync's word |
| `settings.sync.apart.label` | section | Repositories not looked at | 未查看的仓库 → **未检查的仓库** | As *Look for updates*: a look is 检查 |
| `settings.sync.apart.all` | field | All {{count}} | 全部 {{count}} 个 |  |
| `settings.sync.apart.include` | button | Include and look ({{count}}) | 纳入并查看（{{count}}） → **纳入并检查（{{count}}）** | As *Look for updates*: a look is 检查 |
| `settings.sync.moves` | status | moves | 移动 |  |
| `settings.sync.stays` | status | stays | 不动 |  |
| `settings.sync.line.none` | status | (no line) | （无主线） |  |
| `settings.sync.line.notFetched` | status | not fetched | 未获取 |  |
| `settings.sync.notFetched.label` | section | Not fetched | 未获取 |  |
| `settings.sync.branch.landed` | status | a landing's → **landed** | 落地产生 → **已落地** | A status word is a participle, not a possessive |

### Settings — Driver

| Key | Kind | English | 中文 | Why |
|---|---|---|---|---|
| `settings.home.label` | field | Daoris home | Daoris 主目录 |  |
| `settings.notify.label` | field | Tell me when a session parks or ends unasked → **Notify me when a session parks** | 会话停下等人、或非我所愿地结束时告诉我 → **会话挂起时通知我** | Over the field budget in both (44 and 20); park is 挂起, and the hint already says *or ends unasked* |
| `settings.strikes.label` | field | Park a quest after this many failed sessions → **Failures before a quest parks** | 失败这么多次后暂停该委托 → **委托挂起前的失败次数** | Over the field budget (44); park is 挂起, where 暂停 is hold's |

### Settings — Agents & accounts (proposed: Agents)

| Key | Kind | English | 中文 | Why |
|---|---|---|---|---|
| `harness.title` | section | Agent tools → **Agents on this machine** | 智能体工具 → **本机智能体** | Agent is the name; *tool* is also a tool call, and 「智能体工具」 is two names at once |
| `harness.body` | field | Which account each tool runs as → **Account each agent runs as** | 每个工具以哪个账户运行 → **各智能体所用账户** | Agent, not tool; a field names its value, not a question |
| `harness.absent` | status | not installed | 未安装 |  |
| `harness.pin.open` | button | Pin a version | 固定版本 |  |
| `harness.pin.action` | button | pin it → **Pin** | 固定 | A button names its object or stands alone, never *it* |
| `harness.pin.unpin` | button | use PATH again → **Unpin** | 改回用 PATH → **取消固定** | The act's name, where the old one named its outcome; the row then says *runs from PATH* |
| `harness.pin.pinned` | status | Daoris runs {{version}} → **pinned {{version}}** | Daoris 运行 {{version}} → **已固定 {{version}}** | A status word, parallel with *pinned — not installed* |
| `harness.pin.missing` | status | pinned {{version}} — not installed | 已固定 {{version}}——但没有安装 |  |
| `harness.pin.fromPath` | status | runs from PATH | 使用 PATH 上的版本 → **取自 PATH** | One phrase for one fact: *What a start runs on* says 取自 PATH |
| `harness.pin.fromPathAbsent` | status | runs from PATH once it is there → **not on PATH yet** | 装到 PATH 上以后使用那里的版本 → **PATH 上尚无** | Over the status budget in both (31 and 17) |
| `harness.pin.placeholder` | placeholder | 1.2.3 | 1.2.3 |  |
| `harness.spawns` | status | sessions spawn on this one → **used by sessions** | 会话在此工具上启动 → **会话所用** | Over the status budget (26); agent, not tool |
| `harness.declaredBy` | status | declared by plugin {{plugin}} | 由插件 {{plugin}} 声明 | Over the status budget with its name, kept: the plugin's name is the point |
| `harness.install` | button | install → **Install** | 安装 | Sentence case |
| `harness.update` | button | update → **Update** | 更新 | Sentence case |
| `harness.refresh` | button | look again → **Look again** | 重新检测 → **重新检查** | Sentence case; one phrase for looking again |
| `harness.accounts` | section | Accounts | 账户 |  |
| `harness.installed` | status | installed | 已安装 |  |
| `harness.doors` | section | Ways in | 接入方式 |  |
| `harness.wire.pipe` | status | direct | 直连 |  |
| `harness.wire.acp` | status | protocol | 协议 |  |
| `harness.profile.signInNew` | button | Sign in to another account | 登录另一个账户 | Over the button budget (26), kept: the account card is in Settings' column, and a shorter name lost *another* |
| `harness.profile.addKey` | button | Add an API key | 添加 API 密钥 |  |
| `harness.profile.keyPlaceholder` | placeholder | paste the key | 粘贴密钥 |  |
| `harness.profile.keySave` | button | Save the key → **Save key** | 保存密钥 | A button is the verb and its object, with no article |
| `harness.profile.sessionsUse` | status | sessions use this → **used by sessions** | 会话使用此账户 → **会话所用** | Parallel with the agent's *used by sessions* |
| `harness.profile.workspaceUses` | status | sessions in {{workspace}} use this → **used in {{workspace}}** | {{workspace}} 中的会话使用它 → **{{workspace}} 所用** | Parallel with *used by sessions*; no pronoun |
| `harness.profile.use` | button | Make default | 设为默认 |  |
| `harness.profile.remove` | button | Remove | 移除 |  |
| `harness.profile.removeMeanIt` | button | Remove it → **Remove account** | 确认移除 → **确认移除账户** | The confirming press names what it removes, in both languages |
| `harness.profile.useForPlaceholder` | placeholder | use for a workspace… | 用于某个工作区…… → **用于某个工作区…** | One ellipsis, the UI's mark |
| `harness.own` | status | this machine's own | 本机自己的 |  |
| `harness.machineDefault` | status | this machine's default | 本机默认 |  |
| `harness.login.action` | button | Log in → **Sign in** | 登录 | Sign in is the name: the row's sign-in is the tool's own, and *Log in* was a second word for it |
| `harness.login.again` | button | Log in again → **Sign in again** | 重新登录 | As *Sign in* |
| `harness.login.in` | status | logged in → **signed in** | 已登录 | As *Sign in* |
| `harness.login.out` | status | not logged in → **not signed in** | 未登录 | As *Sign in* |
| `harness.login.unknown` | status | unknown | 未知 |  |
| `harness.login.keyed` | status | unchecked | 未验证 |  |
| `harness.profileOut` | choice | {{name}} (not logged in) → **{{name}} (not signed in)** | {{name}}（未登录） | As *Sign in* |
| `harness.settings.open` | button | Model & effort | 模型与推理强度 |  |
| `harness.settings.model` | field | Model | 模型 |  |
| `harness.settings.effort` | field | Effort | 推理强度 |  |
| `harness.settings.toolDefault` | choice | the tool's own default → **The agent's own default** | 工具自己的默认值 → **智能体自己的默认值** | Agent, not tool; sentence case |
| `harness.settings.otherModel` | choice | Another model… | 其他模型… |  |
| `harness.settings.notSet` | choice | not set → **Not set** | 未设置 | Sentence case |
| `harness.settings.save` | button | Save | 保存 |  |
| `harness.settings.modelIdPlaceholder` | placeholder | a model id the tool accepts → **a model ID the agent accepts** | 工具接受的模型 ID → **智能体接受的模型 ID** | Agent, not tool; ID as a word is capitals |
| `usage.title` | section | What each account has carried → **Usage** | 各账户各自承担了多少 → **用量** | A door names its destination: the Agents menu's *Usage* opens here; 「各账户各自承担了多少」 was the English question translated |

### Settings — Permissions

| Key | Kind | English | 中文 | Why |
|---|---|---|---|---|
| `settings.rules.file` | field | The rules file | 规则文件 |  |
| `settings.rules.defaults` | section | Daoris's defaults | Daoris 的默认规则 |  |
| `settings.rules.defaultOn` | field | {{id}} is on | {{id}} 已开启 |  |
| `settings.rules.list.allow` | status | allow | 允许 |  |
| `settings.rules.list.ask` | status | ask | 询问 |  |
| `settings.rules.list.deny` | status | deny | 拒绝 |  |
| `settings.rules.scopeMachine` | section | Every session on this machine | 本机的每个会话 |  |
| `settings.rules.scopeWorkspace` | section | workspace {{name}} → **Workspace {{name}}** | 工作区 {{name}} | Sentence case |
| `settings.rules.scopeRepository` | section | repository {{name}} → **Repository {{name}}** | 仓库 {{name}} | Sentence case |
| `settings.rules.addTitle` | section | Add a rule | 添加规则 |  |
| `settings.rules.add` | button | add → **Add rule** | 添加 → **添加规则** | A button names its object; sentence case |
| `settings.rules.on` | status | on | 开启 |  |
| `settings.rules.off` | status | off | 关闭 |  |
| `settings.rules.proposals.title` | section | Proposed by agents → **Proposals** | 智能体提议的更改 → **提议** | A door names its destination: the Agents menu's *Proposals* opens here |
| `settings.rules.proposals.accept` | button | accept → **Accept** | 接受 → **采纳** | Accept is 采纳, as the review's is; sentence case |
| `settings.rules.proposals.decline` | button | decline → **Decline** | 拒绝 → **谢绝** | Decline is 谢绝; 拒绝 is the rules' *deny* |
| `settings.rules.proposals.earlier` | section | Earlier proposals ({{count}}) | 更早的提议（{{count}}） |  |
| `settings.rules.proposals.state.proposed` | status | not yet judged | 尚未判断 → **待判断** | A state waiting is 待 and the verb |
| `settings.rules.proposals.state.waiting` | status | waiting for you → **waiting on you** | 等你决定 → **等你处理** | One word for waiting on the person, as a session's |
| `settings.rules.proposals.state.applied` | status | applied | 已生效 → **已应用** | One act, one word: the proposal card's press is *Apply* (应用) |
| `settings.rules.proposals.state.accepted` | status | accepted | 已接受 → **已采纳** | Accept is 采纳 |
| `settings.rules.proposals.state.declined` | status | declined | 已拒绝 → **已谢绝** | Decline is 谢绝 |
| `settings.rules.proposals.state.refused` | status | refused | 被拒收 |  |
| `settings.rules.proposals.state.unchanged` | status | unchanged | 无变化 |  |
| `settings.across.title` | section | Reading and writing across repositories → **Across repositories** | 跨仓库读取与写入 → **跨仓库读写** | Over the section budget (39); one noun phrase in each language |
| `settings.across.workspaceField` | field | Reading across in {{workspace}} | {{workspace}} 中的跨仓库读取 |  |
| `settings.across.repositoryField` | field | Reading {{repository}}'s checkout | 读取 {{repository}} 的检出 |  |
| `settings.across.inherited` | choice | inherit ({{state}}) → **Inherit ({{state}})** | 继承（{{state}}） | Sentence case |
| `settings.across.on` | choice | on → **On** | 开 → **开启** | Sentence case; one pair of words for a switch across Permissions, as its defaults' 开启 and 关闭 |
| `settings.across.off` | choice | off → **Off** | 关 → **关闭** | As *On* |
| `settings.across.writesInto` | field | Its sessions may also write into | 它的会话还可以写入 |  |
| `settings.across.stopWriting` | button | stop {{repository}} writing into {{to}} → **Stop {{repository}} writing into {{to}}** | 停止 {{repository}} 写入 {{to}} | Sentence case |
| `settings.across.addWritePlaceholder` | placeholder | let it write into… | 允许写入…… → **允许写入…** | One ellipsis, the UI's mark |

### Settings — Plugins

| Key | Kind | English | 中文 | Why |
|---|---|---|---|---|
| `plugin.folder` | field | The plugins folder | 插件文件夹 |  |
| `plugin.running` | status | running | 运行中 |  |
| `plugin.off` | status | off | 已关闭 |  |
| `plugin.enable` | button | Turn on | 开启 |  |
| `plugin.disable` | button | Turn off | 关闭 |  |
| `plugin.forget` | button | Remove | 移除 |  |
| `plugin.update.ask` | button | Update… | 更新… |  |
| `plugin.source.folder` | field | Added from | 添加自 |  |
| `plugin.update.title` | section | What an update changes | 更新会改变什么 → **更新内容** | A Chinese title names what the section holds |
| `plugin.update.what.version` | field | version → **Version** | 版本 | Sentence case |
| `plugin.update.what.command` | field | command → **Command** | 命令 | Sentence case |
| `plugin.update.what.points` | field | points → **Points** | 挂点 | Sentence case |
| `plugin.update.what.harnesses` | field | agents → **Agents** | 智能体 | Sentence case |
| `plugin.update.what.servers` | field | servers → **Servers** | 服务器 | Sentence case |
| `plugin.update.apply` | button | Update now | 立即更新 |  |
| `plugin.update.cancel` | button | Not now | 暂不 |  |
| `plugin.offers.title` | section | Daoris's own plugins | Daoris 自带的插件 |  |
| `plugin.offers.notInstalled` | status | not installed | 未安装 |  |
| `plugin.offers.install` | button | Install | 安装 |  |
| `plugin.kit.title` | section | Make a plugin | 制作插件 |  |
| `plugin.kit.new` | button | New | 新建 |  |
| `plugin.kit.id` | field | Plugin id → **Plugin ID** | 插件 id → **插件 ID** | ID as a word is capitals; `id` stays in backticks as a field |
| `plugin.kit.points` | field | Where it speaks → **Points** | 在哪里说话 → **挂点** | Point is the name, as the update's diff says |
| `plugin.kit.kind.decision` | choice | a decision, before a planned start spends anything → **A decision, before a planned start spends anything** | 一个决定，在计划的启动花费任何东西之前 | Sentence case; over the choice budget, kept: each names a point and when it speaks |
| `plugin.kit.kind.observation` | choice | an observation, after a session ends → **An observation, after a session ends** | 一次观察，在会话结束之后 | Sentence case |
| `plugin.kit.kind.act` | choice | an act, once a landing has made its branch → **An act, once a landing has made its branch** | 一个动作，在落地已建好它的分支之后 | Sentence case |
| `plugin.kit.folder` | field | Make it in | 建在 |  |
| `plugin.kit.folderPlaceholder` | placeholder | a folder in your plugins repository | 你的插件仓库中的一个文件夹 |  |
| `plugin.kit.choose` | button | Choose… | 选择… |  |
| `plugin.kit.tryFolder` | section | Try a folder | 试运行文件夹 |  |
| `plugin.kit.try` | button | Try | 试运行 |  |
| `plugin.kit.ok` | status | ok | 通过 |  |
| `plugin.kit.failed` | status | failed | 未通过 |  |
| `plugin.kit.said` | field | What it said on stderr | 它在 stderr 上说的话 |  |

### Settings — Browser

| Key | Kind | English | 中文 | Why |
|---|---|---|---|---|
| `settings.browser.which.title` | section | Which browser | 使用哪个浏览器 → **所用浏览器** | A Chinese title names what the section holds |
| `settings.browser.which.label` | field | Sessions and you use → **Browser for sessions and you** | 会话和你使用 → **会话与你所用的浏览器** | A field names its value; the label read as the end of a sentence |
| `settings.browser.which.daoris` | choice | Daoris's own | Daoris 自己的 → **Daoris 浏览器** | Daoris's browser is Daoris 浏览器 on every screen |
| `settings.browser.which.edge` | choice | your Edge → **Your Edge** | 你的 Edge | Sentence case |
| `settings.browser.driving.label` | field | Driving it now → **Driven by** | 现在谁在操作 → **当前操作者** | A field names its value (who), not a question |
| `settings.browser.driving.open` | button | open {{name}} → **Open {{name}}** | 打开 {{name}} | Sentence case |
| `settings.browser.links.label` | field | Links on the page open in | 页面上的链接打开于 |  |
| `settings.browser.links.system` | choice | the system's browser → **System browser** | 系统浏览器 | Sentence case; a choice is a noun |
| `settings.browser.links.daoris` | choice | Daoris's browser | Daoris 的浏览器 → **Daoris 浏览器** | Daoris's browser is Daoris 浏览器 on every screen |
| `settings.browser.favorites.title` | section | Favorites | 收藏 |  |
| `settings.browser.favorites.label` | field | Favorites file | 收藏文件 |  |
| `settings.browser.favorites.address` | field | address → **Address** | 地址 | Sentence case |
| `settings.browser.favorites.titleField` | field | title → **Title** | 标题 | Sentence case |
| `settings.browser.favorites.titlePlaceholder` | placeholder | the page's host | 页面的主机名 |  |
| `settings.browser.favorites.add` | button | keep it → **Add favorite** | 收藏 | A button names its object, never *it* |
| `settings.browser.favorites.remove` | button | remove → **Remove** | 移除 | Sentence case |
| `settings.browser.extensions.title` | section | Other software's extensions | 其他软件的扩展 |  |
| `settings.browser.extensions.label` | field | Registered for Chrome | 为 Chrome 注册的扩展 |  |
| `settings.browser.extensions.offer` | choice | offer → **Offer** | 询问 | Sentence case |
| `settings.browser.extensions.refuse` | choice | refuse → **Refuse** | 拒绝 | Sentence case |

### Settings — Logs (proposed: Machine log)

| Key | Kind | English | 中文 | Why |
|---|---|---|---|---|
| `settings.logs.title` | section | Machine log | 本机日志 | Goes in NAME1b: a card alone in its domain carries no title, since the list names it (U57) |
| `settings.logs.folder` | field | Folder | 文件夹 |  |
| `settings.logs.open` | button | Open the folder | 打开文件夹 |  |
| `settings.logs.since` | field | period → **Period** | 时段 | Sentence case |
| `settings.logs.span.1h` | choice | last hour → **Last hour** | 最近一小时 | Sentence case |
| `settings.logs.span.1d` | choice | last day → **Last day** | 最近一天 | Sentence case |
| `settings.logs.span.7d` | choice | last 7 days → **Last 7 days** | 最近 7 天 | Sentence case |
| `settings.logs.span.30d` | choice | last 30 days → **Last 30 days** | 最近 30 天 | Sentence case |
| `settings.logs.source` | field | source → **Source** | 来源 | Sentence case |
| `settings.logs.everySource` | choice | every source → **Every source** | 所有来源 | Sentence case |
| `settings.logs.event` | field | event → **Event** | 事件 | Sentence case |
| `settings.logs.everyEvent` | choice | every event → **Every event** | 所有事件 | Sentence case |
| `settings.logs.levels` | field | level → **Level** | 级别 | Sentence case |
| `settings.logs.floor.every` | choice | every level → **Every level** | 所有级别 | Sentence case |
| `settings.logs.floor.warn` | choice | warnings → **Warnings** | 警告 | Sentence case |
| `settings.logs.floor.error` | choice | errors → **Errors** | 错误 | Sentence case |
| `settings.logs.level.info` | status | info | 信息 |  |
| `settings.logs.level.warn` | status | warning | 警告 |  |
| `settings.logs.level.error` | status | error | 错误 |  |
| `settings.logs.refresh` | button | Read again | 重新读取 |  |

### Settings — An account's sign-in

| Key | Kind | English | 中文 | Why |
|---|---|---|---|---|
| `signin.title` | section | Signing in to {{profile}} | 正在登录 {{profile}} |  |
| `signin.titleNew` | section | Signing in to another {{harness}} account | 正在登录另一个 {{harness}} 账户 |  |
| `signin.cancel` | button | Cancel | 取消 → **取消登录** | The Cancel stops the sign-in; 取消 alone is the back-out every form has |
| `signin.copy` | button | Copy link | 复制链接 |  |
| `signin.copied` | status | Copied → **copied** | 已复制 | A status word is lower case |
| `signin.codePlaceholder` | placeholder | code from the page | 页面上的代码 |  |
| `signin.continue` | button | Continue | 继续 |  |
| `signin.output` | section | The tool's own output | 工具自身的输出 |  |

### The frame: the activity bar, the strip's menus, the regions, the status bar, the palette.

| Key | Kind | English | 中文 | Why |
|---|---|---|---|---|
| `nav.overview` | nav | Overview | 总览 |  |
| `nav.sessions` | nav | Sessions | 会话 |  |
| `nav.quests` | nav | Quests | 委托 |  |
| `nav.projects` | nav | Projects → **Repositories** | 项目 → **仓库** | One concept, two words: the view lists repositories, and every row, button and sentence in it says *repository* (项目 and 仓库, 15 and 4); the owner's call |
| `nav.convergence` | nav | Convergence | 同归 |  |
| `nav.map` | nav | Map | 地图 |  |
| `nav.search` | nav | Search | 搜索 |  |
| `nav.settings` | nav | Settings | 设置 |  |
| `menu.app` | nav | Daoris | 道衍 |  |
| `menu.view` | nav | View | 视图 |  |
| `menu.workspace` | nav | Workspace | 工作区 |  |
| `menu.agents` | nav | Agents | 智能体 |  |
| `menu.setup` | menu | Set up Daoris → **Setup** | 配置 Daoris → **配置** | A door names its destination: the domain is *Setup* |
| `menu.settings` | menu | Settings | 设置 |  |
| `menu.driver` | menu | Driver | 驱动 |  |
| `menu.plugins` | menu | Plugins | 插件 |  |
| `menu.refresh` | menu | Refresh the index | 刷新索引 |  |
| `menu.language` | menu | Switch language | 切换语言 |  |
| `menu.about` | menu | About Daoris | 关于 道衍 → **关于道衍** | Chinese beside Chinese is tight |
| `menu.workspace.none` | menu | No workspace yet | 还没有工作区 |  |
| `menu.workspace.add` | menu | Add repository… | 添加仓库… |  |
| `menu.workspace.import` | menu | Import a folder… | 导入一个文件夹… → **导入文件夹…** | A menu item names its object; 「一个」 is an article |
| `menu.workspace.wire` | menu | Wire to a remote… | 接线到远端部署… → **接线到远端…** | The remote is 远端; 部署 is the deployment it points at |
| `menu.workspace.settings` | menu | Workspace settings | 工作区设置 |  |
| `menu.agents.tools` | menu | Tools & accounts → **Agent settings** | 工具与账户 → **智能体设置** | A door names its destination (the domain *Agents*), parallel with *Workspace settings*; *Tools* was a third name |
| `menu.agents.rules` | menu | What agents may do → **Permissions** | 智能体可以做什么 → **权限** | A door names its destination: the domain is *Permissions* |
| `menu.agents.proposals` | menu | Proposals | 提议的更改 → **提议** | A door names its destination: the section is *Proposals* |
| `menu.agents.usage` | menu | Usage | 用量 |  |
| `menu.agents.ai` | menu | Daoris's own AI → **AI features** | Daoris 自身的 AI → **AI 功能** | A door names its destination: the domain is *AI features* |
| `layout.menu.rail` | menu | Session list | 会话列表 |  |
| `layout.menu.panel` | menu | Panel | 面板 |  |
| `layout.menu.right` | menu | Right side bar | 右侧栏 |  |
| `work.views.toRight` | menu | Move {{view}} to the right side bar | 把{{view}}移到右侧栏 |  |
| `work.views.toPanel` | menu | Move {{view}} to the panel | 把{{view}}移到面板 |  |
| `work.views.reset` | menu | Reset view locations | 重置视图位置 |  |
| `work.views.console` | tab | Console | 控制台 |  |
| `work.views.terminal` | tab | Terminal | 终端 |  |
| `work.review.tab` | tab | Review | 改动 → **审阅** | Review is 审阅, as the head's press and the palette's row; 改动 is the diff's *changes* |
| `work.review.timelineTab` | tab | Timeline | 时间线 |  |
| `work.timeline.title` | tab | Timeline | 时间线 |  |
| `work.panel.title` | tab | console → **Console** | 控制台 | Sentence case: the tab says *Console* where the panel's list does |
| `help.title` | tab | Ask Daoris | 问道衍 |  |
| `work.menu.monitor` | menu | Monitor window | 监视窗口 |  |
| `work.menu.browser` | menu | Browser → **Daoris's browser** | 浏览器 → **Daoris 浏览器** | The View menu's item opens Daoris's browser, not the Browser domain; it names what it opens |
| `work.dock.close` | button | close the side bar → **Close the side bar** | 关闭侧栏 | Sentence case |
| `work.dock.open` | button | open {{tab}} → **Open {{tab}}** | 打开{{tab}} | Sentence case |
| `work.dock.full` | button | fill the frame → **Fill the frame** | 铺满整个区域 → **铺满** | Sentence case; 「整个区域」 repeats what fill says |
| `work.dock.unfull` | button | back beside the session → **Back beside the session** | 回到会话旁边 | Sentence case |
| `work.panel.show` | button | show the panel → **Show the panel** | 显示面板 | Sentence case |
| `work.panel.hide` | button | hide the panel → **Hide the panel** | 隐藏面板 | Sentence case |
| `work.panel.stream.stopShort` | button | stop → **Stop** | 停止 | Sentence case |
| `work.rail.close` | button | close the rail → **Hide the session list** | 收起会话栏 → **收起会话列表** | The rail is the session list, as the View menu and the strip's toggle name it |
| `work.rail.open` | button | open the rail → **Show the session list** | 展开会话栏 → **展开会话列表** | As *Hide the session list* |
| `work.status.driver` | status | driver | 驱动 |  |
| `work.status.driverRunning` | status | ready | 就绪 |  |
| `work.status.driverStopped` | status | not answering | 无响应 |  |
| `work.status.driverAbsent` | status | none here | 本机没有 |  |
| `work.status.sessionsLabel` | status | active sessions | 进行中的会话 |  |
| `work.status.sessions` | status | {{count}} session | {{count}} 个会话 |  |
| `work.status.workspace` | status | workspace | 工作区 |  |
| `work.status.everyWorkspace` | status | every workspace | 全部工作区 |  |
| `work.status.remote` | status | remote | 远端 |  |
| `work.status.remoteWired` | status | wired | 已接线 |  |
| `work.status.remoteLocal` | status | local only | 仅本地 |  |
| `work.status.tierLabel` | status | recall | 检索 |  |
| `work.status.indexedLabel` | status | index | 索引 |  |
| `work.status.setupLabel` | status | setup | 配置 |  |
| `work.status.setup` | status | setup: {{done}} of {{of}} | 配置：{{done}}/{{of}} |  |
| `work.sync.label` | status | sync | 同步 |  |
| `work.sync.level` | status | synced | 已同步 |  |
| `work.sync.never` | status | not synced yet | 尚未同步 |  |
| `work.sync.walled` | status | unreachable | 无法连接 |  |
| `work.sync.inConflict` | section | In conflict | 冲突 |  |
| `work.sync.now` | menu | Sync now | 立即同步 |  |
| `work.sync.syncing` | menu | Syncing… | 同步中… |  |
| `work.sync.remotes` | menu | Remotes… → **Wiring…** | 远端接线… → **接线…** | A door names its destination, the section *Wiring*; today it opens the Workspace domain at its top, and NAME1b opens it at Wiring, as *Wire to a remote…* does (U72) |
| `palette.title` | section | Commands | 命令 |  |
| `palette.open` | button | Commands (Ctrl+K) | 命令（Ctrl+K） |  |
| `palette.scope` | status | every workspace | 全部工作区 |  |
| `palette.group.go` | section | go to → **Go to** | 前往 | Sentence case |
| `palette.group.do` | section | do → **Actions** | 操作 | A group is named by a noun, parallel with its siblings |
| `palette.group.work` | section | work → **Sessions** | 工作 → **会话** | *Work* named the frame D66 retired; the group's rows act on sessions |
| `command.go.overview` | command | Overview | 总览 |  |
| `command.go.sessions` | command | Sessions | 会话 |  |
| `command.go.quests` | command | Quests | 委托 |  |
| `command.go.projects` | command | Projects → **Repositories** | 项目 → **仓库** | A door names its destination (nav.projects) |
| `command.go.convergence` | command | Convergence | 汇聚 → **同归** | A door names its destination: the view is 同归, and 汇聚 was a second name for it |
| `command.go.map` | command | Map | 地图 |  |
| `command.go.search` | command | Search | 搜索 |  |
| `command.go.settings` | command | Settings | 设置 |  |
| `command.work.start` | command | Start a session… | 开启会话… |  |
| `command.work.review` | command | Review what this session landed | 查看该会话落地的改动 → **审阅该会话落地的工作** | Review is 审阅 |
| `command.work.monitor` | command | Open the monitor window | 打开监视窗口 |  |
| `command.work.browser` | command | Open Daoris's browser — sign in where sessions will look | 打开 Daoris 的浏览器——在会话会看的地方登录 → **打开 Daoris 浏览器——在会话会看的地方登录** | Daoris's browser is Daoris 浏览器 |
| `command.work.help` | command | Ask Daoris — what this machine lacks, and how to set it up | 问道衍——本机缺什么，以及如何设置 |  |
| `command.work.detach` | command | Open this session in its own window | 在单独的窗口中打开该会话 |  |
| `command.do.setup` | command | Set up Daoris — the steps this machine needs, in order → **Setup — the steps this machine needs, in order** | 配置 Daoris——本机需要的步骤，按先后排列 → **配置——本机需要的步骤，按先后排列** | A door names its destination: the domain is *Setup* |
| `command.do.ask` | command | Ask the workspace… | 向工作区提出请求… → **向工作区提需求…** | An ask is 需求 (the owner's call, asks.group) |
| `command.do.refresh` | command | Refresh the index | 刷新索引 |  |
| `command.do.language` | command | Switch language | 切换语言 |  |
| `sidebar.refresh` | button | refresh index → **Refresh the index** | 重建索引 → **刷新索引** | One act, one name: the Daoris menu and the palette say *Refresh the index*, and 重建 is a different act |
| `sidebar.refreshing` | button | reading… → **Refreshing…** | 读取中… → **刷新中…** | As *Refresh the index* |
| `browser.door.open` | button | open Daoris's browser → **Open Daoris's browser** | 打开 Daoris 的浏览器 → **打开 Daoris 浏览器** | Sentence case; Daoris's browser is Daoris 浏览器 |
| `browser.driving.menu` | section | Sessions driving Daoris's browser | 正在操作 Daoris 浏览器的会话 | Daoris's browser is Daoris 浏览器 |
| `scope.every` | choice | every workspace · {{count}} → **Every workspace · {{count}}** | 全部工作区 · {{count}} | Sentence case |
| `scope.none` | status | no workspace yet | 还没有工作区 |  |
| `about.index` | field | Index | 索引 |  |
| `about.surface` | field | Running as | 运行方式 |  |
| `common.close` | button | close → **Close** | 关闭 | Sentence case |
| `common.dismiss` | button | dismiss → **Dismiss** | 忽略 | Sentence case |
| `common.cancel` | button | never mind → **Never mind** | 算了 → **取消** | Sentence case; 取消 is the back-out, where 「算了」 reads as a shrug |

### Overview

| Key | Kind | English | 中文 | Why |
|---|---|---|---|---|
| `overview.title` | title | Overview | 总览 |  |
| `overview.tiles.adopted.label` | field | Adopted projects → **Adopted repositories** | 已加入的项目 → **已采用的仓库** | Adopt is 采用 (加入, 采用 and 接入 were three words for it); a repository, not a project |
| `overview.tiles.open.label` | field | Open quests | 待接委托 |  |
| `overview.tiles.progress.label` | field | In progress → **Taken quests** | 进行中 → **已接委托** | One word per state: the pill says *Taken*; parallel with *Open quests* |
| `overview.tiles.knowledge.label` | field | Knowledge entries | 知识条目 |  |
| `overview.outstanding.title` | section | Outstanding — oldest first | 悬而未决——最久在前 |  |
| `overview.outstanding.answer` | button | answer them → **Answer** | 去答复 → **答复** | A button names its object or stands alone, never *them*; 「去」 is filler |
| `overview.outstanding.ask` | button | ask for something → **Ask for something** | 有事相托 → **提需求** | Sentence case; an ask is 需求, and 「有事相托」 named no act |
| `overview.outstanding.emptyHeadline` | headline | Nothing is sitting | 无事搁置 |  |
| `overview.repositories.title` | section | Repositories, by what the index holds → **Repositories by index size** | 仓库，按索引所收内容 → **按索引大小排列的仓库** | Over the section budget (37); the design language already calls it *Repositories by index size* |
| `overview.repositories.button` | button | projects → **Repositories** | 项目 → **仓库** | A door names its destination (nav.projects) |
| `overview.repositories.emptyHeadline` | headline | The index holds nothing yet | 索引里还什么都没有 |  |

### Sessions

| Key | Kind | English | 中文 | Why |
|---|---|---|---|---|
| `work.attention.title` | section | What needs you | 等你处理 |  |
| `work.attention.emptyHeadline` | headline | Nothing is waiting on you | 没有需要你处理的事 → **没有等你处理的事** | The band's own name for what waits (等你处理) |
| `work.attention.parked` | status | parked at a checkpoint | 停在检查点等人 → **在检查点挂起** | Park is 挂起, where 暂停 is hold's |
| `work.attention.open.proposal` | status | open the ask to publish it | 打开请求以发布 → **打开需求以发布** | An ask is 需求 |
| `work.attention.open.intake` | status | open the ask to answer it | 打开请求以答复 → **打开需求以答复** | An ask is 需求 |
| `work.attention.intake` | status | its intake asked you | 受理会话在等你回答 |  |
| `work.attention.rule` | status | an agent asks to do more | 智能体请求放宽权限 |  |
| `work.attention.trust` | status | waiting on your trust | 等你授予信任 |  |
| `trust.title` | title | Trust this folder for the agent? | 让智能体信任这个文件夹？ | A question is kept: the card asks the agent's own question in the open (D73) |
| `chain.ask` | status | ask #{{id}} | 请求 #{{id}} → **需求 #{{id}}** | An ask is 需求 |
| `command.work.quickAsk` | command | Quick Ask — one question to Ask Daoris, without opening its side bar | 快速提问——向问道衍问一个问题，不必打开侧栏 |  |
| `work.rail.label` | section | sessions → **Sessions** | 会话列表 → **会话** | Sentence case; the list's head names what it holds, as the view does |
| `work.rail.menu.review` | menu | Review its work → **Review** | 查看它的改动 → **审阅** | A door names its destination, the review, without a pronoun |
| `work.rail.menu.copy` | menu | Copy its id → **Copy session ID** | 复制会话 ID | The object named, ID in capitals |
| `work.rail.search.placeholder` | placeholder | search by name or by what was said | 按名称或对话内容搜索 |  |
| `work.rail.search.byName` | section | by name → **By name** | 名称匹配 | Sentence case |
| `work.rail.search.byContent` | section | in what was said → **In what was said** | 对话内容匹配 | Sentence case |
| `work.rail.empty.headline` | headline | Nothing is running | 当前没有会话在运行 |  |
| `work.rail.ended` | section | ended → **Ended** | 已结束 | Sentence case |
| `work.monitor.detach` | menu | Open in its own window | 在单独的窗口中打开 |  |
| `work.kind.driven` | status | driven | 驱动 |  |
| `work.kind.chat` | status | chat | 对话 → **聊天** | A chat is 聊天; 对话 is the conversation every structured session keeps |
| `work.intake.kind` | status | intake | 受理 |  |
| `work.identity.conversation` | title | conversation → **Chat** | 对话 → **聊天** | One concept: a chat is titled *Chat* until its first message names it, as its row's kind says |
| `work.identity.session` | title | session → **Session** | 会话 | A title is sentence case |
| `work.group.held` | status | held | 已暂停 |  |
| `work.group.drivable` | status | drives here | 在此驱动 |  |
| `work.group.busy` | status | busy | 占用中 |  |
| `work.group.notAdopted` | status | not adopted | 未接入 → **未采用** | Adopt is 采用 |
| `work.group.noCheckout` | status | not on this machine | 本机没有此检出 → **不在本机** | Over the status budget (7); the same fact in fewer words |
| `work.start.title` | title | Start a session | 开启会话 |  |
| `work.start.repository` | field | repository → **Repository** | 仓库 | Sentence case |
| `work.start.harness` | field | agent tool → **Agent** | 智能体工具 → **智能体** | Agent is the name; *agent tool* is two |
| `work.start.harnessDefault` | choice | this machine's default → **This machine's default** | 本机默认 | Sentence case |
| `work.start.harnessDefaultNamed` | choice | this machine's default: {{name}} → **This machine's default: {{name}}** | 本机默认：{{name}} | Sentence case |
| `work.start.profile` | field | account → **Account** | 账户 | Sentence case |
| `work.start.profileDefault` | choice | whatever this machine already decided → **As already set** | 沿用本机既有的选择 → **沿用已有设置** | Over the choice budget (37); not *this machine's default*, since a workspace may set its own and the start takes that first |
| `work.start.ownTree` | field | in a working tree of its own → **In a tree of its own** | 使用它自己的工作树 → **使用独立工作树** | Tree is the name; the Chinese names no pronoun |
| `work.start.go` | button | start → **Start** | 开启 | Sentence case |
| `work.head.repository` | field | repository → **Repository** | 仓库 | Sentence case |
| `work.head.quest` | field | quest → **Quest** | 委托 | Sentence case |
| `work.head.tool` | field | tool → **Agent** | 工具 → **智能体** | Agent is the name; *tool* is also a tool call |
| `work.head.machine` | field | machine → **Machine** | 机器 | Sentence case |
| `work.head.started` | field | started → **Started** | 开始于 | Sentence case |
| `work.head.elapsed` | field | running → **Running for** | 已运行 | Sentence case; a duration follows |
| `work.head.ran` | field | ran for → **Ran for** | 运行了 | Sentence case |
| `work.head.moved` | field | moved → **Moved** | 变动于 | Sentence case |
| `work.head.itsWork` | field | its work → **Its work** | 它的工作 | Sentence case |
| `work.head.review` | button | review → **Review** | 审阅 | Sentence case |
| `work.head.noteMore` | button | show all → **Show all** | 展开全部 | Sentence case |
| `work.head.noteLess` | button | show less → **Show less** | 收起 | Sentence case |
| `work.head.waiting` | section | This one is waiting on you | 这个会话在等你 |  |
| `work.intake.title` | title | intake for ask #{{ask}} → **Intake for ask #{{ask}}** | 请求 #{{ask}} 的受理会话 → **需求 #{{ask}} 的受理会话** | Sentence case; an ask is 需求 |
| `work.intake.ask` | field | ask → **Ask** | 请求 → **需求** | Sentence case; an ask is 需求 |
| `work.intake.room` | field | room → **Room** | 受理目录 | Sentence case |
| `work.intake.waiting` | section | This intake is asking you | 这个受理会话在问你 |  |
| `work.intake.answer` | button | answer ask #{{ask}} → **Answer ask #{{ask}}** | 答复请求 #{{ask}} → **答复需求 #{{ask}}** | Sentence case; an ask is 需求 |
| `work.intakeRunning.title` | section | This intake is reading ask #{{ask}} | 这个受理会话正在读请求 #{{ask}} → **这个受理会话正在读需求 #{{ask}}** | An ask is 需求 |
| `work.intakeRunning.open` | button | open ask #{{ask}} → **Open ask #{{ask}}** | 打开请求 #{{ask}} → **打开需求 #{{ask}}** | Sentence case; an ask is 需求 |
| `work.awaiting.finish` | button | finish it → **Finish** | 就此完成 → **完成** | A button stands alone, never *it*; 「就此」 is filler |
| `work.awaiting.decline` | button | decline… → **Decline…** | 谢绝… | Sentence case |
| `work.awaiting.declineConfirm` | button | decline with this reason → **Decline with this reason** | 以此理由谢绝 | Sentence case |
| `work.awaiting.stop` | button | stop it → **Stop session** | 停止它 → **停止会话** | A button names its object, never *it* |
| `work.awaiting.carryOn` | button | answer and carry on… → **Answer and carry on…** | 回答并继续… | Sentence case |
| `work.awaiting.answerConfirm` | button | carry on with this answer → **Carry on with this answer** | 以此回答继续 | Sentence case |
| `work.frame.open` | button | open in Sessions → **Open in Sessions** | 在会话中打开 → **在「会话」中打开** | Sentence case; a view's name in a Chinese sentence takes 「」, as *Open in Sessions* on a quest does |
| `work.open` | button | open in Sessions → **Open in Sessions** | 在「会话」中打开 | Sentence case |
| `work.monitor.title` | title | Monitor → **Monitor window** | 监视 → **监视窗口** | The window names itself as the View menu's item does |
| `work.monitor.empty.headline` | headline | Nothing is running | 当前没有会话在运行 |  |
| `work.detached.gone.headline` | headline | No record of that session | 没有这个会话的记录 |  |
| `work.attended.none.headline` | headline | Nothing attended | 尚未选中会话 |  |
| `work.relations.title` | section | Who it worked with | 它与谁协作 → **协作关系** | A Chinese title names what the section holds |
| `work.relations.askedBy` | field | Asked by | 发起者 |  |
| `work.relations.resumed` | field | Carried on once #{{id}} was answered | #{{id}} 得到答复后继续 |  |
| `work.relations.asked` | field | Asked of another repository | 向另一个仓库提出 |  |
| `work.find.firstFailure` | button | first failure → **First failure** | 首次失败 | Sentence case |
| `work.find.lastWords` | button | last words → **Last words** | 最后的话 | Sentence case |
| `work.find.placeholder` | placeholder | find in this session | 在本会话中查找 |  |
| `work.find.none` | status | nothing found | 未找到 |  |
| `work.find.previous` | button | previous match → **Previous match** | 上一处 | Sentence case |
| `work.find.next` | button | next match → **Next match** | 下一处 | Sentence case |
| `work.composer.label` | field | message → **Message** | 消息 | Sentence case |
| `work.composer.send` | button | send → **Send** | 发送 | Sentence case |
| `work.composer.queue` | button | queue → **Queue** | 排队 | Sentence case |
| `work.composer.finish` | button | finish → **Finish** | 结束 → **完成** | Sentence case; finish is 完成, as the waiting card's press |
| `work.composer.stop` | button | stop → **Stop** | 停止 | Sentence case |
| `work.composer.stopTurn` | button | stop turn → **Stop turn** | 停止本轮 | Sentence case |
| `work.composer.attach` | button | attach files → **Attach files** | 附加文件 | Sentence case |
| `work.composer.queued` | status | waiting for this turn to end | 等待这一轮结束 |  |
| `work.composer.opening` | status | waiting for the conversation to open | 等待对话打开 |  |
| `work.steer.now` | button | send now → **Send now** | 立即发送 | Sentence case |
| `work.conversation.earlier` | button | load earlier → **Load earlier** | 加载更早的内容 | Sentence case |
| `work.conversation.bottom` | button | back to bottom → **Back to bottom** | 回到底部 | Sentence case |
| `work.conversation.show` | button | show all → **Show all** | 展开 | Sentence case |
| `work.conversation.hide` | button | fold → **Fold** | 收起 | Sentence case |
| `work.conversation.you` | status | you | 你 |  |
| `work.conversation.target` | field | the target Daoris composed → **The target Daoris composed** | Daoris 撰写的目标 | Sentence case |
| `work.conversation.thought` | status | thought | 思考 |  |
| `work.conversation.worked` | status | what it did | 过程 |  |
| `work.conversation.working` | status | working… | 工作中… |  |
| `work.conversation.driver` | field | driver → **Driver** | 驱动 | Sentence case |
| `work.conversation.plan` | field | plan → **Plan** | 计划 | Sentence case |
| `work.code.copy` | button | copy → **Copy** | 复制 | Sentence case |
| `work.code.copied` | status | copied | 已复制 |  |
| `work.code.plain` | status | text | 文本 |  |
| `work.tool.status.pending` | status | waiting | 等待 |  |
| `work.tool.status.in_progress` | status | running | 运行中 |  |
| `work.tool.status.completed` | status | done | 完成 |  |
| `work.tool.status.failed` | status | failed | 失败 |  |
| `work.tool.status.refused` | status | not allowed here | 此处不允许 |  |
| `work.tool.status.stopped` | status | stopped | 已停止 |  |
| `work.tool.status.cancelled` | status | cancelled | 已取消 |  |
| `work.tool.status.disconnected` | status | disconnected | 已断开 |  |
| `work.tool.status.session-ended` | status | ended with its session | 随会话结束 |  |
| `work.tool.input` | field | input → **Input** | 输入 | Sentence case |
| `work.tool.output` | field | output → **Output** | 输出 | Sentence case |
| `work.review.layout` | field | layout → **Layout** | 布局 | Sentence case |
| `work.review.unified` | choice | unified → **Unified** | 合并 → **单栏** | Sentence case; 合并 is merge's word |
| `work.review.split` | choice | side by side → **Side by side** | 并排 | Sentence case |
| `work.review.viewed` | field | viewed → **Viewed** | 已看 | Sentence case |
| `work.review.binary` | status | binary | 二进制 |  |
| `work.review.none.headline` | headline | Nothing attended | 尚未选中会话 |  |
| `work.review.empty.headline` | headline | Nothing landed | 没有落地的改动 |  |
| `work.review.status.added` | status | added | 新增 |  |
| `work.review.status.modified` | status | modified | 修改 |  |
| `work.review.status.deleted` | status | deleted | 删除 |  |
| `work.review.status.renamed` | status | renamed | 重命名 |  |
| `work.review.status.copied` | status | copied | 复制 |  |
| `work.review.accept` | button | accept → **Accept** | 采纳 | Sentence case |
| `work.review.accepting` | button | merging… → **Landing…** | 合并中… → **落地中…** | Accept lands the work; *merging* is one of its two forms, and says the wrong thing under a branch rule |
| `work.review.sendBack` | button | send it back… → **Send back…** | 退回… | Sentence case, without the pronoun |
| `work.review.discard` | button | discard the tree → **Discard tree** | 丢弃工作树 | A button is the verb and its object, with no article |
| `work.review.discardMeanIt` | button | discard it anyway → **Discard anyway** | 仍然丢弃 | A button stands alone, never *it* |
| `work.review.hand` | button | hand it to {{plugin}} → **Hand to {{plugin}}** | 交给 {{plugin}} | A button names its object or stands alone, never *it* |
| `work.review.handing` | button | handing it on… → **Handing off…** | 正在交接… | Hand off is the name |
| `work.review.pullRequest` | button | open the pull request → **Open the pull request** | 打开拉取请求 | Sentence case |
| `work.preview.tab` | tab | Preview: {{name}} | 预览：{{name}} |  |
| `work.preview.close` | button | close the preview → **Close the preview** | 关闭预览 | Sentence case |
| `work.preview.reload` | button | read it again → **Read again** | 重新读取 | Sentence case, without the pronoun |
| `work.preview.show` | field | show → **Show** | 显示 | Sentence case |
| `work.preview.file` | choice | File | 文件 |  |
| `work.preview.changes` | choice | Changes | 改动 |  |
| `work.preview.reading` | status | reading… | 读取中… |  |
| `work.terminal.open` | button | Open a terminal | 打开终端 |  |
| `work.terminal.again` | button | Start again | 重新启动 |  |
| `work.terminal.new` | button | New terminal | 新建终端 |  |
| `work.terminal.close` | button | Close {{name}} | 关闭{{name}} |  |
| `work.terminal.default` | status | default | 默认 |  |
| `work.terminal.endedMark` | status | ended | 已结束 |  |
| `work.panel.stream.subagent` | status | subagent | 子代理 → **子智能体** | A subagent is an agent: 代理 is a proxy, and the agent's name is 智能体 |
| `work.panel.stream.task` | status | background | 后台 |  |
| `work.panel.stream.live` | status | running | 运行中 |  |
| `work.panel.stream.ended` | status | ended | 已结束 |  |
| `work.panel.stream.state.completed` | status | completed | 已完成 |  |
| `work.panel.stream.state.failed` | status | failed | 失败 |  |
| `work.panel.stream.state.stopped` | status | stopped | 已停止 |  |
| `work.panel.stream.state.cancelled` | status | cancelled | 已取消 |  |
| `work.panel.stream.state.session-ended` | status | ended with its session | 随会话结束 |  |
| `console.label` | field | console → **Console** | 控制台 | Sentence case |
| `console.live` | status | live | 实时 |  |
| `chain.title` | section | How this work ran | 这件工作如何流转 → **工作流转** | A Chinese title names what the section holds |
| `chain.show` | button | show how it ran → **Show how it ran** | 展开流转详情 | Sentence case |
| `chain.hide` | button | hide how it ran → **Hide how it ran** | 收起流转详情 | Sentence case |
| `chain.current` | status | this quest | 当前委托 |  |
| `chain.thisSession` | status | this session | 本会话 |  |

### Quests, and the asks above them

| Key | Kind | English | 中文 | Why |
|---|---|---|---|---|
| `quests.title` | title | Quests | 委托 |  |
| `quests.new` | button | new quest → **New quest** | 新建委托 | Sentence case |
| `quests.addressedTo` | field | addressed to → **Receiver** | 受托方 | Receiver is the name, as the ask's composer says; sentence case |
| `quests.everyone` | choice | everyone → **Everyone** | 所有人 | Sentence case |
| `quests.includeClosed` | field | include closed → **Include closed** | 含已关闭 → **包含已关闭** | Sentence case; 含 alone is terse |
| `quests.groups.open` | section | Open — waiting to be taken ({{count}}) | 待接——等待有人接下（{{count}}） |  |
| `quests.groups.progress` | section | In progress ({{count}}) → **Taken ({{count}})** | 进行中（{{count}}） → **已接下（{{count}}）** | One word per state: the pill says *Taken* |
| `quests.groups.closed` | section | Closed ({{count}}) | 已关闭（{{count}}） |  |
| `quests.card.sat` | status | sat {{days}}d | 搁置 {{days}} 天 |  |
| `quests.card.waits` | status | waits on #{{id}} | 等待 #{{id}} |  |
| `quests.card.resume` | button | resume {{repository}} → **Resume {{repository}}** | 恢复 {{repository}} | Sentence case |
| `quests.empty.headlineAll` | headline | No open quests anywhere | 各处均无待接委托 |  |
| `quests.empty.headlineFor` | headline | Nothing asked of {{repository}} | 没有向 {{repository}} 发起的委托 |  |
| `quests.detail.from` | field | from → **From** | 来自 | Sentence case |
| `quests.detail.to` | field | to → **To** | 致 | Sentence case |
| `quests.detail.filed` | field | filed → **Filed** | 发起 | Sentence case |
| `quests.detail.moved` | field | moved → **Moved** | 变更 | Sentence case |
| `quests.detail.state` | field | state → **State** | 状态 | Sentence case |
| `quests.detail.sitting` | field | sitting → **Sitting** | 搁置 | Sentence case |
| `quests.detail.waitsOn` | field | waits on → **Waits on** | 等待 | Sentence case |
| `quests.detail.asked` | field | asked → **Asked** | 已询问 | Sentence case |
| `quests.detail.take` | button | take → **Take** | 接下 | Sentence case |
| `quests.detail.done` | button | done → **Mark done** | 完成 → **标为完成** | A button is a verb; *Done* reads as closing the drawer |
| `quests.detail.decline` | button | decline… → **Decline…** | 谢绝… | Sentence case |
| `quests.detail.declineConfirm` | button | decline with this reason → **Decline with this reason** | 以此理由谢绝 | Sentence case |
| `quests.detail.retry` | button | try it again → **Try again** | 再试一次 → **重试** | A button stands alone, never *it* |
| `quests.detail.delete` | button | delete… → **Delete…** | 删除… | Sentence case |
| `quests.detail.deleteMeanIt` | button | delete it → **Delete quest** | 确认删除 → **确认删除委托** | The confirming press names what it deletes, in both languages |
| `quests.detail.links` | section | Links | 链接 |  |
| `quests.detail.files` | section | Files | 文件 |  |
| `quests.detail.conflicts` | section | Conflicts | 冲突 |  |
| `quests.detail.dismiss` | button | Dismiss | 清除 → **忽略** | One act, one word: the toast's *Dismiss* is 忽略 |
| `quests.compose.title` | title | New quest | 新建委托 |  |
| `quests.compose.nobody.headline` | headline | Nobody can be asked yet | 还没有可以委托的对象 |  |
| `quests.compose.from` | field | from → **From** | 来自 | Sentence case |
| `quests.compose.fromPlaceholder` | placeholder | the repository asking… | 委托方仓库… |  |
| `quests.compose.to` | field | to → **To** | 致 | Sentence case |
| `quests.compose.toPlaceholder` | placeholder | the repository asked… | 受托方仓库… |  |
| `quests.compose.titleLabel` | field | what is wanted, in one line → **What is wanted, in one line** | 所求何事，一行说清 | Sentence case |
| `quests.compose.bodyLabel` | field | why, and the evidence → **Why, and the evidence** | 为什么，以及证据 | Sentence case |
| `quests.compose.publish` | button | publish quest → **Publish quest** | 发布委托 | Sentence case |
| `quests.compose.addStep` | button | add a next step… → **Add a next step…** | 添加下一步… | Sentence case |
| `quests.compose.stepLegend` | section | then → **Then** | 随后 | Sentence case |
| `quests.compose.stepTo` | field | then ask → **Then ask** | 随后委托 | Sentence case |
| `quests.compose.stepTitle` | field | what is wanted next, in one line → **What is wanted next, in one line** | 下一步要什么，一句话 | Sentence case |
| `quests.compose.stepBody` | field | why, and how to tell it is done → **Why, and how to tell it is done** | 为什么，以及如何判断已完成 | Sentence case |
| `quests.compose.removeStep` | button | no next step → **Remove next step** | 不要下一步 → **移除下一步** | A button is a verb |
| `quests.session.title` | section | session → **Session** | 会话 | Sentence case |
| `quests.session.stop` | button | stop session → **Stop session** | 停止会话 | Sentence case |
| `trust.open` | button | trust this folder… → **Trust this folder…** | 信任这个文件夹… | Sentence case |
| `trust.grant` | button | trust this folder → **Trust this folder** | 信任这个文件夹 | Sentence case |
| `trust.cancel` | button | not now → **Not now** | 暂不 | Sentence case |
| `asks.group` | section | Asks ({{count}}) | 请求（{{count}}） → **需求（{{count}}）** | An ask is 需求: 请求 is every request's word (a pull request's, an agent's for more), and an ask is what a person wants made; the owner's call |
| `asks.ask` | button | ask → **Ask** | 发起请求 → **提需求** | Sentence case; an ask is 需求 |
| `asks.state.Open` | status | asked | 已提出 |  |
| `asks.state.Proposed` | status | proposed | 已提议 |  |
| `asks.state.Published` | status | published | 已发布 |  |
| `asks.state.Done` | status | done | 已完成 |  |
| `asks.state.Closed` | status | closed | 已关闭 |  |
| `asks.compose.title` | title | Ask the workspace | 向工作区提出请求 → **向工作区提需求** | An ask is 需求 |
| `asks.compose.nowhere.headline` | headline | There is no workspace to ask yet | 还没有可以提出请求的工作区 → **还没有可以提需求的工作区** | An ask is 需求 |
| `asks.compose.circle` | field | workspace → **Workspace** | 工作区 | Sentence case |
| `asks.compose.circlePlaceholder` | placeholder | which workspace to ask | 选择要提出请求的工作区 → **选择工作区** | The field names the act; an ask is 需求 |
| `asks.compose.sentence` | field | what is wanted, and why → **What is wanted, and why** | 需要什么，以及为什么 | Sentence case |
| `asks.compose.to` | field | receiver → **Receiver** | 接收方 → **受托方** | Sentence case; one word for a quest's receiver, 受托方 beside the asker's 委托方 |
| `asks.compose.toNobody` | choice | nobody yet — let the declarations propose → **Nobody yet — let the declarations propose** | 暂不指定——由声明来提议 | Sentence case |
| `asks.compose.submit` | button | ask → **Ask** | 提出 | Sentence case |
| `asks.record.circle` | field | workspace → **Workspace** | 工作区 | Sentence case |
| `asks.record.asked` | field | asked → **Asked** | 提出于 | Sentence case |
| `asks.record.asker` | field | asked by → **Asked by** | 提出者 | Sentence case |
| `asks.record.tier` | field | answered → **Answered** | 应答方式 | Sentence case |
| `asks.record.notPublished` | field | not published → **Not published** | 未发布 | Sentence case |
| `asks.record.closedBecause` | field | closed because → **Closed because** | 关闭原因 | Sentence case |
| `asks.record.quests` | section | Became | 已成为的委托 |  |
| `asks.record.proposal` | section | Where it belongs | 归属 |  |
| `asks.record.publishTo` | button | publish to {{repository}} → **Publish to {{repository}}** | 发布给 {{repository}} | Sentence case |
| `asks.record.publish` | button | publish → **Publish** | 发布 | Sentence case |
| `asks.record.anotherPlaceholder` | placeholder | any repository in workspace {{circle}} that can be asked | 工作区 {{circle}} 中任一可被委托的仓库 |  |
| `asks.record.links` | section | Links | 链接 |  |
| `asks.record.files` | section | Files | 文件 |  |
| `asks.record.close` | button | close the ask → **Close ask** | 关闭请求 → **关闭需求** | A button is the verb and its object, with no article; an ask is 需求 |
| `asks.record.closeWhy` | placeholder | why — what became of it | 原因——它最终怎样了 |  |
| `asks.record.closeConfirm` | button | close with this reason → **Close with this reason** | 以此原因关闭 | Sentence case |
| `asks.record.delete` | button | delete… → **Delete…** | 删除… | Sentence case |
| `asks.record.deleteMeanIt` | button | delete it → **Delete ask** | 确认删除 → **确认删除需求** | The confirming press names what it deletes, in both languages |
| `asks.record.intake` | section | intake session → **Intake session** | 受理会话 | Sentence case |
| `map.asks` | section | Asks | 请求 → **需求** | An ask is 需求 |

### Repositories (today *Projects*)

| Key | Kind | English | 中文 | Why |
|---|---|---|---|---|
| `projects.title` | title | Projects → **Repositories** | 项目 → **仓库** | As nav.projects: the owner's call |
| `projects.empty.headline` | headline | No repository is registered yet | 还没有登记任何仓库 → **还没有注册任何仓库** | Register is 注册 (注册 and 登记 were 15 each) |
| `projects.empty.headlineIn` | headline | No repository in {{workspace}} yet | {{workspace}} 里还没有仓库 |  |
| `projects.owns` | field | owns → **Owns** | 拥有 | Sentence case |
| `projects.accepts` | field | accepts → **Accepts** | 接受 | Sentence case |
| `projects.packs` | field | packs → **Packs** | 包 → **规范包** | A pack is 规范包; 包 alone is any package |
| `projects.fed` | field | fed from → **Fed from** | 来源提交 | Sentence case |
| `projects.line` | field | line → **Line** | 主线 | Sentence case |
| `projects.unlandedLabel` | field | unlanded → **Unlanded** | 未落地 | Sentence case |
| `projects.workspace` | field | workspace → **Workspace** | 工作区 | Sentence case |
| `projects.outside.title` | section | Registered, not adopted | 已登记，尚未加入 → **已注册，尚未采用** | Register is 注册; adopt is 采用 |
| `projects.manage.add` | button | add repository → **Add repository** | 添加仓库 | Sentence case |
| `projects.manage.addTitle` | title | Add a repository to this machine | 把仓库添加到本机 |  |
| `projects.manage.choose` | button | choose a folder… → **Choose a folder…** | 选择文件夹…… → **选择文件夹…** | Sentence case; one ellipsis, the UI's mark |
| `projects.manage.register` | button | register it → **Register** | 注册它 → **注册** | A button stands alone, never *it* |
| `projects.manage.adopted` | status | adopted | 已采用 |  |
| `projects.adoptedDot` | status | adopted | 已加入 → **已采用** | Adopt is 采用, as the manage drawer's chip says |
| `projects.manage.notAdopted` | status | not adopted | 尚未采用 → **未采用** | A status word; parallel with 已采用 |
| `projects.manage.git` | status | git checkout | git 检出 |  |
| `projects.manage.noGit` | status | no git history | 没有 git 历史 |  |
| `projects.manage.workspacePlaceholder` | placeholder | default | default |  |
| `projects.manage.importTitle` | title | Import a folder as a workspace | 把文件夹导入为工作区 |  |
| `projects.manage.importWorkspacePlaceholder` | placeholder | each keeps its own | 各自保持原样 |  |
| `projects.manage.importRun` | button | import them → **Import** | 导入它们 → **导入** | A button stands alone, never *them* |
| `projects.manage.open` | button | manage → **Manage** | 管理 | Sentence case |
| `projects.manage.title` | title | Manage {{name}} | 管理 {{name}} |  |
| `projects.manage.wiring` | section | Workspace | 工作区 |  |
| `projects.manage.rewire` | button | re-wire → **Move to workspace** | 重新接线 → **移到工作区** | Wire is the remote's word; this press moves the repository to another workspace |
| `projects.manage.declaration` | section | Declaration | 声明 |  |
| `projects.manage.summary` | field | summary → **Summary** | 一句话说明 | Sentence case |
| `projects.manage.writeDeclaration` | button | write daoris.json → **Write daoris.json** | 写入 daoris.json | Sentence case |
| `projects.manage.retire` | button | retire → **Retire** | 注销 | Sentence case; the section above it shares the key and the word |
| `projects.manage.retireConfirm` | button | yes, retire it → **Retire repository** | 确认注销 → **确认注销仓库** | The confirming press names what it retires, in both languages |
| `projects.driver.label` | field | driver → **Driver** | 驱动 | Sentence case |
| `projects.driver.drive` | field | drive on this machine → **Drive on this machine** | 在本机驱动 | Sentence case |
| `projects.driver.hold` | field | hold → **Hold** | 暂停 | Sentence case |
| `projects.driver.trees` | field | own tree per session → **A tree per session** | 每个会话独立工作树 | Sentence case; tree is the name |

### Convergence, Search, Map

| Key | Kind | English | 中文 | Why |
|---|---|---|---|---|
| `convergence.title` | title | Convergence | 同归 |  |
| `convergence.threshold` | field | Similarity ≥ | 相似度 ≥ |  |
| `convergence.lower` | button | lower it to {{value}} → **Lower to {{value}}** | 调低到 {{value}} | Sentence case, without the pronoun |
| `convergence.empty` | headline | Nothing converges at {{value}} or above | 相似度 {{value}} 及以上未见同归 |  |
| `convergence.methods.Convergent.label` | section | Same lesson, different words | 同一教训，不同措辞 |  |
| `convergence.methods.Restatement.label` | section | Substantially the same words | 措辞大体相同 |  |
| `convergence.methods.Identical.label` | section | The same document, pasted | 同一份文档，原样粘贴 |  |
| `search.title` | title | Search | 搜索 |  |
| `search.placeholder` | placeholder | a decision, a trap, a rule — in your own words | 一项决策、一个陷阱、一条规则——用你自己的话 |  |
| `search.clear` | button | clear the query → **Clear search** | 清除查询 → **清除搜索** | The object is the search, as the view names it |
| `search.localOnly` | field | each repository's own only → **Each repository's own only** | 仅各仓库自有 | Sentence case |
| `search.emptyHeadline` | headline | No matches | 没有匹配 |  |
| `search.noneHeadline` | headline | Nothing answered | 没有搜索作答 |  |
| `search.toConvergence` | button | look for convergence → **Look in Convergence** | 去看同归 → **在同归中查看** | A door names its destination, as the map's *See them in Convergence* |
| `map.title` | title | Map | 地图 |  |
| `map.find` | placeholder | find a repository | 查找仓库 |  |
| `map.zoomIn` | menu | Zoom in | 放大 |  |
| `map.zoomOut` | menu | Zoom out | 缩小 |  |
| `map.fit` | menu | Show the whole map | 显示整张地图 |  |
| `map.lines` | menu | Lines → **Connections** | 线条 → **连线** | *Lines* is also Settings' lines (主线): two things, one English word; 连线 is the map's own word already |
| `map.kind.quests` | menu | Quests between repositories | 仓库之间的委托 |  |
| `map.kind.asks` | menu | What your asks became | 你的请求变成的委托 → **你的需求变成的委托** | An ask is 需求 |
| `map.kind.chains` | menu | Chains, step by step | 委托链的每一步 |  |
| `map.kind.depends` | menu | What each says it uses | 各自声明依赖的仓库 |  |
| `map.kind.knowledge` | menu | The same thing learned twice | 两边学到的同一件事 |  |
| `map.when.title` | section | Which quests | 哪些委托 |  |
| `map.when.all` | menu | All of them | 全部 |  |
| `map.when.open` | menu | Open only → **Not closed** | 仅未完成的 → **仅未关闭的** | *Open* is a quest's state before it is taken; this is open or taken |
| `map.when.week` | menu | Moved in the last 7 days | 近 7 天有变动的 |  |
| `map.when.month` | menu | Moved in the last 30 days | 近 30 天有变动的 |  |
| `map.when.short.open` | status | open → **not closed** | 未完成 → **未关闭** | As *Not closed* |
| `map.when.short.week` | status | 7 days | 7 天 |  |
| `map.when.short.month` | status | 30 days | 30 天 |  |
| `map.empty.headline` | headline | No repositories in this workspace | 这个工作区里还没有仓库 |  |
| `map.empty.headlineAll` | headline | No repository in any workspace yet | 还没有哪个工作区里有仓库 |  |
| `map.working` | status | working now → **working** | 正在工作 → **工作中** | One word per state, as a session's pill |
| `map.parked` | status | waiting on you | 在等你 → **等你处理** | One word for waiting on the person |
| `map.detail.openConvergence` | button | See them in Convergence | 在同归中查看 |  |
| `map.detail.openCode` | button | Open its code map → **Open code map** | 打开它的代码地图 → **打开代码地图** | A button names its object, never *its* |
| `code.title` | title | {{repository}}: its code → **{{repository}}: code map** | {{repository}}：它的代码 → **{{repository}}：代码地图** | The view shows its code map, and names it so |
| `code.back` | button | Back to the workspace | 回到工作区 |  |
| `code.none.headline` | headline | {{repository}} keeps no code map | {{repository}} 没有代码地图 |  |
| `code.detail.dependsOn` | field | depends on → **Depends on** | 依赖 | Sentence case |
| `code.detail.usedBy` | field | used by → **Used by** | 被依赖 | Sentence case |

### Ask Daoris

| Key | Kind | English | 中文 | Why |
|---|---|---|---|---|
| `help.close` | button | close Ask Daoris → **Close Ask Daoris** | 关闭问道衍 | Sentence case |
| `help.new` | button | new conversation → **New conversation** | 新对话 | Sentence case |
| `help.quick.title` | title | Quick Ask | 快速提问 |  |
| `help.quick.expand` | button | open in the side bar → **Open in the side bar** | 在侧栏中打开 | Sentence case |
| `help.quick.close` | button | close Quick Ask → **Close Quick Ask** | 关闭快速提问 | Sentence case |
| `help.proposal.title` | section | Ask Daoris proposes | 问道衍提议 |  |
| `help.proposal.titleDelete` | section | Ask Daoris proposes a delete | 问道衍提议删除 |  |
| `help.proposal.titleGo` | section | Ask Daoris suggests a screen | 问道衍建议打开一个界面 |  |
| `help.proposal.titlePlugin` | section | Ask Daoris proposes a plugin | 问道衍提议一个插件 |  |
| `help.proposal.titleHand` | section | Ask Daoris proposes handing a branch on | 问道衍提议交接一个分支 |  |
| `help.proposal.titleSync` | section | Ask Daoris proposes bringing repositories up to date | 问道衍提议同步到最新 → **问道衍提议把仓库更新到最新** | Bring up to date is 更新到最新, where 同步 is sync's word; and the Chinese names its object, the repositories |
| `help.proposal.apply` | button | apply → **Apply** | 应用 | Sentence case |
| `help.proposal.dismiss` | button | not now → **Not now** | 暂不 | Sentence case |
| `help.proposal.delete` | button | delete → **Delete** | 删除 | Sentence case |
| `help.proposal.go` | button | go there → **Go there** | 前往 | Sentence case |
| `help.proposal.look` | button | look for updates → **Look for updates** | 查看更新 → **检查更新** | The same act as Updates' first press, in the same words |
| `help.door.sessions` | button | open Sessions → **Open Sessions** | 打开会话 | Sentence case |
| `help.door.projects` | button | open Projects → **Open Repositories** | 打开项目 → **打开仓库** | A door names its destination (nav.projects) |
| `help.door.agents` | button | open Agents & accounts → **Open Agents** | 打开智能体与账户 → **打开智能体** | A door names its destination: the domain is *Agents* |
| `help.door.workspace` | button | open Workspace settings → **Open Workspace** | 打开工作区设置 → **打开工作区** | A door names its destination: the domain is *Workspace* |
| `help.door.ai` | button | open Daoris's own AI → **Open AI features** | 打开道衍自身的 AI → **打开 AI 功能** | A door names its destination; and 道衍 in running text was the wordmark's |
| `help.setup` | button | set up Daoris step by step: {{done}} of {{of}} done → **Set up Daoris step by step: {{done}} of {{of}} done** | 逐步配置 Daoris：{{of}} 步已完成 {{done}} 步 | Sentence case |

### The words every screen shares

| Key | Kind | English | 中文 | Why |
|---|---|---|---|---|
| `status.Open` | status | Open → **open** | 待接 | A status word is lower case, as every other pill's |
| `status.Taken` | status | Taken → **taken** | 进行中 → **已接下** | One word per state: *taken* is 接下 where the drawer's press says it, and 进行中 was a third name |
| `status.Done` | status | Done → **done** | 已完成 | A status word is lower case |
| `status.Declined` | status | Declined → **declined** | 已谢绝 | A status word is lower case |
| `sessionState.queued` | status | queued | 已排队 → **排队中** | A state in progress is the verb and 中 |
| `sessionState.starting` | status | starting | 启动中 |  |
| `sessionState.working` | status | working | 工作中 |  |
| `sessionState.awaiting-person` | status | awaiting person → **waiting on you** | 等待人工 → **等你处理** | One word for waiting on the person, everywhere it is shown (U1); 「人工」 is manual labour |
| `sessionState.completed` | status | completed | 已完成 |  |
| `sessionState.declined` | status | declined | 已谢绝 |  |
| `sessionState.stood-down` | status | stood down | 已让位 |  |
| `sessionState.failed` | status | failed | 失败 |  |
| `sessionState.stopped` | status | stopped | 已停止 |  |
| `sessionState.idle` | status | idle | 空闲 |  |
| `kind.Rule` | status | Rule → **rule** | 规则 | A status word is lower case |
| `kind.Knowledge` | status | Knowledge → **knowledge** | 知识 | A status word is lower case |
| `kind.Skill` | status | Skill → **skill** | 技能 | A status word is lower case |
| `kind.Decision` | status | Decision → **decision** | 决策 | A status word is lower case |
| `kind.Fix` | status | Fix → **fix** | 修复 | A status word is lower case |
| `kind.TaskOutcome` | status | Task outcome → **task outcome** | 任务结果 | A status word is lower case; 任务 is right here, a task and not a quest |
| `provenance.Local` | status | Local → **local** | 本地 | A status word is lower case |
| `provenance.Canonical` | status | Canonical → **canonical** | 典章 | A status word is lower case |
| `carry.linksLabel` | field | links — a ticket, a page, a document; one per line → **Links — a ticket, a page, a document; one per line** | 链接——工单、页面、文档；每行一个 | Sentence case; over the field budget, kept: the qualifier after the dash is the field's instruction |
| `carry.choose` | button | choose files… → **Choose files…** | 选择文件… | Sentence case |
| `carry.remove` | button | remove {{name}} → **Remove {{name}}** | 移除 {{name}} | Sentence case |

## 4. What NAME1b does beyond the strings

- **Settings' *Machine log* card loses its title**, since the domain's list names it (U57), once the domain is
  named *Machine log*.
- **Updates keeps its sub-head** inside *Session branches* (`Sync.tsx`); only the words change. Its first press
  is the one the heading names.
- **The sync menu's *Wiring…* opens at Wiring** (`App.tsx` hands it `openSettings('workspace')`, the domain's
  top), as the Workspace menu's *Wire to a remote…* already does: a menu item named for a part opens at it (U72).
- **Two proposals are the owner's call** and everything that follows from them moves only if the owner takes
  them: the view *Projects* → *Repositories* (仓库), and an ask 请求 → 需求. Their rows say so.
- **The design language moves with the strings.** `docs/2026-09-19-platform-ux.md` §5 names today's words
  (*Daoris's own AI*, *Agents & accounts*, *Get started*, *In progress*, *Projects*), and its *one word per
  thing* bullet in §4 lists four of the glossary's terms; NAME1b updates those lines and points the bullet at
  the glossary.
- **The tests and stories that pin a value** move with it. `i18n.test.ts`'s word tests (工作区, 账户, 委托) say
  what the glossary says; NAME1b keeps them or lets the check hold them, and adds none.
- **The check turns to `--strict`** in the web's build beside the parity check, for the facts' half only.

## 5. What the glossary finds in sentences

`names:check --all` holds every sentence, tooltip and toast to the words a term must not be called (never to
the term's name, since a sentence may say a thing its own way). It finds 130, by word:

| Word | Keys | Note |
|---|---|---|
| 中文: 请求 | `asks.compose.filesLabel`, `asks.compose.nowhere.body`, `asks.record.deleteConfirm`, `asks.record.deleteTitle`, `map.asksLabel`, `map.detail.asks`, `map.legend.asks`, `settings.ai.intake.cleared`, `settings.ai.intake.named`, `settings.ai.intake.tierOff`, `settings.ai.intake.tierOn`, `settings.ai.intake.why`, `settings.browser.links.why`, `settings.rules.proposals.byIntake`, `trust.holding.ask`, `work.attention.askWhere`, `work.intake.hint`, `work.intake.stopMeans`, `work.intakeRunning.hint` |  |
| 中文: 这台机器 | `errors.PREVIEW_NO_TREE`, `errors.SESSION_NOT_REVIEWABLE`, `errors.SESSION_TREE_GONE`, `errors.SESSION_TREE_UNLISTED`, `harness.profile.note`, `quests.session.orphanEnded`, `settings.across.noCheckout`, `settings.across.none`, `settings.notify.body`, `settings.notify.off`, `settings.notify.on`, `settings.strikes.never`, `usage.note`, `work.review.landed.noCheckout` |  |
| 中文: 登记 | `asks.compose.nowhere.body`, `map.outside`, `overview.repositories.emptyBody`, `overview.repositories.hint`, `projects.empty.body`, `projects.manage.importBody`, `projects.outside.body`, `projects.outside.lead`, `quests.compose.nobody.body`, `settings.workspaces.none`, `work.attention.unanswerableWhy`, `work.rail.treeTip` |  |
| English: tick | `settings.ai.intake.hint`, `settings.rules.proposals.hint`, `settings.rules.proposals.unjudged`, `settings.strikes.body`, `settings.strikes.zero`, `settings.sync.apart.body`, `settings.sync.noneHeld`, `work.awaiting.answered`, `work.intake.hint` |  |
| 中文: 接收方 | `asks.compose.hint`, `asks.compose.hintIntake`, `asks.compose.hintUnknown`, `asks.record.noProposal`, `asks.tier.named`, `asks.tierShort.named`, `work.attention.proposalNobody` |  |
| 中文: 这台电脑 | `settings.description`, `setup.ask.message`, `setup.desktop`, `setup.intro`, `setup.reading`, `setup.step.driven.when`, `work.status.setupTip` |  |
| English: projects | `asks.compose.nowhere.body`, `overview.repositories.more`, `quests.card.resumeTip`, `quests.compose.nobody.body`, `settings.workspaces.none`, `work.start.none` |  |
| 中文: 加入 | `overview.repositories.adoptedDot`, `overview.repositories.hint`, `projects.description`, `projects.outside.body`, `projects.outside.lead`, `projects.undeclared` |  |
| 中文: 项目 | `asks.compose.nowhere.body`, `overview.repositories.hint`, `quests.compose.nobody.body`, `settings.workspaces.none`, `work.start.none` |  |
| 中文: 拒绝 | `chain.pendingHint`, `quests.compose.stepHint`, `settings.rules.proposals.declined`, `settings.strikes.terminal`, `work.relations.declined` |  |
| English: profile | `settings.browser.which.hintDaoris`, `settings.browser.which.hintEdge`, `settings.home.hint`, `settings.home.hintOverridden`, `settings.home.hintThisStart` | not drift: a browser's or Windows' profile, which the sentence rightly names |
| 中文: 接受 | `asks.compose.hint`, `errors.INVALID_PAYLOAD_VALUE`, `settings.rules.proposals.accepted`, `work.review.landed.treeStays` | `errors.INVALID_PAYLOAD_VALUE` is a request's value, not a move, and is not drift; the rest are |
| 中文: 代理 | `settings.across.body`, `settings.across.readByNone`, `settings.across.savedOff`, `settings.across.savedOn` |  |
| 中文: 工具 | `harness.failed`, `plugin.body`, `usage.contextTip` |  |
| English: login | `settings.ai.intake.tierOn`, `settings.ai.intake.why`, `settings.strikes.zero` |  |
| 中文: 暂停 | `settings.strikes.never`, `settings.strikes.set`, `settings.strikes.terminal` |  |
| English: logged in | `harness.login.hint`, `signin.ready` |  |
| English: project | `overview.outstanding.emptyBody`, `quests.empty.body` |  |
| 中文: 远端 | `projects.manage.workspaceNote`, `projects.workspaceTip` |  |
| 中文: 教义 | `projects.manage.doctrine`, `projects.manage.retireNotDelete` |  |
| 中文: Ask Daoris | `settings.across.body`, `settings.across.readBy` |  |
| 中文: 开始使用 | `setup.desktop`, `work.status.setupTip` |  |
| English: get started | `setup.desktop`, `work.status.setupTip` |  |
| English: rail | `work.attended.none.body`, `work.rail.resize` |  |
| English: harness | `asks.tier.declarations` |  |
| 中文: 接入 | `errors.REPOSITORY_NOT_ADOPTED` |  |
| 中文: 结束 | `errors.HARNESS_ACTION_IDLE` | not drift: a process that ended, not a session finished |
| English: tidy | `errors.SESSION_TREE_GONE` |  |
| English: log in | `harness.login.hint` |  |
| English: logging in | `harness.secrets` |  |
| 中文: 自身的 AI | `help.intro` |  |
| English: Daoris's own AI | `help.intro` |  |
| 中文: 目录树 | `quests.description` |  |
| 中文: 周期 | `settings.ai.intake.hint` |  |
| 中文: 同步 | `settings.sync.bringing` |  |
| English: helper | `settings.sync.notFetched.https` | not drift: git's credential helper |
| 中文: 查看 | `work.attention.open.trust` | a look at a folder's trust question, not the review; the English *review the folder* may say *look at the folder* instead |
| English: addressed to | `work.attention.emptyBody` |  |
| 中文: 会话栏 | `work.rail.resize` |  |

The two that are the owner's call (请求 for an ask, *project* for a repository) move only with their rows.

**What the check cannot see.** A sentence the driver writes passes through in English as `{{why}}` (the
defaults' reasons, why a quest sits), and 中文 says it in its own words, so there is no English for a term to
match. Read by hand: `settings.rules.defaultWhy.commit` and `.tree-guard` say 目录树 (a tree is 工作树),
`.no-push` says 这台电脑 (本机), and `work.sitting.NotDrivable`, `.Held` and `.NoRoot` say 接收方 (受托方).
Their 拒绝 is a refusal, which is right, and `.commit`'s 任务 is a task, not a quest.


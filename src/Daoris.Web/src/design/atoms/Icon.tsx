import { createContext } from 'react';
import { Archive, ArchiveRestore, ArrowDown, ArrowDownToLine, ArrowUpToLine, ArrowLeft, ArrowLeftRight, ArrowUp, Bell, BookOpen, Bot, Brain, Check, ChevronDown, ChevronRight, ChevronUp, CircleDot, CircleHelp, ClipboardPaste, Cloud, CloudOff, Compass, Copy, Ellipsis, Eye, FileDiff, FilePen, FileText, Flag, FolderOpen, Gauge, GitBranch, GitMerge, GitPullRequest, Globe, Hand, History, Inbox, Info, Pin, ScanEye, Keyboard, KeyRound, Languages, LayoutDashboard, LayoutGrid, Layers, Link, ListChecks, ListTodo, LogIn, Maximize2, Menu as MenuGlyph, Minimize2, Monitor, Network, PanelBottom, PanelLeft, PanelLeftClose, PanelLeftOpen, PanelRight, PanelRightClose, Paperclip, Pause, Play, Plug, Plus, Puzzle, Redo2, Reply, RotateCw, Route, Scissors, Search, Settings, Shield, Split, Square, SquareArrowOutUpRight, SquareTerminal, Terminal, TextSelect, Trash2, TriangleAlert, Undo2, UserCheck, Users, Workflow, Wrench, WrapText, X, ZoomIn, ZoomOut } from 'lucide-react';
import { cn } from '../../lib/cn';

/** The icon roster. Lucide, tree-shaken; always decorative beside a real label. */
const ICONS = {
  overview: LayoutGrid,
  quests: ArrowLeftRight,
  projects: Layers,
  convergence: GitMerge,
  // The workspace map: repositories as nodes, and what moved between them.
  map: Network,
  search: Search,
  // Knowledge: the one place for Search and Convergence, an open book, since each of its modes keeps
  // its own glyph inside it.
  knowledge: BookOpen,
  // A GEAR: Settings is the application's settings page now, and a gear is what every
  // application draws for one. The sliders it wore as "Machine" read as a mixer, not a place.
  settings: Settings,
  refresh: RotateCw,
  plus: Plus,
  x: X,
  check: Check,
  inbox: Inbox,
  languages: Languages,
  // The review's own three: a disclosure arrow both ways, and the dock's tab.
  chevronDown: ChevronDown,
  chevronRight: ChevronRight,
  diff: FileDiff,
  // The way through a long run: the previous match, and the jump to where it first failed.
  chevronUp: ChevronUp,
  failure: TriangleAlert,
  // Ask Daoris: its button on the strip, and its panel's heading.
  help: CircleHelp,
  // The second screen: the monitor window, and popping one session out into its own.
  monitor: Monitor,
  external: SquareArrowOutUpRight,
  // Daoris's own browser. Not the globe: that is a fetch, in the conversation's tool cards.
  browser: Compass,
  // 🔴 The two FRAMES, and they need glyphs of their own. Manage first wore `overview` — the same
  // grid as the Overview domain three rows below it — so the rail showed one icon twice meaning two
  // different things, which is worse than an unlabelled icon: it is a wrong label.
  frameManage: LayoutDashboard,
  frameWork: SquareTerminal,
  // The status bar's own two (2026-09-22): whether this circle has a deployment wired. A bar item
  // that is a word alone reads as a caption, and the glyph is what makes it scan as an item.
  cloud: Cloud,
  cloudOff: CloudOff,
  // Where a circle stands with its remote, the way every IDE's sync item says it: what is
  // waiting to go up, what could not come level, and what needs a person.
  ahead: ArrowUp,
  behind: ArrowDown,
  conflict: TriangleAlert,
  // The accounts surface (2026-09-22): an account, signing into one, and removing one. The bin is on
  // Remove because Remove now deletes; it was kept off Forget while forgetting removed nothing.
  account: KeyRound,
  login: LogIn,
  remove: Trash2,
  // A setting's WHY (2026-09-23): the paragraph that used to sit above every control, one hover away.
  info: Info,
  // Signing in (2026-09-23): the link the tool printed, copied into a browser of the person's own.
  copy: Copy,
  // What a quest carries: an address, and a file.
  link: Link,
  attach: Paperclip,
  // The menus by domain. A check beside a menu's check column read as "this one is ticked",
  // so what agents may do wears a shield, plugins a plug and usage a gauge.
  shield: Shield,
  plug: Plug,
  gauge: Gauge,
  // The conversation: a tool call wears its ACP kind — reading, editing, running a command
  // (the terminal the Work frame already wears), searching, the web, thinking, anything else — and
  // the plan, and the way back to the tail.
  read: FileText,
  edit: FilePen,
  execute: SquareTerminal,
  fetch: Globe,
  think: Brain,
  tool: Wrench,
  plan: ListTodo,
  toBottom: ArrowDownToLine,
  toTop: ArrowUpToLine,
  // The frame's own controls: the rail and the dock closed and opened, and the dock over the frame.
  railClose: PanelLeftClose,
  railOpen: PanelLeftOpen,
  dockClose: PanelRightClose,
  // The layout toggles on the strip — VS Code's pictures for the same three regions.
  layoutRail: PanelLeft,
  layoutPanel: PanelBottom,
  layoutRight: PanelRight,
  full: Maximize2,
  unfull: Minimize2,
  // A row's own menu.
  more: Ellipsis,
  // Stopping one piece of background work from its tab: the square every player and
  // terminal draws for stop, which is not the bin (removing) or the cross (closing).
  stop: Square,
  // The person's own shell: a prompt, where the console's framed square is a session's output.
  terminal: Terminal,
  // A folder of Daoris's own, opened in the file manager: the machine log's.
  folder: FolderOpen,
  // An ended session taken out of the list and brought back: the box, not the bin, since it is kept whole.
  archive: Archive,
  unarchive: ArchiveRestore,
  // A session waiting on the person, answered from its row's menu: the reply every mail client draws.
  answer: Reply,
  // An ask's or a quest's work paused and carried on: a player's pause and play, never the stop's square.
  pause: Pause,
  resume: Play,
  // The menu bar's Edit: a field's own acts, drawn as every editor's Edit menu draws them.
  undo: Undo2,
  redo: Redo2,
  cut: Scissors,
  paste: ClipboardPaste,
  selectAll: TextSelect,
  // Help › Keyboard shortcuts: the list of every key the window answers.
  keyboard: Keyboard,
  // The menu bar folded into one on a narrow window (UX7a2): VS Code's own glyph for the same menu.
  menu: MenuGlyph,
  // A workflow's steps by kind (WORKFLOW1b, the workflow design §6.2): what a step is, beside the rail's mark for who takes
  // part. The agent's work, another agent reading it, the person looking at it, the two forms of a landing, the pull request a
  // plugin opens, a plugin that may hold a start, and the end of the line.
  stepWork: Bot,
  stepOpinion: ScanEye,
  stepLook: Eye,
  stepMerge: GitMerge,
  stepBranch: GitBranch,
  stepPullRequest: GitPullRequest,
  stepHold: Hand,
  stepFinished: Flag,
  // A way on that leads nowhere, drawn and never saved.
  stepNoFurther: CircleDot,
  // The rest of the step kinds' glyphs: a decision with its outcomes, a plugin's step, the checks, the line, your review.
  stepDecision: Split,
  stepPlugin: Puzzle,
  stepCheck: ListChecks,
  stepLine: Route,
  stepReview: UserCheck,
  // Where a piece of work stands in its workflow: the session's side bar view and the doors into it.
  workflow: Workflow,
  // Who is at work on a session (COWORKVIEW1a, the cowork view design §5.1): the side bar view of its parts and subagents.
  atWork: Users,
  // Ask Daoris's history: its list, the way back to it from a conversation, a pinned conversation's mark, and a
  // code block's long lines wrapped at its pane.
  history: History,
  back: ArrowLeft,
  pin: Pin,
  wrap: WrapText,
  // Notifications (NOTIFY2b, the notification design §6): the title bar's button and the view's tab, the one new glyph.
  bell: Bell,
  // The window's zoom: View's rows and the status bar's level, larger or smaller than the page as drawn.
  zoomIn: ZoomIn,
  zoomOut: ZoomOut,
} as const;

export type IconName = keyof typeof ICONS;

/**
 * A glyph's size, given in the pixels it has at 100%, as rem (ZOOM1, the UI/UX design §3.11): the window zooms by the root's
 * size, so a glyph grows with the words beside it, and in the frame, which wears the level's inverse, it keeps its size.
 */
export const glyphSize = (pixels: number) => `${pixels / 16}rem`;

export function Icon({ name, size = 16, className }: { name: IconName; size?: number; className?: string }) {
  const Glyph = ICONS[name];
  return <Glyph size={glyphSize(size)} strokeWidth={1.7} aria-hidden className={cn('shrink-0', className)} />;
}

/**
 * The class that keeps the window's frame at its own size while the content zooms (ZOOM1, the UI/UX design §3.11): a CSS
 * zoom of the level's inverse (`--frame-zoom`, set by `design/zoom.ts`), declared in `tokens.css`.
 */
export const FRAME_HELD = 'frame-held';

/**
 * Whether what opens here opens from the frame: the title strip, the activity bar and the status bar provide it, so a
 * menu, a tip or a list they open keeps the frame's size too. Read through a portal, as React's context is.
 */
export const FrameHeld = createContext(false);

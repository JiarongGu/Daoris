/**
 * The shapes this tool has always had, written down.
 *
 * They were implicit while the source was JavaScript — described accurately in comments, agreed on by
 * every module, and checked by nothing. Naming them is most of what the TypeScript migration buys:
 * the lock, the manifest and the plan are the three things `sync` reasons about at once (D19), and
 * that state space is exactly where a wrong shape would be expensive.
 */
import type { ExitCode } from './errors.ts';

/** Where a document lives in the canon and where it lands in a repository. */
export interface CanonFile {
  /** `core`, or the pack's name. */
  pack: string;
  /** Path inside the canon, e.g. `core/rules/sensitive-info.md`. */
  source: string;
  /** Path inside the target directory, e.g. `rules/sensitive-info.md`. */
  target: string;
}

/** One pack: its name, what `init` prints about it, and everything it ships. */
export interface Pack {
  name: string;
  description: string;
  /** The canon contract this pack was written against (PLUG1). Absent in a manifest means 1. */
  api: number;
  files: CanonFile[];
  /**
   * The core rows this pack offers to switch off, each with the reason (D71). Keyed by a core
   * document's target, or a core skill's directory. Empty for core, and for almost every pack.
   */
  switchesOff: Record<string, string>;
}

/** A core row a selected pack offers to switch off, or has switched off (D71). */
export interface CoreSwitch {
  /** A core document's target (`rules/x.md`), or a core skill's directory (`skills/x`). */
  target: string;
  /** The pack that offers the switch. */
  by: string;
  /** The pack's own reason — what `status` prints. */
  because: string;
}

/**
 * What installs, after the repository's confirmations are applied (D71).
 *
 * @remarks
 * One answer computed in one place, because `sync`, `status`'s update report and `analyze`'s
 * projection all ask it — and three copies of "which core rows are off" are three chances to
 * disagree about the always-loaded doctrine.
 */
export interface Selection {
  files: CanonFile[];
  /** Confirmed in the manifest and offered by a selected pack: these rows do not install. */
  switchedOff: CoreSwitch[];
  /** Offered by a selected pack and not confirmed: the row stays installed, and this is reported. */
  offers: CoreSwitch[];
}

/** The canon as read from disk. */
export interface Canon {
  version: string;
  root: string;
  packs: Map<string, Pack>;
}

/**
 * What a harness expects on disk. **The tier is the LOCATION** — a directory for the tiers a harness
 * reaches for, and a marked REGION in a shared file for the one it loads without being asked
 * (D7 as amended by D59).
 *
 * @remarks
 * `.claude/rules/` was read by exactly one of the three harnesses this family drives, so the
 * always-loaded tier lives where all three look. That makes it the one tier whose location is not
 * harness-specific at all; what stays harness-specific is the POINTER, below.
 */
export interface HarnessTier {
  /** Where this tier's files live under the target, for a tier that is a directory. */
  dir?: string;
  /** Where this tier lives when it is a span inside a file the repository owns (D59). */
  region?: {
    /** The file, relative to the repository root — NOT to the target directory. */
    file: string;
    /** The region's name, which is what its markers carry. */
    name: string;
  };
  /** True for the tier the harness loads into every session without being asked. */
  alwaysLoaded: boolean;
  /** Present for a tier whose unit is a directory rather than a file, e.g. skills. */
  entryFile?: string;
  /** Frontmatter fields the harness itself requires of that tier. */
  frontmatter?: readonly string[];
}

export interface Harness {
  id: string;
  name: string;
  supported: boolean;
  /** Files whose presence says a repository is set up for this harness. */
  detect: readonly string[];
  defaultTarget: string;
  /**
   * The file this harness reads that others do not, and what it must import so the tier reaches it
   * anyway. Absent for a harness that reads the tier's own file directly (D59).
   */
  pointer?: { file: string; imports: string };
  tiers: Record<string, HarnessTier>;
  /** Where the provenance line goes — under the frontmatter, because it is only frontmatter at byte 0 (D14). */
  headerPlacement: 'top' | 'below-frontmatter';
  /**
   * A tier copied to a second root for a harness that reads only there (D117 §3.2): where a reference
   * layout links a folder, Daoris writes files, since a checkout without links holds a link as text.
   * The descriptor's data, so the day that harness reads the target itself, the entry goes and `sync`
   * retires every mirror.
   */
  mirror?: {
    /** The tier copied, by its canon name. */
    tier: string;
    /** Where the copies go, relative to the repository root. */
    root: string;
    /** The harness the copies are for, as a reader names it. */
    reader: string;
    /** Folders inside each unit that are never copied: per-agent metadata another agent writes. */
    skip: readonly string[];
  };
  /**
   * The root a repository on the older layout keeps its on-demand tiers in, so the documents left
   * there can be named with the move that fixes them (D117 §5.2).
   */
  formerly?: string;
}

/** One file of a mirror, as `sync` would write it and the lock records it (D117 §3.2). */
export interface MirrorEntry {
  /** The copy, relative to the repository root. */
  path: string;
  /** The source it copies, relative to the repository root. */
  of: string;
  sha256: string;
}

/** What `sync` decided about one mirror file. */
export interface MirrorWrite extends MirrorEntry {
  /** Text for a `SKILL.md` (it carries the mirror header), bytes for every other file. */
  content: string | Buffer;
  state: 'create' | 'update' | 'unchanged';
}

/** The mirror table of D117 §5.4, decided. */
export interface MirrorPlan {
  writes: MirrorWrite[];
  /** In the lock, as the lock, source gone: deleted. */
  retire: string[];
  /** In the lock, absent, source gone: the entry is dropped. */
  drop: string[];
  /** In the lock and edited here. `sourceEdited` when its canonical source drifted too. */
  edited: { path: string; of: string; canonical: boolean; sourceEdited: boolean }[];
  /** Not in the lock and not what would be written: the repository's own file at a mirror path. */
  collisions: string[];
  /** Edited, and the skill it mirrors went: nothing can receive the edit. */
  editedGone: string[];
}

/** The pointer table of D117 §5.4, decided: each room's `CLAUDE.md`. */
export interface RoomPlan {
  pointers: { room: string; path: string; state: 'create' | 'append' | 'unchanged'; content: string | null }[];
  /** Undeclared rooms whose region comes out; `content` null when the region was all of the file. */
  unpoint: { room: string; path: string; content: string | null }[];
  /** Undeclared rooms with no region: only the lock forgets them. */
  drop: string[];
  /** Declared with no instructions of their own: refused. */
  missing: string[];
  /** What the lock records once the sync lands. */
  records: string[];
}

/** A path Daoris would write that is a link, or a link checked out as text (D117 §5.4). */
export interface LinkProblem {
  path: string;
  kind: 'link' | 'text' | 'file';
}

/** One canonical document moving from the lock's root to the manifest's (D117 §5.4). */
export interface Move {
  target: string;
  /** Both relative to the repository root. */
  from: string;
  to: string;
}

/** A harness this repository shows a sign of, and the files that said so. */
export interface DetectedHarness {
  id: string;
  name: string;
  /** False for a harness whose signals are known but whose layout Daoris does not generate (D23). */
  supported: boolean;
  evidence: string[];
}

/** What `check` found, and the numbers it reports either way. */
export interface DriftReport {
  drifted: string[];
  missing: string[];
  stalePacks: string[];
  coreBytes: number;
  overBudget: boolean;
  indexStale: boolean;
  /** The core rows the lock says are off, and by which pack (D71) — named on every run. */
  switchedOff: { target: string; by: string }[];
  /** Rows the manifest and the lock disagree about: a fact, so it fails like a stale pack. */
  staleSwitches: string[];
  /** The manifest names a layout the files were not written under: a move `sync` has not made yet. */
  staleLayout: string | null;
  /** Mirrors edited here, each with its source (D117 §3.3). */
  mirrorsDrifted: { path: string; of: string }[];
  /** Mirrors the lock records and the disk lacks, or a source has and the lock does not. */
  mirrorsMissing: string[];
  /** Mirrors as the lock says, whose source has moved on since. */
  mirrorsBehind: string[];
  /** Declared rooms with no `AGENTS.md`. */
  roomsWithoutInstructions: string[];
  /** Declared rooms whose `CLAUDE.md` does not import it. */
  roomPointersMissing: string[];
  /** Paths Daoris writes that are a link, or a link held as text. */
  links: LinkProblem[];
  /** The root instruction file's whole size — reported against the smallest limit measured, never failed on. */
  agentsBytes: number;
  /** The repository's own documents in a tier the index no longer lists, with the move that fixes each. */
  unlisted: { path: string; move: string }[];
  /** The repository's own skills under the mirror root, read by that harness alone. */
  readAlone: string[];
  ok: boolean;
}

/**
 * What a repository is, and what it can usefully be asked for.
 *
 * This is the registration a repository makes with the family: the service reads it while indexing, so
 * an agent elsewhere can discover who exists, what each one owns, and what kind of quest is worth
 * addressing to it. Without it, publishing a quest is guessing what the other side does — which is the
 * same "the knowledge does not travel" problem the whole arrangement exists to solve.
 *
 * Nouns only, like everything else in the manifest (D26): what this repository IS, never a command.
 */
export interface Domain {
  /** One line: what this repository is, for someone who has never opened it. */
  summary: string;
  /** The areas it owns. A change in one of these belongs here rather than anywhere else. */
  owns: string[];
  /** Kinds of quest it welcomes. Guidance for the asker, not a contract. */
  accepts: string[];
  /**
   * The repositories it depends on, by the names the registry knows them by (D91). Optional: absent
   * says nothing is declared. Read through `usesOf`, never directly.
   */
  uses?: string[];
}

/** `daoris.json` — inert data, deliberately (D26). */
/**
 * What may leave this machine for a remote deployment (D47 §4). It lives in the MANIFEST — tracked
 * and reviewed — because disclosure is the repository's call, not one person's local toggle. Absence
 * is the default and means local: the cost of the wrong default is asymmetric, since over-sharing is
 * a disclosure and under-sharing is an inconvenience (D21).
 */
export interface RemoteDeclaration {
  /** Join a remote: the registration, quests and session records become visible to it. */
  join: boolean;
  /** Feed indexed knowledge content too. Meaningless without `join`, and refused without it. */
  knowledge: boolean;
}

export interface Manifest {
  source: string;
  packs: string[];
  harness: string;
  target: string;
  coreBudgetBytes: number;
  /** Absent until a repository registers itself; a quest can still be addressed, less usefully. */
  domain?: Domain;
  /** Absent until a repository opts into a remote deployment; silence means local (D47 §4). */
  remote?: RemoteDeclaration;
  /**
   * The core rows this repository confirms a selected pack may switch off: target → the pack
   * (D71). A pack's offer does nothing until it is named here, and a row named here that no
   * selected pack offers is refused — a repository alone still cannot drop core (D4).
   */
  switchedOff?: Record<string, string>;
  /**
   * Folders with an `AGENTS.md` of their own (D117 §2.2): declared, never found, since a walk of the
   * tree meets build output and worktrees. Relative to the repository, with forward slashes; empty
   * when none are declared.
   */
  rooms: string[];
  /** Resolved at read time so an unknown name fails at the edge, naming what exists. */
  harnessDescriptor: Harness;
}

/** One row of `daoris.lock`: what was written, from where, and what it hashed to. */
export interface LockEntry {
  pack: string;
  source: string;
  target: string;
  canonVersion: string;
  sha256: string;
  /**
   * Present when this entry is a **span inside a file** rather than a file of its own (D59): the
   * file, relative to the repository root.
   *
   * @remarks
   * `target` stays the canonical identity either way — `rules/sensitive-info.md` — because that is
   * what `upstream` names, what the canon calls it, and what a retirement matches on. What changes
   * is where to look and what to hash: the rule's BODY inside the region, not a file's whole bytes.
   */
  in?: string;
}

/**
 * `daoris.lock` — the authority. Anything absent from it is invisible to the tool, which is what
 * makes a repository's own files safe (D5).
 */
export interface Lock {
  /** Stamped by the writer, so a caller hands over entries and provenance without inventing it. */
  version?: number;
  canonVersion: string;
  source: string;
  entries: LockEntry[];
  /**
   * The core rows switched off here, and by which pack (D71) — recorded so the offline `check` can
   * name them without reading any pack. Absent when nothing is off, so an ordinary lock is unchanged.
   */
  switchedOff?: { target: string; by: string }[];
  /**
   * The descriptor and root the entries were written under (D117 §5.1). Absent means `claude-code`
   * and `.claude`, the only layout written before, so a lock on that layout is unchanged byte for byte.
   * 🔴 The lock, not the manifest, says where the files are: between a manifest's flip and the sync
   * that moves them, the manifest names the new root and the files are still at the old.
   */
  harness?: string;
  target?: string;
  /** Every mirror file `sync` wrote, and what it hashed to (D117 §3.2). Absent when there are none. */
  mirrors?: MirrorEntry[];
  /** The rooms whose pointers `sync` keeps, so a room taken out of the manifest loses its pointer. */
  rooms?: string[];
}

/**
 * The part of the lock most readers need.
 *
 * `canonVersion` and `source` are provenance the writer stamps; everything that merely asks "what did
 * Daoris put here" wants only the entries' targets, and saying so keeps those callers from having to
 * invent provenance they do not have. A full `Lock` satisfies this, and so does a test fixture.
 */
export type LockLike = { entries: readonly Pick<LockEntry, 'target'>[] };

/** What `sync` decided about one file, before anything is written. */
export interface PlannedWrite extends CanonFile {
  content: string;
  sha256: string;
  state: 'create' | 'update' | 'unchanged';
  /**
   * Present when this write is a **span inside a file** rather than a file of its own (D59): the
   * file, relative to the repository root. `content` is then the rule's body, and the whole region is
   * assembled from every write that names the same file.
   */
  in?: string;
  /**
   * The frontmatter a span dropped, captured where the canon was in hand.
   *
   * @remarks
   * The roster's rows are built from `applies_when` and `enforces`, and a span carries the body
   * alone — so without this the region would have to re-read the canon while assembling, which is
   * the one place it is not holding it.
   */
  meta?: Record<string, string>;
}

/** A canonical file that moved rather than being retired and re-added. */
export interface Rename {
  from: string;
  to: string;
}

/**
 * The whole of `sync`'s decision, separated from applying it so a plan can be printed or asserted
 * without touching disk.
 */
export interface SyncPlan {
  writes: PlannedWrite[];
  deletes: string[];
  /**
   * The deletes that were SPANS (D59): they leave with the region's rewrite, and there is no file of
   * Daoris's at their old path — a file there is the repository's own, and is never removed.
   */
  leavesRegion?: string[];
  /** In the lock and edited here — an improvement that may want promoting, not a mistake (D13). */
  drifted: string[];
  /** Not in the lock: the repository wrote it before adopting, and overwriting would destroy it (D12). */
  collisions: string[];
  renames: Rename[];
  /** Retirements the repository has edited — the worst moment to lose work, so they refuse. */
  editedRetirements: string[];
  /** Core rows a confirmed switch takes out (D71). Their files are in `deletes`; this names why. */
  switchedOff: CoreSwitch[];
  /** Offers nobody confirmed: the row stays, and `sync` says so. */
  offers: CoreSwitch[];
  /**
   * Switched-off files this repository edited (D71). They refuse like drift rather than like an
   * edited retirement: the canonical file still exists, so `upstream` can still save the edit.
   */
  editedSwitchedOff: string[];
  /**
   * The layout the lock was written under, and the one the manifest names (D117 §5.1). A move when
   * the two targets differ: files are read and deleted at `from`, written at `to`.
   */
  layout: { from: { harness: string; target: string }; to: { harness: string; target: string } };
  /** Canonical documents whose old file goes once the new one is written: moved, or a move finished. */
  moves: Move[];
  /** The repository's own file where a move would write (repository-relative), and what was moving there. */
  moveCollisions: { from: string | null; to: string }[];
  /** The repository's own documents in an old on-demand tier: each refuses the move (D117 §5.4). */
  leftBehind: { path: string; move: string }[];
  /** The same own document under both roots: which one is meant is the repository's call. */
  bothRoots: { old: string; neu: string }[];
  /** Own always-loaded files in an old `rules/` directory: reported, never refused. */
  keptRules: string[];
  mirrors: MirrorPlan;
  rooms: RoomPlan;
  links: LinkProblem[];
}

/** One canon changelog section: which version, and what it said. */
export interface CanonNote {
  version: string;
  body: string;
}

/** One document a repository already had, and what it costs. */
export interface SurveyedDoc {
  target: string;
  bytes: number;
}

/** What a repository already carries, before Daoris touches it. */
export interface Survey {
  rules: SurveyedDoc[];
  knowledge: SurveyedDoc[];
  skills: SurveyedDoc[];
}

/** A pack the repository shows evidence for — a hint, never a recommendation. */
export interface PackSuggestion {
  name: string;
  why: string;
  evidence: string[];
}

/** A local document that looks like a canonical one under another name. */
export interface Twin {
  local: string;
  canonical: string;
  score: number;
}

/** What adopting would do here. Writes nothing (see `analyze`). */
export interface AnalysisReport {
  target: string;
  harness: { detected: DetectedHarness[]; supported: DetectedHarness[]; others: DetectedHarness[] };
  contract: string[];
  existing: Survey;
  suggested: PackSuggestion[];
  /** Paths the repository owns that the canon would claim — sync refuses until each is resolved. */
  collisions: string[];
  /** In the lock already, so a difference is an update to install rather than a collision (D12). */
  updates: string[];
  twins: Twin[];
  budget: { current: number; projected: number; limit: number };
  /** Core rows a chosen pack would switch off, awaiting the manifest's confirmation (D71). */
  offers: CoreSwitch[];
  /** Core rows the manifest already confirms off — out of every projection above. */
  switchedOff: CoreSwitch[];
}

/**
 * Everything a command is handed. The dispatcher passes all four; each command's own signature narrows
 * to what it reads, which is documentation that cannot go stale.
 */
export interface CommandArgs {
  root: string;
  argv: string[];
  write: (line: string) => void;
  packageRoot: string;
}

/**
 * One row of the dispatcher's table (MOD7), exported by its module under `cli/` as `command`. It lives
 * here rather than in the dispatcher so that no row has to import the dispatcher (`cli.ts` says why).
 */
export interface CliCommand {
  /** The verb a person types, and the module's file name under `cli/`. */
  readonly name: string;
  /**
   * Offline and gate-safe, or opt-in (D35, D50). A doctrine command's module is walked by the test
   * that holds the network and the spawning primitives away from every gate.
   */
  readonly kind: 'doctrine' | 'management';
  /** Its lines in `daoris --help`, exactly as printed, the first naming the verb. */
  readonly usage: readonly string[];
  /** Its lines under `Options:`, for a flag it owns. They print in the table's order. */
  readonly options?: readonly string[];
  /** Verbs it was once called. Each says where it went, once, and fails like any unknown command (AGT1). */
  readonly formerly?: readonly string[];
  readonly run: (args: CommandArgs) => ExitCode | Promise<ExitCode>;
}

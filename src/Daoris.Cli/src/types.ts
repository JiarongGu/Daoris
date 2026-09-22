/**
 * The shapes this tool has always had, written down.
 *
 * They were implicit while the source was JavaScript — described accurately in comments, agreed on by
 * every module, and checked by nothing. Naming them is most of what the TypeScript migration buys:
 * the lock, the manifest and the plan are the three things `sync` reasons about at once (D19), and
 * that state space is exactly where a wrong shape would be expensive.
 */

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
  files: CanonFile[];
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
  /** In the lock and edited here — an improvement that may want promoting, not a mistake (D13). */
  drifted: string[];
  /** Not in the lock: the repository wrote it before adopting, and overwriting would destroy it (D12). */
  collisions: string[];
  renames: Rename[];
  /** Retirements the repository has edited — the worst moment to lose work, so they refuse. */
  editedRetirements: string[];
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

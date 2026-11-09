// The files section's reading as its molecules draw it; the bridge's `FILES` answer is one, and its presses are these.

/** The layers a rule can come from, in the order they are read; the floor is read after all of them. */
export type FilesLayer = 'found' | 'repository' | 'machine' | 'person' | 'floor';

/** The kinds of file a rule names. */
export type FilesKind = 'dependency' | 'intermediate' | 'output' | 'cache' | 'local' | 'scratch' | 'repository' | 'unknown';

export const FILES_KINDS: readonly FilesKind[] = ['dependency', 'intermediate', 'output', 'cache', 'local', 'scratch', 'repository', 'unknown'];

/** What a layer below the winning one said of a path. */
export type FilesBelow = { layer: FilesLayer; kind: FilesKind };

/** One path git does not manage: its effective rule, the layer that decided, what the walk counted and what the layers below said. */
export type FilesFile = {
  path: string;
  /** Absent where git tracks the path. */
  kind?: FilesKind | null;
  /** Absent where no rule names the path. */
  layer?: FilesLayer | null;
  floor?: string | null;
  copy?: boolean;
  idle?: string | null;
  files?: number | null;
  bytes?: number | null;
  more?: boolean;
  evidence?: string[];
  below?: FilesBelow[];
};

/** An act the rules would make in a tree and the person's answer to it. */
export type FilesAct = { act: string; path: string; verdict: 'made' | 'denied' | 'held' };

export type FilesAllow = { rule: string; by?: string | null };

/** One scope of the person's permissions. */
export type FilesPermissions = { scope: 'machine' | 'repository'; deny: string[]; ask: string[]; allow: FilesAllow[] };

/** One rule of the person's own. */
export type FilesRule = { scope: 'machine' | 'repository'; kind: FilesKind; paths: string[]; copy?: boolean | null };

/** A path of the person's file git's syntax cannot state, read as written, and its two git wordings. */
export type FilesKept = { scope: string; kind: FilesKind; path: string; whole: string[]; files: string[] };

/** `FILES`'s answer for a repository, never a machine path. */
export type FilesLook = {
  repository: string;
  commit: string;
  foundCommit?: string | null;
  foundAt?: string | null;
  files: FilesFile[];
  acts: FilesAct[];
  permissions: FilesPermissions[];
  rules: FilesRule[];
  /** Why the person's file stands and does not read, where it does not. */
  unread?: string | null;
  problems: string[];
  kept?: FilesKept[];
  firstForm?: boolean;
};

/** The presses of the section: read again, the person's rules set and removed, a permission added or taken back. */
export type FilesPress =
  | { act: 'read' }
  | { act: 'set'; kind: FilesKind; paths: string[]; copy?: boolean; machine?: boolean }
  | { act: 'remove'; paths: string[]; machine?: boolean }
  | { act: 'settle'; wordings: { scope: string; path: string; whole: boolean }[] }
  | { act: 'permit'; change: 'add' | 'remove'; list: 'deny' | 'ask' | 'allow'; rule: string; machine?: boolean };

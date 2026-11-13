/**
 * The types of `fsx.mjs`, for the typed modules under `tools/merge/` that import it. Every export of the script is declared
 * here and nothing else is: `tools/merge/declarations.test.ts` holds the two to the same names.
 */
import type {
  ExecFileSyncOptions, ExecFileSyncOptionsWithStringEncoding, SpawnSyncOptions, SpawnSyncOptionsWithStringEncoding, SpawnSyncReturns,
} from 'node:child_process';
import type { renameSync } from 'node:fs';

type Environment = NodeJS.ProcessEnv;
type Held = { tries?: number; waitMs?: number; rename?: typeof renameSync };

/** The git a tool starts and the environment it starts it with. */
export declare function gitStart(env?: Environment): { file: string; env: Environment };
export declare function spawnGit(args: readonly string[], options: SpawnSyncOptionsWithStringEncoding): SpawnSyncReturns<string>;
export declare function spawnGit(args: readonly string[], options?: SpawnSyncOptions): SpawnSyncReturns<string | Buffer>;
export declare function execGit(args: readonly string[], options: ExecFileSyncOptionsWithStringEncoding): string;
export declare function execGit(args: readonly string[], options?: ExecFileSyncOptions): string | Buffer;
export declare function isMain(url: string): boolean;
export declare function repositoryFiles(root: string, options?: { env?: Environment }): string[];
export declare function renameHeld(from: string, to: string, options?: Held): void;
export declare function writeAtomic(file: string, data: string | Uint8Array, options?: Held): void;
export declare function stagedTree(cwd: string, options?: { env?: Environment }): string;
export declare function copyTree(from: string, to: string): void;
export declare function treeDiff(left: string, right: string): string[];

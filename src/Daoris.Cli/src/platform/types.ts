/**
 * The command shapes every module reads: what a command is handed and one row of the dispatcher's table (MOD7).
 * The doctrine's records (the canon, the lock, the manifest, the plans and the reports) are in `../doctrine/records.ts`.
 */
import type { ExitCode } from './errors.ts';

/**
 * Everything a command is handed. The dispatcher passes all five; each command's own signature narrows
 * to what it reads, which is documentation that cannot go stale.
 */
export interface CommandArgs {
  root: string;
  argv: string[];
  write: (line: string) => void;
  packageRoot: string;
  /**
   * Daoris's driver program, which runs a management family's verbs (PUSH1f2a, D194 point 1): handed in by the dispatcher, as a family
   * is handed its door, so the dispatcher's test hands a stand-in. Absent, a family that hands a verb on uses the real one.
   */
  driverProgram?: (family: string, words: readonly string[]) => ExitCode;
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

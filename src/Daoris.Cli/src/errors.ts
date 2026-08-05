/**
 * An expected failure, carrying the process exit code it should produce.
 *
 * Exit codes are part of the contract, because `check` runs inside build gates:
 * 0 clean, 1 policy failure (drift, stale, over budget), 2 tool error.
 */
export type ExitCode = 0 | 1 | 2;

export class DaorisError extends Error {
  readonly exitCode: ExitCode;

  constructor(message: string, exitCode: ExitCode = 2) {
    super(message);
    this.name = 'DaorisError';
    this.exitCode = exitCode;
  }
}

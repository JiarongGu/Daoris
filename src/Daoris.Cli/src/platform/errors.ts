/**
 * An expected failure, carrying the process exit code it should produce.
 *
 * Exit codes are part of the contract, because `check` runs inside build gates:
 * 0 clean, 1 policy failure (drift, stale, missing), 2 tool error. The core budget is deliberately
 * NOT in that list: it reports and never fails (D54).
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

/**
 * A refusal by one named check (TOOLS4, D121 §3.6): an archive or a download that failed it. The check is the
 * code both twins spell (`hash`, `outside`, `truncated`, …), so a screen and a terminal name the same failure;
 * the driver's `ToolRefusal` carries the same codes. Its exit code is a DaorisError's, and the verb that knows a
 * check is a refusal rather than a tool error says 1.
 */
export class RefusalError extends DaorisError {
  readonly check: string;

  constructor(check: string, message: string, exitCode: ExitCode = 2) {
    super(message, exitCode);
    this.name = 'RefusalError';
    this.check = check;
  }
}

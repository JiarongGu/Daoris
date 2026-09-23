/**
 * The rehearsal harness both gates share: the transcript, the check/section vocabulary, and the
 * capturing command runner. Extracted because the two rehearsals carried byte-identical copies that
 * had already begun to drift (one had the kill-timeout, one had `process.exit` where the other set
 * `exitCode`) — and a harness that diverges between gates makes the same failure read differently
 * depending on which gate caught it.
 */
import { execSync } from 'node:child_process';
import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

/**
 * Every run leaves a transcript under `_fixtures/rehearsal-logs/`, outside anything a passing run
 * deletes, so a failure is evidence rather than a memory (REH1). Console output is mirrored into it;
 * `beforeExit` runs first on the way out — the family rehearsal stops its hosts there.
 */
export function openTranscript(repoRoot, name, { beforeExit } = {}) {
  const transcript = [`${name} rehearsal — ${new Date().toISOString()} — node ${process.version}`];
  const emit = console.log.bind(console);
  console.log = (...args) => {
    const line = args.join(' ');
    transcript.push(line);
    emit(line);
  };
  process.on('exit', (code) => {
    beforeExit?.();
    transcript.push(`\nexit ${code}`);
    const logDir = join(repoRoot, '_fixtures', 'rehearsal-logs');
    mkdirSync(logDir, { recursive: true });
    const logPath = join(logDir, `${name}-${new Date().toISOString().replace(/[:.]/g, '-')}.log`);
    writeFileSync(logPath, `${transcript.join('\n')}\n`);
    emit(`  transcript: ${logPath}`);
  });
}

/** The check/section vocabulary, with the counters the final summary reads. */
export function makeChecker() {
  const totals = { checks: 0, failures: 0 };
  return {
    totals,
    check(label, condition, detail = '') {
      totals.checks += 1;
      if (condition) {
        console.log(`  ok    ${label}`);
      } else {
        totals.failures += 1;
        console.log(`  FAIL  ${label}${detail ? `\n          ${detail}` : ''}`);
      }
    },
    section(title) {
      console.log(`\n${title}`);
    },
  };
}

/**
 * Run a command, capturing output and exit code — never throwing, so a failure is a FAIL line. A
 * timeout kills the child and returns its partial output, so a hung child is a captured FAIL rather
 * than a frozen gate.
 */
export function capture(command, cwd, { env = {}, timeout = 0, input } = {}) {
  try {
    const out = execSync(command, {
      cwd,
      encoding: 'utf8',
      // stdin stays closed unless a check hands the command something to read (`agent key`, AGT3).
      stdio: [input === undefined ? 'ignore' : 'pipe', 'pipe', 'pipe'],
      ...(input === undefined ? {} : { input }),
      env: { ...process.env, ...env },
      ...(timeout ? { timeout, killSignal: 'SIGKILL' } : {}),
    });
    return { code: 0, out };
  } catch (error) {
    const timedOut = error.killed || error.signal === 'SIGKILL';
    return {
      code: error.status ?? -1,
      out: `${error.stdout ?? ''}${error.stderr ?? ''}${timedOut ? '\n[killed: exceeded the timeout]' : ''}`,
    };
  }
}

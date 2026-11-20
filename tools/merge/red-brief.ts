/**
 * The red brief: what a failing merge, preflight or watch of main writes once, so the steward and the fixer read
 * one file and never the logs. For each failing gate: the failing tests, or the first error where no test is named; their file
 * and line; the first assertion or error lines, kept short; and whether it fails on main too, which the caller checks by
 * running only that test's selection, or the gate, on main's tree (`mark`).
 *
 * The tests are read from the gate's trx (`readTrx`), else from its log: vitest's `FAIL` blocks, `node --test`'s `✖` list, `dotnet test`'s
 * `Failed` lines, a `not ok` or `✖` line, or the first error line (never a pass line or a warning) with the place it names.
 */
import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { BLOCKING } from './reach.ts';
// @ts-expect-error — untyped workspace tooling, read through the seam its own tests use
import * as untyped from '../test-failures.mjs';

/**
 * Whether a failure of `gate` starts nothing more, so every red is found in one round: under `--keep-going` (`true`) none does;
 * under `'blocking'`, a merge's and the queue's default, only a blocking light gate's, since nothing past a broken index or
 * census reads true; `false` every one.
 */
export type KeepGoing = boolean | 'blocking';
export const failsFast = (keepGoing: KeepGoing, gate: string): boolean => keepGoing === false || (keepGoing === 'blocking' && BLOCKING.includes(gate));

/** Whether a failure fails on main too: `main` the same way, `other` another way, `branch` not at all, `unchecked` not run there. */
export type Mark = { on: 'main' | 'other' | 'branch' | 'unchecked'; why: string };
export type BriefFailure = { test: string | null; file: string | null; line: number | null; lines: string[]; mark?: Mark };
export type BriefGate = { gate: string; log: string | null; failures: BriefFailure[]; mark?: Mark; note?: string };

/** The brief's file in a run's folder: `local/scratch/merge-<branch>/brief.md` for a merge or a preflight. */
export const BRIEF_FILE = 'brief.md';
/** At most this many failures a gate, and lines a failure, in the brief: the log holds the rest. */
export const BRIEF_FAILURES = 12;
export const BRIEF_LINES = 4;
const WIDTH = 160;
const { failingTestLine, notAFailure, noneFailed, redAloneLine, testOfLine } = untyped as { failingTestLine: (line: string) => boolean; notAFailure: (line: string) => boolean; noneFailed: (exit?: number | null) => string; redAloneLine: (line: string) => boolean; testOfLine: (line: string) => string | null };

// eslint-disable-next-line no-control-regex
const plain = (text: string): string => text.replace(/\x1b\[[0-9;]*[A-Za-z]/g, '').replace(/\r\n?/g, '\n');
const clip = (line: string): string => {
  const trimmed = line.trim();
  return trimmed.length > WIDTH ? `${trimmed.slice(0, WIDTH - 1)}…` : trimmed;
};
const slashed = (file: string): string => file.replaceAll('\\', '/');

/** The first `file:line[:col]`, `file(line,col)` or `in file:line N` a line names, its file as written. */
export function placeIn(line: string): { file: string; line: number } | null {
  const dotnet = /\bin (\S.*?):line (\d+)/.exec(line);
  if (dotnet) return { file: slashed(dotnet[1] as string), line: Number(dotnet[2]) };
  const paren = /([\w./\\@-]+\.[A-Za-z]{1,5})\((\d+),\d+\)/.exec(line);
  if (paren) return { file: slashed(paren[1] as string), line: Number(paren[2]) };
  const colon = /([\w./\\@-]+\.[A-Za-z]{1,5}):(\d+)(?::\d+)?/.exec(line);
  return colon ? { file: slashed(colon[1] as string).replace(/^file:\/+/, ''), line: Number(colon[2]) } : null;
}

const ERROR = /\b(?:\w*Error|error|ERR!|Exception|FAIL(?:ED)?|failed|Assert\.\w+)\b|✖|×|expected /;
const NOISE = /^npm (?:error|ERR!) (?:Lifecycle|code|path|workspace|location|command failed|A complete log)|^npm (?:error|ERR!)\s*$/;

/** The error lines from `start`: the first `count` non-empty lines that are not a stack frame or npm's own trailer. */
function errorLines(lines: string[], start: number, count = BRIEF_LINES): string[] {
  const out: string[] = [];
  for (let i = start; i < lines.length && out.length < count; i += 1) {
    const line = lines[i] as string;
    if (!line.trim() || /^\s*(?:at |❯ |⎯)/.test(line) || NOISE.test(line.trim())) continue;
    out.push(clip(line));
  }
  return out;
}

/** The first place the lines after `start` name, within `span` lines. */
function placeAfter(lines: string[], start: number, span = 40): { file: string; line: number } | null {
  for (let i = start; i < Math.min(lines.length, start + span); i += 1) {
    const found = placeIn(lines[i] as string);
    if (found && !/node_modules|node:internal/.test(found.file)) return found;
  }
  return null;
}

/** vitest's failures: each `FAIL <file> > <title>` block, its first error lines and the first place in its file. */
function vitestFailures(lines: string[]): BriefFailure[] {
  const out: BriefFailure[] = [];
  lines.forEach((line, i) => {
    const found = /^\s*FAIL\s+(\S+\.(?:test|spec|e2e)\.[cm]?[jt]sx?)\s+>\s+(.+?)\s*$/.exec(line);
    if (!found) return;
    const file = slashed(found[1] as string);
    const test = `${file} > ${(found[2] as string).replace(/\s+\d+ms$/, '')}`;
    if (out.some((each) => each.test === test)) return;
    let at: { file: string; line: number } | null = null;
    for (let j = i + 1; j < Math.min(lines.length, i + 60) && !/^\s*FAIL\s/.test(lines[j] as string); j += 1) {
      const place = placeIn(lines[j] as string);
      if (place && place.file.endsWith(file.split('/').at(-1) as string)) {
        at = place;
        break;
      }
    }
    out.push({ test, file, line: at?.line ?? null, lines: errorLines(lines, i + 1) });
  });
  return out;
}

/** `node --test`'s failures: its `test at <file>:<line>` then `✖ <title>` list, with the error lines under each. */
function nodeFailures(lines: string[]): BriefFailure[] {
  const out: BriefFailure[] = [];
  lines.forEach((line, i) => {
    const at = /^test at (.+?):(\d+):\d+\s*$/.exec(line);
    const title = at && /^✖ (.*?)(?: \(\d+(?:\.\d+)?ms\))?\s*$/.exec(lines[i + 1] ?? '');
    if (!at || !title) return;
    const file = slashed(at[1] as string);
    const test = `${file} > ${title[1] as string}`;
    if (out.some((each) => each.test === test)) return;
    out.push({ test, file, line: Number(at[2]), lines: errorLines(lines, i + 2) });
  });
  return out;
}

/** `dotnet test`'s failures as its console says them: `Failed <test> [time]`, its message and the first frame with a line. */
function dotnetFailures(lines: string[]): BriefFailure[] {
  const out: BriefFailure[] = [];
  lines.forEach((line, i) => {
    const found = /^\s+Failed (\S.*?) \[[^\]]*\]\s*$/.exec(line);
    if (!found || out.some((each) => each.test === found[1])) return;
    const place = placeAfter(lines, i + 1, 30);
    out.push({ test: found[1] as string, file: place?.file ?? null, line: place?.line ?? null, lines: errorLines(lines.slice(i + 1).filter((each) => !/^\s*(?:Error Message|Stack Trace):\s*$/.test(each)), 0) });
  });
  return out;
}

/** The failures a gate's log names, or its first error with its place; none named says the run ended without a failing test, with its exit. */
export function failuresInLog(text: string, exit: number | null = null): BriefFailure[] {
  const lines = plain(text).split('\n');
  for (const read of [vitestFailures, nodeFailures, dotnetFailures]) {
    const found = read(lines);
    if (found.length) return found;
  }
  const red = lines.findIndex((line) => redAloneLine(line));
  const crossed = lines.findIndex((line) => failingTestLine(line));
  const named = red >= 0 ? red : crossed;
  const first = named >= 0 ? named : lines.findIndex((line) => ERROR.test(line) && !NOISE.test(line.trim()) && !notAFailure(line));
  if (first < 0) {
    const tail = lines.filter((line) => line.trim()).slice(-BRIEF_LINES).map(clip);
    return [{ test: null, file: null, line: null, lines: [noneFailed(exit), ...tail] }];
  }
  const place = placeIn(lines[first] as string) ?? placeAfter(lines, first + 1, 8);
  const test = first === crossed && first !== red ? testOfLine(lines[first] as string) : null;
  return [{ test, file: place?.file ?? null, line: place?.line ?? null, lines: errorLines(lines, first) }];
}

/** A failure the trx names (`readTrx`'s shape), as the brief says it: the first frame with a line is its place. */
export function trxFailure(failure: { test: string; message?: string; frames?: { file?: string | null; line?: number | null }[] }): BriefFailure {
  const frame = (failure.frames ?? []).find((each) => each.file && each.line);
  return { test: failure.test, file: frame?.file ? slashed(frame.file) : null, line: frame?.line ?? null, lines: (failure.message ?? '').split('\n').filter((line) => line.trim()).slice(0, BRIEF_LINES).map(clip) };
}

/**
 * A failure's selection run on main's tree, as the merge queue's probe answers it (`prefixRunner`): a pass is the branch's, a
 * failure with the same signature main's too, another failure main's another way, and a selection that cannot run is unchecked.
 */
export function markOfProbe(outcome: { result: string; signature?: unknown } | null, sameSignature: boolean, sha: string): Mark {
  if (!outcome) return { on: 'unchecked', why: 'its check on main did not run' };
  if (outcome.result === 'pass') return { on: 'branch', why: `passes on main (${sha})` };
  if (outcome.result === 'fail' && sameSignature) return { on: 'main', why: `fails on main too (${sha}), the same way` };
  if (outcome.result === 'fail') return { on: 'other', why: `fails on main too (${sha}), another way` };
  return { on: 'unchecked', why: `its check cannot run on main (${sha})` };
}

/** A failure the queue judged (`judgeFailure`), marked by its run on the candidate's base: main's tip when the candidate formed. */
export function markOfJudgement(judged: { outcome: string; why?: string; member?: number }, members: readonly { branch: string }[]): Mark {
  const why = judged.why ?? judged.outcome;
  if (judged.outcome === 'inherited' || (judged.outcome === 'stop' && ['base', 'expired'].includes(why))) return { on: 'main', why: `fails on main too: ${why}` };
  if (judged.outcome === 'pinned') return { on: 'branch', why: `the branch's: ${members[(judged.member ?? 1) - 1]?.branch ?? 'a member'} brings it; it passes on main` };
  return { on: 'unchecked', why: `${judged.outcome}: ${why}` };
}

const MARK_SAID: Record<Mark['on'], string> = { main: 'MAIN TOO', other: 'MAIN TOO, ANOTHER WAY', branch: 'BRANCH', unchecked: 'NOT CHECKED' };
const placeSaid = (failure: BriefFailure): string => (failure.file ? ` at \`${failure.file}${failure.line ? `:${failure.line}` : ''}\`` : '');

/** The brief's text: a heading, one line on what ran, then each failing gate with its failures, each marked. */
export function briefText(input: { title: string; ran: string; gates: readonly BriefGate[] }): string {
  const out = [`# ${input.title}`, '', input.ran, ''];
  const marks = input.gates.flatMap((gate) => (gate.mark ? [gate.mark] : gate.failures.flatMap((each) => (each.mark ? [each.mark] : []))));
  const count = (on: Mark['on']): number => marks.filter((mark) => mark.on === on).length;
  out.push(`Marks: ${count('branch')} the branch's, ${count('main') + count('other')} main's too, ${count('unchecked')} not checked.`, '');
  for (const gate of input.gates) {
    out.push(`## ${gate.gate}${gate.mark ? ` [${MARK_SAID[gate.mark.on]}: ${gate.mark.why}]` : ''}`, '');
    if (gate.note) out.push(gate.note, '');
    if (gate.log) out.push(`Log: \`${gate.log}\``, '');
    const shown = gate.failures.slice(0, BRIEF_FAILURES);
    for (const failure of shown) {
      out.push(`- ${failure.test ? `\`${failure.test}\`` : 'first error'}${placeSaid(failure)}${failure.mark ? ` [${MARK_SAID[failure.mark.on]}: ${failure.mark.why}]` : ''}`);
      for (const line of failure.lines) out.push(`    ${line}`);
    }
    if (gate.failures.length > shown.length) out.push(`- … ${gate.failures.length - shown.length} more in the log`);
    out.push('');
  }
  return `${out.join('\n').trimEnd()}\n`;
}

/** The brief written into the run's folder; answers its path. */
export function writeBrief(dir: string, text: string): string {
  mkdirSync(dir, { recursive: true });
  const file = join(dir, BRIEF_FILE);
  writeFileSync(file, text);
  return file;
}

/** A failed gate's result as the brief reads it. */
export type Failed = { gate: { name: string }; log?: string | null; trx?: string | null; cause?: string | null; code?: number | null };
/** A failure signed for the main check (`failuresOf`'s shape): a test from its trx, or the gate read whole. */
export type Signed = { names: 'test' | 'gate'; test: string; failure?: { test: string; message?: string; frames?: { file?: string | null; line?: number | null }[] } };

/**
 * Each failed gate as the brief says it. `signed` reads a result's failures as the queue signs them, `read` a file's text, and
 * `mark` checks one on main. A gate whose trx names tests is briefed and marked test by test; any other by its log, marked once,
 * by its check on main whole. A death of memory is the machine's, never checked on main.
 */
export async function briefGates(results: readonly Failed[], seams: {
  signed: (result: Failed) => Signed[]; read: (file: string) => string | null; mark: (failure: Signed, result: Failed) => Promise<Mark>; shown: (file: string) => string;
}): Promise<BriefGate[]> {
  const gates: BriefGate[] = [];
  for (const result of results) {
    const log = result.log ? seams.shown(result.log) : null;
    const text = result.log ? seams.read(result.log) ?? '' : '';
    if (result.cause === 'memory') {
      gates.push({ gate: result.gate.name, log, failures: failuresInLog(text, result.code ?? null), mark: { on: 'unchecked', why: "it died of memory: the machine's, not the change's" } });
      continue;
    }
    const signed = seams.signed(result);
    const tests = signed.filter((each) => each.names === 'test' && each.failure);
    if (tests.length) {
      const failures: BriefFailure[] = [];
      for (const each of tests) failures.push({ ...trxFailure(each.failure as NonNullable<Signed['failure']>), mark: await seams.mark(each, result) });
      gates.push({ gate: result.gate.name, log, failures });
      continue;
    }
    const whole = signed[0];
    gates.push({ gate: result.gate.name, log, failures: failuresInLog(text, result.code ?? null), ...(whole ? { mark: await seams.mark(whole, result) } : {}) });
  }
  return gates;
}
